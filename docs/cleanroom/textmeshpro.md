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
- **Characters:** the zero-width space gets a quad. Character corners include the padding.
  Small caps keep the text's size and line metrics. Sprites don't raise the line's ascender,
  and `spriteCount` stays 0.
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
