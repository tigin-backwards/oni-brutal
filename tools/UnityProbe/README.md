# UnityProbe

A mod for the original game, built against the local install's assemblies, that measures how
Unity's player behaves where documentation is silent (lifecycle timing around `Destroy`,
reactivation of objects awaiting destruction, `OnApplicationQuit` order). The engine's behaviour
in those areas follows its output; `tests/OniBrutal.UnityEngine.Tests/LifecycleTests.cs` encodes
the results.

Use it only with a scratch copy of the game and a scratch profile (`XDG_CONFIG_HOME`), never the
player's own mods folder:

1. `dotnet build -c Release` (set `OniManaged` if the game is not at `~/projects/oni-linux`).
2. Copy `SemanticsProbe.dll` with a `mod.yaml` (`staticID: SemanticsProbe`) and a `mod_info.yaml`
   (`supportedContent: ALL`, `APIVersion: 2`) into the scratch profile's
   `Klei/Oxygen Not Included/mods/Local/SemanticsProbe/`.
3. Start the original once so it lists the mod in `mods/mods.json`, set the entry's `enabled` to
   `true` and `enabledForDlc` to `[""]`, and start it again.
4. Read the `PROBE` lines in `Player.log`.

What it records (each line prefixed `PROBE f<frame>`): lifecycle scenarios (Destroy, activation,
coroutines, start/late-update timing; Start order, scenarios 12 to 19; Destroy of objects whose
Start is pending, scenario 20, alone with `PROBE_SCENARIO=startdestroy`), the Duplicants' spawn
order after a save loads (`MINION`, `KSTART`: build against the original's own `Managed` with
`-p:OniManaged=…`, start the colony after `=== values: screen`), engine values (`VAL …`: formatting, maths, JSON,
hierarchy queries, Instantiate order, Instantiate of parts awaiting Destroy and the points in
the frame where Destroy is carried out, SendMessage, screen, AddComponent's refusals), and UI layout dumps (`UI …`) of the
front end 8 s after it loads and of the in-game overlay canvases once the clock has run for 120
frames. Compare the two players' outputs with frame numbers taken relative to each scenario
header.

With `PROBE_SCENARIO=canvas` only the canvas probe runs (`CanvasProbe.cs`, lines `CANVAS …`, at the
main menu): which disabled Behaviours the FindObjects family returns, and whether nested canvases
under a disabled root canvas (or an inactive one) draw, take raycasts and keep their root. Set
`PROBE_CAPTURE_DIR` for its screenshots, read back by pixel; `CanvasTests` and `SceneRootsTests`
encode the results.

Both players write the same version stamp for the mod (ARCHITECTURE D3), so one `mods.json`
serves both. In U59 that stamp is `692750779`, with `enabledForDlc: ["", "EXPANSION1_ID"]`;
with another stamp the game reinstalls the mod and disables it.

With `PROBE_SCENARIO=textwidth` only the text-width probe runs (`TextWidth.cs`, lines `TW …`,
10 s into the main menu, about two seconds of work): TMP's preferred width and height of short
labels in every NotoSans and GRAYSTROKE font asset at 12, 14 and 16 pt, regular and bold, set up
as `SetTextStyleSetting` does, with each character's origin and advance; in rects at three
pivots, under a `HorizontalLayoutGroup` and in a `LayoutElement`. Values are logged in "R"
format with their float bits and E2eDriver's two-decimal dump form, after a check of that
formatting itself. Compare the players with `tools/UnityProbe/tw-check.py diff ORIG OURS`, and
check the rounding rule with `tw-check.py rule LOG`; `docs/cleanroom/textmeshpro.md`
("Preferred sizes to the hundredth") records what it measured.

With `PROBE_SCENARIO=textoverflow` only the overflow probe runs (`TextOverflow.cs`, lines `OV …`,
10 s into the main menu): `isTextOverflowing`, `firstOverflowCharacterIndex`, `isTextTruncated`,
the `textInfo` counts and each character's code, visibility and line, for texts of white space
alone and with white space around a visible character, in NotoSans-Regular 14 pt and GRAYSTROKE
REGULAR SDF 12 pt. The rects run from 0 × 0 through the text's preferred size (a hundredth and a
thousandth under and over it) and one line of the font to 400 × 100, with Overflow, Truncate,
Ellipsis and Masking, wrapping on and off. Each text's layout in the 400 × 100 rect is logged
too (`metrics`: origins, advances, line ascenders and descenders). Compare the players with
`tools/UnityProbe/ov-check.py diff ORIG OURS`, and check the rule against a log with
`ov-check.py rule LOG`; `textmeshpro.md` ("Overflow at white space") records what it measured.
Before them it logs `CH` lines: further white space and control characters (CR, vertical tab,
form feed, U+0003, U+2000–U+200B, U+2028, U+2029, U+3000, U+FEFF, U+180E) alone and around
"A" in low rects. It also logs `TAB` lines: the fonts' tab settings and tabs after other
characters at two sizes, in rects 50, 400 and 1000 wide. `PROBE_OV_PART=chars` runs only
these.

