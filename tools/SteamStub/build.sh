#!/usr/bin/env bash
# Builds the offline Steam test double (see gen.py) into out/steamstub/libsteam_api.so.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/out/steamstub"
mkdir -p "$out"
python3 -I "$root/tools/SteamStub/gen.py" "$root/src/ThirdParty/Steamworks.NET/Runtime/autogen/NativeMethods.cs" "$out/steam_api_stub.s"
as --64 -o "$out/steam_api_stub.o" "$out/steam_api_stub.s"
ld -shared -soname libsteam_api.so -o "$out/libsteam_api.so" "$out/steam_api_stub.o"
echo "$out/libsteam_api.so"
