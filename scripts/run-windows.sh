#!/usr/bin/env bash
# Development/test runs of the Windows player from WSL: stages out/win (scripts/build-windows.sh)
# in %TEMP%\oni-brutal\host and starts OniBrutal.exe on the Windows side with the Windows game
# install. Test runs never touch the user's own game data, Steam account or speakers: the run
# home (ONI_RUN_HOME, default %TEMP%\oni-brutal\run-home) takes the place of Documents,
# AppData and LocalLow (--run-home), the offline Steam double replaces steam_api64.dll, and the
# game's audio goes to <run home>\fmodoutput.wav (--audio-wav). Extra arguments go to the host.
# As scripts/run.sh, `--game-tweak none --splash none` come first: no tweaks and no loading splash
# unless the run names them.
#   scripts/run-windows.sh --frames 600 --capture 590:C:/path/shot.png
set -euo pipefail
source "$(dirname "$0")/env.sh"
win_temp="$(cmd.exe /c 'echo %TEMP%' 2>/dev/null | tr -d '\r')"
stage="$(wslpath "$win_temp")/oni-brutal"
mkdir -p "$stage"
rm -rf "$stage/host" "$stage/steamstub"
cp -r "$root/out/win/host" "$root/out/win/steamstub" "$stage/"
# ONI_STEAM_STUB=PATH: another steam_api64.dll for the run (a local test setup's).
[ -n "${ONI_STEAM_STUB:-}" ] && cp "$ONI_STEAM_STUB" "$stage/steamstub/steam_api64.dll"
stub=steamstub
run_home="${ONI_RUN_HOME:-$win_temp\\oni-brutal\\run-home}"
game="${ONI_WIN_GAME_DIR:-C:\\Program Files (x86)\\Steam\\steamapps\\common\\OxygenNotIncluded}"
exec "$stage/host/OniBrutal.exe" --game "$game" --run-home "$run_home" --audio-wav "$run_home" --game-tweak none --splash none \
  --native-override "steam_api64=$win_temp\\oni-brutal\\$stub\\steam_api64.dll" "$@"
