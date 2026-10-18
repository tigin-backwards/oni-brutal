#!/usr/bin/env bash
# Builds this project's replacements of Unity's packages (PLAN M9) into out/packages, the
# directory `--ui-packages` loads them from, and checks that they provide every type and member
# the game's assemblies use (tools/ApiCheck against the shipped packages' references).
#   scripts/build-packages.sh [debug|release]
set -euo pipefail
source "$(dirname "$0")/env.sh"
config="${1:-debug}"
mkdir -p "$root/out/packages"
# A package not implemented yet does not build: it is left out, and runs keep the shipped one.
for p in UnityEngine.UI Unity.TextMeshPro Unity.Timeline; do
    rm -f "$root/out/packages/$p.dll" "$root/out/packages/$p.pdb"
    if dotnet build "$root/src/$p" -c "$config" -v q -nologo > "$root/out/build-$p.log" 2>&1; then
        cp "$root/out/bin/$p/$config/$p.dll" "$root/out/bin/$p/$config/$p.pdb" "$root/out/packages/"
    else
        echo "$p: does not build (out/build-$p.log); the shipped package stays in use"
    fi
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
# While one package is still Unity's, it calls into this project's other packages (a mixed run of
# scripts/m9-compare.sh): the shipped TextMeshPro into this uGUI, for example.
mixed=""
for p in UnityEngine.UI Unity.TextMeshPro Unity.Timeline; do
    [ -f "$root/out/packages/$p.dll" ] || mixed="$mixed $p.dll"
done
if [ -n "$mixed" ]; then
    replaced="$(for p in UnityEngine.UI Unity.TextMeshPro Unity.Timeline; do if [ -f "$root/out/packages/$p.dll" ]; then echo -n "$p,"; fi; done)"
    dotnet "$root/out/bin/ApiCheck/release/ApiCheck.dll" "$managed" "$check" "${replaced%,}" $mixed \
      --json "$root/out/apicheck-packages-mixed.json" > "$root/out/apicheck-packages-mixed.txt" || true
    echo "mixed (shipped$mixed):"; head -2 "$root/out/apicheck-packages-mixed.txt"
fi
