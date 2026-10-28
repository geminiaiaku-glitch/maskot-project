$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$stage = Resolve-Path artifacts/notification-setup
$external = Join-Path $stage 'Application'
Import-Certificate -FilePath (Join-Path $stage 'Momo-Preview.cer') -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
Add-AppxPackage -Path (Join-Path $stage 'Momo-Identity.msix') -ExternalLocation $external
$output = Join-Path (Resolve-Path artifacts) ('installed-smoke/run-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
$process = Start-Process (Join-Path $external 'Maskot.exe') -ArgumentList "--self-test --self-test-output `"$output`"" -PassThru
if (-not $process.WaitForExit(90000)) { $process.Kill($true); throw 'Installed Momo self-test timed out.' }
$report = Get-Content (Join-Path $output 'result.json') -Raw | ConvertFrom-Json
$probe = Get-Content (Join-Path $output 'notification-platform.json') -Raw | ConvertFrom-Json
if ($process.ExitCode -ne 0 -or -not $report.passed) { throw ('Installed self-test failed: ' + ($report | ConvertTo-Json -Depth 8)) }
if ($probe.requiresInstallation -or -not $probe.packageIdentity) { throw ('Installed process has no package identity: ' + ($probe | ConvertTo-Json)) }
if (-not $probe.listenerTypeAvailable -or $probe.userAccessRequested) { throw ('Notification API capability probe failed: ' + ($probe | ConvertTo-Json)) }
if ($probe.nativeAccess -notin @('Allowed','Denied','Unspecified')) { throw ('Native access-status API failed: ' + ($probe | ConvertTo-Json)) }
Write-Output ('Installed identity verified: ' + $probe.packageIdentity)
Get-AppxPackage -Name MomoProject.MomoNotifications | Remove-AppxPackage
