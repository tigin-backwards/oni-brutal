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
mod's version stamp); so are mods' loading (D11) and the bubble manager's one use of a struct's
hash as an identity (D26). No other game IL is touched.

| Mono provided | Host provides | Needed by |
|---|---|---|
| CAS types in mscorlib | `System.Security.Permissions` package in the host's deps | Newtonsoft.Json 7 (save header, settings, mods.json, key bindings) |
| In-box BinaryFormatter | `System.Runtime.Serialization.Formatters` package + `EnableUnsafeBinaryFormatterSerialization` | `ObjectCloner.SerializingCloner` (every worldgen settings clone) |
| Code pages via `I18N*` | `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` before game code | Ionic.Zip static constructors (IBM437) |
| `System.CodeDom` | `System.CodeDom` package (binding only; never executed) | Ionic.Zip SFX path (dead) |
| Settings classes (`System.Configuration.ApplicationSettingsBase`, in Mono's `System.dll`) | `System.Configuration.ConfigurationManager` package | mods' generated `Properties.Settings` classes (a mod whose types fail to load is dropped by KMod) |
| Reflection writes to `static readonly` fields (and reads that observe them) | Static fields of assemblies from `Managed/` and of mods' assemblies (`ModAccess`; the game's `LocString.CreateLocStringKeys` writes back mods' strings classes) are loaded with `InitOnly` cleared (Field-table flags only; IL untouched; such images load from memory, so their `Assembly.Location` is empty, which no game code reads) | `Localization.OverloadStrings` (translations of `STRINGS.UI.PERMIT_RARITY`), `LocString.CreateLocStringKeys` |
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
  code's own formatting. Fixed-decimal formats round differently at a half: Mono rounds a
  float's 7-significant-digit form, .NET its exact value, so `67.255f.ToString("F2")` is "67.26"
  on Mono and "67.25" here (`0.005f`: "0.01" and "0.00"). Kept: .NET's is the exact rounding of
  the value the game holds. The engine's own `ToString` methods (vectors, colours, matrices, rects)
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
- **D12 `callvirt` to a static method.** Mono compiles one as a call; CoreCLR's JIT rejects the
  method ("Method not found: '?'"). C# compilers never emit it, but Harmony transpilers that
  swap an instance call for a static helper and keep the opcode do (Mod Updater's patch of
  `KMod.Manager.Subscribe`). **Resolved by a patch** of the game's Harmony (`TranspiledCalls`):
  the instructions its transpilers produce (`CodeTranspiler.GetResult`) have such calls made
  calls, as Mono would run them, and each is logged.
- **D13 Inlined patches of value types' virtual methods.** Before detouring a method,
  Harmony's MonoMod marks its MethodDesc not to be inlined, so code compiled later calls the
  patched method. For a value type's override (`GetHashCode`, `Equals`, `ToString`), the handle
  reflection gives is the method's unboxing stub; the JIT inlines the unboxed MethodDesc after
  it, which stays unmarked, so code tiered up after the patch ran the original (Fast Track's
  `AxialI.GetHashCode`: dictionary lookups missed, `ClusterGrid.RegisterEntity` threw on every
  starmap entity). Mono has no unboxing stubs. **Resolved by a patch** of MonoMod's
  `PlatformTriple.TryDisableInlining` (`UnboxedMethods`): the unboxed MethodDesc is marked too
  (CoreCLR 10's layout; the host's `--check-types` patches an emitted value type's
  `GetHashCode` and fails if dictionary lookups miss after tier-up).
- **D14 Mono's enum formatting.** Mods that add elements name their `SimHashes` values by
  patching enum formatting: Mono's private `Enum.InternalFormat(RuntimeType eT, Object value)`, or
  `Enum.ToString()`. In Mono's `system/enum.cs`, `ToString()` is
  `InternalFormat(GetType(), GetValue())` (the enum's type and underlying value);
  `ToString(format)` with "G" or none, the obsolete overloads and the IFormattable route
  `string.Format` takes all call the virtual `ToString()`; `Format(type, value, "G")` calls
  `InternalFormat`. CoreCLR has no InternalFormat, and its `ToString(format)` and span formatting
  do not go through `ToString()` (Rocketry Expanded's ElementUtil: the patch failed, element names
  came out as numbers, lookups by name failed). **Resolved by patches** of `AccessTools.Method`
  and `AccessTools.DeclaredMethod` (the latter is how `[HarmonyPatch]` attributes resolve) and of
  Harmony's `PatchProcessor.Patch` (`MonoEnumFormat`): looked up on `Enum`, the name gives a
  stand-in with Mono's signature, and once a mod patches it or `Enum.ToString()` the formatting is
  routed as Mono routes it; without such a mod nothing is patched. `--check-types` patches both
  as mods do and checks each path.
- **D15 A second version of a loaded assembly.** For an assembly without a strong name, Mono
  answers a load of another file with the assembly already loaded under that simple name,
  whatever the versions (`mono_domain_assembly_search` compares names ignoring version, key and
  case). CoreCLR refuses the second version, and KMod then drops the whole mod (a mod shipping its
  own copy of a library another mod loaded first). Mono's binding of a reference to such an
  assembly searches the same way, so a mod built against PLib 4.22 runs with an older PLib another
  mod loaded; CoreCLR fails the reference ("manifest definition does not match"). **Resolved in**
  `ModAccess`: such a file is given the loaded assembly (`LoadFrom`), and a reference CoreCLR cannot
  bind is given the assembly loaded under its simple name (the default context's Resolving event);
  each substitution is logged. `--check-types` loads two versions of an emitted assembly and binds a
  third that references the second.
- **D16 Exceptions nothing catches on other threads.** They end a CoreCLR process; Unity's
  player keeps running (measured with PerfProbe in the original: a thread's exception is logged,
  by UnityEngine's AppDomain.UnhandledException handler calling Debug.LogException; a thread-pool
  item's and an async void method's are dropped silently, with no UnhandledException; Mono passes
  IsTerminating true). Sgt_Imalas' UtilLibs checks for updates from an async void method that
  throws when offline. **Resolved by** .NET 10's `ExceptionHandling.SetUnhandledExceptionHandler`
  (`UnhandledExceptions`): a thread's exception raises UnhandledException (and is logged as
  Unity's are), a thread pool's is dropped, the main thread's still ends the process;
  `--check-types` throws on both kinds of thread.
- **D17 A try block entered with values on the stack.** ECMA-335 requires an empty evaluation
  stack where a try block starts; CoreCLR's JIT refuses a method that enters one with values on
  it (InvalidProgramException when Harmony applies the patch), and Mono compiles it. Harmony
  transpilers that insert a call returning a value nobody uses produce such methods (Better Info
  Cards' patch of `SelectToolHoverTextCard.UpdateHoverElements` leaves a `KSelectable` behind
  before a `foreach`; the mod failed to load). **Resolved by a patch** of the game's Harmony
  (`TranspiledTryEntries`, beside D12's): in the instructions its transpilers produce, values a
  try block is entered with and never consumes are popped before it. They are dead, since `leave`
  and `throw`, the only ways out of a try, empty the stack. A method whose try block does use them,
  or that the analysis cannot follow, is left as it was, and each change is logged.
  `--check-types` transpiles a method so and runs it.
- **D18 Patches of a generic class's shared code.** A method of a generic class instantiated
  over reference types runs code that every such instantiation shares, on Mono as on CoreCLR, so
  a patch of one instantiation's method applies to all (Better Info Cards patches
  `HoverTextDrawer.Pool<MonoBehaviour>.Draw`, which the game never instantiates, to see the
  widgets its `Pool<Image>` and `Pool<LocText>` draw). When tiered compilation recompiles that
  shared code, MonoMod moves its detour to the new code only for a recompiled method equal to the
  patched one; the shared code's is the canonical method (`Pool<__Canon>.Draw`), so the patch
  stopped running once the method tiered up (Better Info Cards: NullReferenceException every
  frame after a few seconds of hovering). **Resolved** in `SharedGenerics`, which subscribes to
  MonoMod's method-compiled event and hands MonoMod's handler every patched method of a generic
  class that has the recompiled method's handle (instantiations sharing code share their method
  handle). `--check-types` patches an emitted generic class's method for one argument and calls
  it for another past tier-up.
- **D19 Patch methods' frames.** Harmony calls a mod's prefixes, postfixes and finalizers from
  the replacement it builds for the patched method, a dynamic method owned by 0Harmony. CoreCLR's
  JIT inlines a small patch method into it, so what the patch method calls sees 0Harmony as its
  caller (`Assembly.GetCallingAssembly()`): Traffic Visualizer's SquareLib looks for its icons in
  0Harmony's folder, and the game stopped loading. On Mono the patch method keeps its frame (the
  mod works in the original player). **Resolved in** `PatchFrames`: before Harmony builds a
  replacement, each patch method of a mod is marked not to be inlined (MonoMod's
  `TryDisableInlining`, which Harmony applies to the patched method itself); this project's own
  patches are left as they were. `--check-types` calls `GetCallingAssembly` from a helper an
  emitted mod's postfix calls.
- **D20 Patching a P/Invoke whose export is missing.** MonoMod prepares a method before
  detouring it; for a P/Invoke, CoreCLR then binds the native export, and a missing one fails the
  patch. Mono binds a P/Invoke at its first call, so mods patch one the platform lacks to keep it
  from being called: Debug Buttons and ONI Together skip `KImGuiUtil.SetKAssertCB`, which the
  game's Linux cimgui does not export, and load in the original player. **Resolved in**
  `PInvokePatches`: a missing export or library does not fail the preparation of a P/Invoke; the
  detour goes on the method's own entry, other P/Invokes bind as before, and calling the original
  still fails as on Mono. `--check-types` patches a P/Invoke to a missing export.
- **D21 Patches of methods an earlier replacement calls.** Harmony compiles a patched method's
  replacement fully optimized, and CoreCLR's JIT inlines small methods it calls; a method patched
  later is marked not to be inlined from then on, but a replacement compiled before keeps its old
  body inlined and the later patch never runs there (True Tiles patches
  `BlockTileRenderer.GetConnectionBits`, then `MatchesDef`, which it calls: no tile connected to its
  neighbours). On Mono the later patch takes effect (the mod works in the original player).
  **Resolved in** `PatchDependents`: the methods each replacement calls are recorded from the
  instructions Harmony builds it from (`CodeTranspiler.GetResult`), and the first time a method is
  patched, the replacements already made that call it are rebuilt, as Harmony rebuilds one when a
  patch is added. `--check-types` patches a caller, then a method it calls, and calls the caller.
- **D22 Classes missing a method of an interface they list.** A mod built against an older game
  can have a class that no longer implements every method of a game interface (Wireless Power's
  side screen lacks the new `ISliderControl.GetSliderTooltip(int)`). Mono loads the class and fails
  only when it is checked or used through the interface (measured in the original player: "VTable
  setup of type … failed" from `IsAssignableFrom` with an interface; KMod's `GetTypes` and
  `UserMod2` check pass, and Wireless Power runs). CoreCLR refuses the class when it loads, so
  `GetTypes` throws and KMod drops the whole mod. **Resolved in** `MissingInterfaceMethods`:
  before `ModAccess` loads a mod's assembly, it is loaded once in a collectible context that binds
  only to assemblies already loaded, and that context is collected before the real load (until
  then `AppDomain.GetAssemblies` lists its copy, and PLib, which picks its instance from there,
  ran the copy, whose `Location` is empty: Threshold Walls). The methods CoreCLR reports missing
  get an explicit implementation that throws the `TypeLoadException` CoreCLR would have. The class
  loads, and calling the missing method fails. Where Mono fails earlier (an interface check of the class),
  ours goes on. `--check-types` loads a mod built against an older shape of an interface, whose
  new method takes one of the game's own types (Trashcans), and calls the method it lacks.
- **D23 Stack frames of patched methods.** Harmony runs a patched method as a dynamic method that
  MonoMod creates with an owner type. On Mono, a frame of it reports a method declared by that
  owner, `MonoMod.Utils.DynamicMethodDefinition` (measured in the original player); CoreCLR's
  `StackFrame.GetMethod` returns the `DynamicMethod`, whose `DeclaringType` and `ReflectedType`
  are null. Customize Plants walks the stack for the class whose `CreatePrefab` made a plant, and
  a null there ended the game's asset setup when another mod had patched a `CreatePrefab` (the
  save never loaded). **Resolved in** `MonoStackFrames`: a mod's calls to `StackFrame.GetMethod`
  (found in its metadata) call the host's instead, which reports a dynamic method with an owner
  as declared by the owner. Writing the rewritten mod resolves its references against the
  assemblies already loaded (an enum default value's type: PLib's mods), and a mod that cannot be
  rewritten loads unchanged. `--check-types` walks the stack from a patched method of a mod
  whose method takes a game enum with a default value.
