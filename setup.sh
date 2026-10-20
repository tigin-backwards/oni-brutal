#!/usr/bin/env bash
# One-time setup for playing Oxygen Not Included on oni-brutal (Linux).
#
# Finds the game through Steam's libraries, builds the player into out/player when run from a
# clone of the repository (a release already has it, next to this script), and prints the line
# to paste into the game's Steam launch options. Run it again after updating.
#   ./setup.sh [GAME_DIR]
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

say() { printf '%s\n' "$*"; }
die() { printf 'setup: %s\n' "$*" >&2; exit 1; }

# The game: the argument, or the first Steam library holding it. Steam lives in one of these
# depending on how it was installed (package, Valve's installer, snap, Flatpak).
find_game()
{
    local steam lib
    for steam in "$HOME/.steam/steam" "$HOME/.local/share/Steam" "$HOME/snap/steam/common/.local/share/Steam" \
                 "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"; do
        [ -f "$steam/steamapps/libraryfolders.vdf" ] || continue
        while IFS= read -r lib; do
            [ -d "$lib/steamapps/common/OxygenNotIncluded/OxygenNotIncluded_Data" ] && { echo "$lib/steamapps/common/OxygenNotIncluded"; return; }
        done < <(sed -n 's/^[[:space:]]*"path"[[:space:]]*"\(.*\)"[[:space:]]*$/\1/p' "$steam/steamapps/libraryfolders.vdf")
    done
}

game="${1:-$(find_game)}"
[ -n "$game" ] || die "Oxygen Not Included wasn't found in any Steam library; install it, or pass its directory: ./setup.sh /path/to/OxygenNotIncluded"
[ -d "$game/OxygenNotIncluded_Data/Managed" ] || die "$game isn't an Oxygen Not Included install (no OxygenNotIncluded_Data/Managed)"
[ -x "$game/OxygenNotIncluded" ] || die "$game is the Windows build; on Linux, Steam has to install the Linux one"
say "Game: $game"

if [ -x "$here/OniBrutal" ]; then
    player="$here" # a release
else
    # A .NET SDK of the version global.json pins (a later patch release works too): one already
    # installed, or one installed into ~/.dotnet (no root needed).
    want=$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([0-9]*\.[0-9]*\.[0-9]\)[0-9]*".*/\1/p' "$here/global.json")
    dotnet=""
    for candidate in "$HOME/.dotnet/dotnet" "$(command -v dotnet || true)"; do
        [ -n "$candidate" ] && [ -x "$candidate" ] && "$candidate" --list-sdks 2>/dev/null | grep -q "^$want" && { dotnet=$candidate; break; }
    done
    if [ -z "$dotnet" ]; then
        say "Installing the .NET SDK ${want}xx into ~/.dotnet..."
        installer=$(mktemp)
        curl -sSfL https://dot.net/v1/dotnet-install.sh -o "$installer" || die "couldn't download the .NET installer"
        bash "$installer" --version "$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$here/global.json")" --install-dir "$HOME/.dotnet" >/dev/null
        rm -f "$installer"
        dotnet="$HOME/.dotnet/dotnet"
    fi
    export DOTNET_ROOT="$(dirname "$dotnet")" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

    say "Building the player (a few minutes the first time)..."
    # Self-contained: it carries its own .NET runtime, so it runs where Steam starts it (a snap's
    # Steam can't see ~/.dotnet).
    "$dotnet" build "$here/src/OniBrutal.Host" -c Release -r linux-x64 --self-contained true -p:OniGameDir="$game" -v q -nologo "-clp:ErrorsOnly;NoSummary" \
        || die "the build failed"
    player="$here/out/player"
    rm -rf "$player"
    cp -r "$here/out/bin/OniBrutal.Host/release_linux-x64" "$player"
    rm -f "$player"/FacadeGen*
fi

say ""
say "Done. In Steam: right-click Oxygen Not Included → Properties → Launch Options, and paste:"
say ""
say "    \"$player/OniBrutal\" --game-tweak exact -- %command%"
say ""
say "Then press Play as usual. To play the original game again, clear the launch options."
