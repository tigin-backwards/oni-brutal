#!/usr/bin/env bash
# The HDR check (tools/HdrCheck/README.md): with HDR on for a monitor, how bright the original
# game, Moono presenting through DXGI and Moono presenting through OpenGL reach it. Each player in
# turn opens fullscreen on that monitor at the main menu; the monitor is captured in FP16 (dupcap,
# Desktop Duplication) and the luminance of its brightest UI (the white title "OXYGEN NOT
# INCLUDED" and the button text) is printed in nits. Run from WSL in a checkout with the Windows
# player built (scripts/build-windows.sh) and the original's isolated copy set up
# (scripts/original-windows.sh). Test runs only: own run homes, the offline Steam double.
#   tools/HdrCheck/hdr-check.sh [MONITOR]      MONITOR: 1-based, as -monitor (default 1)
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
monitor="${1:-1}"
[ "$(powershell.exe -NoProfile -Command "(Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).RunningAppID" | tr -d '\r')" = 457140 ] \
    && { echo "Oxygen Not Included is running in Steam; close it first." >&2; exit 1; }
[ -f "$root/out/win/host/OniMoono.exe" ] || { echo "no out/win: run scripts/build-windows.sh first" >&2; exit 1; }
win_temp="$(cd /mnt/c && cmd.exe /c 'echo %TEMP%' 2>/dev/null | tr -d '\r')"
work="$(wslpath "$win_temp")/oni-hdrcheck"; workw="$win_temp\\oni-hdrcheck"
out="$root/out/hdr-check"
mkdir -p "$work" "$out"
cp "$root/tools/HdrCheck/dupcap.c" "$root/tools/HdrCheck/build.cmd" "$work/"
(cd "$work" && cmd.exe /c build.cmd 2>&1 | tr -d '\r') || true
[ -f "$work/dupcap.exe" ] || { echo "dupcap did not build (needs the Visual Studio C++ build tools)" >&2; exit 1; }
rm -rf "$work/host"; cp -r "$root/out/win/host" "$work/host"
cp "$root/out/win/steamstub/steam_api64.dll" "$work/steam_api64.dll"

# The monitor's desktop rectangle (GLFW's and Unity's order is the display order, monitor 1 the
# primary one): "X Y W H".
read -r mx my mw mh <<< "$(powershell.exe -NoProfile -Command "
Add-Type -AssemblyName System.Windows.Forms
\$s = [System.Windows.Forms.Screen]::AllScreens | Sort-Object { -not \$_.Primary }, { \$_.Bounds.X }
\$b = \$s[$monitor - 1].Bounds; \"\$(\$b.X) \$(\$b.Y) \$(\$b.Width) \$(\$b.Height)\"" | tr -d '\r')"
echo "monitor $monitor: ${mw}x$mh at $mx,$my"

home() { # home NAME: an empty run home whose game settings ask for fullscreen at the monitor's size
    local d="$work/$1/Documents/Klei/OxygenNotIncluded"
    rm -rf "$work/$1"; mkdir -p "$d"
    printf 'strings: {}\nints:\n  ResolutionWidth: %s\n  ResolutionHeight: %s\n  RefreshRateNumerator: 60\n  RefreshRateDenominator: 1\n  FullScreen: 1\nfloats: {}\n' "$mw" "$mh" > "$d/kplayerprefs.yaml"
}

stop() { powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w "$root/tools/HdrCheck/stop.ps1")" -Match "$1" > /dev/null 2>&1 || true; }

capture() { # capture NAME SECONDS: the monitor in FP16, SECONDS after the player started
    sleep "$2"
    (cd "$work" && ./dupcap.exe $((mx + 10)) $((my + 10)) fp16 "$1.pfm" | tr -d '\r')
    cp "$work/$1.pfm" "$out/$1.pfm"
}

game='C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded'
for which in ours-dxgi ours-opengl; do
    home "$which"
    dxgi=on; [ "$which" = ours-opengl ] && dxgi=off
    "$work/host/OniMoono.exe" --game "$game" --run-home "$workw\\$which" --audio-wav "$workw\\$which" --splash none --no-tweaks-menu \
        --native-override "steam_api64=$workw\\steam_api64.dll" --log "$workw\\$which\\player.log" -monitor "$monitor" --dxgi "$dxgi" > /dev/null 2>&1 &
    capture "$which" 45
    stop "$workw\\$which\\player.log"
    wait || true
    grep -a '\] present:' "$work/$which/player.log" | sed "s/^/$which: /" | cut -c1-160
done
home original
(cd "$root" && ONI_RUN_HOME="$workw\\original" ORIG_SECONDS=90 ONI_STEAM_STUB="$work/steam_api64.dll" scripts/original-windows.sh -monitor "$monitor" > /dev/null 2>&1) &
capture original 70
stop "$workw\\original\\Player.log"
wait || true

python3 "$root/tools/HdrCheck/summary.py" "$out/original.pfm" "$out/ours-dxgi.pfm" "$out/ours-opengl.pfm" | tee "$out/summary.txt"