- **D24 Special folders that do not exist.** On Linux and macOS, Mono's
  `Environment.GetFolderPath` gives a special folder's path (its XDG user directory, or the home's
  `Desktop`, `Documents`…) whether or not it exists; CoreCLR checks unless asked not to and gives
  "" otherwise. Harmony's debug log creates the Desktop folder from that path, so a mod that turns
  the log on (Simple Cheat) failed to load on a machine without `~/Desktop`:
  `Directory.CreateDirectory("")` throws. **Resolved in** `MonoFolderPaths`: the default option
  is answered without the existence check, as Mono answers it; Windows is unchanged.
  `--check-types` asks for the Desktop of a home that has none.
- **D25 Float intermediate precision.** Unity starts Mono with `-O=-float32`
  (`UnityPlayer.so`; Mono's own default is float32 on): float arithmetic is evaluated in double
  and rounded to float32 only where the value is stored (a local, field, array element,
  parameter or return value) or cast. CoreCLR rounds every float operation to float32. So a
  float expression used without being stored first can differ in the last bit:
  `(int)(0.7f * 10f)` is 6 on Mono and 7 here, `Math.Round(0.9f * 255)` 229 and 230. In the game
  this shows as rare off-by-one results where a product lands on an integer, such as the germs
  a ladder passes on (`(int)(count * 0.005f)`, one fewer on Mono at multiples of 200), and in
  some UI roundings at a half. Kept: .NET's results are those the code as written asks for, and
  emulating Mono (widening every float load and call result in all game and mod IL) would
  break mods' transpilers that match IL patterns. Measured by `tools/UnityProbe`
  (FloatPrecision.cs, `PROBE_SCENARIO=floatprec`); test tools that compare players compute in
  double, where both runtimes agree.
- **D26 Hashes of structs without a GetHashCode.** A struct that does not override GetHashCode
  is hashed by `ValueType.GetHashCode`. Mono XORs every field into the hash
  (`ves_icall_System_ValueType_InternalGetHashCode`: an `int` field's value, a string field's
  `mono_string_hash_internal`, any other field's own `GetHashCode`), starting from the address
  of a runtime structure. CoreCLR uses the first field alone when the struct holds floats or
  references, so values that differ only in a later field collide. That is only slower in a
  dictionary, but `BubbleManager.Archetype.GetId` uses the hash as an identity: bubbles are
  filed under the hash of (velocity, element, fade speed), so every bubble rising straight up
  had one id, and the first element registered under it was the one every later bubble released
  (after an Oxy Coral's oxygen bubble, a Thermal Gas Fissure gave oxygen instead of natural
  gas). **Resolved by a patch** (`MonoValueTypeHashes`): that call computes Mono's hash
  (`OniBrutal.Engine.MonoValueTypeHash`, starting from 0, so ids equal Mono's up to its
  per-process start). This is the host's fourth change to game code (with D1, D3 and D11).
  **Scanned** (2026-10-23, Mono.Cecil over IL: `constrained.` or `box` before
  `object::GetHashCode`, `EqualityComparer<T>.GetHashCode` and `HashCode` over such a struct;
  a struct as a dictionary key only hashes to a bucket and is left out): every game assembly
  in `Managed/` that is not Unity's or the BCL (Assembly-CSharp, Assembly-CSharp-firstpass,
  ImGui, ImGui.NET, FMODUnity*, Harmony, Newtonsoft.Json, VYaml, Ionic.Zip, LibNoiseDotNet,
  ArabicSupport, Steamworks.NET), and of mods only the Workshop builds in the catalog's
  `out/ws` (archived versions included), 1229 assemblies in all. Mods outside that set are
  not covered. Hits besides `GetId`, neither a collision under CoreCLR: Klei's ImGui object
  drawer (`MemberDrawerCollection.DrawFor`) puts the hash of `MemberDrawContext` (two bools)
  into an ImGui ID, an identity, but CoreCLR hashes a struct of plain values by all its bytes,
  so it tells the four values apart (Mono's XOR does not: (true, false) and (false, true)
  collide there); Antigravity Multiplayer (3635632392) sends the hash of `Random.state` (four
  ints, hashed by all bytes as well) to clients as a random seed, a value, not an identity.
  `--check-types` compares two elements' archetype ids. `tests/e2e/thermal-gas-fissure.yaml`
  checks the fissure's gas.

## 3. The engine API (`OniBrutal.UnityEngine`)

### 3.1 Surface rule

The public surface is **exactly** what the consumers reference, with Unity's names, member
kinds, signatures, `virtual`/`sealed`-ness, and base types, verified by `tools/ApiCheck`
(conformance gate, §9). We do not add public API Unity doesn't have. Internals are free-form.
Mods may call Unity API the game never does; types they are known to need are listed in
`engine-required-types.tsv`, and running `tools/ApiCheck` over a profile's mod assemblies finds
the members they reference that the engine lacks. `scripts/apisurface.sh` (`ApiCheck --surface`)
lists every public type and member of the shipped Unity assemblies that the engine and our
packages lack, and which of them the Workshop builds in `out/ws` reference.

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
    component's first update callback, at the next Start pass or that callback, whichever
    comes first (§3.3). Activation and deactivation
    recurse in Unity's order (Awake/OnEnable for the whole subtree, parent first; OnDisable
    children-first when deactivating).
  - `Destroy` is deferred to the end of the frame; `DestroyImmediate` is synchronous,
    children first, with `OnDisable` then `OnDestroy`.
  - Exceptions in a message are logged through `Debug.LogException` and do not stop the frame,
    as in Unity.
