$ErrorActionPreference = 'Stop'
$registryKeys = @(
    'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.minifloat.host'
    'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.minifloat.host'
)
$expected = Join-Path $PSScriptRoot 'app\com.minifloat.host.json'
foreach ($registryKey in $registryKeys) {
    if (Test-Path -LiteralPath $registryKey) {
        $registered = (Get-Item -LiteralPath $registryKey).GetValue('')
        if ($registered -ne $expected) { throw "Registration belongs to another installation: $registryKey. No registrations were removed." }
    }
}
foreach ($registryKey in $registryKeys) {
    if (Test-Path -LiteralPath $registryKey) { Remove-Item -LiteralPath $registryKey }
}
Write-Host 'Native host registrations removed. Remove MiniFloat from chrome://extensions and/or edge://extensions to finish.'
Write-Host 'Your files and settings have been kept.'