With `PROBE_SCENARIO=inputlayout` only the input-field layout probe runs (`InputLayout.cs`, lines
`IL …`, 10 s into the main menu). It logs `TMP_InputField`'s layout element values (min,
preferred, flexible, priority) beside its text component's and placeholder's preferred sizes,
and `LayoutUtility`'s values and the rects. The fields are built in code as Klei's prefabs are,
and the cases vary the field's rect, the text rect, the viewport's anchors, offsets and mask
padding, the text's margins and text, the placeholder, the line type and the character limit.
Then Geyser Control's slider row and other layouts (a `HorizontalLayoutGroup` with height
control on and off, `ContentSizeFitter`s) are logged through four forced layout passes and three
frames. Check the rule with `tools/UnityProbe/il-check.py rule LOG`, and compare the players
with `il-check.py diff ORIG OURS`. `textmeshpro.md` ("The input field's layout values")
records what it measured.

With `PROBE_SCENARIO=inputtext` only the input-text probe runs (`InputText.cs`, lines `IT …`, 10 s
into the main menu, about a minute). It covers `KInputTextField`s built as the slider row's
`NumberInputField`, with text set by `text =`, `SetTextWithoutNotify` or the text component's
`text`, on inactive, active and focused fields, across `characterLimit`, `lineType` and
`contentType`, with one of them set after the text in some cases. Each field is followed
through a frame, enabling, activation, an edit (End, a typed "9", Backspace, through
`ProcessEvent`) and deselection. Each step logs the text, the label, the caret and the
`onValueChanged` / `onEndEdit` / `onSubmit` reports. Check the rule with
`tools/UnityProbe/it-check.py rule LOG`, and compare the players with `it-check.py diff ORIG
OURS`. `textmeshpro.md` ("Text set from code") records what it measured.

