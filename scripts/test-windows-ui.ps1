$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$output = Join-Path $PWD ('artifacts/windows-smoke/run-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $output | Out-Null
$exe = Join-Path $PWD 'artifacts/win-x64/Maskot.exe'
$process = Start-Process -FilePath $exe -ArgumentList "--self-test --self-test-output `"$output`"" -PassThru -RedirectStandardOutput (Join-Path $output 'stdout.txt') -RedirectStandardError (Join-Path $output 'stderr.txt')
if (-not $process.WaitForExit(60000)) {
    $process.Kill($true)
    throw 'Windows UI checks did not finish within 60 seconds.'
}
$reportPath = Join-Path $output 'result.json'
if (-not (Test-Path $reportPath)) {
    $details = Get-ChildItem $output -File | Where-Object { $_.Name -in @('failure.txt', 'stderr.txt') } | Get-Content -Tail 30 | Out-String
    throw "Application exited with code $($process.ExitCode) without a UI test report. $details"
}
$report = Get-Content $reportPath -Raw | ConvertFrom-Json
Get-Content $reportPath
if ($process.ExitCode -ne 0 -or -not $report.passed) {
    $details = $report.checks | ConvertTo-Json -Depth 8
    throw "Windows UI regression checks failed. $details"
}
Write-Output 'Windows WPF movement, animation pixels, fullscreen layer, and panel checks passed.'
