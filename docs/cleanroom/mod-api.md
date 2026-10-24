# Public API that Workshop mods use and the engine lacks: what to specify

Status: open. Nothing here is implemented. Each section is the input for a functional
specification, written by the specification side (README.md, "Roles") from Unity's public
documentation and from measurement in the original player, and then implemented from that
specification alone.

## How the list was made

`tools/ApiCheck` checks only what the game's own assemblies reference. A mod may call any public
Unity member, so `scripts/apisurface.sh` (`ApiCheck --surface`) also lists:

- every public type and member of the shipped Unity assemblies the engine replaces (the
  `UnityEngine.*Module` assemblies and `UnityEngine.UI`, `Unity.TextMeshPro`, `Unity.Timeline`,
  `Unity.InternalAPIEngineBridge.004`) that the engine and this project's packages lack, by name
  and signature, from metadata only;
- which of those the Workshop builds in `out/ws` reference.

The rest of the mod-referenced list was implemented from measurement in the original player
(`tools/UnityProbe`, `PROBE_SCENARIO=statics`): `GarbageCollector.isIncremental`, `TextAlignment`
and `TextMesh.alignment`. So were `Color`'s 157 named colours, which a reported build of
SkillsInfoScreen calls (`Color.darkGreen`), though no Workshop build in `out/ws` does. What is
left needs behaviour that a few values cannot pin down.

## 1. `GUI.tooltip` (IMGUI)

**Used by:** Customize and edit Duplicant traits (3687060621, rank 508), its IMGUI window.

**Interface:** `public static string GUI.tooltip { get; set; }` in `UnityEngine.IMGUIModule`.
Next to it, `protected static Rect GUI.tooltipRect { get; set; }` (protected, in a type whose
constructor is public).

**The specification must say** (extending `imgui.md`):
- what `tooltip` reads during each event of an `OnGUI` pass: the `GUIContent.tooltip` of the
  control under the mouse, of the control that has keyboard focus, or of the last control drawn;
- when it is set and when it is cleared: per control, per event, per window, per frame;
- what a set from user code does: does it last until the next control, or until the end of the
  event;
- how windows (`GUI.Window`, `GUILayout.Window`) and groups change it;
- its value outside `OnGUI`.

**To measure:** a probe window with two buttons that carry tooltips. Log `GUI.tooltip` after each
control and at the end of `OnGUI`, for each event type, with the mouse over each button, over
neither, and with a text field focused.

## 2. `UnityWebRequest` upload and download handlers

**Used by:** Mod Manager // by @Ony (1843965353, rank 26), when it asks Steam's Web API for
Workshop details (a POST body sent through `UploadHandlerRaw`, the answer read through
`DownloadHandlerBuffer`), and when it downloads a file (`DownloadHandlerFile`). Bottle emptier
extend (1885337332, rank 63) calls `UnityWebRequest.Post(string, string)` and sets
`uploadHandler`.

**Interface** (`UnityEngine.UnityWebRequestModule`, namespace `UnityEngine.Networking`):
- `class UploadHandler : IDisposable`: `byte[] data { get; }`, `string contentType { get; set; }`,
  `float progress { get; }`, `virtual void Dispose()`, and a finalizer.
- `sealed class UploadHandlerRaw : UploadHandler`: constructors `(byte[] data)`,
  `(NativeArray<byte> data, bool transferOwnership)`, `(NativeArray<byte>.ReadOnly data)`;
  `override void Dispose()`.
- `sealed class DownloadHandlerBuffer : DownloadHandler`: `()`, `static string
  GetContent(UnityWebRequest www)`, `override void Dispose()`, and a protected override of
  `GetNativeData()`.
- `sealed class DownloadHandlerFile : DownloadHandler`: `(string path)`, `(string path, bool
  append)`, `bool removeFileOnAbort { get; set; }`, and protected overrides of `GetNativeData()`,
  `GetData()` and `GetText()`.
- `UnityWebRequest`: `UploadHandler uploadHandler { get; set; }`; the constructors `(string url,
  string method, DownloadHandler, UploadHandler)` and the same with `Uri`; `static
  UnityWebRequest Post(...)` in its overloads (string or `Uri`, with a string body, with a body
  and a content type, with a `WWWForm`, with multipart sections with or without a boundary, and
  with a dictionary of fields); `PostWwwForm(string or Uri, string)`.

`DownloadHandler`, `UnityWebRequest`'s other members, and the request's transport exist
(`Networking/WebRequest.cs`).

**The specification must say:**
- for each `Post` overload, the method, the body bytes, the `Content-Type` header and which
  handlers the new request gets. In particular, does `Post(string, string)` send the string as
  form-encoded data or as given;
- what `data`, `contentType` and `progress` give before, during and after sending, and after
  `Dispose`;
- `UploadHandlerRaw`'s copy-or-own rules for each constructor;
- `DownloadHandlerBuffer`: what `data` and `text` give, and what `GetContent` gives for a request
  whose handler is not a buffer;
- `DownloadHandlerFile`:
  - when the file is created, truncated or appended to;
  - what happens to it on abort with `removeFileOnAbort` on and off;
  - what `data` and `text` give (Unity documents that they are not supported);
  - the error for a path that cannot be written;
- what happens when `uploadHandler` is set after `SendWebRequest`, or set twice;
- disposal through `disposeUploadHandlerOnDispose`.

**To measure:** a probe against a local HTTP server that echoes each request's method, headers
and body. Every `Post` overload, every handler, and a download that is aborted half way.

## 3. `NativeArray<T>` allocation

**Used by:** [Vanilla + DLC] Support Package+ OV3.0 (2784329372, rank 717):
`new NativeArray<T>(int length, Allocator allocator, NativeArrayOptions options)`.

**Interface** (`UnityEngine.CoreModule`, namespace `Unity.Collections`):
- the enums `Allocator` (Invalid 0, None 1, Temp 2, TempJob 3, Persistent 4, AudioKernel 5,
  Domain 6, FirstUserIndex 64);
- the enums `NativeArrayOptions` (UninitializedMemory 0, ClearMemory 1);
- the constructor above.

The engine has `NativeArray<T>` already (`Core/Collections.cs`), without this constructor.

**The specification must say:**
- which allocators a player build accepts;
- what an invalid allocator, a negative length, or a `T` that is not blittable throws, and with
  what message;
- whether `UninitializedMemory` may give non-zero memory, which a reimplementation may then leave
  zeroed;
- the lifetime rules a player enforces without the safety system: `Temp` at the end of the frame,
  `Dispose` twice, use after `Dispose`.

**To measure:** each allocator and option; the error cases; `Dispose` twice; reading after
`Dispose`.

## Not specified here

- **`Color`'s named colours** and the other values above: measured in the original player and
  implemented.
- **Members only stale mod builds reference.** They are not in the shipped assemblies, so they
  are not this Unity's API: old TextMeshPro's `TMP_FontAsset.fontInfo`, `characterDictionary`,
  `defaultFontAsset`, `TMP_TextElement.width`, `TMP_Asset.material` and `FaceInfo`, merged into
  old PLib copies; and uGUI's `InputField.onEndEdit` with its old type.
- **The rest of the public surface no mod uses** (about 2,800 types and 5,800 members, mostly UI
  Toolkit, physics, terrain, XR and analytics). `scripts/apisurface.sh` lists it. A member moves
  here when a mod starts using it.
