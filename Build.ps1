$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework 4.x compiler was not found.' }
$outputDir = Join-Path $projectRoot 'app'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'native') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 "/out:$outputDir\MiniFloat.exe" "/win32manifest:$projectRoot\native\app.manifest" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 128,128
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(22,40,43))
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(130,226,192)),7
$graphics.DrawRectangle($pen,21,30,86,67)
$points = [System.Drawing.Point[]]@([System.Drawing.Point]::new(53,45),[System.Drawing.Point]::new(53,81),[System.Drawing.Point]::new(82,63))
$graphics.FillPolygon([System.Drawing.Brushes]::White,$points)
$bitmap.Save((Join-Path $projectRoot 'extension\icon128.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$pen.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
Write-Host "Built: $outputDir\MiniFloat.exe"
