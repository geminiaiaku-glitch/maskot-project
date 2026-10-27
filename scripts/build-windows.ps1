param([switch]$Run)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet restore Maskot.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test tests/Maskot.Core.Tests/Maskot.Core.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=core.trx' --results-directory artifacts/test-results
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet publish src/Maskot.Windows/Maskot.Windows.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/win-x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Copy-Item README.md, NOTICE.md, docs/TESTING.md, docs/WINDOWS-ACCEPTANCE.md artifacts/win-x64/
Copy-Item docs/MULAI-DI-SINI.txt artifacts/win-x64/
New-Item -ItemType Directory -Force -Path artifacts/win-x64/docs | Out-Null
Copy-Item docs/*.md artifacts/win-x64/docs/
$packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
$noticeFolder = 'artifacts/win-x64/notices'
New-Item -ItemType Directory -Force -Path $noticeFolder | Out-Null
foreach ($package in @('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64')) {
    $version = Get-ChildItem (Join-Path $packageCache $package) -Directory | Sort-Object { [version]$_.Name } | Select-Object -Last 1
    if (-not $version) { throw "Runtime package missing: $package" }
    $licenses = Get-ChildItem $version.FullName -File | Where-Object { $_.Name -match 'LICENSE|NOTICE' }
    if (-not $licenses) { throw "Runtime license missing: $package" }
    foreach ($license in $licenses) { Copy-Item $license.FullName (Join-Path $noticeFolder ($package + '-' + $license.Name)) }
}
Compress-Archive -Path artifacts/win-x64/* -DestinationPath artifacts/Maskot-win-x64.zip -Force
$archiveHash = (Get-FileHash artifacts/Maskot-win-x64.zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Encoding ascii -Path artifacts/Maskot-win-x64.zip.sha256 -Value "$archiveHash  Maskot-win-x64.zip"
Write-Output "Maskot-win-x64.zip SHA256: $archiveHash"
if ($Run) { Start-Process (Join-Path $PWD 'artifacts/win-x64/Maskot.exe') }
