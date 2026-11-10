param([string]$OutputName = 'UlanziLite.exe')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler is required.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$outputPath = Join-Path $root $OutputName
$outputDirectory = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /warnaserror+ /codepage:65001 /out:"$outputPath" /win32icon:"$root\assets\app.ico" /win32manifest:"$root\assets\app.manifest" "/resource:$root\assets\app.ico,UlanziLite.Icon" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host "Built $outputPath"
