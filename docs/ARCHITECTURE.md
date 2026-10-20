# Architecture

This document fixes the shape of the system: what we replace, what we keep, how the pieces
fit, and the rules each piece follows. `docs/PLAN.md` sequences the work. The evidence behind
every decision is in `docs/research/` (`assets.md`, `unity-usage.md`, `runtime.md`,
`brutal-api.md`); section references like *usage §1.3* point there.

## 1. Goal and boundary

Run Oxygen Not Included's **original, unmodified** managed code and data on stock .NET 10
CoreCLR, with Unity's engine replaced by our own implementation built on BRUTAL.

**What "the Unity layer" is.** Unity ships a game as three things:

1. the native player (`UnityPlayer.so`: object system, serialization, renderer, input, …);
2. the engine's managed API: `UnityEngine.*Module.dll` (thin C# over the native player);
3. the Mono runtime and its class libraries (`MonoBleedingEdge`, `Managed/mscorlib.dll`, …).

We replace **all three**: (1) and (2) with our engine, (3) with .NET 10.

**What we keep, unmodified, as game code:**

| Assembly | Why it stays |
|---|---|
| `Assembly-CSharp`, `Assembly-CSharp-firstpass` | The game. |
| `UnityEngine.UI.dll`, `Unity.TextMeshPro.dll`, `Unity.Timeline.dll` | *Superseded by PLAN M9: replaced by this project's clean-room packages; the shipped ones remain the differential reference.* Package code compiled into the game, running only on the public `UnityEngine.*` API. The shipped TextMeshPro is a **Klei fork** (`KForceUpdateDirty`, `TMP_StyleSheet.instance`); the game subclasses uGUI types and depends on their protected surface and internal ordering (*usage §8*). Reimplementing them would re-derive behaviour the game was tuned against and lose Klei's changes. They are treated exactly like `Assembly-CSharp`: IL we execute, not engine we replace. |
| `FMODUnity*.dll`, `com.rlabrecque.steamworks.net.dll`, `ImGui.NET.dll`, `Newtonsoft.Json`, `VYaml`, `LibNoiseDotNet`, `Ionic.Zip`, `ArabicSupport`, … | Third-party game dependencies. |
| Native plugins (`libSimDLL.so`, `libfmodstudio.so`, `libresonanceaudio.so`, `libsteam_api.so`, `cimgui.so`) | The game's own natives, P/Invoked by game code. |
| All data files (`globalgamemanagers*`, `level*`, `*.assets`, `.resS`, `resources.resource`, `StreamingAssets/**`, `Resources/unity default resources`, `Resources/unity_builtin_extra`) | Loaded read-only by our asset layer. |

Nothing from `UnityPlayer.so`, `UnityEngine.*Module.dll`, or Mono is loaded.

The reference install is the Linux build at `~/projects/oni-linux` (Unity 6000.3.5f2,
`m_GraphicsAPIs = [OpenGLCore]`). The target platform is **linux-x64**. Windows needs
Windows-matching native plugins and shader data (the Windows build carries D3D11 programs
only), so it is out of scope until Linux is at parity.

## 2. Process and assembly structure

```
OniBrutal.Host (exe)                      entry point; ALC setup; main loop; CLI flags
 ├─ UnityEngine.*Module.dll  (generated)  type-forwarding facades, one per Unity module name
 │     └──forwards to──┐
 ├─ OniBrutal.UnityEngine.dll             THE engine API: every public UnityEngine.* / UnityEngine.*.*
 │     │                                  type the game, uGUI, TMP, Timeline and FMODUnity reference
 │     ├─ OniBrutal.Assets.dll            Unity file formats: SerializedFile, UnityFS, type trees,
 │     │                                  LZ4/LZMA, object readers, managed-layout builder. No engine types.
 │     ├─ OniBrutal.Graphics.dll          GL 4.5 core device: buffers, textures, programs, state,
 │     │                                  framebuffers; Unity shader-blob runtime. No engine types.
 │     ├─ OniBrutal.Platform.dll          GLFW window, GL context, input events, clipboard, cursors,
 │     │                                  display modes, timing.
 │     └─ BRUTAL packages                 Ahwoo.BRUTAL.{Core,Glfw,OpenGl,Numerics,Stb,…}
 └─ game assemblies from Managed/ (unmodified)
```

**Why facades.** Game IL binds to `[UnityEngine.CoreModule]UnityEngine.GameObject`,
`[UnityEngine.UIModule]UnityEngine.Canvas`, and so on. CoreCLR follows `TypeForwardedTo`, so a
facade named `UnityEngine.CoreModule` that forwards to `OniBrutal.UnityEngine` binds every
reference exactly as compiled, while the implementation lives in one assembly with ordinary
`internal` access between subsystems (Unity itself relies on `InternalsVisibleTo` between its
modules; we avoid needing that). The facade sources are **generated** by `tools/FacadeGen` from
the original module assemblies (to learn which type lives in which module) intersected with the
types we implement. A facade `UnityEngine.dll` (forwarding everything) is also generated for
completeness.

**Assembly identity.** Unity assemblies are referenced as version `0.0.0.0`, no public key.
Our facades carry the same names and version `0.0.0.0`.

**Load context.** The game runs in the **default** AssemblyLoadContext, so its `Type.GetType`,
`Assembly.Load` and `AppDomain` queries behave as under Unity. `src/OniBrutal.Host/AssemblyPolicy.cs`
resolves names the framework does not provide:

1. Names in the .NET shared framework always win, so Mono's BCL copies in `Managed/` are never
   loaded. Mono class libraries the framework lacks (`I18N*`, `Mono.Security`, …) are refused.
2. `UnityEngine` and `UnityEngine.*Module` resolve only to our facades in `engine/`; Unity's
   originals are never loaded (a module without a facade is one nothing references).
3. Unity's own support packages (Burst, Collections, Mathematics, MemoryProfiler, Profiling.Core,
   Multiplayer.Center.Common) are not loaded: they implement engine features we replace and no
   game assembly references them.
4. Unity's UI packages (uGUI, TMP, Timeline) are this project's (PLAN M9), from `packages/`
   next to the host; `--shipped-ui-packages` loads the shipped ones from `Managed/` instead.
