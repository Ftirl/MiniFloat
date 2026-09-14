$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$executablePath = Join-Path $projectRoot 'app\MiniFloat.exe'
if (-not (Test-Path -LiteralPath $executablePath)) { throw 'app\MiniFloat.exe is missing. Run Build.ps1 first.' }
$extensionManifest = Get-Content -LiteralPath (Join-Path $projectRoot 'extension\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $extensionManifest.key) { throw 'The extension public key is missing.' }
$sha = [System.Security.Cryptography.SHA256]::Create()
try { $digest = $sha.ComputeHash([Convert]::FromBase64String($extensionManifest.key)) } finally { $sha.Dispose() }
$extensionId = -join ($digest[0..15] | ForEach-Object { [char](97 + ($_ -shr 4)); [char](97 + ($_ -band 15)) })
$hostManifest = [ordered]@{
    name = 'com.minifloat.host'
    description = 'MiniFloat native desktop video window'
    path = $executablePath
    type = 'stdio'
    allowed_origins = @("chrome-extension://$extensionId/")
}
$manifestPath = Join-Path $projectRoot 'app\com.minifloat.host.json'
[System.IO.File]::WriteAllText($manifestPath,($hostManifest | ConvertTo-Json -Depth 4),[System.Text.UTF8Encoding]::new($false))
$registryKeys = @(
    'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.minifloat.host'
    'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.minifloat.host'
)
foreach ($registryKey in $registryKeys) {
    New-Item -Path $registryKey -Force | Out-Null
    Set-Item -LiteralPath $registryKey -Value $manifestPath
}
Write-Host 'MiniFloat native host registered for Chrome and Edge for the current user.' -ForegroundColor Green
Write-Host "Extension ID: $extensionId"
Write-Host 'Open chrome://extensions or edge://extensions, enable Developer mode, then Load unpacked:'
Write-Host (Join-Path $projectRoot 'extension') -ForegroundColor Cyan
Write-Host 'Do not move this folder after installation; if moved, run Install.cmd again.'
