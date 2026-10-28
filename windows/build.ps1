$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path $csc)) { throw '.NET Framework C# compiler was not found.' }
New-Item -ItemType Directory -Force dist | Out-Null
& $csc /nologo /target:winexe /optimize+ /out:dist\SubtitleBlocker.exe /win32icon:app.ico /reference:System.Drawing.dll /reference:System.Windows.Forms.dll SubtitleBlur.cs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host 'Built dist\SubtitleBlocker.exe'
