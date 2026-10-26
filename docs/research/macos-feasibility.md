# macOS support: feasibility and what it would take

**Scope.** Can Moono (our .NET 10 player with its own OpenGL 4.5 renderer) run Oxygen Not
Included on macOS, and what would a port cost? This is research, not implementation. The
document answers six questions:

1. what shader programs the Mac data carries
2. how GL-specific the engine is above the device
3. .NET and Objective-C/Metal interop on macOS
4. windowing and input
5. host and platform pieces
6. how much can be tested without a Mac

It ends with a phased plan, the risks and a recommendation.

**Inputs.**
- The macOS depot (Steam depot 457142) at
  `/mnt/c/Program Files (x86)/Steam/steamapps/content/app_457140/depot_457142/OxygenNotIncluded.app`,
  Unity 6000.3.5f2 (`Contents/Info.plist`), the same game build as our Linux install
  (`~/projects/oni-linux`, both carry `KleiVersion.ChangeList` 744825).
- Our asset reader (`src/OniMoono.Assets`) on that data, through a new survey tool,
  `tools/ShaderSurvey` (committed with this document).
- Mach-O headers read with a small header parser; file listings; md5 of every data file.
- The Klei decomp (`out/local-tools/decomp`, "AC" = Assembly-CSharp, "FP" = firstpass), read only.
- Web sources for the platform facts, linked where used and dated 2026-10.

**Clean room.** No Unity C# or native code was read or disassembled. Everything about Unity's
data formats below was inferred from the shipped data. Where a fact rests only on Unity's
documented behaviour or on community reports, it is marked **(to verify on a Mac)**.

**Scratch.** Throwaway scripts and their outputs live in
`/tmp/claude-1000/-home-nigit-projects-oni-brutal/e03f53aa-206b-41be-b050-37042ea437c7/scratchpad/macos/`:

| File | What it is |
|---|---|
| `mac-variants.tsv`, `linux-variants.tsv` | `ShaderSurvey variants` on both builds |
| `scripts/cmpvar.py`, `scripts/cov.py` | Comparisons of those two files |
| `all-metal/`, `all-gl/` | Every Metal and GLCore blob, from `ShaderSurvey dump '*'` |
| `scripts/feat.py` | Feature census over those blobs |
| `mac-tex.tsv`, `linux-tex.tsv` | Texture2D formats in both builds |
| `md5-*.txt` | Whole-data-folder hashes |
| `adiff-*.txt` | `tools/AssemblyDiff` between the Linux and Mac game assemblies |
| `pub-osx-x64/` | An osx-x64 publish of the host |

---

## 0. Summary

| Question | Answer |
|---|---|
| Metal shaders in the Mac data? | **Yes, MSL source, not metallib.** All 120 shaders carry platform 14 (Metal): 522 vertex and 535 fragment programs, 945 distinct MSL texts, 6.5 MB. **All 120 also still carry OpenGLCore (platform 15) GLSL**, and the Mac set is a byte-identical superset of the Linux set (583 shared programs plus 13 extra variants). |
| Reflection usable? | Yes. Each Metal subprogram has a parameter blob (constant-buffer layouts, texture, sampler and buffer slots). `ShaderSurvey check-metal` matched every binding against the `[[texture(n)]]`/`[[sampler(n)]]`/`[[buffer(n)]]` the MSL declares: **1057 of 1057 consistent.** |
| Other platform-specific data? | Practically none. The texture formats (DXT1/5, BC7, RGBA32 and others) are identical, the `.resS` payloads of three of four files are byte-identical, and fonts, StreamingAssets and audio banks match. Only the serialized files' platform tag, shader blobs and sprite-atlas names differ. The game assemblies differ by compile defines: Mac has no ImGui dev tools, and the EOS library name differs. |
| GL-specific above the device? | Little. The seam is one abstract class with 17 overridable members. The GL ties above it are the loader picking platform 15 and GL clip-space and origin conventions. Much backend-neutral logic currently lives inside `Gl/` (variant selection, uniform resolution, render state) and would be hoisted out before a second backend. |
| .NET on macOS | .NET 10 supports osx-x64 and osx-arm64 (macOS 15, 26 and 27), x64 under Rosetta included. Our host cross-publishes for both. **BRUTAL ships osx-arm64 natives only** (no osx-x64), which suits the arm64 build; the x86_64 fallback needs x86_64 builds of glfw, freetype/png/zlib and stb. |
| Interop | Raw `objc_msgSend` through function pointers is enough. SharpMetal (MIT, low activity) and ppy's Veldrid.MetalBindings fork (MIT) exist. Silk.NET has no Metal binding. |
| Windowing | GLFW 3.5 on Cocoa with `NO_API` and a CAMetalLayer on `glfwGetCocoaView`. Brutal.Glfw already exposes `GetCocoaWindow`/`GetCocoaView`. The .NET `Main` thread is the Cocoa main thread. |
| Architecture | **Primary: native arm64, with onimaxxing-sim in place of Klei's SimDLL** (§3.4). Klei's Mac SimDLL is x86_64 only. onimaxxing-sim is a Rust port of SimDLL with the same C ABI and is bit-exact with vanilla, as verified upstream against its session corpus. Every other native the arm64 process loads ships an arm64 build: the game's FMOD and Steam bundles are universal, and BRUTAL's dylibs are osx-arm64. **Fallback: x86_64 under Rosetta 2 with Klei's SimDLL.** Apple keeps Rosetta general-purpose only "through macOS 27", and afterwards only "a subset … aimed at … older unmaintained gaming titles". |
| Testable on Linux | Cross-build, the shader survey and reflection checks, and the backend-neutral refactors. No MSL compiler runs on Linux. |
| Effort | About **5 to 7 engineer-months** to a playable Metal build: platform port 3–4 weeks, Metal backend 10–14 weeks, shader loading 3–5 weeks, testing set-up 2–3 weeks, plus about 3 weeks of Linux-side preparation. |
| Recommendation | Feasible. Do the Linux-side preparation first, since it pays off with no Mac. Commit to the rest only with Mac hardware available. Target arm64 with onimaxxing-sim, which takes the Rosetta horizon off the critical path, and keep Klei's x86_64 SimDLL under Rosetta as the fallback. The arm64 build then depends on the port staying bit-exact through game updates (R10). Details in §9. |

---

## 1. Shaders in the Mac data

### 1.1 What is there

`ShaderSurvey summary` over the nine data files (`level0-2`, `resources.assets`,
`sharedassets0-2.assets`, `Resources/unity default resources`, `Resources/unity_builtin_extra`):

| | Mac depot | Linux install |
|---|---|---|
| Shaders (class 48) | 120 | 119 (Mac adds `Hidden/VideoDecodeOSX`) |
| Shaders with platform 15 (GLCore) | 120 | 119 |
| Shaders with platform 14 (Metal) | **120** | 6 (Unity built-ins only, partly unusable, see below) |
| Other platforms | none | 4, 9, 18, 26 on the same 6 built-ins |
| GLCore subprograms (GPU program types 6/7/8) | 529 / 1 / 66 | 516 / 1 / 66 |
| Metal subprograms (23 = vertex, 24 = fragment) | 522 / 535 | (58 / 66, built-ins) |

The Mac build was made with both Metal and OpenGLCore enabled. Each serialized file's
`TargetPlatform` is 2 (StandaloneOSX) against 24 (StandaloneLinux64) on Linux.

**GLCore on the Mac is a superset of Linux.** Every one of Linux's 583 distinct GLCore programs
(keyed by shader, pass, keywords and SHA-1 of the program bytes) is present on the Mac,
byte-identical. The Mac adds 13 programs:
- `Hidden/VideoDecodeOSX` (4)
- `DISABLE_K_LIGHTING` variants of five `Klei/BatchedAnimation*` shaders
- `UNITY_UI_ALPHACLIP` of `Klei/BatchedAnimationUI`
- `DISABLE_TEMPERATURE_PARTICLES` of `Klei/PostFX/LightBufferCompositor`

The last is the only one game code toggles (`AC/LightBufferCompositor.cs:50,54`). Linux and the
Metal set both lack it, so a Metal player renders that pass as our Linux player does today.

So our existing loader (`ShaderPrograms.Load`, `src/OniMoono.UnityEngine/Engine/Rendering/ShaderPrograms.cs:52`)
reads the Mac data unchanged. That makes a GL path on the Mac possible as far as the data goes;
§2.4 covers why it is not the target.

**Metal programs are MSL source.** Each Metal program blob has:
- the same 24-byte entry header as GLCore (magic `0x0C0A75BA`, GPU program type, stats);
- a keyword list;
- a length-prefixed payload: a small binary header (magic `0xF00DCAFE`), the entry-point name
  `xlatMtlMain`, then plain HLSLcc-generated MSL (`#include <metal_stdlib>` …);
- after the payload, the vertex channel mask and an attribute table.

Nothing is compiled AIR or metallib. A Metal device compiles source at run time
(`newLibraryWithSource`), as Unity's own Metal device does.

Example, `Klei/PostFX/FullScreen` fragment: `texture2d<float> _MainTex [[ texture(0) ]]`,
`sampler sampler_MainTex [[ sampler (0) ]]`, and an output `SV_Target0 [[ color(rp_output_remap_0) ]]`.

### 1.2 Feature census of the 945 MSL programs

`scripts/feat.py all-metal`:

