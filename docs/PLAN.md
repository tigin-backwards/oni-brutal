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

## Open items

Known behaviours not yet reproduced, each to be closed by a named milestone:

- `Resources.UnloadUnusedAssets` keeps loaded assets resident (Unity destroys unreferenced
  assets, so a later load yields a fresh instance). Needs reachability tracking of persistent
  objects (weak table references + engine-side references). Milestone: M2.
- Playables evaluation of animation/audio clip playables throws NotSupportedException (no
  Unity animation/audio engine); only reachable through Timeline content, which the game does
  not ship. Milestone: M6 (confirm unreachable or implement).
- CPU texel reads (`GetPixel*`) cover uncompressed formats and BC1/BC3/BC4/BC5; BC6H/BC7,
  ETC/EAC, ASTC and PVRTC throw. No readable texture in the build uses them (all 2682 textures
  checked by `--check-bind`). Milestone: M6 (confirm).
- Animator: data-driven features absent from the build throw at load (blend trees, layer masks,
  synchronized layers, StateMachineBehaviours, non-legacy clip events); additive layers blend
  as override, no PPtr curves, root motion, culling modes or damped SetFloat. Timeline animation
  playables do not reach the Animator sampler. Milestone: M6 (confirm unreachable).
- `GameObject.CreatePrimitive` needs MeshCollider/CapsuleCollider shapes in the surface.
  Milestone: M2 (with the physics module).

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
