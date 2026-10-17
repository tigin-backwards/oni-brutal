#!/usr/bin/env bash
# Builds the Windows player into out/win (framework-dependent, win-x64): the host (OniBrutal.exe)
# with the engine facades and the GLFW/OpenGL natives, the Restarter.exe the game's App.Restart
# starts, and the offline Steam test double (steam_api64.dll, test runs only).
#   scripts/build-windows.sh [debug|release]
set -euo pipefail
source "$(dirname "$0")/env.sh"
config="${1:-release}"
dotnet build "$root/src/OniBrutal.Host" -c "$config" -r win-x64 --self-contained false -v q -nologo
dotnet build "$root/src/OniBrutal.Restarter" -c "$config" -r win-x64 --self-contained false -v q -nologo
out="$root/out/win"
rm -rf "$out"; mkdir -p "$out/host" "$out/steamstub"
cp -r "$root/out/bin/OniBrutal.Host/${config}_win-x64/." "$out/host/"
rm -f "$out/host/Restarter" "$out/host/FacadeGen."*
cp "$root/out/bin/OniBrutal.Restarter/${config}_win-x64/Restarter."{exe,dll,runtimeconfig.json} "$out/host/"
python3 -I "$root/tools/SteamStub/gen.py" "$root/src/ThirdParty/Steamworks.NET/Runtime/autogen/NativeMethods.cs" "$out/steamstub/steam_api64.dll"
echo "$out"
