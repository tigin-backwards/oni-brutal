#!/usr/bin/env bash
# Differential comparison for PLAN M9: runs a scenario with Unity's shipped UI packages and with
# this project's (PACKAGES_DIR, default out/packages), dumps the UI at the scenario's frames
# (--dump-ui), and diffs the dumps (scripts/ui-diff.py). Captures are kept for a visual check.
#   scripts/m9-compare.sh SCENARIO OUTDIR [PACKAGES_DIR]
# A scenario (scripts/m9-scenarios/NAME.sh) sets FRAMES, DUMPS (frame numbers), ARGS (host
# arguments: --input …) and optionally SAVE (a .sav resumed as the profile's only colony; the
# scenario's inputs load it). Runs are isolated (a scratch run home, audio to a private sink) and
# use a 1024x768 window on DISPLAY (default :97).
set -uo pipefail
source "$(dirname "$0")/env.sh"
scenario=$1; out=$2; packages="${3:-$root/out/packages}"
SAVE=""; source "$root/scripts/m9-scenarios/$scenario.sh"
mkdir -p "$out"
run() # NAME [host args]
{
    local name=$1; shift
    local home="$out/home-$name" d
    rm -rf "${home:?}"
    d="$home/config/unity3d/Klei/Oxygen Not Included"
    mkdir -p "$d"
    printf 'strings: {}\nints:\n  ResolutionWidth: 1024\n  ResolutionHeight: 768\n  RefreshRateNumerator: 60\n  RefreshRateDenominator: 1\n  FullScreen: 0\nfloats: {}\n' > "$d/kplayerprefs.yaml"
    if [ -n "$SAVE" ]; then
        local colony; colony=$(basename "$SAVE" .sav)
        mkdir -p "$d/cloud_save_files/76561197960265729/$colony"
        cp "$SAVE" "$d/cloud_save_files/76561197960265729/$colony/$colony.sav"
    fi
    local dumps=() f
    for f in $DUMPS; do dumps+=(--dump-ui "$f:$out/$name-$f.txt" --capture "$f:$out/$name-$f.png"); done
    DISPLAY="${DISPLAY:-:97}" ONI_RUN_HOME="$home" timeout 1800 "$root/scripts/audio-sink/record.sh" "$out/$name.raw" \
        "$root/scripts/run-offline.sh" -screen-width 1024 -screen-height 768 -screen-fullscreen 0 \
        --frames "$FRAMES" --max-frame-rate 60 --log "$out/$name.log" "${dumps[@]}" "${ARGS[@]}" "$@" > "$out/$name.stdout" 2>&1
    echo "$name: exit $?, $(grep -ci exception "$out/$name.log") exception lines"
    rm -f "$out/$name.raw"
}
run shipped
run ours --ui-packages "$packages"
status=0
for f in $DUMPS; do
    echo "== frame $f"
    "$root/scripts/ui-diff.py" "$out/shipped-$f.txt" "$out/ours-$f.txt" > "$out/diff-$f.txt" || status=1
    tail -1 "$out/diff-$f.txt"
done
exit $status
