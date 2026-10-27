param([switch]$Run)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet restore Maskot.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
New-Item -ItemType Directory -Force -Path artifacts | Out-Null
dotnet list Maskot.slnx package --vulnerable --include-transitive --format json | Set-Content -Encoding utf8 artifacts/dependency-audit.json
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$audit = Get-Content artifacts/dependency-audit.json -Raw | ConvertFrom-Json
foreach ($project in $audit.projects) {
    foreach ($framework in $project.frameworks) {
        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            if ($package.vulnerabilities) { throw "Dependency advisory found for $($package.id). See dependency-audit.json." }
        }
    }
}
dotnet test tests/Maskot.Core.Tests/Maskot.Core.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=core.trx' --results-directory artifacts/test-results
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet publish src/Maskot.Windows/Maskot.Windows.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/win-x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Copy-Item README.md, NOTICE.md, docs/TESTING.md, docs/WINDOWS-ACCEPTANCE.md artifacts/win-x64/
Copy-Item docs/MULAI-DI-SINI.txt artifacts/win-x64/
Copy-Item artifacts/dependency-audit.json artifacts/win-x64/
$runtimes = (Get-Content src/Maskot.Windows/bin/Release/net10.0-windows10.0.19041.0/win-x64/Maskot.runtimeconfig.json -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks
@{ commit = (git rev-parse HEAD); sdk = (dotnet --version); runtimes = $runtimes } | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 artifacts/win-x64/build-info.json
New-Item -ItemType Directory -Force -Path artifacts/win-x64/docs | Out-Null
Copy-Item docs/*.md artifacts/win-x64/docs/
$packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
$noticeFolder = 'artifacts/win-x64/notices'
New-Item -ItemType Directory -Force -Path $noticeFolder | Out-Null
foreach ($package in @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64')) {
    $runtime = $runtimes | Where-Object { ($_.name.ToLowerInvariant() + '.runtime.win-x64') -eq $package }
    if (-not $runtime) { throw "Published runtime version missing: $package" }
    $versionPath = Join-Path (Join-Path $packageCache $package) $runtime.version
    $licenses = Get-ChildItem $versionPath -File | Where-Object { $_.Name -match 'LICENSE|NOTICE' }
    if (-not $licenses) { throw "Runtime license missing: $package" }
    foreach ($license in $licenses) { Copy-Item $license.FullName (Join-Path $noticeFolder ($package + '-' + $license.Name)) }
}
Compress-Archive -Path artifacts/win-x64/* -DestinationPath artifacts/Maskot-win-x64.zip -Force
$archiveHash = (Get-FileHash artifacts/Maskot-win-x64.zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Encoding ascii -Path artifacts/Maskot-win-x64.zip.sha256 -Value "$archiveHash  Maskot-win-x64.zip"
Write-Output "Maskot-win-x64.zip SHA256: $archiveHash"
if ($Run) { Start-Process (Join-Path $PWD 'artifacts/win-x64/Maskot.exe') }