- **Script execution order** comes from `MonoScript.m_ExecutionOrder` in
  `globalgamemanagers.assets`; within one order, update order is the order components' activation
  began (or they were enabled later), matching Unity's behaviour list. Pending `Start` calls are not sorted by it: they run in
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
                Start pass
FixedUpdate     0..n steps (accumulator, maximumDeltaTime clamp), each followed by a Start pass;
                coroutines WaitForFixedUpdate; Destroy carried out
Update          Update; Start pass (ScriptRunDelayedDynamicFrameRate); Destroy carried out;
                coroutines (null, WaitForSeconds, custom yields).
PreLateUpdate   Animators (DirectorUpdateAnimationBegin/End); LateUpdate; Start pass; Destroy
                carried out
PostLateUpdate  Canvas.willRenderCanvases (uGUI layout/graphic rebuild) → render all cameras
                → screen-space-overlay canvases → WaitForEndOfFrame coroutines → present
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

A Start pass runs the Starts queued before it began; those queued meanwhile (objects a Start
creates) wait. FixedUpdate, Update and LateUpdate start a member whose Start is still pending
just before its first call, so it starts ahead of pending behaviours without that message. A
behaviour also takes its place among the update callbacks when its activation begins: a
component that an Awake adds (the game's `MyCmpAdd`) is enabled before its adder but updates
after it. All of this was measured in the original (UnityProbe scenarios 13 to 19). The game
depends on it: SaveLoader loads a save from a Start, so the Duplicants' StandardWorker starts
before their other components and spawns their MinionIdentity before their KPrefabID. A dead
Duplicant thus enters `Components.LiveMinionIdentities` and is removed again once its KPrefabID
restores the Dead tag, and that removal reorders the list.

Destroy is carried out at three points, measured in the original (UnityProbe's destroy phases
and scenario 20): after the fixed steps, after the Start pass that follows Update, and after
LateUpdate's; a call made later in the frame waits for the next frame's Update. The Start pass
comes first, so its Starts still find what Update destroyed, and a pending Start runs on a
descendant that Destroy left enabled before its OnDestroy. Loading a save, Game's Start (just
before its first Update) destroys the new-colony screen, whose CharacterContainers register in
their Start and unregister in OnDestroy.

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
  `LineRenderer` (mods' lines: its strip as Unity's BakeMesh measures it, corners mitered; rounded
  corners and caps are drawn square), `CanvasRenderer` (world-space and screen-space-camera canvases), and the per-frame
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
  properties the game animates (UI). `ParticleSystem`: the modules the prefabs actually enable. Features no game data
  uses (mods' effects: clamp velocity, collision, lights, sub emitters, other shapes and
  texture-sheet or trail variants…) are not simulated; the system loads without each and logs it
  once (`[engine] particles: … is not simulated`).
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
scope; algorithmic equivalence is not. The runtime's float rounding differs from Unity's Mono
(D25) and so does its number formatting (D2).

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
`Plugins/lib<name>.so` on Linux, and for the macOS bundle the executable of
`Contents/PlugIns/<name>.bundle`: `GameLayout`, which tells the layout from the install's files;
macOS is groundwork only, `docs/research/metal-backend-design.md`); Steamworks.NET is built per runtime identifier (library name
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
