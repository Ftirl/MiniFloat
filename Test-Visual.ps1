$ErrorActionPreference = 'Stop'
$testOutput = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$visualProcess = Start-Process -FilePath (Join-Path $PSScriptRoot 'app\MiniFloat.exe') -ArgumentList @('--visual-test', ('"' + $testOutput + '"')) -WindowStyle Hidden -PassThru
$peakWorkingBytes = 0
while (-not $visualProcess.HasExited) {
    $visualProcess.Refresh()
    $peakWorkingBytes = [Math]::Max($peakWorkingBytes, $visualProcess.WorkingSet64)
    Start-Sleep -Milliseconds 100
}
$visualProcess.WaitForExit()
"Visual test peak working set (includes two test windows): $([Math]::Round($peakWorkingBytes / 1MB, 1)) MB" | Set-Content -LiteralPath (Join-Path $testOutput 'memory-results.txt')
Get-Content -LiteralPath (Join-Path $testOutput 'visual-results.txt')
exit $visualProcess.ExitCode
