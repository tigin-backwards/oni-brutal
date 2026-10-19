# Plan

Goal: Oxygen Not Included's original managed code and data running on .NET 10 CoreCLR with
Unity's engine replaced by our implementation on BRUTAL, at close to parity with the original
(float bit-exactness excluded). Architecture: `docs/ARCHITECTURE.md`. Evidence:
`docs/research/`.

## Principles

- **Unmodified game.** Game IL and data are never patched. Third-party libraries that cannot
  run on CoreCLR are ported from their upstream source (Steamworks.NET, *ARCHITECTURE §2.1*).
- **Unity's behaviour is the spec.** When unsure how Unity behaves, find out (documentation,
  the shipped uGUI/TMP IL that encodes Unity's contracts, the data), write it down in the code
  comment, then implement. No guessing that "probably works".
- **Complete or absent.** An engine member is implemented fully or not at all; missing members
  surface as `MissingMethodException` and in `tools/ApiCheck`. No placeholder throws, no
  silent no-ops (legitimate player no-ops are documented as such).
- **Every milestone ends verified**: its exit criteria are tests or runs that pass, recorded in
  the commit that closes the milestone.
- **Coherent commits**, each building and passing tests.

## Environment

- .NET SDK 10.0.402 at `~/.dotnet` (`DOTNET_ROOT=$HOME/.dotnet`).
- Game: `~/projects/oni-linux` (Linux build, Unity 6000.3.5f2, base game, no DLC).
- GL: WSLg Mesa d3d12 — `GALLIUM_DRIVER=d3d12 MESA_D3D12_DEFAULT_ADAPTER_NAME=NVIDIA` gives
  GL 4.6 core on the RTX 3090; llvmpipe (GL 4.5) is the software fallback for CI-like runs.
- **Steam:** the game quits with code 2 if `SteamAPI.Init()` fails (as the original does). Runs
  past the launch scene require a Linux Steam client, logged in with an account that owns ONI,
  running in the same environment. Until then, runs are verified up to that checkpoint.
- Dev settings for runs: `kprivacyprefs.yaml` with `disableDataCollection: true`,
  `ignorekcrashreporter.txt` in the CWD, and `settings.yml` automation flags (`devBootSmoke`,
  `devAutoWorldGen`, `autoResumeGame`, `devQuitAfterLoadingSave`) for scripted checkpoints.

## Milestones

### M0 — Foundations

1. Solution skeleton per *ARCHITECTURE §8*: `Directory.Build.props`, `Directory.Packages.props`
   (central versions: `Ahwoo.BRUTAL.*` 2026.10.x, xunit, compat packages), `.editorconfig`,
   analyzer policy, `global.json` pinning SDK 10.0.402.
2. `tools/FacadeGen`: reads Unity's original module assemblies to map type → module, intersects
   with the types implemented in `OniBrutal.UnityEngine`, and emits one facade project per
   module (`UnityEngine.CoreModule` … plus `UnityEngine.dll`), version 0.0.0.0, unsigned.
3. `tools/ApiCheck` wired as `scripts/apicheck.sh` over the full consumer set
   (Assembly-CSharp, firstpass, UnityEngine.UI, Unity.TextMeshPro, Unity.Timeline, FMODUnity*,
   ImGui.NET, Steamworks.NET), writing `out/apicheck.json` and a summary by module.
4. `OniBrutal.Host`: game ALC with the resolution policy, native library resolver, eager
   preload, compat packages, code pages, runtimeconfig switches, CLI flags
   (`--game`, `--headless`, `--frames`, `--log`, `--screenshot`).
5. Steamworks.NET 20.1.0 port in `src/ThirdParty/Steamworks.NET` (pinned upstream commit,
   minimal documented diff).
6. A first engine slice large enough to load every game type: all engine **types** referenced
   (with base types, interfaces, enums and attribute classes complete) so that every type in
   every game assembly loads.

**Exit:** `dotnet build` clean; host loads all game assemblies and `GetTypes()` succeeds on
every one (B7 path); ApiCheck reports zero missing **types**; unit test project runs.

### M1 — Assets

1. SerializedFile v22 reader (header, types, objects, script types, externals), UnityFS reader
   (LZMA + LZ4/LZ4HC, blocks-info at end), `.resS`/`.resource` streams.
2. Type-tree runtime: load the 6000.3.5f2 release trees (vendored, compressed), typed walker.
3. Managed layout builder from reflection (Unity serialization rules), shared by
   deserialization, cloning and JsonUtility.
4. Corpus test: every object of every file (+ `hires_base_bundle`) consumes exactly its byte
   size; ~18.8k MonoBehaviours across ~700 script classes resolve their layouts.
