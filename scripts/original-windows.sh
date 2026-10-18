#!/usr/bin/env bash
# Comparison runs of the ORIGINAL Windows player from WSL, isolated as our own test runs are
# (scripts/run-windows.sh). Not part of the product.
#
# The original runs from a renamed copy of the install (ORIG_DIR, default
# E:\Development\BrutalNotIncluded): Unity finds its data folder by the executable's name, so
# BrutalNotIncluded.exe + BrutalNotIncluded_Data run beside any other copy, and the user's Steam
# install is never touched. In the copy:
# - BepInEx 6 (Unity.Mono), copied from an existing install (ONI_BEPINEX_SOURCE, a game folder
#   holding BepInEx\core, winhttp.dll and doorstop_config.ini), loads tools/OriginalIsolation:
#   Documents and the local application data folder answer folders under the run home (saves,
#   settings, mods), the game's audio goes to <run home>\fmodoutput.wav, and without a run
#   home the player ends before reading any user data;
# - the offline Steam double replaces steam_api64.dll (ONI_STEAM_STUB=PATH: another one, a local
#   test setup's), so no Steam client or account is involved;
# - ignorekcrashreporter.txt keeps crash reports local;
# - the product name in globalgamemanagers (PlayerSettings) reads "Brutal Not Included", the same
#   length as the original's, so Unity's own per-user data (the registry key
#   HKCU\Software\Klei\Brutal Not Included, LocalLow\Klei\Brutal Not Included) is the copy's
#   own and never the installed game's. The game's code does not read the product name.
# Unity's log goes to <run home>\Player.log. The run ends after ORIG_SECONDS (default 600), the
# process killed.
#
#   ONI_RUN_HOME='C:\...\orig-home' scripts/original-windows.sh -screen-width 1024 -screen-height 768 -screen-fullscreen 0
set -euo pipefail
source "$(dirname "$0")/env.sh"
: "${ONI_RUN_HOME:?set ONI_RUN_HOME to the run home (a Windows path)}"
orig_win="${ORIG_DIR:-E:\\Development\\BrutalNotIncluded}"
orig="$(wslpath "$orig_win")"
name="$(basename "$orig")"
source_win="${ONI_WIN_GAME_DIR:-C:\\Program Files (x86)\\Steam\\steamapps\\common\\OxygenNotIncluded}"
source="$(wslpath "$source_win")"
managed="$orig/${name}_Data/Managed"

if [ ! -d "$orig/${name}_Data" ]; then
  : "${ONI_BEPINEX_SOURCE:?set ONI_BEPINEX_SOURCE to a game folder with a BepInEx 6 Unity.Mono install, for the first setup}"
  echo "setting up $orig_win from $source_win"
  mkdir -p "$orig"
  rsync -a --exclude='*.dmp' "$source/" "$orig/"
  mv "$orig/OxygenNotIncluded.exe" "$orig/$name.exe"
  mv "$orig/OxygenNotIncluded_Data" "$orig/${name}_Data"
  mkdir -p "$orig/BepInEx"
  cp -r "$ONI_BEPINEX_SOURCE/BepInEx/core" "$orig/BepInEx/"
  cp "$ONI_BEPINEX_SOURCE/winhttp.dll" "$ONI_BEPINEX_SOURCE/doorstop_config.ini" "$orig/"
  [ -f "$ONI_BEPINEX_SOURCE/.doorstop_version" ] && cp "$ONI_BEPINEX_SOURCE/.doorstop_version" "$orig/"
  touch "$orig/ignorekcrashreporter.txt"
  python3 - "$orig/${name}_Data/globalgamemanagers" <<'PY'
import sys
path = sys.argv[1]
data = open(path, 'rb').read()
old = b'\x13\x00\x00\x00Oxygen Not Included'
assert data.count(old) == 1, 'product name not found once'
open(path, 'wb').write(data.replace(old, b'\x13\x00\x00\x00Brutal Not Included'))
PY
fi

dotnet build "$root/tools/OriginalIsolation" -c Release -v q -nologo -p:GameManaged="$managed" -p:BepInExCore="$orig/BepInEx/core" > /dev/null
mkdir -p "$orig/BepInEx/plugins"
cp "$root/out/bin/OriginalIsolation/release/OriginalIsolation.dll" "$orig/BepInEx/plugins/"
stub="${ONI_STEAM_STUB:-$root/out/win/steamstub/steam_api64.dll}"
[ -f "$stub" ] || { echo "no Steam double at $stub: run scripts/build-windows.sh" >&2; exit 1; }
cp "$stub" "$orig/${name}_Data/Plugins/x86_64/steam_api64.dll"

home="$(wslpath "$ONI_RUN_HOME")"; mkdir -p "$home"
args=""; for a in -logFile "$ONI_RUN_HOME\\Player.log" "$@"; do args="$args'${a//\'/\'\'}',"; done
powershell.exe -NoProfile -Command "
\$env:ONI_RUN_HOME = '${ONI_RUN_HOME//\'/\'\'}'
\$p = Start-Process -FilePath '$orig_win\\$name.exe' -WorkingDirectory '$orig_win' -ArgumentList @(${args%,}) -PassThru
if (-not \$p.WaitForExit(${ORIG_SECONDS:-600} * 1000)) { Stop-Process -Id \$p.Id -Force; 'stopped after ${ORIG_SECONDS:-600} s' } else { 'exited ' + \$p.ExitCode }
" | tr -d '\r'
