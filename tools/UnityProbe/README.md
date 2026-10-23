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