5. Object binders for the native classes needed by M2 (GameObject, Transform, RectTransform,
   MonoBehaviour/ScriptableObject header, TextAsset, MonoScript, Texture2D metadata, Sprite,
   SpriteAtlas, Material, Shader metadata, Mesh, Font, Camera, Canvas, CanvasRenderer,
   CanvasGroup, VideoClip, globalgamemanagers managers: BuildSettings, ResourceManager,
   TagManager, TimeManager, InputManager, PlayerSettings, QualitySettings, GraphicsSettings,
   MonoManager, ShaderNameRegistry).

**Exit:** corpus test green; binders unit-tested against known values from the asset survey.

### M2 — Object model, player loop, headless boot

1. `UnityEngine.Object` / `GameObject` / `Component` / `Transform` / `Behaviour` /
   `MonoBehaviour` / `ScriptableObject` with native-side state, fake null, lifecycle messages,
   activation semantics, deferred/immediate destroy, `DontDestroyOnLoad`, Find APIs.
2. Instantiate (serialization-shaped clone with remap and callbacks).
3. Player loop, Time, coroutines (all yield types the game uses), execution order.
4. Scenes (`SceneManager.LoadScene` single mode with Unity's next-frame completion), Resources
   (`Load`, `LoadAll`, `FindObjectsOfTypeAll`, `UnloadUnusedAssets`,
   `GetBuiltinResource`), AssetBundles.
5. Application, Debug (logger, handlers, `logMessageReceived`, thread rules), Player.log,
   SystemInfo, Screen (headless values), Input (headless: no events), PlayerLoop hooks used by
   uGUI.
6. Math and value types (Vector*, Quaternion, Matrix4x4, Mathf, Random, Color*, Rect, Bounds,
   AnimationCurve, Gradient, LayerMask, …), Unity.Collections NativeArray surface used by the game.
7. FMOD: whatever FMODUnity needs to initialize and load banks (sound plays through FMOD's own
   output).

**Exit:** `--headless` run: launchscene → LaunchInitializer → (with Steam) global prefabs,
`Global.Awake`, `Assets` async loaders, `InitializeCheck` → frontend loads →
`MainMenu.OnSpawn` with `devBootSmoke` exiting 0; zero exceptions logged. Without Steam: the
run reaches `SteamManager` and exits with code 2, exactly like the original.

### M3 — Graphics

1. `OniBrutal.Platform`: GLFW window + GL 4.5 core context, events, vsync, resize.
2. `OniBrutal.Graphics`: GL device (buffers, textures incl. DXT/BC upload, RTs/MRT, FBOs,
   samplers, state), Unity shader runtime (SerializedShader parse, LZ4 blobs, GLSL split,
   variant selection, parameter binding, material-driven render state), program cache.
3. Engine rendering: Texture2D/RenderTexture/Texture2DArray/Texture3D, Mesh, Material,
   MaterialPropertyBlock, Shader globals/keywords, Camera rendering (culling, queues, sorting,
   callbacks, `OnRenderImage`, `CommandBuffer`, `SetTargetBuffers`), `Graphics.DrawMesh`/
   `Blit`/`BlitMultiTap`, `GL` immediate mode, MeshRenderer/SpriteRenderer, built-in constants.
4. Debug screenshot capture (`--screenshot`).

**Exit:** boot logo canvas renders (needs M4 canvas basics for the logo: do the minimal canvas
path here or merge with M4); a shader test scene renders every game shader's default variant
without GL errors; screenshots match expectations.

### M4 — UI and input

1. RectTransform (anchors, pivots, driven properties, `DrivenRectTransformTracker`, dimension
   change callbacks), Canvas (render modes, scaler data, sorting, nested canvases,
   `willRenderCanvases`, `ForceUpdateCanvases`), CanvasRenderer (mesh/material/texture, clip
   rects, `cull`, alpha), CanvasGroup, `RectTransformUtility`, canvas batching and drawing.
2. Shipped uGUI and TMP run unchanged: EventSystem, StandaloneInputModule, VirtualInputModule,
   layout groups, masks, scroll rects; TMP static font atlases, sprite assets, styles.
3. Input: Unity `KeyCode` mapping, legacy Input Manager axes from data, mouse/scroll, text
   input, cursor textures, clipboard, focus.
4. `Unity.InternalAPIEngineBridge.004` (engine-side bridge TMP calls into) reimplemented.
5. Animator for UI controllers (AnimatorController + AnimationClip curves).

**Exit:** main menu renders matching a reference screenshot of the original; menus navigate
(options, load screen, new game flow up to the destination screen) by mouse and keyboard.

### M5 — Gameplay

1. New game: worldgen (threaded, SimDLL settle) → backend scene → `SaveLoader` → Sim running.
2. World rendering: property textures, TextureLerper cameras, MRT pass, liquid command buffer,
   lighting composite, overlay camera chain and post-FX, KAnim batches (float data textures +
   VTF), terrain/tiles/backwall/gas/liquid, status items, placers, GL visualizers.
3. Tools, overlays, screens, timelapse capture (`ReadPixels` + `EncodeToPNG`).
4. Save → quit to menu → load → continue.
5. Hi-res texture bundle streaming (`StreamedTextures`).

**Exit:** scripted run: new colony, play 10 cycles at speed 3 with building/digging, save,
reload, continue; zero exceptions; screenshots at fixed checkpoints reviewed against the
original.

### M6 — Remaining modules and parity

1. VideoPlayer (EBML demux + libvpx VP8, `Hidden/VideoDecode`), intro and tutorial videos.
2. ParticleSystem (modules enabled by the game's prefabs), legacy `UI.Text`/`Font`/`TextGenerator`,
   TextCore `FontEngine` (FreeType) for dynamic atlases, `UnityWebRequest` (MOTD, crash report),
   `ImageConversion`, `ScreenCapture`, `JsonUtility`, Physics raycast, IMGUI surface, `Display`,
   `QualitySettings`, fullscreen/resolution switching.
3. Parity sweep: every main-menu option, every overlay, every tool, settings persistence,
   language switching (font swap), performance profiling against the original.

**Exit:** a parity checklist (`docs/PARITY.md`, written at the start of M6 from the game's
screens and features) fully ticked or with documented deviations only.

### M7 — Render thread (done 2026-10-17)

Why: on large colonies the main thread spends about 37% of its time issuing GL work (DeCrew,
cycle 17628: ~1700 draws and ~2 MiB of texture uploads per frame), in series with game code.
Unity records on the main thread and executes on a render thread; we execute inline, which is
why CoreCLR's faster game code does not show as frame rate (26 fps against the original's 26).
Profile and counters: `--frame-stats` (draws, binds, uniforms, uploads).

Done first (2026-10-16), each measured on DeCrew: bindings cache, uniform value cache, texture
uploads through a pixel-unpack ring, vertex array per mesh layout and `glBindTextureUnit`. They
remove ~45k GL calls per frame but leave render time at ~13 ms: timing the device on Mesa d3d12
(WSL) shows `Draw` at ~12.5 ms per frame, of which draw calls 3.9 ms with single calls stalling
2–3 ms (the driver waiting for the GPU), texture uploads 2.3 ms, uniforms 1.2 ms, texture binds
0.9 ms. The stalls are a property of this driver; the same measurement on a native driver
decides how much a render thread gains there.

Design: `RenderDevice` calls become commands in a per-frame list, executed by a GL thread that
owns the render context (the presenter thread or a new one) while the main thread runs the next
frame. Everything a command reads is captured at record time: resolved uniform values and
texture/mesh references with content versions (texel and vertex data copied into the upload
rings when their version changes, as uploads do now), render state, targets. Sync points wait
for the GL thread: `ReadPixels`/captures, `GetPixels` of render textures, shader warmup, device
caps. One frame of latency at most, as Unity's render thread.

Exit: identical captures and probe output in the parity scenarios; DeCrew frame time bounded by
the slower thread (estimate ~25 ms, from ~38).

Result (`GlWorker`, ARCHITECTURE §3.5): DeCrew 34.6 fps (from 25.8; the original 26), main-thread
render 6.2 ms (recording) and present 0.2 ms (from 13.8 and 3.3); Regrettable Dimension played
28k frames at speed 3: 36.0 fps (from 25.6), no exceptions, no white-out. Paused captures are
identical to before; liquids (Cool Abyss) as before. Texture uploads no longer go through the
persistent pixel-unpack ring: with the GPU a frame or two behind the render thread, staged
uploads corrupted background textures under Mesa's d3d12 driver (white-out probe: three events
per run, with or without explicit flushing; none with a full wait per frame or with uploads from
memory). The uniform ring is unaffected in every run. One diagnostic run that made the main thread
wait for the GL thread every frame, on a build still staging uploads through that ring, ended in
a segmentation fault at frame ~19k; with uploads from memory two such runs passed 24k frames
without one, and no run of the shipping configuration has crashed.