| Feature | Programs | Note |
|---|---|---|
| `function_constant` | 532 | Only two constants: `rp_output_remap_mask` (index 1, 484 programs, colour-attachment remap) and `has_base_vertex_instance` (index 4, 48 instanced programs). The device must specialise every function with `MTLFunctionConstantValues`, e.g. an identity remap. |
| Constant buffers `VGlobals`/`FGlobals` | 752 | Per-stage loose uniforms packed into one buffer. GLCore uses plain `uniform`s for the same values. |
| `UnityPerDraw` / `UnityPerFrame` / `UnityPerCamera` | 433 / 483 / 402 | Unity's built-in cbuffers, as `constant T& [[ buffer(n) ]]`. |
| `texture2d_array` / `texturecube` / `texture3d` / `depth2d` | 51 / 97 / 2 / 25 | `depth2d` with `sample_compare` (31) is shadow sampling, in the Standard / Legacy shaders only. |
| `.read(` (texel fetch) | 79 | `Klei/BatchedAnimation*` instance data. |
| `const device` buffers | 43 | `unity_ParticleInstanceData` (procedural-instancing particles) and Physics2D SDF `element_buffer`. |
| `render_target_array_index` | 6 | Unity built-ins (clear, error, VR blit). |
| `invariant` position | 460 | Needs MSL 2.1+ (macOS 10.14+); fine for macOS 12+. |

For comparison, the GLCore set: `#version 150` 856 stages, 330 104, 410 3 and **420 132**. The
420 stages are the 66 GLCore43 programs (`GL_ARB_shader_storage_buffer_object`), all
procedural-instancing particle variants.

### 1.3 Variants and keywords against GL

From `scripts/cmpvar.py` and `scripts/cov.py`:
- **GLCore programs hold both stages, keyed by the union of keywords. Metal keys each stage by
  the keywords that stage uses.** Of Linux's 583 GL keyword sets, 92 have no Metal vertex *and*
  fragment program with exactly the same set. Every one of them is a stage that ignores a
  keyword: `VERTEXLIGHT_ON` is vertex-only, `_ALPHABLEND_ON` fragment-only. No Metal keyword
  set lacks a GL counterpart.
- So **variant selection must run per stage**, with Unity's best-match rule, which
  `ShaderPrograms.SelectVariant` (`ShaderPrograms.cs:195`) already implements over a variant
  list. A Metal pipeline is (vertex function, fragment function, function constants, vertex
  descriptor, attachment formats, blend state).
- `Hidden/VR/BlitFromTex2DToTexArraySlice` subshader 1 is GL-only (a geometry-shader path). The
  Metal subshader 0 exists.
- The player program lists (`m_PlayerSubPrograms`) have four tiers per stage, and only the last
  is populated on both builds, so tier choice is not a question.

### 1.4 Uniforms and bindings: the parameter blobs

GLCore variants are bound through **GL reflection** today:
- `GlPrograms.Reflect`, `src/OniMoono.Desktop/Gl/GlPrograms.cs:214-253`: `glGetActiveUniform`,
  uniform-block offsets, sampler units assigned in order;
- attributes by HLSLcc name `in_POSITION0…` (`:116-120`).

Nothing reads Unity's parameter data. Metal has no `glGetUniformLocation`, and its run-time
reflection (`MTLRenderPipelineReflection`) exists only after a pipeline is built. The shipped
data has what is needed up front:
- `m_ParameterBlobIndices` per player subprogram names a **parameter blob** in the same platform
  table;
- the pass stage's `m_CommonParameters` in the type tree holds the parameters every variant
  shares (for example `_MainTex` of `Klei/PostFX/FullScreen`), with names through the pass's
  `m_NameIndices`.

The parameter-blob layout, inferred from the data (`ShaderSurvey.cs`, `ParameterBlob`), is the
same on GLCore and Metal:
- magic `0x0C0A75BA`;
- a constant-buffer count, of which the first is an unnamed root;
- per buffer: name, size, a member list and a struct count (0 in every blob here). Each member
  is a name plus six ints: type, rows, columns, matrix flag, array size and byte offset. For
  example, `VGlobals` of `Klei/BatchedAnimation`'s vertex program is 108 bytes:
  `_ClusterWorldSizeInfo` @0, `BUILD_AND_ANIM_TEXTURE_SIZE` @16, `ANIM_TEXTURE_START_OFFSET` @32 …
  `SUPPORTS_SYMBOL_OVERRIDING` @104. `UnityPerFrame` is 368 bytes with `unity_MatrixVP` @272.
- a binding list, each entry a name and a kind:
  - 0 = texture (index, sampler index, −1 for read-only textures, dimension);
  - 1 = constant buffer (index, array size);
  - 2 = structured buffer (index, 1).

**Consistency check.** `ShaderSurvey check-metal` builds the expected slots from common
parameters plus parameter blob and compares them with the slots the MSL declares, both ways:

```
1057 Metal subprograms checked, 0 inconsistent
```

Every Metal parameter blob in the game parses exactly to its end. On the Linux data, the 6
built-in shaders that carry Metal have entries that point past their table (63 of 190), so
Unity's Linux build ships them half-stripped. That doesn't matter, since nothing uses them.

**Consequences for a Metal device.**
- It packs each constant buffer from the blob's member offsets. These are D3D-style packing
  rules, not std140, and the device trusts the offsets rather than recomputing them.
- It binds textures and samplers to the declared slots, and buffers to `[[buffer(n)]]`. The
  vertex stage's buffers share the argument table with vertex streams, so streams go above the
  highest cbuffer slot, as Unity's vertex attribute table implies.
- Today's uniform resolution (`GlUniforms.Resolve`, `GlPrograms.cs:311-661`) is keyed by GL
  reflection. It needs a backend-neutral form, a list of (name, kind, buffer, offset, size), that
  either reflection or the parameter blob can fill. The same model would let the GL device drop
  some reflection calls.
- The vertex stage ends with the channel mask our GL path already uses (`ShaderVariant.ChannelMask`),
  followed by pairs mapping channels to `[[ attribute(n) ]]`: (channel, 13 + n), confirmed for all
  522 vertex programs by `check-metal` (`MetalProgram.Inputs`).

### 1.5 Conventions baked into the programs

HLSLcc compiled each platform with its own conventions. The clearest example is
`Klei/PostFX/FullScreen`'s vertex stage:
- GLCore writes `vs_TEXCOORD0 = in_TEXCOORD0 * vec2(1,-1) + vec2(0,1)`;
- Metal passes the UV through unchanged.

So the Metal programs expect Unity's "UV starts at top" platform conventions:
- clip-space z in [0,1];
- render-texture content stored top-down;
- `_ProjectionParams.x = −1` when rendering into a texture.

Our engine models none of this. See §2.2.

### 1.6 Everything else in the data

| Data | Mac vs Linux |
|---|---|
| Texture2D formats (2682 textures) | **Identical**: 2321 DXT5, 124 DXT1, 125 RGBA32, 66 Alpha8, 32 ARGB4444, 13 RGB24, 1 BC7. The only differences are 65 sprite-atlas names whose build hash suffix differs (`sactx-…-DXT5|BC3-mainHUD-<hash>`). BC formats are supported by Metal on Apple Silicon and Intel Macs; our CPU block decoders are the fallback. |
| `.resS` streams | `resources.assets.resS`, `sharedassets1/2.assets.resS` and `resources.resource` (audio) byte-identical. `sharedassets0.assets.resS` same size, different hash (atlas packing order, as the names suggest). |
| StreamingAssets (763 files: banks, codex, worldgen, strings, templates, fonts) | Identical except `hires_base_bundle`, whose content (390 DXT5 textures, embedded type trees) matches; only its platform tag differs. |
| `Resources/unity default resources`, `unity_builtin_extra` | Mac builds of Unity's built-ins (Linux also has `UnityPlayer.png`). |
| Managed assemblies | `UnityEngine.CoreModule`, FMODUnity and Steamworks.NET have the same shape (`tools/AssemblyDiff`, 0 differences). Assembly-CSharp has 830 differences: the ImGui dev tools (`DevTool*`, `DevPanel`, `StatusItemStackTraceWatcher`) are compiled out on Mac, and there is no cimgui in `PlugIns`. Firstpass has 720: the EOS P/Invokes name `libEOSSDK-Mac-Shipping` instead of `EOSSDK-Win64-Shipping.dll` (no EOS platform is ever created). The Mac's `ScriptingAssemblies.json` lists `UnityEngine.ARModule` and `InsightsModule` as well, which our facade map must cover. |
| Natives (`Contents/PlugIns/*.bundle/Contents/MacOS/*`) | SimDLL: x86_64 MH_DYLIB, id `@rpath/libSimDLL.dylib`, minos 10.13, links libc++ and libSystem only. fmodstudio, fmod_haptics, resonanceaudio and libsteam_api: universal x86_64 + arm64. No cimgui, no sdkencryptedappticket. All are code-signed by Klei. |

Mods built against the Windows or Linux Assembly-CSharp that touch the dev-tool types would fail
on Mac, as they do with Klei's player.

---

## 2. The renderer seam

### 2.1 The device interface

`RenderDevice` (`src/OniMoono.UnityEngine/Engine/Rendering/RenderDevice.cs:55-121`) is an
abstract class with 17 overridable members:

| Concern | Members |
|---|---|
| Capabilities | `Caps` (a `GraphicsCaps`, 16 abstract members: device type, texture and RT format support…), `RendersFrames` |
| Resources | `ReleaseResource(object)`, `Upload(TextureNative)`. Meshes and textures are created lazily inside `Draw`/`SetRenderTarget`/`Upload`, the handle kept in the engine object's `Gpu` slot (`GraphicsDevice.Adopt`). |
| Targets | `SetRenderTarget(in RenderTargetBinding)`: colour RTs, depth, mip; null = back buffer |
| Programs | `SetPass(Material, int)`, `WarmupShaders()` |
| State | `SetViewport`, `SetScissor` (bottom-left origin), `Clear`, plus the non-virtual `SetCamera` |
| Draws | `Draw(in DrawRequest)`: mesh, submesh, matrix, material, pass, property sheet, extra keywords. `DrawImmediate(ImmediatePrimitive, vertices, modelView, projection)`: GL.Begin-style. |
| Readback | `ReadPixels(source RT or back buffer, rect, Texture2D, x, y)` |
| Frame | `Present()` |
| Profiling | `TakeGpuFrameMilliseconds`, `TakeFrameCounters` |

`NullRenderDevice` (`RenderDevice.cs:157-181`) already runs the game headless. The platform side
is `IPlatform` (`Engine/Platform/IPlatform.cs:10-37`), which has nothing GL-specific in it.

The GL backend is 4358 lines in `src/OniMoono.Desktop/Gl/`:

| File | Lines |
|---|---|
| GlDevice | 1066 |
| GlPrograms | 764 |
| GlTextures | 743 |
| GlMeshes | 282 |
| Gl | 267 |
| GlRenderState | 258 |
| GlWorker | 244 |
| GlPresenter | 222 |
| GlBindings | 147 |
| GlBindless | 134 |
| GlGpuProfile | 125 |
| GlStreamBuffer | 85 |
| GlTimings | 21 |

`GlfwPlatform.cs` is a further 600.

### 2.2 Engine-side code that assumes GL

| Assumption | Where | What a Metal device needs |
|---|---|---|
| Shaders come from platform 15 only | `ShaderNative.OpenGLCorePlatform`/`HasOpenGLCorePrograms` (`ShaderNative.cs:81,91,160`); `ShaderPrograms.Load`/`IsGlCore`/`Variants` (`ShaderPrograms.cs:52-189`); `Material.isSupported` (`Rendering/Material.cs:21`) | The loader takes the device's platform (14 or 15), and variants are kept per stage (§1.3). |
| GL clip space, z in [−1,1] | `CameraNative.Projection` (`Rendering/Camera.cs:352-362`); hard-coded ortho matrices (`Graphics.cs:130,143,327,378`, `GUIRenderer.cs:88`, `CanvasSystem.cs:1004`, `VideoPlanes.cs:43`); the GL device applies no fix-up (`GlDevice.cs:61-62`), `_ProjectionParams.x = 1` (`GlPrograms.cs:464-466`), GL `_ZBufferParams` (`:468-472`) | The device rewrites projection-bearing built-ins (`glstate_matrix_projection`, `unity_MatrixVP`, `unity_MatrixP`, `_ZBufferParams`) to z in [0,1]. When the target is a render texture it also flips Y and sets `_ProjectionParams.x = −1`, as Unity's Metal device does, and reverses front-face winding to match. No `GL.GetGPUProjectionMatrix` exists in our engine yet (grep). The game never calls it (Klei decomp: no hits). |
| Bottom-left origin for viewport, scissor, ReadPixels and texture rows | `RenderDevice.cs:83-87`; callers `Texture.cs:495`, `AsyncGPUReadback.cs:60`, `FrameRenderer.cs:187,209` | Converted inside the device: Metal viewports and scissors are top-left; readback of a flipped RT reverses rows. |
| RenderTextureFormat defaults "desktop GL" | `TextureNatives.cs:354-392` | The defaults (ARGB32, ARGBHalf) are fine on Metal; the GraphicsFormat → native mapping is device code. |
| `ImmediatePrimitive` uses GL.Begin values | `RenderDevice.cs:137-145` | Unity API; harmless. Quads become indices in the device. |
| `SystemInfo.operatingSystemFamily`, `Application.platform` | `Core/SystemInfo.cs:18`, `Core/Application.cs:21`: Windows or Linux only | Must report `MacOSX` / `OSXPlayer`. FMODUnity selects its platform from `Application.platform` (`FMODUnity/Settings.cs:356` maps Linux, `docs/research/runtime.md` §7.5), and `PointerEvents.cs:265` already has an `IsMacOS` check. |

### 2.3 Backend-neutral logic that lives in `Gl/` today

A second backend would duplicate these unless they move to the engine first:
- Pass and variant choice: `GlDevice.IsForwardPass`/`Passes`/`ResolveUsePass`/`Variant`
  (`GlDevice.cs:453-494, 652-665`).
- Default textures (white, black, bump; array, 3D and cube) and unbound-sampler rules (`:219-268, 598-603`).
- ShaderLab state parsing, `PassState`/`ResolvedState` (`GlRenderState.cs:10-141`).
- Uniform value resolution: built-in matrices, time and screen constants, the property lookup
  order and per-material caching (`GlUniforms`, `GlPrograms.cs:311-661`). Keyed by GL
  reflection, so it needs the neutral parameter model of §1.4.
- Per-frame recording: `ProgramInputs`/`FrameArena` (`GlDevice.cs:523-606`, `GlWorker.cs:184-243`).
- Mesh layout and index conversion (`GlMeshes.cs:22-29, 214-262`).
- Texel snapshots for uploads (`GlTextures.cs:79-202`).

This refactor can be done and tested on Linux against the GL device, with the existing e2e and
screenshot suites as the safety net.

### 2.4 What a Metal device implements

1. `MetalCaps`: `GraphicsDeviceType.Metal`, format support (BC, depth formats, and so on).
2. **Programs**:
   - MSL compiled lazily per variant with `newLibraryWithSource`, cached per (blob, function constants);
   - `MTLRenderPipelineState` keyed by (vertex function, fragment function, vertex descriptor
     from mesh layout and channel table, attachment pixel formats, sample count, blend and
     colour mask, alpha-to-coverage);
   - `MTLDepthStencilState` per depth/stencil state;
   - a pipeline cache (in memory, optionally an `MTLBinaryArchive` on disk; Apple advises
     against keeping it inside the app bundle).
3. **Uniforms**: constant buffers packed from the parameter model into a per-frame ring buffer,
   bound by offset (`setVertexBuffer:offset:atIndex:` / `setVertexBytes` for small ones).
4. **Resources**:
   - `MTLTexture` for 2D, array, 3D and cube, with mips, swizzles (ARGB32/Alpha8 via
     `MTLTextureSwizzleChannels`) and partial updates (`replaceRegion`);
   - samplers from `SamplerSettings`;
   - render textures (colour + depth-stencil, MSAA resolve via store actions, auto mips via
     blit encoder, MRT);
   - mesh vertex streams as `MTLBuffer`s.

   Metal has no constant vertex attributes, so absent channels (white colour and so on) need a
   zero-stride default buffer.
5. **Passes**: render passes begin and end around target changes. A clear becomes a load action
   at pass start or a quad mid-pass. Viewport and scissor are converted to top-left.
6. **Conventions**: the projection rewrite and RT flip of §2.2, done once in the device.
7. **Threading**: today the main thread records, the GL worker thread executes and a presenter
   thread blits and swaps (`GlWorker.cs:16-179`, `GlPresenter.cs:22-221`). Metal fits the same
   recording model with one `MTLCommandBuffer` per frame. Present is
   `CAMetalLayer.nextDrawable` → blit or draw → `presentDrawable`, with vsync through
   `displaySyncEnabled`, so the presenter thread and shared-context dance disappear.
8. **Readback**: blit to a shared `MTLBuffer`, wait on the command buffer, then the engine's
   `TextureFormats.Encode`, as GL does after `glReadPixels`.
9. **Profiling**: `MTLCommandBuffer.GPUStartTime/GPUEndTime`.
10. **Host wiring**: device choice in `Player.cs:22-34` and `ShaderCheck.cs:24-26`, and a
    `GlfwPlatform` variant without GL context hints or the hidden shared window (`GlfwPlatform.cs:60-105`).

Size guess: comparable to the GL backend, about 4000–5000 lines, plus 1000–2000 lines of
Objective-C interop and roughly 1000 lines of backend-neutral code moved out of `Gl/`.

**Why not GL 4.1 on the Mac.** The data would allow it (§1.1). But:
- the device uses DSA (`glCreateBuffers`, `glNamedBufferData`, `glBindTextureUnit`…, about 130
  GL entry points), `ARB_buffer_storage` persistent mapping (`GlStreamBuffer.cs:38-39`),
  `glTexStorage`, bindless and debug output;
- Unity's GLSL uses `layout(location/binding)`, which `GlPrograms.Prepare` (`:140-153`) satisfies
  with 4.3/4.2 extensions macOS does not have;
- the 66 `#version 420` programs would be unusable.

A 4.1 variant of the device is perhaps 3–5 weeks of work for a renderer Apple deprecated in
2018, which runs as a translation layer over Metal on Apple Silicon and gets no fixes. It is
worth it only as a stopgap to see the game on a Mac early; it is not a product path.

**Why not Vulkan through MoltenVK.** It would need:
- GLSL → SPIR-V of Unity's GLSL (glslang can compile it, but `UNITY_LOCATION` and plain
  uniforms need rewriting into blocks);