5. Everything else loads from `Managed/` (game and third-party libraries). Our Steamworks.NET
   port and the engine bridge ship with the host and therefore take precedence.

The scripting assemblies of `ScriptingAssemblies.json` are preloaded before any game code runs,
as the player does.

**Native libraries.** `ResolvingUnmanagedDll` on the game ALC maps the game's `DllImport`
names (`SimDLL`, `fmodstudio`, `resonanceaudio`, `steam_api`, `cimgui`, …) to
`OxygenNotIncluded_Data/Plugins/lib*.so`. BRUTAL installs its own per-assembly resolvers;
we never call `SetDllImportResolver` on BRUTAL assemblies. The Vulkan loader name quirk in
`brutal-api.md` does not affect us (no Vulkan).

### 2.1 Replacing Mono: runtime compatibility (*runtime §0*)

Binding and JIT are clean: every BCL reference in the game resolves on .NET 10 and all ~82k
game methods JIT. The differences that remain are behaviours Mono's class libraries provided
and .NET 10 ships out-of-band. The host restores them the way .NET documents for
.NET Framework-era code where it can. Two serialization paths of the game are patched at run
time where they persist runtime-specific values (D1 state-machine parameter names, D3 a local
mod's version stamp); no other game IL is touched.

| Mono provided | Host provides | Needed by |
|---|---|---|
| CAS types in mscorlib | `System.Security.Permissions` package in the host's deps | Newtonsoft.Json 7 (save header, settings, mods.json, key bindings) |
| In-box BinaryFormatter | `System.Runtime.Serialization.Formatters` package + `EnableUnsafeBinaryFormatterSerialization` | `ObjectCloner.SerializingCloner` (every worldgen settings clone) |
| Code pages via `I18N*` | `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` before game code | Ionic.Zip static constructors (IBM437) |
| `System.CodeDom` | `System.CodeDom` package (binding only; never executed) | Ionic.Zip SFX path (dead) |
| Reflection writes to `static readonly` fields (and reads that observe them) | Static fields of assemblies from `Managed/` are loaded with `InitOnly` cleared (Field-table flags only; IL untouched; such images load from memory, so their `Assembly.Location` is empty, which no game code reads) | `Localization.OverloadStrings` (translations of `STRINGS.UI.PERMIT_RARITY`), `LocString.CreateLocStringKeys` |
| Assemblies preloaded by the player | Host eagerly loads every assembly in `ScriptingAssemblies.json` that we serve, before any game type initializes | `App..cctor` / `KSerialization.Manager` assembly snapshots |

**Steamworks.NET.** One explicit-layout union in `com.rlabrecque.steamworks.net` 20.1.0
(`SteamDatagramRelayAuthTicket+ExtraField+OptionValue`, `byte[]` overlapping `long`) is
accepted by Mono and rejected by CoreCLR's type loader; the game's whole-domain
`GetTypes()` scans then crash. This is a third-party library that does not support our runtime,
so we port it: build Steamworks.NET **20.1.0 from upstream source** (MIT) under the same
assembly name and version, with that one union changed to a CoreCLR-valid layout. The port
lives in `src/ThirdParty/Steamworks.NET/` with the upstream commit pinned and the diff
documented. The host serves it instead of the shipped copy.

**Steam itself** is required, as for the original game: `SteamAPI.Init()` failure makes
`SteamManager` call `Application.Quit(2)`, and our `Application.Quit` honours it. Running
past the boot checkpoint needs a Steam client logged in with an account that owns the game.

**Documented deviations** (inherent to the runtime swap; not fixable without patching game IL):

- **D1 State-machine parameters in saves.** `StateMachineSerializer` stores and matches
  parameters by their context type's `Type.FullName`, whose generic arguments are
  assembly-qualified (`mscorlib` on Mono, `System.Private.CoreLib` here; Unity modules versus the
  engine assembly). Unpatched, no parameter of a save from the original matched and all loaded
  at their defaults: a large colony (DeCrew, cycle 17628) lost all fifteen Duplicants within
  four cycles, its food and suit docks unreachable, where the original keeps it alive.
  **Resolved by a patch:** the host (`StateMachineSaveNames`) has the serializer use the names
  the original player writes (`OniBrutal.Engine.MonoTypeNames`, each type argument's assembly
  taken from the game's own Mono class libraries in `Managed/` or the engine facades), so saves
  load fully in both players in both directions. Saves written by this player before the patch
  carry CoreCLR names; their state-machine parameters load at defaults once. Everything else in
  saves (KSerialization body, Sim blob, header) is runtime-independent.
- **D2 Number formatting.** `double`/`float.ToString()` without a format prints the shortest
  round-trippable string on .NET (Mono printed 15 significant digits), and a negative value
  that rounds to zero keeps its sign ("-0.0" where Mono printed "0.0") — cosmetic, in game
  code's own formatting. The engine's own `ToString` methods (vectors, colours, matrices, rects)
  format as Unity's Mono player does.
- **D3 `string.GetHashCode`** is randomized per process (Mono's is stable). Its only persisted
  use is the version stamp of local and dev (unpackaged) mods in `mods.json`
  (`KMod.Local.Subscribe`: `directoryName.GetHashCode()`). Left alone, the stamp differs at
  every launch, so `KMod.Manager` treats the mod as changed, asks for a restart and disables
  it. **Resolved by a patch:** the host (`ModVersionStamp`) uses the game's Harmony to replace
  that one call with Mono's algorithm (`OniBrutal.Engine.MonoStringHash`, the .NET Framework
  64-bit hash, checked against the original player with tools/UnityProbe). Stamps are then
  stable and equal to the original player's, so `mods.json` works in both players. This is
  one of the host's two changes to game code (with D1); game code's other `GetHashCode` calls stay
  randomized, and none of them is persisted. A `mods.json` written before the patch carries
  random stamps: each local mod asks for a restart once and has to be re-enabled once.
- **D4 `Thread.Abort`** throws on CoreCLR; reached only by `ThreadedHttps.End` after a 2 s join
  timeout at shutdown.
- **D5 `Process.MainModule`** is the host executable; `App.Restart` relaunches the host (we make
  the host the executable that is relaunched, so this works as intended).
- **D6 Runtime tight sprite meshes.** `Sprite.Create(…, SpriteMeshType.Tight)` traces alpha
  outlines in Unity with an unpublished algorithm; the engine creates the full-rect mesh
  instead. Rendered pixels are identical (the omitted area is fully transparent); only
  `sprite.vertices`/`triangles` differ. Sprites loaded from data keep their baked meshes.
- **D7 `GarbageCollector.GCMode`.** CoreCLR's collector cannot be switched off; Disabled and
  Manual hold a no-GC region (`GC.TryStartNoGCRegion`, budget a quarter of available memory),
  re-entered each frame if the runtime had to end it. Entering may collect once, and an
  exhausted budget collects where Unity's heap would grow. Only the game's scripted-profiling
  path changes the mode.
- **D8 TextMesh scale.** Legacy `TextMesh` geometry comes from the same legacy text generator
  as uGUI `Text`, at 0.1 world units per font pixel times `characterSize`. Unity's exact native
  scale and line-box metrics for TextMesh are unpublished; only TMP's example scripts create
  TextMeshes, so no game screen depends on them.
- **D9 UnloadUnusedAssets and ScriptableObject callbacks.** Unloading probes reachability with
  the runtime's collector (the object table holds candidates weakly for one collection), so a
  reclaimed object cannot run code. ScriptableObjects whose type declares `OnDisable` or
  `OnDestroy` (Unity calls both when it unloads one) are therefore never unloaded. No
  ScriptableObject in the game declares either.
- **D10 JsonUtility digits.** Unity's JSON writer is RapidJSON 1.1.0, whose Grisu2 rounding of
  the last digits of long outputs indexes past a ten-entry table and multiplies by whatever
  bytes follow it in `UnityPlayer.so` (identified by matching 35 observed outputs). The engine
  implements the corrected algorithm, so for such values its last digit can be smaller than
  Unity's (0.2f: …224 here, …225 in Unity); both parse back to the same number. The game writes
  JSON with `JsonUtility` only for its scripted performance capture.
- **D11 Mods built against publicized game assemblies.** Many mods are compiled against copies
  of the game's assemblies with every member made public, and use private members directly. Mono
  does not check member access at run time; CoreCLR throws FieldAccessException or
  MethodAccessException when such a method is compiled. **Resolved by a patch** (`ModAccess`):
  the host replaces the `Assembly.LoadFrom` call in `KMod.DLLLoader.LoadDLLs`, and a mod
  assembly that names a game member or type its code could not reach (private, internal) is
  given CoreCLR's `IgnoresAccessChecksTo` attribute for `Assembly-CSharp` and
  `Assembly-CSharp-firstpass` (Mono.Cecil) and loaded from memory, its symbols with it. The
  game's own assemblies are not changed (making their members public instead would change what
  KSerialization saves: its default opt-out mode saves every public field and property). The mod
  still sees itself as loaded from its file: `Assembly.Location` reports the file (mods find
  their folders from it) and its folder is searched for its references. Other mods load as
  before. This is the host's third change to game code (with D1 and D3).

## 3. The engine API (`OniBrutal.UnityEngine`)

### 3.1 Surface rule

The public surface is **exactly** what the consumers reference, with Unity's names, member
kinds, signatures, `virtual`/`sealed`-ness, and base types, verified by `tools/ApiCheck`
(conformance gate, §9). We do not add public API Unity doesn't have. Internals are free-form.
Mods may call Unity API the game never does; types they are known to need are listed in
`engine-required-types.tsv`, and running `tools/ApiCheck` over a profile's mod assemblies finds
the members they reference that the engine lacks.

The consumer set is: `Assembly-CSharp`, `Assembly-CSharp-firstpass`, `UnityEngine.UI`,
`Unity.TextMeshPro`, `Unity.Timeline`, `FMODUnity*`, `ImGui.NET`, Steamworks.NET.

Members are implemented **completely or not at all**. A member that is not yet implemented
does not exist; the runtime then fails with `MissingMethodException`, which names it exactly,
and ApiCheck lists it. There are no `throw new NotImplementedException()` placeholders and no
silent no-ops. Where Unity's behaviour is legitimately a no-op on this platform (e.g.
`Gizmos.*` in a player, `Handheld.*`), the member is implemented as that no-op with a comment
citing why.

