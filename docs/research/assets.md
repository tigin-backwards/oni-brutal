# ONI Unity data files: asset survey (Linux build, Unity 6000.3.5f2)

Source: `/home/nigit/projects/oni-linux/OxygenNotIncluded_Data` (Linux64 player, base game only, no DLC bundles).
All numbers come from parsing the files with a small Python parser written for this survey (`/tmp/claude-1000/asset-survey/`: `sfile.py` for the SerializedFile and type-tree reader, `env.py` for PPtr resolution and helpers, `bundle.py` for UnityFS, `lz4b.py` for the LZ4 block decoder, plus survey scripts). The parser deserializes objects with the 6000.3.5f2 **release** type trees from `cache/typetree/InfoJson_6000.3.5f2.json` (`ReleaseRootNode`). It checks every object by comparing the bytes it consumed against the object's byte size.
- Verified: every native object in every file deserializes to exactly its byte size. The one exception is `PlayerSettings` (1 object), which reads most fields but leaves a size mismatch near the end. MonoBehaviours are read only through the base header, because their script fields need managed layouts.
- Marked **(heuristic)**: MonoBehaviour references that came from scanning the payload for `(int32 fileID, int64 pathID)` pairs that resolve to existing objects. These have occasional false positives.
- Marked **(export)**: facts taken from the AssetRipper export at `/mnt/e/asset-ripper-out`. That export is a Windows build with DLC, a slightly different version. Path IDs of shared objects match the Linux build 1:1 (see §12).
- Note: `release_6000.3.5f2.dump` (text) is unusable as a machine source. Some field names contain spaces (`Fixed Timestep`, `near clip plane`, `Rolloff Scale`), which makes type and name ambiguous. Use `InfoJson_*.json` (`ReleaseRootNode`) instead. That is what the parser does.

---

## 1. SerializedFile format specifics

| Property | Value (all 9 data files + `Resources/unity default resources` + `Resources/unity_builtin_extra`) |
|---|---|
| Header | 20-byte legacy header, **big-endian** (`metaSize, fileSize, version, dataOffset` as u32 + endianness byte + 3 reserved). Because version ≥ 22, the header continues with `u32 metadataSize, i64 fileSize, i64 dataOffset, i64 unknown(0)` (still BE). |
| Format version | **22** (all files) |
| Metadata/object endianness | byte at offset 16 = **0 → little-endian** metadata and object data |
| Unity version string | `6000.3.5f2` |
| Target platform | **24 = StandaloneLinux64** |
| EnableTypeTree | **0 (stripped)** in every player file. The `hires_base_bundle` AssetBundle has type trees **embedded** (1). |
| Type table entry | `i32 classID, u8 isStripped, i16 scriptTypeIndex, [16B scriptID hash if classID==114 or <0 or scriptTypeIndex>=0], 16B oldTypeHash`. No tree, because EnableTypeTree=0. MonoBehaviour gets one type entry per script class (sharedassets0 has 348 type entries for 320 scripts). |
| Object table | `i32 count`, then per object: align4, `i64 pathID, i64 byteStart (relative to dataOffset), u32 byteSize, i32 typeIndex` (index into the type table) |
| Script types (`m_ScriptTypes`) | `i32 count`, per entry `i32 localFileIndex, align4, i64 pathID`. Counts: level0 8, level1 5, level2 7, resources 38, sharedassets0 320, sharedassets1 9, sharedassets2 466. **All point at fileIndex 1 = `globalgamemanagers.assets`**, which holds all 5500 MonoScripts. |
| Externals | `cstr assetPath(empty), 16B GUID, i32 type, cstr pathName`. Names are plain file names (`sharedassets0.assets`, `globalgamemanagers.assets`, `resources.assets`). Built-ins: `Library/unity default resources` (GUID 0000…0e00…, maps to `Data/Resources/unity default resources`) and `Resources/unity_builtin_extra` (GUID …0f…). PPtr fileID n means externals[n-1]; fileID 0 means the same file. |
| RefTypes (`[SerializeReference]`) | **0 in every file.** No managed-reference registry is needed. |
| UserInformation | empty string |
| Object alignment | Objects start 8-aligned inside the data section. In-object alignment follows the type-tree `0x4000` (kAlignBytes) flag. Strings and arrays align to 4 after their data when flagged. |

**Externals per file:**
- globalgamemanagers: [unity default resources, sharedassets0, unity_builtin_extra, sharedassets2, globalgamemanagers.assets, resources.assets]
- globalgamemanagers.assets: [unity default resources]
- level0: [gga, udr, sharedassets0]
- level1: [gga, udr, sharedassets1]
- level2: [gga, udr, sharedassets2]
- resources.assets: [gga, sharedassets0, udr]
- sharedassets0: [gga, udr]
- sharedassets1: [gga, udr, sharedassets0]
- sharedassets2: [gga, udr, resources.assets, sharedassets0, sharedassets1]

**Streaming (.resS / .resource):**
- `Texture2D.m_StreamData` and `Mesh.m_StreamData` are `StreamingInfo {u64 offset, u32 size, string path}`. In player files `path` is the **bare filename** relative to the Data dir (`sharedassets0.assets.resS`). Inside the bundle it is `archive:/CAB-729cbed85bef6c6a60deedcb2af2bcac/CAB-….resS`. When `size==0` the pixel data is inline in `image data` (TypelessData).
- Texture offsets in .resS are 16-byte aligned (2482/2482 in sharedassets0).
- `VideoClip.m_ExternalResources` is `StreamedResource {string m_Source, u64 m_Offset, u64 m_Size}`. All 18 point into `resources.resource`, which is a raw concatenation of .webm files (169,770,950 B).
- AudioClip would use `.resource` too, but there are **no AudioClips** (see §8).
- Neither Mesh streams data (both meshes are inline).

**globalgamemanagers highlights:**
- `BuildSettings.scenes` = `Assets/scenes/launchscene.unity`, `Assets/scenes/frontend.unity`, `Assets/scenes/backend.unity`.
- `m_GraphicsAPIs=[17]`, which is **OpenGLCore only**.
- `MonoManager.m_Scripts` has 5530 entries.
- `ShaderNameRegistry` has 285 names. 120 are non-null, which is exactly the 120 Shader objects found (used by `Shader.Find`).
- `GraphicsSettings`:
  - 34 `m_AlwaysIncludedShaders`, all in `unity_builtin_extra`.
  - `m_SpritesDefaultMaterial` = `globalgamemanagers.assets:2` (Sprites-Default, shader `Sprites/Default` at gga:3).
  - `m_ShaderDefinesPerShaderCompiler` has a single platform 15 (GLCore).
  - No SRP (built-in render pipeline). Tier rendering path is 1 (Forward).
- `PlayerSettings`: Gamma colour space (`m_ActiveColorSpace=0`), MT rendering on, default 1024×768, resizable, run in background.
- `QualitySettings`: two levels, Default and Low, with vSync=1. Low has `globalTextureMipmapLimit=1`.
- `TagManager`:
  - Layers: Default, TransparentFX, Ignore Raycast, Water, UI(5), VirtualCursor(6), Portrait(8), Overlay(9), BlockSelection(10), World(11), Lights(12), Selection(13), Flora(14), Place(15), PlaceWithDepth(16), Pickupable(17), MaskedOverlayBG(18), MaskedOverlay(19), Regions(22), Shadows(24), SimDebugView(25), Construction(26), WorldGenDebug(27), UnstableGround(29), ForceDraw(30), RTT(31).
  - No custom tags. One sorting layer.
- `AudioManager.m_DisableAudio=1`. Unity audio is off and all audio goes through FMOD.
- `TimeManager` fixed timestep = 4233599/141120000 s ≈ 0.03 s.
- Other files in the Data dir:
  - `RuntimeInitializeOnLoads.json`: KleiItemsStatusRefresher.Initialize, Klei.KProfilerPlugin.InitModule, plus Burst/Collections/MemoryProfiler initializers.
  - `ScriptingAssemblies.json`
  - `boot.config`: gfx jobs enabled, `gfx-threading-mode=4`.

## 2. Per-file object counts by ClassID, and MonoBehaviour script histograms

Sizes in brackets are serialized object bytes inside the .assets file (excluding .resS).

**globalgamemanagers** — 19 objects, 19 type entries, size 610,068 B

PlayerSettings(129) 1, InputManager(13) 1, TagManager(78) 1, AudioManager(11) 1, ShaderNameRegistry(94) 1, MonoManager(116) 1, GraphicsSettings(30) 1, TimeManager(5) 1, DelayedCallManager(98) 1, PhysicsManager(55) 1, BuildSettings(141) 1, QualitySettings(47) 1, ResourceManager(147) 1, NavMeshProjectSettings(126) 1, Physics2DSettings(19) 1, RuntimeInitializeOnLoadManager(300) 1, StreamingManager(1403656975) 1, VFXManager(937362698) 1, MultiplayerManager(655991488) 1

**globalgamemanagers.assets** — 5503 objects, 4 type entries, size 719,488 B

MonoScript(115) 5500, PreloadData(150) 1, Material(21) 1, Shader(48) 1

**level0** — 30 objects, 16 type entries, size 4,240 B

MonoBehaviour(114) 11, GameObject(1) 6, RectTransform(224) 4, Transform(4) 2, CanvasRenderer(222) 2, Canvas(223) 2, RenderSettings(104) 1, LightmapSettings(157) 1, NavMeshSettings(196) 1

**level1** — 12 objects, 10 type entries, size 2,072 B

MonoBehaviour(114) 5, GameObject(1) 2, Transform(4) 2, RenderSettings(104) 1, LightmapSettings(157) 1, NavMeshSettings(196) 1

**level2** — 20 objects, 15 type entries, size 3,152 B

MonoBehaviour(114) 7, GameObject(1) 4, Transform(4) 3, Camera(20) 1, RenderSettings(104) 1, LightmapSettings(157) 1, NavMeshSettings(196) 1, Canvas(223) 1, RectTransform(224) 1

**resources.assets** — 219 objects, 49 type entries, size 8,151,296 B

MonoBehaviour(114) 69, Material(21) 54, Texture2D(28) 31 [5.5 MB], VideoClip(329) 18, TextAsset(49) 17 [1.4 MB], GameObject(1) 6, RectTransform(224) 6, Shader(48) 5, Font(128) 5, CanvasRenderer(222) 4, MonoScript(115) 3, RenderTexture(84) 1

**sharedassets0.assets** — 26444 objects, 348 type entries, size 391,633,964 B

MonoBehaviour(114) 8595 [5.9 MB], TextAsset(49) 3684 [312.9 MB], GameObject(1) 3423, RectTransform(224) 3336, Texture2D(28) 2545 [64.9 MB], CanvasRenderer(222) 2362, Sprite(213) 1954 [4.4 MB], Material(21) 199, Transform(4) 87, Shader(48) 73, Canvas(223) 54, SpriteRenderer(212) 29, CanvasGroup(225) 20, ParticleSystem(198) 12, ParticleSystemRenderer(199) 12, AnimationClip(74) 10, AnimatorController(91) 8, Font(128) 8, Animator(95) 8, MeshRenderer(23) 5, MeshFilter(33) 5, BoxCollider2D(61) 4, Animation(111) 3, SphereCollider(135) 2, SpriteAtlas(687078895) 2, PreloadData(150) 1, Mesh(43) 1, Camera(20) 1, VideoPlayer(328) 1

**sharedassets1.assets** — 41 objects, 16 type entries, size 7,920 B

Texture2D(28) 21, MonoBehaviour(114) 9, GameObject(1) 4, Transform(4) 3, PreloadData(150) 1, Camera(20) 1, Canvas(223) 1, RectTransform(224) 1

**sharedassets2.assets** — 24779 objects, 490 type entries, size 93,433,564 B

MonoBehaviour(114) 10103 [1.7 MB], GameObject(1) 5377, RectTransform(224) 5260, CanvasRenderer(222) 3695, Transform(4) 117, Texture2D(28) 48, Canvas(223) 39, Material(21) 32, CanvasGroup(225) 28, MeshRenderer(23) 19, MeshFilter(33) 19, Animator(95) 19, AnimationClip(74) 4, Shader(48) 3, Texture2DArray(187) 3 [89.5 MB], AnimatorController(91) 2, BoxCollider2D(61) 2, VideoPlayer(328) 2, PreloadData(150) 1, Mesh(43) 1, Avatar(90) 1, Font(128) 1, Sprite(213) 1, Camera(20) 1, SpriteRenderer(212) 1

**Resources/unity default resources** — 99 objects, 8 type entries, size 5,883,780 B

Texture2D(28) 37, MonoScript(115) 30, Mesh(43) 13, ComputeShader(72) 9 [5.4 MB], Shader(48) 6, Material(21) 2, Font(128) 1, MonoBehaviour(114) 1

**Resources/unity_builtin_extra** — 32 objects, 1 type entries, size 264,348 B

Shader(48) 32

### MonoBehaviour script histograms (m_Script → MonoScript in globalgamemanagers.assets)
Assembly abbreviations: A=Assembly-CSharp, F=Assembly-CSharp-firstpass, UI=UnityEngine.UI, TMP=Unity.TextMeshPro, FMOD=FMODUnity. Every m_Script resolved. All MonoScripts live in `globalgamemanagers.assets` (5500), except 3 in `resources.assets` and 30 in `unity default resources`.

**level0** — 11 MonoBehaviours, 8 distinct scripts; by assembly: {'F': 2, 'UI': 8, 'A': 1}:

Image[UI] 2, GraphicRaycaster[UI] 2, CanvasScaler[UI] 2, KProfilerBegin[F] 1, EventSystem[UI] 1, StandaloneInputModule[UI] 1, LaunchInitializer[A] 1, KProfilerEnd[F] 1