### M8 — Windows (started 2026-10-17)

Findings: the Windows build (`OxygenNotIncluded.exe`, Steam) is the same game build as the
Linux one (KleiVersion.ChangeList 744825); its assemblies differ only in a Windows-only field
(`Global.saveFolderTestResult`) and the Epic Online Services library name. Every shader in its
data carries OpenGLCore programs next to the D3D11 ones (platforms 4 and 15), so the OpenGL
device runs the Windows data as it is; no DXBC translation is needed for the game. (The D3D11
programs keep only signature and code chunks, RDEF stripped; translating them, for mod bundles
built only for D3D11, would take names from Unity's per-program parameter tables.)

Work:
- Engine, Windows semantics: `Application.platform` WindowsPlayer; `persistentDataPath`
  `%USERPROFILE%/AppData/LocalLow/<company>/<product>` and the other paths with forward slashes,
  as Unity reports them; `PlayerPrefs` in the registry (HKCU\Software\<company>\<product>, names
  suffixed with Unity's hash); system fonts from the Windows font folder; `SystemInfo` strings.
- Host: natives as `<name>.dll` from `Plugins/x86_64`; the GLFW and libvpx natives for win-x64;
  the game's `Documents\Klei` root, which the game reads through `Environment.GetFolderPath`.
- Test infrastructure: the offline Steam double as a PE DLL (`steam_api64`), written by the
  generator directly (no native toolchain on either side); a run home that keeps test runs out
  of the user's Documents, LocalLow and registry; audio kept off the speakers; runs started
  from WSL through interop.
- Parity: the Linux original is the reference (same build); saves cross-load between the
  platforms; frame rate on the native driver.

Done (2026-10-17): the Windows player (`scripts/build-windows.sh`, `scripts/run-windows.sh`) runs
the Windows build on the native driver (NVIDIA RTX 3090, OpenGL 4.5): type-load, asset, bind and
shader checks pass (119 shaders, every variant compiles); the menu renders as on Linux; DeCrew
loads and plays without exceptions; Regrettable Dimension played 28k frames at speed 3 with no
exceptions and no white-out, seven autosaves, audio to the run home's WAV file; the user's own
Documents, LocalLow and Steam account untouched. FreeType moved to BRUTAL's package on both
platforms (the system library on Linux before); its `long` fields are `CLong`. Presentation
queues two frames (three buffers) with a copy fence instead of a `glFinish` per frame.

Open:
- Frame rate on NVIDIA's OpenGL driver (done): DeCrew ran at ~26 fps against 35 under WSL (Mesa
  d3d12 on the same GPU). `--gpu-profile` put 33 of 46 GPU ms in `Klei/BatchedAnimation`: binding
  a different texture for the vertex stage between draws (each animation batch has its own data
  textures) serialized the draws (~37 µs each whatever their size). With bindless textures
  (`GL_ARB_bindless_texture`) the frame's GPU time is 6 ms and DeCrew runs at ~42 fps, bounded by
  the main thread (~23.5 ms); paused captures match the binding path within run-to-run variation.
- Videos on Windows (done): there is no system libvpx there and no BRUTAL package for it, so the
  engine decodes VP8 itself, a port of RFC 6386's reference decoder; every frame of the game's
  18 clips decodes to the same planes as libvpx, which is now only the tests' reference (the
  dependency is gone on Linux too).
