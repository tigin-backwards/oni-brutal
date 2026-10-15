#!/usr/bin/env bash
# Builds the engine and lays out out/engine: the implementation assembly plus the generated
# UnityEngine.*Module forwarding facades the game binds to.
set -euo pipefail
source "$(dirname "$0")/env.sh"
config="${1:-debug}"
dotnet build "$root/tools/FacadeGen" -c Release -v q -nologo
dotnet build "$root/src/OniBrutal.UnityEngine" -c "$config" -v q -nologo
mkdir -p "$root/out/engine"
cp "$root/out/bin/OniBrutal.UnityEngine/$config/OniBrutal.UnityEngine.dll" "$root/out/bin/OniBrutal.UnityEngine/$config/OniBrutal.UnityEngine.pdb" "$root/out/engine/"
dotnet "$root/out/bin/FacadeGen/release/FacadeGen.dll" emit "$root/src/Facades/unity-module-map.tsv" "$root/out/engine" "$root/out/engine/OniBrutal.UnityEngine.dll"
