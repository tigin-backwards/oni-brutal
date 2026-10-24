#!/usr/bin/env bash
# The project's verification gate (ARCHITECTURE §9): build everything, unit tests, API
# conformance summary, and the host's type-load / runtime-services check against the game, with
# this project's UI packages (the default) and with Unity's shipped ones (the differential
# runs' reference, scripts/m9-compare.sh).
set -euo pipefail
source "$(dirname "$0")/env.sh"
dotnet build "$root/OniBrutal.slnx" -v q -nologo
# Every suite runs and reports; the gate fails at the end if any of them failed.
failed=()
tests() { # tests NAME: run a test binary, print its totals and failures
    local out status=0
    out=$("$root/out/bin/$1/debug/$1" 2>&1) || status=$?
    grep -E 'Total:|\[FAIL\]' <<< "$out" || true
    [ $status -eq 0 ] || failed+=("$1")
}
tests OniBrutal.UnityEngine.Tests
tests OniBrutal.Assets.Tests
tests ModTriage.Tests
scripts_log=$(python3 -m unittest discover -s "$root/tests/scripts" 2>&1) || failed+=(scripts)
echo "scripts: $(tail -1 <<< "$scripts_log")"
grep -A12 -E '^(FAIL|ERROR): ' <<< "$scripts_log" | sed 's/^/  /' || true
"$root/scripts/build-engine.sh" >/dev/null
"$root/scripts/apicheck.sh" > "$root/out/apicheck-summary.txt" || true; head -2 "$root/out/apicheck-summary.txt"
"$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" --check-types --log "$root/out/check-types.log" | grep -vE '^(ok   [A-Za-z0-9.-]+ +[0-9]+ types|from )'
"$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" --check-assets --log "$root/out/check-assets.log" | grep -v '^\[engine\]'
"$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" --check-bind --log "$root/out/check-bind.log" | grep -v '^\[engine\]'
"$root/scripts/build-packages.sh" >/dev/null; echo "packages: $(sed -n 2p "$root/out/apicheck-packages.txt")"
"$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" --shipped-ui-packages --check-types --log "$root/out/check-types-shipped.log" | grep -vE '^(ok   [A-Za-z0-9.-]+ +[0-9]+ types|from )'
"$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" --shipped-ui-packages --check-bind --log "$root/out/check-bind-shipped.log" | grep -v '^\[engine\]'
[ ${#failed[@]} -eq 0 ] || { echo "test suites failed: ${failed[*]}"; exit 1; }
