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
- **Tabs:** they advance to the next multiple of `tabSize` × the space width.
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
    Margins are included.
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
  - `DeactivateInputField()` ends it.
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

KInputTextField overrides `LateUpdate` and calls the base implementation, so the base's
`LateUpdate` must do its share of the work above (starting a requested activation, caret
blinking, scrolling the text).

**`TMP_Dropdown`:** like uGUI's `Dropdown`, with TMP texts:
- **Contents:** `options` (`OptionData`: text and image), `value` and `onValueChanged`.
- **Showing the list:** `Show()` builds the list from `template` under a new canvas with a
  blocker. Clicking an option sets `value` and hides the list.
- **`RefreshShownValue`** updates the caption.
- `set_options` replaces the options and refreshes the caption.

### Miscellaneous

- **`TMPro_ExtensionMethods`:** `Compare(Vector3, Vector3, accuracy)` and
  `Compare(Quaternion, …)` compare components rounded to `accuracy` decimal places;
  `Tint(Color32, float)` multiplies RGB.
- **`FastAction<T>`:** an ordered set of delegates, called in insertion order.
- **`TMP_Text` exposes** `rectTransform`, `transform` (cached), `mesh` and the `isVolumetricText`
  family as documented.

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
  the caret graphic.
- **Stored alignment:** values 0–15 in the game's data use the oldest alignment table; a value
  set at run time is split into its horizontal and vertical bytes.
- **Truncation:** Truncate and Ellipsis stop where the first line that doesn't fit begins,
  leaving a blank record. Truncation keeps the part of a wrapped word that fits.
- **Auto-size** doesn't choose a size that would break a word, and `preferredHeight` uses the
  auto-sized size. A margin change keeps the preferred sizes until the text changes.
- **`textBounds`** spans the characters' advances.
- **Input field:** `inputValidator` doesn't change `characterValidation`.
- **Dynamic font assets** use the "TextMeshPro/Mobile/Distance Field" shader.
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
     character indices are current right after it.
   - Not matched: setting `caretPosition` in code does not redraw the shipped package's
     caret until the next change; this implementation redraws it at the next rebuild.

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