**level1** — 5 MonoBehaviours, 5 distinct scripts; by assembly: {'UI': 2, 'A': 3}:

EventSystem[UI] 1, SceneInitializerLoader[A] 1, VirtualInputModule[A] 1, StandaloneInputModule[UI] 1, InputModuleSwitch[A] 1

**level2** — 7 MonoBehaviours, 7 distinct scripts; by assembly: {'UI': 4, 'A': 3}:

EventSystem[UI] 1, SceneInitializerLoader[A] 1, InputModuleSwitch[A] 1, StandaloneInputModule[UI] 1, VirtualInputModule[A] 1, GraphicRaycaster[UI] 1, CanvasScaler[UI] 1

**resources.assets** — 69 MonoBehaviours, 38 distinct scripts; by assembly: {'FMOD': 13, 'F': 6, 'TMP': 32, 'A': 12, 'UI': 6}:

TMPro.TMP_FontAsset[TMP] 14, TMPro.TMP_SpriteAsset[TMP] 12, TMPro.TMP_ColorGradient[TMP] 4, Image[UI] 2, LayoutElement[UI] 2, ToolTip[F] 2, RawImage[UI] 2, FMODUnity.PlatformVisionOS[FMOD] 1, FMODUnity.PlatformMobileLow[FMOD] 1, FMODUnity.PlatformIOS[FMOD] 1, FMODUnity.PlatformDefault[FMOD] 1, FMODUnity.Settings[FMOD] 1, NodeEditorFramework.NodeCanvas[F] 1, TMPro.TMP_StyleSheet[TMP] 1, TMPro.TMP_Settings[TMP] 1, KAnimGroupFile[F] 1, Audio[F] 1, AudioMixerSnapshots[A] 1, BuildingCellVisualizerResources[A] 1, Db[A] 1, GameAudioSheets[A] 1, GlobalResources[A] 1, ProgressBarsConfig[A] 1, StateMachineDebuggerSettings[A] 1, NodeEditorFramework.NodeEditorState[F] 1, FMODUnity.PlatformPlayInEditor[FMOD] 1, FMODUnity.PlatformWindows[FMOD] 1, FMODUnity.PlatformMobileHigh[FMOD] 1, FMODUnity.PlatformLinux[FMOD] 1, FMODUnity.PlatformAndroid[FMOD] 1, FMODUnity.PlatformMac[FMOD] 1, FMODUnity.PlatformAppleTV[FMOD] 1, FMODUnity.PlatformWebGL[FMOD] 1, HealthBar[A] 1, KSelectableProgressBar[A] 1, ProgressBar[A] 1, KSelectableHealthBar[A] 1, KBoxCollider2D[A] 1

**sharedassets0.assets** — 8595 MonoBehaviours, 320 distinct scripts; by assembly: {'TMP': 61, 'F': 3409, 'A': 1378, 'UI': 3745, 'ImGui': 1, 'FMOD': 1}; top 50:

KAnimFile[F] 2152, LayoutElement[UI] 1556, Image[UI] 1192, LocText[A] 578, VerticalLayoutGroup[UI] 305, KImage[F] 283, HorizontalLayoutGroup[UI] 275, KButton[F] 222, ToolTip[F] 215, SetTextStyleSetting[F] 178, HierarchyReferences[A] 127, KBatchedAnimController[A] 103, ContentSizeFitter[UI] 98, MultiToggle[A] 91, Mask[UI] 80, KPrefabID[F] 68, TextureAtlas[F] 67, Scrollbar[UI] 46, KScrollRect[F] 42, KBatchedAnimCanvasRenderer[F] 42, GraphicRaycaster[UI] 36, AspectRatioFitter[UI] 36, TextLinkHandler[A] 35, TMPro.TextMeshProUGUI[TMP] 29, SymbolOverrideController[A] 28, CommentMB[A] 26, TextStyleSetting[F] 25, RectMask2D[UI] 24, ColorStyleSetting[F] 23, BlockTileDecorInfo[A] 23, GridLayoutGroup[UI] 20, Text[UI] 17, KInputTextField[F] 17, ImageToggleState[F] 16, UICurvePath[A] 16, Toggle[UI] 15, TMPro.TMP_FontAsset[TMP] 14, RawImage[UI] 14, TMPro.TMP_SubMeshUI[TMP] 10, LoopingSounds[A] 9, UIDupeRandomizer[A] 9, KRectStretcher[F] 8, VideoOverlay[A] 8, KChildFitter[F] 7, Button[UI] 7, KToggle[F] 6, KSelectable[A] 6, UILineRenderer[F] 6, URLOpenFunction[A] 6, MotdBox[A] 6

**sharedassets1.assets** — 9 MonoBehaviours, 9 distinct scripts; by assembly: {'A': 7, 'UI': 2}:

LightingSettings[A] 1, KCanvasScaler[A] 1, Lighting[A] 1, FrontEndManager[A] 1, GraphicRaycaster[UI] 1, SceneInitializer[A] 1, CanvasScaler[UI] 1, FixGraphicsCorruption[A] 1, ScreenResize[A] 1

**sharedassets2.assets** — 10103 MonoBehaviours, 466 distinct scripts; by assembly: {'F': 2027, 'TMP': 23, 'A': 2021, 'UI': 6031, 'FMOD': 1}; top 50:

LayoutElement[UI] 2533, Image[UI] 1785, LocText[A] 910, VerticalLayoutGroup[UI] 660, KImage[F] 496, HorizontalLayoutGroup[UI] 469, ToolTip[F] 443, SetTextStyleSetting[F] 392, KButton[F] 252, ContentSizeFitter[UI] 172, HierarchyReferences[A] 168, MultiToggle[A] 157, TextLinkHandler[A] 123, Mask[UI] 108, Scrollbar[UI] 97, KScrollRect[F] 93, ImageToggleState[F] 88, KBatchedAnimController[A] 60, KToggle[F] 53, RectMask2D[UI] 50, KInputTextField[F] 38, GraphicRaycaster[UI] 34, GridLayoutGroup[UI] 33, KBatchedAnimCanvasRenderer[F] 24, TextStyleSetting[F] 23, UILineRenderer[F] 22, ColorStyleSetting[F] 21, DescriptorPanel[A] 21, KNumberInputField[F] 20, KSlider[F] 18, HoverTextConfiguration[A] 16, SymbolOverrideController[A] 16, TMPro.TextMeshProUGUI[TMP] 16, Button[UI] 15, Outline[UI] 15, LoopingSounds[A] 14, ToggleGroup[UI] 12, CanvasScaler[UI] 12, CrewPortrait[A] 12, Text[UI] 10, ImageToggleStateThrobber[F] 10, KRectStretcher[F] 10, KChildFitter[F] 8, KCanvasScaler[A] 8, TableRow[A] 7, DropDown[A] 7, DropDownEntry[A] 7, GenericUIProgressBar[A] 7, SparkLayer[A] 7, GraphedLine[A] 7

**GameObject component counts** (sharedassets0 / sharedassets2): most GameObjects have 2–5 components, and the max is 52 / 16. Native component classes seen on GameObjects: Transform, RectTransform, CanvasRenderer, Canvas, CanvasGroup, SpriteRenderer, MeshRenderer, MeshFilter, Animator, Animation, ParticleSystem and ParticleSystemRenderer (12 each), BoxCollider2D, SphereCollider, Camera, VideoPlayer (3).

## 3. Scenes and the bootstrap chain

| Build index | File | Scene | Objects | Content |
|---|---|---|---|---|
| 0 | level0 (4,240 B) | `Assets/scenes/launchscene.unity` | 30 | Boot canvas and LaunchInitializer |
| 1 | level1 (2,072 B) | `Assets/scenes/frontend.unity` | 12 | SceneInitializerLoader and EventSystem |
| 2 | level2 (3,152 B) | `Assets/scenes/backend.unity` | 20 | camera, SceneInitializerLoader, EventSystem, Canvas |

The scenes are almost empty. The real content is **prefab assets in sharedassetsN**, which a single loader component instantiates. sharedassetsN holds the assets first referenced by sceneN: sharedassets0 has 174 root prefabs, sharedassets1 has 4, sharedassets2 has 288, and resources.assets has 2 (HealthBar_UI, ProgressBar_UI). All three scenes also contain RenderSettings (no fog, ambient mode 3, `m_SpotCookie` = builtin_extra:10001), LightmapSettings (empty) and NavMeshSettings (null).

**level0 / launchscene** (roots):
- `EventSystem`: Transform, EventSystem[UI], StandaloneInputModule[UI]
- `Launch Initializer`: Transform, **LaunchInitializer[A]**, KProfilerBegin[F], KProfilerEnd[F]. LaunchInitializer references these root prefabs in sharedassets0 **(heuristic, ordered)**: KObjectManager:8491, BundledAssetsLoader:9824, KScreenManager:8496, ScreenPrefabs:8487, Global:8495, MusicManager:8533, InputInit:8743, Audio:8693, EffectPrefabs:8492, EntityPrefabs:8493, GlobalAssets:9016, GameAssets:8611, CustomGameSettings:9483, ImGui:11589, VirtualCursorController:11431.
- `BootCanvas` (layer UI): RectTransform, Canvas, CanvasScaler, GraphicRaycaster
  - `BootBG`: RectTransform, CanvasRenderer, Image
  - `BootLogo`: RectTransform, CanvasRenderer, Image, with sprite `klei_logo` (sharedassets0:6696)
- `Canvas`: RectTransform, Canvas, CanvasScaler, GraphicRaycaster

**level1 / frontend:**
- `SceneInitializerLoader`: Transform, SceneInitializerLoader[A]. It references MB `sharedassets1:38`, which is **SceneInitializer** on prefab `SceneInitializerFE`.
- `EventSystem`: EventSystem, StandaloneInputModule, InputModuleSwitch[A], VirtualInputModule[A, namespace UnityEngine.EventSystems]
- sharedassets1 root prefabs:
  - `SceneInitializerFE` (SceneInitializer refs: Texture2D radiation1, FrontEndManager, Lighting)
  - `FrontEndManager`: RectTransform, FrontEndManager[A], Canvas, GraphicRaycaster, KCanvasScaler[A], CanvasScaler
  - `FrontEndCamera`: ScreenResize[A], Camera, FixGraphicsCorruption[A]
  - `Lighting`: Lighting[A], which references the LightingSettings MB

**level2 / backend:**
- `camera`: Transform and Camera
- `SceneInitializerLoader`, which references sharedassets2:14738 = **SceneInitializer** on prefab `SceneInitializer` (+GridVisualizer). Its references **(heuristic)**: SaveGame (sa0:9036), cameraMain, PreUpdateScheduler, SaveLoader, Lighting (sa1:24), WorldRenderer, Game, PathProber, DebugCellDrawer, UISounds, Sounds, MinionGroupProber (sa2).
- `EventSystem` (same as level1)
- `Canvas`: CanvasScaler, GraphicRaycaster
- sharedassets2 roots: the ~40 Tool prefabs (DigTool, BuildTool, Sandbox*…), `Game`, `WorldRenderer` (World, WaterCubes, SimDebugView, PropertyTextures, FlowOffsetRenderer, Pathfinding, WorldDamage…), `CameraController`, `GameScreenManager`, cursors, FX prefabs, and ~150 UI side screens.

The bootstrap chain is therefore: **launchscene → LaunchInitializer instantiates the 15 global prefabs (sharedassets0) → loads frontend → SceneInitializerLoader instantiates SceneInitializerFE (sharedassets1) → … → backend → SceneInitializer prefab (sharedassets2) instantiates Game/World/etc.** The scenes themselves use only Transform, RectTransform, Canvas, CanvasRenderer and Camera, plus MonoBehaviours for UGUI (Image, CanvasScaler, GraphicRaycaster, EventSystem, StandaloneInputModule) and game classes.

## 4. Resources (ResourceManager.m_Container in globalgamemanagers)

**505 container entries**, plus 5448 `m_DependentAssets`.
- Targets by file: sharedassets0 359, resources.assets 130, globalgamemanagers.assets 10, null 6. The 6 null entries are the 4 `builtin_shaders/defaultresourcesextra/klei{common,mrt,noise,sim}` (shader includes, stripped) plus duplicate null entries for `gameassets` and `kbatched_anim`.
- Several paths map to multiple objects (for example a Texture2D and its Sprite, or a MonoScript and the MB).
- Types: Sprite 250, Shader 61, Texture2D 42, Material 41, TMP_FontAsset 19, VideoClip 18, MonoScript 13, TMP_SpriteAsset 13, TextAsset 8, TMP_ColorGradient 4, GameObject 2 (`gameassets`, `globalassets`), RenderTexture 1 (`targetrendertexture`), plus single MonoBehaviour config assets: KAnimGroupFile (`animgrouptags`), Audio, AudioMixerSnapshots, BuildingCellVisualizerResources, Db, DiseaseVisualization, FMODUnity Settings (+13 Platform* sub-objects under `fmodstudiosettings`), GameAudioSheets, GlobalResources, ProgressBarsConfig, NodeCanvas, StateMachineDebuggerSettings, TMP_StyleSheet, SubstanceTable (`substancehookup`), TMP_Settings.

By top-level folder (paths are lower-cased):

