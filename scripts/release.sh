#!/usr/bin/env bash
# Builds the release archives into out/release: the self-contained player for Windows and for
# Linux, each with its setup script, the README and BRUTAL's NOTICE.txt, under one top folder
# named oni-brutal (extracting a newer release over an older one keeps the paths the same).
#   scripts/release.sh [VERSION]      (VERSION defaults to the date of HEAD's commit)
# The build compiles against the game's assemblies, so it runs where the game is installed
# (ONI_GAME_DIR); nothing of the game goes into the archives.
set -euo pipefail
source "$(dirname "$0")/env.sh"
version="${1:-$(git -C "$root" log -1 --format=%cd --date=format:%Y.%m.%d)}"
out="$root/out/release"
rm -rf "$out"; mkdir -p "$out"
build() { dotnet build "$1" -c Release -r "$2" --self-contained true -p:OniGameDir="$game" -v q -nologo "-clp:ErrorsOnly;NoSummary"; }

stage() { # stage DIR SETUP: the archive's oni-brutal folder
    mkdir -p "$1/player"
    cp "$root/$2" "$root/README.md" "$root/NOTICE.txt" "$1/"
}

# Windows: OniBrutal.exe with the Restarter.exe the game's App.Restart starts.
build "$root/src/OniBrutal.Host" win-x64
build "$root/src/OniBrutal.Restarter" win-x64
win="$out/windows/oni-brutal"
stage "$win" setup.ps1
cp -r "$root/out/bin/OniBrutal.Host/release_win-x64/." "$win/player/"
rm -f "$win/player/Restarter" "$win/player/"FacadeGen*
cp "$root/out/bin/OniBrutal.Restarter/release_win-x64/Restarter."{exe,dll,deps.json,runtimeconfig.json} "$win/player/"

# Linux: OniBrutal with the Restarter script.
build "$root/src/OniBrutal.Host" linux-x64
lin="$out/linux/oni-brutal"
stage "$lin" setup.sh
cp -r "$root/out/bin/OniBrutal.Host/release_linux-x64/." "$lin/player/"
rm -f "$lin/player/"FacadeGen*
chmod +x "$lin/setup.sh" "$lin/player/OniBrutal" "$lin/player/Restarter"

# zip for Windows; tar.gz for Linux, which keeps the executable bits.
(cd "$out/windows" && python3 -I -c "
import os, sys, zipfile
with zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for d, _, files in os.walk('oni-brutal'):
        for f in files: z.write(os.path.join(d, f))
" "$out/oni-brutal-$version-windows-x64.zip")
tar -C "$out/linux" -czf "$out/oni-brutal-$version-linux-x64.tar.gz" oni-brutal
ls -la "$out"/*.zip "$out"/*.tar.gz