### 3.2 Object model

- **Engine state lives on the "native side", never in managed fields of public types.** Like
  Unity, `UnityEngine.Object` has only private value-type fields: `IntPtr m_CachedPtr` (here a
  `GCHandle` to the engine's internal `NativeObject` record) and `int m_InstanceID`. Public
  engine classes (`GameObject`, `Component`, `Transform`, `Behaviour`, `MonoBehaviour`, …)
  declare **no other instance fields** unless Unity's own class declares them (e.g. the
  managed fields of `MonoBehaviour`). Reason: game code reflects over the full field set of
  components — `Localization.SetFont` walks `Instance|Public|NonPublic` fields recursively and
  follows `GameObject` fields into `GetComponents()` (*runtime §7.4*), and KSerialization builds
  templates over `Object`/`Component`/`Behaviour` fields with `GetCustomAttributes`. Managed
  back-references there would recurse forever or be serialized.
- The `NativeObject` record holds the alive/destroyed flag, name, hide flags, owning asset (if
  loaded from a file), and for GameObjects/Components the scene-graph data. **Fake-null
  semantics** follow Unity: `==`, `!=`, `op_Implicit(bool)` and `Equals` treat destroyed objects
  (and C#-created `UnityEngine.Object`s with no record) as null; these checks are lock-free
  reads usable from any thread.
- Value types used by saves keep Unity's exact field shape: `Vector2/3/4` (`x,y,z,w` public
  fields), `Color` (`r,g,b,a`), `Color32` (`r,g,b,a` bytes), `Quaternion` (`x,y,z,w`), and **no
  public settable instance properties** on `Color32`/`Vector4` (KSerialization's OptOut rule
  would serialize them). No full type name we define may also exist in a game assembly.
- `GameObject` owns an ordered component list; `Transform`/`RectTransform` is always first.
  `GetComponent*` matches by assignability, including interfaces, in component order.
  Lookups are cached per (GameObject, Type).
