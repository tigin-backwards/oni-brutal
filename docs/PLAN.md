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

### M7 (proposed) — Render thread

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

### M8 (proposed) — Windows

The Windows build ships D3D11 programs only, and different native plugins (`SimDLL.dll`,
FMOD, `steam_api64.dll`). Unity produced the Linux build's GLSL with HLSLcc (DXBC → GLSL; MIT,
Unity-Technologies/HLSLcc), so the Windows build's DXBC can be translated at load (cached) into
the same kind of GLSL the OpenGL device already runs, rather than writing a D3D11 device
(BRUTAL binds OpenGL and Vulkan, not Direct3D). Remaining work: Windows-side natives resolution
in AssemblyPolicy, paths and persistent-data roots, the Steam stub for Windows, and a parity
baseline against the Windows player.

## Open items

Known behaviours not reproduced, all confirmed unreachable by the game (M6):

- Playables evaluation of animation/audio clip playables throws NotSupportedException; only
  Timeline content reaches it, and no game assembly references Timeline or PlayableDirector.
- CPU texel reads (`GetPixel*`) cover uncompressed formats and BC1/BC3/BC4/BC5; BC6H/BC7,
  ETC/EAC, ASTC and PVRTC throw. No readable texture in the build uses them (all 2682 textures
  checked by `--check-bind`).
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
- M6 in progress (2026-10-16): docs/PARITY.md ticked (Mop, Disinfect, Harvest, Attack and Wrangle orders carried out in both players). `tools/UnityProbe` (a mod run
  in both players) compares lifecycle and timing semantics, engine values, the main-menu and
  in-game UI layout (about 3400 elements), victory sequence, event popup, 18 building side
  screens and a Mop order with the original; its findings are fixed or documented (ARCHITECTURE
  D2, D3, D10). ApiCheck (ImGui assembly now included): 13 missing members, the UI Toolkit
  panel internals.
- **M6: exit met** (2026-10-16): docs/PARITY.md fully ticked or with documented deviations
  (ARCHITECTURE D1–D10).
