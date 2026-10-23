# TextMeshPro (Unity.TextMeshPro): functional specification

This project's replacement of TextMeshPro, the assembly `Unity.TextMeshPro`
(`src/Unity.TextMeshPro`, namespace `TMPro`). Every text in the game is a TextMeshPro text:
Klei's `LocText` derives from `TextMeshProUGUI`. The shipped package is a build modified by
Klei: the game calls a Klei addition, `TextMeshProUGUI.KForceUpdateDirty()`. The rules are in
`docs/cleanroom/README.md`, and PLAN M9 describes the stage.

## Interface

**Generated shapes:** `src/Unity.TextMeshPro/Generated/` holds the type declarations, enums,
interfaces, delegates and serialized fields. `scripts/regen-packages.sh` regenerates them from
the shipped package's metadata. Do not edit them; write the rest in partial declarations.

**What the data contains:** the serialized fields are how the game's data stores its assets.
`out/data-scripts.tsv` lists them: 29 `TMP_FontAsset`, 13 `TMP_SpriteAsset`, 4
`TMP_ColorGradient`, one `TMP_Settings`, one `TMP_StyleSheet`, 45 `TextMeshProUGUI`, 16
`TMP_SubMeshUI`, 5 `TMP_InputField` and 2 `TMP_Dropdown`. Thousands more `LocText`
components carry `TextMeshProUGUI`'s fields.

**Required members:** `out/surface/package-members.tsv`, the `Unity.TextMeshPro` rows, lists
what the game calls and overrides.

`LocText`:
- hides `Awake`, `Start` and `OnDestroy` with its own methods that call the base ones;
- overrides `text` (get and set), `SetText(string)`, `OnEnable` and the protected
  `GenerateTextMesh()`;
- calls `KForceUpdateDirty()`.

`KInputTextField` derives from `TMP_InputField` and overrides `LateUpdate`. Two classes derive
from `TMP_InputValidator`.

The game also:
- reads `textInfo`: characters, lines, words, links and mesh info;
- uses `TMP_TextUtilities.FindIntersectingLink/Character/Word/Line` and
  `IsIntersectingRectTransform` for clickable links;
- edits `meshInfo` vertex data and calls `UpdateVertexData` and `UpdateGeometry`;
- adds styles at run time (`TMP_StyleSheet.instance.AddStyle(new TMP_Style(name, open, close))`
  then `RefreshStyles()`);
- swaps font assets for localization;
- creates a dynamic font asset (`TMP_FontAsset.CreateFontAsset(Font, …)`).

`scripts/build-packages.sh` runs ApiCheck for the game's references; it must report nothing
missing.

**The engine:** it is this project's own code (`src/OniBrutal.UnityEngine`) and gives
`Unity.TextMeshPro` access to its internals. The parts used here:
- **`UnityEngine.TextCore`:** `FaceInfo`, `Glyph`, `GlyphRect`, `GlyphMetrics`, and
  `TextCore.LowLevel.FontEngine`, a FreeType-backed font engine that loads font files,
  renders glyphs to atlases in SDF and bitmap modes, and returns the OpenType adjustment
  tables;
- **meshes, materials and shader property IDs;**
- **`CanvasRenderer`.**

The shaders, such as `TextMeshPro/Distance Field` and `TextMeshPro/Sprite`, are the game's own
assets. They define what the vertex channels and material properties mean.

**uGUI:** this package builds on uGUI (`MaskableGraphic`, `CanvasUpdateRegistry`,
`LayoutUtility`, `ILayoutElement`, `Selectable`, the event system). Until this project's uGUI
is finished, compile against the shipped `UnityEngine.UI.dll` as a metadata reference only.
At run time, `scripts/m9-compare.sh` loads your TextMeshPro with the shipped uGUI when
out/packages holds only `Unity.TextMeshPro.dll`. Use only uGUI's public and protected API.

## Behaviour

These follow the TextMeshPro documentation: the user guide, the rich-text tags reference and
the scripting API. What the game relies on is spelled out below.

### Assets

**`TMP_Asset`:** `hashCode`, `material`, `materialHashCode` and `version`.

**`TMP_FontAsset`:**
- **Contents:**
  - `faceInfo`;
  - `glyphTable` and `characterTable` (`TMP_Character`: unicode, glyph index, scale);
  - `atlasTextures`, `atlasWidth`, `atlasHeight`, `atlasPadding`, `atlasRenderMode`;
  - `material`, `fallbackFontAssetTable`, `fontFeatureTable` (the glyph-pair adjustment
    records used for kerning), `atlasPopulationMode` (Static or Dynamic) and
    `creationSettings`;
  - the style weights: `normalStyle`, `normalSpacingOffset`, `boldStyle`,
    `boldSpacing`, `italicStyle`, `tabSize`;
  - the font weight table.
- **Lookup tables:** loaded assets build their lookups on first use, from `characterTable`
  by unicode to glyph.
- **Fallbacks:** a character missing from an asset is looked up in its fallback list
  (depth-first, each asset once). Then the global fallbacks (`TMP_Settings.fallbackFontAssets`)
  are tried, then the default font asset. A dynamic asset adds missing characters by rendering
  them into its atlas through the engine's `FontEngine` before it falls back. A character that
  cannot be found anywhere becomes the missing-glyph character (`TMP_Settings.missingGlyphCharacter`,
  `□` by default) of the primary asset, with a warning when `TMP_Settings.warningsDisabled` is
  false.
- **`CreateFontAsset(Font, samplingPointSize, atlasPadding, renderMode, atlasWidth,
  atlasHeight[, populationMode, enableMultiAtlas])`:**
  - It creates a dynamic asset over the font file, with an empty atlas texture (Alpha8) and a
    material on `TextMeshPro/Distance Field` (SDF modes) or the bitmap shader.
  - Characters are added on demand.
  - `TryAddCharacters` and `HasCharacter` are public.

**`TMP_SpriteAsset`:** a sprite sheet with `spriteCharacterTable` and `spriteGlyphTable`,
`spriteSheet` and `material`. Sprites are looked up by name, by index, or by the unicode of a
sprite character. Fallback sprite assets apply.

**`TMP_StyleSheet` and `TMP_Style`:**
- Named styles with an opening and a closing definition, which are rich text inserted for
  `<style="name">` … `</style>`.
- `instance` is the default sheet from `TMP_Settings`.
- `GetStyle(hash or name)`, `AddStyle`, `RefreshStyles()` (rebuilds the lookup).
- `TMP_Style(name, opening, closing)`.

**`TMP_Settings`:**
- The project asset, found through `Resources.Load` at `TMP Settings`.
- **Contents:** `defaultFontAsset`, `fallbackFontAssets`, `defaultSpriteAsset`,
  `defaultStyleSheet`, `defaultFontSize`, `enableWordWrapping`, `enableKerning`,
  `enableExtraPadding`, `enableTintAllSprites`, `enableParseEscapeCharacters`,
  `missingGlyphCharacter`, `warningsDisabled` and `useModernHangulLineBreakingRules`.
- **Line breaking rules:** the leading and following characters, from text assets.
- Read the asset's serialized fields; the game's data holds the one used.

**`TMP_ColorGradient`:** four corner colours and a mode (Single, HorizontalGradient,
VerticalGradient, FourCornersGradient).

**`ShaderUtilities`:**
- The shader property IDs as public static ints, among them `ID_GlowPower`, which the game
  reads, `ID_FaceColor`, `ID_OutlineColor`, `ID_OutlineWidth`, `ID_GradientScale`,
  `ID_ScaleRatio_A`/`B`/`C`, `ID_WeightNormal`, `ID_WeightBold`, `ID_ClipRect`,
  `ID_StencilID` and `ID_StencilComp`.
- `GetPadding(material, enableExtraPadding, isBold)`: the extra pixels the material's outline,
  dilate, glow and underlay need around a glyph, in atlas units.
- `UpdateShaderRatios(material)`, `IsMaskingEnabled` and `GetFontStyle`.

### Text layout (`TMP_Text`)

`TMP_Text` (a `MaskableGraphic`) holds the text and its settings, and lays the text out into
`textInfo` and a mesh. `TextMeshProUGUI` draws through its `CanvasRenderer`, with sub-meshes
(`TMP_SubMeshUI` children) for characters from other materials: fallback fonts and sprites.
`TextMeshPro` draws through a `MeshRenderer` in the world; the game references only its
transform.

**Settings:**
- **Content and fonts:**
  - `text`, `SetText(string)`, `SetText(format, args…)`, which replaces `{0}`, `{1}`,
    `{0:2}`-style placeholders with float arguments, and `SetCharArray`;
  - `font` (a `TMP_FontAsset`), `fontSharedMaterial`, `fontMaterial` (an instance) and
    `fontSize`.
- **Auto-size:** `enableAutoSizing`, `fontSizeMin`, `fontSizeMax`, `characterWidthAdjustment`
  and `lineSpacingAdjustment`.
- **Style and colour:**
  - `fontStyle` (FontStyles flags: Bold, Italic, Underline, LowerCase, UpperCase, SmallCaps,
    Strikethrough, Superscript, Subscript, Highlight) and `fontWeight`;
  - `color`, `alpha`, `enableVertexGradient`, `colorGradient` and `overrideColorTags`.
- **Spacing and placement:**
  - `characterSpacing`, `wordSpacing`, `lineSpacing` and `paragraphSpacing`;
  - `alignment` (TextAlignmentOptions);
  - `textWrappingMode` (and the older `enableWordWrapping`) with `wordWrappingRatios`;
  - `overflowMode`: Overflow, Ellipsis, Masking, Truncate, ScrollRect, Page or Linked;
  - `margin`.
- **Rendering options:** `enableKerning`, `extraPadding`, `richText`, `isRightToLeftText`,
  `isOrthographic`, `isOverlay` and `isTextObjectScaleStatic`.
- **Visibility:** `maxVisibleCharacters`, `maxVisibleWords`, `maxVisibleLines`,
  `firstVisibleCharacter` and `pageToDisplay`.
- `fontFeatures`, the OTL feature tags; `kern` is enabled with kerning.
- `autoSizeTextContainer`, and `TextContainer` (obsolete, but referenced).

**When it lays out:**
- A property change sets `havePropertiesChanged` and dirties the vertices and the layout.
- During the canvas graphic rebuild (`Rebuild(PreRender)`), a text with properties changed is
  laid out and its mesh regenerated by the protected virtual `GenerateTextMesh()`. LocText
  overrides that method.
- `ForceMeshUpdate(ignoreActiveState, forceTextReparsing)` does it immediately.
- The game's `KForceUpdateDirty()` marks the text as changed, so it regenerates in the next
  rebuild: `havePropertiesChanged` is set, and the vertices and layout are dirtied.
- After a layout, `havePropertiesChanged` is false, and `TMPro_EventManager.TEXT_CHANGED_EVENT`
  (a `FastAction<Object>`: `Add`, `Remove`, `Call`) is called with the text object.