- **Activation and messages** follow Unity's player semantics (*usage §3*):
  - Message methods (`Awake`, `OnEnable`, `Start`, `Update`, `LateUpdate`, `FixedUpdate`,
    `OnDisable`, `OnDestroy`, `OnApplicationFocus/Quit`, `OnRenderImage`, `OnPreCull`,
    `OnPostRender`, `OnRectTransformDimensionsChange`, `OnTransformParentChanged`,
    `OnBecameVisible/Invisible`, `OnValidate` (editor-only: never), `OnGUI`, …) are discovered
    per type **by name, most-derived declaration only, any accessibility**, and bound once into
    open delegates. A base implementation runs only if the derived one calls it.
  - `Awake` runs when a component's GameObject first becomes active in hierarchy (or at
    load/instantiate time if already active), then `OnEnable`; `Start` runs before that
    component's first `Update`, deferred to the next Start phase. Activation and deactivation
    recurse in Unity's order (Awake/OnEnable for the whole subtree, parent first; OnDisable
    children-first when deactivating).
  - `Destroy` is deferred to the end of the frame; `DestroyImmediate` is synchronous,
    children first, with `OnDisable` then `OnDestroy`.
  - Exceptions in a message are logged through `Debug.LogException` and do not stop the frame,
    as in Unity.
- **Script execution order** comes from `MonoScript.m_ExecutionOrder` in
  `globalgamemanagers.assets`; within one order, update order is the order components became
  enabled, matching Unity's behaviour list. Pending `Start` calls are not sorted by it: they run in
  the order the behaviours were scheduled (became active and enabled), as Unity's delayed-call
  queue does.
- `ScriptableObject` and `MonoBehaviour` instances are created with
  `RuntimeHelpers.GetUninitializedObject` + the parameterless constructor, as Unity does
  (field initializers run, then deserialization overwrites serialized fields).
- **Costs at ONI's scale.** A late-game colony holds hundreds of thousands of objects, most of
  them scene roots (buildings, items, critters), and creates, destroys and re-parents them
  constantly (an item stored is parented to its storage). Scene roots are therefore an ordered
  slot list (`SceneRoots`: constant-time removal, compaction into a new array, enumeration
  safe against it), and per-frame walks cache what they look up per GameObject against its
  copy-on-write component array (`UiParts` for the canvas walk). Searches by type
  (`FindObjectsByType`) walk the scenes and skip inactive subtrees when inactive objects are not
  asked for.
- **Per-frame allocation.** ONI issues ~1,700 draws a frame (most through `Graphics.DrawMesh`) and
  rebuilds several meshes every frame, so the render path allocates almost nothing per draw:
  draw items, draw lists and canvas batches are reused frame to frame (`FrameRenderer.NewDrawItem`,
  `NewDrawList`; valid until `EndFrame`); queued meshes are values; `DrawMesh` shares the property
  block's sheet with the queued draw until the frame renders, a later change moving the block to a
  copy (`PropertySheet.SharedEpoch`); the device's per-draw inputs come from the recording frame's
  arena (`FrameArena.Next`) with their command delegate made once; mesh uploads hold the mesh's own
  arrays, which are never written once published (`MeshNative`); colours given as bytes stay bytes.
- **Resolved once, not per draw.** Program variants are selected once per material, pass and draw
  keyword list while keywords are unchanged (`MaterialNative.Variants`); what a material and the
  global properties give a program (uniform values, block bytes, sampler textures, pass state) is
  resolved once per their versions (`MaterialValues`), a draw resolving only its per-draw values
  and what its own block overrides; render queues and `_ST` property ids are parsed once.

### 3.3 Player loop

One thread, Unity's order (*usage §1.3, §3.5*), driven by `OniBrutal.Host`:

```
Initialization  time update (Time.*), pending scene-load completion
EarlyUpdate     platform events → Input state; Screen size changes
FixedUpdate     0..n steps (accumulator, maximumDeltaTime clamp), coroutines WaitForFixedUpdate
Update          Start for pending components; Update; Start for components enabled during Update
                (ScriptRunDelayedDynamicFrameRate); coroutines (null, WaitForSeconds, custom yields).
                Update/LateUpdate/FixedUpdate reach a behaviour only after its Start.
PreLateUpdate   Animators (DirectorUpdateAnimationBegin/End); LateUpdate
PostLateUpdate  Canvas.willRenderCanvases (uGUI layout/graphic rebuild) → render all cameras
                → screen-space-overlay canvases → WaitForEndOfFrame coroutines → present
                → deferred Destroy processing
```

Quit (`Application.Quit`, or `--frames N` in test runs): `OnApplicationQuit` to every active
behaviour, every scene unloaded (OnDisable/OnDestroy), the graphics device and window closed,
the log closed, then the process ends with `_exit(code)`: game threads may still be inside
native plugins (SimDLL worldgen), and running the plugins' static destructors under them crashes
or deadlocks. The game registers no `ProcessExit` handlers.

Pending Starts run in the order they were queued, as Unity's delayed-call queue does: a
behaviour is queued when its activation begins, before its Awake (or when it is enabled later),
not by script execution order. Both were measured in the original player: a large save's World
(order 6) starts before Game (order -10), and an object an Awake creates starts after its creator
although it is enabled first. (Loading a save, Game's Awake creates the screens, among them the
new-colony screen; Game starts first and destroys that screen, whose Start would otherwise name
the colony anew and move its saves to a new folder.)

