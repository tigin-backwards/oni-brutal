# How Oxygen Not Included uses Unity: a per-subsystem map

Researched 2026-10-15 against:
- decompiled game code in `cache/decomp/{Assembly-CSharp,Assembly-CSharp-firstpass,FMODUnity,UnityEngine.UI,Unity.TextMeshPro}` (ilspycmd 9.1). All paths below are relative to `cache/decomp/`. **A/** means `Assembly-CSharp/` and **F/** means `Assembly-CSharp-firstpass/`.
- the metadata inventory `cache/inventory/linux_unity.json`, which lists every Unity member the game assemblies reference;
- a second inventory, made for this document, of what `UnityEngine.UI.dll` and `Unity.TextMeshPro.dll` themselves reference in `UnityEngine.*`. It was generated with `tools/Inventory` into `/tmp/claude-1000/usage-research/pkg_unity.json`.

Call-site counts are `grep` counts over decompiled source, excluding `TMPro.Examples` where noted. They are approximate, but their order of magnitude is reliable. "Inventory" means that the member appears in `linux_unity.json`.

## 0. Executive summary

- **The world is not drawn by GameObjects with Renderers.** Nearly all world geometry is submitted every frame with `Graphics.DrawMesh` (24 call sites) from `LateUpdate`, using hand-built `Mesh`es and `MaterialPropertyBlock`s. The render pipeline is the built-in forward renderer, assembled by `CameraController` from about 9 cameras plus 6 off-screen "TextureLerper" cameras. Those cameras are chained through depth order, `targetTexture`, `SetTargetBuffers` (MRT), one `CommandBuffer`, `OnPreCull` full-screen quads, `OnRenderImage` blits (12 components) and `OnPostRender` GL drawing (6 components).
- **Most of the visual logic lives in Klei shaders**, which come from serialized assets and are not in code. Shader inputs are fed through about 70 `Shader.SetGlobal*` properties per frame (`A/Lighting.cs` sets 69 of them) and 17 global "property textures" built from Sim data (`A/PropertyTextures.cs`). Several of those are uploaded raw from native SimDLL pointers via `Texture2D.LoadRawTextureData(IntPtr,int)`.
- **KAnim (every animated sprite) is GPU-skinned.** The mesh has all-zero positions. The vertex shader reconstructs each quad from `uv0 = (controllerIdx, symbolIdx, corner)` and from point-sampled **RGBAFloat** data textures. Those textures are written by the CPU straight into `Texture2D.GetRawTextureData<T>()` NativeArrays and then sent with `Apply()` (`F/KAnimBatch*.cs`). This requires vertex-texture fetch and float textures.
- **UI is uGUI plus TextMeshPro.** It is built almost entirely from serialized prefabs: 327 `Util.KInstantiateUI` calls against about 40 UI `AddComponent` calls. It uses `Screen Space Overlay` and `Screen Space Camera` canvases, a `ConstantPixelSize` CanvasScaler driven by `KCanvasScaler`, and `StandaloneInputModule` (mouse) or Klei's `VirtualInputModule` (gamepad). The game's TMP is **Klei-patched**: it has `TextMeshProUGUI.KForceUpdateDirty`, and `TMP_StyleSheet.instance` / `AddStyle` / `RefreshStyles` / a 3-argument `TMP_Style` ctor. That is a strong argument for **running the shipped `UnityEngine.UI.dll` and `Unity.TextMeshPro.dll` unmodified on top of our `UnityEngine.*` modules**, rather than reimplementing them (section 8).
- **Object model.** There are 1,288 MonoBehaviour-derived classes; 1,095 of them derive from `KMonoBehaviour`. The game makes 10.5k `GetComponent<T>` calls, about 1,700 `SetActive` calls, about 800 Instantiate calls (through `Util.KInstantiate*` wrappers) and about 140 `StartCoroutine` calls. Gameplay prefabs are **runtime-built inactive GameObjects** (`A/EntityTemplates.cs`) that are cloned with `Object.Instantiate`. This means Unity's serialization-based clone semantics (which fields are copied, how references are remapped) are load-bearing.
- **Peripheral modules.** Audio is FMOD (no `UnityEngine.AudioModule` use at all). Physics, Physics2D, Timeline/Playables, IMGUI, JsonUtility and ScreenCapture are peripheral (debug tools or the FMOD integration). VideoPlayer (intro and DLC cinematics), ParticleSystem (a handful of FX), Animator (UI toggles), UnityWebRequest (MOTD, crash reporter) and ImageConversion (timelapse PNGs, mod and colony images) are user-visible but not core.
- **Not visible from code: scene and prefab data.** This includes which post-FX components sit on the scene's main camera, their order, the base camera depth, projection settings, layer names, script execution order and every UI layout value. All of it lives in `level0..2`, `sharedassets*.assets`, `resources.assets` and the DLC/texture AssetBundles in `StreamingAssets`. A faithful runtime therefore needs a Unity serialized-file loader. The ~30 Klei shaders also exist only as compiled Unity shader blobs in those files.

---

## 1. Rendering (most important)

### 1.1 Unity rendering APIs referenced (inventory) and call counts

| Area | Members referenced (inventory) | Call sites |
|---|---|---|
| `Camera` | `CopyFrom`, `Render`, `AddCommandBuffer`/`RemoveCommandBuffer`, `SetTargetBuffers(RenderBuffer[],RenderBuffer)`, set `clearFlags`/`cullingMask`/`depth`/`targetTexture`/`orthographic(Size)`/`fieldOfView`/`near`/`farClipPlane`/`rect`/`aspect`/`allowHDR`/`renderingPath`/`transparencySortMode`/`useOcclusionCulling`/`backgroundColor`/`depthTextureMode`, `ResetAspect`, all `ScreenTo*`/`ViewportTo*`/`WorldTo*` conversions, `ScreenPointToRay`/`ViewportPointToRay`, `pixelWidth/Height`, `Camera.main` | `Camera` 275 refs in 91 files; `Camera.main` 125 |
| `Graphics` | `DrawMesh` (6 overloads, including the one with `Camera` + `MaterialPropertyBlock` + shadow flags), `Blit` (3 overloads), `BlitMultiTap`, `DrawTexture` (NodeEditor only) | DrawMesh 24, Blit 23, BlitMultiTap 3 |
| `GL` | `Begin(int)`, `End`, `Vertex3`, `Vertex`, `TexCoord2`, `Color`, `PushMatrix`/`PopMatrix`, `LoadOrtho` | 118 refs, 12 `GL.Begin` sites (modes 1=LINES, 5=TRIANGLE_STRIP, 7=QUADS) |
| `CommandBuffer` | ctor, `Blit(Texture,RTI)`, `SetGlobalTexture(string,RTI)`, `SetRenderTarget(RTI[],RTI)`, `DrawRenderer`, `Clear`, `name` | 1 class (`A/MRTLiquidInterception.cs`) |
| `RenderTexture` | ctors `(w,h,d)`, `(w,h,d,fmt)`, `(RenderTexture)`; `GetTemporary` x3, `ReleaseTemporary`, `active` get/set, `IsCreated`, `Release`, `DiscardContents`, `colorBuffer`, `depthBuffer`, `format`, `depth`, `autoGenerateMips` | 17 `new`, 5 `GetTemporary` |
| `Texture2D` | ctors `(w,h)`, `(w,h,TextureFormat,bool)`, `(w,h,GraphicsFormat,TextureCreationFlags)`; `Apply()`/`Apply(bool,bool)`, `GetRawTextureData()` (byte[] and `NativeArray<T>`), `GetPixelData<T>`, `LoadRawTextureData(byte[])`/`(IntPtr,int)`, `SetPixels`, `SetPixels32`, `SetPixel`, `GetPixel(s)`, `GetPixelBilinear`, `ReadPixels`, `whiteTexture`, `grayTexture` | 28 `new Texture2D` |
| `Texture3D` | ctor, `SetPixels`, `SetPixels32`, `Apply` | 2 sites (`A/TerrainBG.cs` noise volume, F/ColorCorrectionLookup) |
| `Mesh` | `vertices`, `uv`, `uv2`, `colors32`, `normals`, `tangents`, `triangles`, `SetVertices(List)`, `SetUVs(ch, List<Vector2/Vector4>/Vector3[]/Vector4[])`, `SetColors(List<Color32>/List<Color>)`, `SetTriangles(int[]/List,sub[,bool])`, `SetIndices(int[],MeshTopology,sub)`, `bounds`, `RecalculateBounds`, `MarkDynamic`, `Clear` | 27 `new Mesh`; `.vertices=` 23, `SetUVs` 12, `SetTriangles` 10 |
| `Material` | ctors `(Shader)`/`(Material)`, `Set{Texture,Float,Vector,Color,Int,Matrix,FloatArray,VectorArray}` (string and int-ID forms), `Get{Float,Color,Texture,Int}`, `Enable/DisableKeyword`, `SetPass`, `passCount`, `renderQueue`, `shader`, `mainTexture`, `color`, `globalIlluminationFlags` | 53 `new Material`; SetTexture 68, SetInt 73, SetFloat 59, SetVector 55, SetColor 38 |
| `MaterialPropertyBlock` | ctor, `Set{Color,Float,Texture,Vector,VectorArray}`, `Get{Float,Texture,Vector}` | 9 `new` |
| `Shader` | `Find`, `PropertyToID`, `SetGlobal{Color,Float,Int,Texture,Vector}`, `isSupported`, `WarmupAllShaders` | Find 31, PropertyToID 127, SetGlobal 100 |
| Renderers | `MeshRenderer` (32 refs/18 files), `MeshFilter`, `SpriteRenderer` (3 files), `TextMesh` (LocText/TeleType only), `ParticleSystemRenderer` (type only), `CanvasRenderer.SetMesh/SetMaterial/materialCount/Clear`; `Renderer.material(s)/sharedMaterial/enabled/sortingOrder/shadowCastingMode/receiveShadows/lightProbeUsage/reflectionProbeUsage/bounds` | no `LineRenderer`, `TrailRenderer` or `SkinnedMeshRenderer` |
| Misc | `LayerMask.NameToLayer/GetMask`, `TransparencySortMode`, `RenderingPath`, `SystemInfo.SupportsRenderTextureFormat/SupportsTextureFormat/maxTextureSize`, `QualitySettings.activeColorSpace` | |

**Not used:** `DrawMeshInstanced`, `DrawProcedural`, `Graphics.SetRenderTarget`, `ComputeShader`, `GraphicsBuffer` (type only), `Mesh.SetVertexBufferData`, SRP/URP, `Light` components for lighting (`Light.color` is read once), and shadows.

### 1.2 Camera rig (`A/CameraController.cs`, `OnPrefabInit` lines 300-400)

The scene ships one camera (`Camera.main`, tagged MainCamera) that carries scene-configured components. `CameraController` clones it many times with `CloneCamera` (`new GameObject` + `AddComponent<Camera>` + `Camera.CopyFrom`). Let `d` be the scene camera's depth (scene data).

| Camera (name) | Depth | Clear | cullingMask | Target | Attached behaviours / notes |
|---|---|---|---|---|---|
| `TextureLerper_<prop>` x6 (`A/TextureLerper.cs`) | -100 | Nothing | `RTT` | ping-ponged `RenderTexture` (format of the source property) | ortho size 0.5, near -5, far 5, aspect 1. Each sits at x = `offsetCounter + 0.5` and sees only its own quad `MeshRenderer` running the `Klei/LerpEffect` material. `targetTexture` is swapped every frame in `Update()` |
| `baseCamera MRT` (made by `A/MultipleRenderTarget.cs` in a coroutine one frame after `Start`) | d-1 | inherited (scene) | baseCamera's mask without `Water` | `SetTargetBuffers([MRT0,MRT1(,MRT2)], MRT0.depthBuffer)` | `MultipleRenderTargetProxy` (screen-size ARGB32 RTs: MRT0 has 24-bit depth and point filtering, MRT1 is point, MRT2 is bilinear and only present while the "coloured overlay" is on). Also carries `MRTLiquidInterception`. **This camera renders the whole world.** |
| `Infrared` | d-1 | Color | 0 | half-res ARGB32 RT | `A/Infrared.cs`: `OnRenderImage` copies source to `minionTexture`. The GameObject is active only in the infrared/disease overlays |
| `Light Buffer` | d-1 | Color | `Lights` | `LightBuffer` RT (screen size, **ARGBHalf**) | `A/LightBuffer.cs` draws one quad per `Light2D` with `Graphics.DrawMesh(..., camera: this, props)` |
| `baseCamera` | d | Color (set by MRT setup) | 0, then `|= ForceDraw` | screen | Components in add order: `MultipleRenderTarget` (`OnPreCull` draws a `FullScreenQuad` of MRT0), `LightBufferCompositor` (`OnRenderImage`: lighting composite), `CameraRenderTexture("_LitTex")` (`OnRenderImage`: blits the source into its own RT and sets a global; it **does not write `dest`**). It also has the liquid `CommandBuffer` at `CameraEvent.BeforeForwardOpaque` |
| `SimOverlayCamera` (only if `SimDebugView.Instance`) | d+1 | Color | `SimDebugView` | screen (captured) | `CameraRenderTexture("_SimDebugViewTex")` |
| `timelapseCamera` (Game scene) | d+2 | (copy of overlay) | (copy) | — | `Timelapser`. Rendered manually with `Camera.Render()` (`CameraController.RenderCameraForTimelapse`) into a 32-bit-depth ARGB32 RT, then `ReadPixels` and `EncodeToPNG` |
| `Overlay` (the original scene `Camera.main`, renamed) | d+3 | **Nothing** | `PlaceWithDepth | Overlay | Construction` (+ overlay-mode layers at runtime) | screen | `renderingPath=Forward`, `allowHDR=false`, tag → Untagged. `CameraReferenceTexture` (`OnPreCull` draws `_LitTex` as a full-screen quad). Scene components: `KAnimActivePostProcessingEffects`, `CustomActiveScreenPostProcessingEffects`, `ColorCorrectionLookup`, and probably `FogOfWarPostFX`, `ClusterCoverPostFX`, `SimDebugViewCompositor`, `GridCompositor`, `BloomEffect`, `FixGraphicsCorruption` (scene data, to verify) |
| `overlayNoDepth` | d+4 | **Depth** | `Overlay | Place` | screen | **tag = "MainCamera", so `Camera.main` returns this camera after init.** Carries the `OnPostRender` GL visualizers `NavPathDrawer`, `RangeVisualizerEffect`, `SkyVisibilityVisualizerEffect`, `ScannerNetworkVisualizerEffect`, `RocketLaunchConditionVisualizerEffect` and `LargeImpactorVisualizerEffect` (DLC4) |
| `uiCamera` | d+5 | Depth | `UI` | screen | `worldCamera` for the screen-space-camera and world-space canvases (`GameScreenManager.SetCamera`) |
| `timelapseFreezeCamera` | uiCamera+3 | — | — | screen | disabled except while timelapse capture shows a frozen frame (`FillRenderTargetEffect` blits a texture to `null`) |

Other cameras: `A/VirtualCursorOverlayFix.cs` (gamepad cursor RT), `A/InitializeCheck.cs` (boot error screen, orthographic), and `A/BatchAnimCamera.cs` (debug, perspective `fieldOfView`).

**Projection.** Zoom is implemented by setting `orthographicSize` on every camera in `cameras` (`CameraController.OrthographicSize` setter). The camera rig sits at z = -100 (`OnPrefabInit`: `(W/2, H/2, -100)`). World→screen code uses `ViewportPointToRay` plus a plane at z=0 with `distance = |origin.z/direction.z|`, which works for both projections. The cameras are orthographic per scene data (to verify). `TransparencySortMode.Orthographic` is set on baseCamera and the timelapse camera.

**Per-frame globals from the camera:** `_WorldCameraPos` (xyz plus orthoSize), `_WorldCursorPos` (`CameraController.Update`) and `_CursorPos` (`World.LateUpdate`).

### 1.3 Frame composition, step by step

Inferred from the code. Unity renders cameras in ascending depth; callbacks run in the order `OnPreCull` → render → `OnRenderImage` chain (component order) → `OnPostRender`.

1. **`LateUpdate` producers.** Script order is scene-defined.
   - `PropertyTextures.LateUpdate` uploads the visible-region Sim property textures (section 1.4) and sets about 8 globals.
   - `World.LateUpdate` → `GroundRenderer.Render(visibleArea)`, `KAnimBatchManager.UpdateActiveArea` / `UpdateDirty(frameCount)` / `Render()`, then `FallingWater.Render()`. Each of these issues `Graphics.DrawMesh(..., camera: null)`, so the meshes are drawn by every camera whose `cullingMask` includes the layer.
   - `TerrainBG.LateUpdate` draws stars, background parallax layers, the gas plane twice (z of `Gas` and `GasFront`), and DLC4 meteor and aurora planes.
   - `BlockTileRenderer.Render` (tiles plus decor), `LightBuffer.LateUpdate` (light quads), `FlowOffsetRenderer` (feedback blit), `ConduitFlowVisualizer` / `SolidConduitFlowVisualizer`, `StatusItemRenderer`, `PrioritizableRenderer` (drawn to the world-space canvas camera only), `SpriteSheetAnimator`, `BubbleManager` and `ParallaxBackgroundObject` follow.
   - `Lighting.Update` sets 69 shader globals each frame.
2. **TextureLerper cameras (depth -100).** Each blends `_SourceTex` (the previous RT) toward `_TargetTex` (the latest property texture) with `_Lerp`, into the other RT of its pair. The result is published with `Shader.SetGlobalTexture("_<Prop>Tex", dest)`. The dest texture is chosen in `LateUpdate` and rendered later in the same frame.
3. **Light Buffer camera (d-1).** It clears and draws additive light quads (material from `CameraController.LightBufferMaterial`, a serialized field) into the ARGBHalf `LightBuffer`. Circle and cone overlay quads draw on the same layer when `Light2D.drawOverlay` is set.
4. **Infrared camera (d-1, overlay only).**
5. **baseCamera MRT (d-1).** It renders every world layer (Default, World, Construction, MaskedOverlay..., not Water) into MRT0 (colour plus depth) and MRT1 (a second colour output from the same shaders). MRT2 (bilinear) is written only when the coloured overlay is on.
6. **baseCamera (d).**
   - Clear colour.
   - `OnPreCull`: queue the full-screen quad (`Klei/PostFX/FullScreen`, layer `ForceDraw`, `invert:true` UVs) sampling MRT0.
   - **CommandBuffer at `BeforeForwardOpaque`** (`MRTLiquidInterception`): `Blit(MRT0→MRT0_Copy)`, `Blit(MRT1→MRT1_Copy)`, `SetGlobalTexture("MRT0_Copy"/"MRT1_Copy")`, `SetRenderTarget([MRT0,MRT1(,MRT2)], MRT0.depth)`, `DrawRenderer(WaterCubes.waterRenderer, material)`. Liquids are drawn **into the MRT** and can read the pre-liquid copies, for refraction.
   - Forward pass: draw the ForceDraw quad. This relies on Unity re-binding the camera target after the CB; **verify**.
   - `OnRenderImage` chain: `LightBufferCompositor` blurs the temperature property texture to 1/8 res (`GetTemporary`, `Blit` with `Klei/PostFX/Blur`, global `_BlurredTemperature`) and sets `_LightBufferTex`, then runs `Blit(src,dest,Klei/PostFX/LightBufferCompositor)` with keyword `DISABLE_TEMPERATURE_PARTICLES` toggled. `CameraRenderTexture` then blits into the global `_LitTex`.
7. **SimOverlayCamera (d+1)** → `_SimDebugViewTex`.
8. **Overlay camera (d+3).** No clear. `OnPreCull` queues a full-screen quad of `_LitTex` (`CameraReferenceTexture`); then overlay/construction-layer objects draw over it. Then the post-FX `OnRenderImage` chain runs:
   - `KAnimActivePostProcessingEffects`: copy, then re-draw KAnim batches with `Klei/BatchedAnimationPstTemperature` for the temperature overlay, issuing `Graphics.DrawMesh` *from inside `OnRenderImage`* on layer `Overlay`.
   - `CustomActiveScreenPostProcessingEffects`: a ping-pong chain of `Func<RenderTexture,Material>` effects using `GetTemporary(w,h,depth,format)`.
   - `ColorCorrectionLookup` (3D LUT via `Texture3D`, pass chosen by colour space).
   - Fog of war (`_UVOffsetScale`), cluster cover (`_CameraCoords`, `_WorldCoords`, `_HideSurface`), SimDebugView compositor (enabled only in debug views), grid compositor (enabled by toggle), and bloom (`BloomEffect`: mask, 4-tap downsample via `BlitMultiTap`, composite).
9. **overlayNoDepth (d+4).** Clear depth, draw `Overlay` and `Place` layers (build placers, selection), then `OnPostRender` GL quads:
   - `material.SetPass(0); GL.PushMatrix(); GL.LoadOrtho(); GL.Begin(TRIANGLE_STRIP)`, a full-screen 4-vertex strip.
   - Shader inputs include an occlusion `Texture2D` (Alpha8 or RGFloat, point, clamp) written via `GetRawTextureData`/`GetPixelData` and then `Apply`.
   - `NavPathDrawer` uses `GL.LINES` (debug).
10. **uiCamera (d+5).** Clear depth, then screen-space-camera canvases.
11. **Screen-space-overlay canvases** (main HUD, front end, crash dialogs) draw last.

**Semantics this pipeline depends on:**
- **`Graphics.DrawMesh` queueing.** Calls made in `Update`/`LateUpdate`, and even inside `OnRenderImage`, are queued for the *current frame* and are culled per camera by layer. A `camera` argument restricts a draw to one camera. Draws are not retained across frames.
- **Bounds and culling.** KAnim, FullScreenQuad and Light meshes use `bounds = (0, float.MaxValue extents)` to defeat culling. Sort position is the transform translation; KAnim adds `z + 0.01/(1+id%256)` to break ties.
- **Render queues** (`A/RenderQueues.cs`): WorldOpaque 2000, Backwall 2001, Stars 2501, NaturalBackwall 3498, BackwallTransparent 3499, Gas/WorldTransparent/Liquid 3500, BlockTiles 4500 (+ element idx for liquid tile overlays). Transparent queues are sorted back-to-front along view Z (orthographic sort mode). Equal-queue ties fall back to submission order.
- **Scene Z layering** (`A/Grid.cs` `GetLayerZ`): `z = -0.5*cell - cell*layer*LayerMultiplier` for `SceneLayer` -3..34 (Background=-1, Backwall=1, Gas=2 ... Liquid=28, Ground=29, TileMain=30, TileFront=31, FXFront2=33). Depth testing between world draws depends on this.
- **`OnRenderImage` contract.** `source` is the camera's colour buffer, and `dest` is either the next component's source or the final target (`null` means the backbuffer). `Graphics.Blit(tex, (RenderTexture)null)` writes to the screen (`FillRenderTargetEffect`). `CameraRenderTexture` never writes `dest`, so what that camera leaves in the backbuffer is undefined; later cameras overwrite it.
- **`Camera.main`** is "the first enabled camera tagged MainCamera" and must react to tag changes made at runtime.
- **`Camera.CopyFrom`** copies camera settings (depth, mask, clear, projection, rect, `targetTexture`). It does not copy GameObject tag, components or command buffers.
- **Screen resize.** Most RTs are rebuilt on `ScreenResize.Instance.OnResize`, and `MultipleRenderTargetProxy.Update` rebuilds when `!IsCreated()` (lost RTs). `RenderTexture.DestroyRenderTexture` is a Klei extension (`F/RenderTextureDestroyerExtensions.cs`).
- **Overlay modes** (`A/OverlayModes.cs`, 2,200 lines) do not swap shaders. They (a) set `Camera.main.cullingMask |= cameraLayerMask` (`MaskedOverlay`, `MaskedOverlayBG`), (b) move objects between layers (`KBatchedAnimController.SetLayer`; the KAnim batch layer is the GameObject layer), (c) toggle MRT2 and the `Infrared` camera, and (d) set globals such as `_ColouredOverlayParameters` and `_OverlayParams`.

### 1.4 Property textures: the Sim → GPU bridge (`A/PropertyTextures.cs`, `F/TextureBuffer.cs`, `F/TexturePage*.cs`, `F/TextureRegion.cs`)

Seventeen world-sized textures (`Grid.WidthInCells x HeightInCells`) are exposed as globals named `_<Property>Tex`:

| Property | Format | Filter | Update path | Lerped (camera) |
|---|---|---|---|---|
| Flow | RGFloat | bilinear | external: `LoadRawTextureData(SimDLL ptr, 8*W*H)` every frame | yes (0.25) |
| Liquid | RGBA32 | — | external, every frame | yes (1) |
| LiquidData | RGBA32 | — | external, every frame | yes (1) |
| MaterialData | RGBA32 | point | external, every frame | no |
| ExposedToSunlight | Alpha8 | — | external, every frame | no |
| SolidDigAmount | RGB24 | — | CPU, every frame | no |
| GasColour / GasDanger / GasPressure | RGBA32 / Alpha8 / Alpha8 | bilinear | CPU, round-robin | yes (0.25) |
| FogOfWar | Alpha8 | — | CPU, every frame | no |
| WorldLight | RGBA32 | — | CPU, round-robin | no |
| StateChange, FallingSolid | Alpha8 | — | CPU, round-robin | no |
| SolidLiquidGasMass(+ForLight) | RGBA32 | — | CPU, every frame | no |
| Temperature | RGB24 | — | CPU, round-robin | no |
| Radiation | RFloat | bilinear | CPU, round-robin | no |

**CPU path.**
1. `TextureBuffer.Lock(region)` gets a `TexturePage` (a `Texture2D`, sides rounded up to multiples of 32, point and clamp) from a pool that double-buffers by `Time.frameCount % 2`.
2. Worker threads (`GlobalJobManager`, 16-row chunks) write directly into `page.texture.GetRawTextureData<byte|float>()`.
3. `Unlock` → `Apply()`, then `Graphics.Blit(page, bufferRT, Klei/TexturePage)` with `_Region` set, so only the dirty sub-rect of a persistent `RenderTexture` is updated.

**Requirements this places on us:**
- `GetRawTextureData<T>()` returns a NativeArray that **aliases** the texture's CPU copy, is writable from other threads, and stays valid until `Apply`.
- `LoadRawTextureData(IntPtr,int)` copies from native memory.
- `TextureUtil.GetRenderTextureFormat` and `TextureFormatToGraphicsFormat` map TextureFormat → RenderTextureFormat/GraphicsFormat (Alpha8→R8, RGFloat, RFloat, RGB24 and others). We need the same RT format support.

Globals set alongside: `_WorldSizeInfo`, `_ClusterWorldSizeInfo`, `_CameraZoomInfo`, `_PropTexWsToCs`, `_PropTexCsToWs`, `_TopBorderHeight`, `_FogOfWarScale`. `SubworldZoneRenderData` adds `_WorldZoneTex` and `_WorldZoneIndexTex` (RGB24 via `LoadRawTextureData(byte[])`).

### 1.5 KAnim batching (`F/KAnimBatchManager.cs`, `F/KAnimBatchGroup.cs`, `F/KAnimBatch.cs`, `F/BatchSet.cs`, `F/KBatchedAnimInstanceData.cs`, `F/SymbolInstanceGpuData.cs`, `F/SymbolOverrideInfoGpuData.cs`, `A/KBatchedAnimController.cs`)

**Spatial bucketing.** Controllers are bucketed by (batch group, layer, z, material type) into `BatchSet`s per 32x32-cell chunk. `UpdateActiveArea` activates the sets in visible chunks; UI and always-visible sets are always active.

**Mesh per group.** `BuildMesh(maxGroupSize * maxVisibleSymbols)` builds quads where every vertex position is `Vector3.zero` and `uv0` (via `SetUVs(0, Vector3[])`) = `(controllerIdx, maxVisibleSymbols-1-symbolIdx, corner 0..3)`. Indices come from `SetIndices(int[], MeshTopology.Triangles, 0)` and bounds are infinite. **The vertex shader computes everything from data textures**, so we need VTF on float textures and an exact vertex ordering.

**Data textures.** All are `Texture2D(w, h, RGBAFloat, mipChain:false)`, point and clamp, with power-of-two width. Pooled sizes come from `KAnimBatchTextureCache`.
- `instanceTex`: 112 bytes per controller.
- `symbolInstanceTex`: 8 float4 per symbol per controller.
- `symbolOverrideInfoTex`: 12 float4 per override.
- `buildAndAnimTex`: shared per group, uploaded once with `Apply(false, makeNoLongerReadable:true)`.

The CPU writes through `GetRawTextureData<byte>/<float>()` and then calls `Apply()` on dirty batches (`UpdateDirty(Time.frameCount)`).

**Per-batch `MaterialPropertyBlock`.**
- Textures: `instanceTex`, `symbolInstanceTex`, `symbolOverrideInfoTex`, `buildAndAnimTex`.
- Size vectors `*_TEXTURE_SIZE = (texelW, texelH, mask, shift)`.
- `ANIM_TEXTURE_START_OFFSET` and `SUPPORTS_SYMBOL_OVERRIDING`.
- Up to 16 atlas textures `atlas0..atlas15`. The per-material-type maximum is `{12,12,12,16,12,13,12,12}`. The shader therefore samples up to 16 textures plus 4 data textures.

**Materials.** Per group and type, `new Material(Shader.Find(...))`:
- `Klei/BatchedAnimation` (Default)
- `Klei/AnimationSimple`
- Placer
- `Klei/BatchedAnimationUI`
- `Klei/AnimationInvisible`
- `Klei/BatchedAnimationHuman`
- `Klei/BatchedAnimationShine`
- DefaultInsideVistas, which sets `_StencilRef=3`, `_StencilComp=4` and `_StencilPass=0`

Material floats `SYMBOLS_PER_BUILD`, `ANIM_TEXTURE_START_OFFSET` and `SYMBOL_OVERRIDES_PER_BUILD` are set per material.

**Draw.** `Render()` issues one `Graphics.DrawMesh(groupMesh, (0,0,z+ε), identity, mat, batch.layer, null, 0, batch.matProperties)` per active non-UI batch, every frame. That is typically hundreds to thousands of draws per frame.

**UI KAnims** (`F/KBatchedAnimCanvasRenderer.cs`) put the same zero-position group mesh into a `CanvasRenderer.SetMesh` and copy the property block into a per-object material clone (`uiMat`). They also implement `IMaskable.RecalculateMasking`: walking `GetComponentsInParent<Mask>()` they set `_StencilComp/_StencilOp/_StencilWriteMask/_ColorMask` themselves. Our canvas batching must **not** rely on CPU vertex positions for these meshes.

**Atlases.** These are `Texture2D` assets from KAnimFile ScriptableObjects. They can be swapped for hi-res ones from `StreamedTextures` (`AssetBundle.LoadFromFileAsync`, `F/StreamedTextures.cs`).

### 1.6 Terrain, tiles, liquids and gases

- **GroundRenderer** (`A/GroundRenderer.cs`, natural solid terrain).
  - Chunked dynamic meshes (`MarkDynamic`, `SetVertices/SetUVs/SetTriangles`) are rebuilt for dirty cells (`MarkDirty` from `World.UpdateCellInfo`).
  - Each element has 4 material clones (opaque, alpha, natural backwall, backwall edge) configured by keywords `OPAQUE`/`ALPHA` (+ `SHINY`/`MATTE`) and ints `_SrcAlpha`/`_DstAlpha`/`_ZWrite`/`_IsBackwallEdge`/`_StencilPass`, with `_AlphaTestMap` (white, gray, or a mask atlas). The blend state is set via material ints, so we need **material-driven render state** (blend factors, ZWrite, stencil from properties).
  - Drawn via `DrawMesh(..., ShadowCastingMode.Off, receiveShadows:false, null, useLightProbes:false)` on layer `World`.
- **BlockTileRenderer** (`A/Rendering/BlockTileRenderer.cs`, built tiles and decor).
  - One material clone per (BuildingDef, Built/UnderConstruction/Replacement), with `_MainTex`, optional `_SpecularTex` + `ENABLE_SHINE`, and `_packedParameters`.
  - Meshes are per 16x16 chunk, built with lists (vertices, uv, colours, triangles), and decor meshes sit at z-0.5.
  - `Rendering.World.TileRenderer`/`Brush`/`DynamicMesh`/`DynamicSubMesh` and `LiquidTileOverlayRenderer` are similar.
- **Liquids.** `A/WaterCubes.cs` is a single static `MeshRenderer` (one quad per cell with uv, uv2, normals and tangents) on layer `Water` with render queue 3500 and `_MainTex2` = wave texture. It is drawn **only** via the baseCamera command buffer. The shader reads `_LiquidTex`, `_LiquidDataTex`, `_FlowTex` and the `MRT*_Copy` textures. `LateUpdate` sets `_CursorWorldPosition` and `LiquidShaderProperties`. `A/FallingWater.cs` draws a dynamic particle mesh with an MPB (`_MainTex`) and `_ClusterWorldSizeInfo`.
- **Gases** are not meshes per cell. `A/TerrainBG.cs` draws one world-sized `gasPlane` twice with `gasMaterial` (queue 3500). The shader samples the lerped `_GasColourTex`/`_GasPressureTex`/`_GasDangerTex` and the flow offset textures. Those come from `A/FlowOffsetRenderer.cs`: a half-res ARGBHalf ping-pong `Graphics.Blit(prev, next, Klei/Flow)` with `_PreviousOffsetTex`, `_FlowParameters`, `_MinFlow`, `_VisibleArea` and `_LiquidGasMask`, published as a global.
- **Background.** `TerrainBG` draws the world backwall plane per parallax layer with an MPB (`_BackWallParameters`, `SetVectorArray("_ZoneTextureParallaxData")`), the stars plane (`_NoiseVolume` = procedural `Texture3D` RGBA32 with mips) and DLC4 fragments.

### 1.7 Smaller world renderers

| Class | What it does |
|---|---|
| `A/StatusItemRenderer.cs` | status icons over buildings; `DrawMesh(..., camera)` per entry; global `_StatusItemParameters` |
| `A/PrioritizableRenderer.cs` | priority overlay quads drawn to `worldSpaceCanvas.worldCamera` only |
| `A/ConduitFlowVisualizer.cs`, `SolidConduitFlowVisualizer.cs` | conduit contents; dynamic mesh with `Klei/ConduitBall` |
| `A/SpriteSheetAnimator.cs` / `SpriteSheetAnimManager.cs` | sprite-sheet FX quads with an MPB |
| `A/BubbleManager.cs` | bubbles with an MPB |
| `A/ParallaxBackgroundObject.cs` | `Resources.GetBuiltinResource<Mesh>("Quad.fbx")` + `DrawMesh(matrix)` |
| `A/SimDebugView.cs` | debug overlay plane `MeshRenderer` on layer `SimDebugView`; `Texture2D.SetPixels`/`LoadRawTextureData`; composited by `SimDebugViewCompositor` with `_Color0.._Color7` and `_Thresholds0/1` |
| Placers and tools (`CommonPlacerConfig`, `DigPlacerConfig`, `ToolMenu`, `StampToolPreview*`) | `MeshRenderer` prefabs on `Place`/`PlaceWithDepth` layers; `ToolMenu` creates an RGBA32 texture via `GraphicsFormat` + `LoadRawTextureData` |
| `A/BackwallSelectionObject.cs`, `CellSelectionObject.cs`, `DragTool.cs` | `SpriteRenderer` (`sprite`, `size`, `sortingOrder=1`) for selection boxes |
| `A/LineLayer.cs` | `Texture2D(96,32)` + `SetPixels32` + `Sprite.Create`, used in a UI Image |

### 1.8 Lists for the replacement

**Shader.Find names** (31 sites):
- KAnim and world: `Klei/BatchedAnimation`, `Klei/AnimationSimple`, `Klei/BatchedAnimationUI`, `Klei/AnimationInvisible`, `Klei/BatchedAnimationHuman`, `Klei/BatchedAnimationShine`, `Klei/BatchedAnimationPstTemperature`, `Klei/TexturePage`, `Klei/LerpEffect`, `Klei/Flow`, `Klei/ConduitBall`, `Klei/StatusItem`, `Klei/Prioritizable`, `Klei/BuildingCell`, `Klei/DLC4/LargeImpactorCometShader`.
- Post-FX: `Klei/PostFX/FullScreen`, `.../CameraRenderTexture`, `.../LightBufferCompositor`, `.../Blur`, `.../BloomMask`, `.../BloomComposite`, `.../GridCompositor`, `.../SimDebugViewCompositor`, `.../Range`, `.../SkyVisibility`, `.../ScannerNetwork`, `.../RocketLaunchCondition`, `.../LargeImpactorVisualizerShader`.
- Third-party: `TextMeshPro/Distance Field`, `TextMeshPro/Mobile/Distance Field SSD`, `Sprites/Default`, `Lines/Colored Blended`, `Hidden/LineShader`, `Hidden/GUITextureClip_ChannelControl` (NodeEditor).
- Plus every shader referenced by serialized materials (ground per element `substance.material`, `def.BlockTileMaterial`, water, gas, backwall, light, UI default and TMP).

**Shader keywords:** `ALPHA`, `OPAQUE`, `SHINY`, `MATTE` (ground, liquid tiles), `ENABLE_SHINE` (block tiles), `DISABLE_TEMPERATURE_PARTICLES` (light compositor). All are per-material, with no global keywords.

**Shader globals.** Literal names: `_MRT0`, `_MRT1`, `_MRT2`, `_LitTex`, `_SimDebugViewTex`, `_BlurredTemperature`, `_WorldZoneTex`, `_WorldZoneIndexTex`, `_ColouredOverlayParameters`, `_CursorPos`, `_MetalParameters`, `_NoiseParameters`, `_OverlayParams`, `_StatusItemParameters`, `_TimeOfDay`, `_WaterParameters`, `_WorldCameraPos`, `_WorldCursorPos`, `_WsToCcs`, `_WsToCs`, plus the 17 `_<Property>Tex`.

Via `PropertyToID`, mostly in `A/Lighting.cs::UpdateLighting`, every frame:
- `_LiquidZ` (int).
- Global textures: `_DigDamageMap`, `_StateTransitionMap`, `_FallingSolidMap`, `_HeatHazeTexture`, `_GenericNormalNoise`, `_DustTex`, `_DiseaseOverlayTex`, `_BuildingDamagedTex`, `_LightBufferTex`.
- Global colours: `_HotBG/MG/FG`, `_ColdBG/MG/FG`, `_RadHazeColor`, `_DarkenTintBackground/Midground/Foreground`, `_BrightenOverlay`, `_CharacterLitColour`, `_CharacterUnlitColour`, `_GridColor`, `_DustColour`, `_FallingSolidColor`, `_StateTransitionColor`, `_WaterTrimColor`.
- Global vectors: `_WaterParameters2`, `_WaterWaveParameters(2)`, `_WaterDetailParameters`, `_WaterDistortionParameters`, `_LiquidParameters2`, `_TileEdgeParameters`, `_SubstanceParameters`, `_AnimParameters`, `_ShineParams(2)`, `_EdgeGlowParameters`, `_HeatHazeParameters`, `_TemperatureParallax`, `_Hot/ColdUVOffset1/2`, `_RadUVOffset1/2`, `_RadUVScales`, `_RadRange1/2`, `_DigMapMapParameters`, `_StateTransitionParameters`, `_FallingSolidParameters`, `_DustInfo`, `_DiseaseOverlayTexInfo`, `_BuildingDamagedUVParameters`, `_BloomParameters`, `_GridParameters`, `_GasOpacity`, `_DebugShowInfo`.
- Global floats: `_WorldZoneForegroundBlend/GasBlend/LiquidBlend/SimpleAnimBlend`.

**Material and MPB property names set by code** (string form):
- `_MainTex`, `_MainTex2`, `_SpecularTex`, `_AlphaTestMap`, `_Colour`, `_ColourTint`, `_ShineColour`, `_ShineMask`
- `_SrcAlpha`, `_DstAlpha`, `_ZWrite`, `_IsBackwallEdge`, `_StencilRef`, `_StencilComp`, `_StencilPass`, `_StencilOp`, `_StencilWriteMask`, `_ColorMask`, `_packedParameters`
- `_LightBufferTex`, `_InvalidTex`, `_EmberTex`, `_FrostTex`, `_Thermal1Tex`, `_Thermal2Tex`, `_RadHaze1Tex`, `_RadHaze2Tex`, `_RadHaze3Tex`, `_NoiseTex`, `_PropertyWorldLight`
- `_TargetTex`, `_SourceTex`, `_Lerp`, `_MeshParams`, `_Region`
- `_PreviousOffsetTex`, `_FlowParameters`, `_MinFlow`, `_VisibleArea`, `_LiquidGasMask`
- `_UVOffsetScale`, `_CameraCoords`, `_WorldCoords`, `_HideSurface`
- `_RangeParams`, `_HighlightColor`, `_HighlightColor2`, `_Highlight`, `_OcclusionParams`, `_OcclusionTex`, `_WorldParams`, `_Icon`
- `_BackWallParameters`, `_ZoneTextureParallaxData` (vector array), `_NoiseVolume`, `_IsInFlight`, `_AccelerationTimeStamp`, `_DecelerationTimeStamp`, `_EntryProgress`, `_LargeImpactorScale`, `_UnscaledTime`
- `_Comet*`, `_Explosion*`, `_LastTime*`, `_Fold*`, `_Landing*`, `_SizeProgress`
- `_CursorWorldPosition`, `_ClusterWorldSizeInfo`, `_BoilingTreshold`, `_MoltenMetalColor`
- `_BloomTex`, `_ClutTex`, `_ClutTex2`, `_Color0.._Color7`, `_Thresholds0/1`, `_ThresholdParameters`
- `_EnvMatrix` (matrix), `_LineColor`, `_LineTexture`, `_TextureScrollSpeeds`, `_SpriteSettings`, `_UIToggleScreenPosition`, `_Offset`, `_Scale`, `_Random`, `_Frequency`, `_WorldUVScale`, `_BackgroundTex`, `_ForegroundTex`, `_ZoneWasRevealed`, `_LastTimePlayerClickedNotification`

Light buffer MPB: `_ColorRange`, `_LightPos`, `_LightDirectionAngle`, `_TintColor`. KAnim names are listed in 1.5.

**Layers used by name:** `Default` (15), `MaskedOverlay` (14 + 11 masks), `Place` (13), `PlaceWithDepth`, `Overlay`, `Construction`, `MaskedOverlayBG`, `World`, `Water`, `UI`, `RTT`, `Pickupable`, `Lights`, `ForceDraw`, `SimDebugView`, `BlockSelection`, `Selection`. The layer names-to-indices table is in `globalgamemanagers` (TagManager).

**Sorting:** `Renderer.sortingOrder` is used only by `BackwallSelectionObject` (sprite). `Canvas.sortingOrder` is used for front-end canvases (incrementing), the crash reporter (32767), `UnityMouseCatcherUI` (32767), achievements (30) and `DragMe` (99). No sorting layers are used.

**RenderTexture inventory:**
- MRT0 (ARGB32, 24-bit depth) and MRT1/MRT2 (ARGB32), plus copies.
- `_LitTex`/`_SimDebugViewTex` (ARGB32 screen size).
- LightBuffer (ARGBHalf screen size).
- Infrared (2x ARGB32 half-res).
- FlowOffset (2x ARGBHalf half-res).
- TextureLerper (2x per lerped property, world size).
- TextureBuffer (1 per CPU property, world size, format mapped from TextureFormat).
- Timelapse (32-bit depth, preview/user resolution).
- Video (clip size, 16-bit depth).
- Virtual cursor (screen size).
- Temporaries: bloom, light blur (1/8 screen), custom FX chain.

---

## 2. UI

### 2.1 Canvases and scaling

- **`F/GameScreenManager.cs`** holds 5 canvases from scene/prefab data: `worldSpaceCanvas`, `ssCameraCanvas`, `ssOverlayCanvas` (default target for screens), `ssHoverTextCanvas` and `screenshotModeCanvas`. Their `worldCamera` is assigned to `uiCamera` at init.
  - `RenderMode` values seen in code: `ScreenSpaceOverlay` (FrontEnd canvases, crash reporter, file error dialog, mouse catcher, VirtualInputModule cursor) and `ScreenSpaceCamera` (virtual cursor).
  - `WorldSpace` is checked in `KBatchedAnimController` (screen offset) and is used by the world-space canvas (hover/world UI).
- **Front end** (`A/FrontEndManager.cs` `ConfigureAsKleiCanvas`): `Canvas` overlay with `additionalShaderChannels = TexCoord1 | Normal | Tangent` (needed by TMP), `CanvasScaler.uiScaleMode = ConstantPixelSize`, `GraphicRaycaster`, `KCanvasScaler`.
- **`A/KCanvasScaler.cs`** computes `scaleFactor = userScale(KPlayerPrefs "UIScalePref"/100) * step(Screen.height)`. The steps are ≤720 → 0.86, 1080 → 1.0, 2160 → 1.33, linear between, and 0.86 when aspect < 1.6. It is recomputed on resize. `scaleFactor` is read back in 3+ places (tooltips, `KBatchedAnimController`, `DropDown`, `GridLayouter`).
- **Canvas API used:** `ForceUpdateCanvases`, `isRootCanvas`, `pixelPerfect`, `overrideSorting`, `sortingOrder`, `worldCamera`, `renderMode`, `referencePixelsPerUnit`. `CanvasGroup` sets `alpha`/`blocksRaycasts`/`interactable`. `RectTransformUtility` covers `ScreenPointToLocalPointInRectangle`, `RectangleContainsScreenPoint`, `ScreenPointToWorldPointInRectangle` and `WorldToScreenPoint`.

### 2.2 Prefabs vs code

UI is **prefab-driven**:
- 327 `Util.KInstantiateUI` calls (`Object.Instantiate(original, parent, worldPositionStays:false)`, optionally `SetActive(true)`).
- Serialized references (public fields typed `GameObject` ×398, `LocText` ×182, `Image` ×118, `KButton` ×115, `RectTransform` ×62, …).
- Only about 40 `AddComponent<UI type>` calls in code (Canvas 7, RectTransform 5, TextMeshProUGUI 4, CanvasGroup 4, GridLayoutGroup 3, GraphicRaycaster 3, …).
- `HierarchyReferences` (`GetReference<T>(name)`) is Klei's named-reference lookup inside prefabs.

### 2.3 uGUI usage

**Subclassed by the game (direct bases):**
- `Image` → `F/KImage.cs`
- `Toggle` → `F/KToggle.cs`, `F/KToggleSlider.cs`
- `Slider` → `F/KSlider.cs`
- `ScrollRect` → `F/KScrollRect.cs` (overrides `OnBeginDrag/OnDrag/OnEndDrag`, `LateUpdate`, `OnRectTransformDimensionsChange`)
- `LayoutElement` → `F/KLayoutElement.cs`, `A/MotdBox_ImageButtonLayoutElement.cs`
- `AspectRatioFitter` → `A/ImageAspectRatioFitter.cs`, `A/RawImageAspectRatioFitter.cs`
- `UIBehaviour` → `A/CodexImageLayoutMB.cs`, `A/KleiPermitDioramaVisScaler.cs`
- `Graphic` → `A/NonDrawingGraphic.cs` (raycast-only)
- `MaskableGraphic` → `F/UnityEngine.UI.Extensions/UIPrimitiveBase.cs` (+ `UILineRenderer`, `OnPopulateMesh`, `VertexHelper.AddUIVertexQuad`)
- `PointerInputModule` → `A/UnityEngine.EventSystems/VirtualInputModule.cs`
- `TextMeshProUGUI` → `A/LocText.cs`; `TMP_InputField` → `F/KInputTextField.cs`; `TMP_InputValidator` → `A/TMPro/TMP_DigitValidator.cs`, `TMP_PhoneNumberValidator.cs`

These overrides call protected members (`Selectable.OnPointerEnter/Exit/Up`, `OnSelect`, `OnDeselect`, `Image.OnEnable/OnDisable`, `Toggle.Start/OnEnable/OnDisable/OnDestroy`, `Graphic.OnDestroy`, `SetVerticesDirty`, `LayoutElement.OnEnable/OnDisable`, `UIBehaviour.Awake/Start/OnRectTransformDimensionsChange`, the `PointerInputModule` internals listed in the inventory). So the replacement must keep **the exact virtual/protected surface and call ordering** of the shipped uGUI.

**Klei's own widgets** are `KMonoBehaviour`s implementing EventSystems interfaces rather than `Selectable`s:
- `F/KButton.cs`: `IPointerEnter/Click/Exit/Down/Up`.
- `F/KScreen.cs`: pointer enter/exit and drag.
- `MultiToggle`, `ToolTip`, `KScreen` (68 direct subclasses).

Interface implementation counts (direct declarations): `IPointerEnterHandler` 24, `IPointerExitHandler` 23, `IPointerClickHandler` 10, `IPointerDownHandler` 6, `IBegin/IEnd/IDragHandler` 5 each, `IPointerUpHandler` 4, `IDeselectHandler` 2, `IScrollHandler` 1.

**Used but not subclassed:**
- Widgets: `Button` (`onClick`), `Dropdown`, `InputField` (legacy), `Scrollbar`, `Text` (legacy, mostly debug and mod screens), `RawImage`, `ToggleGroup`, `ColorBlock`, `Navigation` (`mode`).
- Layout: `GridLayoutGroup`, `Horizontal/VerticalLayoutGroup`, `ContentSizeFitter` (`SetLayoutHorizontal/Vertical` called directly), `LayoutRebuilder.ForceRebuildLayoutImmediate` (9 sites).
- Raycasting: `GraphicRaycaster` (`blockingMask`, `blockingObjects`, `ignoreReversedGraphics`).
- Masking: `Mask` (46 refs, `showMaskGraphic`), `RectMask2D` (2 refs).
- Effects: `Outline` and `Shadow` (1 each).

**Layout reliance is heavy.**
- `LayoutElement` (129 refs: min/preferred/flexible, `ignoreLayout`, `layoutPriority`), `GridLayoutGroup` (58), `HorizontalOrVerticalLayoutGroup` (14), `ILayoutElement` (1 implementer).
- `OnRectTransformDimensionsChange` is implemented by 6 game classes and `OnTransformParentChanged` by 3.
- `DrivenRectTransformTracker` is used in firstpass (`KChildFitter`/`KRectStretcher`).
- Layout must run at the right point in the frame: `Canvas.willRenderCanvases` → `CanvasUpdateRegistry`, which the shipped uGUI drives itself.

**EventSystem.**
- `EventSystem.current`, `IsPointerOverGameObject` (gameplay gating: is the cursor over UI), `RaycastAll`, `SetSelectedGameObject`, `currentSelectedGameObject`, `sendNavigationEvents`.
- `ExecuteEvents.Execute/ExecuteHierarchy/GetEventHandler` with handlers cancel, drop, endDrag, initializePotentialDrag, move, pointerClick/Down/Up, scroll, submit and updateSelected (VirtualInputModule).
- `A/InputModuleSwitch.cs` enables `StandaloneInputModule` while the mouse moves and `VirtualInputModule` while a gamepad is active (it also toggles `Cursor.visible`).
- `StandaloneInputModule` reads `Input.mousePosition`, `GetMouseButton*`, `mouseScrollDelta` and `GetButtonDown("Submit"/"Cancel"/axes)` through `BaseInput`. **The legacy Input Manager axes and buttons ("Horizontal", "Vertical", "Submit", "Cancel", "Mouse X/Y", "Mouse ScrollWheel") must exist.**

### 2.4 TextMeshPro

- **Main type: `A/LocText.cs : TextMeshProUGUI`.** It hides `Awake`/`Start`/`OnDestroy` with `private new` methods that call the base ones explicitly. It overrides `text`, `SetText`, `OnEnable` and `GenerateTextMesh`.
- **Text styling.** Styling comes through `TextStyleSetting` ScriptableObjects (`F/TextStyleSetting.cs`, `F/SetTextStyleSetting.cs`: `font`, `fontSize`, `fontStyle`, `enableKerning`, `textWrappingMode`, `extraPadding`). Links are rewritten to `<b><style="KLink"><link="...">`.
- **Rich-text tags in code strings** (counts in `A/STRINGS` and elsewhere): `<b>` 1,870, `<smallcaps>` 431, `<i>` 349, `<color>` 293, `<indent>` 233, `<size>` 231, `<sub>` 114, `<alpha>` 78, `<style>` 57, `<sprite>` 27, `<link>` 27, `<sup>` 8, `<u>` 6, `<font>` 4, `<uppercase>` 2, `<line-height>` 2. Localized `.po` strings in `StreamingAssets/strings` add more.
- **Sprite assets.** `<sprite=N>` uses the default sprite asset. `<sprite="oni_sprite_assets" name="...">` uses a named sprite asset. Steam Input glyphs come from `Resources.LoadAll<Sprite>("Sprite Assets/...")`.
- **Links.** Click handling uses `TMP_TextUtilities.FindIntersectingLink/Character/Word/Line` and `IsIntersectingRectTransform`, plus `textInfo.linkInfo[].GetLinkID()`.
- **Styles.** The `"KLink"` style is serialized. Two styles are added at runtime via Klei-only API: `new TMP_Style(name, open, close)`, `TMP_StyleSheet.instance.AddStyle`, `RefreshStyles` (`A/GlobalAssets.cs:118`).
- **Fonts.** SDF `TMP_FontAsset`s are serialized. Localization swaps fonts with `Resources.FindObjectsOfTypeAll<TMP_FontAsset>()` (`A/Localization.cs:873`). `TMP_FontAsset.CreateFontAsset` appears only in `TMPro.Examples`. `StreamingAssets/fonts/NotoSansUI-Regular.ttf` exists; whether it is used as a dynamic fallback needs checking. TMP itself references 107 `TextCoreFontEngineModule` members (`FontEngine`, glyph and OTL tables), so dynamic atlases or OpenType features would need a FreeType-backed `FontEngine`. `TMP_Text.fontFeatures` is read.
- **Mesh access.** `textInfo.meshInfo[].vertices/colors32/uvs0/uvs2`, `CopyMeshInfoVertexData`, `UpdateVertexData(flags)`, `UpdateGeometry(mesh, i)`, `ForceMeshUpdate`, `characterInfo[]`, `lineInfo[]`, `wordInfo[]`, `TMPro_EventManager.TEXT_CHANGED_EVENT`, `ShaderUtilities.ID_GlowPower`, `fontSharedMaterial`.
- **Klei-only TMP members:** `TextMeshProUGUI.KForceUpdateDirty()` (used by `A/HoverTextDrawer.cs:220`), and the `TMP_StyleSheet` instance API above. **The shipped `Unity.TextMeshPro.dll` is a Klei fork.**
- Also used: `TMP_Dropdown`, `TMP_InputField` (`onValueChanged/onEndEdit/onSubmit/onFocus`, `characterLimit`, `ActivateInputField`), `TextContainer.anchorPosition`, `TextMeshPro` (3D, `transform` only), and `isOverlay`/`isOrthographic`/`isTextObjectScaleStatic`/`isRightToLeftText` setters.
- Legacy `UnityEngine.TextMesh` (3D text) appears only in `LocText`/`TeleType` helper paths and examples. Legacy `UnityEngine.UI.Text` appears in debug, mod and older screens.

---

## 3. Object model and lifecycle

### 3.1 Class counts

- MonoBehaviour-derived game classes: **1,288**, of which 1,095 derive from `KMonoBehaviour` (`F/KMonoBehaviour.cs`) and 68 directly from `KScreen`. The inventory's `derivedCounts` shows 170 classes deriving directly from `MonoBehaviour`.
- ScriptableObject-derived: **37** (34 direct). Examples: `BuildingDef`, `EquipmentDef` (both created with `ScriptableObject.CreateInstance`), `KAnimFile`, `KAnimGroupFile`, `TextStyleSetting`, `SubstanceTable`, `Db` and `GlobalResources`.
- `PropertyAttribute` subclasses: 6. `ISerializationCallbackReceiver` implementers: `Assets`, `MusicManager`, `KAnimControllerBase`, `SubstanceTable`, `TintedSprite`, and the `Tag` and `HashedString` structs. Their `OnAfterDeserialize` runs at load and on clone.

### 3.2 Message methods implemented

Classes per message, approximate; only MonoBehaviour-derived owners are counted:

| Message | Classes | Message | Classes |
|---|---|---|---|
| Update | 141 | OnRenderImage | 12 |
| Awake | 74 | OnApplicationFocus | 7 |
| Start | 59 | OnDrawGizmosSelected | 7 |
| LateUpdate | 40 | OnPostRender | 6 |
| OnEnable | 29 | OnRectTransformDimensionsChange | 6 |
| OnDisable | 27 | Reset (editor) | 6 |
| OnDestroy | 21 | OnApplicationQuit | 4 |
| FixedUpdate | 2 (`KComponentSpawn`, `KComponentCleanUp`) | OnTransformParentChanged | 3 |
| OnPreCull | 2 | OnBecameVisible/Invisible | 2 (ProgressBar, HealthBar) |
| OnGUI | 1 (NodeEditor runtime, debug) | OnValidate / OnDrawGizmos | 1 each |

Plus every `KMonoBehaviour` gets Awake, Start, OnEnable, OnDisable and OnDestroy through the base class. Not used: `OnMouse*`, collision and trigger callbacks, `OnTransformChildrenChanged`, `OnApplicationPause`, `OnLevelWasLoaded`, `OnAnimatorMove`, `OnAudioFilterRead`.

### 3.3 KMonoBehaviour lifecycle (`F/KMonoBehaviour.cs`)

- `public void Awake()` → `InitializeComponent()`:
  - registers a `KObject` (`KObjectManager`);
  - runs `MyAttributes.OnAwake`, the reflection-driven `[MyCmpGet]`/`[MyCmpReq]`/`[MyCmpAdd]` injection (374/385/154 fields), which turns into `GetComponent`/`AddComponent` calls;
  - then calls `OnPrefabInit()`, unless the static flag `isPoolPreInit` is set.
- `private void OnEnable()` → `OnCmpEnable()`. `protected virtual OnDisable()` → `OnCmpDisable()`, unless `isLoadingScene`.
- `public void Start()` → `Spawn()` → `SimAndRenderScheduler.Add` → `MyAttributes.OnStart` → `OnSpawn()`. It is idempotent via `isSpawned`, and `Spawn()` is also called manually.
- `public void OnDestroy()` → `OnForcedCleanUp()`. During a scene load it calls `OnLoadLevel()` instead of `OnCleanUp()`. Otherwise it queues KObject destruction and calls `OnCleanUp()`.
- **Edge cases that rely on Unity's by-name, most-derived message lookup:**
  - `Game` declares `private new void OnDestroy()` (`A/Game.cs`).
  - `ProductInfoScreen` and `ImageToggleState` declare `public new void Awake()`.
  - `NotificationHighlightTarget`, `SplashMessageScreen`, `QuickLayout`, `KleiPermitDioramaVis_JoyResponseBalloon`, `ManagementScreenNotificationOverlay` and `ImageToggleStateThrobber` declare their own `OnEnable`, hiding the private base one.
  - `LocText` hides `TextMeshProUGUI`'s `Awake`, `Start` and `OnDestroy`.

  Unity calls **only the most-derived declaration of each message name, whatever its access level**. The base version runs only if the subclass calls it explicitly.
- `isNull => this == null` and `inputManager.usedMenus[i].Equals(null)` (`A/InputModuleSwitch.cs`) rely on **fake-null semantics**: `UnityEngine.Object.==`, `op_Implicit(bool)` and `Equals` return true or false for destroyed objects as Unity does.

### 3.4 Instantiate, Destroy, SetActive, GetComponent

- **Instantiate.**
  - `Util.KInstantiate` (259 calls) and `GameUtil.KInstantiate` (100) wrap `Object.Instantiate(original, pos, rot[, parent])`. When the prefab has a `RectTransform` and a parent is given, they instantiate without a parent and then call `SetParent(parent, worldPositionStays:true)`. Afterwards they rename the clone to `original.name` (dropping "(Clone)"), assign `KPrefabID.InstanceID`, copy tags and init functions, and optionally `SetLayerRecursively`.
  - `Util.KInstantiateUI` (327) wraps `Instantiate(original, parent, worldPositionStays:false)`.
  - 77 more direct `Instantiate` calls.
  - **Gameplay prefabs are runtime-built, inactive, `DontDestroyOnLoad` GameObjects** (`A/EntityTemplates.cs:70-181`). Clones therefore start inactive, and Awake runs on the clone's first activation.
  - Clone semantics required: only serialized fields are copied (public or `[SerializeField]`, not `[NonSerialized]`; there are 2,146 `[SerializeField]`, 149 `[NonSerialized]` and 286 `[Serializable]` types). References to objects inside the cloned hierarchy are remapped to the clones, and external references are kept. `ISerializationCallbackReceiver` hooks fire.
  - `Util.PreInit` (`F/Util.cs:283`) calls `InitializeComponent()` on prefab components while `isPoolPreInit` is set.
- **Destroy.** `Object.Destroy` is called about 208 times directly and about 223 times via `Util.KDestroyGameObject`/`DeleteObject` (80). Destruction is deferred to the end of the frame. `DestroyImmediate` is called 110 times, including in runtime UI code, and must be synchronous, including the child `OnDestroy`/`OnDisable` calls.
- **SetActive:** about 921 `SetActive(true)`, 813 `SetActive(false)` and 532 non-literal calls. `activeSelf` is used 145 times and `activeInHierarchy` 72 times. Awake, OnEnable and Start ordering on activation of inactive hierarchies matters: Awake is deferred until the object is first active, and Start runs before the first Update of that object.
- **GetComponent<T>:** 10,465 calls; `GetComponentInChildren` 357, `GetComponentsInChildren` 128 (including the `List<T>` and `includeInactive` overloads), `GetComponentInParent` 32, `TryGetComponent` 58, `AddComponent` 1,174, `GetComponent(Type)` 4, `GetComponent(string)` in inventory. Lookup must be fast and must match by inheritance and interfaces.
- **Transform:** `SetParent(t[, worldPositionStays])` 143, `Find(name)`/`transform.Find` 146, `GetChild`, `childCount`, `SetSiblingIndex`/`SetAsFirst`/`SetAsLastSibling` (UI draw order), `hasChanged` get/set, and the `TransformPoint`/`InverseTransformPoint` family. Klei adds `TransformExtensions.SetPosition/SetLocalPosition/GetPosition`, which are cached wrappers in `F/TransformExtensions.cs`.
- **Finding objects:** `GameObject.Find` 19; `FindObjectsByType`/`FindFirstObjectByType`/`Resources.FindObjectsOfTypeAll` 28 in total. `FindObjectsOfTypeAll` is used for `Assets`, `TMP_FontAsset`, `LocText`, `TextStyleSetting` and `MeshRenderer` (`Game.cs` turns off reflection probes), and also by mods.
- **SendMessage:** only `F/ButtonLock.cs` (4 calls, `DontRequireReceiver`). `Invoke`/`InvokeRepeating` are not used. `CompareTag` is used by FMOD; `tag` get/set is used for the camera "MainCamera" logic.
- **`DontDestroyOnLoad`:** 24 sites (Global, App, entity templates, FMOD).

### 3.5 Coroutines

- `StartCoroutine(IEnumerator)`: about 141 calls in 98 files. `StartCoroutine(string)`: 1. `StopCoroutine(Coroutine)`: 36. `StopAllCoroutines`: 6.
- Yield types:
  - `null`: 91.
  - `SequenceUtil.WaitForSecondsRealtime(x)`: 71. This returns **a cached, shared `WaitForSecondsRealtime` instance per duration**, so the managed `CustomYieldInstruction` logic must match Unity's exactly, including how it resets on reuse and how concurrent users interfere.
  - `WaitForSeconds`: 21 new plus a cached variant.
  - `WaitForEndOfFrame`: 1 plus cached. Used by Timelapser and screenshot flows (`ReadPixels` after rendering).
  - `WaitForFixedUpdate`: 3.
  - `WaitUntil`: 15.
  - Nested `IEnumerator`s (`Updater.*`, `this.Interpolate(...)`, `PresUtil.*`), which must run as nested coroutines that start in the same frame.
  - `yield return StartCoroutine(...)`: 2.
  - `UnityWebRequest.SendWebRequest()` async operation (MOTD).
- Klei also runs its own updater sequences (`Updater`, `UIScheduler`, `GameScheduler`, `SimAndRenderScheduler`) from `Update`/`LateUpdate`. These are not Unity coroutines.

### 3.6 Assets, Resources and scenes

- **Scenes:** `level0` (boot, `InitializeCheck`), "frontend", "backend". `F/App.cs`'s `App.LoadScene(name)` only sets a flag. The actual `SceneManager.LoadScene(name)` (synchronous, single mode) runs in `App.LateUpdate`, after `KObjectManager.Cleanup`, `Resources.UnloadUnusedAssets()` and `GC.Collect()`, wrapped by `OnPreLoadScene`/`OnPostLoadScene`. Unity finishes a synchronous `LoadScene` at the start of the next frame: old objects get `OnDestroy` with `KMonoBehaviour.isLoadingScene` set, and the new scene's Awake calls follow. `OnPostLoadScene` runs *before* that happens. Also used: `SceneManager.GetActiveScene().name/rootCount/GetRootGameObjects`.
- **Resources.Load paths:** `"Db"`, `"GlobalResources"`, `"BuildingCellVisualizerResources"`, `"StateMachineDebuggerSettings"`, `"AudioMixerSnapshots"`, `"GameAudioSheets"`, `"ProgressBarsConfig"`, `"Audio"`, `"animgrouptags"` (`KAnimGroupFile`), `"1x1_white"` (Sprite), `"video_webm/<name>"` (VideoClip), `typeof(T).Name` via `F/SingletonResource.cs`, and `LoadAll<Sprite>("Sprite Assets/...")`. `GetBuiltinResource<Mesh>("Quad.fbx")` is also used.
- **AssetBundles:**
  - `A/BundledAssetsLoader.cs` uses `AssetBundle.LoadFromFile(StreamingAssets/<dlc bundle>)` + `LoadAsset<GameObject>("Expansion1Assets")` and per-DLC `"<dir>Assets"`. This is critical: DLC content prefabs come from here.
  - `F/StreamedTextures.cs` uses `LoadFromFileAsync` for the hi-res texture bundle (`hires_base_bundle`), plus `Unload(true)`.
- **`RuntimeInitializeOnLoadMethod`:** `KleiItemsStatusRefresher.Initialize` (AfterSceneLoad) and `Klei.KProfilerPlugin.InitModule` (SubsystemRegistration). See `OxygenNotIncluded_Data/RuntimeInitializeOnLoads.json`.
- **Script execution order:** no `[DefaultExecutionOrder]` attributes. Any custom order is in `globalgamemanagers` (MonoManager) and must be read from there.

---

## 4. Input

- **Unity Input members used** (call sites):
  - `Input.mousePosition` 31, `GetKey` 29, `GetKeyDown` 16, `GetAxis` 11 (`"Mouse X"`, `"Mouse Y"`, `"Mouse ScrollWheel"`), `GetButtonDown` 8 (VirtualInputModule: Submit, Cancel, Horizontal, Vertical), `GetMouseButton(Down/Up)` 10, `GetKeyUp` 4, `GetAxisRaw` 4, `mouseScrollDelta` 2, `GetTouch`/`touchCount`/`simulateMouseWithTouches` (unused paths), `mousePresent`, `ResetInputAxes`.
  - Also `Event.current` and IMGUI events (NodeEditor only), `Display.RelativeMouseAt`, `Cursor.visible`, `Cursor.SetCursor(Texture2D, hotspot, mode)` (`A/InterfaceTool.cs`: tool cursors), and `GUIUtility.systemCopyBuffer` (clipboard).
- **KInputManager / KInputController** (`F/KInputManager.cs`, `F/KInputController.cs`, `F/KKeyCode.cs`):
  - `GameInputManager.Update()` is called from `Global.Update` (`A/Global.cs:495`).
  - Bindings map `(KKeyCode, Modifier)` → `Action` (280 actions). `KKeyCode` values below `KleiKeys` are **cast directly to `UnityEngine.KeyCode`**, so the numeric values must match Unity's `KeyCode` enum. Values above that are Klei virtual keys: MouseScrollUp/Down, derived from `GetAxis("Mouse ScrollWheel")`.
  - Each frame it polls `GetKeyDown`/`GetKeyUp`/`GetKey` per bound key and modifier keys (Alt, Ctrl, Shift, CapsLock, BackQuote), then dispatches `KButtonEvent`s through `KScreenManager` (top screen first) and then tools.
  - `GetMousePos()` = `Input.mousePosition` (or the virtual cursor position).
  - About 131 distinct `KeyCode` names appear in code.
- **Gamepad** goes through Steam Input (`F/SteamInputInterpreter.cs`), not Unity Input.
- **UI pointer input** goes through `StandaloneInputModule`/`BaseInput` (section 2.3).
- **Required Unity semantics:** `GetKeyDown`/`GetKeyUp` are true for exactly one frame; mouse position is in pixels with the origin at the bottom-left; `GetAxis("Mouse ScrollWheel")` is smoothed with the Input Manager's default sensitivity; and `ResetInputAxes` clears all of it.

## 5. Time, Application, Screen, QualitySettings, PlayerPrefs, SystemInfo, Debug, Random, Mathf

- **Time:** `unscaledDeltaTime` 110, `time` 101, `unscaledTime` 54, `deltaTime` 47, `realtimeSinceStartup` 35, `timeScale` get/set 24 (pause = 0; speed is controlled through `SpeedControlScreen`), `timeSinceLevelLoad` 11, `frameCount` 8 (KAnim dirty tracking and TexturePagePool double-buffering), `renderedFrameCount` 1, `fixedDeltaTime` 1. Code relies on `deltaTime == 0` while paused (`TextureLerper` falls back to `unscaledDeltaTime`).
- **Application:**
  - Used: `streamingAssetsPath` 23, `isPlaying` 19, `isEditor` 11, `dataPath` 9, `platform` 8, `targetFrameRate` (set) 5, `OpenURL` 3, `logMessageReceived` += / -= (crash reporter), `systemLanguage`, `persistentDataPath`, `consoleLogPath`, `Quit(int)`.
  - `OnApplicationFocus` matters: App throttles with `Thread.Sleep` when unfocused.
  - `Debug.unityLogger.logHandler` is replaced by `LogCatcher` (`A/Game.cs:782`).
- **Screen:** `height` 59, `width` 50, `currentResolution` 10, `resolutions` 5, `fullScreen` 5, `fullScreenMode` get/set 4, `dpi` 2, `SetResolution(w,h,FullScreenMode,RefreshRate)` 2 (`A/GraphicsOptionsScreen.cs`). `Resolution.refreshRateRatio`/`RefreshRate` are used. Screen-size RTs depend on `Screen.width/height` updating after resize, and `ScreenResize.Instance.OnResize` is Klei's own polling.
- **QualitySettings:** `GetQualityLevel`/`SetQualityLevel(int,bool)`, `vSyncCount` get/set, `activeColorSpace` (LUT pass selection).
- **PlayerPrefs:** not used. Klei has its own `F/KPlayerPrefs.cs`, which is file-based.
- **SystemInfo:** gameplay reads `maxTextureSize`, `SupportsTextureFormat` and `SupportsRenderTextureFormat`. About 25 other members appear only in `KCrashReporter`/metrics (vendor, device, memory, …). They can be descriptive stubs.
- **Debug:** the game's own global `Debug` class (`F/Debug.cs`) shadows `UnityEngine.Debug` and forwards to `UnityEngine.Debug.Log*`, `LogException`, `isDebugBuild`, `developerConsoleVisible`, `DrawLine`/`DrawRay`. `Gizmos.*` (114 refs) is editor-only and can be a no-op.
- **UnityEngine.Random:** `Range(int,int)`/`Range(float,float)` (about 318 refs, partly Klei's own random classes), `value` 56, `insideUnitCircle` 2. Determinism is not save-critical, because the Sim and worldgen use Klei's own `KRandom`/`SeededRandom`. The int `Range` must exclude max, and the float `Range` must include it.
- **Mathf:** about 40 members (inventory). `Mathf.Approximately`, `Repeat`, `PingPong`, `SmoothStep`, `RoundToInt` (banker's rounding, like `Math.Round`), `Clamp`, `Lerp(Unclamped)`, `Log(f,b)`, `IsPowerOfTwo`. Must be bit-exact with Unity's float implementations where saves or UI depend on them.
- **Other:** `AnimationCurve` (`Evaluate`, `keys`, `pre/postWrapMode`; used by UI tweening), `Gradient`, `Color`/`Color32`/`ColorUtility.ToHtmlStringRGB`, `Pool.ObjectPool<T>`/`CollectionPool`, `Unity.Collections.NativeArray<T>` (+ `NativeArrayUnsafeUtility.GetUnsafePtr`/`ReadOnlyPtr`), `ProfilerRecorder` (memory, PerformanceCaptureMonitor), `Scripting.GarbageCollector.GCMode` (`KProfilerBegin`).

## 6. Other modules: purpose and criticality

| Module | Used for | Critical path? |
|---|---|---|
| **AudioModule** | — (no references). All audio is FMOD via `FMODUnity` + `F/KFMOD.cs`/`Audio.cs`. FMOD needs only `Transform`, `Rigidbody(2D).linearVelocity`, `TextAsset.GetData` and `Application` paths. | n/a |
| **VideoModule** | `A/VideoScreen.cs` (intro, DLC intros, victory cinematics) and `A/VideoWidget.cs` (codex videos). `VideoPlayer.clip/targetTexture/Play/Pause/Stop/time/isLooping/loopPointReached/audioOutputMode/timeReference/externalReferenceTime`. Clips come from `Resources.Load<VideoClip>("video_webm/...")` (WebM). Audio is synced externally (FMOD "Videos.bank"; `audioOutputMode` is likely None). | Peripheral, but the first-run intro plays before the main menu; it must be skippable or stubbable |
| **AnimationModule** | `Animator.Play(string[,layer])`, `SetBool`, `GetCurrentAnimatorStateInfo(0).IsTag`, `isInitialized`. Used on UI toggles and menus (`BuildMenuCategoriesScreen`, `KToggle`/`KToggleArtExtensions`, `NotificationAnimator`, `SkillsScreen`, …). Controllers are serialized `AnimatorController` assets animating UI properties. | Visible UI polish; the build menu depends on it. A minimal Animator that can play state clips curve-wise is needed for fidelity |
| **ParticleSystemModule** | `ParticleSystem.Stop/IsAlive/emission.enabled/main.stopAction/shape.position/scale`. Missile smoke trails (DLC), pollination VFX, meteor FX (`DestroyAfter`). Systems are fully configured in prefabs. | Gameplay-visible but not essential; a partial simulation is acceptable at first |
| **Timeline / Director / Playables** | Only FMODUnity's `FMODEventTrack`/`FMODEventPlayable` types. The game code does not use them. | No |
| **PhysicsModule** | `Physics.Raycast(ray, out hit, ∞, BlockSelectionLayerMask)` in `A/PlayerController.GetCursorPos` (called on every tool hover). It falls back to a plane projection when nothing is hit. `Physics.gravity` is read by `FallingWater` (Vector2 gravity). There are no 3D colliders in code. | Raycast is called each frame. Returning "no hit" is correct unless scene data contains colliders on `BlockSelection` (to verify). `gravity` must be (0,-9.81,0) |
| **Physics2DModule** | `BoxCollider2D.size` (`MinionSelectPreviewConfig`) and `Rigidbody2D` for FMOD. The game uses its own `KBoxCollider2D`/`KCircleCollider2D`. | No (an inert component is enough) |
| **IMGUI** | NodeEditorFramework runtime editor (`F/NodeEditorFramework.*`, debug), FMOD debug overlay (`GUI.Window`), `TextEditor`/`GUIUtility.systemCopyBuffer` for clipboard copy (`A/CopyTextFieldToClipboard.cs`, `A/ReportErrorDialog.cs`). Dev tools use Dear ImGui (`ImGui.NET`), not IMGUI. | Only the clipboard is needed |
| **UnityWebRequest** | `A/MotdDataFetchRequest.cs` (main-menu news, `Get`, `downloadHandler.data/text`, `result`, `timeout`) and `A/KCrashReporter.cs` (`Post` multipart upload). | Main menu, but failure-tolerant. Can be backed by `HttpClient` |
| **ImageConversion** | `LoadImage`: colony retirement screenshots (`RetireColonyUtility`, `Slideshow`), mod preview images (`KMod/Mod.cs`, `ModUtil`), MOTD images, Steam UGC previews, language images. `EncodeToPNG`: timelapse (`A/Timelapser.cs`, every cycle in gameplay) and the MOTD cache. | Gameplay: timelapse capture runs at cycle boundaries (`Camera.Render` + `RenderTexture.active` + `ReadPixels` + `EncodeToPNG`) |
| **ScreenCaptureModule** | `ScreenCapture.CaptureScreenshot(path, superSize)` in `A/DebugHandler.cs` (debug hotkeys). | No |
| **JSONSerializeModule** | `JsonUtility.ToJson` in `A/PerformanceCaptureMonitor.cs`. | No |
| **TextRenderingModule** | `Font.material`, `TextMesh` (rare), the `TextAnchor`/`FontStyle`/wrap enums, `UIVertex`. The legacy `UI.Text` depends on it (`Font` glyph rasterization). | Legacy Text is in debug and mod screens only; low |
| **UIModule** | `Canvas`, `CanvasGroup`, `CanvasRenderer`, `RectTransformUtility` (sections 2 and 1.5). | Yes |
| **AssetBundleModule** | DLC content bundles and the hi-res texture bundle (section 3.6). | **Yes** (DLC prefabs) |

## 7. Klei engine-side libraries in Assembly-CSharp-firstpass

Only **236 of the 2,447** firstpass files `using UnityEngine`, against 3,223 of 4,409 in Assembly-CSharp. Much of firstpass is pure .NET: YamlDotNet, ProcGen, Delaunay, Satsuma, Epic Online Services, rail, KSerialization, FuzzySharp, ClipperLib and others. The Unity-touching core:

- **`KMonoBehaviour`, `KObject`, `KObjectManager`, `MyCmp*` attributes, `KComponents`/`KGameObjectComponentManager`, `KComponentSpawn`/`KComponentCleanUp`.** The lifecycle wrapper (section 3.3) depends on Awake, Start, OnEnable, OnDisable and OnDestroy semantics, on `GetComponent`/`AddComponent`, and on `FixedUpdate` + `Update` both calling `comps.Spawn()`. `FixedUpdate` must therefore run 0..n times per frame, before `Update`, as in Unity.
- **`Util`, `TransformExtensions`, `KPrefabID`, `GameObjectPool`.** These handle Instantiate/SetParent/name/layer recursion and pooled `SetActive` cycling.
- **`App`, `Global` (A), `GenericGameSettings`, `KPlayerPrefs`, `DlcManager`.** These cover scene loading (section 3.6), the focus sleep loop, `Application.Quit`, and `targetFrameRate`/`vSyncCount` through `GraphicsOptionsScreen`.
- **KAnim stack.** `KAnimFile` (a ScriptableObject holding `TextAsset` anim/build bytes plus `Texture2D` atlases), `KGlobalAnimParser`, `KAnimGroupFile` (`Resources.Load("animgrouptags")`), `KAnimBatchManager`/`Group`/`Batch`/`BatchSet`/`KBatchGroupData`, `KAnimConverter`, `KBatchedAnimCanvasRenderer`, `KBatchedAnimInstanceData`, `SymbolInstanceGpuData` and `SymbolOverrideInfoGpuData` (all NativeArray writers), and `StreamedTextures`/`TextureBundle`. The controllers themselves (`KBatchedAnimController`, `KAnimControllerBase`, `KBatchedAnimUpdater`) live in A. This is the heaviest renderer dependency (section 1.5).
- **Texture infrastructure:** `TextureBuffer`, `TexturePage`, `TexturePagePool`, `TextureRegion`, `TextureUtil` (format maps), `TextureAtlas`, `RenderTextureDestroyer`/`Extensions`, `MeshCreator`, `DrawUtil`, `RenderUtil`.
- **Screens and input:** `KScreen`, `KScreenManager` (screen stack; input is routed by sort key, not by Unity), `GameScreenManager` (canvases), `KInputManager`, `KInputController`, `KInputHandler`, `GameInputMapping`, `SteamInputInterpreter`, `ToolTip`/`ToolTipScreen`, `KButton`, `KToggle`, `KImage`, `KSlider`, `KScrollRect`, `KInputTextField`, `KLayoutElement`, `KChildFitter`, `KRectStretcher` (`DrivenRectTransformTracker`), `HierarchyReferences` (A), `TextStyleSetting`, `SetTextStyleSetting`, `EventSystem` (a Klei class of that name, distinct from `UnityEngine.EventSystems.EventSystem`).
- **Grid and Sim interface.** These live in A: `A/Grid.cs`, `A/Sim.cs` (32 `DllImport("SimDLL")`), `A/ConduitTemperatureManager.cs` and `A/World.cs`. The native Sim hands back raw pointers that become textures (`PropertyTextures.external*Tex` via `LoadRawTextureData(IntPtr)`) and cell arrays. The only Unity contact is the texture upload and `Vector2/3` math. `F/Klei/KProfilerPlugin.cs` uses a third DllImport set.
- **Profiling and debug:** `KProfiler*` (`ProfilerRecorder`, `GarbageCollector.GCMode`), `PerformanceMonitor`, `FPSCounter`, `DebugUtil`, `Debug`. All are peripheral.
- **`UnityStandardAssets.ImageEffects`** (`PostEffectsBase`, `ColorCorrectionLookup`): uses `GL.QUADS`, `Texture3D` LUTs and `Graphics.Blit` with a pass index.
- **`UnityEngine.UI.Extensions`** (`UILineRenderer`, `UIPrimitiveBase`): custom `MaskableGraphic`s using `OnPopulateMesh`/`VertexHelper`/`UIVertex`.

## 8. UnityEngine.UI and Unity.TextMeshPro: how much is used?

**What the game references** (inventory):

| Package | Types referenced / total | Members referenced | Subclassed types |
|---|---|---|---|
| UnityEngine.UI | 83 types (about 58 by name in source) of 119 source files | 364 | `Image`, `Toggle`, `Slider`, `ScrollRect`, `LayoutElement`, `AspectRatioFitter`, `UIBehaviour`, `Graphic`, `MaskableGraphic`, `PointerInputModule` (10 bases, 18 game subclasses) |
| Unity.TextMeshPro | 34 types (about 20 by name in source) of 130 source files | 152 | `TextMeshProUGUI` (LocText), `TMP_InputField` (KInputTextField), `TMP_InputValidator` (2) |

**What the packages need from Unity** (generated with `tools/Inventory` against the two package DLLs; scratch output in `/tmp/claude-1000/usage-research/pkg_unity.json`):

| Consumer | CoreModule | UIModule | TextRendering | TextCoreFontEngine | InputLegacy | IMGUI | UIElements | Physics/2D | Animation |
|---|---|---|---|---|---|---|---|---|---|
| UnityEngine.UI.dll | 401 | 66 | 52 | — | 25 | 11 | 84 | 4 / 4 | 3 |
| Unity.TextMeshPro.dll | 325 | 36 | 5 | 107 | 3 | 9 | — | — | — |

Notes on the dependencies:
- uGUI's 84 `UIElementsModule` members (`PanelRaycaster`/`PanelEventHandler` for UI Toolkit) can be inert stubs, because the game has no `UIDocument`s.
- uGUI's Physics/Physics2D raycasters are not used by game canvases; they can be stubbed to "no hit".
- TMP's 107 TextCore members are the font-engine and OpenType-table surface. With static SDF atlases most are data structures (`Glyph`, `GlyphRect`, `FaceInfo`, adjustment records) plus a few `FontEngine` calls that only run for dynamic atlases.

**Recommendation.** Do **not** reimplement either package. Run the shipped IL unmodified:
- Both are ordinary managed assemblies that touch the engine only through public `UnityEngine.*` APIs: Canvas/CanvasRenderer/RectTransform, Graphic rebuild via `Canvas.willRenderCanvases`, Material/Shader/Mesh, Input and TextCore data types.
- The game depends on their **exact protected/virtual surface and internal behaviour**: layout rebuild order, Selectable state transitions, ScrollRect elasticity, `PointerInputModule` internals used by `VirtualInputModule`, and TMP's text layout and mesh layout read back by game code.
- The TMP build is a Klei fork (`KForceUpdateDirty`, the `TMP_StyleSheet` instance API).

Reimplementing even "the used 30%" would mean re-deriving those behaviours. Implementing the roughly 1,000 Unity members those two DLLs need instead adds mostly CoreModule surface we need anyway, plus `Canvas`/`CanvasRenderer` (the real work: canvas batching, clipping/`RectMask2D` soft-clip rects, stencil masks), `RectTransform` (driven properties, `DrivenRectTransformTracker`) and a small `FontEngine`. The same applies to `FMODUnity.dll` and its Timeline/Playables types.

---

## 9. Open questions (need scene, asset or runtime data)

1. Exact component lists and their order on the scene's main camera (the post-FX order), its base depth, `orthographic` flag, clear colour and `nearClipPlane`/`farClipPlane`. Read these from `level2` (backend) and `level1` (frontend).
2. The layer index table (TagManager) and any custom script execution order (MonoManager) in `globalgamemanagers`.
3. Whether Unity re-binds the camera target after the `BeforeForwardOpaque` command buffer that calls `SetRenderTarget(MRT)`. Section 1.3 step 6 depends on it. Test in real Unity or with RenderDoc on the shipped build.
4. The shader blob format in this Linux build (GL core / Vulkan / SPIR-V variants) for the ~30 Klei shaders and the TMP/UI shaders. Also the per-shader render state (blend, ZWrite, stencil) that material ints such as `_SrcAlpha`/`_ZWrite`/`_Stencil*` drive.
5. Whether any prefab carries 3D colliders on `BlockSelection` (for `PlayerController.GetCursorPos`), and which prefabs carry Animators and ParticleSystems.
6. Whether TMP falls back to dynamic font atlases (`NotoSansUI-Regular.ttf`) for any shipped language.