| Folder | Entries | Types | Examples |
|---|---|---|---|
| `(root)` | 146 | Shader 61, MonoScript 13, Texture2D 12, Material 12, TMP_FontAsset 11, TextAsset 6, misc MB | `kbatched_anim`, `liquid`, `substance`, `lightbuffer`, `notosans-regular` (FontAsset+Material+Texture), `db`, `globalresources`, `tmp settings`, `names_female`, `linebreaking leading characters` |
| `sprite assets/` | 271 | Sprite 246, TMP_SpriteAsset 12, Texture2D 11, TextAsset 2 | `ps4sheet` (55), `steamdecksheet` (55), `steamcontrollersheet` (39), `xb360sheet`, `xboxonesheet`, `oni_sprite_assets` (27), `default sprite asset`, `inlineicons` |
| `fonts & materials/` | 44 | Material 28, TMP_FontAsset 8, Texture2D 8 | `liberationsans sdf`, `anton sdf`, `bangers sdf - drop shadow`, `roboto-bold sdf - urp` |
| `video_webm/` | 18 | VideoClip | `intro/spaced_out_intro`, `tutorials/{digging,insulation,locomotion,morale,piping,power}`, `victoryloops/*`, `victoryshorts/*`, `dlc4/*`, `dlc5/aquatic_loop` |
| `textures/` | 15 | Texture2D 11, Sprite 3, Material 1 | `aaline`, `boxselection`, `ne_box` (NodeEditor UI) |
| `builtin_shaders/` | 4 | null | `defaultresourcesextra/kleicommon` … |
| `color gradient presets/` | 4 | TMP_ColorGradient | |
| `saves/`, `sprites/`, `style sheets/` | 1 each | NodeCanvas, TMP_SpriteAsset (emojione), TMP_StyleSheet | |

All 61 Klei game shaders are reachable by Resources name (the code does `Resources.Load<Shader>("kbatched_anim")` and similar). This explains why most of them have zero Materials referencing them (§5).

## 5. Shaders

There are **120 Shader objects**: sharedassets0 73, unity_builtin_extra 32, unity default resources 6, resources.assets 5, sharedassets2 3, globalgamemanagers.assets 1.

**GPU program platforms:** every game shader (all files except `unity default resources`) contains **only platform 15 = OpenGLCore**. Each blob is an LZ4-block compressed sub-program table, and every sub-program carries **plain GLSL source text** (`#version 150`, `GL_ARB_explicit_attrib_location`, HLSLcc output with `#ifdef VERTEX / #ifdef FRAGMENT` sections, `hlslcc_mtx4x4unity_ObjectToWorld` uniform arrays, `UNITY_BINDING` UBO macros). The Linux build has **no SPIR-V or Vulkan programs at all**. The 6 shaders in `unity default resources` (the shared engine file) carry D3D11, GLES3, Metal, GLCore, Vulkan(18) and 26. The SPIR-V magic was not found in those blobs, presumably because the Vulkan programs are SMOL-V compressed **(unverified)**.

Consequences:
- The original GLSL can be fed directly to an OpenGL 3.2+ backend, or compiled with glslang to SPIR-V for Vulkan.
- That needs the Unity uniform/UBO naming conventions (`unity_ObjectToWorld`, `unity_MatrixVP`, `_MainTex_ST`, `$Globals` blocks).
- It also needs the per-subprogram parameter tables from `SerializedProgramParameters` (VectorParams, MatrixParams, TextureParams with sampler binding, ConstantBuffers, Samplers) and the `m_KeywordIndices` used for variant selection.
- For hand-rewriting shaders, the GLSL is the semantic reference.

**AssetRipper decompiled shaders:**
- There are 75 `.shader` files: `ExportedProject/Assets/Resources/*.shader` (61, the Resources-named Klei shaders) and `Assets/Shader/*.shader` (14: TMP, Klei_Substance/LiquidSubstance/Standard Unlit, Unlit_ImGui, Hidden_BlurEffectConeTap, Hidden_ColorCorrection3DLut).
- **All 75 are `//DummyShaderTextExporter` stubs.** They have a correct `Properties {}` block and tags, but a trivial pass (pos transform plus a constant colour or a single `_MainTex` sample).
- They are **not usable as semantic references** except for property names, defaults and attributes. The real semantics must come from the GLSL inside the Linux build's shader blobs. Example: Klei/Lightbuffer's real vertex shader computes `vs_TEXCOORD0.zw` from `_WorldSizeInfo`, and the stub omits this.

**Material cross-reference:**
- 288 Materials: sharedassets0 199, resources 54, sharedassets2 32, default resources 2, gga 1.
- Most-referenced shaders: `Klei/Substance` 114 (per-element materials `matXxx`), `TextMeshPro/Distance Field` 26, `Unlit/Beached_Parallax` 24, `Klei/Unlit Transparent` 21, `GUI/Text Shader` 15 (legacy Font materials), `TextMeshPro/Sprite` 13, `TextMeshPro/Mobile/Distance Field` 12.
- **9 materials have a null m_Shader:** `animMat` (sa0), `animOverlayMat` and `animBldgPlaceMat` (resources), and TMP URP/HDRP sample materials. The game assigns kanim shaders at runtime.
- 61 of the 120 shaders are referenced by no Material. These are loaded by name via Resources or `Shader.Find`, or are always-included built-ins.

Full shader table (kw = keywords excluding the 4 STEREO_*/UNITY_SINGLE_PASS_STEREO entries present in every shader; Mats = Materials whose m_Shader points at it; Blob = compressed→decompressed bytes):