**Parsing:**
- `richText` enables the documented tags:
  - weight and style: `<b>`, `<i>`, `<u>`, `<s>`, `<sup>`, `<sub>`, `<mark=…>`,
    `<lowercase>`, `<uppercase>`, `<smallcaps>`, `<font-weight>`;
  - fonts and colour: `<color=…>` (names and #RRGGBB[AA]), `<alpha>`, `<font="…">`,
    `<material>`, `<gradient>`;
  - size and spacing: `<size=…>` (px, %, em, ±), `<cspace>`, `<mspace>`, `<space>`,
    `<line-height>`, `<line-indent>`, `<indent>`, `<margin>`, `<pos>`, `<width>`;
  - placement: `<align>`, `<voffset>`, `<rotate>`;
  - other content: `<sprite …>`, `<link="…">`, `<nobr>`, `<noparse>`, `<style>`, `<br>`,
    `<page>`, `<zwsp>`.
- Closing tags pop the stacks. Unknown tags are drawn as text.
- Escape sequences (`\n`, `\t`, `\uXXXX`) are parsed when `TMP_Settings.enableParseEscapeCharacters`
  is set.
- UTF-16 surrogate pairs are one character.
- Measure what the game's texts use: the localization strings use links, colours, sizes, styles
  and sprites.

**Placing characters:**
- **Glyph scale:** the scale is `fontSize / faceInfo.pointSize × faceInfo.scale`, times the
  character's scale, with sub and superscript adjustments.
- **Advance:** each character advances by its glyph's horizontal advance, plus:
  - `characterSpacing` (in em/100), and `wordSpacing` for spaces;
  - the font's normal or bold spacing offset;
  - the kerning pair adjustment from `fontFeatureTable` when kerning is on.
- **Monospacing:** `<mspace>` centres each glyph in the fixed width instead.
- **Tabs:** they advance to the next multiple of `tabSize` × the face's `tabWidth`, scaled
  to the size, from the line's start (measured: "Further characters" under "Overflow at
  white space" below).
- **Lines:**
  - Lines break at `\n` and when a word overflows the width with wrapping on.
  - Words are separated by whitespace and the break opportunities of CJK characters. The
    line-breaking rules decide which characters cannot start or end a line.
  - A word wider than the line is broken at characters.
  - `<nobr>` prevents breaks.
- **Line height:** the line height is the font's line height, scaled to the size, plus
  `lineSpacing` (em/100). `paragraphSpacing` is added after `\n`.
- **Vertical alignment:** Top, Middle, Bottom, Baseline, Midline and Capline use the font's
  ascender, descender and cap height, as documented. Geometry alignment uses the mesh bounds.
- **Horizontal alignment:**
  - Left, Center, Right.
  - Justified: the spaces stretch, except on the last line.
  - Flush: every line stretches.
  - Each line's offset is computed from its width, or its visible width, as documented.
- **Overflow:**
  - Ellipsis: text that does not fit ends in "…" (U+2026) where it is cut.
  - Truncate: the rest is hidden.
  - Page: pages split at the height, and `pageToDisplay` picks one.
  - Masking: the material clips at the rect.
  - Linked: the overflow continues in `linkedTextComponent`.
  - `isTextOverflowing` and `firstOverflowCharacterIndex` report it.
- **Auto-size:** with `enableAutoSizing`, the size is searched between `fontSizeMin` and
  `fontSizeMax` for the largest one at which the text fits. Character width and line spacing
  are adjusted within their limits first, as documented. The size used is reported by
  `fontSize`.
- **Preferred sizes:**
  - `preferredWidth` and `preferredHeight` (and `GetPreferredValues(…)`) are the size of the
    laid-out text: without wrapping for the width, wrapped at the given width for the height.
    Margins are included. Both are taken to the next hundredth above, measured from the
    line's start rather than the rect (see "Preferred sizes to the hundredth" below).
  - They are the `ILayoutElement` values: minimum 0, flexible -1.
  - `renderedWidth` and `renderedHeight` are those of the visible characters.
  - `bounds` and `textBounds` are the mesh's bounds and the text's.

**The mesh**, per character quad (four vertices: bottom-left, top-left, top-right,
bottom-right):
- **Position:** the glyph's rect, with padding for the material (`ShaderUtilities.GetPadding`)
  and an italic shear of `italicStyle` (×0.01) per unit height.
- **uv0:**
  - xy: the glyph's atlas UVs, including the padding;
  - w: the SDF scale the shader expects, from `fontSize`, the canvas's lossy scale and the
    gradient scale. It is negative for bold, which the shader reads as a weight switch.
- **uv1** (`uvs2`): the face and outline texture coordinates, mapped over the line or the
  character as `horizontalMapping` and `verticalMapping` say. By default they are 0 to 1 over
  the character.
- **Colour:** the vertex colour, from `color`, colour tags, the vertex gradient and `alpha`,
  as Color32.
- **Normal and tangent:** (0, 0, −1) and (−1, 0, 0, 1).
- **Decorations:** underline and strikethrough are drawn with the font's underline glyph, as
  three quads per run. Highlight (`<mark>`) is quads behind the text.
- **Sprites** use the sprite asset's material, in a sub-mesh, tinted only with
  `tintAllSprites` or the tag's `tint=1`.

Use `--dump-ui-vertices` to measure exactly what the shipped package generates for a given
text, channel by channel, and compare it with yours. The shaders are the shipped ones, so the
channel meanings must match exactly.

**`textInfo` (`TMP_TextInfo`):**
- **Counts:** `characterCount`, `spriteCount`, `spaceCount`, `wordCount`, `linkCount`,
  `lineCount`, `pageCount` and `materialCount`.
- **Arrays:**
  - `characterInfo` (`TMP_CharacterInfo`): character, index, element type, material
    reference index, vertex index, the corners (bottomLeft, topLeft, topRight, bottomRight),
    origin, ascender, baseline, descender, xAdvance, aspect ratio, scale, colour, style,
    visibility, page and line numbers;
  - `wordInfo` (first and last character index, character count, `GetWord()`);
  - `lineInfo` (character counts, first and last character indices including the visible
    ones, the extents, ascender, baseline, descender, width, line height, alignment);
  - `linkInfo` (`GetLinkID()`, `GetLinkText()`, the character range);
  - `pageInfo`;
  - `meshInfo` (`TMP_MeshInfo` per material: `mesh`, `vertices`, `uvs0`, `uvs2`, `colors32`,
    `normals`, `tangents`, `triangles`, `vertexCount`, `SwapVertexData`, `SortGeometry`).
- **Copies:** `CopyMeshInfoVertexData()` returns copies.
- **Arrays grow** in the documented way. Entries beyond the counts are stale data, which
  callers must not read.

**After a layout:**
- **`UpdateVertexData([flags])`** pushes the `meshInfo` arrays back to the meshes and renderers.
  The game changes vertex colours and positions this way.
- **`UpdateGeometry(mesh, index)`** sets a mesh on the renderer of that index.

**Materials and masking (UGUI):**
- `fontSharedMaterial` is the material for rendering, with `GetModifiedMaterial` applying the
  stencil for `Mask` (stencil materials) as for any `MaskableGraphic`.
- Under a `RectMask2D`, the clip goes through `canvasRenderer.EnableRectClipping` and the
  sub-meshes follow. The `_ClipRect` and softness come from the canvas renderer.
- `TMP_SubMeshUI` children are created on demand, named after their material, hidden in the
  hierarchy as Unity does (`HideFlags`). They are reused, and disabled when unused.

**`TMP_TextUtilities`:**
- **`FindIntersectingCharacter(text, screenPos, camera, visibleOnly)`:** the index of the
  character whose quad contains the point, or -1.
- **`FindIntersectingWord`, `FindIntersectingLine`, `FindIntersectingLink`:** likewise for
  words, lines and links. The link test uses the link's character quads.
- **`IsIntersectingRectTransform(rect, position, camera)`.**
- The conversions use `RectTransformUtility` with the canvas's camera; null for overlay
  canvases.

### Input field and dropdown

**`TMP_InputField`:**
- **Settings:**
  - `textViewport`, `textComponent` (a TMP text), `placeholder` (a Graphic) and `text`;
  - `characterLimit`, `contentType`, `lineType`, `inputType`, `keyboardType`,
    `characterValidation` and `inputValidator` (a `TMP_InputValidator`, whose
    `Validate(ref text, ref pos, ch)` the game overrides);
  - `caretBlinkRate`, `caretWidth`, `selectionColor`, `readOnly`, `richText`,
    `onFocusSelectAll`, `resetOnDeActivation`, `restoreOriginalTextOnEscape`,
    `isRichTextEditingAllowed` and `pointSize`.
- **Events:** `onValueChanged`, `onEndEdit`, `onSubmit`, `onSelect`, `onDeselect`,
  `onTextSelection`, `onEndTextSelection`, `onTouchScreenKeyboardStatusChanged`, and the
  public `Action onFocus`, which the game assigns.
- **Activation:**
  - `ActivateInputField()` starts editing in the next `LateUpdate`, with focus and caret.
  - That start is a private instance method, `void ActivateInputFieldInternal()`, which mods
    patch by name (Favorite Buildings, to know when the player is typing). Measured in the
    original player:
    - After `ActivateInputField()`, it runs once in the same frame's `LateUpdate`, with
      `isFocused` still false; `isFocused` is true from the next frame.
    - Selecting the field (`EventSystem.SetSelectedGameObject`) with `shouldActivateOnSelect`
      on runs it the same way; with it off, selecting neither runs it nor focuses the field.
    - `ActivateInputField()` on a field already focused does not run it again.
    - `ActivateInputField()` while the GameObject is inactive does nothing: it does not run,
      not even once the object is active again, and the field stays unfocused.
  - `DeactivateInputField()` ends it. It calls `DeactivateInputField(bool clearSelection)` with
    false, so a patch of either overload sees every deactivation. Deselecting the field and
    disabling its GameObject both call `DeactivateInputField()`.
  - Deactivating a focused field that the event system has selected also deselects it, even
    with `clearSelection` false. Measured order: `DeactivateInputField()` →
    `DeactivateInputField(false)`, and within that the deselection runs `OnDeselect`, which calls
    `DeactivateInputField()` → `DeactivateInputField(false)` again before the outer call
    returns. Afterwards `currentSelectedGameObject` is no longer the field, so selecting it
    again activates it as above. On a field that is not focused (and not selected),
    `DeactivateInputField(true)` changes no selection and makes no nested call.
  - Clicking the field selects and activates it.
  - Deselecting it (`OnDeselect`) deactivates it.
- **Editing:**
  - While focused, the field drains the keyboard events with `Event.PopEvent` in
    `OnUpdateSelected` (measured: not in `LateUpdate`), which the event system's input
    module sends to the selected object every frame.
  - It handles the documented editing keys: arrows, Home and End, Backspace and Delete,
    Ctrl+A/C/V/X, Enter per line type, and Escape.
  - Typed characters are validated: by `characterValidation`, or by `inputValidator`, which
    the game uses.
  - `onValueChanged` fires on every change. Enter in a single-line field fires `onSubmit`
    and `onEndEdit`, then deactivates.
- **Drawing:**
  - The caret and selection are drawn by a child graphic in the viewport.
  - The text is scrolled to keep the caret visible.
  - The placeholder shows when the text is empty.
- **Layout element:** the field's preferred size is its text component's plus its viewport's
  offsets. The rects do not enter it. See "The input field's layout values" below.

KInputTextField overrides `LateUpdate` and calls the base implementation, so the base's
`LateUpdate` must do its share of the work above (starting a requested activation, caret
blinking, scrolling the text).

**`TMP_InputField`: when the label and the caret are set up, measured** (2026-10-22,
`tools/UnityProbe`, the `UIS input` lines; fields built in code on an overlay canvas, phase by
phase: right after each step, then Update, a coroutine's `yield return null`, LateUpdate,
`Canvas.willRenderCanvases` and the end of the frame). "The label" is the text component's
`text`.
- **On enable, with a text component:**
  - The label becomes the field's text followed by U+200B (zero-width space) at once, in
    `OnEnable`: `""` gives `"​"`, a text set while inactive (`"hello"`) gives
    `"hello​"`.
  - It is not laid out then. `textInfo.characterCount` stays 0 and `isTextOverflowing` false
    through that frame's LateUpdate. They become 1 and true for the empty field in the canvas
    rebuild before rendering, as for any changed text. (This implementation lays it out at
    once: `characterCount` 1 right after `SetActive(true)`. Not matched.)
- **The caret is made in that same `OnEnable`,** when the field has no caret yet:
  - It is a new GameObject `Caret` (hideFlags `DontSave`) with `RectTransform`,
    `CanvasRenderer`, `TMP_SelectionCaret` and a `LayoutElement` that ignores layout, made the
    first child of `textViewport`.
  - It is made at the world origin and keeps that world position as the viewport's child, with
    the RectTransform defaults: anchors (0.5, 0.5), size 100 × 100, pivot (0.5, 0.5). Under a
    viewport centred at world (640, 459.5) its anchoredPosition is (−640, −459.5).
  - Still within that `OnEnable`, the field calls the private instance method
    `void AssignPositioningIfNeeded()` once. It copies the text component's RectTransform onto
    the caret's: `localPosition`, `localRotation`, `localScale`, `anchorMin`, `anchorMax`,
    `anchoredPosition`, `sizeDelta` and `pivot`. Measured with a Harmony hook: on entry the
    caret's anchors are (0.5, 0.5)–(0.5, 0.5) with size 100 × 100; on return they are the text's,
    (0, 0)–(1, 1) with size 0 × 0.
  - The caret's RectTransform is held, from its creation, in the private instance field
    `RectTransform caretRectTrans`. It is already set when `AssignPositioningIfNeeded` runs.
  - `AssignPositioningIfNeeded` runs nowhere else. It does not run on a later enable (the caret
    exists), on select, focus or `ActivateInputField`, on typing, on setting `text`, or on
    deselect. It is called once per caret made: for the template and for a clone, which both
    make one.
  - **Why the names are interface:** PLib's `TextMeshProPatcher`, in 36 of the 74 decompiled
    mods in `out/local-tools/moddecomp`, finds `TMP_InputField.AssignPositioningIfNeeded` by
    name. It puts a Harmony prefix on it that takes `___caretRectTrans` and
    `___m_TextComponent`, and a postfix on `OnEnable` that takes `___m_VerticalScrollbar` and
    `___m_TextComponent`. When the
    caret's eight values differ from the text's, the prefix skips the method. It starts a
    coroutine on the field instead, which copies them at the end of the frame. Without the
    method, the patch is not applied at all, and the caret is positioned at once. With the
    method but without the field, Harmony fails to patch, and PLib logs "Unable to patch
    TextMeshPro bug" (inferred from Harmony and PLib's code, not measured). So both names must
    exist, with these types.
  - **The result in the original with PLib:** measured by reproducing that prefix in the probe.
    A field hidden again in the frame it was shown loses the coroutine. Its caret then keeps
    the defaults (100 × 100, centred, at the world origin) for good, through being shown again,
    focus and typing. Sweep By Type's filter field is in this state in the original
    (`Caret` rect −50, −50, 100, 100); in ours it fills the text area.
  - A field enabled without a text component makes no caret. It makes none when a text
    component is assigned later, nor on activation, but makes one on its next enable.
- **Assigning `textComponent`** (and `placeholder`) to an enabled field leaves the text
  component alone: its `""` stays `""`. (This implementation writes `"​"` into it then.
  Not matched; this is Duplicant Stat Selector's search field, whose component is assigned at
  run time.)
- **Setting `text`:**
  - to the value it already holds changes nothing: that field's label stays `""`;
  - to another value writes the label (`"ab​"`) and lays it out at once
    (`characterCount` 3);
  - back to `""` gives `"​"`.
- **Activation** of a field whose label was never written (selecting it, with
  `shouldActivateOnSelect`, or `ActivateInputField()`) writes `"​"` in the LateUpdate that
  activates it. It is seen in that frame's `willRenderCanvases`, with `isFocused` true.
- **Not measured:** `ForceLabelUpdate()` on a field whose label was never written (measured only
  after it had been written: no change); the caret's layer; a text component on an inactive
  GameObject at enable.
- **Two text behaviours the same probe measured** (they apply to every `TextMeshProUGUI`, not only
  input fields; probe output `uis-check.py diff … input`):
  - An empty text in a rect lower than its font's line reports `isTextOverflowing` false. For
    auto-size it still counts as not fitting.
  - A text with no canvas above it is not laid out. It stays dirty and is laid out in the first
    canvas rebuild after it is parented under a canvas (a field built in code, then parented).

**`TMP_Dropdown`:** like uGUI's `Dropdown`, with TMP texts:
- **Contents:** `options` (`OptionData`: text and image), `value` and `onValueChanged`.
- **Showing the list:** `Show()` builds the list from `template` under a new canvas with a
  blocker. Clicking an option sets `value` and hides the list.
- **`RefreshShownValue`** updates the caption.
- `set_options` replaces the options and refreshes the caption.

### Miscellaneous

- **`TMPro_ExtensionMethods`:** `Compare(Vector3, Vector3, accuracy)` and
  `Compare(Quaternion, …)` compare components rounded to `accuracy` decimal places;
  `Tint(Color32, float)` multiplies RGB. Colour helpers mods call (Paint Your Pipes multiplies
  pipe colours), all public static extension methods, measured in the original player:
  - `Color32 Multiply(this Color32 c1, Color32 c2)`: each channel, alpha included, is
    ⌊c1·c2 / 255⌋ (integer division; 1×255 = 1, 1×128 = 0, 128×128 = 64, 255×255 = 255).
  - `Color32 Tint(this Color32 c1, Color32 c2)`: the same as `Multiply`, alpha included.
  - `Color MinAlpha(this Color c1, Color c2)`: c1's RGB with the smaller of the two alphas.
  - `bool Compare(this Color32 a, Color32 b)` and `bool Compare(this Color a, Color b)`: every
    channel exactly equal (a difference of 1e-7 in one float channel is unequal).
    `CompareRGB` (both overloads) is the same without alpha.
- **`FastAction<T>`:** an ordered set of delegates, called in insertion order.
- **`TMP_Text` exposes** `rectTransform`, `transform` (cached), `mesh` and the `isVolumetricText`
  family as documented.
- **`TMP_Text.ignoreRectMaskCulling`** (get and set, a `bool`, false by default) keeps the value it
  is given and changes nothing else. The shipped package marks it
  `[Obsolete("This does nothing. It was added to prevent PLib from breaking after upgrading to Unity 6.3")]`
  (a warning, not an error); keep the attribute and its message. Old PLib, embedded in many mods,
  sets it on input fields' text.

### Measured on the shipped package

Found by running the implementation's tests and the differential runs against the shipped
package (M9). They refine the sections above.
- **Sub-meshes:** `TMP_SubMeshUI` children are named "New Game Object". A sub-mesh that
  already holds a material the layout uses keeps that material's number; unused ones stay.
- **Characters:** character corners include the padding. Small caps keep the text's size and
  line metrics. Sprites don't raise the line's ascender, and `spriteCount` stays 0.
- **Zero-width space:** draws nothing, except where white space is preserved (input fields):
  there its quad goes in the spare slot without being counted. If the font lacks the
  character, it uses an empty glyph of the text's own font, not a fallback's.
- **Buffers:** grow to the next power of two of (quads + 1). Grown slots have colour zero,
  new buffers are white.
- **SDF scale refresh:** checked in `Canvas.willRenderCanvases` against the lossy scale's y
  for which the SDF scale (uv0.w) was last set. Only a change beyond a factor of 1.25 either
  way acts (0.8 and 1.25 do not); it multiplies uv0.w by the ratio and uploads, without a new
  layout. A scale to or from 0 regenerates the text. Texts with `isTextObjectScaleStatic` are
  skipped. Only screen-space-overlay canvases divide the SDF scale by `scaleFactor`.
- **Spacing and advances:** character, word and line spacing are in hundredths of the text's
  font size, whatever `<size>` says. `xAdvance` includes the spacing after the character;
  `lineInfo.maxAdvance` and `length` follow it.
- **Bold and italic:** with extra padding, a bold quad is as wide as a regular one. Italic
  glyphs lean around half the cap height, not the baseline.
- **Words:** a text starting with white space or a symbol records an empty word (0, -1) first.
- **Unwrapped measurement:** a rect without width doesn't wrap, and
  `GetPreferredValues(w, h)` returns the unwrapped width. A text added to a default 100 × 100
  rect makes it 200 × 50. A text never laid out reports one material.
- **Caret:** its material is set when the field is enabled; its CanvasRenderer is added before
  the caret graphic. The caret graphic is created only when the field is enabled with a text
  component. A field given its text component after it was enabled has no caret until it is
  enabled again (measured: a mod builds its field in code, then assigns the viewport and text).
  Canvas updates and frames don't create the caret, and nothing is logged; a rebuild with no
  caret draws none.
- **Stored alignment:** values 0–15 in the game's data use the oldest alignment table; a value
  set at run time is split into its horizontal and vertical bytes.
- **Truncation:** Truncate and Ellipsis stop where the first line that doesn't fit begins,
  leaving a blank record. Truncation keeps the part of a wrapped word that fits.
- **Auto-size** doesn't choose a size that would break a word, and `preferredHeight` uses the
  auto-sized size. A margin change keeps the preferred sizes until the text changes.
- **`textBounds`** spans the characters' advances.
- **Input field:** `inputValidator` doesn't change `characterValidation`.
- **Dynamic font assets** use the "TextMeshPro/Mobile/Distance Field" shader.
- **A saved material of another atlas:** when a text is loaded, a saved `m_sharedMaterial`
  whose main texture is not its font's atlas texture is replaced by the font's material.
  A saved material that does draw the font's atlas is kept, whichever material it is, and so
  is a material set at run time, whatever its atlas. Re-enabling a text does not check
  again. (Found in QA: the Duplicant cards' expectation lines are saved with a font other than
  their material's, RobotoCondensed-Regular Material; kept, the glyphs sampled the wrong
  atlas. Test `ASavedMaterialOfAnotherAtlasGivesWayToTheFontsMaterial`.)
- **Timeline:** `TimelineClip` has no public constructor.

### Corrections from the defect review (2026-10-20)

A review of this implementation found these behaviours missing or wrong. Each must first be
measured on the shipped package as a black box (a test, or `--dump-ui` / `--dump-ui-vertices` in
a differential run, with `--shipped-ui-packages`), then matched. Where the measurement differs
from what is described here, the measurement wins and this section is corrected.

1. **Horizontal overflow with wrapping off.** With word wrapping off and `Ellipsis` or
   `Truncate`, a line wider than the rect is cut at the rect's width: the characters that fit
   are kept, and for `Ellipsis` the last visible characters give way to "…" so it fits. Lines
   above it are not affected, and a single line is never removed entirely. Measure a one-line
   label slightly wider than its rect, and a two-line text whose second line is too wide.
   *Measured* (14 pt GRAYSTROKE, "QUIT GAME", 68.05 wide, in a 65.05 rect; test
   `WithoutWrappingTruncateAndEllipsisCutALineAtTheRectsWidth`):
   - The cut is at the first visible character whose glyph advance passes the width ("E"):
     Truncate keeps "QUIT GAM" and ends with the blank record (9 records, one line).
   - Ellipsis replaces the last character that fit ("M") with "…": "QUIT GA…" and the blank
     record. The ellipsis is drawn at the text font's scale even when its glyph comes from a
     fallback (NotoNaskhArabic, 32 pt: scale 14/124, 2.131 wide, where a typed "…" from the same
     fallback is 8.26). This holds for the vertical Ellipsis too.
   - Lines above the cut are not affected; the cut line ends the text: in "QUIT GAME
     LONGER\nAB" the second line is not shown.
   - The correction above is wrong on one point: when even a line's first character does not
     fit, the line is not kept. On the first line nothing is shown (0 characters, 0 lines) in
     both modes; on a later line Truncate leaves the blank record alone on it, and Ellipsis
     shows nothing at all.
   - A cut at the width, and any line wider than the rect without wrapping (Overflow mode
     too), is not an overflow: `isTextOverflowing` is false and `firstOverflowCharacterIndex`
     -1; `isTextTruncated` is true.
   - Not matched: on the cut line the shipped package counts the replaced character in
     `visibleCharacterCount` besides the ellipsis, and its `length` includes the padding.
2. **Several atlas textures.** A glyph's `atlasIndex` selects the texture it is drawn from. A
   font asset whose glyphs span several atlases (a dynamic font with multi-atlas support after
   its first atlas fills, or a shipped asset with `atlasIndex > 0` glyphs) draws each glyph from
   its own atlas: one sub-mesh per atlas, each with a material whose main texture is that atlas.
   Measure with a dynamic font asset small enough to fill its first atlas.
   *Measured* (LiberationSans at 48 pt, padding 5, 64 × 64 atlases, multi-atlas on, "ABCDEF" at
   20 pt; test `GlyphsInLaterAtlasesDrawFromTheirOwnAtlas`): each atlas holds one letter, so
   the six letters open atlases 1–6. Every character whose glyph is in atlas k > 0 is drawn in
   a sub-mesh whose material is a copy of the text's material with atlas k as its main
   texture (`characterInfo.material` and `meshInfo[n].material` too): `materialCount` 7, the
   text's own mesh empty, six sub-meshes of one quad. Glyphs in atlas 0 stay in the text's
   own mesh. Also seen, not matched: the shipped package adds "…" (U+2026) and "_" (U+005F) to
   a dynamic asset at its first layout, whatever the overflow mode, so they take the first
   atlas (without multi-atlas, the letters then come from a fallback); its `atlasTextures`
   array grows by doubling (8 entries for 7 atlases, the last null); and the asset's material
   and textures are unnamed.
3. **Rejected characters over a selection.** Typing a character that validation rejects
   (`characterValidation`, `contentType`, `onValidateInput`, a custom `inputValidator`)
   changes nothing: the selection stays selected and the text unchanged. Validation sees the
   text as it is before the selection is replaced. Measure in a field with Integer validation
   and its text selected, typing a letter.
   *Measured* (test `ARejectedCharacterLeavesTheSelection`):
   - Integer validation, "123" all selected, "x" typed: the text, the selection and
     `onValueChanged` are untouched; likewise with "23" selected, with `contentType`
     IntegerNumber, and when `onValidateInput` returns 0.
   - The last sentence of the correction is wrong: validation sees the text **with the
     selection removed**, at the position where the selection began. "-" typed over the
     selected "-" of "-12" is accepted (it is at the start of "12"), giving "-12"; ".2"
     selected in "1.2" with Decimal and "." typed gives "1."; `onValidateInput` is called with
     ("a", 1) for "bc" selected in "abc", and with ("", 0) for all of it.
   - A custom validator (`CustomValidator` with `inputValidator`) is the exception: the
     selection is removed before it is called and stays removed when it rejects the
     character ("BC" selected in "ABC", "x" rejected: "A", caret 1, without
     `onValueChanged`).
   - With validation None, `inputValidator` is not used; `characterLimit` counts the text
     with the selection removed ("1234" all selected with a limit of 4 accepts "5").
   - Ctrl+A puts the selection's anchor at the end and the caret at the start
     (`selectionAnchorPosition` 3, `caretPosition` 0 for "123").
4. **Line breaks after hyphens.** Measure whether a line may break after `-` (U+002D) and after
   a soft hyphen (U+00AD), whether the soft hyphen is drawn at a break and hidden elsewhere,
   and whether a non-breaking hyphen (U+2011) prevents a break; implement what is measured.
   *Measured* (14 pt GRAYSTROKE, "aaaa" 30.23 wide, "aaaa-" 37.79; test
   `LinesMayBreakAfterHyphensAndSoftHyphens`):
   - A line may break after "-": "aa-bb-cc-dd" in 38.79 gives "aa-" / "bb-" / "cc-dd" (where
     "aa-bb" would fit), and "aaaa-bbbb" in 30.73 gives "aaaa" / "-" / "bbbb".
   - Not after the hyphen U+2010 or the non-breaking hyphen U+2011 (both from the fallback
     here): "aaaa‑bbbb" in 30.73 breaks between characters, "aaaa" / "‑bbb" / "b".
   - The soft hyphen U+00AD takes no room (the preferred width of "aaaa", U+00AD, "bbbb" is
     that of "aaaabbbb", 60.46) and is not drawn, its corners at its origin. A line may break
     after it; there it is drawn with its glyph's advance (3.472) when that fits the line
     (38.79), and stays hidden when it does not (30.73), the break the same.
   - Nor after the en and em dashes (U+2013, U+2014) or "/": those words break between
     characters.
   - Not matched: a line that ends where the text wraps (or is cut) has a `length` that
     includes the material padding of its last character (0.141 here); a text's last line
     does not.
5. **Caret and selection with rich text.** The input field keeps string positions (in its
   text) and character indices (in the laid-out text) apart. Typed tags, escapes and surrogate
   pairs make them differ; the caret and the selection are drawn at the laid-out character for
   their string position, and moving up and down starts from that character. Measure a field
   with rich text on, after typing `a<b>c`, an escape, and an emoji.
   *Measured* (test `TheCaretIsAtTheLaidOutCharacterOfItsStringPosition`):
   - `caretPosition`, `selectionAnchorPosition` and `selectionFocusPosition` are character
     indices; `stringPosition`, `selectionStringAnchorPosition` and
     `selectionStringFocusPosition` string positions. "a<b>c" lays out as "a", "c" and the end
     marker (source indices 0, 4, 5): at the end the caret is 2 and the string position 5.
     A string position is at the first character whose source index is not before it.
     Setting `caretPosition` to 1 sets the string position to 4 (that character's index).
   - Left moves to the character before, at its source index: 5 → 4 → 0 (over the tag).
     Right moves past the source of the caret's character: 0 → 1 (before the tag) → 5.
     Shift+Left from the end: anchor 2 / 5, focus 1 / 4.
   - A surrogate pair is one character of two positions: "a😀b" (the emoji drawn as the
     missing glyph □) ends at caret 3, string position 4; Left gives 2 / 3, then 1 / 1. An
     escape typed as "\" and "n" is parsed into a line feed, one character of two positions:
     "a\nb" ends at 3 / 4 on the second line; Left gives 2 / 3, then 1 / 1.
   - Up and down start from the caret's character: in the multi-line "ab<b>cd" + Enter +
     "ef", Up from the end (7 / 10) goes to "c" (2 / 5), the nearest character above, and
     Down back. Up on the first line goes to the start.
   - The caret quad is `caretWidth` wide at its character's origin, from the ascender to the
     descender of the character before it on the same line (its own at a line's start): after
     "a" in "a😀b" it is as tall as "a", after "□" as the fallback's □. The selection runs
     from its first character's origin to its last's advance.
   - The text component is laid out when a key changes the text, so `textInfo` and the
     character indices are current right after it. Counted with `TEXT_CHANGED_EVENT` (test
     `EachChangeLaysTheTextOutOnceAndAPasteOnce`): three characters typed in one frame lay it
     out three times; a paste of 3,000 characters raises `onValueChanged` 3,000 times and lays
     it out once; setting `text` lays it out once. The canvas rebuild after any of these does
     not lay it out again (a text laid out by `ForceMeshUpdate` is not regenerated in the
     next rebuild unless it changes again).
   - Not matched: setting `caretPosition` in code does not redraw the shipped package's
     caret until the next change; this implementation redraws it at the next rebuild.

**Measured after the review of these corrections:**
- **The record where truncation stops** is U+0003, not drawn, with the source index of the
  character it stopped at, at the pen position there (origin and advance), on its line. An
  ellipsis has the source index of the character it replaced. In a 100-wide input field whose
  text truncates (Truncate or Ellipsis), "QUIT GAME NOW" stops before "W": the text is not
  scrolled, the caret at the end is character 12 (the record, index 12) and is drawn at its
  origin; Left goes to 11 / 11 (the ellipsis, or "O"); End goes to 12 / 12, the index of the
  last laid-out character rather than the text's length. Test
  `InATruncatedTextTheCaretEndsWhereTheTextStops`.
- **Soft hyphens and the line's height:** a hidden soft hyphen takes no part in its line's
  ascender, descender or line height ("aaaa", U+00AD, "bbbb" is 14.70 high, as "aaaabbbb",
  though its soft hyphen comes from a taller fallback). One drawn where the line breaks does
  (the line is 23.8 high) but is not counted when the line is checked against the rect's
  height. When Truncate or Ellipsis continue that line (38.79 wide, 20 high), the soft
  hyphen is hidden again: "aaaa", "b", then the blank record (Ellipsis: "aaaa…"). Test
  `AHiddenSoftHyphenTakesNoPartInItsLinesHeight`. Not matched: the shipped package
  sometimes reports a drawn soft hyphen's `character` as "-" (U+002D) and its line 0.035
  lower.
- **Password fields** show one asterisk per UTF-16 unit, and the shipped package's caret
  steps per unit, between the halves of a surrogate pair (a, 😀, b: Left from 4 gives 3, 2,
  1); a character typed there goes between them, and Backspace removes one unit, leaving a
  lone high surrogate. This implementation steps the same way but removes a whole pair with
  Backspace and Delete (deliberately not matched: it would leave a lone surrogate). In a
  plain field both packages step and delete by pairs.
- **Not measured** (multi-atlas fonts only, which the game does not use): which material a
  decoration (underline, strikethrough, highlight) takes when its run starts in atlas k; and
  the per-atlas material copies are made once per source material and texture, not
  refreshed when the source material changes later.

### Preferred sizes to the hundredth (measured 2026-10-23)

Found in mod play-tests: overlay legend rows (Gas Overlay, Rooms Expanded, Pipe Flow Overlay),
Bigger Building Menu's search bar and a Butcher Station label were 0.01 narrower in this
implementation (67.25 for 67.26, 100 for 100.01, 13.75 for 13.76), and a label of one space
was 0 × 19.04 where the shipped package's is 0.01 × 0.01. The UI dump's formatting is not
the cause: `Math.Round(v, 2)` and `"0.##"` give the same strings in both players for every
float tested, and the layout groups add the same child values to the same sums. The first
value that differs is the text's own `preferredWidth`.

Measured with `PROBE_SCENARIO=textwidth` (tools/UnityProbe, `TextWidth.cs`): 19 labels in
every NotoSans and GRAYSTROKE font asset, at 12, 14 and 16 pt, regular and bold, set up as
`SetTextStyleSetting` does (kerning, extra padding, no wrapping), in a 400 × 40 rect at
three pivots, under a `HorizontalLayoutGroup` beside a 24-wide icon, and copied into a
`LayoutElement`. `tools/UnityProbe/tw-check.py` compares two logs and checks the rule below.
- **The rule.** Let x be the widest line's width, as a float: the pen at the end of its last
  visible character (that character's origin plus its glyph's advance, without the
  character or style spacing after it), measured from where the line's pen starts.
  `preferredWidth` is (⌊x × 100⌋ + 1) / 100, with x × 100 taken exactly (in double, not
  rounded to a float first), stored as the nearest float. So a width already on a
  hundredth goes up by 0.01 (67.25 gives 67.26, 139 gives 139.01), and any other goes to
  the next hundredth (85.09375 gives 85.1). The rule reproduces all 648 regular-style
  texts at pivot 0, including white space, against the shipped package; ceiling to the
  hundredth, as this implementation did before, misses 21 of the 576 that are not white
  space, and the 72 that are.
- **Implemented.** This implementation follows the rule: in the same probe it reproduces it
  on all 648 regular-style texts, at every pivot and under the layout group. Against the
  shipped package 9 texts (27 at the three pivots, regular and bold) still differ by 0.01,
  all where a character's pen is one float step off at a scale that is not a power of two
  (see "Also seen" below).
- **Not tied to the rect.** The value does not depend on where the rect is: the same text at
  pivot 0.5, 0 and 0.37, and as a child of the layout group, has the same bits in all 1,368
  cases. This implementation used to sum the pens in the rect's coordinates, so its value
  moved with the rect: 68 texts changed between the pivots and 61 under the layout group
  (GRAYSTROKE REGULAR 16 pt "Hydrogen" was 67.51 alone and 67.5 in the row), and "i" in
  NotoSansCJKjp 12 pt was 3.31 where the shipped package's pen is the float 3.3
  (3.2999999523) and gives 3.3. It now measures from the line's own pen start and gives the
  same bits at every pivot and in the row.
- **White space.** A text of only white space (" ", "  ") has no visible character, so x is
  0: its preferred width is 0.01, and its preferred height is 0.01 too (the height is that of
  the visible characters, none here, by the same rule). This implementation gave 0 and one
  line's height (12.6 for GRAYSTROKE 12 pt, 19.04 for NotoSans 14 pt); it now gives
  0.01 × 0.01. An empty text ("") is 0 × 0 in both.
- **Heights** follow the same rule (first line's ascender to the last line's descender):
  NotoSans-Regular at 16 pt is 21.75 high and gives 21.76; at 12 pt 16.3125 gives 16.32.
  No other height measured falls on a hundredth, so ceiling and the rule agree on the rest.
- **Bold:** x ends at the last character's regular glyph advance, as above; the bold spacing
  after it is not counted. NotoSans-Regular 12 pt bold "Ethanol Gas" ends its last advance at
  71.21, its "s" begins at 65.1 and is 5.75 wide: x is 70.849998 and the width 70.85 (this
  implementation gave 70.86 for the centred rect, and now gives 70.85). The rule reproduces 574 of 576 bold texts
  from the logged values; the two misses are within one float step of a hundredth, where
  this reconstruction from logged values is not exact.
- **Layout groups and layout elements** take the value as it is: a `HorizontalLayoutGroup`
  with 4 + 4 of padding, 12 of spacing and a 24-wide child reports 44 + the text's width in
  float (148.26001 for 104.26; 44.01 for " "), and a `LayoutElement` given it keeps the bits.

| Font, size, text (regular) | x | shipped | this implementation before | now |
|---|---|---|---|---|
| NotoSans-Regular 12, "Ethanol Gas" | 67.25 | 67.26 | 67.25 | 67.26 |
| NotoSans-Regular 12, "Super Coolant Gas" | 104.25 | 104.26 | 104.25 | 104.26 |
| NotoSans-Regular 12, "Nuclear Fallout" | 85.09375 | 85.1 | 85.1 | 85.1 |
| NotoSans-Regular 12, "Room" | 33.1875 | 33.19 | 33.19 | 33.19 |
| NotoSans-Regular 16, "Super Coolant Gas" | 139 | 139.01 | 139 | 139.01 |
| NotoSans-Regular 16, "Room" | 44.25 | 44.26 | 44.25 | 44.26 |
| GRAYSTROKE REGULAR SDF 16, "Hydrogen" | 67.5 | 67.51 | 67.5 at pivot 0 | 67.51 |
| GRAYSTROKE OUTLINE SDF 14, "A" | 8.5 | 8.51 | 8.5 | 8.51 |
| NotoSansCJKjp-Regular 12, "Oxygen" | 41.850002 | 41.86 | 41.86 | 41.86 |
| NotoSansCJKjp-Regular 12, "i" | 3.2999999523 | 3.3 | 3.31 | 3.3 |
| NotoSans-Regular 14, " " | 0 | 0.01 × 0.01 | 0 × 19.04 | 0.01 × 0.01 |
| any, "" | — | 0 × 0 | 0 × 0 | 0 × 0 |

Not measured: margins (they are expected to be added to x before the rounding, as the
section above says they are included), several lines, wrapping, auto-size and rich-text
spacing tags. Measure them the same way before relying on them.

Also seen, not part of the rule: the character pens themselves (`xAdvance` at pivot 0) differ
by one float step in 318 of 1,152 non-white-space texts. NotoSans at 12 pt (scale 0.5) matches
in every case, as do one-character texts; the misses are at scales that are not powers of two
(14/24, 16/24, 12/124 and so on), at characters whose neighbour forms a kerning pair ("Hy",
"yg", "oo", "Me"). There the shipped package's pen is the float above or below origin +
advance × (size / point size): NotoSans 14 pt "Oxygen" has its "g" end at 34.08854294,
this implementation at 34.08853912. The rule above turns this into a 0.01 difference only
where a pen falls within a float step of a hundredth (NotoSans 16 pt "Oxygen", 57.875 shipped
and 57.875004 here, gives 57.88 in both). Matching it needs a measurement of how a kerning
pair's adjustment enters the pen (the font assets' pair records and texts with kerning off).

### Overflow at white space (measured 2026-10-23)

Found in a mod play-test: Butcher & Fishing Stations has a label of one space (" ", 14 pt,
centred) in a rect its `LayoutElement` sizes to the text's preferred size, 0.01 × 0.01 (see
"Preferred sizes to the hundredth" above). The shipped package reports `isTextOverflowing`
false and `firstOverflowCharacterIndex` -1; this implementation reports true and 0. The
`textInfo` counts are the same in both.

Measured with `PROBE_SCENARIO=textoverflow` (tools/UnityProbe, `TextOverflow.cs`): 22 texts
(" ", "  ", "\t", " \n ", "\n", "", U+00A0, "A", " A", "A ", " A ", "  A  ", "A\n", "\nA",
"A\n ", " \nA", "A B", "AB", "A\nB", " \n\nA", "A \n ", "…") in NotoSans-Regular 14 pt and
GRAYSTROKE REGULAR SDF 12 pt, set up as `SetTextStyleSetting` does. Rect widths 0, 0.005, 0.01,
the preferred width and 0.01 and 0.001 under and over it, and 400; heights 0, 0.005, 0.01, the
preferred height and the same steps around it, one line of the font (ascender to descender)
and 0.01 under and over it, and 100. Overflow, Truncate, Ellipsis and Masking, wrapping on and
off, centred (and top-left for Overflow without wrapping): 37,692 cases, each with
`isTextOverflowing`, `firstOverflowCharacterIndex`, `isTextTruncated`, the `textInfo` counts
and every character's code, `isVisible` and `lineNumber`. `tools/UnityProbe/ov-check.py rule
LOG` predicts each case from the rule below and reproduces all 37,692; this implementation
(with the preferred sizes above) reproduces 25,947 of its 38,124.
- **Checked characters.** Every character except white space is checked against the rect's
  height. The white space here is space, no-break space and line feed; "Further characters"
  below lists the rest. The tab is checked too, though it is not visible (`isVisible` false,
  character 9).
- **Vertical overflow.** The text overflows at the first checked character whose line's
  bottom is below the rect: the distance from the first line's ascender down to that line's
  descender is greater than the rect's height. Exactly at the height fits: NotoSans 14 pt's
  first line ends at 19.03125, so 19.03125 fits and 19.03 overflows; its second line ends at
  38.098953. `firstOverflowCharacterIndex` is that character's index, and
  `isTextOverflowing` is true when there is one.
  - So a text of white space alone never overflows, in any rect (0 × 0 too), in any mode,
    with or without wrapping: " ", "  ", " \n ", "\n" and U+00A0 report false and -1, and
    keep their characters.
  - White space before the first checked character, after the last one, and lines of white
    space alone never overflow either: " A" in a rect of height 0 overflows at 1, "\nA" at 1,
    " \n\nA" at 3; "A\n " in a 400 × 19.03125 rect does not overflow.
  - Lines of white space still take their height: in " \nA" the "A" overflows below 38.098953,
    in " \n\nA" below 57.16666.
  - Alignment makes no difference, and Masking reports the same as Overflow.
- **Cut at the width** (Truncate and Ellipsis, wrapping on or off). The first visible character
  (not white space, not a tab) whose pen after it passes the rect's width cuts the text there.
  The pen is measured from the line's start, as laid out. With wrapping on, such a character
  is one that is still too wide on the line it wraps to. "A" (8.93 wide) in a rect 0 wide, wrapped,
  Truncate, shows nothing and reports `isTextTruncated` true; in Overflow it shows "A". This
  cut is not an overflow: `firstOverflowCharacterIndex` stays -1, as in "Horizontal overflow
  with wrapping off" above. A tab is never cut.
- **Which cut.** The text is cut at the earlier of the two. When the cut at the width comes
  first, the index is -1. When both fall on the same character, it is the overflow's.
- **Truncate** at the cut c:
  - At c = 0, nothing is shown: `characterCount` 0, `lineCount` 0.
  - Otherwise the characters before c stay as laid out and c becomes the blank record
    (U+0003, not visible): `characterCount` is c + 1. White space before the cut is kept:
    " A" in a rect of height 0 keeps " " and the record on one line.
  - The record is on c's line. When that line began by wrapping (not after a line feed), the
    record stays at the end of the line before, and the text has one line fewer: "A B" wrapped
    at 21.69 × 19.04 gives "A", " " and the record on line 0; "\nA" at height 0 gives "\n" and
    the record on line 1.
- **Ellipsis** at the cut c: "…" takes the place of the last character before c whose line
  fits the rect's height and at whose pen "…" fits the width (its pen plus the ellipsis's
  advance, 11.083328 for NotoSans 14 pt, 7.078125 for GRAYSTROKE 12 pt from its fallback). The
  blank record follows it on the same line, and the text ends there (`lineCount` is that line
  + 1). When no character qualifies, nothing is shown (`characterCount` 0, `lineCount` 0).
  - " A" at 12.57 × 100 gives "…" and the record; "  A  " at 16.22 gives " …".
  - " A" 0 wide gives nothing: "…" does not fit at the space's pen 0.
  - " A" at height 0 gives nothing: line 0 does not fit.
  - " \n\nA" at 400 × 19.03125 gives " …" on line 0, in place of the first line feed: the
    second line does not fit.
  - GRAYSTROKE 12 pt " \nA" at 7.28 × 12.59 gives "…" in place of the space: at the line
    feed's pen (2.28) it does not fit.
  - This refines "Ellipsis shows nothing at all" in "Horizontal overflow with wrapping off"
    above: that holds where "…" is wider than the room before the cut.
- **Preferred height** is measured down to the last line with a checked character. "A\n " and
  "A \n " are 8.94 × 19.04 (this implementation: 38.1 high), " \nA" 38.1 and " \n\nA" 57.17
  high. "\t" is 0.01 × 19.04 (its width is that of no visible character, its height that of a
  checked one).

- **Implemented.** This implementation follows the rule: in the same probe `ov-check.py rule`
  reproduces all 37,692 cases from its log, and `ov-check.py diff` finds none that differ from
  the shipped package, preferred sizes included. "…" fits where its glyph's advance at its own
  font's scale fits (7.078125 for GRAYSTROKE 12 pt, from its fallback); it is still drawn at
  the text font's scale, as "Horizontal overflow with wrapping off" above measured. The further
  characters below are implemented as listed there, and auto-size still counts a line of white space
  below the rect as not fitting.

Where this implementation deviated before (`TMP_Text.Layout.cs` and `TMP_Text.Place.cs`):
- It checks each line, at the line's first character, whatever that is. So white space
  overflows (" " at 0.01 × 0.01 reports 0), and the index is the line's start (" A" and "\nA"
  at height 0 report 0, "A\n " at 400 × 19.03125 reports 2).
- Truncate and Ellipsis then remove the whole overflowing line: " A" at height 0 shows
  nothing where the shipped package keeps " " and the record. A text of white space alone in
  a low rect is cut to nothing where the shipped package keeps it.
- Ellipsis places "…" at the character before the cut without checking that it fits: " A" 0
  wide shows "…", and GRAYSTROKE 12 pt " \nA" at 7.28 × 12.59 shows " …".
- With wrapping on, Truncate and Ellipsis keep a character too wide for any line ("A" 0 wide
  stays "A"; " A" 0 wide gives " " and "A" on two lines).
- A tab is drawn as a visible U+25A1 and is 14.01 × 20.74 (NotoSans 14 pt), and lines of
  white space after the last checked character count towards the preferred height.

| Text (NotoSans-Regular 14 pt, centred), rect, mode | shipped: overflowing, index; characters | this implementation before |
|---|---|---|
| " ", 0.01 × 0.01, Overflow (Butcher & Fishing Stations) | false, -1; " " | true, 0; " " |
| " ", 0 × 0, Truncate | false, -1; " " | true, 0; nothing |
| U+00A0, 0 × 0, Ellipsis | false, -1; U+00A0 | true, 0; nothing |
| " \n ", 0 × 0, Overflow, wrapped | false, -1; 3 characters, 2 lines | true, 0 |
| "A", 400 × 19.03, Overflow | true, 0 | true, 0 |
| "A", 400 × 19.03125, Overflow | false, -1 | false, -1 |
| "\t", 400 × 19.03125, Overflow | false, -1; "\t" not visible | true, 0; U+25A1 visible |
| " A", 400 × 0, Overflow | true, 1 | true, 0 |
| " A", 400 × 0, Truncate | true, 1; " ", record | true, 0; nothing |
| " A", 400 × 0, Ellipsis | true, 1; nothing | true, 0; nothing |
| " A", 0 × 100, Ellipsis | false, -1, truncated; nothing | false, -1; "…", record |
| " A", 12.57 × 100, Ellipsis | false, -1; "…", record | the same |
| "\nA", 400 × 0, Overflow | true, 1 | true, 0 |
| "\nA", 400 × 0, Truncate | true, 1; "\n", record on line 1 | true, 0; nothing |
| "\nA", 400 × 38.09, Ellipsis | true, 1; "…", record on line 0 | the same |
| "A\n ", 400 × 19.03125, Overflow | false, -1 | true, 2 |
| " \n\nA", 400 × 19.03125, Ellipsis | true, 3; " ", "…", record | true, 2; the same characters |
| "A B", 21.69 × 19.04, Truncate, wrapped | true, 2; "A", " ", record, 1 line | the same |
| "A B", 21.69 × 19.04, Ellipsis, wrapped | true, 2; "A", "…", record | the same |
| "A", 0 × 100, Truncate, wrapped | false, -1, truncated; nothing | false, -1; "A" |
| " A", 0 × 100, Truncate, wrapped | false, -1; " ", record, 1 line | " ", "A", 2 lines |
| GRAYSTROKE 12 pt " \nA", 7.28 × 12.59, Ellipsis | true, 2; "…", record | true, 2; " ", "…", record |
| GRAYSTROKE 12 pt U+00A0 (fallback, 20.41 high), 400 × 12.59, Overflow | false, -1 | true, 0 |
| preferred size, "A\n " | 8.94 × 19.04 | 8.94 × 38.1 |
| preferred size, "\t" | 0.01 × 19.04 | 14.01 × 20.74 |

**Further characters** (measured 2026-10-23, the same probe with `PROBE_OV_PART=chars`, `CH`
lines). Each character x below was measured alone, doubled, and in "xA", "Ax", "A\nx", "A\nxx"
and "x\nA", in NotoSans-Regular 14 pt and GRAYSTROKE REGULAR SDF 12 pt. The rects were 0 × 0,
0.01 × 0.01, 400 × 0, 400 × one line (and 0.01 under it) and 400 × 100, in Overflow and
Truncate. Both fonts agree on everything except advances and heights.
- **Not checked** (never overflow; a text of only them reports false and -1 in 0 × 0 and
  0.01 × 0.01 and keeps its characters under Truncate; "xA" in 400 × 0 overflows at 1; "A\nx" in a
  rect one line high does not overflow; "A\nx" is as high as "A"): space, U+00A0, line feed,
  CR (U+000D), vertical tab (U+000B), U+2000–U+200A, the zero-width space U+200B, U+2028,
  U+2029 and U+3000. None of them is visible.
  - Line feed, vertical tab, U+2028 and U+2029 break the line ("x\nA" has three lines). CR
    does not: it is an ordinary character on its line, 7.146 wide in NotoSans 14 pt and 0 in
    GRAYSTROKE 12 pt ("x\nA" has two lines).
  - Not checked does not mean no height. A white-space character that comes from a fallback
    font with a deeper descender still deepens its line. In NotoSans 14 pt, U+3000 (descender
    5.79 below the baseline against 4.08) makes "x\nA" 39.81 high, not 38.1. The same goes
    for U+00A0 and U+2000–U+200A in GRAYSTROKE 12 pt: "x\nA" is 33.01, not 25.18. On the last
    line it still raises the preferred height, though by an amount not explained here: "A\nx"
    is 20.33 (U+3000, NotoSans) and 14.03 (U+00A0 and U+2000–U+200A, GRAYSTROKE, also U+3000
    15.13), where one line is 19.04 and 12.6. Its overflow index is not affected.
- **Checked** (as a visible character: a text of only it overflows at 0 in 0 × 0 and
  0.01 × 0.01, Truncate there leaves nothing, "xA" in 400 × 0 overflows at 0, and "A\nx" in a rect
  one line high overflows at 2):
  - the tab (not visible, see below);
  - form feed (U+000C) and U+180E: neither font has them, so each is drawn as the missing
    glyph U+25A1, visible: 14.01 × 20.74 (NotoSans 14 pt), 12.01 × 17.78 (GRAYSTROKE 12 pt);
  - U+FEFF: visible (`isVisible` true), 0 wide, from a fallback: 0.01 × 23.82 (NotoSans),
    0.01 × 20.42 (GRAYSTROKE).
- **U+0003 ends the text.** Nothing after it is laid out: "x" and "xA" have no characters and
  no lines and are 0.01 × 0.01; "x\nA" has none either. "Ax" has two characters, the second
  U+0003 (not visible, of no height), and is 8.94 × 19.04. "A\nx" has three characters on two
  lines, but is 8.94 × 4.09 (NotoSans) and 7.29 × 1.21 (GRAYSTROKE), less than the "A" alone:
  an anomaly measured, not explained. None of them overflows.
- **The tab's advance.** A tab moves the pen to the next tab stop. Stops are multiples of
  T = `tabSize` × `faceInfo.tabWidth` × (font size / `faceInfo.pointSize`) × `faceInfo.scale`,
  counted from the line's start. A pen already on a stop moves to the next one.
  - NotoSans-Regular has `tabSize` 10 and `tabWidth` 62.5 at point size 24: T is 364.58333 at
    14 pt and 729.16667 at 28 pt. GRAYSTROKE REGULAR SDF has 10 and 310 at 124: T is 300 at
    12 pt, 600 at 24 pt.
  - "\t" ends at T, "\t\t" at 2T, "A\t" and " \t" at T, "\tA" has its "A" at T.
  - The stops do not depend on the rect: the same in rects 50, 400 and 1000 wide. Centred
    text lays the line out the same and then centres it ("\t" from -T/2 to T/2).
  - The space width plays no part: 10 × the space is 36.458 and 22.778, which this
    implementation uses. It agrees with T only where `tabWidth` is 10 spaces (NotoSans: 62.5
    is 10 × 6.25), and even there it leaves out the `tabSize` factor.
  - The preferred width counts a tab at its start, not its end. "\t" is 0.01 and "\t\t"
    364.59 (the second tab starts at T), "A\t" 8.94, " \t" 3.65, and "\tA" 373.52, which is
    T + the "A". It is the next hundredth above the larger of the last visible character's
    end and the last tab's start.

| Character (NotoSans-Regular 14 pt) | checked | alone in 0 × 0 | "xA", 400 × 0 | "A\nx", 400 × 19.03125 | preferred size of x; of "A\nx" |
|---|---|---|---|---|---|
| U+000D (CR) | no | false, -1 | true, 1 | false, -1 | 0.01 × 0.01; 8.94 × 19.04 |
| U+000B (VT), U+2028, U+2029 | no, break the line | false, -1 | true, 1 | false, -1 | 0.01 × 0.01; 8.94 × 19.04 |
| U+2000–U+200A, U+200B | no | false, -1 | true, 1 | false, -1 | 0.01 × 0.01; 8.94 × 19.04 |
| U+3000 (from a fallback) | no | false, -1 | true, 1 | false, -1 | 0.01 × 0.01; 8.94 × 20.33 |
| U+0009 (tab) | yes, not visible | true, 0 | true, 0 | true, 2 | 0.01 × 19.04; 8.94 × 38.1 |
| U+000C (FF), U+180E | yes, drawn as U+25A1 | true, 0 | true, 0 | true, 2 | 14.01 × 20.74; 14.01 × 39.81 |
| U+FEFF | yes, visible, 0 wide | true, 0 | true, 0 | true, 2 | 0.01 × 23.82; 8.94 × 42.89 |
| U+0003 | ends the text | false, -1, no characters | false, -1, none | false, -1 | 0.01 × 0.01; 8.94 × 4.09 |

| Tab, rect 400 wide, left-aligned | shipped: tab ends at (from the line's start) | this implementation before |
|---|---|---|
| NotoSans-Regular 14 pt "\t" | 364.58333 | 36.458 |
| GRAYSTROKE REGULAR SDF 12 pt "\t" | 300 | 22.778 |

- **Implemented.** This implementation follows the block. In the probe's `CH` and `TAB`
  lines it matches the shipped package's overflow, index, truncation, counts and characters,
  and the tab's stops and preferred widths, except where the block leaves the heights of
  white space from a fallback and "A\n\u0003" unexplained: there "x\nA" is 39.74 where the
  shipped package gives 39.81 (NotoSans, U+3000, U+000C, U+180E), "A\nx" stays one line high,
  and "Ax" in a rect one line high overflows at the "A" (U+3000, and U+00A0 and U+2000–U+200A
  in GRAYSTROKE, which the shipped package lays out at the text font's height after a
  visible character). It also gives index 0, not 1, for "Ax" one line high where x is a
  taller checked character (U+000C, U+180E, U+FEFF), which the block does not cover. CR's
  record spans its glyph's advance but leaves the pen where it is ("\rA" has its "A" at the
  line's start, as the probe logs it). The zero-width space is unchecked only where it is not
  drawn: where white space is preserved it is visible (see "Text layout"), and this
  implementation checks it there like any visible character. An empty `TMP_InputField`'s text
  is a lone U+200B with white space preserved, and in the shipped package the field is one
  line high (Butcher & Fishing Stations: 21.04 with its padding); without this it was 0.01
  high (2.01 with padding).

Not measured: Page, Linked
and ScrollRect, auto-size (whether a text of white space alone fits), margins, rich text,
several fonts on one line, and whether "…" backs up past a visible character it cannot follow.
Measure them the same way before relying on them.

### The input field's layout values (measured 2026-10-23)

Found in a mod play-test: Geyser Control opens a `MultiSliderSideScreen`. Each slider row's
`TitleRow` (a `HorizontalLayoutGroup` controlling its children's width and height, not
expanding them) holds a `NumberInputField`, a `KInputTextField` (a `TMP_InputField`). In the
shipped package the row is 24 high and the field's rect is -32, -12, 64, 24. In this
implementation the row's preferred height was 33,329.72 and the screen 166,846 px tall, drawn
as a white column. The first value that differs is the field's own `preferredHeight`: 19.04
in the shipped package, which equals its text component's, and 44.04 in this implementation
at the field's first layout. That value then grows by 20.04 with every layout pass.

The prefab, read in the original player through E2eDriver:
- **The field:** a 64 × 24 rect (anchors top-left of the row), with a `LayoutElement` (min
  64 × 24, preferred width 64, preferred height -1, flexible width 0, priority 1), a sliced
  `Image` (priority 0), `KNumberInputField` and `KInputTextField`. Its settings: `SingleLine`,
  `characterLimit` 4 (set by `SliderSet.SetTarget`), `DecimalNumber`, no placeholder.
- **`textViewport`:** "Text Area", with a `RectMask2D` (padding 0), stretched over the field
  with offsets 0.
- **`textComponent`:** "Text", a `LocText` at 14 pt, no wrapping. It is anchored across the
  viewport's top edge: anchors (0, 1)–(1, 1), size delta (-3, -1), position (1.5, -0.5). Its
  rect is 61 wide and -1 high, whatever the field's height.
- **The values:** the field's preferred size is 32.09 × 19.04, its text's 32.09 × 19.04.
  `LayoutUtility` gives the field 64 × 24: the `LayoutElement`'s preferred width and minimum
  height are the larger values. Forcing the
  row's layout again (`LayoutRebuilder.ForceRebuildLayoutImmediate`) changes nothing.

Measured with `PROBE_SCENARIO=inputlayout` (tools/UnityProbe, `InputLayout.cs`): fields built in
code as Klei's prefabs are, a field with an `Image` and a `TMP_InputField`, its child
"Text Area" (`RectMask2D`) as `textViewport`, and its grandchild "Text" (NotoSans-Regular
14 pt, no wrapping) as `textComponent`. The cases vary:
- the field's rect (64 × 24, 200 × 100, 10 × 5);
- the text rect (stretched with offsets 0, inset by 10 and 6, moved, Geyser Control's
  top-anchored one, a fixed 50 × 10 at the centre);
- the viewport (stretched with offsets, fixed 40 × 10 at the centre, moved, anchored to part
  of the field, larger than the field, with `RectMask2D` padding, not assigned);
- the text's margins, and the text: "1915", "" and longer ones;
- with and without a placeholder (20 pt, wider and taller than the text);
- each line type, `characterLimit` 0 and 2, and no text component.

Each value is read at once, after a forced rebuild and a frame later. The same fields are then
laid out through four forced passes and three frames: in Geyser Control's row, under a
`HorizontalLayoutGroup` with `childControlHeight` on, off, and on with
`childForceExpandHeight`, with a `ContentSizeFitter` on the field, and in a row whose own
`ContentSizeFitter` fits its height. `tools/UnityProbe/il-check.py rule LOG` checks the rule
below against a log, and `il-check.py diff ORIG OURS` compares two. The rule reproduces all 888
lines of the shipped package's log.
- **The rule.** With a text component:
  - `preferredWidth` is the text component's `preferredWidth` + (viewport `offsetMin.x` −
    viewport `offsetMax.x`).
  - `preferredHeight` is the text component's `preferredHeight` + (viewport `offsetMin.y` −
    viewport `offsetMax.y`).
  - The offsets are the viewport's own, as set relative to its anchors. With no viewport they
    count as 0.
  - `minWidth` and `minHeight` are 0, `flexibleWidth` and `flexibleHeight` -1, and
    `layoutPriority` 1. `CalculateLayoutInputHorizontal` and `CalculateLayoutInputVertical`
    change none of them.
  - Without a text component, both preferred values are 0.
- **Not the rects.** Neither the field's rect nor the text component's enters the value. The
  same text gives the same value at 64 × 24, 200 × 100 and 10 × 5, and in every text rect,
  including a negative one (Geyser Control's -1 high, or -10 × -7 in a 10 × 5 field). So
  laying the field out cannot change its own preferred size, and repeated passes give the
  same value: every pass in every layout above reports the same preferred size as the first.
- **The viewport's offsets, not its rect.** A viewport fixed at 40 × 10 in the field's centre
  has offsets (-20, -5) and (20, 5), so the field is 32.09 − 40 = -7.91 wide and
  19.04 − 10 = 9.04 high. That holds in a 64 × 24 field and in a 200 × 100 one, and when the
  viewport is moved by (6, 2), since its offsets differ by the same amounts. The value is not
  clamped: -7.91 is reported, and `LayoutUtility.GetPreferredWidth` makes it 0 (as for any
  value below the minimum). A viewport larger than the field (offsets (-5, -3) and (7, 4)) gives
  32.09 − 12 = 20.09 by 19.04 − 7 = 12.04. A viewport anchored to the middle half of the field
  with offsets 0 adds nothing. The `RectMask2D`'s padding adds nothing either.
- **The text's own preferred size,** as "Text layout" and the sections above give it. Its
  margins are in it (margins 4, 2, 6, 3 give 42.09 × 24.04). An empty field's text is the lone
  U+200B: 0.01 × 19.04. The line type enters only through the text: a multi-line type turns
  wrapping on, so "abc def ghi jkl mno" in a 64-wide text is 124.8 × 57.17 (three lines), as
  its text component reports.
- **The placeholder does not count,** shown or not: an empty field with a placeholder 293.81 ×
  27.19 is 0.01 × 19.04, and so is the same field without it.
- **`characterLimit` does not count** beyond what it does to the text.
- **In layouts,** the field is laid out like any element of priority 1 with these values:
  - Under a `HorizontalLayoutGroup` controlling height, the field "1915" becomes
    32.09 × 19.04.
  - With height control off, it is 32.09 wide and keeps its 24.
  - With a `ContentSizeFitter` on the field, it is 32.09 × 19.04 from the next canvas update.
  - Beside Geyser Control's `LayoutElement`, the larger value wins: 64 × 24, the row 24.

Where this implementation deviated before (`TMP_InputField.cs`, the layout element at the end):
- It added to the text's preferred size the difference between the field's rect and the text
  component's rect, at least 0, not the viewport's offsets. That difference changes with the
  field's own size wherever the text rect does not stretch with the field.
- Geyser Control's text is anchored to the top and is -1 high in every field. So the field's
  preferred height was 19.04 + (h + 1), where h is the field's current height: 44.04 at 24. A
  parent that sizes the row to its preferred height makes the field 44.04 high, the next pass
  reads 64.08, and so on. In the probe's fitted row, the passes give 44.04, 64.08, 84.12,
  104.16, 124.2 and then 20.04 more each frame. In the game it kept growing while the side
  screen is open: the UI dump read the row at 33,329.72, and the field's `preferredHeight` was
  82,485.54 when read later.
- Where the text rect does stretch, the value was wrong without growing:
  - a text inset by (-20, -12) gives 52.09 × 31.04 for 32.09 × 19.04;
  - a viewport fixed at 40 × 10 gives 56.09 × 33.04 for -7.91 × 9.04;
  - a viewport larger than the field is clamped to 32.09 × 19.04 for 20.09 × 12.04;
  - a field without a viewport counts the text's offsets (44.09 × 26.04 for 32.09 × 19.04).
- It matched where the viewport and the text both stretch over the field: offsets 0 (32.09 ×
  19.04), or offsets on the viewport with the text at 0 (44.09 × 26.04). It also matched the
  margins, the empty text, the placeholder, the line types and the missing text component.
  In the probe 540 of the 888 lines differ, every one in `preferredWidth` or `preferredHeight`
  first.

| Field (text "1915", NotoSans 14 pt, 32.09 × 19.04) | shipped | this implementation before |
|---|---|---|
| Geyser Control: 64 × 24, text anchored to the top, size delta (-3, -1) | 32.09 × 19.04 | 35.09 × 44.04 |
| the same, the field 200 × 100 | 32.09 × 19.04 | 35.09 × 120.04 |
| the same in a row fitted to its height, passes 1 to 4 | 19.04 every pass | 64.08, 84.12, 104.16, 124.2 |
| the same in Geyser Control's row (`LayoutElement` min 64 × 24) | row 24 | row 44.04; in the game 33,329.72 |
| text and viewport stretched, offsets 0, any field size | 32.09 × 19.04 | 32.09 × 19.04 |
| text inset (size delta (-20, -12)) | 32.09 × 19.04 | 52.09 × 31.04 |
| viewport offsets (5, 3) and (-7, -4) | 44.09 × 26.04 | 44.09 × 26.04 |
| viewport anchored (0.25, 0.5)–(0.75, 1), offsets (2, 1) and (-3, -4) | 37.09 × 24.04 | 69.09 × 36.04 |
| viewport fixed 40 × 10 at the centre | -7.91 × 9.04 | 56.09 × 33.04 |
| viewport offsets (-5, -3) and (7, 4), larger than the field | 20.09 × 12.04 | 32.09 × 19.04 |
| no viewport, text inset by (5, 3) and (7, 4) | 32.09 × 19.04 | 44.09 × 26.04 |
| `RectMask2D` padding (4, 3, 2, 1) | 32.09 × 19.04 | 32.09 × 19.04 |
| text margins (4, 2, 6, 3) | 42.09 × 24.04 | 42.09 × 24.04 |
| empty text, Geyser Control's text rect | 0.01 × 19.04 | 3.01 × 44.04 |
| empty text, a placeholder 293.81 × 27.19 shown | 0.01 × 19.04 | 0.01 × 19.04 |
| `MultiLineNewline`, "abc def ghi jkl mno" in 64 wide | 124.8 × 57.17 | 124.8 × 57.17 |
| no text component | 0 × 0 | 0 × 0 |
| min, flexible, priority (every case) | 0, -1, 1 | 0, -1, 1 |

- **Implemented.** This implementation follows the rule: the text's preferred size plus the
  viewport's `offsetMin` − `offsetMax`, 0 without a viewport, no rect read and no clamp. In the
  probe `il-check.py rule` holds for all 888 lines of its log. Against the shipped package's
  log the only values that differ are the inactive fields' `characterLimit` and `SingleLine`
  cases below (60 lines), which are outside the rule.

Also seen, outside the rule: `text` set from code on a field that is still inactive (as the probe
builds them) keeps what `characterLimit` and `SingleLine` would refuse from typing. "1915"
with a limit of 2 stays "1915", and "ab\ncd" in a `SingleLine` field keeps its line feed and is
two lines (16.45 × 38.1). This implementation cuts them to "19" and "abcd". "Text set from
code" below measures this in every state: the shipped package keeps the value whole on active
and focused fields too.

Not measured: a scaled or rotated viewport, a viewport that is not the field's child,
`ForceLabelUpdate` between passes, the vertical scrollbar, and auto-size text.

### Text set from code (measured 2026-10-23)

Found in a mod play-test: with its layout fixed, Geyser Control's slider fields still showed
cut values. `SliderSet.SetTarget` gives each field a `characterLimit`
(⌊1 + log10(max + decimal places)⌋, 4 for mass per cycle), and then `KNumberInputField.SetDisplayValue`
sets `field.text` to the value. The shipped package keeps "507.2", "63.6", "65208.3" and
"61.3", in the field's `text` and in its label. This implementation cut them to "507.",
"63", "65208." and "61".

Measured with `PROBE_SCENARIO=inputtext` (tools/UnityProbe, `InputText.cs`, lines `IT …`): 438
cases. Each field is a `KInputTextField` built as the slider row's `NumberInputField` is
(an `Image`, "Text Area" with a `RectMask2D`, "Text" anchored to its top), with
`onFocusSelectAll` off. Its line type, content type and limit are set first, as `SetTarget`
sets them, or one of them after the text. Then the text is set, in one of three ways:
`text =`, `SetTextWithoutNotify`, or the text component's own `text`. The field is in one of
three states then: inactive, active but not focused, or focused (selected and activated two
frames before).

The cases vary:
- Geyser Control's values with limit 4 and `DecimalNumber`;
- the limits 0, 1 and 4;
- each line type with a line feed, a tab and a CR;
- each content type (Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric,
  Name, EmailAddress, Password, Pin) with "-12.5e3", "ab c!1" and "x\ny";
- a limit with `MultiLineNewline`.

Each field is then followed step by step: a frame on, being enabled (when inactive), activation
(selected and `ActivateInputField`, two frames), an edit (End, then a typed "9", then
Backspace, through `ProcessEvent`) and deselection. Every step logs the field's `text`, its
label, `caretPosition` and the `onValueChanged`, `onEndEdit` and `onSubmit` reports.
`tools/UnityProbe/it-check.py rule LOG` checks the rule below, and `it-check.py diff ORIG OURS`
compares two players' logs. The rule holds for all 438 cases in the shipped package's log.
- **`text =` and `SetTextWithoutNotify` keep the value whole.**
  - `characterLimit` does not cut it: "507.2" with limit 4 stays "507.2", and "abcdefg" with
    limit 1 stays "abcdefg".
  - `SingleLine` keeps line feeds and tabs: "ab\ncd" stays two lines, "a\tb" keeps its tab.
  - No content type validates or changes it: IntegerNumber keeps "ab c!1", Name keeps
    "-12.5e3".
  - This holds in every state: inactive, active, and focused while being edited.
  - The label becomes the value followed by U+200B ("507.2​"). With `Password` and `Pin` the
    label is one "*" per character, a line feed included ("x\ny" shows "***​").
- **Nothing applies them later.** The text stays whole a frame on, when the field is enabled,
  on activation and focus, and on deselection.
  - Setting `characterLimit`, `contentType` or `lineType` after the text changes nothing either:
    "507.2" then limit 4 stays "507.2", and no `onValueChanged` is sent.
- **Only an edit is limited and validated, one typed character at a time.**
  - A typed character is refused while the text is as long as the limit or longer: "507.2" with
    limit 4 stays "507.2" when "9" is typed, with no `onValueChanged`. "12" becomes "129".
  - Deleting is not limited: Backspace on "507.2" gives "507.", and the rest is not cut.
  - The content type checks only the typed character against the text around it. IntegerNumber
    takes "9" at the end of "-12.5e3", giving "-12.5e39"; Name refuses it.
  - The rest of the text is never checked again: "x\ny" keeps its line feed in a `SingleLine`
    field through every edit.
- **`onValueChanged`:**
  - `text =` reports the whole value once, at once, in every state, including inactive fields.
  - `SetTextWithoutNotify` reports nothing.
  - Edits report the edited text.
  - Backspace on an empty field still reports `""` once (Name, after its refused "9"), though
    nothing changed.
  - Deselecting reports `onEndEdit` with the text as it is ("507." after the Backspace).
- **The text component's own `text`:**
  - On an enabled field (active or focused), it is put back at once: right after
    `textComponent.text = "507.2"` it reads back as the field's text and U+200B ("​" for an
    empty field). The field's `text` stays "". No event is sent.
  - On an inactive field it keeps the value until the field is enabled, which writes the
    field's text and U+200B, as "when the label and the caret are set up" above describes.
  - Edits work on the field's `text`, not on the label: "9" typed afterwards gives "9".
- **Activation** with `onFocusSelectAll` off leaves the caret where it was, at 0
  (`caretPosition` 0, `selectionAnchorPosition` 0), not at the end. End then moves it to the
  end of the caret's line as laid out, not of the text. In a `SingleLine` field "ab\ncd" takes
  "9" after "ab". In a 64-wide multi-line field "a\tb" wraps (the tab reaches 364.58), and "9"
  goes in after "a".

Where this implementation deviated before (`TMP_InputField.cs`: `SetText` and the
`characterLimit` setter):
- **`SetText` cut.** `text =` and `SetTextWithoutNotify` removed line feeds and tabs in a
  `SingleLine` field (so "x\ny" became "xy" under every single-line content type), and cut
  the value to `characterLimit`. `onValueChanged` and the label carried
  the cut value. That is Geyser Control's "507." and "65208." (the game's limits differ by row).
  CR was kept, as in the shipped package.
- **The `characterLimit` setter cut** the text when the limit fell below its length, and
  sent `onValueChanged` with the cut text.
- **The text component's own text** stayed as set on an enabled field until activation wrote
  the label, where the shipped package puts it back at once.
- **Activation** put the caret at the end of the text (7 in "abcdefg"), not at 0, in all 132
  activations of a field with text.
- **Backspace** on an empty field sent nothing.
- **End** in the wrapped multi-line "a\tb" went to the end of the text ("a\tb9").
- It matched elsewhere: `SetTextWithoutNotify` sent nothing, a typed character was refused at
  the limit, CR was kept, setting the text on a focused field left the caret at 0, the label
  of an inactive field was kept until enable, and `onEndEdit` came on deselection.
- In the probe, 1,628 of 4,428 steps differed. 338 of 438 cases followed the rule, and the 100
  that did not were the limit and line-feed cases above.

| Field, text set from code | shipped: `text`; `onValueChanged` | this implementation before |
|---|---|---|
| limit 4, `DecimalNumber`, `text = "507.2"` (inactive, active or focused) | "507.2"; "507.2" | "507."; "507." |
| the same, "65208.3" | "65208.3"; "65208.3" | "6520"; "6520" |
| the same, "12", then "9" typed | "12", then "129" | the same |
| the same, "507.2", then "9" typed | "507.2", "9" refused, no event | "507.", "9" refused |
| the same, "507.2", then Backspace and deselect | "507."; `onEndEdit` "507." | "507"; `onEndEdit` "507" |
| limit 4, `SetTextWithoutNotify("507.2")` | "507.2"; nothing | "507."; nothing |
| `text = "507.2"`, then `characterLimit = 4` | "507.2"; nothing more | "507."; "507." |
| limit 1, `text = "abcdefg"` | "abcdefg" | "a" |
| limit 3, `MultiLineNewline`, "a\nbcd" | "a\nbcd" | "a\nb" |
| `SingleLine`, "ab\ncd" | "ab\ncd" (two lines) | "abcd" |
| `SingleLine`, "a\tb" | "a\tb" | "ab" |
| `SingleLine`, "ab\rcd" | "ab\rcd" | "ab\rcd" |
| `IntegerNumber`, "ab c!1" | "ab c!1" | "ab c!1" |
| `Password`, "x\ny" | "x\ny", label "***​" | "xy", label "**​" |
| `textComponent.text = "507.2"`, active field with text "" | label "​" at once, `text` "" | label "507.2" until activation |
| the same, inactive field | label "507.2" until enable, then "​" | the same |
| activation, "abcdefg", `onFocusSelectAll` off | caret 0 | caret 7 |
| Backspace in an empty field | `onValueChanged` "" | nothing |

- **Implemented.** `text =` and `SetTextWithoutNotify` keep the value whole (only `\0` is
  removed, as before). The `characterLimit`, `contentType` and `lineType` setters leave the
  text alone and send nothing. Typed characters are limited and validated as before.
  - A text written into an enabled field's text component is put back at once, from the text
    component's dirty-vertices callback.
  - Activation with `onFocusSelectAll` off leaves the caret where it was.
  - The End key goes to the end of the caret's laid-out line, before its last character (the
    line feed, or the white space a wrapped line ends in). On the last line it goes to the
    last laid-out character, as before. `MoveTextEnd` itself is unchanged.
  - Backspace in an empty field reports `""`.
  - In the probe, `it-check.py rule` holds for all 438 cases of our log. `it-check.py diff`
    against the shipped package's log finds none of the 4,428 steps different. Tests are in
    `TmpInputTextTests`.
- **Also measured by that diff:** setting `characterLimit` writes the label again, also on an
  inactive field. Take an inactive field whose text component has its own "507.2": setting
  `characterLimit = 4` makes the label "​" (the field's text "" and U+200B) at once, before
  the field is enabled. This implementation does the same.

Not measured: `onValidateInput` and `inputValidator` (the probe's fields have neither; some of
the game's fields do), `characterValidation` set without a content type, pasting, `text =` with the value the field already holds, rich text, and
`readOnly`. Measure them the same way before relying on them.

## Verification

1. **Build:**
   - `scripts/build-packages.sh` builds the package, and ApiCheck reports nothing missing for
     the game.
   - The mixed check covers Unity's shipped uGUI and Timeline: while they are Unity's, your
     TextMeshPro calls into the shipped uGUI, so use only its public and protected API.
2. **Unit tests** (`tests/OniBrutal.UnityEngine.Tests`, with a reference to the package
   project):
   - rich-text parsing;
   - wrapping and the line-breaking rules;
   - each alignment;
   - each overflow mode;
   - auto-size;
   - kerning;
   - `textInfo` contents;
   - the `TMP_TextUtilities` hit tests;
   - the input field's editing and validation;
   - dynamic font assets, through the engine's FontEngine;
   - the style sheet.
3. **Differential runs** (`scripts/m9-compare.sh`), loading this TextMeshPro, with uGUI shipped
   or this project's:
   - The `menu` and `colony` scenarios, plus scenarios you add for long texts with wrapping and
     auto-size, localization fonts, links, the input fields and the dropdowns.
   - No differences beyond the tolerance in the geometry hashes, `textInfo` counts and font
     sizes, or each one explained.
4. **Performance:** DeCrew's frame rate is not worse than with the shipped package.
