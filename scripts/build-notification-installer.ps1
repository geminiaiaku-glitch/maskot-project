$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$stage = 'artifacts/notification-setup'
$identity = 'artifacts/notification-identity'
New-Item -ItemType Directory -Force -Path $stage, "$stage/Application", "$identity/Assets", "$stage/Application/Assets" | Out-Null
Get-ChildItem artifacts/win-x64 | Where-Object { $_.Extension -ne '.pdb' } | Copy-Item -Destination "$stage/Application" -Recurse -Force
Copy-Item packaging/notifications/AppxManifest.xml "$identity/AppxManifest.xml"
$sample = Get-ChildItem artifacts/windows-smoke -Recurse -Filter momo-paused.png | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $sample) { throw 'Native Momo render missing; run Windows UI checks first.' }
Add-Type -AssemblyName System.Drawing
$image = [Drawing.Image]::FromFile($sample.FullName)
try {
    foreach ($item in @(@('StoreLogo.png',50), @('Square44x44Logo.png',44), @('Square150x150Logo.png',150))) {
        $size = [int]$item[1]
        $bitmap = New-Object Drawing.Bitmap($size,$size)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $width = [int]($size * $image.Width / $image.Height)
            $graphics.DrawImage($image,[int](($size-$width)/2),0,$width,$size)
            $bitmap.Save((Join-Path (Resolve-Path "$identity/Assets") $item[0]),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $image.Dispose() }
Copy-Item "$identity/Assets/*" "$stage/Application/Assets/"
$kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
$tools = Get-ChildItem $kits -Directory | Where-Object { $_.Name -match '^10\.' } | Sort-Object { [version]$_.Name } -Descending | Where-Object { Test-Path (Join-Path $_.FullName 'x64/makeappx.exe') } | Select-Object -First 1
if (-not $tools) { throw 'Windows SDK MakeAppx/SignTool missing.' }
$makeappx = Join-Path $tools.FullName 'x64/makeappx.exe'
$signtool = Join-Path $tools.FullName 'x64/signtool.exe'
# Microsoft specifies /nv for identity packages whose executable lives outside the MSIX.
# Signature verification and real Add-AppxPackage registration remain separate release gates.
& $makeappx pack /o /d $identity /nv /p "$stage/Momo-Identity.msix"
if ($LASTEXITCODE -ne 0) { throw 'Identity package build failed.' }
$cert = New-SelfSignedCertificate -Type Custom -KeyUsage DigitalSignature -CertStoreLocation Cert:\CurrentUser\My -Subject 'CN=Momo Project Preview' -FriendlyName 'Momo Preview package signing' -NotAfter (Get-Date).AddMonths(12) -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3','2.5.29.19={text}')
try {
    Export-Certificate -Cert $cert -FilePath "$stage/Momo-Preview.cer" | Out-Null
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint /s My "$stage/Momo-Identity.msix"
    if ($LASTEXITCODE -ne 0) { throw 'Package signing failed.' }
    Import-Certificate -FilePath "$stage/Momo-Preview.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    & $signtool verify /pa /v "$stage/Momo-Identity.msix"
    if ($LASTEXITCODE -ne 0) { throw 'Package signature verification failed.' }
    $certificateHash = (Get-FileHash "$stage/Momo-Preview.cer" -Algorithm SHA256).Hash.ToLowerInvariant()
    $packageHash = (Get-FileHash "$stage/Momo-Identity.msix" -Algorithm SHA256).Hash.ToLowerInvariant()
    $applicationHash = (Get-FileHash "$stage/Application/Maskot.exe" -Algorithm SHA256).Hash.ToLowerInvariant()
    @"
internal static class InstallerData {
 internal const string CertificateHash = "$certificateHash";
 internal const string PackageHash = "$packageHash";
 internal const string ApplicationHash = "$applicationHash";
 internal const string Thumbprint = "$($cert.Thumbprint)";
}
"@ | Set-Content -Encoding utf8 artifacts/InstallerData.cs
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    & $compiler /nologo /target:winexe /utf8output /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:"$stage/Pasang-Momo.exe" packaging/notifications/Installer.cs artifacts/InstallerData.cs
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    Copy-Item docs/PASANG-NOTIFIKASI.txt "$stage/MULAI-DI-SINI.txt"
    @{ identity='MomoProject.MomoNotifications'; version='0.5.0.0'; publisher=$cert.Subject; certificateThumbprint=$cert.Thumbprint; certificateSha256=$certificateHash; msixSha256=$packageHash; applicationSha256=$applicationHash; privateKeyIncluded=$false } | ConvertTo-Json | Set-Content -Encoding utf8 "$stage/package-info.json"
    Compress-Archive -Path "$stage/*" -DestinationPath artifacts/Maskot-Notifikasi-Setup.zip -Force
    $hash = (Get-FileHash artifacts/Maskot-Notifikasi-Setup.zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  Maskot-Notifikasi-Setup.zip" | Set-Content -Encoding ascii artifacts/Maskot-Notifikasi-Setup.zip.sha256
} finally { Remove-Item ("Cert:\CurrentUser\My\" + $cert.Thumbprint) -DeleteKey -ErrorAction SilentlyContinue }
