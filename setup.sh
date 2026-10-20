#!/usr/bin/env bash
# One-time setup for playing Oxygen Not Included on oni-brutal (Linux).
#
# Finds the game through Steam's libraries, builds the player into out/player when run from a
# clone of the repository (a release has it in player/ beside this script), and prints the line
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

if [ -x "$here/player/OniBrutal" ]; then
    player="$here/player" # a release
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
    # The output is shown only if the build fails.
    if ! output=$("$dotnet" build "$here/src/OniBrutal.Host" -c Release -r linux-x64 --self-contained true -p:OniGameDir="$game" -v q -nologo 2>&1); then
        printf '%s\n' "$output" >&2
        die "the build failed"
    fi
    player="$here/out/player"
    rm -rf "$player"
    cp -r "$here/out/bin/OniBrutal.Host/release_linux-x64" "$player"
    rm -f "$player"/FacadeGen*
fi

# Steam in a sandbox runs the game where only some of the home folder can be read: a Flatpak
# Steam sees its own ~/.var/app folder, a snap Steam no hidden folders. The player is installed
# into the sandbox's own folder then (ONI_STEAM_SANDBOX=flatpak|snap|none overrides the guess
# made from where the game is installed).
sandbox="${ONI_STEAM_SANDBOX:-}"
if [ -z "$sandbox" ]; then
    case "$game" in
        "$HOME/.var/app/com.valvesoftware.Steam/"*) sandbox=flatpak ;;
        "$HOME/snap/steam/"*) sandbox=snap ;;
        *) sandbox=none ;;
    esac
fi
case "$sandbox" in
    flatpak) inside="$HOME/.var/app/com.valvesoftware.Steam/data/oni-brutal/player" ;;
    snap) inside="$HOME/snap/steam/common/oni-brutal/player" ;;
    *) inside="" ;;
esac
if [ -n "$inside" ]; then
    rm -rf "$inside"; mkdir -p "$(dirname "$inside")"
    cp -r "$player" "$inside"
    player="$inside"
    say "Steam runs as a $sandbox here: the player is installed where it can see it ($player)."
fi

launch="\"$player/OniBrutal\" --game-tweak exact -- %command%"
# On the clipboard where a clipboard tool exists: copied from a terminal, a long line can pick
# up its wrapping.
copied=""
for tool in "wl-copy" "xclip -selection clipboard" "xsel --clipboard --input"; do
    command -v "${tool%% *}" >/dev/null 2>&1 && printf '%s' "$launch" | $tool 2>/dev/null && { copied=1; break; }
done
say ""
if [ -n "$copied" ]; then
    say "Done. The launch options are on your clipboard. In Steam: right-click Oxygen Not Included →"
    say "Properties → Launch Options, and paste them. They are:"
else
    say "Done. In Steam: right-click Oxygen Not Included → Properties → Launch Options, and paste:"
fi
say ""
say "    $launch"
say ""
say "Then press Play as usual. To play the original game again, clear the launch options."
