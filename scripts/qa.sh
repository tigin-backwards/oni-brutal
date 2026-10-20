#!/usr/bin/env bash
# The QA harness: exercises what the engine and the game tweaks do, and fails on a regression.
#   scripts/qa.sh [--bless] [STAGE...]        (stages default to all of them, in this order)
# Stages:
#   unit     the build, both test suites and scripts/check.sh (API conformance, type loading,
#            assets, binding), with this project's UI packages and Unity's shipped ones.
#   dogfood  scripted sessions through the game, each a scenario (scripts/qa-scenarios, or a
#            scripts/m9-scenarios name): the menu, a new game to embark, a colony's screens and
#            overlays, typing, save and load, a window resize, clicks within one frame. Fails on a
#            log exception or error whose signature the reference lacks, or a capture that differs
#            from the reference beyond the scenario's tolerance.
#   verify   U57 with every game tweak and every engine verify mode, for at least 10 game minutes:
#            every check reports zero mismatches.
#   perf     U57 with every game tweak: peak memory under 16 GB, the steady frame rate no lower than
#            the reference's less 15% (the machine's noise), multi-target framebuffers not piling up.
# verify and perf need a large Spaced Out colony and its DLC content, which cannot be published:
# out/local-tools/qa.env sets QA_U57_GAME (a game folder with the DLC content), QA_U57_STEAM (a
# Steam double owning the DLC) and QA_U57_SAVE; without it those stages are skipped, and say so.
# It can also export the machine's GL settings for Xvfb (e.g. GALLIUM_DRIVER), which every run uses.
# --bless stores this run's captures, signatures and frame rate as the references
# (out/qa/reference), once they have been looked at; --bless-from=RUN stores a past run's.
set -uo pipefail
source "$(dirname "$0")/env.sh"
export PATH="$HOME/.dotnet:$PATH"
bless=0; stages=(); bless_from=""
for a in "$@"; do
    case $a in
        --bless) bless=1 ;;
        --bless-from=*) bless_from=${a#--bless-from=} ;;
        *) stages+=("$a") ;;
    esac
