# One-time setup for playing Oxygen Not Included on oni-brutal (Windows).
#
# Finds the game through Steam's libraries, builds the player when run from a copy of the
# repository (a release has it in player\ beside this script), installs it to
# %LOCALAPPDATA%\oni-brutal\player, and puts the line for the game's Steam launch options on the
# clipboard unless every Steam account here already has it (read from Steam's localconfig.vdf,
# never written). The install location stays the same, so after an update running this again is all
# that is needed.
#   setup.cmd [GAME_DIR], or powershell -ExecutionPolicy Bypass -File setup.ps1 [GAME_DIR]
param([string]$Game)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

# Explorer's "Run with PowerShell" closes the window as soon as the script ends; started that way
# (its command sets the process's execution policy), the window stays open until Enter.
$fromExplorer = [Environment]::CommandLine -match 'Set-ExecutionPolicy -Scope Process Bypass'
function Finish($code) { if ($fromExplorer) { Read-Host 'Press Enter to close' | Out-Null }; exit $code }
function Die($message) { Write-Host "setup: $message" -ForegroundColor Red; Finish 1 }
trap { Die $_ }

# The game: the argument, or the first Steam library holding it.
function Find-Game {
    $steam = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if (-not $steam) { return $null }
    $folders = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (-not (Test-Path $folders)) { return $null }
    foreach ($line in Get-Content $folders) {
        if ($line -match '^\s*"path"\s*"(.*)"\s*$') {
            $dir = Join-Path ($Matches[1] -replace '\\\\', '\') 'steamapps\common\OxygenNotIncluded'
            if (Test-Path (Join-Path $dir 'OxygenNotIncluded_Data')) { return $dir }
        }
    }
    return $null
}

if (-not $Game) { $Game = Find-Game }
if (-not $Game) { Die "Oxygen Not Included wasn't found in any Steam library; install it, or pass its directory: setup.ps1 'C:\path\to\OxygenNotIncluded'" }
if (-not (Test-Path (Join-Path $Game 'OxygenNotIncluded_Data\Managed'))) { Die "$Game isn't an Oxygen Not Included install (no OxygenNotIncluded_Data\Managed)" }
Write-Host "Game: $Game"

if (Test-Path (Join-Path $here 'player\OniBrutal.exe')) {
    $built = Join-Path $here 'player' # a release
} elseif (-not (Test-Path (Join-Path $here 'global.json'))) {
    # Opened from inside the zip, Explorer runs this script alone from a temporary folder.
    Die 'extract the whole zip first (right-click it -> Extract All), then run setup.cmd from the extracted folder'
} else {
    # A .NET SDK of the version global.json pins (a later patch release works too): one already
    # installed, or one installed for this user (no administrator needed).
    $pinned = (Get-Content (Join-Path $here 'global.json') -Raw | ConvertFrom-Json).sdk.version
    $band = $pinned.Substring(0, $pinned.Length - 2) # 10.0.402 -> 10.0.4
    $dotnet = $null
    $userDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    foreach ($candidate in @($userDotnet, (Get-Command dotnet -ErrorAction SilentlyContinue).Source)) {
        if ($candidate -and (Test-Path $candidate) -and ((& $candidate --list-sdks) -match "^$([regex]::Escape($band))")) { $dotnet = $candidate; break }
    }
    if (-not $dotnet) {
        Write-Host "Installing the .NET SDK $pinned for this user..."
        $installer = Join-Path $env:TEMP 'dotnet-install.ps1'
        Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
        & $installer -Version $pinned -InstallDir (Split-Path $userDotnet) | Out-Null
        Remove-Item $installer
        $dotnet = $userDotnet
    }
    $env:DOTNET_ROOT = Split-Path $dotnet
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'

    Write-Host 'Building the player (a few minutes the first time)...'
    # Self-contained: it carries its own .NET runtime.
    foreach ($project in 'src\OniBrutal.Host', 'src\OniBrutal.Restarter') {
        # The output is shown only if the build fails.
        $output = & $dotnet build (Join-Path $here $project) -c Release -r win-x64 --self-contained true "-p:OniGameDir=$Game" -v q -nologo 2>&1
        if ($LASTEXITCODE -ne 0) { $output | Write-Host; Die 'the build failed' }
    }
    $built = Join-Path $here 'out\bin\OniBrutal.Host\release_win-x64'
    Remove-Item -Force (Join-Path $built 'Restarter'), (Join-Path $built 'FacadeGen.*') -ErrorAction SilentlyContinue
    $restarter = Join-Path $here 'out\bin\OniBrutal.Restarter\release_win-x64'
    foreach ($file in 'Restarter.exe', 'Restarter.dll', 'Restarter.deps.json', 'Restarter.runtimeconfig.json') { Copy-Item (Join-Path $restarter $file) $built }
}

# Installed to one place, so the launch options never change.
$player = Join-Path $env:LOCALAPPDATA 'oni-brutal\player'
if (Get-Process -Name OniBrutal -ErrorAction SilentlyContinue) { Die 'the game is running on oni-brutal; quit it first' }
if (Test-Path $player) { Remove-Item -Recurse -Force $player }
New-Item -ItemType Directory -Force (Split-Path $player) | Out-Null
Copy-Item -Recurse $built $player

$exe = Join-Path $player 'OniBrutal.exe'
$launch = "`"$exe`" -- %command%"

# What each Steam account on this machine has as the game's launch options, read (never written)
# from its localconfig.vdf: Steam keeps them under Software\Valve\Steam\apps\457140, and the
# account's name under friends\PersonaName.
function Read-LaunchOptions($file) {
    $text = Get-Content -Raw -LiteralPath $file
    $tokens = [regex]::Matches($text, '"((?:[^"\\]|\\.)*)"|[{}]')
    $path = New-Object System.Collections.Generic.List[string]
    $key = $null; $persona = $null; $options = $null
    foreach ($t in $tokens) {
        if ($t.Value -eq '{') { $path.Add($key); $key = $null; continue }
        if ($t.Value -eq '}') { if ($path.Count) { $path.RemoveAt($path.Count - 1) }; $key = $null; continue }
        $value = $t.Groups[1].Value -replace '\\(.)', '$1'
        if ($null -eq $key) { $key = $value; continue }
        $where = ($path -join '/').ToLowerInvariant()
        if ($where -eq 'userlocalconfigstore/friends' -and $key -eq 'PersonaName') { $persona = $value }
        if ($where -eq 'userlocalconfigstore/software/valve/steam/apps/457140' -and $key -eq 'LaunchOptions') { $options = $value }
        $key = $null
    }
    [pscustomobject]@{ Persona = $persona; Options = $options }
}

$accounts = @()
$steam = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
if ($steam) {
    foreach ($config in Get-ChildItem -Path (Join-Path $steam 'userdata\*\config\localconfig.vdf') -ErrorAction SilentlyContinue) {
        try { $accounts += Read-LaunchOptions $config.FullName } catch { }
    }
}
# Set: the options start this player for the game (other options of the user's may be there too).
function Is-Set($options) { $options -and $options.Contains("`"$exe`"") -and $options.Contains('%command%') }
# Options from before the Brutal Tweaks screen: they still work, and win over the screen.
function Says-Set($a) {
    Write-Host "Steam already starts Oxygen Not Included on oni-brutal for $($a.Persona)." -ForegroundColor Green
    if ($a.Options -match '--game-tweak|--splash|--frame-stats') {
        Write-Host "  Its launch options choose the tweaks, so the game's Brutal Tweaks screen can't change those."
        Write-Host "  To choose them in the game instead, set the launch options to: $launch"
    }
}
$unset = @($accounts | Where-Object { -not (Is-Set $_.Options) })

Write-Host ''
if ($accounts.Count -gt 0 -and $unset.Count -eq 0) {
    foreach ($a in $accounts) { Says-Set $a }
    Write-Host ''
    Write-Host 'Done. Nothing else to do: press Play in Steam. The optional tweaks are on the main menu: Brutal Tweaks.'
    Write-Host 'To play the original game again, clear the launch options (right-click Oxygen Not Included -> Properties).'
    Finish 0
}
foreach ($a in $accounts) {
    if (Is-Set $a.Options) { Says-Set $a }
    elseif ($a.Options) { Write-Host "$($a.Persona): the launch options are set to something else: $($a.Options)" -ForegroundColor Yellow }
    else { Write-Host "$($a.Persona): no launch options yet." -ForegroundColor Yellow }
}
# On the clipboard: copied from the console, a long line picks up its wrapping as spaces.
Set-Clipboard -Value $launch
if ($accounts.Count -gt 0) { Write-Host '' }
Write-Host 'Done. The launch options are on your clipboard. In Steam: right-click Oxygen Not Included ->'
Write-Host 'Properties -> Launch Options, and paste them with Ctrl+V. They are:'
Write-Host ''
Write-Host "    $launch" -ForegroundColor Green
Write-Host ''
Write-Host 'Then press Play as usual. The optional tweaks are on the main menu: Brutal Tweaks.'
Write-Host 'To play the original game again, clear the launch options.'
Finish 0