Coroutines are Unity's state machine: an `IEnumerator` is advanced immediately by
`StartCoroutine`; nested `IEnumerator`/`Coroutine` yields wait for the child;
`CustomYieldInstruction` is re-polled each frame via `keepWaiting` (the shared cached
`WaitForSecondsRealtime` instance must behave exactly as Unity's managed implementation does).

### 3.4 Serialization and assets

**Readers (`OniBrutal.Assets`)** parse SerializedFile v22 (big-endian header, little-endian
data), UnityFS bundles (LZMA, LZ4/LZ4HC block and blocks-info compression), `.resS`/`.resource`
streaming, and resolve PPtrs (`fileID` → externals, `pathID`). Built-in files map to
`OxygenNotIncluded_Data/Resources/`.

**Native classes** (GameObject, Transform, Material, Shader, Texture2D, Sprite, SpriteAtlas,
Mesh, TextAsset, Font, Canvas, CanvasRenderer, Camera, RectTransform, VideoClip, AnimationClip,
AnimatorController, ParticleSystem, …) are read with the **Unity 6000.3.5f2 release type
trees** (`cache/typetree/InfoJson_6000.3.5f2.json`, vendored into the repo as a compressed
resource), because player files strip type trees. A generic type-tree walker produces a typed
field view; per-class "binders" copy fields into engine objects. Every object must consume
exactly its byte size; a mismatch is an error, never ignored.

**Script classes (MonoBehaviour / ScriptableObject payloads)** have no type tree. We build the
layout from the **actual managed type** following Unity's serialization rules:
serializable fields are instance fields that are public or `[SerializeField]`, not
`[NonSerialized]`, not static/const/readonly, of a serializable type (primitives, enums by
underlying type, `string`, `UnityEngine.Object` subclasses as PPtr, `[Serializable]`
non-abstract classes/structs (generic allowed), arrays and `List<T>` of these (no nested
collections), and Unity's built-in serializable structs: Vector2/3/4(Int), Quaternion,
Matrix4x4, Color, Color32, Rect(Int), Bounds(Int), LayerMask, AnimationCurve, Gradient,
RectOffset, GUIStyle, Hash128, …); depth limit 10; `[FormerlySerializedAs]` irrelevant for
reading (names come from our layout, the data has none). Alignment follows Unity's rules
(4-byte align after bool/byte/char/short runs, after strings and arrays). The exact-size
check over all ~18.8k MonoBehaviours in the build is the proof that the rules are right.

The layout builder is shared by three users: **file deserialization, `Object.Instantiate`
cloning, and `JsonUtility`**. Cloning is serialization-shaped, not a shallow copy: only
serialized fields are copied; references to objects inside the cloned hierarchy are remapped
to the clones; external references are kept; `ISerializationCallbackReceiver` hooks run
(`OnBeforeSerialize` on the source, `OnAfterDeserialize` on the clone). Non-serialized fields
of the clone keep their constructor/initializer values.

**Loading model.** Scenes (`level0..2`) are loaded in full on `SceneManager.LoadScene`.
Other files load objects **lazily by PPtr**: an object is materialized the first time it is
referenced, with its dependencies. `Resources.Load` uses the `ResourceManager.m_Container` map;
`AssetBundle.LoadFromFile(Async)` mounts a UnityFS archive as additional files.
`Resources.UnloadUnusedAssets` releases GPU resources of unreferenced loaded assets.

### 3.5 Rendering

**Device: OpenGL 4.5 core via `Brutal.OpenGl` + GLFW.** This build's only graphics API is
OpenGLCore, and every shader ships as GLSL 150 text produced by Unity's HLSLcc, with
per-variant parameter tables (*assets §5*). Executing those programs directly on GL is the most
faithful possible renderer: no shader is hand-ported, so every Klei visual effect keeps its
exact logic. (Vulkan is the alternative BRUTAL emphasises; it would require compiling this
GLSL with relaxed default-uniform-block rules and re-deriving Unity's GL binding model on top.
GL 4.5 is available on Linux drivers and on WSLg hardware via Mesa d3d12 (verified: GL 4.6 core
on an RTX 3090 with `GALLIUM_DRIVER=d3d12`).)

**Shader runtime (`OniBrutal.Graphics`).** From `SerializedShader`: properties, subshaders,
passes, tags, render state (blend/ZTest/ZWrite/Cull/ColorMask/Stencil/Offset — each either a
constant or a reference to a material property, e.g. `[_SrcAlpha]`), keyword space, and the
platform-15 program blobs (LZ4). For each pass, variant selection matches Unity's
(local+global enabled keywords → best matching subprogram). Programs are compiled and linked
on first use and cached by (shader, pass, variant). Uniform binding follows the subprogram's
`SerializedProgramParameters` (vector/matrix params by name and array size, textures by name
→ sampler unit, constant buffers with member offsets, built-in `unity_*`/`hlslcc_mtx4x4*`
matrices). Fixed-function GLSL helpers Unity injects (`#ifdef VERTEX`/`FRAGMENT` sections) are
split exactly as Unity's GL device does.

**Engine renderer (`OniBrutal.UnityEngine`, Rendering namespace).** Unity's built-in forward
renderer, restricted to what the game uses (*usage §1*):

- Cameras render in ascending `depth`; per camera: `OnPreCull` → cull (layer mask, Renderer
  enabled/visible) → sort (opaque front-to-back by queue, transparent back-to-front along view
  Z for `TransparencySortMode.Orthographic`, ties in submission order) → command buffers at
  their `CameraEvent`s → draw → `OnRenderImage` chain (component order, temporary RTs) →
  `OnPostRender`.
- Draw sources: `MeshRenderer`+`MeshFilter`, `SpriteRenderer`, `ParticleSystemRenderer`,
  `CanvasRenderer` (world-space and screen-space-camera canvases), and the per-frame
  `Graphics.DrawMesh` queue (cleared after the frame; `camera` argument restricts it).
- Unity's built-in shader constants are maintained per draw/camera (`unity_ObjectToWorld`,
  `unity_MatrixVP`, `_ProjectionParams`, `_ScreenParams`, `_Time`, `_ZBufferParams`, …) with GL
  clip-space conventions as Unity's GLCore device sets them.
- `RenderTexture` (incl. MRT via `SetTargetBuffers`, temporaries pool, `active`), `Texture2D`
  (CPU copy + `GetRawTextureData<T>` aliasing NativeArray, `Apply`, DXT/BC upload,
  `LoadRawTextureData(IntPtr)`), `Texture2DArray`, `Texture3D`, `Mesh` (dynamic buffers,
  submeshes, topologies), `Material`/`MaterialPropertyBlock`/`Shader` globals, `Graphics.Blit`
  (`Hidden/BlitCopy` from `unity_builtin_extra`), `GL` immediate mode, `CommandBuffer`.