- the whole Vulkan device we don't have;
- MoltenVK or KosmicKrisp under it.

Khronos deprecated MoltenVK on 2026-10-13, now community-maintained
(https://github.com/KhronosGroup/MoltenVK). KosmicKrisp (Vulkan 1.4 conformant) needs Metal 4,
macOS 26 and Apple Silicon, and its behaviour under Rosetta is unknown. This path adds three
layers to reach the API the shipped MSL already targets. BRUTAL's Vulkan package loads a
`VulkanEx` helper on macOS but ships no loader (`docs/research/brutal-api.md` §2.1). Not
recommended.

---

## 3. .NET on macOS

### 3.1 Runtime

- .NET 10 supports macOS 15, 26 and 27 on Arm64 and x64, and states "Rosetta 2 [x64 emulator]
  is supported on macOS Arm64"
  (https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md). .NET 11 has the
  same table and still ships macOS x64; no osx-x64 deprecation is announced.
- **Verified here.** `dotnet publish src/OniMoono.Host -c Release -r osx-x64 --self-contained`
  succeeds on Linux. It produces an x86_64 Mach-O apphost, `libcoreclr.dylib` and so on, 84 MB
  (`pub-osx-x64/`). Self-contained avoids the side-by-side x64 SDK under `/usr/local/share/dotnet/x64`.
- **Open CoreCLR bugs under Rosetta**, all open as of 2026-10:
  - dotnet/runtime#136288: intermittent start-up hang of a self-contained x64 app in Rosetta's
    translation cache, seen on .NET 12 alpha with macOS 27.0.1; .NET 11 RCs passed 200 launches;
  - #133797: `thread_get_state` hang used by the process-wide barrier;
  - #111628: application hang;
  - #89272: GC deadlock spawning a process.

  These need testing on real hardware. Our host uses server GC with many threads, which makes
  the GC-suspension hangs more likely to show.
- **Rosetta's horizon.** Apple: Rosetta "will be available through macOS 27 — as a
  general-purpose tool … Beyond this timeframe, we will keep a subset of Rosetta functionality
  aimed at supporting older unmaintained gaming titles, that rely on Intel-based frameworks"
  (https://developer.apple.com/documentation/apple-silicon/about-the-rosetta-translation-environment;
  Apple declined further guidance, https://developer.apple.com/forums/thread/787530). Klei's own
  Mac player (Unity, Mono and SimDLL) is x86_64 only, so it depends on the same subset. Whether a
  new x86_64 .NET process counts is not stated.

### 3.2 Native libraries we depend on

BRUTAL packages ship natives for **linux-x64, win-x64 and osx-arm64 only**
(`~/.nuget/packages/ahwoo.brutal.*/*/runtimes/`; `docs/research/brutal-api.md:13,1223`). The
osx-arm64 dylibs are thin arm64, minos 15.0, and code-signed. The osx-arm64 publish of the host
carries all of them, and `--check-natives --native-arch arm64 --native-dir` finds every entry
point we import in them (§3.4). The table below is about the x86_64 fallback only.

| Package | What we use | osx-x64 status |
|---|---|---|
| Brutal.Glfw | windowing, input; already has `Brutal.GlfwApi.Cocoa.CocoaExtensions.GetCocoaWindow/GetCocoaView/GetCocoaMonitor` | needs an x86_64 `libglfw` (GLFW 3.5, zlib licence; trivial CMake build, or a universal binary) |
| Brutal.OpenGl | `Glad` + GL entry points | not needed for a Metal device |
| Brutal.FreeType (+ ZLib, libpng) | font rasterisation | needs x86_64 freetype, png and zlib |
| Brutal.Stb | image decode | needs x86_64 stb |
| Core, Numerics, Logging | managed only | fine |

The osx-x64 publish above contained `Brutal.*.dll` but **no native dylibs**, which confirms the
gap. The options are:
- (a) ask the BRUTAL maintainers for osx-x64 or universal natives (BRUTAL has no Metal package);
- (b) build the five libraries ourselves for x86_64 and ship them beside the player;
- (c) avoid them on Mac by replacing stb and freetype use with managed code or ImageIO/CoreText.

(b) is a day or two with a Mac or osxcross toolchain. The licences (zlib, FTL/GPL dual, MIT or
public domain) allow it.

### 3.3 Objective-C and Metal interop

| Option | Licence and state | Fit |
|---|---|---|
| **Raw `objc_msgSend` via function pointers** (`delegate* unmanaged<IntPtr, IntPtr, …>` on `NativeLibrary.GetExport(libobjc, "objc_msgSend")`, `sel_registerName`, `objc_getClass`) | No dependency. The .NET design notes this is all that is needed when exceptions and varargs don't matter (https://github.com/dotnet/designs/blob/main/accepted/2021/objectivec-interop.md) | **Recommended.** A Metal device needs about 100–150 selectors. On x86_64, struct returns larger than 16 bytes use `objc_msgSend_stret` (arm64 has no stret), which matters for e.g. `MTLSize`-returning getters. `ObjectiveCMarshal` (System.Runtime.InteropServices.ObjectiveC) is optional plumbing for reference tracking and exceptions; not needed. |
| SharpMetal (github.com/IsaacMarovitz/SharpMetal) | MIT; 1.1.0 (2025-11), net8/net9 targets, low activity | Usable as a reference or a dependency; generated, broad. Rides on the same `objc_msgSend` approach. |
| ppy.Veldrid.MetalBindings | MIT; osu!'s fork still publishes (4.9.76, 2026-03) | A hand-written subset that has shipped in a game (osu!lazer on macOS). A good reference. |
| Silk.NET | MIT; no Metal bindings in 2.x or 3.0 | No. |
| .NET for macOS (`net10.0-macos`, dotnet/macios) | MIT; very active | Full AppKit and Metal bindings, but it needs the macOS workload and a matching Xcode to build, and changes our app model. Overkill for one device. |
| Generate our own (ClangSharp over the Metal headers, or Apple's metal-cpp, Apache-2.0, as the selector catalogue) | MIT / Apache | Viable for breadth. Hand-written is smaller for our needs. |

`MTLCreateSystemDefaultDevice` returns nil in a process that does not link CoreGraphics
(https://developer.apple.com/documentation/metal/mtlcreatesystemdefaultdevice()). GLFW's
Cocoa build links it (the arm64 `libglfw.3.5.dylib` loads CoreGraphics, QuartzCore and
AppKit), so loading GLFW first is enough; otherwise `dlopen` CoreGraphics.

### 3.4 The arm64 build: onimaxxing-sim in place of SimDLL

**Decision.** The Mac build targets native arm64, with onimaxxing-sim as its SimDLL. Klei's Mac
SimDLL is the one native without an arm64 slice, so it would force the whole process under
Rosetta. onimaxxing-sim (workspace `~/projects/oni-worldgen`, crates `onimaxxing-sim-ffi` (the
cdylib), `-engine` and `-physics`; MIT) is a Rust port of SimDLL with the same C ABI. Its
bit-exactness with vanilla is **verified upstream, not by us**. That covers the port's session
corpus, and on arm64 a static aarch64 Linux build of its replay tool under qemu-aarch64: the four
vanilla pins (debris, germs, ratsnest, eclipse_s2) gave bit-identical digests on every frame and
channel. That check covers arm64 code generation (FMA contraction, NaN propagation, denormals:
on arm64 the x86 `FtzGuard` does nothing and a software flush-to-zero applies, as on wasm32). It
runs neither the Mach-O dylib, nor macOS's libSystem, nor the game. The sim carries its own ports
of the C runtime's maths (`ucrt_libm`) and takes only allocation and threads from libSystem.

**The x86_64 build stays as the fallback**, with Klei's SimDLL under Rosetta, for as long as
Rosetta runs it (§3.1).

**How the host picks the sim.** `GameLayout.Native` (`src/OniMoono.UnityEngine/Engine/Platform/GameLayout.cs`)
is used by `AssemblyPolicy.ResolveNative`. A game plugin is used when it has the process's
architecture. Otherwise the host takes a replacement of the same name from `plugins/` beside the
host (`plugins/libSimDLL.dylib`), if that file has the architecture. The result:

| Process | SimDLL it binds |
|---|---|
| arm64 | the port in `plugins/`, since Klei's has no arm64 code |
| x86_64 | Klei's, with or without a port present |

So the Mac package ships the port as `plugins/libSimDLL.dylib` in the arm64 build, and no
configuration is needed. `--native-override SimDLL=PATH` still forces any library, for tests and
for the port on Linux. A replacement or override is logged once, with the library's
`SYSINFO_Acquire` string, which names the library the game's imports bound to (the game itself
never calls it). `GameLayoutTests` covers the selection rule.

**The port's builds** (oni-worldgen commit 2d591b9f): built with zig 0.13.0 and
cargo-zigbuild 0.24.0, in `dist/simdll-macos/`. All four are Mach-O dylibs with install name
`@rpath/libSimDLL.dylib`, whose only dependency is `/usr/lib/libSystem.B.dylib`:

| File | Arch | Feature | minos |
|---|---|---|---|
| `libSimDLL-arm64-simthread.dylib` | arm64 | sim-thread (the sim runs on its own thread; PrepareGameData does not block); **the one to ship** | 11.0 |
| `libSimDLL-arm64-default.dylib` | arm64 | the tick runs inside PrepareGameData | 11.0 |
| `libSimDLL-x86_64-simthread.dylib` | x86_64 | sim-thread | 10.13 |
| `libSimDLL-x86_64-default.dylib` | x86_64 | default | 10.13 |

`--check-natives` gives each of them **16 of the game's 32 SimDLL imports**:

- **16 present:** 7 `SIM_*`, 2 `SYSINFO_*`, 7 `ConduitTemperatureManager_*`. "16/16" in the
  port's own notes counts these only.
- **16 absent:** the `kprofile*`/`kprofiler_*` functions of `Klei.KProfilerPlugin`, which also
  imports from `SimDLL`. Klei's SimDLL exports them, hence "32 of 32" there. No game code reaches
  them: every caller in `KProfiler` is `[Conditional("ENABLE_KPROFILER")]`, compiled out of the
  shipped game, and `KProfilerPlugin.Initialized` is never set (`FP/KProfiler.cs`,
  `FP/Klei/KProfilerPlugin.cs`). The check lists them as expected-absent for SimDLL.

**Every native an arm64 process loads**, from
`--check-natives --native-arch arm64 --native-dir out/publish/osx-arm64`. That command checks
each library's arm64 slice and the dependencies the slice links. Klei's other plugins are read
from the depot.

| Native | Loaded by | Comes from | arm64 |
|---|---|---|---|
| `libSimDLL.dylib` | the game's `SimDLL` imports | onimaxxing-sim, shipped in `plugins/` | **yes** (thin arm64; 16/32, the rest never called) |
| `fmodstudio` | FMODUnity | the game's bundle | yes (universal; 571/571) |
| `resonanceaudio`, `fmod_haptics` | FMOD itself, by path (not P/Invoked) | the game's bundles | yes (universal) |
| `libsteam_api.dylib` | Steamworks.NET | the game's bundle | yes (universal; 924/930, the 6 also missing on Linux) |
| `libglfw.3.5.dylib` (+ `libglfw.3`, `libglfw`) | Brutal.Glfw | BRUTAL's osx-arm64 package | yes (138/154; the 16 absent are GLX, WGL, Wayland, Win32 and X11 accessors that the Cocoa build lacks by design) |
| `libfreetype.6.dylib`, linking `libpng18.18.dylib` and `libzlib.1.dylib` | the engine's font rasteriser | BRUTAL (FreeType, ZLib packages) | yes (15/15; both dependencies found beside it) |
| `libstb.dylib` | Brutal.Stb | BRUTAL | yes (17/17) |
| `libglad.dylib` | Brutal.OpenGl | BRUTAL | yes (2/2); only the GL device uses it, the Metal device does not |
| `libcoreclr`, `libclrjit`, `libSystem.*.Native`, `libhostfxr`, `libhostpolicy`, `libclrgc*` | .NET 10 | the self-contained osx-arm64 publish | yes (apphost and runtime are arm64) |
| `libSystem`, `libc++`, `libobjc`; Cocoa, AppKit, Foundation, CoreFoundation, CoreGraphics, CoreServices, QuartzCore, IOKit, CoreAudio, AudioUnit, AVFoundation, Carbon; Metal | the libraries above, the Metal device | macOS | yes (system) |
| cimgui, EOS, Rail, sdkencryptedappticket | not shipped on Mac, never called | — | — |

**Measured on Linux** (x86_64; the port as a Linux `.so` built from the same commit):

- **Binding:** `--native-override SimDLL=<libonimaxxing_sim_ffi.so>` binds both builds, default
  and sim-thread. Our log reads `Native SimDLL: … (--native-override): SYSINFO "onimaxxing-sim-ffi
  Rust runtime (onimaxxing-sim-physics port)"`. The crate's `oni-ffi.log` (written to `$TEMP`)
  records `SIM_Initialize ABI=0xcafe0001 pid=<the player's>`. Terra1088x768 loads and runs on
  both builds.
- **Robustness:** on QA's U57 colony the port as built crashes within about 1600 to 1850 sim
  ticks (a handle-version bug, §3.5). A fix is proposed upstream and was checked here on a
  scratch build.
- **Performance:** the port is slower than Klei's SimDLL on Linux (37% of its frame rate on
  Terra, 65% on U57; §3.5).

**What is open:**

- **Upstream:** the handle-version fix (until it lands, large colonies crash on the port) and
  the port's speed against Klei's (§3.5).
- The port on a Mac: `NativeLibrary.Load` of the dylib, a colony run, its `oni-ffi.log`. The
  crate looks for `$TEMP` and `$TMP`, which macOS does not set (`$TMPDIR` is the Mac variable), so
  on a Mac the log falls back to the working directory (a patch is proposed upstream).
- Ad-hoc signing: arm64 code must be signed, and the zig-linked dylibs need `codesign -s -`
  before shipping **(to verify on a Mac)**.
- **Harmony on osx-arm64.** The host's game patches (D1, D3, D11), the game tweaks and mods all
  use the game's Harmony 2.4.2, which writes native detours. Klei's Mac player is x86_64, so no
  ONI mod has run Harmony on arm64 macOS. Whether it works under CoreCLR there (arm64 detours,
  W^X on Apple Silicon) is **to verify on a Mac**, and it is the first thing to run.
- A Mach-O arm64 build of the Steam test double (`tools/SteamStub`), for offline runs.
- **Bit-exactness through game updates.** The arm64 build is only as correct as the port. Each
  game update that changes SimDLL needs the port updated and re-pinned against the new vanilla
  (upstream's session corpus) before the arm64 build can ship that update. Until then the
  x86_64 build with Klei's new SimDLL is the build to use. Our `--check-natives` catches an
  update that adds or renames imports, but not one that changes behaviour.

### 3.5 Performance: Klei's SimDLL against the port, on Linux

**Setup.** Measured 2026-10-24 on this machine (WSL, Mesa d3d12, Xvfb), x86_64 Linux. The player
is the release build with `--game-tweak all` at 1024x768, as `scripts/qa.sh`'s perf stage runs
it, with these differences:

- the save resumes itself (`AutoResumeSaveFile`) and runs for 4800 frames;
- PerfProbe holds speed 3, with `walltime.txt` on.

The port is the sim-thread `.so` built from oni-worldgen 2d591b9f, bound with
`--native-override`. Runs alternate native and port, two rounds per save. Each pair has the
machine to itself (perf.lock exclusive, under 2 cores busy).

How the columns are read:

- **fps** is the mean of PerfProbe's 600-frame windows, the first one (the load) left out.
- **StepTheSim** is `Game.StepTheSim`'s wall time per frame on the main thread, from PerfProbe's
  walltime windows, again without the first. With the sim-thread build it is the time spent
  waiting for the sim.

| Save | SimDLL | Runs | fps | Frame | StepTheSim |
|---|---|---|---|---|---|
| Terra1088x768 (1088x768 grid, sim-bound) | Klei's | 2 | 109.7, 110.0 | 9.1 ms | 3.4 ms |
| Terra1088x768 | onimaxxing-sim | 2 | 40.6, 40.1 | 24.8 ms | 17.2 ms |
| U57 (QA's perf colony) | Klei's | 4 | 47.1, 46.9, 46.8, 46.7 | 21.3 ms | 1.9 ms |
| U57 | onimaxxing-sim as built | 2 | 30.6, 31.9 (before the crash) | 32.0 ms | 6.6 ms |
| U57 | onimaxxing-sim with the proposed fix | 2 | 31.5, 28.8 | 33.4 ms | 6.9 ms |

**What this shows:**

- **On Linux x86_64, the port is much slower than Klei's SimDLL here.** On Terra it reaches 37%
  of Klei's frame rate, with 14 ms more a frame waiting on the sim. On U57 it reaches about 65%.
  The frame time grows by more than StepTheSim does there, so part of the cost lies outside the
  main thread's wait (for example `ConduitTemperatureManager_Update` and the port's logging: it
  writes `CTM_HASH_STATS` lines to `oni-ffi.log` continuously, about 0.5 MB a minute).
- **How this carries to the Mac:** it is not a measurement of the Mac. There the comparison is
  the port native on arm64 against Klei's SimDLL under Rosetta. Still, a gap this size makes the
  port's speed a risk for the arm64 build's frame rate on large colonies (R9). It should be
  measured upstream, which has the profiler and the corpus.
- **The port as built crashes on U57.** The sim thread panicked (`free_handles entries must be
  valid handles`, `compacted_vector.rs:153`) at sim tick 1844 in one run and 1619 in the other,
  4800 frames not reached. The cause: a handle's version byte above 127 makes the packed `i32`
  negative, and the port treats every negative handle as invalid, where SimDLL and the game's
  `Sim.IsValidHandle` reserve only −1 and −2. A scratch build with that check corrected (patch
  proposed upstream; the workspace was not changed) ran both U57 runs to the end (tick 2335 and
  beyond) at the same speed. Presumably no pinned session runs long enough to reuse a slot 128
  times, so upstream's parity runs could not show this.

---

## 4. Windowing and input

- **GLFW with Metal.**
  - Create the window with `ClientApi = NO_API` (`GLFW_NO_API`, listed in
    `docs/research/brutal-api.md:106`).
  - Then `GetCocoaView(window)` → `[view setLayer: CAMetalLayer]` and `[view setWantsLayer: YES]`,
    as GLFW's own Vulkan surface code does (https://github.com/glfw/glfw/blob/3.5.1/src/cocoa_window.m).
  - With our own layer we must update `contentsScale` and `drawableSize` from the
    content-scale and framebuffer-size callbacks ourselves.
- **Retina.** `GLFW_SCALE_FRAMEBUFFER` (formerly `COCOA_RETINA_FRAMEBUFFER`) defaults to true,
  so the framebuffer is in pixels.
  - `GlfwPlatform` already reports framebuffer pixels as the screen size and scales cursor
    positions from window to framebuffer coordinates (`GlfwPlatform.cs:122,144,149-150,278,385-391`).
  - DPI comes from the monitor's physical size (`:175-179`); on a Mac it should come from
    `glfwGetWindowContentScale` × 72 or 96 **(to verify how Unity's Mac player reports
    `Screen.dpi` and `Screen.width` on Retina)**.
- **GL-only assumptions in `GlfwPlatform`.**
  - Context hints for GL 4.5 core, forward-compat and debug (`:60-70`).
  - A hidden 1x1 shared render window (`:96-105`).
  - Swap interval and `SwapBuffers` in `GlPresenter`.
  - An X11 preference on Linux (`:52-58`).
  - The Metal variant drops all of these. Everything else (input callbacks, cursors, clipboard,
    monitors, video modes) is API-neutral.
  - Exclusive full screen already maps to full-screen-window off Windows (`:238`), which is
    also right on macOS.
- **Main thread.** GLFW requires init, window creation and event processing on the main
  thread. A synchronous .NET `Main` is the process's first thread, which Cocoa treats as main
  (Avalonia works from `Main` but fails from `Task.Run`, https://github.com/AvaloniaUI/Avalonia/issues/2800).
  Our player loop already runs GLFW on the `Main` thread. Init hints `CocoaMenubar` and
  `CocoaChdirResources` are in Brutal.Glfw (`brutal-api.md:51`); the chdir hint should be off,
  since we resolve paths ourselves.
- **Keys.** The game binds only Ctrl, Alt, Shift and Caps Lock. It never maps Command
  (`FP/KInputController.cs:66-82,226-230`; `KKeyCode` defines `LeftCommand`/`RightCommand`, but
  nothing uses them), so Mac players use the physical Control key, as with Klei's player. GLFW's
  default menu bar gives Cmd-Q → window close, which the game's quit path already handles. Our
  UIElements `actionKey` already picks Command on macOS (`UIElements/OtherEvents.cs:20`).

---

## 5. Host and platform pieces

### 5.1 Finding the game and its data

- Layout: `OxygenNotIncluded.app/Contents/{MacOS,Frameworks,PlugIns,Resources/Data,MonoBleedingEdge}`.
  The data folder is `Contents/Resources/Data`, not `OxygenNotIncluded_Data`.
- The Steam install is
  `~/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app`
  **(to verify)**, with `libraryfolders.vdf` in the same Steam root, so `setup.sh`'s `find_game`
  loop works with one more root.
- `Program.Main` hard-codes `GameDir/OxygenNotIncluded_Data` (`src/OniMoono.Host/Program.cs:25`).
  On macOS it should accept the `.app` (or its parent) and use `Contents/Resources/Data`.
- `Application.dataPath` is a separate value. Unity's Mac player reports the bundle's
  `Contents` folder (Unity-documented behaviour, **to verify**). Our `EngineConfig.DataPath` is
  set to the data folder (`Program.cs:858`), so the Mac branch must set it to `Contents` while the
  asset system keeps reading `Contents/Resources/Data`. The game's `dataPath` uses are tolerant:
  - `settings.yml` beside dataPath (`FP/Klei/GenericGameSettings.cs:104`);
  - `modReport.json` written beside it (`AC/KMod/Manager.cs:1010`);
  - `Managed/ModLoader.dll` is probed and absent (`AC/KMod/DLLLoader.cs:19`);
  - `hashes.json` (`AC/KCrashReporter.cs:270-273`);
  - `Tuning.json` (`FP/TuningSystem.cs:99`).

  With dataPath = `Contents` these resolve inside the bundle, as they do for Klei's player.
  FMODUnity's Mac platform looks for its plugins relative to dataPath, which is why Unity's Mac
  dataPath is `Contents` and the plugins live in `Contents/PlugIns` **(to verify against
  FMODUnity's PlatformMac)**.
- `StreamingAssets` stays `Contents/Resources/Data/StreamingAssets`.

### 5.2 Saves, mods, logs

- The game's root folder on non-Windows is `Application.persistentDataPath` itself
  (`FP/Util.cs:27,760-767,784-791`). Saves go in `save_files/` (`AC/SaveLoader.cs:468`), mods in
  `mods/{Local,Dev,Steam}` (`AC/KMod/Manager.cs:80-83`, `AC/Global.cs:346-347`,
  `AC/KMod/Label.cs:30-33`), plus `kplayerprefs.yaml` and `keybindings.json`. No game code
  branches on macOS (no `OSXPlayer` comparisons anywhere in the decomp).
- Unity's Mac `persistentDataPath` is `~/Library/Application Support/<bundle id>`. For this game
  that is `~/Library/Application Support/unity.Klei.Oxygen Not Included` (bundle id
  `unity.Klei.Oxygen Not Included`, `Info.plist`), which is where Mac players find their ONI
  saves **(to verify on a Mac)**.
- `consoleLogPath` is `~/Library/Logs/<company>/<product>/Player.log` per Unity's documentation
  **(to verify)**. Our host sets it to `persistentDataPath/Player.log` on every platform
  (`Program.cs:851`), so a Mac branch of `Paths.Configure` sets all three.
  `temporaryCachePath` is `~/Library/Caches/<bundle id>` **(to verify)**.
- **Same saves as Klei's Mac player**, so saves and mods are shared, as on Linux and Windows.

### 5.3 Native library resolution

- The game P/Invokes bare names: `SimDLL` (`AC/Sim.cs` 9 imports, `AC/ConduitTemperatureManager.cs` 7,
  `FP/Klei/KProfilerPlugin.cs` 16), `fmodstudio`/`fmod` (FMODUnity) and `steam_api` (Steamworks.NET).
- `AssemblyPolicy.ResolveNative` (`src/OniMoono.Host/AssemblyPolicy.cs:101-111`) looks in
  `Plugins/` for `lib{name}.so`, `{name}.so` and `{name}`. On macOS it must look for
  `Contents/PlugIns/{name}.bundle/Contents/MacOS/{name}` (the executable named by the bundle's
  `CFBundleExecutable`, which is `SimDLL`, `fmodstudio`, `resonanceaudio`), then
  `Contents/PlugIns/lib{name}.dylib`.
  - steam_api's bundle executable is `libsteam_api.dylib`, so reading `CFBundleExecutable` from
    `Info.plist` is the robust rule.
  - `NativeLibrary.Load` of the full path works for MH_DYLIB inside a bundle.
  - SimDLL's install name is `@rpath/libSimDLL.dylib` and it links only libc++ and libSystem, so
    no rpath set-up is needed.
- A plugin without the process's architecture gives way to the host's `plugins/lib{name}.dylib`
  (§3.4): this is how the arm64 build binds onimaxxing-sim.
- FMOD loads `resonanceaudio` itself by path from its plugin folder (see §5.1).
- These are Klei-signed binaries loaded by our unsigned or ad-hoc-signed process. Without the
  hardened runtime, library validation does not apply, so this works **(to verify)**.

### 5.4 Steam, the Steam double, the Restarter

- **Steamworks.NET.** We rebuild 20.1.0 for CoreCLR (`src/ThirdParty/Steamworks.NET`). Its csproj
  picks `UNITY_STANDALONE_LINUX` or `_WIN` by RID (`Steamworks.NET.csproj:13-21`, RIDs
  `linux-x64;win-x64`). macOS needs `osx-x64` → `UNITY_STANDALONE_OSX`, which selects the same
  small callback packing and `steam_api` library name as Linux (`Runtime/Packsize.cs:23`,
  `Runtime/autogen/NativeMethods.cs:46`). The shipped `libsteam_api.dylib` is universal.
- **Steam double** (`tools/SteamStub/build.sh`): it generates x86-64 assembly and links an ELF
  `.so` with GNU `as`/`ld`. A Mach-O build needs `clang -arch x86_64 -dynamiclib` on a Mac, or
  `llvm-mc` + `ld64.lld` (no clang or lld is installed on this machine). Small job.
- **Restarter.** The game runs `<dir of MainModule.FileName>/Restarter <exe>` (`FP/App.cs:38,54-67`).
  Our `src/OniMoono.Host/Restarter` is a POSIX `sh` script and works on macOS unchanged, as long
  as it sits beside our host executable (not in Klei's `Contents/MacOS`).
- **Launching from Steam.** As on Linux, the user's launch option points Steam at our player with
  `%command%` after it (Steam on macOS supports launch options the same way). `setup.sh` needs a
  Mac branch:
  - the Steam root;
  - the `.app` path;
  - an osx-x64 build;
  - `xattr -dr com.apple.quarantine` on the player folder;
  - `arch -x86_64` when the build machine's default `dotnet` is arm64.

### 5.5 Signing and quarantine

- A downloaded player gets `com.apple.quarantine`. Since macOS 15, control-click no longer
  bypasses Gatekeeper; the user must use System Settings → Privacy & Security → Open Anyway, or
  `xattr -dr com.apple.quarantine <dir>`.
- A player built locally by `setup.sh` from the repository is not quarantined.
- Apple Silicon requires arm64 code to be signed (ad-hoc is enough). That rule does not apply to
  x86_64 code under Rosetta (https://eclecticlight.co/2020/08/22/apple-silicon-macs-will-require-signed-code/).
  Ad-hoc signing everything anyway (`codesign -s - --force --deep`) is cheap and future-proof.
- Under the hardened runtime a JIT-ing .NET app needs `com.apple.security.cs.allow-jit`. We
  would not enable hardened runtime unless we notarise; notarising needs a paid Apple developer ID.

---

## 6. Testing without a Mac

### 6.1 Verifiable here (done or ready)

Status as of 2026-10-24; the details are in `docs/research/metal-backend-design.md` §1–§2.
`scripts/check.sh` runs every check marked *in the gate*, the Mac ones when the depot is present
(`ONI_MAC_APP`, by default this machine's Steam depot download).

| Check | Status |
|---|---|
| osx-arm64 self-contained publish of the host (primary) | **Verified, in the gate**: arm64 Mach-O apphost, with BRUTAL's osx-arm64 dylibs beside it (glfw, freetype with png and zlib, stb, glad); Steamworks.NET with `UNITY_STANDALONE_OSX`. When the port's arm64 dylib is found (`ONI_SIM_PORT_ARM64`), the gate puts it in `plugins/`. |
| osx-x64 self-contained publish of the host (fallback) | **Verified, in the gate**: builds (about 20 s warm), x86_64 Mach-O apphost; Steamworks.NET builds for osx-x64 with `UNITY_STANDALONE_OSX`. Brutal natives missing (§3.2). |
| onimaxxing-sim in our player | **Verified on Linux** (§3.4): `--native-override` binds the port's `.so`, which our log and the crate's log both show. Bit-exactness with vanilla is verified upstream against the port's session corpus, and on arm64 under qemu, not by us. **Found here:** a crash on U57, fixed by a proposed patch, and frame rates of 37–65% of Klei's (§3.5). |
| Shader data parses; Metal reflection consistent with MSL | **Verified, in the gate**: `ShaderSurvey check-metal` 1057/1057, now also each entry decoded by the engine's reader and its vertex attribute table (component 13 + n = `[[ attribute(n) ]]`) against the MSL. |
| Metal program loading in the engine | **Verified**: `ShaderPrograms.Load(shader, 14)`, Metal variants per stage, `SelectStages`; all 1057 programs and blobs decode (`ShaderDataTests`); the GLCore path decodes all 583 Linux and 596 Mac GLCore variants as before. |
| Metal parameters against GLCore's | **Verified, in the gate**: `ShaderSurvey compare-metal`: every Metal program is selected by a GLCore variant; 10700 constants, 1860 textures and 69 storage blocks agree; the differences fall in six explained classes (built-in cbuffers loose on GLCore, `hlslcc_zcmp` samplers, `texture.read`, the GLCore blob's lossy `$Globals` and int type code, 9 GLCore-only keyword variants) plus VR multiview. |
| Variant coverage Metal vs GL | **Done** (§1.3), and in `compare-metal`. |
| Constant-buffer packing | **Verified**: `MetalConstantBuffers` against all 2688 bound buffers' 9576 members' blob offsets and the MSL structs' layout, and against the 10172 default-block uniforms (53624 values) the GL device sets for the same draw in 570 GLCore variants (`MetalConstantBufferTests`). |
| P/Invokes against the Mac dylibs | **Verified, in the gate**, for both architectures. x86_64 (`--native-arch x86_64`): SimDLL 32/32, fmodstudio 571/571, steam_api 924/930 (the 6 are missing on Linux too). arm64 (`--native-arch arm64 --native-dir out/publish/osx-arm64`): the port's SimDLL 16/32 (the 16 kprofiler functions are never called, §3.4), fmodstudio and steam_api as on x86_64 from their arm64 slices, and BRUTAL's glfw 138/154 (the 16 are other platforms' accessors), freetype 15/15, stb 17/17, glad 2/2, with every dependency they link found. In both, cimgui, EOS, Rail and the encrypted-ticket libraries are not shipped, as expected. |
| Mac host paths (`GameLayout`, `Paths.ConfigureMac`, bundle native resolution) | **Verified** on fake `.app` trees (`GameLayoutTests`); the host reads the depot's bundle (`--check-assets`: 0 failures). `NativeLibrary.Load` and the user folders' real locations need a Mac. |
| Backend-neutral refactors (§2.3) | **Designed** (`metal-backend-design.md` §3), not done: they touch the per-frame render path. Covered, when done, by the GL e2e and screenshot suites. |
| Projection and flip logic | **Designed** (`metal-backend-design.md` §6); unit-testable once the neutral `ClipConventions` exists. |
| Headless player on Mac data **under Linux** | Not possible: SimDLL is Mach-O (the port runs on Linux as a `.so`, but the rest of the Mac bundle's plugins do not). |

**MSL compilation on Linux.** No toolchain exists. Apple's "Metal Developer Tools for Windows"
(`metal.exe`, login-gated, not redistributable) compiles MSL to metallib on Windows; whether it
runs under Wine is untested (https://developer.apple.com/metal/tools/,
https://github.com/libsdl-org/SDL_shadercross/issues/39). This machine is WSL on Windows, so the
Windows tools could run natively on the host side: a cheap experiment to compile all 945 MSL
programs offline, not done here. vkd3d and SPIRV-Cross generate MSL but don't validate it; no
open-source MSL front end exists.

### 6.2 What needs a Mac

Everything that loads SimDLL, Metal, Cocoa or the Steam dylib:
- the player itself, natively on arm64 with the port, which includes Harmony on osx-arm64 (§3.4);
- Rosetta behaviour of the x86_64 fallback (the CoreCLR hangs of §3.1);
- MSL compile times and pipeline-cache behaviour;
- Retina, full screen and multi-monitor;
- performance;
- the Steam launch path and Gatekeeper.

An Apple Silicon Mac is the target to test on: it runs the primary arm64 build and the Rosetta fallback. An Intel Mac
is a secondary check (different GPU families, AMD or Intel). The game's 2 GB `sharedassets0.assets.resS`
and the per-frame upload pattern make unified memory the friendlier case.

### 6.3 Hosted macOS CI

GitHub-hosted runners (https://github.com/actions/runner-images, 2026-10):
- `macos-26`/`macos-latest` and `macos-15` are arm64 with Rosetta installed by the image scripts;
- `macos-26-intel` and `macos-15-intel` are x86_64;
- macos-14 retires 2026-11-02;
- x86_64 runner images end after macos-15 retires in fall 2027.

They have a paravirtualised Metal GPU on arm64 that works but is fragile (WebGL stalls,
runner-images#14840; MPS failures, #9254). The Intel runners fault or hang on multisampled
rendering, and GitHub won't fix it (#14836).

**What could run there:**
- osx-arm64 build and unit tests natively (arm64 runner); osx-x64 under Rosetta or on an Intel runner;
- `ShaderSurvey check-metal`;
- a Metal smoke test that compiles every MSL variant and builds pipelines, the Metal equivalent
  of `--check-shaders` (`src/OniMoono.Host/ShaderCheck.cs`); avoid MSAA on Intel runners;
- the headless player (`--headless --frames N`) natively with onimaxxing-sim, and x86_64 under
  Rosetta with Klei's SimDLL, which exercises the platform port end to end without rendering;
- a few rendered frames and `--capture` screenshots, compared loosely: a paravirtual GPU will not
  match Linux pixels.

**Blocker for hosted CI.** All of these need the game's Mac depot on the runner. The game files
cannot be put in the repository or a public artifact, and fetching them needs SteamCMD with an
owning account's credentials. Fine for a private repository with secrets, still a licensing
judgement call. The repository has no CI today (`.github/` absent). A self-hosted Mac runner, or
a Mac mini on the e2e lock scheme, is the practical route for anything past the build.

---

## 7. Phased plan

Efforts are rough engineer-weeks for someone who knows this codebase, agent-assisted as now.
Phase 0 needs no Mac. The rest needs Mac hardware from Phase 1 on.

| Phase | Work | Needs a Mac | Effort |
|---|---|---|---|
| **0. Linux-side preparation** | Hoist backend-neutral logic out of `Gl/` (§2.3) behind a neutral program model: per-stage variants, a parameter list (name, kind, buffer, offset) filled by GL reflection or parameter blobs, render state and default textures. Make `ShaderPrograms` platform-parameterised (14 or 15) with per-stage selection. Port the parameter-blob reader from `ShaderSurvey` into the engine with tests. Add an osx-x64 cross-build to `scripts/check.sh`. | no | 3 |
| **1. Platform port** | `Paths` Mac branch (persistent, log and cache paths; dataPath = `Contents`); `.app` discovery; PlugIns bundle resolver; `Application.platform`/`SystemInfo` for macOS; facade-map check for the Mac `ScriptingAssemblies.json`; Steamworks.NET osx-arm64 and osx-x64; Mach-O Steam double; `setup.sh` Mac branch (osx-arm64 publish with onimaxxing-sim in `plugins/`, quarantine, ad-hoc signing of the host and the port). **Milestone:** the arm64 headless player loads a save and runs N frames natively with onimaxxing-sim, with Harmony's patches applied; Steam DLC checks pass. Then the x86_64 fallback under Rosetta with Klei's SimDLL, which needs x86_64 builds of glfw/freetype/png/zlib/stb (or BRUTAL's) and is where the CoreCLR-under-Rosetta hangs would show. | yes | 3–4 |
| **2. Metal backend** | Objective-C interop layer (function-pointer `objc_msgSend`, `_stret` on x86_64, autorelease pools per frame, about 150 selectors). Window with CAMetalLayer, drawable size and scale, present and vsync. `MetalDevice`: caps, textures and samplers, render textures, MSAA and resolve, meshes and vertex descriptors, constant-buffer ring, pipeline and depth-stencil caches, passes, clears, scissor and viewport, immediate mode, readback, GPU timing. Clip-space and RT-flip conventions (§2.2). **Milestone:** main menu, then a loaded colony, rendering correctly; e2e screenshots compared with Linux. | yes | 10–14 |
| **3. Shader loading from Mac bundles** | Load platform-14 programs: MSL plus function constants (`rp_output_remap_mask`, `has_base_vertex_instance`), per-stage selection, cbuffer packing from parameter blobs and common parameters, vertex attribute table, structured buffers for the particle variants. Compile cache, off-thread compilation and warm-up (`WarmupShaders`), optional `MTLBinaryArchive` in the cache folder. A Metal `--check-shaders`. Partly overlaps Phase 0 (model) and Phase 2 (pipelines). | yes (compile); model no | 3–5 |
| **4. Testing strategy** | CI: cross-build plus ShaderSurvey on Linux; a macOS job (private, game data via SteamCMD secrets or self-hosted) for build, unit tests, headless smoke, Metal shader check and a capture. Port the e2e driver's lock and lane scheme to a Mac host for screenshot and UI-dump comparisons against the Linux player and Klei's Mac player. Performance pass against Klei's Mac player (frame time on a large colony, as in `docs/research/u57-frame-inventory.md`). | yes | 2–3, then ongoing |

**Total: about 21–29 weeks (5–7 months) to a playable, tested Metal build native on Apple Silicon,
with the Rosetta build as the fallback.** Phase 2 is the dominant and least certain cost. An early GL 4.1 stopgap (§2.4) would
add 3–5 weeks and shorten the time to "something on screen" but not the total.

---

## 8. Main risks

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| R1 | **Rosetta's end.** General-purpose Rosetta ends after macOS 27; what remains targets "older unmaintained gaming titles". An x86_64 .NET player may or may not qualify; Klei's own player is in the same position. | Low for the primary build, High for the fallback | **Mitigated:** the primary build is native arm64 with onimaxxing-sim (§3.4), so only the x86_64 fallback depends on Rosetta. (Klei ships no arm64 SimDLL. An out-of-process x86_64 sim helper would need shared-memory placement of the sim's arrays, which managed code reads by raw pointer, `AC/Grid.cs:698-706`; not needed now.) |
| R2 | **CoreCLR hangs under Rosetta** (dotnet/runtime#136288, #133797, #111628, #89272). | Medium for the fallback only | Test the x86_64 build in Phase 1, after the arm64 milestone. Try workstation GC and `DOTNET_EnableWriteXorExecute=0`. Pin a .NET patch that passes; report upstream. |
| R3 | **Convention bugs**: z range, RT flip, winding, `_ProjectionParams`, readback orientation. Metal's MSL bakes in Unity's top-left conventions (§1.5); our engine models GL's only. | Medium | Implement them in one place in the device. Compare screenshots with the Linux GL player and with Klei's Mac player pass by pass (`--dump-render-textures`). |
| R4 | **MSL compile cost at load**: 945 programs, compiled from source. | Medium | Compile lazily per used variant, off the main thread, with a persistent pipeline archive; measure on hardware. Unity does the same with the same sources, so it is bounded by Klei's own load times. |
| R5 | **No Mac in the loop**: every Phase 1–4 change needs hardware; hosted CI cannot carry game data publicly and has fragile GPUs. | Medium | A dedicated Mac (Apple Silicon) as a self-hosted runner and e2e host. |
| R6 | **BRUTAL natives**: osx-arm64 only, minos 15. | Low | Fine for the arm64 build (verified complete, §3.4); minos 15 sets the arm64 build's minimum macOS. For the fallback, build x86_64 ourselves or request upstream. Keep the Mac device free of further BRUTAL native dependencies. |
| R7 | **Metal bindings maintenance**: hand-written interop is ours to maintain. | Low | Keep it to the subset used; metal-cpp as the reference for selectors and signatures. |
| R8 | **Mod compatibility**: Mac Assembly-CSharp lacks the dev-tool types; mods with native dylibs or Windows-only assumptions. | Low | Same as Klei's Mac player; out of scope. |
| R9 | **Performance**: the port's sim speed, which on Linux x86_64 gives 37% of Klei's frame rate on a large sim-bound map and 65% on U57 (§3.5); for the fallback, x86_64 JIT code and the sim translated by Rosetta. | Medium–High for the primary build | Measure in Phase 4 against Klei's Mac player, which runs under Rosetta. |
| R10 | **The port's parity through game updates.** The arm64 build is as correct as onimaxxing-sim is bit-exact with the current vanilla SimDLL. A game update that changes SimDLL can make the port diverge, with no error. | Medium | Upstream re-pins its session corpus against each new vanilla before the arm64 build takes the update; meanwhile the x86_64 build with Klei's new SimDLL is the one to use. `--check-natives` (in the gate) catches import changes, not behaviour changes. |
| R11 | **Harmony on osx-arm64**: the host's game patches, the game tweaks and every Harmony mod detour native code on arm64, which no ONI player has done on macOS (Klei's is x86_64). | Medium | First check on a Mac (§3.4). If it fails, the x86_64 fallback carries the build until Harmony is fixed upstream. |

---

## 9. Recommendation

**macOS support is feasible, and the data is unusually favourable.** The Mac depot ships every
shader as MSL source with complete, self-consistent reflection (1057/1057), and the same
GLCore programs we already run. Textures, fonts and assets match the Linux build byte for byte.
No game code branches on macOS. The engine's device seam is narrow. The hard parts are a new
Metal device of about the GL backend's size, and Unity's D3D/Metal-style clip and texture
conventions, which our engine has not needed so far.

Recommended course:

1. **Do Phase 0 now** (about 3 weeks, no Mac). Hoisting the backend-neutral logic out of `Gl/`
   and making the shader model platform- and stage-aware simplifies the GL device as well. It
   turns the Metal work into "write a device" rather than "split the renderer", and it is fully
   testable with the existing Linux suites. `ShaderSurvey check-metal`, the osx-arm64 and osx-x64
   cross-builds and both architectures' native checks are in the gate.
2. **Commit to Phases 1–4 only with an Apple Silicon Mac available** as a development and
   self-hosted test machine. Do the platform port first and reach the headless milestone, since
   that is where Harmony on arm64 (R11) and, for the fallback, the Rosetta and CoreCLR risks show. Then build the Metal backend fed by the
   shipped MSL (the preferred direction; MoltenVK and GL 4.1 are not worth it, §2.4).
3. **Build for arm64 with onimaxxing-sim; keep x86_64 as the fallback (R1, R10).** The port
   removes the dependency on Rosetta. Every other native the arm64 process loads already has an
   arm64 build (§3.4). The x86_64 build with Klei's SimDLL under Rosetta stays as the fallback:
   for game updates the port has not yet caught up with, and for anything arm64-specific (R11)
   that fails on a Mac. The price is a dependency on the port staying bit-exact with vanilla,
   which upstream verifies, not us. Before the port ships, two things have to land upstream:
   the handle-version fix, and a frame rate closer to Klei's SimDLL on large colonies (§3.5).

---

## Appendix: tools

`tools/ShaderSurvey` (committed). It references only `OniMoono.Assets` and needs no engine or
device:

```
dotnet build tools/ShaderSurvey -c Release
dotnet out/bin/ShaderSurvey/release/ShaderSurvey.dll summary DATADIR
dotnet out/bin/ShaderSurvey/release/ShaderSurvey.dll variants DATADIR > variants.tsv
dotnet out/bin/ShaderSurvey/release/ShaderSurvey.dll dump DATADIR 'Klei/BatchedAnimation' OUT 14
dotnet out/bin/ShaderSurvey/release/ShaderSurvey.dll params DATADIR 'Klei/BatchedAnimation' 14
dotnet out/bin/ShaderSurvey/release/ShaderSurvey.dll check-metal DATADIR
dotnet out/bin/ShaderSurvey/release/ShaderSurvey.dll compare-metal DATADIR [all|CLASS]
```

DATADIR is a data folder: `OxygenNotIncluded.app/Contents/Resources/Data` or
`OxygenNotIncluded_Data`. Platform 14 is Metal, 15 GLCore. The survey reads the data through the
readers in `src/OniMoono.Assets/Shaders/` that the engine uses. `tools/AssemblyDiff` compared the
two builds' assemblies (§1.6). The host's `--check-natives` (`--game` a game folder or an `.app`)
checks the P/Invokes against an install's plugins:

```
OniMoono --game OxygenNotIncluded.app --check-natives --native-arch x86_64
OniMoono --game OxygenNotIncluded.app --check-natives --native-arch arm64 --native-dir out/publish/osx-arm64
OniMoono --game OxygenNotIncluded.app --check-natives --native-arch arm64 --native-override SimDLL=libSimDLL-arm64-simthread.dylib
```

`--native-arch` picks the Mach-O slice. `--native-dir` names a publish folder: its BRUTAL dylibs
are checked, and its `plugins/` holds replacements. `--native-override` checks one library in
place of a plugin.