With `PROBE_SCENARIO=inputcaret` only the input-caret probe runs (`InputCaret.cs`, lines `IC …`,
10 s into the main menu, about a minute). `IC settings` lines list the loaded game fields'
`onFocusSelectAll`, `resetOnDeActivation` and line type. `IC end` lines follow
`KInputTextField`s through an edit and its end. The text is typed, typed after Ctrl+A and
Backspace, typed and then selected with Shift+Home, or set from code. The end is Return
through `ProcessEvent`, `OnSubmit`, `DeactivateInputField()` or `(true)`, deselection or
disabling, with `resetOnDeActivation` and `onFocusSelectAll` on and off, and with listeners that
do what Klei's `KInputField` does on `onEndEdit`. They log the caret, the selection and every
event with the caret it was sent with. `IC end x` lines do the same with real key presses, sent
to the player's window through XTest (libX11 and libXtst, as `scripts/e2e.py` types), ended by
Return, Escape or deselection. `IC cull` lines log the caret graphic's
`CanvasRenderer.cull` beside an `Image` with its rect. The caret is at its defaults
(`AssignPositioningIfNeeded` skipped by a Harmony prefix) or placed, under the field's own
`RectMask2D`, inside an outer one or outside it, never focused or focused. `PROBE_IC_PART=end`,
`x` or `cull` runs one part. Check the rules with `tools/UnityProbe/ic-check.py rule LOG`, and
compare the players with `ic-check.py diff ORIG OURS [end|x|cull]`. `textmeshpro.md` ("The
caret after an edit ends", "The caret graphic and RectMask2D") records what it measured.

With `PROBE_SCENARIO=fontasset` only the font-asset probe runs (`FontAsset.cs`, lines `FA …`, 10 s
into the main menu, a few seconds). It calls `TMP_FontAsset.CreateFontAsset` by family and style
name and by file path, with installed and missing families, other cases, empty and null names,
point sizes of 0 and below, and files that are not fonts. It logs what each call returns or
throws, and what it logs. For each asset it logs the face, atlas, material, creation settings
and tables. Then it uses an asset in a `TextMeshProUGUI` (a character to add, one the face
lacks) and logs the asset's character table. It also calls the engine's internal
`FontEngine.TryGetSystemFontReference` by reflection. `textmeshpro.md` ("Font assets from OS
fonts") records what it measured.

With `PROBE_SCENARIO=pointermove` only the pointer-move probe runs (`PointerMove.cs`, lines `PM
…`, 10 s into the main menu). It sets a `BaseInput` of its own as the current input module's
`inputOverride`, which moves the pointer and presses the left button frame by frame over a small
hierarchy of handlers. It logs every `OnPointerMove`, `OnPointerEnter` and `OnPointerExit` with
its event data. `ugui.md` ("Pointer move events") records what it measured.

With `PROBE_SCENARIO=scrollbar` only the scrollbar probe runs (`ScrollbarPress.cs`, lines `SB
…`, 10 s into the main menu, a few seconds). It builds a fresh `Scrollbar` per case (each
direction, several sizes, values and step counts) and presses its track or handle through the
current module's `inputOverride`, held for 1 to 60 frames, some moving while held. Each frame
logs the value and the handle's anchors, and each `onValueChanged` its argument. Then a
`Scrollbar` subclass runs both protected `ClickRepeat` overloads by hand and as coroutines,
idle and with a press held, and overrides `Update`; `PROBE_SB_PART=press` runs only the press
cases, `clickrepeat` only the subclass cases, and `clickrepeat2` only the held-press cases (a
value set while held, the event's position fields, a drag, a released press). `ugui.md`
("Scrollbar presses on the track") records what it measured.

With `PROBE_SCENARIO=layoutcull` only the layout-and-cull probe runs (`LayoutCull.cs`, lines `LC
…`, 10 s into the main menu, about a minute). `LC lay` lines follow a `VerticalLayoutGroup`
(a subclass counting its layout calls) with three children through one change at a time:
- a child's position, local position, scale, size, pivot or anchors, the group's position or
  size, or a drag that sets a child's position every frame;
- then a re-layout from elsewhere;
- across fitters, child size control and `ignoreLayout`.

Then come other ways to re-lay out, nested groups, and when `OnRectTransformDimensionsChange`
arrives for rects changed in `Canvas.willRenderCanvases`. `LC cull` lines give the offsets
across a `RectMask2D`'s edge at which `Image`s and `TextMeshProUGUI` texts of several kinds are
culled. These are at canvas scale 1 and 1.032, with padding and softness, each with its rect and
mesh bounds. Then come when the cull is re-evaluated, and a copy of Modify Difficulty Settings'
toggle row. Compare the players with `tools/UnityProbe/lc-check.py diff ORIG OURS
[--positions]`. `ugui.md` ("Moved children of layout groups", "RectMask2D culling") records what
it measured.

To run one of them in a player without the e2e runner's lanes, give it a scratch profile
(`XDG_CONFIG_HOME`) whose `mods/Local/SemanticsProbe/` holds the DLL and the two yaml files, with
the `mods.json` entry described above. Run the original player through `scripts/e2e.py`'s
`original_player` copy (Unity's player locks its folder), under `xvfb-run -a`, with
`PROBE_SCENARIO` set.
With `PROBE_SCENARIO=submesh` only the sub-mesh probe runs (`SubMesh.cs`, lines `SM …`, 10 s
into the main menu, about a minute). It covers the `TMP_SubMeshUI` children that texts in GRAYSTROKE
REGULAR SDF 12 pt and NotoSans-Regular 14 pt make for CJK characters and "▼" taken from
fallback fonts. Each step logs every sub-mesh's `raycastTarget`, `maskable`, activity, components, rect, mesh and depth.
It also logs whether `EventSystem.RaycastAll` reaches it. The steps cover the parent's
`raycastTarget` set before and after the sub-mesh exists, the sub-mesh's own `raycastTarget`
set, texts without fallback glyphs, enable and activation cycles of either object, relayouts,
pivot, anchors, `maskable` and a clone. `reg` lines follow one step each on a fresh sub-mesh,
`img` lines run the same steps on an `Image` as a control, and `hand` lines cover a
`TMP_SubMeshUI` added by hand. Then every loaded
`TMP_SubMeshUI` (the prefabs' saved ones) is listed, and copies of three prefab labels and of
the whole `LargeImpactorSequenceReticle` are checked. `textmeshpro.md` ("Sub-meshes and
raycasts") records what it measured.

With `PROBE_SCENARIO=wordcount` only the word probe runs (`WordCount.cs`, lines `WC …`, 10 s
into the main menu, about a minute). It logs `textInfo.wordCount`, each word's first and last
character index and count, each line's word count and the characters, in NotoSans-Regular 14 pt
and GRAYSTROKE REGULAR SDF 12 pt. The texts are hyphens, dashes, non-breaking spaces,
apostrophes, digits, CJK, tags and texts without words, then a sweep of single characters in
"abXcd", "Xab", "abX", "12X34" and alone. Wrapped texts follow, each distinct break from 1 wide to
past the preferred width. Check the rule with `tools/UnityProbe/wc-check.py rule LOG`, and
compare the players with `wc-check.py diff ORIG OURS`. `textmeshpro.md` ("Words and their
separators") records what it measured.

With `PROBE_SCENARIO=tallline` only the tall-line probe runs (`TallLine.cs`, lines `TL …`, 10 s
into the main menu, about ten seconds). It lays out texts whose line grows taller part-way: the
geyser names' U+2011 from a fallback font, `<size>`, `<voffset>` and spaces in `<size>`, on the
first line and on later ones, in GRAYSTROKE REGULAR SDF 14 and 12 pt and NotoSans-Regular
14 pt, with Overflow, Truncate and Ellipsis, wrapping on and off. It sweeps the rect's height
from 0 to 70 and logs each distinct result (`isTextOverflowing`, `firstOverflowCharacterIndex`,
`isTextTruncated`, the counts, each record and each line's metrics), each text's layout in a
tall rect (`metrics`), and the details screen's title in its own rect. Check the rule with
`tools/UnityProbe/tl-check.py rule LOG`, and compare the players with `tl-check.py diff ORIG
OURS`. `textmeshpro.md` ("Lines that grow part-way") records what it measured.

With `PROBE_SCENARIO=emptytext` only the empty-text probe runs (`EmptyText.cs`, lines `ET …`, 10 s
into the main menu, about a minute). It lays out texts with nothing visible: "", a space, texts
of rich-text tags alone ("<b></b>", "<b> </b>", "<br>") and U+200B, in NotoSans-Regular 16 pt
and GRAYSTROKE REGULAR SDF 12 pt, in rects 95.7 wide and 0 to 100 high (one line of the font
and a hundredth under and over it among them), in every overflow mode but Linked, wrapping on
and off, auto-size off and on (`fresh` lines, a new text per case). Then "kg" and "AB\nCD" are
laid out in a rect they overflow or fit, the text set to "", a space, "<b></b>" or null and laid
out by `ForceMeshUpdate` or by the frame's rebuild, and the rect resized (`after` lines); and
auto-size with `fontSizeMax` above and below `fontSize` (`automax` lines). Each logs
`isTextOverflowing`, `firstOverflowCharacterIndex`, `isTextTruncated`, the `textInfo` counts,
the font size and the preferred size. Check the rule with `tools/UnityProbe/et-check.py rule
LOG`, and compare the players with `et-check.py diff ORIG OURS`. `textmeshpro.md` ("Texts with
nothing to lay out") records what it measured.

With `PROBE_SCENARIO=childactive` only the child-activation probe runs (`ChildActivation.cs`, lines
`CA …`, 10 s into the main menu, a few seconds). A `GridLayoutGroup` (a subclass counting its
layout calls and messages) holds nine children; some start inactive and have never been laid
out. The children are bare rects with an `Image` child, bare rects alone, `Image`s or
`LayoutElement`s, and the grid is either a root group or nested in two `VerticalLayoutGroup`s
with a `ContentSizeFitter`, as the build menu nests it. One step at a time: a child appears,
one disappears, all swap after a reorder, a child's own child is toggled, the grid component is
disabled. Each logs the messages the grid and the children receive, every
`RectTransform.reapplyDrivenProperties`, the layout rebuild queue (read from the registry by
reflection) and the children's positions, through two frames. Measured 2026-10-23: `SetActive`
raises `reapplyDrivenProperties` for each RectTransform whose `activeInHierarchy` changed, the
child before its parent, before `OnEnable`/`OnDisable`, with no
`OnRectTransformDimensionsChange`; the group is queued at once and re-lays out in that frame.
`ActivatedLayoutChildTests` encodes it.

With `PROBE_SCENARIO=childdims` only the child-dimensions probe runs (`ChildDimensions.cs`,
lines `CD …`, 10 s into the main menu, about a minute). A rect with a child, a grandchild and a
stretched child, each with a spy, is moved, resized, scaled, rotated or re-pivoted: in a
coroutine, in `Canvas.willRenderCanvases` or during the layout rebuild; with and without
graphics on it; on canvases at scale 1 and 0.86, pixel perfect or not; then as a child of a
`HorizontalLayoutGroup`, and a copy of the main menu's news row. Each message is logged with the
phase it came after. `PROBE_CD_PART=extra` runs the pixel-perfect details instead: which objects
are told (a `CanvasRenderer` alone, disabled, transparent or culled graphics, a layout element),
several moves in a frame, a drag, values set to themselves, new, cloned, activated and
reparented objects, a move at the end of the frame, and nested canvases that override
`pixelPerfect`. `ugui.md` ("Moves on pixel-perfect canvases") records what it measured, and
`PixelPerfectMoveTests` encodes it.

With `PROBE_SCENARIO=floatprec` only the float-precision probe runs (`FloatPrecision.cs`, lines
`VAL fp …`, at mod load; the full run includes it in its values): float expressions whose
float32 and double results differ, each line saying which one the player gave. Its header
records what it measured: the original rounds float arithmetic to float32 only on stores.

With `PROBE_SCENARIO=findorder` only the find-order probe runs (`FindOrder.cs`, lines `FO …`, 10 s
into the main menu): the order of `Resources.FindObjectsOfTypeAll`. It logs every `LocText` the
call returns (instance id, scene, activity, pivot, path) and whether the ids ascend, then
components and ScriptableObjects made in a known order, some destroyed and others made after.
The original returns ascending instance ids: the newest run-time object first, the objects
loaded from files last. `FindObjectsOrderTests` encodes the result.

With `PROBE_SCENARIO=newobject` only the `new` probe runs (`NewObject.cs`, lines `NEW …`, at the
main menu): what `new` gives for a ScriptableObject subclass, a bare ScriptableObject and a
MonoBehaviour (whether it is alive, its instance id, name, hide flags and `ToString`, which of
Awake and OnEnable run and when, what reaches the log), then Instantiate, DestroyImmediate and
Destroy on it. `ScriptableObjectNewTests` encodes the results.

With `PROBE_SCENARIO=statics` only the statics probe runs (`StaticValues.cs`, lines `SV …`, at mod
load): every public static property and field of `Color` and `Color32`, found by reflection (so
each player lists what it has), with the components' float bits; `GarbageCollector.isIncremental`;
and a new `TextMesh`'s `alignment` and what reads back after each value is set. Compare the
players with `tools/UnityProbe/sv-check.py diff ORIG OURS` (bits, not the "R" text), and print
the engine's colour table from the original's log with `sv-check.py cs LOG`. Measured
2026-10-23: 157 named colours, none on `Color32`; `isIncremental` False; alignment Left by
default and any value read back as set. `ModApiTests` encodes them.

With `PROBE_SCENARIO=logging` only the logging probe runs (`Logging.cs`, lines `LOG …`, 10 s into
the main menu): each log type through `UnityEngine.Debug`, `Debug.unityLogger` and Klei's `Debug`,
between `LOG case NAME begin` and `end` lines so that the text the player writes for it lies in
between, with what `Application.logMessageReceived` got (condition and stack trace); then
`Application.GetStackTraceLogType` per type, the same calls with every type set to None and to
Full, and exceptions left uncaught in an Update and a coroutine. Measured 2026-10-24 (U57): every
type reports ScriptOnly, yet messages (Log, Warning, Error, an Exception-typed message) get no
stack trace, in the log or the callbacks. An exception's text is its innermost "Type: message",
the frames of each exception in Mono's format (`  at A.B () [0x00000] in <mvid>:0 `, then
"Rethrow as Outer: message" and the outer one's), the managed frames of the logging call in
Unity's (`UnityEngine.Debug:LogException(Exception)`, `A:B(String, Action)`, a nested type named
`Namespace.<>c`), and an empty line; the callbacks get the frames as `A.B () (at <mvid>:0)` and the
call from `UnityEngine.Debug`'s frame on. An exception from Update has its own frames alone. None
gives no trace for exceptions either; Full gives native frames. `LogTracesTests` encodes it.

With `PROBE_SCENARIO=alignment` only the alignment probe runs (`Alignment.cs`, lines `AL …`, 10 s
into the main menu): copies of loaded `LocText`s, one per value of their stored
`m_textAlignment`, and new `TextMeshProUGUI`s, given an alignment before and after they first
wake (copied under an inactive parent, as Chain Tool's menu does), each step logging the
alignment, its halves and the stored value. `textmeshpro.md` ("Alignment set before a text
first wakes") records what it measured.

With `PROBE_SCENARIO=tooltip` only the tooltip probe runs (`Tooltip.cs`, lines `TT …`, 10 s
into the main menu, about a minute and a half). One behaviour draws IMGUI controls with and
without `GUIContent` tooltips (GUI and GUILayout controls, a group, an area, scroll views) and
opens three windows (`GUI.Window`, a `GUILayout.Window` laid out as Customize Duplicant traits'
editor, one over another); a second behaviour only reads. The real mouse is moved over each
control through XTest, and every OnGUI call and window function logs `GUI.tooltip` at its
start, after each control and at its end, per event, with `tooltipRect`; Update, LateUpdate
and the end of the frame read it too. Then the same with every call resetting the value first,
values set from code, keyboard focus, and presses held and dragged off. The XTest window must be
the player's (it focuses the largest mapped window). Check the rules with
`tools/UnityProbe/tt-check.py rule LOG`, and compare the players with `tt-check.py diff ORIG
OURS`. `docs/cleanroom/imgui.md` ("GUI.tooltip") records what it measured.

With `PROBE_SCENARIO=guilayout` only the GUILayout probe runs (`GuiLayout.cs`, lines `GL …`, 10 s
into the main menu, about 80 s; `PROBE_GL_PART=size`, `wsize`, `order` or `mm` runs one part).
`size` logs the default skin's styles and the rects GUILayout gives: every control kind with
one to three lines, other font sizes, paddings and margins, sequences of entries, groups and
areas with and without a style, scroll views and window content. `wsize` logs
`GUILayout.Window`'s returned rect against the rect passed in, with options and content or size
changing between frames. `order` logs every OnGUI call and window function per event, for two
behaviours with windows added, dropped, brought to the front or back, focused and clicked
(through XTest, as the tooltip probe; the EventSystem is disabled meanwhile). `mm` changes the
GUILayout calls of one event against the Layout before it (entries added, removed, regrouped;
in OnGUI, scroll views and windows; during Repaint, MouseDown, MouseUp and `Used`, and through a
button's handler as Customize Duplicant traits' SAVE and DEL), and logs each call's rect or
exception and what the player logs. Check the rules with `tools/UnityProbe/gl-check.py rule
LOG`, and compare the players with `gl-check.py diff ORIG OURS`. `docs/cleanroom/imgui.md`
("GUILayout, measured") records what it measured.

## UI semantics (`UiSemantics.cs`, `UIS` lines)

Ten seconds into the front end, after the front-end dump, it builds small hierarchies on an
overlay canvas of its own (scale 1) and logs frame by frame. Each line is `PROBE f<frame> UIS
<section> <label> <phase> <values>`. A phase is `now` (right after the step), then per frame
`U` (an Update), `co` (a coroutine's `yield return null`), `L` (a LateUpdate), `R`
(`Canvas.willRenderCanvases`, after the canvas rebuild) and `E` (the end of the frame).

- `scroll`: ScrollRect's `normalizedPosition` and its scrollbars' `value`/`size`. Content
  smaller than, equal to and larger than the viewport, by pivot and offset; the movement
  types; one axis only; scaled content; the content resized at run time; hairline offsets
  and random exactly-fitting layouts. `scroll raw` lines give the inputs at full precision
  (the view's rect, the content's world corners and those corners in the view's space).
  `zoom anchoring` reproduces Blueprints Expanded's pointer-anchored preview zoom.
- `input`: when `TMP_InputField` fills its text component and makes and positions its caret.
  Fields are built inactive, active, with the text component assigned later, outside any
  canvas, cloned, and with PLib's replacement of `AssignPositioningIfNeeded` emulated.
  `UiSemantics.Install` hooks that private method with Harmony when it exists (`UIS hooks`
  says whether it does) and logs each call (`APIN`).
- `depth`: `CanvasRenderer.absoluteDepth`/`relativeDepth` and `Graphic.depth` for renderers
  of every kind on one canvas. Then one change at a time, to see which changes renumber it.

Run it in both players with the same DLL; in ours, `--shipped-ui-packages` separates the
packages from the engine. Then compare:

```
tools/UnityProbe/uis-check.py diff ORIG/Player.log OURS/stdout.txt [scroll|input|depth]
tools/UnityProbe/uis-check.py fit LOG          # the ScrollRect rule against the raw lines
tools/UnityProbe/uis-check.py inputs ORIG/Player.log OURS/stdout.txt
```

`docs/cleanroom/ugui.md` (ScrollRect) and `textmeshpro.md` (TMP_InputField) record what it
measured. Elastic lines differ by frame timing, and so do the `time` lines.