- Gamma colour space (`m_ActiveColorSpace=0`): no sRGB conversions.

**Textures for sampling.** Where the driver has `GL_ARB_bindless_texture` (NVIDIA's, for one),
sampler uniforms are set to resident handles of (texture, sampler object) pairs and draws change
no texture bindings: on NVIDIA's OpenGL driver a draw whose vertex stage reads other textures
than the previous draw's waits for it (the game's animation batches each have their own data
textures; in a large colony that was ~33 of 46 GPU ms per frame, 2.5 with handles). Programs are
compiled with `#extension GL_ARB_bindless_texture` and the default `layout(bindless_sampler)`
qualifier ahead of their uniforms; sampling state then lives in sampler objects, and a texture
whose image is specified again is recreated under a new name (a handle fixes its layout).
Elsewhere (Mesa's d3d12 driver under WSL) textures are bound to units. A texture without an
image (zero width or height) is sampled as no texture, (0, 0, 0, 1).

**Render thread, presentation and frame pacing.** Unity's player records rendering on the main
thread and executes it on a render thread (`kGfxThreadingModeClientWorkerJobs` on GLCore), and
presents from there, so GL work and a swap that blocks — vsync, or a display path that reads the
frame back, as Mesa's d3d12 driver does under X11 — overlap the next frame's game work. We do
the same (`GlWorker`): the device's methods run on the main thread and record commands; a GL
thread that owns the rendering context (a hidden window's, shared with the visible one)
executes them. Recording resolves everything a draw reads from engine objects, so the main
thread can change them while the GL thread works: the program variant, the pass state with its
property references, every uniform value (into a per-frame arena), the textures each sampler
gets, and the mesh's layout and submesh range; changed texels and vertex data are copied when
recorded (a non-readable texture hands over its memory, as it keeps no CPU copy). `Present`
hands the frame to the GL thread once the previous frame has been executed, so one frame is in
flight. Work whose result the main thread needs waits for the GL thread: `ReadPixels` and
captures, compiling a program variant the first time it is used, `Shader.WarmupAllShaders`,
diagnostics. A command that throws is logged and the frame goes on, as an exception in a draw
used to unwind only that draw. The GL thread renders into an offscreen back buffer (colour
texture + depth-stencil); at present it fences the frame and hands it to a presenter thread
current on the window's context, which waits for the fence on its own thread (neither context's
GPU queue waits on the other's), blits to the window, fences the copy and swaps with
`QualitySettings.vSyncCount` as the interval. Up to two frames are queued for presentation, as
Unity's default of two queued frames, and three back buffers rotate; the GL thread waits (on the
GPU) for a buffer's copy fence before drawing into it again. Back-buffer reads (`ReadPixels` of the screen,
captures) read the offscreen buffer of the current frame. Pacing follows Unity's desktop
player: with vSync the swap paces frames; otherwise `Application.targetFrameRate` when set;
otherwise none. Without a display (`--headless`, `--batchmode`) frames are paced at
`targetFrameRate` or 60 Hz.

Textures loaded from asset files are uploaded as part of loading, as in Unity (the load queues
them; the main thread records their upload before the next frame renders), not on first draw: first
draws of screens with many textures (the research tree) would otherwise hitch for a second and
throw off the game's time-based UI animation. Non-readable textures drop their CPU copy then.

