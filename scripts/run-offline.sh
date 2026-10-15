#!/usr/bin/env bash
# Development/test runs on machines without a logged-in Steam client: like scripts/run.sh, with
# the offline Steam test double (tools/SteamStub) loaded in place of the game's libsteam_api.so.
# It reports a logged-in owner of the base game (no DLC, no Workshop items) and never delivers
# callbacks. Not part of the product; parity runs use the real Steam client.
set -euo pipefail
source "$(dirname "$0")/env.sh"
stub="$("$root/tools/SteamStub/build.sh")"
exec "$(dirname "$0")/run.sh" --native-override "steam_api=$stub" "$@"
