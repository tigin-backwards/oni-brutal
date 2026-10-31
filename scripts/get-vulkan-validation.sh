#!/usr/bin/env bash
# Fetches the Khronos Vulkan validation layer for --vulkan-validation into
# out/local-tools/vulkan-validation/{linux,windows} (gitignored; nothing is installed). Pinned:
# Vulkan-ValidationLayers vulkan-sdk-1.4.363.0, from LunarG's SDK packages of that release
# (Khronos publishes desktop binaries only as a moving nightly), checked against the SHA-256s
# below. Only the layer's library and manifest are kept.
# - Linux: the distro's layer (vulkan-validationlayers) when installed, which the loader finds
#   itself; else the SDK tarball's.
# - Windows: the SDK installer is never run: the 7z archive with the layers is read out of it
#   (bsdtar or 7z, or Windows' own tar.exe from WSL).
# The player looks for the layer next to its executable (vulkan-validation/) and in the
# checkout's out/local-tools/vulkan-validation/<os>, and points the Vulkan loader at it
# (VK_ADD_LAYER_PATH); scripts/run-windows.sh copies the Windows layer next to the staged player.
#   scripts/get-vulkan-validation.sh [linux|windows|both]   (default both)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
version=1.4.363.0
linux_sha=197962f5cbf80baf2775a03336a01cee7c8745686c65aaa70d3f751ade4d7e43
windows_sha=94a82d378f7a5e3e54c9db7d2fb7016af136e14ac0a18dbf0f2f67a36352d141
out="$root/out/local-tools/vulkan-validation"
dl="$out/downloads"
which="${1:-both}"
mkdir -p "$dl"

fetch() { # fetch URL FILE SHA
    if [ ! -f "$dl/$2" ] || ! echo "$3  $dl/$2" | sha256sum -c --status; then
        echo "downloading $2"
        curl -fSL --retry 3 -o "$dl/$2.part" "$1"
        mv "$dl/$2.part" "$dl/$2"
    fi
    echo "$3  $dl/$2" | sha256sum -c --status || { echo "$2: SHA-256 differs from the pinned one; removed" >&2; rm -f "$dl/$2"; exit 1; }
}

if [ "$which" != windows ]; then
    if [ -f /usr/share/vulkan/explicit_layer.d/VkLayer_khronos_validation.json ]; then
        echo "linux: the distro's validation layer is installed; the loader finds it (nothing fetched)"
    else
        f="vulkansdk-linux-x86_64-$version.tar.xz"
        fetch "https://sdk.lunarg.com/sdk/download/$version/linux/$f" "$f" "$linux_sha"
        rm -rf "$out/linux"; mkdir -p "$out/linux"
        tar -xJf "$dl/$f" -C "$out/linux" --strip-components=3 "$version/x86_64/lib/libVkLayer_khronos_validation.so"
        tar -xJf "$dl/$f" -C "$out/linux" --strip-components=5 "$version/x86_64/share/vulkan/explicit_layer.d/VkLayer_khronos_validation.json"
        # The manifest names the library relative to the SDK's tree; here they sit side by side.
        sed -i 's|"library_path": "[^"]*"|"library_path": "./libVkLayer_khronos_validation.so"|' "$out/linux/VkLayer_khronos_validation.json"
        # The SDK's library carries its debug information (400 MB); not needed for messages.
        command -v strip > /dev/null && strip --strip-debug "$out/linux/libVkLayer_khronos_validation.so"
        echo "linux: $out/linux"
    fi
fi

if [ "$which" != linux ]; then
    f="vulkansdk-windows-X64-$version.exe"
    fetch "https://sdk.lunarg.com/sdk/download/$version/windows/$f" "$f" "$windows_sha"
    # The installer (Qt Installer Framework) carries its components as 7z archives: the first one
    # whose signature header sizes it inside the file holds Bin/ (the layers).
    python3 -I - "$dl/$f" "$dl/layers.7z" <<'PY'
import struct, sys
data = open(sys.argv[1], 'rb').read()
i = 0
while (i := data.find(b"7z\xbc\xaf\x27\x1c", i)) >= 0:
    off, size = struct.unpack_from('<QQ', data, i + 12)
    total = 32 + off + size
    if 32 < total <= len(data) - i and size < 1 << 26:
        open(sys.argv[2], 'wb').write(data[i:i + total])
        sys.exit(0)
    i += 6
sys.exit("no 7z archive found in the installer")
PY
    rm -rf "$out/windows"; mkdir -p "$out/windows"
    files=(Bin/VkLayer_khronos_validation.dll Bin/VkLayer_khronos_validation.json)
    if command -v bsdtar > /dev/null; then extract() { bsdtar -xf "$dl/layers.7z" -C "$out/windows" --strip-components 1 "${files[@]}"; }
    elif command -v 7z > /dev/null; then extract() { 7z e -y -o"$out/windows" "$dl/layers.7z" "${files[@]}" > /dev/null; }
    elif [ -x /mnt/c/Windows/System32/tar.exe ]; then
        extract() { (cd "$out/windows" && /mnt/c/Windows/System32/tar.exe -xf "$(wslpath -w "$dl/layers.7z")" --strip-components 1 "${files[@]}"); }
    else echo "windows: needs bsdtar, 7z or (WSL) Windows' tar.exe to read the SDK's 7z archive" >&2; exit 1; fi
    extract
    rm -f "$dl/layers.7z"
    [ -f "$out/windows/VkLayer_khronos_validation.dll" ] || { echo "windows: the layer was not in the archive" >&2; exit 1; }
    echo "windows: $out/windows"
fi