- Parity runs: save cross-load between the platforms, menus and options, mods, the Windows
  original as reference where it can run without the user's own data and account.

### M9 — Without Unity's packages (started 2026-10-18; stages 1–4 done 2026-10-18)

**Status:** this project's uGUI, TextMeshPro and Timeline are written (`src/UnityEngine.UI`,
`src/Unity.TextMeshPro`, `src/Unity.Timeline`) and are what the host loads by default, from
`packages/` next to it; `--shipped-ui-packages` loads Unity's instead (the differential runs'
reference). Their tests pass against both the shipped packages and ours (47 uGUI, 71
TextMeshPro and Timeline), ApiCheck finds nothing missing, and the differential runs differ only
where two shipped runs do or by float rounding (`docs/PARITY.md`). Frame rates (Windows, PerfProbe,
speed 3, runs alternated; the machine was busier than for the 48–56 fps measurement, so absolute
figures are lower, and that build measured the same now): DeCrew 35.3–36.3 fps with ours
against 32.6–32.9 with the shipped packages; U57 11.7–12.6 against 12.1–12.3, equal within the
spread.

The goal is that nothing tied to Unity is loaded. Three Unity packages loaded from
`Managed/`:
- **uGUI** (`UnityEngine.UI.dll`);
- **TextMeshPro** (`Unity.TextMeshPro.dll`);
- **Timeline** (`Unity.Timeline.dll`), which is referenced but has no content in the game.

All three are under the Unity Companion License. They are replaced by this project's
assemblies of the same names, built into the host's `packages/` folder, which `AssemblyPolicy`
prefers over `Managed/`.

**What has to be kept:**
- **What the game calls:** 83 uGUI types (281 member references) and 34 TextMeshPro types
  (118), as `tools/Inventory` reports.
