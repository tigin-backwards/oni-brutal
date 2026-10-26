#!/usr/bin/env bash
# Regenerates the shapes of the Unity packages this project replaces (PLAN M9): the type headers,
# enums, interfaces, delegates and serialized fields in src/<Package>/Generated/, from the shipped
# packages' metadata, plus out/surface/package-{types,members,versions}.tsv. The script classes
# the game's data uses come from the host's asset check.
set -euo pipefail
source "$(dirname "$0")/env.sh"
managed="$game/OxygenNotIncluded_Data/Managed"
dotnet build "$root/tools/SurfaceGen" -c Release -v q -nologo
dotnet build "$root/src/OniMoono.Host" -v q -nologo
"$root/out/bin/OniMoono.Host/debug/OniMoono" --game "$game" --check-assets --list-data-scripts "$root/out/data-scripts.tsv" --log /dev/null > /dev/null
dotnet "$root/out/bin/SurfaceGen/release/SurfaceGen.dll" packages "$managed" "$root/out/surface" "$root/src" "$root/out/data-scripts.tsv" "$root/src/UnityEngine.UI/package-required-types.tsv"