done
# --bless-from=RUN: a past run's dogfood captures and signatures, once looked at, become the
# references (without running the scenarios again).
if [ -n "$bless_from" ]; then
    for d in "$bless_from"/dogfood/*/; do
        name=$(basename "$d"); r="$root/out/qa/reference/dogfood/$name"
        rm -rf "$r"; mkdir -p "$r"; cp "$d"/signatures.txt "$d"/f*.png "$r"/ 2>/dev/null
        rm -f "$r"/f*-diff.png; echo "blessed $name from $bless_from"
    done
    exit 0
fi
[ ${#stages[@]} -eq 0 ] && stages=(unit dogfood verify perf)
qa="$root/out/qa"; ref="$qa/reference"; run="$qa/run-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$run" "$ref"
[ -f "$root/out/local-tools/qa.env" ] && source "$root/out/local-tools/qa.env"
tools="$root/scripts/qa-tools.py"
failures=0
fail() { echo "FAIL $*"; echo "FAIL $*" >> "$run/summary.txt"; failures=$((failures + 1)); }
ok() { echo "ok   $*"; echo "ok   $*" >> "$run/summary.txt"; }
note() { echo "     $*"; echo "     $*" >> "$run/summary.txt"; }

# ---- build ----
# The release player every stage runs: the engine, the UI packages and the host, which carries its
# own copy of the engine (a build of the engine alone does not reach the player). Once a run.
built=0
build_release()
{
    [ $built -eq 1 ] && return 0
    if "$root/scripts/build-engine.sh" release > "$run/build-release.txt" 2>&1 \
       && "$root/scripts/build-packages.sh" >> "$run/build-release.txt" 2>&1 \
       && dotnet build "$root/src/OniBrutal.Host" -c Release -v q -nologo >> "$run/build-release.txt" 2>&1 \
       && dotnet build "$root/tools/PerfProbe" -c Release -v q -nologo >> "$run/build-release.txt" 2>&1; then
        built=1; return 0
    fi
    fail "release build (see $run/build-release.txt)"; return 1
}

# ---- unit ----
stage_unit()
{
    local log="$run/unit.txt"
    build_release || return
    "$root/scripts/check.sh" > "$log" 2>&1; local status=$?
    [ $status -eq 0 ] || fail "unit: scripts/check.sh exited $status (see $log)"
    grep -E 'Total: [0-9]+, Errors: [0-9]+, Failed: [0-9]+' "$log" | sed 's/\x1b\[[0-9;]*m//g' | while read -r line; do echo "     $line"; done
    if grep -qE 'Errors: [1-9]|Failed: [1-9]' "$log"; then fail "unit: test failures (see $log)"; else ok "unit: tests"; fi
    if grep -qE '^[1-9][0-9]* failures|unresolved scripts [1-9]|missing types: [1-9]|^FAIL' "$log"; then fail "unit: host checks (see $log)"; else ok "unit: host checks"; fi
    local missing; missing=$(grep -oE 'missing members: [0-9]+' "$root/out/apicheck-summary.txt" | head -1 | grep -oE '[0-9]+')
    if [ "${missing:-99}" -le 13 ]; then ok "unit: ApiCheck, $missing members missing (13 known, UI Toolkit internals)"; else fail "unit: ApiCheck, $missing members missing"; fi
}

# ---- dogfood ----
SCENARIOS=(menu options newgame colony codex management research naming overlays saveload resize burst intro)
[ -n "${QA_SCENARIOS:-}" ] && read -ra SCENARIOS <<< "$QA_SCENARIOS"

run_scenario() # NAME
{
    local name=$1 out="$run/dogfood/$1" file
    file="$root/scripts/qa-scenarios/$name.sh"; [ -f "$file" ] || file="$root/scripts/m9-scenarios/$name.sh"
    # Scenario settings: TOLERANCE (fraction of pixels a capture may differ: random Duplicants,
    # real-time animation), KEPT_MAX (multi-target framebuffers alive at the end: one per target
    # set whose textures are still referenced), REQUIRE (log lines the run must print).
    SAVE=""; FRAMES=0; DUMPS=""; ARGS=(); TOLERANCE=0.03; DLC=0; KEPT_MAX=1; REQUIRE=()
    source "$file"
    # DLC=1: the scenario needs the DLC content and a DLC-owning Steam double (qa.env).
    local gamedir="$game" runner=("$root/scripts/run-offline.sh")
    if [ "$DLC" = 1 ]; then
        u57_ready || { note "dogfood $name: skipped (needs the DLC setup in out/local-tools/qa.env)"; return; }
        gamedir=$QA_U57_GAME; runner=("$root/scripts/run.sh" --native-override "steam_api=$QA_U57_STEAM")
    fi
    # QA_EXTRA_ARGS: host arguments added to every scenario (e.g. --shipped-ui-packages, for
    # comparing a scenario with Unity's UI packages).
    [ -n "${QA_EXTRA_ARGS:-}" ] && read -ra extra <<< "$QA_EXTRA_ARGS" && ARGS+=("${extra[@]}")
    local home="$out/home" d
    rm -rf "${out:?}"; mkdir -p "$out"
    d="$home/config/unity3d/Klei/Oxygen Not Included"; mkdir -p "$d"
    printf 'strings: {}\nints:\n  ResolutionWidth: 1024\n  ResolutionHeight: 768\n  RefreshRateNumerator: 60\n  RefreshRateDenominator: 1\n  FullScreen: 0\nfloats: {}\n' > "$d/kplayerprefs.yaml"
    if [ -n "$SAVE" ]; then
        local colony; colony=$(basename "$SAVE" .sav)
        mkdir -p "$d/cloud_save_files/76561197960265729/$colony"
        cp "$SAVE" "$d/cloud_save_files/76561197960265729/$colony/$colony.sav"
    fi
    local caps=() f
    for f in $DUMPS; do caps+=(--capture "$f:$out/f$f.png" --dump-ui "$f:$out/f$f.txt"); done
    ONI_GAME_DIR="$gamedir" ONI_RUN_HOME="$home" ONI_CONFIG=release timeout 1800 xvfb-run -a -s "-screen 0 1280x1024x24" \
        "$root/scripts/audio-sink/record.sh" "$out/audio.raw" "${runner[@]}" \
        -screen-width 1024 -screen-height 768 -screen-fullscreen 0 --frames "$FRAMES" --max-frame-rate 60 \
        --log "$out/log.txt" "${caps[@]}" "${ARGS[@]}" > "$out/stdout.txt" 2>&1
    local status=$?
    rm -f "$out/audio.raw"
    # The game's own log (Debug.Log, exceptions) goes to stdout with the engine's.
    { echo "exit $status"; python3 "$tools" signatures "$out/stdout.txt"; } > "$out/signatures.txt"
    local rdir="$ref/dogfood/$name"
    if [ $bless -eq 1 ]; then
        rm -rf "$rdir"; mkdir -p "$rdir"; cp "$out/signatures.txt" "$rdir/"
        for f in $DUMPS; do cp "$out/f$f.png" "$rdir/" 2>/dev/null; done
        ok "dogfood $name: blessed ($(($(wc -l < "$out/signatures.txt") - 1)) signatures)"; return
    fi
    if [ ! -d "$rdir" ]; then fail "dogfood $name: no reference (run with --bless after checking $out)"; return; fi
    local new; new=$(comm -13 <(sort "$rdir/signatures.txt") <(sort "$out/signatures.txt"))
    if [ -n "$new" ]; then fail "dogfood $name: new log signatures:"; echo "$new" | sed 's/^/       /' | tee -a "$run/summary.txt"; fi
    local worst=0 bad=""
    for f in $DUMPS; do
        if [ ! -f "$out/f$f.png" ]; then bad="$bad f$f(missing)"; continue; fi
        local r; r=$(python3 "$tools" image "$rdir/f$f.png" "$out/f$f.png" "$out/f$f-diff.png" | cut -d' ' -f1)
        awk -v r="$r" -v t="$TOLERANCE" 'BEGIN { exit !(r > t) }' && bad="$bad f$f($r)"
        awk -v r="$r" -v w="$worst" 'BEGIN { exit !(r > w) }' && worst=$r
    done
    if [ -n "$bad" ]; then fail "dogfood $name: captures differ beyond $TOLERANCE:$bad"; else [ -z "$new" ] && ok "dogfood $name (largest capture difference $worst)"; fi
    # Multi-target framebuffers go with their textures.
    local kept; kept=$(grep -aoE '[0-9]+ kept' "$out/stdout.txt" | tail -1 | cut -d' ' -f1)
    [ "${kept:-0}" -le "$KEPT_MAX" ] || fail "dogfood $name: $kept multi-target framebuffers kept at the end (at most $KEPT_MAX)"
    local line
    for line in "${REQUIRE[@]}"; do grep -aqF "$line" "$out/stdout.txt" || fail "dogfood $name: the log lacks \"$line\""; done
}

stage_dogfood()
{
    build_release || return
    local s
    for s in "${SCENARIOS[@]}"; do run_scenario "$s"; done
}

# ---- U57 (verify, perf) ----
u57_ready()
{
    [ -n "${QA_U57_GAME:-}" ] && [ -n "${QA_U57_STEAM:-}" ] && [ -f "${QA_U57_SAVE:-}" ]
}

# u57_run OUT FRAMES [host args] — U57 resumed from the menu, PerfProbe keeping it at speed 3,
# peak resident memory written to OUT/peak-rss (kB).
u57_run()
{
    local out=$1 frames=$2; shift 2
    local home="$out/home" d
    rm -rf "${out:?}"; mkdir -p "$out"
    d="$home/config/unity3d/Klei/Oxygen Not Included"
    mkdir -p "$d/cloud_save_files/76561197960265729/U57" "$d/mods/Local"
    cp "$QA_U57_SAVE" "$d/cloud_save_files/76561197960265729/U57/U57.sav"
    printf 'strings: {}\nints:\n  ResolutionWidth: 1024\n  ResolutionHeight: 768\n  RefreshRateNumerator: 60\n  RefreshRateDenominator: 1\n  FullScreen: 0\nfloats: {}\n' > "$d/kplayerprefs.yaml"
    cp -r "$root/out/bin/PerfProbe/release" "$d/mods/Local/PerfProbe"
    echo '{"version":1,"mods":[{"label":{"distribution_platform":0,"id":"PerfProbe","title":"PerfProbe","version":-2037174645},"status":1,"enabled":true,"enabledForDlc":["","EXPANSION1_ID"],"crash_count":0,"reinstall_path":null,"staticID":"PerfProbe"}],"mod_load_in_progress":false}' > "$d/mods/mods.json"
    ONI_GAME_DIR="$QA_U57_GAME" ONI_RUN_HOME="$home" ONI_CONFIG=release timeout 3600 xvfb-run -a -s "-screen 0 1280x1024x24" \
        "$root/scripts/run.sh" --native-override "steam_api=$QA_U57_STEAM" -screen-width 1024 -screen-height 768 -screen-fullscreen 0 \
        --frames "$frames" --input 400:click:948,75 --input 900:click:153,192 --game-tweak all --log "$out/log.txt" "$@" > "$out/stdout.txt" 2>&1 &
    local pid=$! peak=0 rss p
    while kill -0 $pid 2>/dev/null; do
        for p in $(pgrep -x OniBrutal); do
            grep -qa "$home" /proc/$p/environ 2>/dev/null || continue
            rss=$(awk '/VmRSS/ {print $2}' /proc/$p/status 2>/dev/null); rss=${rss:-0}
            [ "$rss" -gt "$peak" ] && peak=$rss
            echo "$(date +%s) $rss" >> "$out/rss.txt"
            # A runaway run is stopped before it takes the machine down.
            [ "$rss" -gt $((24 * 1024 * 1024)) ] && kill -KILL $p
        done
        sleep 5
    done
    wait $pid; echo $? > "$out/exit"
    echo $peak > "$out/peak-rss"
}

stage_verify()
{
    u57_ready || { note "verify: skipped (no out/local-tools/qa.env)"; return; }
    build_release || return
    local out="$run/verify"
    ONI_TWEAK_VERIFY=1 ONI_VERIFY_CANVAS_SKIPS=1 ONI_VERIFY_COMPONENT_LAYOUTS=1 ONI_VERIFY_TEXEL_COPIES=1 u57_run "$out" 7000
    local report; report=$(python3 "$tools" verify "$out/stdout.txt"); local status=$?
    echo "$report" | sed 's/^/     /' | tee -a "$run/summary.txt"
    [ $status -eq 0 ] || fail "verify: a verify mode reported mismatches"
    local tweak
    for tweak in fast-anim-events fast-sublimation fast-jobs fast-particle-collision fast-offset-tables fast-anim-cleanup "canvas skips" "component layouts" "texel copies"; do
        echo "$report" | grep -q "$tweak:" || fail "verify: no report from $tweak"
    done
    read -r fps span windows <<< "$(python3 "$tools" perf "$out/stdout.txt")"
    if [ "${span:-0}" -ge 600 ]; then ok "verify: $span game seconds checked"; else fail "verify: only ${span:-0} game seconds checked (10 minutes needed)"; fi
}

stage_perf()
{
    u57_ready || { note "perf: skipped (no out/local-tools/qa.env)"; return; }
    build_release || return
    local out="$run/perf"
    u57_run "$out" 4800
    local peak; peak=$(cat "$out/peak-rss")
    if [ "$peak" -lt $((16 * 1024 * 1024)) ]; then ok "perf: peak memory $((peak / 1024)) MB"; else fail "perf: peak memory $((peak / 1024)) MB (16 GB limit)"; fi
    read -r fps span windows <<< "$(python3 "$tools" perf "$out/stdout.txt")"
    local kept; kept=$(grep -aoE '[0-9]+ kept' "$out/stdout.txt" | tail -1 | cut -d' ' -f1)
    if [ "${kept:-0}" -le 1 ]; then ok "perf: multi-target framebuffers kept ${kept:-0}"; else fail "perf: $kept multi-target framebuffers kept"; fi
    if [ $bless -eq 1 ]; then echo "$fps" > "$ref/perf-baseline"; ok "perf: $fps fps blessed as the baseline"; return; fi
    local base; base=$(cat "$ref/perf-baseline" 2>/dev/null || echo 0)
    if awk -v f="$fps" -v b="$base" 'BEGIN { exit !(f >= b * 0.85) }'; then ok "perf: $fps fps over $windows windows (baseline $base)"; else fail "perf: $fps fps, below the baseline $base less 15%"; fi
}

for s in "${stages[@]}"; do
    case $s in
        unit|dogfood|verify|perf) echo "== $s"; "stage_$s" ;;
        *) echo "unknown stage $s"; exit 2 ;;
    esac
done
echo "== $failures failure(s); run in $run"
exit $(( failures > 0 ))
