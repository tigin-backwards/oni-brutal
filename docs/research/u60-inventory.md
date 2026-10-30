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
| UI packages | One TMP member (`SetCharArray(ReadOnlySpan<char>)`), being implemented from the spec. |
| Game tweaks | Static review against the decomp in progress; runtime check needs a U60 player. |
| Linux build | Waiting for the 761015 Linux depot download. |
| Main menu and a save on U60 | Not yet run. |
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
- **Vulkan as the Linux default:** the Windows data still carries OpenGL programs
  (`--check-shaders` passes). Whether the Linux data still does needs the Linux build.
- **Linux restarter fix:** to compare with `src/OniMoono.Restarter` once the Linux build is here.

## Game tweaks

See `u60-tweaks-*.md` per area (animation, spatial, overlays, sim, save) once the reviews land.
