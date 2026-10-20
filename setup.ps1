# One-time setup for playing Oxygen Not Included on oni-brutal (Windows).
#
# Finds the game through Steam's libraries, builds the player when run from a copy of the
# repository (a release has it in player\ beside this script), installs it to
# %LOCALAPPDATA%\oni-brutal\player, and puts the line for the game's Steam launch options on the
# clipboard. The install location stays the same, so after an update running this again is all
# that is needed.
#   powershell -ExecutionPolicy Bypass -File setup.ps1 [GAME_DIR]
param([string]$Game)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

function Die($message) { Write-Host "setup: $message" -ForegroundColor Red; exit 1 }

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

$launch = "`"$(Join-Path $player 'OniBrutal.exe')`" --game-tweak exact -- %command%"
# On the clipboard: copied from the console, a long line picks up its wrapping as spaces.
Set-Clipboard -Value $launch
Write-Host ''
Write-Host 'Done. The launch options are on your clipboard. In Steam: right-click Oxygen Not Included ->'
Write-Host 'Properties -> Launch Options, and paste them with Ctrl+V. They are:'
Write-Host ''
Write-Host "    $launch" -ForegroundColor Green
Write-Host ''
Write-Host 'Then press Play as usual. To play the original game again, clear the launch options.'