Per-draw uniform blocks are streamed through a ring buffer that stays persistently mapped for the
device's lifetime (`GlStreamBuffer`, ARB_buffer_storage): data is copied into the mapping, blocks
are bound by range, and a fence per frame keeps a region from being reused before the GPU has
consumed it; the storage is never respecified (no orphaning). Texture content updates into
storage of unchanged size and format (`Apply` on the game's per-frame property textures) use
`glTexSubImage2D` from client memory. Routing them through a pixel-unpack buffer (orphaned or
persistently mapped) halved their cost but corrupted textures on Mesa's d3d12 driver in long runs
(white sprite-atlas pages, world-sized garbage triangles, device removal), so it is not used.

**UI rendering.** `Canvas` implements Unity's canvas batching: per root canvas, walk
`CanvasRenderer`s in hierarchy order, apply `CanvasGroup` alpha, `RectMask2D` clip rects
(`_ClipRect` + `UNITY_UI_CLIP_RECT`) and stencil masks (materials from uGUI's own
`StencilMaterial`), and draw in order (no reordering across materials; Unity's batch
merging is an optimisation we may add only where it cannot change the result).

### 3.6 Platform

`OniBrutal.Platform` wraps `Brutal.Glfw`: one window with a GL 4.5 core context, vsync,
resize, fullscreen/windowed modes (`Screen.*`), monitors/resolutions, DPI, keyboard
(GLFW key → Unity `KeyCode`), text input (UTF-32 via our own char-callback handling where
BRUTAL's 16-bit truncation would corrupt input), mouse buttons/position (bottom-left origin)
and scroll, hardware cursors from `Texture2D`, clipboard, focus events. Legacy Input Manager
axes/buttons come from `globalgamemanagers` `InputManager` settings, with Unity's gravity /
sensitivity / dead-zone smoothing.

### 3.7 Text and fonts

TMP runs as shipped. The engine provides what it needs: `Font`, `TextCore` data types,
`FontEngine` used only for dynamic atlases, and `TextGenerator` for the legacy `UI.Text`
(debug/mod screens).

- **FreeType** is BRUTAL's build (`Ahwoo.BRUTAL.FreeType`: `libfreetype.so` / `freetype.dll`)
  through our own P/Invokes (Unity links FreeType statically; the game ships none); its records'
  C `long` fields are `CLong`, 64-bit on Linux and 32-bit on Windows. OpenType layout data FreeType does not expose (GPOS
  `kern`/`mark`/`mkmk`, GSUB `liga`, GDEF classes, legacy `kern`, OS/2) is read from the sfnt
  tables in managed code.
- **Atlases**: Unity's MaxRects packer over TMP's free/used rect lists (glyph + padding on each
  side); SDF modes rasterize the outline (×8/16/32 supersampled for `SDF8/16/32`) and run
  Gustavson's anti-aliased EDT; the field is 0.5 on the outline with spread = padding + 1
  (TMP's `_GradientScale`), measured against the game's own SDF atlases.
- **Legacy fonts** (class 128) render glyphs on demand into the font's own `Font Texture`
  (Alpha8, from 256², grown when a rebuild with the current frame's glyphs still overflows)
  and raise `Font.textureRebuilt` on rebuilds. Fonts without data (the built-in
  `LegacyRuntime`) use the built-in default face, Liberation Sans — the face Unity embeds for
  it (LegacyRuntime's serialized metrics are Liberation Sans's) — taken from the game's own
  Liberation Sans font asset; other data-less fonts first try the OS fonts in `m_FontNames`.

### 3.8 Audio, video, other modules

- Audio: FMOD through the game's own `FMODUnity` + native `libfmodstudio.so`; the engine
  supplies what FMODUnity needs (paths, Transform, components, Timeline/Playables types).
- Video: `VideoPlayer` decodes VP8 WebM from `resources.resource` with our own EBML demuxer and
  our own VP8 decoder (`Video/Engine/Vp8`, a port of RFC 6386's reference decoder; VP8 decoding is
  exact by specification, and every frame of the game's 18 clips decodes to the same planes as
  libvpx, which the tests use as reference) on a decode thread per player, so no codec library
  is needed on either platform. Frames are converted to RGBA on the CPU with the exact formula of the build's own
  `Hidden/VideoDecode` shader (BT.601 limited range, bilinear chroma) into a CPU `Texture2D`
  that the device uploads; a GPU path running that shader on the planes can replace the
  converter. The game's clips have no audio tracks (and the game sets `audioOutputMode = None`);
  video audio is not played.
- `Animator`: AnimatorController state machine + AnimationClip curve evaluation for the
  properties the game animates (UI). `ParticleSystem`: the modules the prefabs actually enable.
- `UnityWebRequest` → `HttpClient`; `ImageConversion` → stb_image / stb_image_write
  (`Ahwoo.BRUTAL.Stb`); `JsonUtility` → the shared layout builder; `Physics.Raycast` → the
  colliders present in data; IMGUI → clipboard and the event/GUI surface the code paths touch.

## 4. Threading

The game relies on release-player leniency (*runtime §6*): worker, path-prober, worldgen,
HTTP and FMOD threads call into the engine. Our rules:

1. **Free-threaded:** math/value types; `Application` path and platform getters (immutable
   after startup); `Object ==`/`!=`/`bool`/`name`/`GetInstanceID`; `Component.transform` /
   `gameObject`; `GetComponent(s)`; `Transform.position` and other transform **reads**;
   `TextAsset.text`/`bytes`; `Resources.Load` (loading is serialized by a lock and completes
   on the calling thread, as the game's lazy `Db.Get` needs); `Debug.Log*`.
2. **`Debug.Log*` from other threads** invokes `logMessageReceivedThreaded` on the caller and
   queues `logMessageReceived` to the main thread, as Unity does (KCrashReporter listens there).
3. **Main thread only** (asserted in all builds, the failure is an exception with a clear
   message, as Unity's "can only be called from the main thread"): structural mutation
   (`Instantiate`, `AddComponent`, `Destroy`, `SetParent`, transform setters, activation),
   rendering and GPU resource APIs, scene loading.
4. Worker-thread writes into `GetRawTextureData` arrays are allowed (plain CPU memory until
   `Apply`). GL calls happen only on the GL thread, apart from the presenter thread's copy
   and swap on the window's own context (§3.5); the device records them on the main thread.
5. Reverse P/Invoke: SimDLL's error callback is created by game code per call; exceptions
   crossing it are fatal on CoreCLR, which matches Unity's crash behaviour on that path.

## 5. Numerics

`Vector2/3/4`, `Quaternion`, `Matrix4x4`, `Color`, `Mathf`, … are implemented with Unity's
formulas (including `Mathf.Approximately`, `RoundToInt` banker's rounding, `Random.Range`
inclusive/exclusive rules, `Quaternion.Euler` order ZXY). Bit-exact float results are out of
scope; algorithmic equivalence is not.

## 6. Configuration and paths

`Application.dataPath` = `<game>/OxygenNotIncluded_Data`, `streamingAssetsPath` =
`<dataPath>/StreamingAssets`, `isEditor` = false, `isPlaying` = true. On Linux,
`persistentDataPath` = `~/.config/unity3d/Klei/Oxygen Not Included` (Unity's Linux rule:
`$XDG_CONFIG_HOME/unity3d/<company>/<product>`) and `platform` = `LinuxPlayer`. On Windows the
player is the Windows one: `platform` = `WindowsPlayer`, `persistentDataPath` =
`%USERPROFILE%/AppData/LocalLow/<company>/<product>` (the LocalLow known folder),
`temporaryCachePath` under `%TEMP%`, all paths with forward slashes as Unity reports them; the
game itself keeps saves, mods and its settings under `Documents\Klei\OxygenNotIncluded`
(`Environment.GetFolderPath`). The Windows build is the same game build as the Linux one
(ChangeList 744825) and its shaders carry OpenGL programs next to the Direct3D ones, so the same
device runs its data. Natives resolve per platform (`Plugins/x86_64/<name>.dll` on Windows,
`Plugins/lib<name>.so` on Linux); Steamworks.NET is built per runtime identifier (library name
and callback packing differ); FreeType comes from BRUTAL's package on both. Company/product/version
come from `globalgamemanagers` PlayerSettings. The host takes `--game <dir>` (default
`~/projects/oni-linux`), `--headless` (no window, no GL: logic-only frames for tests),
`--frames N`, `--screenshot <dir>` (debug captures), and `--log <file>`.

The host directory carries a `Restarter` (`Restarter.exe` on Windows, a .NET program), as Klei's
player directory does: `App.Restart` (language change) starts it with the player's executable
path and quits. Ours waits for the player to exit and starts it again with the arguments it was
started with (recorded by the host in `ONI_BRUTAL_RESTART_ARGS`, shell-quoted, and
`ONI_BRUTAL_RESTART_ARGV`, JSON, with `ONI_BRUTAL_RESTART_PID`).

Test runs (not the product): `--run-home DIR` keeps a run's per-user data under DIR (Unity's
paths, and on Windows the Documents and local application data folders the game asks .NET for),
`--audio-wav DIR` sends the game's audio to `DIR/fmodoutput.wav` (FMOD's WAV writer) instead of
the sound device, and the offline Steam double (`tools/SteamStub`, an ELF library or a PE DLL
written directly) stands in for the Steam client. `scripts/run-windows.sh` uses all three.

## 7. Logging and diagnostics

`Debug.Log*` and the logger/handler surface behave as Unity's (the game replaces the handler).
Our engine logs through `Brutal.Logging` into the same Player.log-style file
(`~/.config/unity3d/Klei/Oxygen Not Included/Player.log`), with engine messages tagged
`[engine]`. GL debug output is enabled in debug builds.

Development diagnostics (host options, never needed by players): `--trace-exceptions` logs every
first-chance exception with per-frame IL offsets (the game ships without symbols; the offset
and `ilspycmd -il` locate the throwing instruction); `--capture FRAME:PATH[:SCALE]` writes a
screenshot through `ScreenCapture.CaptureScreenshot`; `--input FRAME:ACTION[:ARGS]` feeds scripted
mouse/keyboard/wheel/text input through the window's input path (held keys for chords such as
Ctrl+F4); `--dump-render-textures FRAME:DIR` writes
the screen and every live render texture as PNG at the end of a frame; `--dump-shader NAME:DIR`
writes a shader's GLCore variants and pass state; `--frame-stats` logs per-phase frame times
and the GPU time per frame (timer queries), any frame over 250 ms with its own breakdown, and every 600 frames the process memory and the managed heap; `--max-frame-rate N[:FRAMES]` bounds the frame rate
(for the first FRAMES frames) so scripted schedules that count frames hold on unpaced displays;
`--probe-ui FRAME:X,Y` logs the canvas elements drawn at a window point with their hierarchy
path, material, texture, colour and group alpha; `--dump-hierarchy FRAME:PATH` logs a subtree's
active flags, rects and components; `--dump-component FRAME:PATH|TYPE` logs a component's serialized fields;
`--report-builtin-assets FILE` writes the objects a run loaded
from Unity's built-in resource files (docs/LICENSING.md); `--check-*` run the corpus gates.

CPU profiles: `perf record -e cpu-clock:u -g` (user-mode sampling needs no privileges at the
default `perf_event_paranoid` 2) with `DOTNET_PerfMapEnabled=1`; the runtime maps JIT code twice
(W^X), so samples fall in a `memfd:doublemapper` mapping and are resolved against
`/tmp/perf-PID.map` by address. `dotnet-trace`'s thread-time sampling stops threads at GC safe
points and charges the time before a safe point to the method that reaches it (it showed lock
and GC-poll time that perf does not), so it is not used for self time.

## 8. Repository layout

```
docs/                     PLAN.md, ARCHITECTURE.md, research/, decisions are recorded inline
src/OniBrutal.Assets/     file formats and readers
src/OniBrutal.Graphics/   GL device and Unity shader runtime
src/OniBrutal.Platform/   window/input
src/OniBrutal.UnityEngine/ engine API implementation (namespaces mirror Unity's)
src/OniBrutal.Host/       executable
src/Facades/              generated UnityEngine.*Module facade projects (do not edit)
src/Unity.InternalAPIEngineBridge.004/  engine-side bridge that the shipped TMP calls into
src/ThirdParty/Steamworks.NET/          upstream 20.1.0 source port (see §2.1)
tests/                    xunit: OniBrutal.Assets.Tests, OniBrutal.UnityEngine.Tests, …
tools/Inventory, tools/ApiCheck, tools/FacadeGen
cache/ (ignored)          decompiled code, BRUTAL clone, inventories, type trees
```

Build settings in `Directory.Build.props`: `net10.0`, nullable, `AllowUnsafeBlocks`,
`TreatWarningsAsErrors` for our projects, BRUTAL analyzer configuration (documented
suppressions only), central package versions in `Directory.Packages.props`.

## 9. Verification

1. **Conformance:** `tools/ApiCheck` over the consumer set; the count of missing members only
   goes down. Each milestone states which areas must reach zero.
2. **Asset corpus test:** every object in every data file (and in bundles) deserializes to
   exactly its byte size, natives and MonoBehaviours alike.
3. **Unit tests** for math, serialization rules, cloning, lifecycle ordering, coroutine
   semantics, layout of uGUI (via the shipped DLL), shader variant selection.
4. **Headless boot runs** asserting the scene/bootstrap sequence reaches defined checkpoints
   with zero logged exceptions.
5. **Visual checks:** `--screenshot` captures at checkpoints, reviewed against reference
   screenshots of the real game.
6. **Gameplay soak:** new colony → N cycles → save → reload → continue, no exceptions.
7. **Offline Steam test double** (`tools/SteamStub`, `scripts/run-offline.sh`). The game
   requires a logged-in Steam client (`SteamAPI_Init`); on machines without one, test runs
   load a generated stand-in for `libsteam_api.so` through the host's dev-only
   `--native-override steam_api=PATH`. Every entry point Steamworks.NET binds exists and
   returns zero except a small table describing a logged-in owner of the base game (no DLC,
   no Workshop items, English); no callback is ever delivered. It is test infrastructure,
   not part of the product: it lets M4–M6 engine behaviour be exercised; Steam-specific game
   behaviour (DLC ownership, Workshop mods, achievements, cloud) is verified only with the
   real client.
8. **Original-player comparisons on Windows** (`scripts/original-windows.sh`). The original runs
   from a renamed copy of the install with BepInEx 6 and `tools/OriginalIsolation`, which
   redirects the same two known folders our host's `--run-home` does and sends FMOD's output to
   a WAV file; the copy's product name (PlayerSettings) is changed to one of the same length so
   Unity's registry key and LocalLow folder are the copy's own; the Steam double stands in for
   Steam. Both players load `tools/PerfProbe`, a game mod that keeps the colony at speed 3 and
   logs the frame rate from the real-time clock, and resume the save through the game's own
   `AutoResumeSaveFile` setting (`scripts/windows-run-home.sh`), so the same code measures both.
