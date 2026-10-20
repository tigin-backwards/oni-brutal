# Development

How to build, run and test oni-brutal from source. If you just want to play, the
[README](../README.md) is all you need.

## Requirements

- Linux x64 with an OpenGL 4.5 driver. The Windows test player is built from Linux or WSL, and
  runs on Windows x64 with an OpenGL 4.5 driver and the .NET 10 runtime.
- The .NET SDK pinned in `global.json` (10.0.402, or a later patch release). The scripts use
  `~/.dotnet/dotnet` if it exists.
- The Linux build of Oxygen Not Included, expected at `~/projects/oni-linux`. To use another
  location, set `ONI_GAME_DIR` for the scripts and tests, and pass `-p:OniGameDir=...` to
  `dotnet build`.
- `python3` and binutils (`as`, `ld`), for the offline Steam stub.
- Optionally `libvpx`, for the one test that compares our VP8 decoder against it. The test is
  skipped without it; the player decodes the game's videos itself.

## Build

```sh
dotnet build OniBrutal.slnx              # debug: engine, host, tools, tests
dotnet build src/OniBrutal.Host -c Release
```

Both write to `out/bin/<project>/<config>/`. The host's output has everything needed to run: the
engine, BRUTAL's native libraries, and the Unity module facades in `engine/`. The host carries
its own copy of the engine, so after changing the engine, rebuild the host too.

`scripts/build-engine.sh [debug|release]` also lays out `out/engine` (the engine and its facades
without the host), which the API conformance check reads.

## Run

```sh
scripts/run.sh                 # needs a running, logged-in Steam client
scripts/run-offline.sh         # no Steam: a stub stands in for libsteam_api
ONI_CONFIG=release scripts/run.sh
```

Both scripts keep saves, settings and mods in `out/run-home` (or `ONI_RUN_HOME`), away from your
real profile in `~/.config/unity3d`. Any other arguments go to the host. Useful ones for testing
are `--log FILE`, `--headless --frames N`, `--capture FRAME:PATH` and `--max-frame-rate N`.
Unity's `-screen-width`, `-screen-height` and `-window-mode` work too. `scripts/run.sh --help`
lists everything.

The offline stub says you own the base game, with no DLC and no Workshop items. It's for testing
on machines without Steam, not for playing.

To play through Steam like a player does, run `./setup.sh`. It builds a self-contained player
into `out/player` and prints the launch options line (`OniBrutal --game-tweak exact -- %command%`).

## Windows

```sh
scripts/build-windows.sh       # out/win: OniBrutal.exe, Restarter.exe and the Steam stub
scripts/run-windows.sh         # from WSL: a test run on the Windows install
```

The Windows player uses the Windows build's own data, whose shaders include OpenGL programs
alongside the Direct3D ones. `run-windows.sh` copies `out/win` to `%TEMP%\oni-brutal` and starts
it on the Steam install (`ONI_WIN_GAME_DIR` to change that). Test runs stay away from your game
data, Steam account and speakers:
- `--run-home` gives the game its own Documents, AppData and LocalLow under
  `%TEMP%\oni-brutal\run-home` (`ONI_RUN_HOME`)
- the Steam stub replaces `steam_api64.dll`
- `--audio-wav` writes the game's sound to `fmodoutput.wav` there

To compare against the original Windows player under the same isolation:

```sh
scripts/windows-run-home.sh RUN_HOME SAVE out/bin/PerfProbe/release:-2037174645
ONI_RUN_HOME='C:\...' scripts/original-windows.sh -screen-width 1024 -screen-height 768 -screen-fullscreen 0
ONI_RUN_HOME='C:\...' scripts/run-windows.sh -screen-width 1024 -screen-height 768 -screen-fullscreen 0
```

`windows-run-home.sh` prepares a run home with a single colony, which the main menu resumes on
its own, and mods enabled (`tools/PerfProbe` holds speed 3 and logs frame rates). The original
runs from a renamed copy of the install (`ORIG_DIR`, default `E:\Development\BrutalNotIncluded`)
with BepInEx 6 and `tools/OriginalIsolation`, which gives it the same separate data folders and
audio file. Its first setup copies BepInEx from an existing install (`ONI_BEPINEX_SOURCE`). The
script has the details.

## Testing

```sh
scripts/check.sh               # before every commit
scripts/qa.sh                  # the full harness
```

`check.sh` builds the solution and runs both test suites. Then it checks the game's assemblies
against the engine's API, and runs the host's type, asset and binding checks against the
installed game. Some tests read shaders and fonts from the game, so it needs to be installed.

`qa.sh` runs the unit stage, then scripted play sessions compared against reference captures
(menu, new game, colony, every main screen and overlay, typing, save, load, resize). It finishes
with a large colony in verify mode with every tweak, and a performance check. The large-colony
stages need local setup in `out/local-tools/qa.env`, and are skipped without it.

## Known differences

A few number formats in game code and a handful of smaller gaps are documented as deviations in
`docs/ARCHITECTURE.md` (D1–D12). Three are fixed by small patches the host makes to game code,
the only ones it makes:
- D1: state-machine values in saves from the original player loaded at their defaults
- D3: local mods disabled at every launch
- D11: mods built against publicized game assemblies failing CoreCLR's access checks

D12 adjusts the game's Harmony instead: a transpiler's `callvirt` to a static method becomes a
call, as Mono runs it.

Floating-point results aren't bit-identical to Mono's, and aren't meant to be.

## Documents

- `docs/PLAN.md`: milestones, their exit criteria, and current status
- `docs/ARCHITECTURE.md`: how the pieces fit together, and the deviations
- `docs/PARITY.md`: what has been compared against the original player, and how
- `docs/PERFORMANCE.md`: where the frame time goes, and what each change bought
- `docs/ENGINE-CONVENTIONS.md`: coding rules for the engine
- `docs/LICENSING.md`: what we redistribute, and what we rely on the game install for
- `docs/GAME-UPDATES.md`: checks to run again when the game updates
- `tools/UnityProbe`: a mod that records how the original player behaves, so the same scenarios
  can be compared in both players
