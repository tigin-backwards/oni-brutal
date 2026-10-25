#!/usr/bin/env bash
# Runs the player with its own persistent-data and cache roots (out/run-home), so test runs
# never read or write the user's real game data (mods, saves, prefs) under ~/.config/unity3d.
# Extra arguments go to the host, e.g.: scripts/run.sh --headless --frames 300
# ONI_CONFIG=release runs the release build (scripts/build-engine.sh release and a release host).
# The player runs in the memory-capped test slice (scripts/contain.sh).
# Test runs measure and compare the game without tweaks unless they name some: `--game-tweak none`
# comes first, so the player's default tweaks and any Moono Tweaks settings file in the run home
# play no part, and a run's own `--game-tweak` arguments (e.g. `exact`) are its whole set. Likewise
# `--splash none`: the default loading splash stays out of test runs unless one asks for it.
# ONI_TWEAK_SETTINGS=1 leaves that out: the run home's settings and the defaults apply, as in a
# user's launch (for trying the main menu's Moono Tweaks screen).
set -euo pipefail
source "$(dirname "$0")/env.sh"
home="${ONI_RUN_HOME:-$root/out/run-home}"
mkdir -p "$home/config" "$home/cache"
export XDG_CONFIG_HOME="$home/config" XDG_CACHE_HOME="$home/cache"
tweaks=(--game-tweak none --splash none)
[ "${ONI_TWEAK_SETTINGS:-}" = 1 ] && tweaks=()
exec "$root/scripts/contain.sh" "$root/out/bin/OniBrutal.Host/${ONI_CONFIG:-debug}/OniMoono" --game "$game" "${tweaks[@]}" "$@"
