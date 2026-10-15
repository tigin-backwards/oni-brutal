#!/usr/bin/env bash
# Runs the player with its own persistent-data and cache roots (out/run-home), so test runs
# never read or write the user's real game data (mods, saves, prefs) under ~/.config/unity3d.
# Extra arguments go to the host, e.g.: scripts/run.sh --headless --frames 300
set -euo pipefail
source "$(dirname "$0")/env.sh"
home="${ONI_RUN_HOME:-$root/out/run-home}"
mkdir -p "$home/config" "$home/cache"
export XDG_CONFIG_HOME="$home/config" XDG_CACHE_HOME="$home/cache"
exec "$root/out/bin/OniBrutal.Host/debug/OniBrutal" --game "$game" "$@"