- **What it derives from:** Klei's classes derive from 15 of these types, for example `LocText`
  from `TextMeshProUGUI`, `KInputTextField` from `TMP_InputField`, `KScrollRect` from
  `ScrollRect`, and a `PointerInputModule`. Their virtual and protected members, and the order
  in which those are called, are part of the contract.
- **Serialized layout:** prefabs serialize these components by field layout, read through the
  managed type. Field names, types and order must match the shipped assemblies.
- **The shipped packages' calls into the engine:** they currently exercise the engine's
  internal API as well (for example uGUI's UI Toolkit bridge). The replacements may drop
  whatever they do not need.

**Process: clean room** (`docs/cleanroom/README.md`). This project's specification side has
read decompiled uGUI and TextMeshPro, so the replacements are written by separate
implementations, one functional specification per area. The implementations may use:
- **Metadata:** `tools/PackageShapes` generates, from the shipped assemblies' metadata, the
  type headers, enums, interfaces and delegates, the public and protected member signatures
  the game needs, and the exact serialized fields. Like `tools/SurfaceGen`, it never reads
  method bodies.
- **Unity's public documentation:** the uGUI and TextMeshPro manuals and API references.
- **The shipped packages as a black box:** `--shipped-ui-packages` or `--ui-packages DIR` picks
  which assemblies a run loads. A differential dump records the results of both on the same screens, and the
  implementations are judged on the difference. They never read the shipped code.

**Stages**, each closed by the gate below:
1. **Tooling:**
   - `tools/PackageShapes`;
   - the host switch;
   - the differential dump. `tools/UnityProbe` UiDump is extended to record, per element: the
     rect, the Graphic's generated vertices (count and a hash with a float tolerance), the
     material, the clipping and masking state, the TextMeshPro text info (characters, lines,
     words, bounds, the size auto-sizing chose), and the selectable state;
   - scripted flows covering the main menu, options, new game, the colony HUD, the management
     screens, text input, scrolling and dropdowns.
2. **uGUI**, in four specifications:
   - the event system (event data, raycasters, input modules, `ExecuteEvents`, selection,
     navigation);
   - graphics (`Graphic`, `MaskableGraphic`, `Image` with its simple, sliced, tiled and filled
     modes, `RawImage`, the legacy `Text`, `VertexHelper`, the canvas update registry, `Mask`,
     `RectMask2D`, stencil materials, clipping);
   - layout (layout groups, fitters, `LayoutElement`, `LayoutRebuilder` and `LayoutUtility`,
     `CanvasScaler`);
   - controls (`Selectable`, `Button`, `Toggle` and `ToggleGroup`, `Slider`, `Scrollbar`,
     `ScrollRect`, `InputField`, `Dropdown`).
3. **TextMeshPro**, in three specifications:
   - assets (font assets, including dynamic atlases through the engine's `FontEngine`, sprite
     assets, style sheets, settings, materials and `ShaderUtilities`);
   - text layout and mesh generation (`TMP_Text`, `TextMeshProUGUI`, `TextMeshPro`: rich text,
     wrapping, overflow, auto-size, alignment, kerning, the text-info structures);
   - `TMP_InputField` and `TMP_Dropdown`.
4. **Timeline:** only the types and members the game references. There is no content.

**Gate per stage:**
- `scripts/check.sh`, with ApiCheck extended to the replaced packages as providers: no
  missing members.
- The differential dump on every scripted screen: no differences beyond float rounding (0.01
  px; vertex hashes at that tolerance), or each difference explained and accepted in
  `docs/PARITY.md`.
- The scripted flows without exceptions, and captures compared with those of the shipped
  packages.
- The DeCrew and U57 frame rates no worse than with the shipped packages.

**Exit criterion:** a run loads no assembly from `Managed/` that comes from Unity, and
`docs/LICENSING.md` lists no Unity code loaded at run time. Unity's built-in resource files
(UI shaders, default resources) remain the only Unity data, and they are read from the user's
install.

## Game tweaks (opt-in, separate from the engine)

`--game-tweak NAME` (repeatable, or `all`) applies changes to Klei's own code (Harmony patches in
`src/OniBrutal.Host/GameTweaks`). The engine reproduces Unity; these change how the game does
something without changing what it produces, and each is off unless asked for. Measured on U57:
- `fast-save-compression`: saves compressed and decompressed with .NET's zlib instead of
  Ionic.Zlib, the same zlib format (the original game loads these saves, and ours the
  original's); compressing 1.3–1.5 s instead of 5.0–5.5, decompressing 1.1–1.5 instead of
  2.5–3.7, files about 14% larger.
- `background-save`: the compression and file write run on a background thread from the
  serialized snapshot; the file is written beside the target and moved over it, and loading,
  the next save, the load screen's delete and move, and the end of the run wait for it.
- `no-save-gc`: the forced full collection at the end of every save is removed.
- `fast-serialization`: KSerialization templates write plain fields through writers compiled
  per type instead of boxing every value through reflection; alternating saves differ only where
  the sim moved between them.

Loading U57 takes 26–28 s here against 3 min 50 s in the original. Of it, about two thirds is the
game's own work (KSerialization reading the save, each object's setup) and a quarter the engine
creating those objects; UnloadUnusedAssets during the load is 1.2 s, almost all its two full
collections. JIT is not the limit: by the colony 13 s of compiling has happened across all
threads, yet compiling this project's assemblies ahead of time (ReadyToRun) removed 5,000 of
69,000 methods and no load time, and turning tiered PGO off cut the JIT to 8 s with the load
unchanged.