| File:PathID | Shader | SS/Pass | SubProgs | Kw | Platforms | Blob (lz4→raw) | Mats | Resources path |
|---|---|---|---|---|---|---|---|---|
| sharedassets0.assets:2763 | `Klei/AnimationInvisible` | 1/1 | 1 | - | GLCore | 336→808 | 0 | kaniminvisible |
| sharedassets0.assets:2814 | `Klei/AnimationSimple` | 1/1 | 1 | - | GLCore | 6654→32172 | 1 | kanimsimple |
| sharedassets0.assets:2797 | `Klei/Area Visualizer` | 1/1 | 1 | - | GLCore | 1207→3380 | 0 | klei areavisualizer |
| sharedassets0.assets:2785 | `Klei/BackWallArray` | 1/1 | 1 | - | GLCore | 1872→6204 | 1 | backwallarray |
| sharedassets0.assets:2806 | `Klei/BatchedAnimation` | 1/1 | 1 | DISABLE_K_LIGHTING | GLCore | 7247→35780 | 0 | kbatched_anim |
| sharedassets0.assets:2813 | `Klei/BatchedAnimationHuman` | 1/1 | 1 | DISABLE_K_LIGHTING | GLCore | 7207→35608 | 0 | kbatched_anim_human |
| sharedassets0.assets:2762 | `Klei/BatchedAnimationPstTemperature` | 1/4 | 3 | DISABLE_K_LIGHTING | GLCore | 8427→82944 | 0 | kbatched_anim_pst |
| sharedassets0.assets:2769 | `Klei/BatchedAnimationShine` | 1/1 | 1 | DISABLE_K_LIGHTING | GLCore | 6099→30236 | 0 | kbatched_anim_shine |
| sharedassets0.assets:2812 | `Klei/BatchedAnimationUI` | 1/1 | 1 | DISABLE_K_LIGHTING, UNITY_UI_ALPHACLIP | GLCore | 5496→29220 | 0 | kbatched_anim_ui |
| sharedassets0.assets:2774 | `Klei/Biome/Unlit Transparent` | 1/1 | 1 | - | GLCore | 2489→9156 | 2 | kleitransparentbiome |
| sharedassets0.assets:2818 | `Klei/BloomedParticleShader` | 1/1 | 2 | SOFTPARTICLES_ON, FOG_LINEAR, FOG_EXP, FOG_EXP2 | GLCore | 1599→7428 | 4 | bloomedparticleshader |
| sharedassets0.assets:2808 | `Klei/Bubbles` | 1/1 | 1 | - | GLCore | 1487→4412 | 1 | kbubbles |
| sharedassets0.assets:2760 | `Klei/BuildingCell` | 1/1 | 1 | - | GLCore | 811→2364 | 0 | buildingcell |
| sharedassets0.assets:2798 | `Klei/ConduitBall` | 1/1 | 1 | - | GLCore | 3279→12292 | 0 | conduitball |
| sharedassets0.assets:2759 | `Klei/DLC4/DemoliorBackground` | 1/1 | 1 | - | GLCore | 2541→8060 | 1 | kleidemoliorbackground |
| sharedassets0.assets:2775 | `Klei/DLC4/LargeImpactorCometShader` | 1/1 | 1 | - | GLCore | 2708→9340 | 0 | largeimpactorcometshader |
| sharedassets0.assets:2790 | `Klei/FallingWater` | 1/1 | 1 | - | GLCore | 1077→2964 | 1 | kfallingwater |
| sharedassets0.assets:2803 | `Klei/Flow` | 1/1 | 1 | - | GLCore | 877→2352 | 0 | flow |
| sharedassets0.assets:2765 | `Klei/GasTest` | 1/1 | 1 | - | GLCore | 3028→10996 | 1 | gastest |
| sharedassets0.assets:2809 | `Klei/LargeImpactorFragments` | 1/1 | 1 | FOG_LINEAR, FOG_EXP, FOG_EXP2 | GLCore | 2551→8208 | 1 | largeimpactorfragments |
| sharedassets0.assets:2815 | `Klei/LerpEffect` | 1/1 | 1 | - | GLCore | 940→2780 | 0 | lerpeffect |
| sharedassets0.assets:2793 | `Klei/Lightbuffer` | 1/1 | 1 | - | GLCore | 1124→3188 | 2 | lightbuffer |
| sharedassets0.assets:2772 | `Klei/Liquid` | 1/1 | 48 | DEBUG_INSPECT, ENABLE_CAUSTIC, ENABLE_WAVE, FREEZING_NON_LINEAR, _SAMPLEFILTERING_BICUBIC_HERMITE, _SAMPLEFILTERING_BILINEAR, _SAMPLEFILTERING_NONE | GLCore | 167912→1872836 | 1 | liquid |
| sharedassets0.assets:2794 | `Klei/LiquidSubstance` | 1/1 | 2 | OPAQUE, ALPHA | GLCore | 2346→15972 | 5 | liquidsubstance |
| sharedassets0.assets:2799 | `Klei/NorthernLights` | 1/1 | 1 | FOG_LINEAR, FOG_EXP, FOG_EXP2 | GLCore | 1692→5256 | 1 | northernlights |
| sharedassets0.assets:2788 | `Klei/PostFX/BloomComposite` | 1/1 | 1 | - | GLCore | 870→2548 | 0 | bloomcomposite |
| sharedassets0.assets:2810 | `Klei/PostFX/BloomMask` | 1/1 | 1 | - | GLCore | 843→2344 | 0 | bloommask |
| sharedassets0.assets:2792 | `Klei/PostFX/Blur` | 1/1 | 1 | - | GLCore | 1078→3368 | 0 | blur |
| sharedassets0.assets:2780 | `Klei/PostFX/CameraRenderTexture` | 1/1 | 1 | - | GLCore | 838→2236 | 0 | camerarendertexture |
| sharedassets0.assets:2817 | `Klei/PostFX/ClusterCoverPostFX` | 1/1 | 1 | - | GLCore | 1090→3120 | 0 | clustercoverpostfx |
| sharedassets0.assets:2784 | `Klei/PostFX/Desaturate` | 1/1 | 1 | - | GLCore | 794→2156 | 0 | desaturate |
| sharedassets0.assets:2777 | `Klei/PostFX/Fill` | 1/1 | 1 | - | GLCore | 815→2224 | 0 | fill |
| sharedassets0.assets:2796 | `Klei/PostFX/FogOfWar` | 1/1 | 1 | - | GLCore | 968→2788 | 0 | fogofwarpostfx |
| sharedassets0.assets:2768 | `Klei/PostFX/FullScreen` | 1/1 | 1 | - | GLCore | 604→1208 | 0 | fullscreen |
| sharedassets0.assets:2770 | `Klei/PostFX/GridCompositor` | 1/1 | 1 | - | GLCore | 1382→4048 | 0 | gridcompositor |
| sharedassets0.assets:2783 | `Klei/PostFX/HueSaturation` | 1/1 | 1 | - | GLCore | 1976→6744 | 0 | huesaturation |
| sharedassets0.assets:2761 | `Klei/PostFX/LargeImpactorVisualizerShader` | 1/1 | 1 | - | GLCore | 3205→10752 | 0 | largeimpactorvisualizershader |
| sharedassets0.assets:2756 | `Klei/PostFX/LightBufferCompositor` | 1/1 | 1 | DISABLE_TEMPERATURE_PARTICLES | GLCore | 3702→13204 | 0 | lightbuffercompositor |
| sharedassets0.assets:2786 | `Klei/PostFX/LightBufferShadowed` | 1/1 | 1 | - | GLCore | 1467→4160 | 1 | lightbuffershadowed |
| sharedassets0.assets:2782 | `Klei/PostFX/Range` | 1/1 | 1 | - | GLCore | 1702→5236 | 0 | range |
| sharedassets0.assets:2801 | `Klei/PostFX/RocketLaunchCondition` | 1/1 | 1 | - | GLCore | 1902→5824 | 0 | rocketlaunchcondition |
| sharedassets0.assets:2764 | `Klei/PostFX/ScannerNetwork` | 1/1 | 1 | - | GLCore | 1902→5824 | 0 | scannernetwork |
| sharedassets0.assets:2804 | `Klei/PostFX/SimDebugViewCompositor` | 1/1 | 1 | - | GLCore | 1188→3324 | 0 | simdebugviewcompositor |
| sharedassets0.assets:2773 | `Klei/PostFX/SkyVisibility` | 1/1 | 1 | - | GLCore | 1895→5760 | 0 | skyvisibility |
| sharedassets0.assets:2802 | `Klei/Prioritizable` | 1/1 | 1 | - | GLCore | 842→2328 | 0 | prioritiable |
| sharedassets0.assets:2767 | `Klei/RocketPath` | 1/1 | 1 | - | GLCore | 906→2400 | 0 | rocketpath |
| sharedassets0.assets:2791 | `Klei/Sprite2DMask` | 1/1 | 1 | - | GLCore | 950→2656 | 4 | ksprite2dmask |
| sharedassets0.assets:2771 | `Klei/Standard Unlit` | 1/2 | 8 | _ALPHATEST_ON, _REQUIRE_UV2, INSTANCING_ON, PROCEDURAL_INSTANCING_ON, SOFTPARTICLES_ON, _ALPHABLEND_ON, _ALPHAPREMULTIPLY_ON, _ALPHAMODULATE_ON, _COLOROVERLAY_ON, _COLORCOLOR_ON, _COLORADDSUBDIFF_ON, _NORMALMAP, _EMISSION, _FADING_ON, EFFECT_BUMP, FOG_LINEAR, FOG_EXP, FOG_EXP2, SHADOWS_DEPTH, SHADOWS_CUBE, POINT, SPOT | GLCore | 3207→38512 | 1 |  |
| sharedassets0.assets:2766 | `Klei/Stars ` | 1/1 | 2 | ENABLE_STAR_TRAVELLING | GLCore | 3652→17192 | 3 | stars |
| sharedassets0.assets:2757 | `Klei/StatusItem` | 1/1 | 1 | - | GLCore | 1393→5728 | 0 | statusitem |
| sharedassets0.assets:2800 | `Klei/Stipple` | 1/1 | 1 | - | GLCore | 1139→3388 | 1 | klei stipple |
| sharedassets0.assets:2758 | `Klei/Substance` | 1/1 | 4 | OPAQUE, ALPHA, MATTE, SHINY | GLCore | 5971→53588 | 114 | substance |
| sharedassets0.assets:2778 | `Klei/TexturePage` | 1/1 | 1 | - | GLCore | 695→1684 | 0 | texturepage |
| sharedassets0.assets:2787 | `Klei/TiledBlock` | 1/1 | 2 | DISABLE_SHINE, ENABLE_SHINE | GLCore | 3961→25604 | 1 | ktiledblock |
| sharedassets0.assets:2795 | `Klei/TiledWall` | 1/1 | 1 | - | GLCore | 2907→10780 | 1 | ktiledwall |
| sharedassets0.assets:2789 | `Klei/Unlit Transparent` | 1/1 | 1 | - | GLCore | 1111→3032 | 21 | klei transparent |
| sharedassets0.assets:2748 | `Legacy Shaders/Diffuse` | 1/3 | 17 | INSTANCING_ON, FOG_LINEAR, FOG_EXP, FOG_EXP2, DIRECTIONAL, LIGHTPROBE_SH, SHADOWS_SHADOWMASK, DYNAMICLIGHTMAP_ON, LIGHTMAP_ON, LIGHTMAP_SHADOW_MIXING, DIRLIGHTMAP_COMBINED, SHADOWS_SCREEN, VERTEXLIGHT_ON, POINT, SPOT, POINT_COOKIE, DIRECTIONAL_COOKIE, UNITY_HDR_ON, EDITOR_VISUALIZATION, SHADOWS_DEPTH, SHADOWS_CUBE | GLCore | 5564→86428 | 0 |  |
| sharedassets0.assets:2750 | `Legacy Shaders/Particles/Additive` | 1/1 | 2 | SOFTPARTICLES_ON, FOG_LINEAR, FOG_EXP, FOG_EXP2 | GLCore | 1521→7120 | 1 |  |
| sharedassets0.assets:2747 | `Legacy Shaders/VertexLit` | 1/3 | 6 | FOG_LINEAR, FOG_EXP, FOG_EXP2, INSTANCING_ON, SHADOWS_DEPTH, SHADOWS_CUBE, POINT, SPOT | GLCore | 3757→37848 | 0 |  |
| sharedassets0.assets:2807 | `Lines/Colored Blended` | 1/1 | 1 | - | GLCore | 765→1852 | 1 | navpath |
| sharedassets0.assets:2751 | `Particles/Standard Surface` | 1/4 | 104 | INSTANCING_ON, PROCEDURAL_INSTANCING_ON, _ALPHATEST_ON, _ALPHABLEND_ON, _ALPHAPREMULTIPLY_ON, _ALPHAMODULATE_ON, _REQUIRE_UV2, SHADOWS_DEPTH, SHADOWS_CUBE, _METALLICGLOSSMAP, SOFTPARTICLES_ON, SHADOWS_SHADOWMASK, FOG_LINEAR, FOG_EXP, FOG_EXP2, _NORMALMAP, _FADING_ON, EFFECT_BUMP, DIRECTIONAL, LIGHTPROBE_SH, SHADOWS_SCREEN, LIGHTMAP_SHADOW_MIXING, VERTEXLIGHT_ON, _EMISSION, UNITY_HDR_ON, POINT, SPOT | GLCore | 28222→1092240 | 1 |  |
| sharedassets0.assets:2752 | `Particles/Standard Unlit` | 1/3 | 16 | INSTANCING_ON, PROCEDURAL_INSTANCING_ON, _ALPHATEST_ON, _ALPHABLEND_ON, _ALPHAPREMULTIPLY_ON, _ALPHAMODULATE_ON, _REQUIRE_UV2, SHADOWS_DEPTH, SHADOWS_CUBE, SOFTPARTICLES_ON, FOG_LINEAR, FOG_EXP, FOG_EXP2, _NORMALMAP, _FADING_ON, EFFECT_BUMP, _EMISSION, _COLOROVERLAY_ON, _COLORCOLOR_ON, _COLORADDSUBDIFF_ON, POINT, SPOT | GLCore | 5022→77996 | 2 |  |
| sharedassets0.assets:2749 | `Standard` | 2/7 | 47 | FOG_LINEAR, FOG_EXP, FOG_EXP2, INSTANCING_ON, _NORMALMAP, _ALPHATEST_ON, _ALPHABLEND_ON, _ALPHAPREMULTIPLY_ON, _METALLICGLOSSMAP, _PARALLAXMAP, DIRECTIONAL, LIGHTPROBE_SH, SHADOWS_SHADOWMASK, DYNAMICLIGHTMAP_ON, LIGHTMAP_ON, LIGHTMAP_SHADOW_MIXING, DIRLIGHTMAP_COMBINED, SHADOWS_SCREEN, VERTEXLIGHT_ON, _EMISSION, _DETAIL_MULX2, _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A, _SPECULARHIGHLIGHTS_OFF, _GLOSSYREFLECTIONS_OFF, POINT, SPOT, POINT_COOKIE, DIRECTIONAL_COOKIE, SHADOWS_DEPTH, SHADOWS_SOFT, SHADOWS_CUBE, UNITY_HDR_ON, EDITOR_VISUALIZATION | GLCore | 16941→322612 | 1 |  |
| sharedassets0.assets:2781 | `TextMeshPro/Distance Field` | 1/1 | 20 | BEVEL_ON, UNDERLAY_ON, UNDERLAY_INNER, GLOW_ON, UNITY_UI_CLIP_RECT, UNITY_UI_ALPHACLIP, OUTLINE_ON | GLCore | 9160→264032 | 26 |  |
| sharedassets0.assets:2819 | `TextMeshPro/Mobile/Distance Field` | 1/1 | 16 | OUTLINE_ON, UNDERLAY_ON, UNDERLAY_INNER, UNITY_UI_CLIP_RECT, UNITY_UI_ALPHACLIP | GLCore | 4897→132188 | 12 |  |
| sharedassets0.assets:2754 | `TextMeshPro/Mobile/Distance Field - 2 Pass` | 1/2 | 12 | OUTLINE_ON, UNDERLAY_ON, UNDERLAY_INNER, UNITY_UI_CLIP_RECT, UNITY_UI_ALPHACLIP | GLCore | 4157→86248 | 1 |  |
| sharedassets0.assets:2805 | `TextMeshPro/Sprite` | 1/1 | 4 | UNITY_UI_CLIP_RECT, UNITY_UI_ALPHACLIP | GLCore | 1676→15092 | 13 |  |
| sharedassets0.assets:2753 | `UI/Default` | 1/1 | 4 | UNITY_UI_CLIP_RECT, UNITY_UI_ALPHACLIP | GLCore | 1870→16008 | 1 |  |
| sharedassets0.assets:2755 | `UI/Default Desat` | 1/1 | 2 | UNITY_UI_ALPHACLIP | GLCore | 1310→6608 | 1 | ui-defaultdesat |
| sharedassets0.assets:2816 | `UI/Graphz` | 1/1 | 2 | UNITY_UI_ALPHACLIP | GLCore | 1249→6472 | 1 | graphz |
| sharedassets0.assets:2776 | `Unlit/Beached_Parallax` | 1/1 | 1 | - | GLCore | 1370→4556 | 24 | vistaparallax |
| sharedassets0.assets:2779 | `Unlit/ImGui` | 1/1 | 1 | - | GLCore | 1089→3132 | 1 |  |
| sharedassets0.assets:2811 | `Unlit/VirtualCursorCutoutShader` | 1/1 | 1 | INSTANCING_ON, FOG_LINEAR, FOG_EXP, FOG_EXP2, DIRECTIONAL, LIGHTPROBE_SH, SHADOWS_SHADOWMASK, DYNAMICLIGHTMAP_ON, LIGHTMAP_ON, LIGHTMAP_SHADOW_MIXING, DIRLIGHTMAP_COMBINED, SHADOWS_SCREEN, VERTEXLIGHT_ON, SHADOWS_DEPTH, SHADOWS_CUBE, POINT, SPOT, POINT_COOKIE, DIRECTIONAL_COOKIE, UNITY_HDR_ON, EDITOR_VISUALIZATION | GLCore | 916→2572 | 1 | virtualcursorcutoutshader |
| resources.assets:89 | `Hidden/LineShader` | 1/1 | 1 | - | GLCore | 817→2380 | 0 | lineshader |
| resources.assets:90 | `TextMeshPro/Distance Field (Surface)` | 1/3 | 18 | GLOW_ON, INSTANCING_ON, FOG_LINEAR, FOG_EXP, FOG_EXP2, DIRECTIONAL, LIGHTPROBE_SH, VERTEXLIGHT_ON, POINT, SPOT, POINT_COOKIE, DIRECTIONAL_COOKIE, SHADOWS_DEPTH, SHADOWS_CUBE | GLCore | 14851→297060 | 1 |  |
| resources.assets:86 | `TextMeshPro/Mobile/Distance Field (Surface)` | 1/2 | 5 | GLOW_ON, INSTANCING_ON, FOG_LINEAR, FOG_EXP, FOG_EXP2, DIRECTIONAL, LIGHTPROBE_SH, VERTEXLIGHT_ON, SHADOWS_DEPTH, SHADOWS_CUBE | GLCore | 4780→43708 | 1 |  |
| resources.assets:88 | `TextMeshPro/Mobile/Distance Field - Masking` | 1/1 | 4 | OUTLINE_ON, UNDERLAY_ON, UNDERLAY_INNER, UNITY_UI_CLIP_RECT, UNITY_UI_ALPHACLIP | GLCore | 2812→32428 | 1 |  |
| resources.assets:87 | `TextMeshPro/Mobile/Distance Field Overlay` | 1/1 | 4 | OUTLINE_ON, UNDERLAY_ON, UNDERLAY_INNER, UNITY_UI_CLIP_RECT, UNITY_UI_ALPHACLIP | GLCore | 2649→28432 | 1 |  |
| sharedassets2.assets:84 | `Hidden/BlurEffectConeTap` | 1/1 | 1 | - | GLCore | 1026→3108 | 0 |  |
| sharedassets2.assets:85 | `Hidden/ColorCorrection3DLut` | 1/2 | 2 | - | GLCore | 1040→5456 | 0 |  |
| sharedassets2.assets:83 | `Unlit/Transparent` | 1/1 | 1 | FOG_LINEAR, FOG_EXP, FOG_EXP2 | GLCore | 813→2216 | 1 |  |
| globalgamemanagers.assets:3 | `Sprites/Default` | 1/1 | 4 | ETC1_EXTERNAL_ALPHA, INSTANCING_ON, PIXELSNAP_ON | GLCore | 1510→12584 | 2 |  |
| unity default resources:10101 | `GUI/Text Shader` | 1/1 | 32 | - | D3D11+GLES3+Metal+GLCore+Vulkan+WebGPU? | 12921→74356 | 15 |  |
| unity default resources:10755 | `Hidden/FrameDebuggerRenderTargetDisplay` | 1/2 | 61 | _TEX2DARRAY, _CUBEMAP, _MSAA_2, _MSAA_4, _MSAA_8 | D3D11+GLES3+Metal+GLCore+Vulkan+WebGPU? | 17957→181276 | 1 |  |
| unity default resources:69 | `Hidden/Internal-Colored` | 1/1 | 32 | - | D3D11+GLES3+Metal+GLCore+Vulkan+WebGPU? | 11372→61120 | 0 |  |
| unity default resources:70 | `Hidden/Internal-Loading` | 1/1 | 32 | - | D3D11+GLES3+Metal+GLCore+Vulkan+WebGPU? | 11258→63492 | 0 |  |
| unity default resources:68 | `Hidden/InternalClear` | 1/8 | 256 | - | D3D11+GLES3+Metal+GLCore+Vulkan+WebGPU? | 11046→58932 | 0 |  |
| unity default resources:17 | `Hidden/InternalErrorShader` | 1/3 | 96 | FOG_LINEAR, FOG_EXP, FOG_EXP2 | D3D11+GLES3+Metal+GLCore+Vulkan+WebGPU? | 17836→149772 | 0 |  |
| unity_builtin_extra:66 | `Hidden/BlitCopy` | 1/1 | 1 | - | GLCore | 830→2424 | 0 |  |
| unity_builtin_extra:67 | `Hidden/BlitCopyDepth` | 1/1 | 1 | - | GLCore | 813→2216 | 0 |  |
| unity_builtin_extra:111 | `Hidden/BlitCopyHDRTonemap` | 1/1 | 1 | - | GLCore | 1845→5508 | 0 |  |
| unity_builtin_extra:107 | `Hidden/BlitCopyWithDepth` | 1/1 | 1 | - | GLCore | 873→2552 | 0 |  |
| unity_builtin_extra:109 | `Hidden/BlitToDepth` | 1/1 | 1 | - | GLCore | 912→2584 | 0 |  |
| unity_builtin_extra:110 | `Hidden/BlitToDepth_MSAA` | 1/1 | 1 | - | GLCore | 849→2156 | 0 |  |
| unity_builtin_extra:17000 | `Hidden/Compositing` | 1/1 | 1 | - | GLCore | 950→2780 | 0 |  |
| unity_builtin_extra:68 | `Hidden/ConvertTexture` | 1/1 | 1 | - | GLCore | 1170→3320 | 0 |  |
| unity_builtin_extra:65 | `Hidden/Internal-CombineDepthNormals` | 1/1 | 1 | - | GLCore | 1237→3544 | 0 |  |
| unity_builtin_extra:15309 | `Hidden/Internal-CubemapToEquirect` | 1/1 | 1 | - | GLCore | 934→2488 | 0 |  |
| unity_builtin_extra:113 | `Hidden/Internal-DebugPattern` | 1/3 | 3 | - | GLCore | 1089→6888 | 0 |  |
| unity_builtin_extra:9004 | `Hidden/Internal-GUIRoundedRect` | 2/2 | 2 | - | GLCore | 2434→8164 | 0 |  |
| unity_builtin_extra:9007 | `Hidden/Internal-GUIRoundedRectWithColorPerBorder` | 2/2 | 2 | - | GLCore | 3106→10316 | 0 |  |
| unity_builtin_extra:9002 | `Hidden/Internal-GUITexture` | 2/2 | 2 | - | GLCore | 862→2388 | 0 |  |
| unity_builtin_extra:9003 | `Hidden/Internal-GUITextureBlit` | 2/2 | 2 | - | GLCore | 1208→3872 | 0 |  |
| unity_builtin_extra:9000 | `Hidden/Internal-GUITextureClip` | 2/2 | 2 | - | GLCore | 1208→3888 | 0 |  |
| unity_builtin_extra:9001 | `Hidden/Internal-GUITextureClipText` | 2/2 | 2 | - | GLCore | 1070→3296 | 0 |  |
| unity_builtin_extra:15308 | `Hidden/Internal-ODSWorldTexture` | 9/10 | 10 | - | GLCore | 4021→33760 | 0 |  |
| unity_builtin_extra:19 | `Hidden/Internal-StencilWrite` | 1/1 | 1 | - | GLCore | 712→1736 | 0 |  |
| unity_builtin_extra:9101 | `Hidden/Internal-UIRAtlasBlitCopy` | 1/1 | 1 | - | GLCore | 1377→6420 | 0 |  |
| unity_builtin_extra:9100 | `Hidden/Internal-UIRDefault` | 1/1 | 40 | _UIE_FORCE_GAMMA, _UIE_TEXTURE_SLOT_COUNT_1, _UIE_TEXTURE_SLOT_COUNT_2, _UIE_TEXTURE_SLOT_COUNT_4, _UIE_RENDER_TYPE_SOLID, _UIE_RENDER_TYPE_TEXTURE, _UIE_RENDER_TYPE_TEXT, _UIE_RENDER_TYPE_GRADIENT | GLCore | 55354→669188 | 0 |  |
| unity_builtin_extra:20501 | `Hidden/Physics2D/SDF_CapsuleGeometry` | 1/1 | 1 | INSTANCING_ON | GLCore | 2528→7872 | 0 |  |
| unity_builtin_extra:20500 | `Hidden/Physics2D/SDF_CircleGeometry` | 1/1 | 1 | INSTANCING_ON | GLCore | 2433→7520 | 0 |  |
| unity_builtin_extra:20506 | `Hidden/Physics2D/SDF_Line` | 1/1 | 1 | INSTANCING_ON | GLCore | 1933→5772 | 0 |  |
| unity_builtin_extra:20507 | `Hidden/Physics2D/SDF_Point` | 1/1 | 1 | INSTANCING_ON | GLCore | 1798→5404 | 0 |  |
| unity_builtin_extra:20502 | `Hidden/Physics2D/SDF_PolygonGeometry` | 1/1 | 1 | INSTANCING_ON | GLCore | 3427→12124 | 0 |  |
| unity_builtin_extra:19011 | `Hidden/TextCore/Distance Field SSD` | 1/1 | 1 | - | GLCore | 1627→5440 | 0 |  |
| unity_builtin_extra:15312 | `Hidden/VR/BlitFromTex2DToTexArraySlice` | 2/2 | 2 | - | GLCore | 1132→5432 | 0 |  |
| unity_builtin_extra:15304 | `Hidden/VR/BlitTexArraySlice` | 1/1 | 1 | - | GLCore | 822→2296 | 0 |  |
| unity_builtin_extra:16000 | `Hidden/VideoComposite` | 1/1 | 1 | - | GLCore | 952→2668 | 0 |  |
| unity_builtin_extra:16001 | `Hidden/VideoDecode` | 1/8 | 15 | ADJUST_TO_LINEARSPACE | GLCore | 2168→43036 | 0 |  |
| unity_builtin_extra:10757 | `Sprites/Mask` | 1/1 | 4 | ETC1_EXTERNAL_ALPHA, PIXELSNAP_ON | GLCore | 1408→11928 | 0 |  |

