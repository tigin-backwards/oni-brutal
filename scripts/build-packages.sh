#!/usr/bin/env bash
# Builds this project's replacements of Unity's packages (PLAN M9) into out/packages, the
# directory `--ui-packages` loads them from, and checks that they provide every type and member
# the game's assemblies use (tools/ApiCheck against the shipped packages' references).
#   scripts/build-packages.sh [debug|release]
set -euo pipefail
source "$(dirname "$0")/env.sh"
config="${1:-debug}"
mkdir -p "$root/out/packages"
for p in UnityEngine.UI Unity.TextMeshPro Unity.Timeline; do
    dotnet build "$root/src/$p" -c "$config" -v q -nologo
    cp "$root/out/bin/$p/$config/$p.dll" "$root/out/bin/$p/$config/$p.pdb" "$root/out/packages/"
done
# ApiCheck resolves members through base types into the engine, so it sees both.
check="$root/out/packages-check"
rm -rf "$check"; mkdir -p "$check"
cp "$root/out/engine/"*.dll "$root/out/packages/"*.dll "$check/"
dotnet build "$root/tools/ApiCheck" -c Release -v q -nologo >/dev/null
dotnet "$root/out/bin/ApiCheck/release/ApiCheck.dll" "$managed" "$check" UnityEngine.UI,Unity.TextMeshPro,Unity.Timeline \
  Assembly-CSharp.dll Assembly-CSharp-firstpass.dll FMODUnity.dll FMODUnityResonance.dll FMODUnityHaptics.dll \
  ImGui.NET.dll ImGui.dll com.rlabrecque.steamworks.net.dll --json "$root/out/apicheck-packages.json" > "$root/out/apicheck-packages.txt" || true
head -2 "$root/out/apicheck-packages.txt"