- `fast-particle-collision`: a radbolt's collision check gathers the colliders around it only when
  another radbolt is registered there, and finds radbolts among the live ones (0.94 → 0.44 ms a
  frame in U57; 40,000 checks verified against the game's own result).

- `off-screen-conveyors` (**changes behaviour**): conveyor items travel along their rails on
  screen as before; off screen their transforms are not moved each frame (85% of the ~950 moves a
  frame in U57), and an item skipped while moving is placed at its cell's centre when it stops.
  While off screen a travelling item's transform, and so the cell the game files it under for
  partitioner lookups, decor and radiation, lags its place in the conveyor's flow; the flow
  itself is unaffected. U57: about 2.9 ms a frame (conveyor render tick, cell-change monitor,
  animation and collider updates). Built against the game's own types in
  `src/OniBrutal.GameTweaks`, loaded only when one of its tweaks is asked for.

- `fast-save-type-checks`: SaveLoadRoot.SaveWithoutTransform's per-component attribute check
  (Type.IsDefined, which reads metadata each time) and type name (Type.ToString, a new string
  each time) kept per type; the same answers. U57: CoreCLR's share of a save 19% → 8%, saves
  about 12% shorter (4.17 → 3.66 s averaged over five).

- `fast-anim-cleanup`: KBatchedAnimUpdater sweeps 16 chunks of its controller grid each frame for
  destroyed controllers, reading every controller there; the tweak keeps the ids of controllers
  destroyed in the last full round of the sweep and sweeps a chunk only when its dictionary still
  holds one, so the same entries go at the same sweep. U57: 94% of chunk sweeps skipped (37,700
  verified with ONI_TWEAK_VERIFY, no destroyed entry found in a skipped chunk), 0.48 → 0.12 ms a
  frame.

Measured and not kept: the loading side of fast-serialization (a U57 load produced the same
colony, 3 bytes of time of day apart, in 31.3 s against 31.6), an exact early-out for
sublimation and a canvas build cache (docs/research/u57-frame-inventory.md).

With all four, the autosave freezes the game for 4.8 s instead of 11.7 (the remainder is
serializing the colony on the main thread, which reads live state, and the colony preview's GPU
read-back). Not tweaked: the per-item sim systems (Sublimates, ranching, transitions…), which
together make up most of a large colony's frame but are each at most a few percent.

## Open items

Known behaviours not reproduced, all confirmed unreachable by the game (M6):

- Playables evaluation of animation/audio clip playables throws NotSupportedException; only
  Timeline content reaches it, and no game assembly references Timeline or PlayableDirector.
- CPU texel reads (`GetPixel*`) cover uncompressed formats, BC1 and BC3–BC7, ETC1, ETC2, EAC,
  ASTC LDR at every block size and PVRTC, each checked against independent decoders (ASTC exactly
  against ARM's astcenc, ASTC HDR to the bit: `BlockDecodingTests`). No readable texture in the build
  uses a compressed format other than BC1 and BC3 (all 2682 textures checked by `--check-bind`).
  Compressed formats the GPU cannot sample (ETC, EAC, ASTC and PVRTC on desktop OpenGL; S3TC or
  BPTC on a context without them) are created with Unity's "Decompressing texture" warning and
  uploaded decoded; a GPU round trip (blit and read back) matched the CPU reads exactly for each.
- Animator: data-driven features absent from the build throw at load (blend trees, layer masks,
  synchronized layers, StateMachineBehaviours, non-legacy clip events); every animation asset of
  the game loads (`AnimationTests.EveryAnimationAssetInTheGameLoads`).
- UI Toolkit panel internals (13 members) stay absent: uGUI reaches them only through UI Toolkit
  panels, and no game assembly references UIDocument.

Environment notes:

- Rare corrupt geometry and one device removal were seen in long runs on Mesa d3d12 under WSL;
  llvmpipe runs show none, and the original player crashes and glitches on d3d12 here too (a
  double fault inside the driver during a probe run, black world frames). To confirm on native
  GPU drivers.
- Parity runs use the offline Steam test double (ARCHITECTURE §9.7) and need `libvpx` for video.

## Tracking

- `docs/PLAN.md` is updated when a milestone closes (status line under its heading).
- ApiCheck missing-member counts per module are recorded at each milestone close.
- Known deviations live in *ARCHITECTURE §2.1*.

## Status

- Planning: complete (this document, `ARCHITECTURE.md`, `research/*`).
- **M0: complete** (2026-10-15). `scripts/check.sh`: solution builds clean; 24 engine unit
  tests pass; ApiCheck: 0 missing types, 757 missing members (743 type refs / 3098 member
  refs); host `--check-types`: all 36 loaded assemblies type-load on CoreCLR, Steamworks.NET
  port verified against the shipped binary (tools/AssemblyDiff), Newtonsoft.Json /
  BinaryFormatter / IBM437 verified through the game's own code. The engine already contains the
  core of M2 (object model, lifecycle, coroutines, cloning) because type completeness required it.
- M1: corpus gate passing (`--check-assets`), rendering data binds (`--check-bind`: 7064
  objects).
- M2 (2026-10-15): headless boot runs launchscene → LaunchInitializer → SteamManager and exits
  the way the original player does outside Steam (`SteamAPI.RestartAppIfNecessary` → `App.Quit`,
  exit 0, clean shutdown with scene unload). Running further (frontend, main menu, devBootSmoke)
  needs a logged-in Steam client on the machine, as for the original game. Test runs use
  `scripts/run.sh` (isolated persistent-data root).
- M3 in progress (2026-10-15): the OpenGL 4.5 device runs the game's own GLSL (all 583 GLCore
  variants decode; `--check-shaders` compiles and links all 119 shaders on Mesa d3d12 / RTX 3090,
  skipping VR multiview variants whose extension the context lacks, as Unity does) and the launch scene's uGUI canvas
  renders correctly (`--capture 2:out.png`). Engine-side renderer complete: cameras, renderers,
  sprites (incl. 9-slice), particles, canvases (overlay/camera/world), video planes, Graphics/GL/
  CommandBuffer; IMGUI (OnGUI dispatch, built-in skin, layout, windows, text editing) verified
  on the GPU; shipped uGUI and TMP (SDF atlas, rich text, dynamic glyphs) render through the GL
  device. ApiCheck: 13 missing members (the UI Toolkit panel internals; see open items).
- M4/M5 in progress (2026-10-15), driven by scripted runs (`--input`, offline Steam test
  double): main menu, options, the new-game flow (game mode, asteroid style, destination,
  worldgen with SimDLL, Duplicant selection, embark) and the colony run without exceptions; the
  colony renders as the original (world composite with light buffer and property textures,
  KAnim batches for Duplicants and buildings, gas, liquids, tiles, HUD, details panels with
  nested canvases and RectMask2D).
- **M5: exit met** (2026-10-16). Scripted run (release build, offline Steam test double, debug-mode
  scratch copy of the game for the Dev Life Support building): new colony, dig orders, a building
  placed, ten cycles at speed 3 with an autosave each cycle and zero exceptions; Load Game →
  colony → Cycle 8 autosave → unpause → play on to the Cycle 9 autosave, zero exceptions.
  Without life support or food production the colony is lost as in the original (no orders:
  suffocation at cycle 3–4 in both; Dev Life Support only: starvation at cycle 9–10).
  Construction with materials, other tools and overlays continue under M6 (docs/PARITY.md).
- Performance (2026-10-15), colony at speed 3, 1024×768, Xvfb + Mesa d3d12 / RTX 3090, frame
  rate from GALLIUM_HUD over the same scripted new game: original Unity player 58 fps mean
  (min 47, max 64); this engine (release build) 57–62 fps mean (min 38–46, max 65–69), with the
  frame presented from a presenter thread as Unity presents from its render thread
  (ARCHITECTURE §3.5). Per-frame CPU: update 1.6 ms, LateUpdate 4.2, canvases 4.8, render 3.7;
  GPU 4–5 ms.
  Re-measured 2026-10-16 after M6 work: 58.6 fps mean (min 38, max 70) in the same scenario.
  Windows, 2026-10-18 (RTX 3090, native driver; the original from an isolated renamed copy,
  scripts/original-windows.sh; both players resuming the same save at speed 3 with
  tools/PerfProbe measuring): DeCrew (base game, cycle 17628) 22–24 fps, the original 12–13.5,
  real time per game cycle 227 s against 299–305 s; U57 (Spaced Out and every DLC, cycle 17077)
  ~12 fps against ~8, 261–282 s per cycle against 405–407 s.
  Re-measured after the engine costs a CPU profile found (scene roots, searches, destroy; the probe
  no longer searches the scene while the game runs, which had slowed both players): DeCrew 27–34
  fps against 15–18.5, a cycle every 213 s against 237 s (speed 3 allows no less than 200 s);
  U57 12.5–15.5 fps against ~8.5, 232–247 s against 378–393 s.
  Re-measured after the allocation and per-draw work (pooled draw records, materials' uniform
  values resolved once per version, DrawMesh sharing its block; allocations down about two
  thirds, collection pauses likewise), back to back: DeCrew 48–56 fps against 17–20, a cycle every
  210 s against 229–232 s (the simulation, not the frame, now sets the pace); U57 17–18.3 fps
  against 8.3–9.5, a cycle every 219–222 s against 363–368 s.
  Where U57's frame goes (2026-10-19, Windows, EventPipe CPU sampling of the main thread, 81 ms
  frames at the time): about two thirds is the game's own logic (sim step, state machines,
  conveyor rendering), so even a free engine would leave U57 near 21 fps. Ruled out as limits:
  GC (about 1.4% of the frame; server GC cuts pauses fourfold and the autosave hitch threefold
  for about 2 GB more memory, without raising the frame rate), lock contention (about ten waits
  per 600 frames; the Monitor time EventPipe shows is its safe-point sampling bias, absent from
  perf on Linux), CPU contention (the process uses under two of sixteen threads) and the draw
  count (about 1,500 draws a frame from the game's own batching, about 2.5 ms of recording).
  Engine costs it found and removed: page faults in the texel copies of the textures the game
  re-applies every frame (about 20 MB a frame; 3.8 ms, now 1.8, with reused buffers), canvas
  elements' property sheets refilled and compared every frame (over 700 elements), and
  reflection in Instantiate's field copies. U57 measured back to back against the build before
  them: 12.4–12.9 fps against 12.2–12.5 in steady windows; the machine's other load moved the
  same build between 12.5 and 16 fps.
  With the engine API hot paths (null checks inlined, objects holding their native record,
  generic component queries without reflection) and server GC, three alternating pairs against
  the build before this work: 13.71 fps against 12.05 in steady windows (+14%), 10.7–11.1
  against 9.0–9.4 in the autosave window; every run of the new build above every run of the old.
  Then (2026-10-19, measured on Linux, where U57 runs with the Windows install's DLC content and
  perf profiles without the Windows sampler's bias, docs/research/u57-frame-inventory.md): the
  device's texture snapshots copied on worker threads (2.2 ms a frame off the main thread),
  components holding their transform and GameObject, and roots' positions read without their
  world matrix. Three alternating pairs against the build before them: 16.01 fps against 14.74 in
  steady windows (+8.6%).
- Clean-room rewrites (2026-10-18): code found to be modelled on Unity's managed source was
  rewritten from functional specifications by separate implementations (docs/cleanroom,
  docs/LICENSING.md "Provenance"):
  - the IMGUI module;
  - UnityEvents;
  - the UI Toolkit events and runtime;
  - TextGenerator's front;
  - the object pools;
  - HSV conversion;
  - SmoothDamp;
  - small routines.

  How the result was checked:
  - the implementations' tests were run against the old code, and the differences settled in
    the specifications;
  - `scripts/check.sh` passes: 281 tests, ApiCheck at 13 missing members;
  - the main menu's Options opens identically;
  - DeCrew plays 6,200 frames without exceptions;
  - typing into the in-game Database search filters it (text input reaches TextMeshPro through
    `Event.PopEvent`).

  IMGUI members that no game assembly references (`GUI.tooltip`, Toolbar, SelectionGrid,
  PasswordField) are not implemented.
- M6 in progress (2026-10-16): docs/PARITY.md ticked (Mop, Disinfect, Harvest, Attack and Wrangle orders carried out in both players). `tools/UnityProbe` (a mod run
  in both players) compares lifecycle and timing semantics, engine values, the main-menu and
  in-game UI layout (about 3400 elements), victory sequence, event popup, 18 building side
  screens and a Mop order with the original; its findings are fixed or documented (ARCHITECTURE
  D2, D3, D10). ApiCheck (ImGui assembly now included): 13 missing members, the UI Toolkit
  panel internals.
- **M6: exit met** (2026-10-16): docs/PARITY.md fully ticked or with documented deviations
  (ARCHITECTURE D1–D10).