## 6. Textures

There are **2,685 Texture2D** objects (resources 31, sa0 2,545, sa1 21, sa2 48, default resources 37, plus 390 in hires_base_bundle) and **3 Texture2DArray**.

| File | Count | Inline | In .resS | Inline bytes | .resS bytes | Formats |
|---|---|---|---|---|---|---|
| resources.assets | 31 | 6 | 16 | 5.5 MB | 9.25 MB | RGBA32 11, Alpha8 10, DXT1 5, DXT5 3, RGB24 1, BC7 1 |
| sharedassets0 | 2545 | 22 | 2482 | 64.5 MB | 2,062.9 MB | **DXT5 2284**, DXT1 110, RGBA32 109, Alpha8 32, RGB24 10 |
| sharedassets1 | 21 | 0 | 21 | 0 | 21.9 MB | DXT5 10, Alpha8 10, DXT1 1 |
| sharedassets2 | 48 (+3 arrays) | 5 | 46 | 89.5 MB (the 3 arrays) | 69.4 MB | DXT5 24, Alpha8 13, DXT1 7, RGBA32 2, RGB24 2 |
| unity default resources | 37 | 36 | 0 | 0.18 MB | – | ARGB4444 32, RGBA32 3, Alpha8 1, DXT1 1 |

The "Count" column also includes 0×0 textures, which have neither inline nor streamed data. These are the 50 legacy `Font Texture` objects (Font dynamic atlases, RGBA32/format 4) and 6 TMP dynamic atlases (Anton, Bangers, Oswald, Roboto-Bold, Electronic Highway Sign, LiberationSans).

- **Formats overall** (TextureFormat enum): DXT5(12) 2321, RGBA32(4) 125, DXT1(10) 124, Alpha8(1) 66, ARGB4444(2) 32, RGB24(3) 13, BC7(25) 1.
- **Texture2DArrays** (`m_Format` is a GraphicsFormat), all inline in sharedassets2:
  - `bgarray`: 1024², 64 slices, GraphicsFormat 97 = RGBA_DXT1_UNorm, 11 mips, 44.7 MB.
  - `bgarray_liquids` and `bgarray_liquids_specular`: 1024², 16 slices, 101 = RGBA_DXT5_UNorm, 22.4 MB each.
  - The format codes were checked against the byte sizes.
- **Sizes** (sa0): 512×256 580, 256² 360, 512² 353, 1024² 304, 1024×512 205, 256×128 166, 2048² 125, 2048×1024 118, 800² 34. The largest are 4096² (`mask_atlas` DXT5 and 3 CJK TMP atlases in Alpha8) and 4096×2048 (`anim_machinemg_0`, `anim_waterfall_0`).
- **Mipmaps:** 2266/2545 have mips in sa0, and all do in sa1 and sa2. Few textures are readable (23 in sa0). Filter mode is mostly bilinear (2). ColorSpace is 0 for almost all; 71 in sa0 are sRGB=1.
- **Pixel storage:** the textures in the .resS files are mostly raw DXT blocks, mip chain concatenated (offsets 16-aligned). Small textures, cursors, TMP static atlases and texture arrays are inline in `image data`. No Crunch formats are used.
- **KAnim atlases:** about 2,036 textures are named `<anim>_N` (for example `achievement_0`). See §10.
- **Sprites:** sa0 has 1954, sa2 has 1 (`Knob`). PPU is 100 for 1833 of them. Mesh types are mostly tight (vertex counts 4–9).
  - **1457 sprites are packed in SpriteAtlas `mainHUD`** (63 `sactx-N-2048x2048-DXT5|BC3-mainHUD-*` textures). 76 more are in `nomipHUD` (2 textures).
  - Atlas-packed sprites have `m_RD.texture = null`. The real texture and UV rect come from `SpriteAtlas.m_RenderDataMap[m_RenderDataKey]`, so the loader **must** implement this lookup.
  - The other 497 sprites reference their own textures. Examples: the TMP sprite sheets PS4Sheet (53), SteamDeckSheet (53), SteamControllerSheet (37), smoke_trail (31).
- **hires_base_bundle:** 390 more Texture2D (all DXT5, `*_hires_0` clothing and cosmetic sheets, 1024×512 ×248 and similar, 11 mips), stored in the bundle's CAB .resS (305 MB uncompressed).

## 7. Fonts

**Legacy `Font` (class 128):** 14 objects in total.
- resources.assets has 5 TTF-embedded fonts, used as TMP source fonts: Roboto-Bold (170,760 B TTF), Bangers, Electronic Highway Sign, Anton, Oswald-Bold.
- sharedassets0 has 8: LeagueSpartan-Bold, Roboto-Regular, Economica-{Regular,Bold,Italic,BoldItalic}-OTF, DroidSans (22 pt), LiberationSans (350 KB).
- sharedassets2 has NotoSans-Regular (306 KB).
- unity default resources has `LegacyRuntime` (Arial, 0 bytes of font data, which means an OS font).
- All are dynamic: `m_FontRenderingMode=0`, `m_ConvertCase=-2`, 0×0 `Font Texture`. They use the material `GUI/Text Shader` (15 materials).
- StreamingAssets also contains `fonts/NotoSansUI-Regular.ttf` (305,888 B).

**TMP_FontAsset (MonoBehaviour, Unity.TextMeshPro):**
- resources.assets has 14: Anton/Bangers/Electronic Highway Sign/Oswald Bold/Roboto-Bold SDF (TMP samples), Unity SDF, Roboto-{Bold,BoldItalic,Italic,Regular} SDF, NotoNaskhArabic-Regular, NotoSansHebrew-Regular, NotoSansThai-Regular, RobotoCondensed-Italic.
- sharedassets0 has 14: LiberationSans SDF (+ Fallback), Economica-Bold-OTF SDF, GRAYSTROKE {ITEM DROP, NAME, OUTLINE, REGULAR} SDF, NotoSans-Regular (+ -Underlay, -SkinQuantityLabel), NotoSansCJK{jp,kr,sc}-Regular, RobotoCondensed-Regular.
- sharedassets2 has 1: Economica-Bold-OTF SDF 2.
- Atlas textures are **Alpha8**, at 512² to 4096² (CJK 4096², NotoSans 2048²). Each asset has its own Material using `TextMeshPro/Distance Field` or `…/Mobile/Distance Field`.
- **(export)** Asset version is 1.1.0 (TMP 3.x style: m_FaceInfo, m_GlyphTable, m_CharacterTable, m_AtlasTextures list). Render modes:
  - **4121 = SMOOTH_HINTED** (anti-aliased bitmap, *not* SDF) for most Klei fonts: Noto*, GRAYSTROKE, Economica, Roboto-Bold/Italic.
  - 4165 = SDFAA for Roboto-Regular/BoldItalic, RobotoCondensed and the TMP samples.
  - 4169 = SDFAA_HINTED for LiberationSans; 32806 = SDF32 for GRAYSTROKE NAME; 16422 = SDF16 for Unity SDF.
  - Even the bitmap-mode fonts render through the Distance Field shaders.
- **(export)** Glyph counts: NotoSansCJKkr 11,592, jp 10,837, sc 10,175; NotoSans 1,977; NotoNaskhArabic 1,325; RobotoCondensed 839. `m_AtlasPopulationMode` is 0 (static) for game fonts and 1 (dynamic) for the TMP samples.
- **TMP Settings (export):** default font asset is LiberationSans SDF ("Fonts & Materials/"), default size 36, kerning on, emoji support on, 11 global fallback font assets (the Noto family), default sprite asset at "Sprite Assets/", a default style sheet, and leading/following line-breaking TextAssets.
- TMP_SpriteAssets (resources): Default Sprite Asset, DropCap Numbers, ErrorSheet, PS4Sheet, SteamControllerSheet, SteamDeckSheet, XB360Sheet, XboxOneSheet, inlineIcons, oni_sprite_assets, EmojiOne (also in sa0).
- The game's own text component is `LocText` (a TextMeshProUGUI subclass, 1,488 instances), plus 45 plain TextMeshProUGUI, 27 legacy `UnityEngine.UI.Text`, and 55 KInputTextField.

