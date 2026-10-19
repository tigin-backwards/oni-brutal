#!/usr/bin/env bash
# The project's verification gate (ARCHITECTURE §9): build everything, unit tests, API
# conformance summary, and the host's type-load / runtime-services check against the game, with
# this project's UI packages (the default) and with Unity's shipped ones (the differential
# runs' reference, scripts/m9-compare.sh).
set -euo pipefail
source "$(dirname "$0")/env.sh"
dotnet build "$root/OniBrutal.slnx" -v q -nologo
"$root/out/bin/OniBrutal.UnityEngine.Tests/debug/OniBrutal.UnityEngine.Tests" | grep -E 'Total:|FAIL|\[FAIL\]' || true
"$root/out/bin/OniBrutal.Assets.Tests/debug/OniBrutal.Assets.Tests" | grep -E 'Total:|\[FAIL\]' || true
"$root/scripts/build-engine.sh" >/dev/null
"$root/scripts/apicheck.sh" > "$root/out/apicheck-summary.txt" || true; head -2 "$root/out/apicheck-summary.txt"
"$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" --check-types --log "$root/out/check-types.log" | grep -vE '^(ok   [A-Za-z0-9.-]+ +[0-9]+ types|from )'
"$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" --check-assets --log "$root/out/check-assets.log" | grep -v '^\[engine\]'
"$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" --check-bind --log "$root/out/check-bind.log" | grep -v '^\[engine\]'
"$root/scripts/build-packages.sh" >/dev/null; echo "packages: $(sed -n 2p "$root/out/apicheck-packages.txt")"
"$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" --shipped-ui-packages --check-types --log "$root/out/check-types-shipped.log" | grep -vE '^(ok   [A-Za-z0-9.-]+ +[0-9]+ types|from )'
"$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" --shipped-ui-packages --check-bind --log "$root/out/check-bind-shipped.log" | grep -v '^\[engine\]'
