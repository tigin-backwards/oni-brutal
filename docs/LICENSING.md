# Unity-originated code and data

An inventory of what this engine still takes from Unity Technologies at run time, for deciding
what has to be replaced before the result can be distributed without Unity's player. It is a
technical inventory, not legal advice: licence terms must be checked against the exact package
versions shipped with the game.

## Replaced by this project (never loaded)

| Unity component | Replacement |
|---|---|
| `UnityPlayer.so` (the native engine) | `OniBrutal.Host`, `OniBrutal.Desktop` (BRUTAL GLFW/OpenGL) |
| `UnityEngine.dll`, the 77 `UnityEngine.*Module.dll` | `OniBrutal.UnityEngine` and generated forwarding facades |
| `Unity.InternalAPIEngineBridge.004.dll` | `src/Unity.InternalAPIEngineBridge.004` |
| Mono runtime and class libraries | stock .NET 10 CoreCLR |
| `Resources/UnityPlayer.png` | not used |

## Unity code still loaded from the game's `Managed/` folder

These are Unity *packages* compiled into the game build; the game's own code references them.

| Assembly | Role in the game |
|---|---|
| `UnityEngine.UI.dll` (uGUI) | every menu, button, panel, layout, scroll view, mask |
| `Unity.TextMeshPro.dll` | all text layout and rendering |
| `Unity.Timeline.dll` | referenced; the game ships no Timeline content |
| `Unity.Collections.dll`, `Unity.Collections.LowLevel.ILSupport.dll`, `Unity.Burst.dll`, `Unity.Burst.Unsafe.dll`, `Unity.Mathematics.dll`, `Unity.Profiling.Core.dll`, `Unity.MemoryProfiler.dll`, `Unity.Multiplayer.Center.Common.dll` | not referenced by any game assembly (only by each other): never loaded, can be left out. The game's `NativeArray` use is `UnityEngine.CoreModule`'s, which this engine implements |

Current versions of uGUI, TextMeshPro and Timeline are, to our knowledge,
distributed under the Unity Companion License (use only with the Unity engine); they are the
main candidates for replacement. Older uGUI source was published under MIT.

## Unity data the game reads

### Built-in resource files (`Resources/`)

Objects a full scripted session loaded from them (menus, new game, colony, every overlay, the
research screens), recorded with `--report-builtin-assets FILE`:

| File | Object |
|---|---|
| `unity default resources` | Font `LegacyRuntime` (with `Font Material`, `Font Texture`) |
| `unity default resources` | Shaders `GUI/Text Shader`, `Hidden/InternalErrorShader` |
| `unity default resources` | Meshes `Plane`, `Sphere` |
| `unity_builtin_extra` | Shader `Hidden/BlitCopy` |

The engine creates its own built-in textures (`UnityWhite` and the other default textures), so
none are read from these files.

### Unity shaders compiled into the game's asset files

Unity's standard shaders the game uses live in its own asset files (`sharedassets0.assets`,
`sharedassets2.assets`, `resources.assets`, `globalgamemanagers.assets`): `UI/Default`,
`UI/Default Desat`, `Sprites/Default`, `Sprites/Mask`, `Unlit/Transparent`, `Standard`,
`Legacy Shaders/*`, `Particles/*`, the `Hidden/*` internal shaders (Blit, GUI, video decode and
composite), and the TextMeshPro shaders (`TextMeshPro/Distance Field` and variants,
`TextMeshPro/Sprite`). Unity has published the source of its built-in shaders, under MIT as far
as we know, so they can be rebuilt from source; the TextMeshPro shaders follow the TextMeshPro
package's licence.

## Not Unity's

Klei's game code and assets; `libSimDLL.so`; FMOD (`libfmodstudio.so`, FMOD's own licence);
Steamworks (`libsteam_api.so`, Steamworks.NET under MIT); `cimgui.so` (Dear ImGui, MIT).