## 8. Audio / Video

- **AudioClip: 0 objects in any file.** `AudioManager.m_DisableAudio=1`. All audio is **FMOD Studio** (`Plugins/libfmodstudio.so`, `libresonanceaudio.so`, managed FMODUnity).
- **FMOD banks** in StreamingAssets (base game): Ambiences 36 MB, Buildings 65 MB, Creatures 23 MB, DuplicantActions 14 MB, DuplicantVoices 19 MB, Environment 20 MB, FX 2 MB, MainMenu 3.3 MB, `Master Bank.bank` 0.2 MB, `Master Bank.strings.bank` 0.3 MB, Music 188 MB, Plants 3.2 MB, Snapshots 80 KB, UI 49 MB, Videos 11 MB.
  - The banks are RIFF `FEV ` containers with embedded FSB5. Music.bank's FSB5 has 352 samples, mode 15 = **Vorbis**.
  - The FMODUnity `Settings` and `Platform*` assets are in Resources (`fmodstudiosettings`).
  - The FMOD native library can be used as-is.
- **VideoClip: 18** (resources.assets), all `Assets/resources/video_webm/**.webm`, 1920×1080, 24/29.97/30 fps, **VP8 WebM**, **no audio tracks** in the clip metadata. Video audio is the FMOD `Videos.bank`.
  - Data is in `resources.resource` (offset and size from `m_ExternalResources`; the file starts with an EBML header).
  - The largest is Spaced_Out_Intro (42 MB, 3300 frames).
  - Played by `VideoPlayer` components (1 in sa0, 2 in sa2), with `Hidden/VideoDecode` and `Hidden/VideoComposite` in builtin_extra.

## 9. StreamingAssets inventory (base game, Linux)

| Item | Files | Size | Notes |
|---|---|---|---|
| `*.bank` | 15 | ~433 MB | FMOD Studio banks (§8) |
| `hires_base_bundle` | 1 | 23.5 MB | **UnityFS** AssetBundle, format 8, `5.x.x`/`6000.3.5f2`, flags 0x243 (LZMA blocks-info, blocks-info at end, 16-byte block padding). One LZMA block expands to 305 MB, holding 2 nodes: `CAB-729cbed85bef6c6a60deedcb2af2bcac` (119,896 B SerializedFile, **EnableTypeTree=1**, 395 objects: 390 Texture2D DXT5, 1 AssetBundle, 1 GameObject, 1 Transform, 1 MonoBehaviour `TextureBundle`[F] with `HiResTextures: vector<PPtr<Texture2D>>`, 1 MonoScript) and the matching `.resS` (305 MB). The AssetBundle container has one entry, `assets/prefabs/hires_base_bundle.prefab`, with a preload table of 394. These are high-resolution duplicant clothing textures. |
| `codex/` | 347 | 342 KB | YAML codex entries in 19 category folders (Creatures 60, Plants 60, MyLog 33, Emails 29, Journals 27, IndustrialIngredients 22, Systems 21, Investigations 21, …) |
| `elements/` | 4 | 153 KB | `gas.yaml`, `liquid.yaml`, `solid.yaml`, `special.yaml` (element definitions) |
| `worldgen/` | 243 | 282 KB | YAML: biomes 13, clusters 13, features 72, noise 24, subworlds 74, traits 18, worlds 15, storytraits 6, plus borders, defaults, layers, mobs, rivers, rooms, temperatures, `seed_names.txt` |
| `templates/` | 133 | 2.9 MB | YAML: poi 62, geysers 23, storytraits 17, rough unused poi 14, events 12, bases 2, dev_tests 1, plus `AllLiquids.yaml`, `chunk.yaml` |
| `strings/` | 17 | 26.4 MB | `strings_template.pot`, `strings_preinstalled_{ko,ru,zh}_klei.po`, icons (png) and Klei's python and bat tooling |
| `fonts/NotoSansUI-Regular.ttf` | 1 | 306 KB | |
| `Tuning.json` | 1 | 4 KB | per-class `"Class+Tuning"` overrides |
| `poi_gravitas_rotated.yaml` | 1 | 144 KB | |

By extension: yaml 727, bank 15, bat 5, py 4, png 4, po 3, pot 1, txt 1, ttf 1, json 1.

**No DLC in this install.** The AssetRipper (Windows) export's StreamingAssets additionally has `dlc/{expansion1,dlc2..5,cosmetic1}`, `expansion1_bundle`, `dlc{2..5}_bundle`, `cosmetic1_bundle`, `hires_*_bundle` and `expansion1_*`/`DLC{2..5}_*` banks. The 13 CAB bundles in its path map (up to 1073 objects each) are those DLC bundles. **The loader must support UnityFS bundles (LZMA and LZ4) from day one**, because base-game hires textures already use one and all DLC content ships as bundles.

## 10. KAnim data

- **KAnimFile** MonoBehaviours: **2152** in sharedassets0, none elsewhere. They are referenced from prefabs and from `KAnimGroupFile` (`Resources/animgrouptags`).
- Layout, decoded from the payload **(heuristic but consistent)**: `PPtr<TextAsset> animFile, PPtr<TextAsset> buildFile, List<PPtr<Texture2D>> textureList, string path` (for example `Assets/anim/base/FX/achievement`).
  - 1526 have anim, build and one texture.
  - 423 have only build and a texture (static or build-only).
  - 203 have only the anim (anim-only files that reuse another build).
- **TextAssets** in sharedassets0:
  - **1729 `*_anim`** (magic `ANIM`, 309.9 MB total).
  - **1949 `*_build`** (magic `BILD`, 2.8 MB).
  - 6 others: TeamCredits, CreditsLocalizations, ElementSounds, Personalities, CreditsSkymap, CreditsMiddleware.
- The kanim data is stored **inline in sharedassets0.assets**, not in .resS. TextAsset.m_Script is a byte array.
- Atlas textures are `<name>_0` Texture2D (DXT5, mipmapped) in sharedassets0.assets.resS. There is one texture per KAnimFile here; the list form allows more.
- resources.assets TextAssets (17): SFXTags_{Creatures,Buildings,Duplicants,Plants,MainMenu,Misc}, TechTree_Generated, TechTree_Expansion1_Generated, MinionModifiers, names_male/female, EmojiOne(+Attribution), LineBreaking Leading/Following, PerformanceTestRun{Info,Settings}.
- Rendering uses `Klei/BatchedAnimation*` shaders (kbatched_anim, _human, _pst, _shine, _ui) through `KBatchedAnimController` (163 instances) and `KBatchedAnimCanvasRenderer` (66 instances) on prefabs.

## 11. Minimum loader feature set (assessment)

**Container and format level:**
1. SerializedFile v22: BE header, LE metadata, stripped type table, object table with i64 byteStart, script types, externals, and PPtr resolution across files. Built-in externals must map to `Data/Resources/…`.
2. A **type-tree driven reader** using the 6000.3.5f2 release trees (InfoJson). It must handle 0x4000 alignment, `TypelessData`, arrays, maps, pair, strings, and embedded type trees for bundles (which reference the common-string table at offset bit 31; take it from InfoJson `Strings`). An alternative is hand-written readers generated from those trees.
3. **MonoBehaviour payload:** type trees are stripped, so the managed field layout must be derived from the game's assemblies, following Unity's serialization rules. These are: public or `[SerializeField]` fields; no properties; `[Serializable]` classes inline; `UnityEngine.Object` references as PPtr; List<T> and T[] as arrays; 4-byte alignment after bool/byte/char/short arrays and strings; no `[SerializeReference]`, since RefTypes=0. This is the single largest piece of work: 8595 + 10103 MBs across ~700 script classes.
4. `.resS` / `.resource` streaming reads using StreamingInfo or StreamedResource.
5. UnityFS AssetBundle reader (LZMA and LZ4/LZ4HC blocks, block-info-at-end flag, 16-byte padding flag 0x200), plus the AssetBundle object (container and preload table).
6. ResourceManager container → `Resources.Load` path table (lower-case paths). Also `ShaderNameRegistry` for `Shader.Find`.
7. Shader blob decode: LZ4 block → GLCore sub-program table → GLSL text plus parameter bindings.

**Native classes that must be deserialized.** These are required because they occur in the data the game instantiates:
- GameObject, Transform, RectTransform, MonoBehaviour (header), MonoScript
- Material, Shader, Texture2D, Texture2DArray, Sprite, SpriteAtlas, Mesh (2 instances, inline), TextAsset, Font
- Canvas, CanvasRenderer, CanvasGroup, Camera, SpriteRenderer, MeshRenderer, MeshFilter, RenderTexture, VideoClip, VideoPlayer
- Animator and AnimatorController/AnimationClip/Animation (few: 27 Animators, 14 clips, 10 controllers; probably UI tweens)
- ParticleSystem and ParticleSystemRenderer (12 each)
- BoxCollider2D (6), SphereCollider (2), Avatar (1)
- AssetBundle, PreloadData
- Global managers: BuildSettings, ResourceManager, GraphicsSettings, QualitySettings, TagManager, PlayerSettings (partial), TimeManager, InputManager, PhysicsManager/Physics2DSettings, ShaderNameRegistry, MonoManager
- Scene-level: RenderSettings, LightmapSettings, NavMeshSettings (can be skipped or ignored)

Field lists for each class follow. They are taken from `InfoJson_6000.3.5f2.json` ReleaseRootNode, which is the same data as `release_*.dump`. Notation: `field:type`. A trailing `¦` means align the stream to 4 after the field. `ColorRGBA(f4)` is 4 floats, `ColorRGBA(u32)` is a packed uint. Vector*/Quaternionf/Rectf/Matrix4x4f/AABB/GUID/Hash128 are inline POD floats or u32. `PPtr<T>` is `{i32 m_FileID, i64 m_PathID}` (12 bytes, no padding). Nested struct definitions are listed below each class.

**GameObject (1)**: m_Component:vector<ComponentPair>¦, m_Layer:unsigned int, m_Name:string¦, m_Tag:UInt16, m_IsActive:bool
  - `ComponentPair` = {component:PPtr<Component>}

**Transform (4)**: m_GameObject:PPtr<GameObject>, m_LocalRotation:Quaternionf, m_LocalPosition:Vector3f, m_LocalScale:Vector3f¦, m_Children:vector<PPtr<Transform>>¦, m_Father:PPtr<Transform>

**RectTransform (224)**: m_GameObject:PPtr<GameObject>, m_LocalRotation:Quaternionf, m_LocalPosition:Vector3f, m_LocalScale:Vector3f¦, m_Children:vector<PPtr<Transform>>¦, m_Father:PPtr<Transform>, m_AnchorMin:Vector2f, m_AnchorMax:Vector2f, m_AnchoredPosition:Vector2f, m_SizeDelta:Vector2f, m_Pivot:Vector2f

**MonoBehaviour (114)**: m_GameObject:PPtr<GameObject>, m_Enabled:UInt8¦, m_Script:PPtr<MonoScript>, m_Name:string¦

**MonoScript (115)**: m_Name:string¦, m_ExecutionOrder:int, m_PropertiesHash:Hash128, m_ClassName:string¦, m_Namespace:string¦, m_AssemblyName:string¦

**Material (21)**: m_Name:string¦, m_Shader:PPtr<Shader>, m_ValidKeywords:vector<string>¦, m_InvalidKeywords:vector<string>¦, m_LightmapFlags:unsigned int, m_EnableInstancingVariants:bool, m_DoubleSidedGI:bool¦, m_CustomRenderQueue:int, stringTagMap:map<string,string>, disabledShaderPasses:vector<string>¦, m_SavedProperties:UnityPropertySheet, m_BuildTextureStacks:vector<BuildTextureStackReference>¦
  - `UnityPropertySheet` = {m_TexEnvs:map<string,UnityTexEnv>, m_Ints:map<string,int>, m_Floats:map<string,float>, m_Colors:map<string,ColorRGBA(f4)>}
  - `UnityTexEnv` = {m_Texture:PPtr<Texture>, m_Scale:Vector2f, m_Offset:Vector2f}
  - `BuildTextureStackReference` = {groupName:string¦, itemName:string¦}

