# U60: what changed for Moono

U60 is the game's November 2026 quality-of-life update. It is in public testing as build 761015 on
Steam's `public_testing` branch, and is built with Unity 6000.3.17f1 (U59: 744825, Unity
6000.3.5f2). This branch (`u60`) supports U60 only. Master stays on U59 until U60 ships, then
`u60` merges into it.

Sources:
- Klei's decompiled game code for both builds in oni-versioning (`aa9baa00` U59, `4a63c523` U60).
- The public test's patch notes.
- `tools/AssemblyDiff`, `tools/ApiCheck` and the player's `--check-*` modes run against the Windows
  U60 install.

## Status

| Area | State |
|---|---|
| Native data layouts | Done: 6000.3.17f1 type trees. Asset, bind and shader checks pass on Windows U60. |
| Version check | Done on `u60`: a Moono build names the game update it needs. Master: to do. |
| Engine API | Done: ApiCheck back to the 13 known UI Toolkit members. |
| UI packages | Done: `TMP_Text.SetCharArray(ReadOnlySpan<char>)` from the spec; package ApiCheck 0 missing. |
| Game tweaks | Static review done (2 fixed, overlays left out where U60 does their work); runtime checks need a U60 player. |
| Linux build | Done: frozen at `~/projects/oni-linux-761015`, DLC content copied (not linked) from the frozen Windows copy, test install `out/local-tools/u60orig`. |
| Main menu and a save on U60 | Linux: main menu reached, no errors. Save: in the QA verify run. Windows: waiting for a time the user isn't playing. |
| Performance | Not yet measured. |
| QA and e2e on U60 | Not yet run. |

## The build

- **Development build:**
  - The public test is a Unity development build. Every managed assembly ships with a `.pdb`, and
    `boot.config` gains `player-connection-*` lines.
  - Unity compiles its assertion and profiler calls into such builds only. The game, uGUI and
    Timeline therefore call `Debug.Assert`, `Assertions.Assert` and `Profiling.Profiler`. A release
    build of U60 may not.
  - In the original, `Debug.isDebugBuild` is probably true on this build; ours reports false. To be
    measured, along with whether any game code branches on it.
