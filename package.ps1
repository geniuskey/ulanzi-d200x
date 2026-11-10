$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputName 'dist\UlanziLite.exe'
& (Join-Path $PSScriptRoot 'test.ps1') -Executable 'dist\UlanziLite.exe'
$executable = Join-Path $PSScriptRoot 'dist\UlanziLite.exe'
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable).FileVersion
$archive = Join-Path $PSScriptRoot "dist\UlanziLite-$version-win-x64.zip"
# Explicit allowlist: never ship local settings, backups, logs or diagnostics.
$files = @($executable, (Join-Path $PSScriptRoot 'README.md'), (Join-Path $PSScriptRoot 'CONTRIBUTING.md'), (Join-Path $PSScriptRoot 'docs'))
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'LICENSE')) { $files += Join-Path $PSScriptRoot 'LICENSE' }
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$archive.sha256", "$hash  $([IO.Path]::GetFileName($archive))`n", [Text.Encoding]::ASCII)
Write-Host "Packaged $archive"
