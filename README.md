# oni-brutal

A replacement for the Unity player that Oxygen Not Included ships with, built on
[BRUTAL](https://github.com/ahwootech/BRUTAL) and running on stock .NET 10 CoreCLR. The game's own
managed assemblies load unchanged. They bind to generated `UnityEngine.*Module` facades that
forward to our engine, `src/OniBrutal.UnityEngine`. Windowing, input and OpenGL come from
BRUTAL's GLFW and OpenGL bindings.

The repository has no game code or assets in it. You need your own copy of the Linux build.

## Requirements

- Linux x64 with an OpenGL 4.5 driver.
- The .NET SDK pinned in `global.json` (10.0.402; later patch releases also work). If
  `~/.dotnet/dotnet` exists, the scripts use it.
- The Linux build of Oxygen Not Included. It is expected at `~/projects/oni-linux`; to use another
  location, set `ONI_GAME_DIR` for the scripts and tests, and pass `-p:OniGameDir=...` to
  `dotnet build`.
- `libvpx` (e.g. `apt install libvpx9`) to play the game's videos. Without it, video players
  report an error and show nothing, and three video tests are skipped.
- To use the offline Steam stub: `python3` and binutils (`as`, `ld`).

## Build

```sh
dotnet build OniBrutal.slnx              # debug: engine, host, tools, tests
dotnet build src/OniBrutal.Host -c Release
```

Both builds write to `out/bin/<project>/<config>/`. The host's output holds everything needed to
run: the engine, the BRUTAL native libraries, and the facades in `engine/`. After changing the
engine, rebuild the host, not just the engine project.

`scripts/build-engine.sh [debug|release]` also lays out `out/engine` (the engine and its facades,
without the host). The API conformance check reads from there.

## Run

```sh
scripts/run.sh                 # needs a running, logged-in Steam client
scripts/run-offline.sh         # no Steam: loads tools/SteamStub in place of libsteam_api
ONI_CONFIG=release scripts/run.sh
```

Both scripts point `XDG_CONFIG_HOME` and `XDG_CACHE_HOME` at `out/run-home`, so saves, settings
and mods stay apart from your real profile in `~/.config/unity3d`. Set `ONI_RUN_HOME` to use a
different directory. Any other arguments go to the host. For example, `--log FILE`,
`--headless --frames N`, `--capture FRAME:PATH` and `--max-frame-rate N` are useful in testing,
and Unity's `-screen-width`, `-screen-height` and `-window-mode` flags also work. An unknown
option prints the full list (`scripts/run.sh --help`).

The offline stub reports that you own the base game, with no DLC and no Workshop items. It is a
test double for machines without Steam, not a way to play.

## Verify

```sh
scripts/check.sh
dotnet test tests/OniBrutal.UnityEngine.Tests
```

`check.sh` is the gate before every commit. It builds the solution and runs both test suites.
Then it checks the game's assemblies against the engine's API surface, and runs the host's type
load, asset and binding checks against the installed game. Some tests read shaders and fonts from
the game install, so the install must be present.

## Known differences

Number formatting in a few game paths, `string.GetHashCode` being randomized, and a handful of
smaller gaps are documented as deviations in `docs/ARCHITECTURE.md` (D1–D10). The one players
will notice is D3: local and dev mods ask for a restart at every launch and come up disabled.
Floating-point results are not bit-identical to Mono's, and are not meant to be.

## Documents

- `docs/PLAN.md`: milestones, their exit criteria, and current status.
- `docs/ARCHITECTURE.md`: how the pieces fit together, and the deviations.
- `docs/PARITY.md`: what has been compared against the original player, and how.
- `docs/ENGINE-CONVENTIONS.md`: coding rules for the engine.
- `docs/LICENSING.md`: what we redistribute, and what we rely on the user's install for.
- `tools/UnityProbe`: a mod that records how the original player behaves, so the same
  scenarios can be compared in both players.
