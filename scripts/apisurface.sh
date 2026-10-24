#!/usr/bin/env bash
# Unity's whole public surface against ours (tools/ApiCheck --surface): every public type and
# member of the shipped Unity assemblies we replace that the engine and our packages lack, and
# which of them the Workshop mods in out/ws reference. Metadata only.
# Writes out/apisurface.txt and out/apisurface.json; prints the summary and the mod-referenced list.
#   scripts/apisurface.sh            (after scripts/build-engine.sh and scripts/build-packages.sh)
set -uo pipefail
source "$(dirname "$0")/env.sh"
dotnet build "$root/tools/ApiCheck" -c Release -v q -nologo >/dev/null
check="$root/out/packages-check"
rm -rf "$check"; mkdir -p "$check"
cp "$root/out/engine/"*.dll "$check/"
[ -d "$root/out/packages" ] && cp "$root/out/packages/"*.dll "$check/" 2>/dev/null
providers="$(cut -f1 "$root/src/Facades/unity-module-map.tsv" | sort -u | paste -sd,),UnityEngine,Unity.InternalAPIEngineBridge.004,UnityEngine.UI,Unity.TextMeshPro,Unity.Timeline"
mods=()
[ -d "$root/out/ws" ] && mods=(--mods "$root/out/ws" --catalog "$root/scripts/mod-catalog.tsv")
dotnet "$root/out/bin/ApiCheck/release/ApiCheck.dll" --surface "$managed" "$check" "$providers" "${mods[@]}" \
  --json "$root/out/apisurface.json" > "$root/out/apisurface.txt"
status=$?
sed -n '/^MOD-REFERENCED, missing in ours, not public/q;p' "$root/out/apisurface.txt"
exit $status
