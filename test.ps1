param([string]$Executable = 'UlanziLite.exe')
$ErrorActionPreference = 'Stop'
$executablePath = Join-Path $PSScriptRoot $Executable
if (!(Test-Path -LiteralPath $executablePath)) { throw "Build first: $executablePath" }
foreach ($mode in @('--self-test', '--render')) {
    $process = Start-Process -FilePath $executablePath -ArgumentList $mode -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "$mode failed with exit code $($process.ExitCode). Check test-results.txt beside the executable." }
    Write-Host "$mode passed"
}