**Shader (48)**: m_Name:string¦, m_ParsedForm:SerializedShader, platforms:vector<unsigned int>¦, offsets:vector<vector<unsigned int>>¦, compressedLengths:vector<vector<unsigned int>>¦, decompressedLengths:vector<vector<unsigned int>>¦, compressedBlob:vector<UInt8>¦, stageCounts:vector<unsigned int>¦, m_Dependencies:vector<PPtr<Shader>>¦, m_NonModifiableTextures:map<string,PPtr<Texture>>, m_ShaderIsBaked:bool¦, m_AssetGUID:GUID, m_AssetLocalIdentifierInFile:SInt64
  - `SerializedShader` = {m_PropInfo:SerializedProperties, m_SubShaders:vector<SerializedSubShader>¦, m_KeywordNames:vector<string>¦, m_KeywordFlags:vector<UInt8>¦, m_Name:string¦, m_CustomEditorName:string¦, m_FallbackName:string¦, m_Dependencies:vector<SerializedShaderDependency>¦, m_CustomEditorForRenderPipelines:vector<SerializedCustomEditorForRenderPipeline>¦, m_DisableNoSubshadersMessage:bool¦}
  - `SerializedProperties` = {m_Props:vector<SerializedProperty>¦}
  - `SerializedProperty` = {m_Name:string¦, m_Description:string¦, m_Attributes:vector<string>¦, m_Type:int, m_Flags:unsigned int, m_DefValue[0]:float, m_DefValue[1]:float, m_DefValue[2]:float, m_DefValue[3]:float, m_DefTexture:SerializedTextureProperty}
  - `SerializedTextureProperty` = {m_DefaultName:string¦, m_TexDim:int}
  - `SerializedSubShader` = {m_Passes:vector<SerializedPass>¦, m_Tags:SerializedTagMap, m_LOD:int}
  - `SerializedPass` = {m_NameIndices:map<string,int>, m_Type:int, m_State:SerializedShaderState, m_ProgramMask:unsigned int, progVertex:SerializedProgram, progFragment:SerializedProgram, progGeometry:SerializedProgram, progHull:SerializedProgram, progDomain:SerializedProgram, progRayTracing:SerializedProgram, m_HasInstancingVariant:bool, m_HasProceduralInstancingVariant:bool¦, m_UseName:string¦, m_Name:string¦, m_TextureName:string¦, m_Tags:SerializedTagMap}
  - `SerializedShaderState` = {m_Name:string¦, rtBlend0:SerializedShaderRTBlendState, rtBlend1:SerializedShaderRTBlendState, rtBlend2:SerializedShaderRTBlendState, rtBlend3:SerializedShaderRTBlendState, rtBlend4:SerializedShaderRTBlendState, rtBlend5:SerializedShaderRTBlendState, rtBlend6:SerializedShaderRTBlendState, rtBlend7:SerializedShaderRTBlendState, rtSeparateBlend:bool¦, zClip:SerializedShaderFloatValue, zTest:SerializedShaderFloatValue, zWrite:SerializedShaderFloatValue, culling:SerializedShaderFloatValue, conservative:SerializedShaderFloatValue, offsetFactor:SerializedShaderFloatValue, offsetUnits:SerializedShaderFloatValue, alphaToMask:SerializedShaderFloatValue, stencilOp:SerializedStencilOp, stencilOpFront:SerializedStencilOp, stencilOpBack:SerializedStencilOp, stencilReadMask:SerializedShaderFloatValue, stencilWriteMask:SerializedShaderFloatValue, stencilRef:SerializedShaderFloatValue, fogStart:SerializedShaderFloatValue, fogEnd:SerializedShaderFloatValue, fogDensity:SerializedShaderFloatValue, fogColor:SerializedShaderVectorValue, fogMode:int, gpuProgramID:int, m_Tags:SerializedTagMap, m_LOD:int, lighting:bool¦}
  - `SerializedShaderRTBlendState` = {srcBlend:SerializedShaderFloatValue, destBlend:SerializedShaderFloatValue, srcBlendAlpha:SerializedShaderFloatValue, destBlendAlpha:SerializedShaderFloatValue, blendOp:SerializedShaderFloatValue, blendOpAlpha:SerializedShaderFloatValue, colMask:SerializedShaderFloatValue}
  - `SerializedShaderFloatValue` = {val:float, name:string¦}
  - `SerializedStencilOp` = {pass:SerializedShaderFloatValue, fail:SerializedShaderFloatValue, zFail:SerializedShaderFloatValue, comp:SerializedShaderFloatValue}
  - `SerializedShaderVectorValue` = {x:SerializedShaderFloatValue, y:SerializedShaderFloatValue, z:SerializedShaderFloatValue, w:SerializedShaderFloatValue, name:string¦}
  - `SerializedTagMap` = {tags:map<string,string>}
  - `SerializedProgram` = {m_SubPrograms:vector<SerializedSubProgram>¦, m_PlayerSubPrograms:vector<vector<SerializedPlayerSubProgram>>¦, m_ParameterBlobIndices:vector<vector<unsigned int>>¦, m_CommonParameters:SerializedProgramParameters, m_SerializedKeywordStateMask:vector<UInt16>¦}
  - `SerializedSubProgram` = {m_BlobIndex:unsigned int, m_Channels:ParserBindChannels, m_KeywordIndices:vector<UInt16>¦, m_ShaderHardwareTier:SInt8, m_GpuProgramType:SInt8¦, m_Parameters:SerializedProgramParameters, m_ShaderRequirements:SInt64}
  - `ParserBindChannels` = {m_Channels:vector<ShaderBindChannel>¦, m_SourceMap:int}
  - `ShaderBindChannel` = {source:SInt8, target:SInt8}
  - `SerializedProgramParameters` = {m_VectorParams:vector<VectorParameter>¦, m_MatrixParams:vector<MatrixParameter>¦, m_TextureParams:vector<TextureParameter>¦, m_BufferParams:vector<BufferBinding>¦, m_ConstantBuffers:vector<ConstantBuffer>¦, m_ConstantBufferBindings:vector<BufferBinding>¦, m_UAVParams:vector<UAVParameter>¦, m_Samplers:vector<SamplerParameter>¦}
  - `VectorParameter` = {m_NameIndex:int, m_Index:int, m_ArraySize:int, m_Type:SInt8, m_Dim:SInt8¦}
  - `MatrixParameter` = {m_NameIndex:int, m_Index:int, m_ArraySize:int, m_Type:SInt8, m_RowCount:SInt8¦}
  - `TextureParameter` = {m_NameIndex:int, m_Index:int, m_SamplerIndex:int, m_MultiSampled:bool, m_Dim:SInt8¦}
  - `BufferBinding` = {m_NameIndex:int, m_Index:int, m_ArraySize:int}
  - `ConstantBuffer` = {m_NameIndex:int, m_MatrixParams:vector<MatrixParameter>¦, m_VectorParams:vector<VectorParameter>¦, m_StructParams:vector<StructParameter>¦, m_Size:int, m_IsPartialCB:bool¦}
  - `StructParameter` = {m_NameIndex:int, m_Index:int, m_ArraySize:int, m_StructSize:int, m_VectorMembers:vector<VectorParameter>¦, m_MatrixMembers:vector<MatrixParameter>¦}
  - `UAVParameter` = {m_NameIndex:int, m_Index:int, m_OriginalIndex:int}
  - `SamplerParameter` = {sampler:unsigned int, bindPoint:int}
  - `SerializedPlayerSubProgram` = {m_BlobIndex:unsigned int, m_KeywordIndices:vector<UInt16>¦, m_ShaderRequirements:SInt64, m_GpuProgramType:SInt8¦}
  - `SerializedShaderDependency` = {from:string¦, to:string¦}
  - `SerializedCustomEditorForRenderPipeline` = {customEditorName:string¦, renderPipelineType:string¦}

**Texture2D (28)**: m_Name:string¦, m_IsAlphaChannelOptional:bool¦, m_Width:int, m_Height:int, m_CompleteImageSize:unsigned int, m_MipsStripped:int, m_TextureFormat:int, m_MipCount:int, m_IsReadable:bool, m_IsPreProcessed:bool, m_IgnoreMipmapLimit:bool¦, m_MipmapLimitGroupName:string¦, m_StreamingMipmaps:bool¦, m_StreamingMipmapsPriority:int¦, m_ImageCount:int, m_TextureDimension:int, m_TextureSettings:GLTextureSettings, m_LightmapFormat:int, m_ColorSpace:int, m_PlatformBlob:vector<UInt8>¦, image data:bytes¦, m_StreamData:StreamingInfo
  - `GLTextureSettings` = {m_FilterMode:int, m_Aniso:int, m_MipBias:float, m_WrapU:int, m_WrapV:int, m_WrapW:int}
  - `StreamingInfo` = {offset:UInt64, size:unsigned int, path:string¦}

**Texture2DArray (187)**: m_Name:string¦, m_IsAlphaChannelOptional:bool¦, m_ColorSpace:int, m_Format:int, m_Width:int, m_Height:int, m_Depth:int, m_MipCount:int, m_MipsStripped:int, m_DataSize:unsigned int, m_TextureSettings:GLTextureSettings, m_UsageMode:int, m_IsReadable:bool, m_IgnoreMipmapLimit:bool¦, m_MipmapLimitGroupName:string¦, image data:bytes¦, m_StreamData:StreamingInfo
  - `GLTextureSettings` = {m_FilterMode:int, m_Aniso:int, m_MipBias:float, m_WrapU:int, m_WrapV:int, m_WrapW:int}
  - `StreamingInfo` = {offset:UInt64, size:unsigned int, path:string¦}

**Sprite (213)**: m_Name:string¦, m_Rect:Rectf, m_Offset:Vector2f, m_Border:Vector4f, m_PixelsToUnits:float, m_Pivot:Vector2f, m_Extrude:unsigned int, m_IsPolygon:bool¦, m_RenderDataKey:pair<GUID,SInt64>, m_AtlasTags:vector<string>¦, m_SpriteAtlas:PPtr<SpriteAtlas>¦, m_RD:SpriteRenderData¦, m_PhysicsShape:vector<vector<Vector2f>>¦, m_Bones:vector<SpriteBone>¦, m_ScriptableObjects:vector<PPtr<MonoBehaviour>>¦
  - `SpriteRenderData` = {texture:PPtr<Texture2D>, alphaTexture:PPtr<Texture2D>, secondaryTextures:vector<SecondarySpriteTexture>¦, m_SubMeshes:vector<SubMesh>¦, m_IndexBuffer:vector<UInt8>¦, m_VertexData:VertexData, m_Bindpose:vector<Matrix4x4f>¦, textureRect:Rectf, textureRectOffset:Vector2f, atlasRectOffset:Vector2f, settingsRaw:unsigned int, uvTransform:Vector4f, downscaleMultiplier:float}
  - `SecondarySpriteTexture` = {texture:PPtr<Texture2D>, name:string¦}
  - `SubMesh` = {firstByte:unsigned int, indexCount:unsigned int, topology:int, baseVertex:unsigned int, firstVertex:unsigned int, vertexCount:unsigned int, localAABB:AABB}
  - `VertexData` = {m_VertexCount:unsigned int, m_Channels:vector<ChannelInfo>¦, m_DataSize:bytes¦}
  - `ChannelInfo` = {stream:UInt8, offset:UInt8, format:UInt8, dimension:UInt8}
  - `SpriteBone` = {name:string¦, guid:string¦, position:Vector3f, rotation:Quaternionf, length:float, parentId:int, color:ColorRGBA(u32)}

**SpriteAtlas (687078895)**: m_Name:string¦, m_PackedSprites:vector<PPtr<Sprite>>¦, m_PackedSpriteNamesToIndex:vector<string>¦, m_RenderDataMap:map<pair<GUID,SInt64>,SpriteAtlasData>, m_Tag:string¦, m_IsVariant:bool¦
  - `SpriteAtlasData` = {texture:PPtr<Texture2D>, alphaTexture:PPtr<Texture2D>, textureRect:Rectf, textureRectOffset:Vector2f, atlasRectOffset:Vector2f, uvTransform:Vector4f, downscaleMultiplier:float, settingsRaw:unsigned int, secondaryTextures:vector<SecondarySpriteTexture>¦}
  - `SecondarySpriteTexture` = {texture:PPtr<Texture2D>, name:string¦}

**Mesh (43)**: m_Name:string¦, m_SubMeshes:vector<SubMesh>¦, m_Shapes:BlendShapeData, m_BindPose:vector<Matrix4x4f>¦, m_BoneNameHashes:vector<unsigned int>¦, m_RootBoneNameHash:unsigned int, m_BonesAABB:vector<MinMaxAABB>¦, m_VariableBoneCountWeights:VariableBoneCountWeights, m_MeshCompression:UInt8, m_IsReadable:bool, m_KeepVertices:bool, m_KeepIndices:bool¦, m_IndexFormat:int, m_IndexBuffer:vector<UInt8>¦, m_VertexData:VertexData¦, m_CompressedMesh:CompressedMesh, m_LocalAABB:AABB, m_MeshUsageFlags:int, m_CookingOptions:int, m_BakedConvexCollisionMesh:vector<UInt8>¦, m_BakedTriangleCollisionMesh:vector<UInt8>¦, m_MeshMetrics[0]:float, m_MeshMetrics[1]:float¦, m_StreamData:StreamingInfo, m_MeshLodInfo:MeshLodInfo
  - `SubMesh` = {firstByte:unsigned int, indexCount:unsigned int, topology:int, baseVertex:unsigned int, firstVertex:unsigned int, vertexCount:unsigned int, localAABB:AABB}
  - `BlendShapeData` = {vertices:vector<BlendShapeVertex>¦, shapes:vector<MeshBlendShape>¦, channels:vector<MeshBlendShapeChannel>¦, fullWeights:vector<float>¦}
  - `BlendShapeVertex` = {vertex:Vector3f, normal:Vector3f, tangent:Vector3f, index:unsigned int}
  - `MeshBlendShape` = {firstVertex:unsigned int, vertexCount:unsigned int, hasNormals:bool, hasTangents:bool¦}
  - `MeshBlendShapeChannel` = {name:string¦, nameHash:unsigned int, frameIndex:int, frameCount:int}
  - `MinMaxAABB` = {m_Min:Vector3f, m_Max:Vector3f}
  - `VariableBoneCountWeights` = {m_Data:vector<unsigned int>¦}
  - `VertexData` = {m_VertexCount:unsigned int, m_Channels:vector<ChannelInfo>¦, m_DataSize:bytes¦}
  - `ChannelInfo` = {stream:UInt8, offset:UInt8, format:UInt8, dimension:UInt8}
  - `CompressedMesh` = {m_Vertices:PackedBitVector, m_UV:PackedBitVector, m_Normals:PackedBitVector, m_Tangents:PackedBitVector, m_Weights:PackedBitVector, m_NormalSigns:PackedBitVector, m_TangentSigns:PackedBitVector, m_FloatColors:PackedBitVector, m_BoneIndices:PackedBitVector, m_Triangles:PackedBitVector, m_UVInfo:unsigned int}
  - `PackedBitVector` = {m_NumItems:unsigned int, m_Range:float, m_Start:float, m_Data:vector<UInt8>¦, m_BitSize:UInt8¦}
  - `StreamingInfo` = {offset:UInt64, size:unsigned int, path:string¦}
  - `MeshLodInfo` = {m_LodSelectionCurve:LodSelectionCurve, m_NumLevels:int¦, m_SubMeshes:vector<MeshLodSubMesh>¦}
  - `LodSelectionCurve` = {m_LodSlope:float, m_LodBias:float¦}
  - `MeshLodSubMesh` = {m_Levels:vector<MeshLodRange>¦}
  - `MeshLodRange` = {m_IndexStart:unsigned int, m_IndexCount:unsigned int¦}