- **`boot.config`:** the graphics threading mode goes from 4 to 6, the native graphics-jobs line is
  gone, and `gc-max-time-slice=3` is new (Unity's incremental GC). None of these is read by Moono:
  CoreCLR's background GC replaces Unity's.
- **Unoptimised game code:** the public test's `Assembly-CSharp` is compiled without
  optimisation. It is full of `nop`s and keeps comparisons in locals; `SaveLoader.Save` alone
  has 75 `nop`s, and U59 had none. Most of the decomp diff in untouched methods is this compiler
  shape. Transpilers must be checked against U60's IL, not the C#. Frame rates measured on the
  public test understate a release build of U60, for vanilla and Moono alike, since both run the
  same game code.
- **New Unity modules:** `UnityEngine.AMDModule`, `UnityEngine.NVIDIAModule` (FSR2 and DLSS) and
  `UnityEngine.InsightsModule`. Nothing in the game references them.

## Native data layouts

Six native classes changed layout between 6000.3.5f2 and 6000.3.17f1. Two of them are read at start-up:

- **`PlayerSettings`:** gained `metalUseMetalDisplayLink`.
- **`PhysicsManager`:** gained `m_IncrementalStaticBroadphase` (version 22 to 23).

The others (`Terrain`, `GraphicsStateCollection`, `ArticulationBody`, `VisualEffectAsset`) are not
in the game's data. With the 6000.3.5f2 trees the player stopped in `ProjectSettings.Load` on an
array count read out of step. The branch now carries the 6000.3.17f1 trees from AssetRipper's
TypeTreeDumps, compacted by `tools/TypeTrees/compact.py` (it reproduces the old file byte for byte).

## Version check

`GameVersionCheck` (host) compares the Unity version in the `globalgamemanagers` header with the
type trees the build carries, before anything loads. On a mismatch it names both game updates and
the build to get. The message goes to the log and stderr, and on Windows to a message box, since a
start from Steam has no console. Unity version stands for game update because a different Unity
version is exactly what breaks the asset reader.

## Engine API

Members U60 needs that U59 did not:

- `Debug.Assert` (three overloads) and `Debug.AssertFormat`: a false condition is logged as
  `LogType.Assert`.
- `Assertions.Assert.IsTrue`: the same, with `raiseExceptions` off.
- `Profiling.Profiler.BeginSample(string)` and `EndSample()`: no-ops.
- `ProfilerRecorder.LastValue`: `PerformanceCaptureMonitor` reads it for "Gfx Used Memory", "Draw
  Calls Count" and "SetPass Calls Count", which report 0 here as before.
- `Resources.UnloadAsset`: `VistaLayers` unloads vista textures it no longer shows. Here the data
  stays loaded, so the behaviour is the same but the memory is not returned. Candidate for a real
  unload if memory matters.

Other CoreModule additions (`Random.onUnitCircle`, `JobHandle` equality, `SpriteRenderer` shader
user values, `SystemInfo.SupportsTypedUAVShaderLoadStoreOnGraphicsFormat`) are not referenced by the
game.

## UI packages

- **uGUI:** no change.
- **TextMeshPro:**
  - `TMP_Text.SetCharArray(ReadOnlySpan<char>)` is new, and U60's `LocText.SetContent` uses it for
    most UI text (spec: `docs/cleanroom/textmeshpro.md`, U60 section).
  - The shipped `TMP_InputField` now calls `GetFocus`, `GetWindowRect` and `ClientToScreen`,
    probably to place the IME candidate window. To be measured on Windows before anything is
    specified (relates to issue #30).

## Host and platform items from the patch notes

- **Steam's loose-file mod format:**
  - `KMod.Steam` now installs a Workshop item from a folder (`Directory`) when it has one, else
    from the legacy zip. It re-downloads corrupt items.
  - Our hooks still match: `KMod.DLLLoader.LoadDLLs` has one `Assembly.LoadFrom` (ModAccess's
    transpiler), and `KMod.Local.Subscribe` is unchanged (ModVersionStamp).
  - A loose-file Workshop mod still needs a runtime check.
- **VYaml 1.2 to 1.4:** loaded from the game's `Managed` folder with no special handling. POI
  templates now go through it, so new-game worldgen covers it at runtime.
- **Incremental GC:** a Unity player setting plus `gc-max-time-slice`. The game's code adds no
  `GarbageCollector` calls. Not applicable to CoreCLR.
- **IMGUI developer tools on Linux:** to be checked with the clean-room IMGUI once the Linux build runs.
- **Vulkan as the Linux default:** the data of both builds still carries OpenGL programs.
  `--check-shaders` compiles all 120 shaders with no errors on the Linux U60 data, and passes on
  the Windows data too. Moono keeps rendering with OpenGL.
- **Linux restarter fix:** `App.Restart` still starts `Restarter` beside the game with the same
  arguments (only the compiler shape changed), so Klei's fix is inside their Restarter binary
  (12.5 MB in U59, 2.3 MB in U60). Moono's own `src/OniMoono.Restarter` replaces it; no change.
- **Plugin layout (Linux):**
  - U60's Linux build moves its plugins into `Plugins/x86_64/` and keeps `libsteam_api.so` in
    `Plugins/AnyCPU/`. Moono looked in `x86_64` only, so on a real install SteamAPI.Init would
    have failed and the game quit. AnyCPU is now searched too (`GameLayout`).
  - FMOD is the logging build, `libfmodstudioL.so` (`fmodstudioL.dll` on Windows); all 571 of
    its entry points resolve.
  - The Windows build adds `Plugins/ARM64/` (Windows on ARM).

## Game tweaks

Each area's review is in `u60-tweaks-<area>.md` (anim, spatial, overlays, sim, save). Each table
lists the game members a tweak touches, what U60 changed (with decomp line numbers), the verdict,
and the runtime check that confirms it once a U60 player runs.

| Area | Tweaks | Verdict |
|---|---|---|
| Animation | fast-anim-batches, fast-anim-cleanup, fast-anim-events, fast-symbol-overrides | fast-anim-batches **updated**. U60 renamed `GetPostProcessingParams` to `GetCustomShaderParams`, so the tweak had switched itself off; a missing write-path method now stops the install. The other three are unchanged. |
| Spatial | fast-partitioner-cleanup, fast-offset-tables, off-world-fallers, fast-particle-collision | Unchanged. Every transpiler anchor still matches U60's unoptimised IL. `OffsetTracker` now keeps a validity flag, cleared only from paths the tweak already patches. |
| Overlays | fast-overlays (power, temperature, targets), fast-overlay-textures | **Retire candidates.** U60 caches circuit answers in `Power.Update`, colours only visible infrared visualizers, moved the target helpers, and `MultipleRenderTargetProxy` manages its copies. On U60 fast-overlays would have stopped start-up (ambiguous `AddTargetIfVisible`, a missing handler). Each part now installs only where the U59 shape exists and logs why it is left out. Retire after the performance comparison. |
| Simulation | fast-sublimation, fast-conveyor-paths, offscreen-conveyors, brain-rate, brain-rate-allowance, slow-reachability, fast-jobs | Unchanged. The differences are compiler shape or the event-system move, which none of them depends on. |
| Save | fast-save-type-checks, background-save, fast-save-compression, fast-serialization, no-save-gc | Unchanged for U60. Two hardening fixes: fast-serialization checks its KSerialization members at install, and background-save now waits for a save in progress before the load screen's cloud/local migration (a gap that U59 had too). |

Open checks once a U60 player runs:
- each review's runtime checks (`ONI_TWEAK_VERIFY=1` counts, no "left out" or "off" lines where
  none is expected);
- the overlay frame times with the tweaks off, to confirm Klei's versions match ours;
- `TMP_Text.SetCharArray` with unchanged text: ours now skips the update when the characters
  equal the current text. Whether Unity's package does the same (layout and events) is to be
  measured in the original.
