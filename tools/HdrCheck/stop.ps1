# stop.ps1 -Match TEXT: stops the game process (Moono's or the original's isolated copy) whose
# command line holds TEXT (the run's own home), never one found by name alone.
param([string]$Match)
if (-not $Match) { exit 2 }
$names = @('OniMoono.exe', 'BrutalNotIncluded.exe')
Get-CimInstance Win32_Process | Where-Object { $names -contains $_.Name -and $_.CommandLine -and $_.CommandLine.Contains($Match) } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