**TextAsset (49)**: m_Name:string¦, m_Script:string¦

**Font (128)**: m_Name:string¦, m_LineSpacing:float, m_DefaultMaterial:PPtr<Material>, m_FontSize:float, m_Texture:PPtr<Texture>¦, m_AsciiStartOffset:int, m_Tracking:float, m_CharacterSpacing:int, m_CharacterPadding:int, m_ConvertCase:int, m_CharacterRects:vector<CharacterInfo>¦, m_KerningValues:map<pair<UInt16,UInt16>,float>, m_PixelScale:float¦, m_FontData:vector<char>¦, m_Ascent:float, m_Descent:float, m_DefaultStyle:unsigned int, m_FontNames:vector<string>¦, m_FallbackFonts:vector<PPtr<Font>>¦, m_FontRenderingMode:int, m_UseLegacyBoundsCalculation:bool, m_ShouldRoundAdvanceValue:bool
  - `CharacterInfo` = {index:unsigned int, uv:Rectf, vert:Rectf, advance:float, flipped:bool¦}

**Canvas (223)**: m_GameObject:PPtr<GameObject>, m_Enabled:UInt8¦, m_RenderMode:int, m_Camera:PPtr<Camera>, m_PlaneDistance:float, m_PixelPerfect:bool, m_ReceivesEvents:bool, m_OverrideSorting:bool, m_OverridePixelPerfect:bool, m_SortingBucketNormalizedSize:float, m_VertexColorAlwaysGammaSpace:bool¦, m_AdditionalShaderChannelsFlag:int, m_UpdateRectTransformForStandalone:int¦, m_SortingLayerID:int, m_SortingOrder:SInt16, m_TargetDisplay:SInt8

**CanvasRenderer (222)**: m_GameObject:PPtr<GameObject>, m_CullTransparentMesh:bool

**CanvasGroup (225)**: m_GameObject:PPtr<GameObject>, m_Enabled:UInt8¦, m_Alpha:float, m_Interactable:bool, m_BlocksRaycasts:bool, m_IgnoreParentGroups:bool¦

**Camera (20)**: m_GameObject:PPtr<GameObject>, m_Enabled:UInt8¦, m_ClearFlags:unsigned int, m_BackGroundColor:ColorRGBA(f4), m_projectionMatrixMode:int, m_GateFitMode:int¦, m_Iso:int, m_ShutterSpeed:float, m_Aperture:float, m_FocusDistance:float, m_FocalLength:float, m_BladeCount:int, m_Curvature:Vector2f, m_BarrelClipping:float, m_Anamorphism:float, m_SensorSize:Vector2f, m_LensShift:Vector2f, m_NormalizedViewPortRect:Rectf, near clip plane:float, far clip plane:float, field of view:float, orthographic:bool¦, orthographic size:float, m_Depth:float, m_CullingMask:BitField, m_RenderingPath:int, m_TargetTexture:PPtr<RenderTexture>, m_TargetDisplay:int, m_TargetEye:int, m_HDR:bool, m_AllowMSAA:bool, m_AllowDynamicResolution:bool, m_ForceIntoRT:bool, m_OcclusionCulling:bool¦, m_StereoConvergence:float, m_StereoSeparation:float

**SpriteRenderer (212)**: m_GameObject:PPtr<GameObject>, m_Enabled:bool, m_CastShadows:UInt8, m_ReceiveShadows:UInt8, m_DynamicOccludee:UInt8, m_StaticShadowCaster:UInt8, m_MotionVectors:UInt8, m_LightProbeUsage:UInt8, m_ReflectionProbeUsage:UInt8, m_RayTracingMode:UInt8, m_RayTraceProcedural:UInt8, m_RayTracingAccelStructBuildFlagsOverride:UInt8, m_RayTracingAccelStructBuildFlags:UInt8, m_SmallMeshCulling:UInt8¦, m_ForceMeshLod:SInt16¦, m_MeshLodSelectionBias:float, m_RenderingLayerMask:unsigned int, m_RendererPriority:int, m_LightmapIndex:UInt16, m_LightmapIndexDynamic:UInt16, m_LightmapTilingOffset:Vector4f, m_LightmapTilingOffsetDynamic:Vector4f, m_Materials:vector<PPtr<Material>>¦, m_StaticBatchInfo:StaticBatchInfo, m_StaticBatchRoot:PPtr<Transform>, m_ProbeAnchor:PPtr<Transform>, m_LightProbeVolumeOverride:PPtr<GameObject>¦, m_SortingLayerID:int, m_SortingLayer:SInt16, m_SortingOrder:SInt16¦, m_MaskInteraction:int¦, m_Sprite:PPtr<Sprite>, m_Color:ColorRGBA(f4)¦, m_FlipX:bool, m_FlipY:bool¦, m_DrawMode:int, m_Size:Vector2f, m_AdaptiveModeThreshold:float, m_SpriteTileMode:int, m_WasSpriteAssigned:bool¦, m_SpriteSortPoint:int
  - `StaticBatchInfo` = {firstSubMesh:UInt16, subMeshCount:UInt16}

**MeshRenderer (23)**: m_GameObject:PPtr<GameObject>, m_Enabled:bool, m_CastShadows:UInt8, m_ReceiveShadows:UInt8, m_DynamicOccludee:UInt8, m_StaticShadowCaster:UInt8, m_MotionVectors:UInt8, m_LightProbeUsage:UInt8, m_ReflectionProbeUsage:UInt8, m_RayTracingMode:UInt8, m_RayTraceProcedural:UInt8, m_RayTracingAccelStructBuildFlagsOverride:UInt8, m_RayTracingAccelStructBuildFlags:UInt8, m_SmallMeshCulling:UInt8¦, m_ForceMeshLod:SInt16¦, m_MeshLodSelectionBias:float, m_RenderingLayerMask:unsigned int, m_RendererPriority:int, m_LightmapIndex:UInt16, m_LightmapIndexDynamic:UInt16, m_LightmapTilingOffset:Vector4f, m_LightmapTilingOffsetDynamic:Vector4f, m_Materials:vector<PPtr<Material>>¦, m_StaticBatchInfo:StaticBatchInfo, m_StaticBatchRoot:PPtr<Transform>, m_ProbeAnchor:PPtr<Transform>, m_LightProbeVolumeOverride:PPtr<GameObject>¦, m_SortingLayerID:int, m_SortingLayer:SInt16, m_SortingOrder:SInt16¦, m_MaskInteraction:int¦, m_AdditionalVertexStreams:PPtr<Mesh>, m_EnlightenVertexStream:PPtr<Mesh>
  - `StaticBatchInfo` = {firstSubMesh:UInt16, subMeshCount:UInt16}

**MeshFilter (33)**: m_GameObject:PPtr<GameObject>, m_Mesh:PPtr<Mesh>

**RenderTexture (84)**: m_Name:string¦, m_IsAlphaChannelOptional:bool¦, m_Width:int, m_Height:int, m_AntiAliasing:int, m_MipCount:int, m_DepthStencilFormat:int, m_ColorFormat:int, m_MipMap:bool, m_GenerateMips:bool, m_SRGB:bool, m_UseDynamicScale:bool, m_UseDynamicScaleExplicit:bool, m_BindMS:bool, m_EnableCompatibleFormat:bool, m_EnableRandomWrite:bool¦, m_TextureSettings:GLTextureSettings, m_Dimension:int, m_VolumeDepth:int, m_ShadowSamplingMode:int
  - `GLTextureSettings` = {m_FilterMode:int, m_Aniso:int, m_MipBias:float, m_WrapU:int, m_WrapV:int, m_WrapW:int}

**VideoClip (329)**: m_Name:string¦, m_OriginalPath:string¦, m_ProxyWidth:unsigned int, m_ProxyHeight:unsigned int, Width:unsigned int, Height:unsigned int, m_PixelAspecRatioNum:unsigned int, m_PixelAspecRatioDen:unsigned int, m_FrameRate:double, m_FrameCount:UInt64, m_Format:int, m_AudioChannelCount:vector<UInt16>¦, m_AudioSampleRate:vector<unsigned int>¦, m_AudioLanguage:vector<string>¦, m_VideoShaders:vector<PPtr<Shader>>¦, m_ExternalResources:StreamedResource, m_HasSplitAlpha:bool, m_sRGB:bool
  - `StreamedResource` = {m_Source:string¦, m_Offset:FileSize, m_Size:UInt64}

**AssetBundle (142)**: m_Name:string¦, m_PreloadTable:vector<PPtr<Object>>¦, m_Container:map<string,AssetInfo>, m_MainAsset:AssetInfo, m_RuntimeCompatibility:unsigned int, m_AssetBundleName:string¦, m_Dependencies:vector<string>¦, m_IsStreamedSceneAssetBundle:bool¦, m_ExplicitDataLayout:int, m_PathFlags:int, m_SceneHashes:map<string,string>
  - `AssetInfo` = {preloadIndex:int, preloadSize:int, asset:PPtr<Object>}

**PreloadData (150)**: m_Name:string¦, m_Assets:vector<PPtr<Object>>¦, m_Dependencies:vector<string>¦, m_ExplicitDataLayout:bool

**ResourceManager (147)**: m_Container:map<string,PPtr<Object>>, m_DependentAssets:vector<ResourceManager_Dependency>¦
  - `ResourceManager_Dependency` = {m_Object:PPtr<Object>, m_Dependencies:vector<PPtr<Object>>¦}

**BuildSettings (141)**: scenes:vector<string>¦, preloadedPlugins:vector<string>¦, enabledVRDevices:vector<string>¦, buildTags:vector<string>¦, hasPROVersion:bool, isNoWatermarkBuild:bool, isPrototypingBuild:bool, isEducationalBuild:bool, isEmbedded:bool, isTrial:bool, hasPublishingRights:bool, hasShadows:bool, hasSoftShadows:bool, hasLocalLightShadows:bool, hasAdvancedVersion:bool, enableDynamicBatching:bool, usesOnMouseEvents:bool, hasClusterRendering:bool¦, m_Version:string¦, m_AuthToken:string¦, m_GraphicsAPIs:vector<int>¦

**Animator (95)**: m_GameObject:PPtr<GameObject>, m_Enabled:UInt8¦, m_Avatar:PPtr<Avatar>, m_Controller:PPtr<RuntimeAnimatorController>, m_CullingMode:int, m_UpdateMode:int, m_ApplyRootMotion:bool, m_LinearVelocityBlending:bool, m_StabilizeFeet:bool, m_AnimatePhysics:bool¦, m_HasTransformHierarchy:bool, m_AllowConstantClipSamplingOptimization:bool, m_KeepAnimatorStateOnDisable:bool, m_WriteDefaultValuesOnDisable:bool¦

**BoxCollider2D (61)**: m_GameObject:PPtr<GameObject>, m_Enabled:UInt8¦, m_Density:float, m_Material:PPtr<PhysicsMaterial2D>, m_IncludeLayers:BitField, m_ExcludeLayers:BitField, m_LayerOverridePriority:int, m_ForceSendLayers:BitField, m_ForceReceiveLayers:BitField, m_ContactCaptureLayers:BitField, m_CallbackLayers:BitField, m_IsTrigger:bool, m_UsedByEffector:bool¦, m_CompositeOperation:int, m_CompositeOrder:int, m_Offset:Vector2f, m_SpriteTilingProperty:SpriteTilingProperty, m_AutoTiling:bool¦, m_Size:Vector2f, m_EdgeRadius:float
  - `SpriteTilingProperty` = {border:Vector4f, pivot:Vector2f, oldSize:Vector2f, newSize:Vector2f, adaptiveTilingThreshold:float, drawMode:int, adaptiveTiling:bool¦}

**SphereCollider (135)**: m_GameObject:PPtr<GameObject>, m_Material:PPtr<PhysicsMaterial>, m_IncludeLayers:BitField, m_ExcludeLayers:BitField, m_LayerOverridePriority:int, m_IsTrigger:bool, m_ProvidesContacts:bool¦, m_Enabled:bool¦, m_Radius:float, m_Center:Vector3f

## 12. Cross-check with the AssetRipper export

`AuxiliaryFiles/path_id_map.json` lists assets per file. For every asset it lists in sharedassets0 (6230), sharedassets1 (21), resources (52) and globalgamemanagers.assets (5499), the Linux build has an object with the **same PathID and class**. In sharedassets2, 52/56 match and 4 differ in type. The export maps only named assets, so its counts are smaller.
- The export's YAML scenes (`Assets/scenes/{launchscene,frontend,backend}.unity`), 13 bundle prefabs and 7004 MonoBehaviour .asset files (with field names) are therefore a good field-name reference for MonoBehaviour layouts.
- Treat them as **(export)**, because the version may be older.
- `AuxiliaryFiles/GameAssemblies/` has the Windows build's managed DLLs.

## 13. Reproducing

```
cd /tmp/claude-1000/asset-survey
python3 -I meta.py        # headers, externals
python3 -I survey1.py     # class histograms + read validation + MB script histograms
python3 -I shaders.py     # shaders + materials (pickles)
python3 -I tex.py         # textures
python3 -I bundle.py      # unpack hires_base_bundle to bundle_out/
python3 -I compact.py 1 4 224 ...   # compact field lists
```
