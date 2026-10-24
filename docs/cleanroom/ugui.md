# uGUI (UnityEngine.UI): functional specification

This project's replacement of Unity's uGUI package. It goes in the assembly `UnityEngine.UI`
(`src/UnityEngine.UI`), with the namespaces `UnityEngine.UI` and `UnityEngine.EventSystems`.
The game builds every menu, panel and HUD element with it. The rules are in
`docs/cleanroom/README.md`, and PLAN M9 describes the stage.

## Interface

**Generated shapes:** `src/UnityEngine.UI/Generated/` holds everything that is fixed:
- the type declarations, with their bases and interfaces;
- the enums, interfaces and delegates;
- the serialized fields of every class and struct.

`scripts/regen-packages.sh` regenerates these from the shipped package's metadata. Do not edit
them. Write the rest in partial declarations next to them.

**Serialized fields:** the game's data serializes components by field order and type, through
the engine's serializer. The generated fields must therefore stay as they are. Field
initializers are not part of the metadata. A component added at run time (`AddComponent`)
starts with Unity's documented defaults, so give each field the default the documentation
states. Where the documentation is silent, measure it: add the component in a run that uses
the shipped package and read its fields through public members.

**Required members:** `out/surface/package-members.tsv` (from `scripts/regen-packages.sh`) lists
every member the game's assemblies use:
- `call`: the game calls the member;
- `override`: the game's classes override the member, so the replacement must call it at the
  moment Unity does.

`scripts/build-packages.sh` runs ApiCheck against the game's references. It must report
nothing missing.

**The game's subclasses:**
- `UIBehaviour`: two classes.
- `PointerInputModule`: Klei's input module overrides `Process`, `UpdateModule`,
  `ShouldActivateModule`, `IsModuleSupported`, `ActivateModule`, `DeactivateModule` and
  `GetMousePointerEventData`, and it calls the protected helpers of `PointerInputModule` and
  `BaseInputModule`.
- Graphics: `Graphic`, `MaskableGraphic` and `Image` (KImage). They override `OnPopulateMesh`,
  `mainTexture`, `SetVerticesDirty` and `SetMaterialDirty`.
- `LayoutElement` and `AspectRatioFitter`: two classes each.
- `ScrollRect`: overrides `LateUpdate`.
- `Slider`.
- `Toggle`: two classes.

These classes rely on the protected and virtual members of their bases, and on the order of the
UIBehaviour messages described below.

**The engine:** the engine's API is this project's own code (`src/OniBrutal.UnityEngine`). The
engine grants `UnityEngine.UI` access to its internals. The parts uGUI builds on:
- `Canvas`, `CanvasRenderer`, `CanvasGroup` and `RectTransform`;
- `RectTransformUtility`;
- `Canvas.willRenderCanvases` and `Canvas.ForceUpdateCanvases`;
- the `ICanvasRaycastFilter` and `ICanvasElement` contracts;
- `Input`, `Event` and `Physics`/`Physics2D` raycasts;
- `Sprite`, `DataUtility` and `Font`;
- the legacy `TextGenerator`.

**UI Toolkit:** the shipped package also has a bridge to UI Toolkit (`UIToolkitInteroperabilityBridge`,
`PanelEventHandler`, `PanelRaycaster`). The game has no UI Toolkit content. Keep only what the
generated shapes require: the bridge's serialized field on `EventSystem`. It does nothing.

## Behaviour

These follow Unity's uGUI documentation, the 1.0 and 2.0 manuals and the scripting reference.
Each area below states what the game relies on and what is easy to get wrong.

### UIBehaviour messages

`UIBehaviour` is a `MonoBehaviour` with protected virtual `Awake`, `OnEnable`, `Start`,
`OnDisable`, `OnDestroy`, `OnValidate` and `Reset`, plus these:
- `OnRectTransformDimensionsChange`, `OnBeforeTransformParentChanged`,
  `OnTransformParentChanged`, `OnDidApplyAnimationProperties`,
  `OnCanvasGroupChanged` and `OnCanvasHierarchyChanged`. The engine sends these messages
  (`Engine/`; search for the message names).
- `IsActive()`, which is `isActiveAndEnabled`.
- `IsDestroyed()`.

Subclasses call the base implementations, and the replacement's own subclasses must do the
same.

### Canvas update cycle (`CanvasUpdateRegistry`, `ICanvasElement`, `CanvasUpdate`)

**When it runs:** the registry subscribes to `Canvas.willRenderCanvases`, which the engine
raises once per frame before canvases render, and on `Canvas.ForceUpdateCanvases`.

**One pass:**
1. Elements that are destroyed or inactive are dropped from both queues.
2. **Layout rebuild:** the layout queue is sorted by hierarchy depth, parents first: depth is the
   number of ancestors. Each element's `Rebuild` is then called for `Prelayout`, `Layout` and
   `PostLayout` in turn; each phase runs over the whole queue before the next phase starts.
   `LayoutComplete` follows for each element.
3. **Clipping:** `ClipperRegistry.Cull()` runs, so every registered `IClipper` performs its
   clipping.
4. **Graphic rebuild:** the graphic queue's elements get `Rebuild` for `PreRender` and then
   `LatePreRender`, followed by `GraphicUpdateComplete`.
5. Both queues are cleared.

**Registering:**
- `RegisterCanvasElementForLayoutRebuild` and `…ForGraphicRebuild` add an element once.
- `TryRegister…` returns false when the registry is in the middle of that kind of rebuild; the
  plain forms log an error instead.
- Registering during the layout phase for a graphic rebuild is allowed and takes effect in the
  same pass.
- `UnRegisterCanvasElementForRebuild` and `DisableCanvasElementForRebuild` behave alike
  (measured on the shipped package): remove the element from the layout queue and call its
  `LayoutComplete`, then remove it from the graphic queue and call its `GraphicUpdateComplete`,
  whichever queues held it (also for an element that was never registered).
- During a rebuild, the half for the queue being rebuilt is skipped with the error "Trying to
  remove X from rebuild list while we are already inside a rebuild loop. This is not
  supported."; the element is still rebuilt in that pass. The other half still happens.
- `IsRebuildingLayout()` and `IsRebuildingGraphics()` report the current phase.

### Graphic

`Graphic` is the base of everything drawn. It requires `CanvasRenderer` and `RectTransform`,
and declares `ICanvasElement`.

**Dirtying:**
- `SetAllDirty` sets layout, vertices and material dirty.
- `SetLayoutDirty` marks the layout for rebuild, through `LayoutRebuilder.MarkLayoutForRebuild`
  on the rect transform, and invokes the layout-dirty callbacks.
- `SetVerticesDirty` and `SetMaterialDirty` register the graphic for a graphic rebuild and
  invoke their callbacks.
- The `Register`/`UnregisterDirty…Callback` methods manage the callbacks.

**Rebuild(PreRender):**
- With vertices dirty, `UpdateGeometry` runs: a `VertexHelper` is cleared and filled by
  `OnPopulateMesh(VertexHelper)`. The default implementation fills one quad covering the
  rect's pixel-adjusted rectangle, in `color`. Then each `IMeshModifier` component on the
  GameObject, in component order, gets `ModifyMesh`. The result is set on the
  `CanvasRenderer` (`SetMesh`), from a shared work mesh.
- With material dirty, `UpdateMaterial` sets the canvas renderer's material count to 1. It sets
  `materialForRendering` (the `IMaterialModifier` components on the GameObject applied in
  order to `material`) and the texture `mainTexture`.

**Lifecycle:**
- `OnEnable` registers the graphic with its canvas (`GraphicRegistry`), cuts off the canvas
  renderer's culling, and dirties everything.
- `OnDisable` unregisters it, clears the canvas renderer (`Clear`), and notifies the layout
  (`LayoutRebuilder.MarkLayoutForRebuild`).
- `OnRectTransformDimensionsChange` dirties the vertices, and with a pending layout rebuild
  the layout as well.
- Parent and canvas changes re-register the graphic and dirty everything.

**Properties:**
- `color`: changing it dirties the vertices.
- `raycastTarget` and `raycastPadding`.
- `material`: the default is `defaultGraphicMaterial`, the engine's default UI material.
- `canvas`: the nearest enabled `Canvas` up the hierarchy.
- `depth`: `canvasRenderer.absoluteDepth`, unchanged. The numbering is the engine's canvas
  batching (`Engine/UI/CanvasSystem.cs`), not this package's; it follows the original's as
  UnityProbe measured it (`UIS depth`), with either package.
- `defaultMaterial`, `mainTexture`, `rectTransform`, `canvasRenderer`.

**Methods:**
- `GetPixelAdjustedRect()`: the rect snapped to pixels when the canvas is pixel-perfect,
  through `RectTransformUtility.PixelAdjustRect`; otherwise the rect.
- `PixelAdjustPoint`.
- `Raycast(Vector2 sp, Camera eventCamera)`: walks up the hierarchy, from this object to the
  root. Every enabled `ICanvasRaycastFilter` component found on the way must accept the point.
  Above a `CanvasGroup` with `ignoreParentGroups`, further `CanvasGroup`s are skipped (other
  filters still count), and a `CanvasGroup` that does not block raycasts fails it.
- `CrossFadeColor(target, duration, ignoreTimeScale, useAlpha[, useRGB])` tweens the canvas
  renderer's colour, not `color`. A duration of 0 applies the colour at once. A tween advances
  every frame while the graphic is active and stops when it is disabled. Unity runs it as a
  coroutine on the graphic, and a replacement may do the same. A new tween replaces a running
  one.
- `CrossFadeAlpha(alpha, duration, ignoreTimeScale)` likewise.
- `SetNativeSize` is virtual.
- `OnCullingChanged` re-dirties the vertices when the element becomes visible again.

**`MaskableGraphic`** adds `maskable` and `isMaskingGraphic`, and `onCullStateChanged` (a
`UnityEvent<bool>`). It implements `IClippable` and `IMaterialModifier`:
- **Stencil:** `GetModifiedMaterial` wraps the material in a stencil material
  (`StencilMaterial.Add`). The stencil value is the number of enabled `Mask` components above
  the graphic, up to its canvas. The graphic's own mask, when it is the masking graphic,
  counts separately.
- **Clipping:** `RecalculateClipping` registers the graphic with the nearest `RectMask2D`
  parent.
- **Culling:** `Cull(clipRect, validRect)` sets the canvas renderer's `cull` when the
  graphic's canvas-space rect does not overlap the clip rect.
- **`SetClipRect`** and **`SetClipSoftness`** pass the clip to the canvas renderer.

### VertexHelper

A builder of UI meshes:
- **Building:** `AddVert` (as a `UIVertex` or as its parts), `AddTriangle`, `AddUIVertexQuad`,
  `AddUIVertexStream` and `AddUIVertexTriangleStream`.
- **Access:** `PopulateUIVertex`, `SetUIVertex`, `GetUIVertexStream` (as a triangle list) and
  `FillMesh(Mesh)`.
- **Counts and housekeeping:** `currentVertCount`, `currentIndexCount`, `Clear` and `Dispose`.

The channels are position, colour, uv0 to uv3, normal and tangent. A vertex added without
them gets `UIVertex.simpleVert`'s defaults: normal (0, 0, −1), tangent (1, 0, 0, −1).

### Image

**Sprite and material:**
- `sprite` and `overrideSprite`: the override wins when it is set. `activeSprite` is the one in
  use.
- With no sprite, the image draws the engine's white texture.
- `mainTexture` is the active sprite's texture, or its alpha texture through `material` as
  Unity does.

**Types:**
- **Simple:** one quad, over the pixel-adjusted rect. With `preserveAspect`, it is fitted
  inside the rect at the sprite's aspect, centred by the pivot. `useSpriteMesh` uses the
  sprite's own mesh.
- **Sliced:** nine quads, split by the sprite's border scaled by `pixelsPerUnit`. When the rect
  is smaller than the borders, the borders shrink proportionally. With `fillCenter` false
  there is no centre quad. A sprite without a border draws as Simple.
- **Tiled:** the centre repeats at the sprite's size divided by `pixelsPerUnit`. A sprite whose
  texture wraps, without a border and not packed, uses one quad with repeating UVs; otherwise
  one quad per tile, clipped at the edges. Borders behave as in Sliced. The engine limits the
  quad count, which Unity documents as 65000 vertices.
- **Filled:** `fillMethod` (Horizontal, Vertical, Radial90, Radial180, Radial360),
  `fillOrigin`, `fillAmount` (0 to 1) and `fillClockwise`. The fill cuts the quad, or for
  radial fills sweeps it, as documented. With `fillAmount` 0, nothing is drawn.

**Other members:**
- `pixelsPerUnitMultiplier` scales the sprite's pixels per unit relative to the canvas's
  `referencePixelsPerUnit`.
- `alphaHitTestMinimumThreshold` (0 by default): above 0, `IsRaycastLocationValid` samples the
  sprite's texture alpha at the point, which needs a readable texture, and the raycast passes
  only at or above the threshold.
- `SetNativeSize` sizes the rect to the sprite's pixel size divided by `pixelsPerUnit`.
- `ILayoutElement`: the preferred size is the sprite's size, or for Sliced and Tiled its border
  size; the minimum size is 0; flexible is -1.

### RawImage, Text, Shadow and Outline

- **RawImage:**
  - `texture`, defaulting to white, and `uvRect`.
  - One quad, with UVs from `uvRect`.
  - `SetNativeSize` uses the texture's size times `uvRect`'s size.
- **Text (legacy):**
  - It lays out its `text` with the engine's `TextGenerator`, using `GetGenerationSettings`
    for the rect.
  - `cachedTextGenerator` is used for the mesh, and `cachedTextGeneratorForLayout` for the
    `ILayoutElement` preferred sizes.
  - The vertices are offset by the rounding offset of the first vertex when the canvas is
    pixel-perfect.
  - A font texture rebuild re-dirties every Text that uses the font
    (`FontUpdateTracker` on `Font.textureRebuilt`).
  - `fontData` (a `FontData`) holds the settings.
- **Shadow and Outline** (`BaseMeshEffect`): `Shadow` adds a copy of the vertices at
  `effectDistance` in `effectColor`, with `useGraphicAlpha`. `Outline` adds four copies, at
  (±x, ±y). The copies are drawn first, behind the original.

### Masking

- **`Mask`** (stencil): with `showMaskGraphic`, the mask's own graphic is drawn. Its graphic
  writes the stencil, and children test it. It works through `GetModifiedMaterial`, the
  stencil material, and the canvas renderer's pop material for unwinding the stencil. It
  needs the GameObject's Graphic.
- **`RectMask2D`** (clipping without stencil):
  - **Clip rect:** its clippables (MaskableGraphics) below it are clipped to the intersection
    of its rect, shrunk by `padding`, with all enclosing RectMask2Ds' rects, all in canvas
    space.
  - **Softness:** `softness` gives the clip a soft edge.
  - **Culling:** elements wholly outside the clip are culled: an `Image` by its rect, a
    TextMeshPro text by its mesh's bounds, with touching edges counting as outside ("RectMask2D
    culling (measured)" below).
  - **When:** it registers with `ClipperRegistry` and recomputes in `PerformClipping` each
    pass. When an element moves or the rect changes, the clip and cull state is updated in that
    pass.

### Layout

Layout is controlled by the layout controllers `ILayoutController`, `ILayoutSelfController`
and `ILayoutGroup`, and by the layout elements (`ILayoutElement`), as the Auto Layout section of
Unity's manual describes.

**`LayoutRebuilder`:**
- `MarkLayoutForRebuild(rect)` finds the root of the layout: the topmost consecutive ancestor
  whose GameObject has an enabled `ILayoutGroup`, stopping at the first one without. That
  root is registered as a rebuild for the next pass.
- **The rebuild:**
  - Widths: `CalculateLayoutInputHorizontal` on every `ILayoutElement` of the subtree,
    children before parents. Then `SetLayoutHorizontal` on the controllers, parents before
    children; self controllers (`ILayoutSelfController`) before the group on the same
    GameObject.
  - Heights: the same, vertically.
- `ForceRebuildLayoutImmediate(rect)` does a rebuild now.

**`LayoutUtility`:**
- `GetMinSize`, `GetPreferredSize`, `GetFlexibleSize` and their per-axis forms, with
  `GetLayoutProperty`.
- Over all enabled `ILayoutElement` components on the GameObject: the one with the highest
  `layoutPriority` wins, and a value below 0 means "not set". Equal priorities take the
  largest value.
- The preferred size is at least the minimum size.

**`LayoutElement`:**
- `ignoreLayout`, `minWidth`/`minHeight`, `preferredWidth`/`preferredHeight` and
  `flexibleWidth`/`flexibleHeight`, all -1 when not set.
- `layoutPriority` is 1 by default.

**Layout groups** (`HorizontalLayoutGroup`, `VerticalLayoutGroup`, `GridLayoutGroup`):

| Setting | Meaning |
|---|---|
| `padding` (RectOffset) | inset of the content |
| `childAlignment` (TextAnchor) | where the children sit when they do not fill the group |
| `spacing` | gap between children |
| `childControlWidth`/`childControlHeight` | the group sets the children's sizes |
| `childForceExpandWidth`/`childForceExpandHeight` | children share the remaining space even without flexible sizes |
| `childScaleWidth`/`childScaleHeight` | children's scale counts in their size |
| `reverseArrangement` | children are placed in reverse order |

- **Children:** the active children whose rect transforms have no `LayoutElement` with
  `ignoreLayout`.
- **Along the axis:**
  - The group's own sizes:
    - the minimum is the padding plus the sum of the children's minimums plus the spacing;
    - the preferred size is the same, with the children's preferred sizes;
    - the flexible size is the sum of the children's flexible sizes.
  - Space between the group's minimum and preferred sizes interpolates every child between
    its minimum and preferred size.
  - Space beyond the preferred size goes to children in proportion to their flexible sizes,
    with force-expand counting each child's flexible size as at least 1.
  - Children that the group does not control keep their size and are only positioned.
- **Across the axis:** each child gets its size clamped to the group's content size: its
  flexible size takes the rest. Its position follows the alignment.
- **Positions:**
  - The space left over goes to the alignment: left, centre or right along the axis,
    `(1 − alignment) × 0` and so on, as documented.
  - Children are placed at their pivot through `SetInsetAndSizeFromParentEdge`. Unity drives
    their rect transforms (`DrivenRectTransformTracker`).
- **`GridLayoutGroup`:**
  - `cellSize`, `spacing`, `startCorner`, `startAxis`, and `constraint` with `constraintCount`
    (Flexible, FixedColumnCount, FixedRowCount).
  - Cells are laid out from the start corner along the start axis.
  - The cell count per row or column comes from the constraint, or from the width or height
    available.
  - The children's sizes are set to the cell size.
- **Re-layout:** `LayoutGroup` re-lays out when its children, their active state, its own rect
  or its settings change, in that frame's pass. `SetDirty()` re-lays out in the next pass when
  called during a rebuild. Moving a child or the group (position or scale only) re-lays out
  nothing; the child keeps its place until the next re-layout ("Moved children of layout groups
  (measured)" below).

**`ContentSizeFitter`:** `horizontalFit` and `verticalFit`, each Unconstrained, MinSize or
PreferredSize. It sets the rect's size along the axis to the GameObject's layout minimum or
preferred size, keeping the pivot.

**`AspectRatioFitter`:** `aspectMode` and `aspectRatio`.

| Mode | Behaviour |
|---|---|
| None | does nothing |
| WidthControlsHeight | height = width / ratio |
| HeightControlsWidth | width = height × ratio |
| FitInParent | as large as fits in the parent at that ratio, anchors stretched, centred |
| EnvelopeParent | as small as covers the parent at that ratio, anchors stretched, centred |

It updates when its rect or its parent's rect changes, and in the update after it is enabled.

**`CanvasScaler`:**
- `uiScaleMode`:
  - ConstantPixelSize: `scaleFactor`.
  - ScaleWithScreenSize: `referenceResolution`, `screenMatchMode` (MatchWidthOrHeight with
    `matchWidthOrHeight`, which blends the log₂ scales of width and height, Expand, Shrink).
  - ConstantPhysicalSize: `physicalUnit`, `fallbackScreenDPI`, `defaultSpriteDPI`.
- `referencePixelsPerUnit`.
- It sets `Canvas.scaleFactor` and `referencePixelsPerUnit` every update.
- It only acts on a root canvas in screen space; world-space canvases use
  `dynamicPixelsPerUnit`.

### Event system

**`EventSystem`:**
- **Current system:** `current` is the most recently enabled EventSystem; the others stay
  registered.
- **Each Update:**
  - Every `BaseInputModule` on the GameObject gets `UpdateModule`.
  - The first module, in component order, whose `ShouldActivateModule()` is true becomes
    current. Switching modules calls `DeactivateModule` on the old one and `ActivateModule` on
    the new one.
  - The current module's `Process()` then runs. Only the current system processes.
- **Selection:**
  - `firstSelectedGameObject` is selected when the system starts.
  - `SetSelectedGameObject(go[, data])`: selecting the object already selected does nothing.
    Otherwise `IDeselectHandler` is sent to the old object and `ISelectHandler` to the new
    one.
  - `currentSelectedGameObject`.
  - `alreadySelecting` guards against re-entry: a call made while a selection is in progress
    logs an error and returns.
- `pixelDragThreshold`: 10 by default.
- `sendNavigationEvents`: true by default.
- **`isFocused`:** `Application.isFocused` while the application has focus.
- `IsPointerOverGameObject([pointerId])` asks the current module.
- **`RaycastAll(eventData, results)`:**
  - Every active `BaseRaycaster` (`RaycasterManager`) is asked for hits.
  - The hits are sorted:
    1. different raycasters with different event cameras: higher camera depth first;
    2. then a higher `sortOrderPriority`, then a higher `renderOrderPriority`, when the
       raycasters differ;
    3. then a higher sorting layer value and a higher sorting order;
    4. then a higher `depth`;
    5. then a smaller `distance`;
    6. then the earlier index.

**Raycasters:**
- **`GraphicRaycaster`:**
  - **Settings:** `ignoreReversedGraphics` (true by default), `blockingObjects` (None,
    TwoD, ThreeD, All) and `blockingMask`.
  - **Display:** only graphics of the raycaster's canvas, on the pointer's display.
  - **Candidates:** for each graphic of its canvas (`GraphicRegistry`) that is a raycast
    target, has a depth other than −1 (it has been drawn; depths start at 0) and is not
    culled, the pointer must be
    inside the graphic's rect, as `RectTransformUtility.RectangleContainsScreenPoint(rect,
    point, eventCamera, raycastPadding)` tests it (the padding as its offset). Use the
    canvas's event camera for non-overlay canvases. The graphic's `Raycast` must pass.
    - This test is on the rect as drawn on the screen. As measured, a graphic collapsed by
      a zero scale (a menu popping in from scale 0) is hit only on the point or line it
      collapsed to, not everywhere. Mapping the pointer into the rect's local space does
      not give this: a zero scale maps every point onto the pivot.
  - **Order:** the hits are sorted by depth, the highest first. With
    `ignoreReversedGraphics`, graphics facing away from the camera are skipped.
  - **Blocking:** with blocking objects, a 2D or 3D physics hit nearer than the canvas cuts
    off the graphics behind it.
  - **Result fields:**
    - `sortOrderPriority`: the canvas's sorting order for an overlay canvas;
    - `renderOrderPriority`: the canvas's `rootCanvas.renderOrder` for an overlay;
    - `eventCamera`: null for an overlay;
    - each result also has `module`, `distance`, `screenPosition`, `index`, `depth`,
      `sortingLayer`, `sortingOrder`, `worldPosition` and `worldNormal`.
- **`PhysicsRaycaster`** and **`Physics2DRaycaster`** exist if the shapes require them. They
  cast from the event camera.

**`ExecuteEvents`:**
- The static event functions: `pointerEnterHandler`, `pointerExitHandler`,
  `pointerDownHandler`, `pointerUpHandler`, `pointerClickHandler`,
  `initializePotentialDrag`, `beginDragHandler`, `dragHandler`, `endDragHandler`,
  `dropHandler`, `scrollHandler`, `updateSelectedHandler`, `selectHandler`,
  `deselectHandler`, `moveHandler`, `submitHandler` and `cancelHandler`, and
  `pointerMoveHandler` (see "Pointer move events").
- **`Execute<T>(go, data, functor)`:** calls the functor on every component of `go` that
  implements `T` and is an active, enabled Behaviour, or a non-Behaviour. They are called in
  component order. An exception in one is logged, and the rest still run. It returns whether
  any were called.
- **`ExecuteHierarchy`:** from `go` upwards, the first object that handles `T`.
- **`GetEventHandler<T>(go)`:** that object, without executing.
- **`CanHandleEvent<T>`** and **`ValidateEventData<T>`**.

**Event data:**
- **`BaseEventData`:** `currentInputModule`, `selectedObject`, `Use()`, `used` and `Reset()`.
- **`AxisEventData`:** `moveVector` and `moveDir`.
- **`PointerEventData`:**
  - Pointer: `pointerId`, `position`, `delta`, `pressPosition`, `scrollDelta` and `button`.
  - Objects: `pointerEnter`, `pointerPress`, `rawPointerPress`, `lastPress`, `pointerDrag`,
    `pointerClick` and `hovered`.
  - Raycasts: `pointerCurrentRaycast` and `pointerPressRaycast`.
  - Clicks and drag: `eligibleForClick`, `clickCount`, `clickTime`, `useDragThreshold`,
    `dragging`.
  - Derived: `enterEventCamera`, `pressEventCamera`, `IsPointerMoving()`, `IsScrolling()`.
  - The `ToString` is as documented.

### Input modules

**`BaseInputModule`:**
- **`input`:** a `BaseInput` (Unity's `Input` wrapper). It is created on the GameObject, or set
  through `inputOverride`.
- **Protected helpers:**
  - `eventSystem`;
  - `FindFirstRaycast` (the first result with a GameObject), `DetermineMoveDirection`,
    `FindCommonRoot` and `GetBaseEventData`/`GetAxisEventData`.
- **`HandlePointerExitAndEnter(eventData, newEnter)`** (with pointer moves, see "Pointer move
  events (measured 2026-10-23)", which corrects step 1):
  1. When the new target is null or the pointer data's enter is unchanged, exit is sent to the
     whole hovered list. When the new target is null, the list is then cleared.
  2. Otherwise exit is sent to old hovered objects up to the common root with the new target,
     and enter to new ones from the new target up to that root.
  3. `hovered` is updated, and `pointerEnter` is set.
- **Enabling:** `OnEnable` and `OnDisable` notify the event system so it updates its module
  list.

**`PointerInputModule`:**
- **Pointer data:** `GetPointerData(id, out data, create)`, `RemovePointerData`,
  `CopyFromTo`, `StateForMouseButton`, `GetLastPointerEventData` and `ClearSelection`.
- **`GetMousePointerEventData(id)`:**
  - The mouse pointer data is updated: position from `input.mousePosition`, the delta since
    last frame, scroll from `input.mouseScrollDelta`, and the button state per mouse button.
  - Left is the pointer id -1, right -2 and middle -3. Right and middle data are copies of the
    left with their own buttons.
  - The raycast is redone: `RaycastAll`, then `FindFirstRaycast`.
- **`ProcessMove`:** enter and exit for the current raycast target, unless the cursor is
  locked.
- **`ProcessDrag`:**
  - It needs a moving pointer with a drag target, and the cursor not locked.
  - Dragging starts when the pointer moves beyond `pixelDragThreshold` from the press position
    (squared distance), or always without `useDragThreshold`. `beginDrag` is sent, then
    `drag` each move.
  - A press that becomes a drag is no longer eligible for click: `pointerUp` is sent to the
    pressed object then.
- **`IsPointerOverGameObject(id)`**, `kMouseLeftId`, `kFakeTouchesId` and the
  `MouseButtonEventData`/`ButtonState`/`MouseState` helper classes are part of the contract.

**`StandaloneInputModule`:**
- **Settings:** `horizontalAxis`, `verticalAxis`, `submitButton`, `cancelButton`,
  `inputActionsPerSecond` (10) and `repeatDelay` (0.5).
- **`ShouldActivateModule`** is true with the base condition and any of:
  - submit, cancel or a navigation axis pressed or non-zero;
  - the mouse moved;
  - a mouse button pressed;
  - touches present.
- **`Process`:**
  1. Unless the application is unfocused (with `isFocused`), the selected object gets
     `updateSelected`.
  2. Then touch events, when touches are supported and used, or else the mouse.
  3. Then, with navigation events enabled, move and submit/cancel.
- **Mouse:**
  - For left, right and middle in turn, pressed and released are processed. The left button
    also gets move and drag; the others get drag only.
  - A non-zero scroll delta sends `scroll` up the hierarchy from the current target.
- **Press:**
  1. `pressPosition`, `pointerPressRaycast` and `eligibleForClick` are set, and the delta is
     zeroed.
  2. The selection is cleared when the new press target differs from the selected object.
  3. `pointerDown` is sent up the hierarchy. When nothing handles it, the target is the
     object's click handler.
  4. A click within 0.3 s on the same object increments `clickCount`; otherwise it resets to
     1.
  5. `pointerPress`, `rawPointerPress`, `pointerClick` and `clickTime` are set.
  6. The drag handler gets `initializePotentialDrag`.
- **Release:**
  1. `pointerUp` goes to the press target.
  2. `pointerClick` goes to the press target when it equals the click handler under the
     pointer and the press is still eligible.
  3. Otherwise `drop` goes to the drop handler under the pointer, when dragging.
  4. `endDrag` is sent when dragging.
  5. The press fields are cleared.
  6. Enter and exit are processed for the current target. When the pointer left the press
     target, exit and enter are sent again, so hover follows the pointer.
- **Navigation:** move events repeat after `repeatDelay` at `inputActionsPerSecond`, reading
  the raw axes. Submit and cancel go to the selected object.

### Selectables

**`Selectable`:**
- **Settings:** `interactable` (true), `navigation` (Automatic), `transition` (ColorTint),
  `colors`, `spriteState`, `animationTriggers` and `targetGraphic`.
- **`allSelectablesArray`, `allSelectableCount`, `AllSelectablesNoAlloc`:** every enabled
  Selectable.
- **`IsInteractable()`** is `interactable` and every enclosing `CanvasGroup` up to one that
  ignores its parents allowing interaction.
- **State:** Normal, Highlighted, Pressed, Selected or Disabled.
  - Disabled when not interactable.
  - Pressed while the pointer that pressed it is down, inside or not.
  - Selected when it is the event system's selection.
  - Highlighted while the pointer is over it.
  - Otherwise Normal.
  - It responds to pointer down, up, enter and exit, select and deselect, and moves.
- **Transitions:**
  - ColorTint: `targetGraphic.CrossFadeColor(color × colorMultiplier, fadeDuration, true, true)`.
    It applies instantly when the transition happens during `OnEnable`, or when the object is
    inactive.
  - SpriteSwap: sets the target image's `overrideSprite` from `spriteState`.
  - Animation: sets the Animator trigger named in `animationTriggers` and resets the others.
- **Pointer down:** a left-button press on an interactable selectable with navigation selects
  it, unless it is already selected.
- **Navigation:** `FindSelectableOnLeft`, `…Right`, `…Up` and `…Down`.
  - Explicit: uses the navigation's targets.
  - Automatic, Horizontal and Vertical: the nearest selectable in that direction, scored by
    distance and angle, as documented.
  - None: there is no navigation.

**`Button`:** `onClick` (`ButtonClickedEvent`, a UnityEvent):
- It is invoked on `OnPointerClick` with the left button when interactable and active.
- It is invoked on `OnSubmit`, which also shows Pressed and then fades back.

**`Toggle`:**
- **Settings:** `isOn`, `group`, `graphic` and `toggleTransition` (None, or Fade over
  0.1 s).
- **Events:** `onValueChanged` (`ToggleEvent`, `UnityEvent<bool>`).
- **Value:**
  - A click with the left button or a submit flips it.
  - `SetIsOnWithoutNotify` sets it without the event.
  - The `graphic`'s alpha shows the state, through `CrossFadeAlpha`.
- **Groups:** joining a group with `isOn` true makes it the group's active toggle.
  - **`ToggleGroup`:** `allowSwitchOff`.
  - `NotifyToggleOn(toggle[, sendCallback])` turns the others off.
  - `RegisterToggle` and `UnregisterToggle`.
  - `AnyTogglesOn`, `ActiveToggles`, `GetFirstActiveToggle` and `SetAllTogglesOff`.
  - Without `allowSwitchOff`, turning off the only toggle that is on is refused.

**`Slider`:**
- **Settings:** `fillRect`, `handleRect`, `direction` (LeftToRight, RightToLeft, BottomToTop,
  TopToBottom), `minValue`, `maxValue`, `wholeNumbers`, `value` and `normalizedValue`.
- **Events:** `onValueChanged` (`SliderEvent`).
- **`SetValueWithoutNotify`** sets the value without the event.
- **Visuals:**
  - The fill rect's anchors span 0 to the value along the direction; an Image fill of type
    Filled instead gets `fillAmount`.
  - The handle's anchors sit at the value.
- **Input:**
  - Dragging the handle moves the value, keeping the offset where the handle was grabbed.
  - Clicking the track jumps to the point.
  - The arrow keys step by 1 for whole numbers, otherwise by a tenth of the range.

**`Scrollbar`:**
- **Settings:** `handleRect`, `direction`, `value` (0 to 1), `size` (0 to 1) and
  `numberOfSteps`.
- **Events:** `onValueChanged`.
- **Visuals:** the handle's anchors span `size` at `value`.
- **Input:** dragging the handle moves the value. A press on the track outside the handle
  centres the handle on the press point at once, and the handle then follows the pointer while
  the button is held; until it moves, the press point is re-applied at the end of each frame.
  The protected `ClickRepeat` overloads and `Update` are part of the surface ("Scrollbar
  presses on the track", measured, below).

**`ScrollRect`:**
- **Settings:**
  - `content`, `viewport` (or its own rect), `horizontal`, `vertical`;
  - `movementType` (Unrestricted, Elastic, Clamped) and `elasticity` (0.1);
  - `inertia` (true), `decelerationRate` (0.135) and `scrollSensitivity` (1);
  - the scrollbars, with their visibility (Permanent, AutoHide, AutoHideAndExpandViewport) and
    spacing;
  - `onValueChanged` (`ScrollRectEvent`, `UnityEvent<Vector2>`).
- **Values:** `normalizedPosition`, `horizontalNormalizedPosition`,
  `verticalNormalizedPosition`, `velocity` and `StopMovement()`.
- **Dragging** with the left button moves the content.
  - When elastic, overshoot beyond the content's bounds is rubber-banded; the standard
    rubber-band function is used.
  - **On release, with inertia:** the velocity decays by `decelerationRate^dt`.
  - **Elastic:** the content returns to its bounds with `Mathf.SmoothDamp` over
    `elasticity`.
  - **Clamped:** the content stays within the bounds.
- **Scroll wheel:** it moves the content by `scrollSensitivity` × the delta. Vertical scrolling
  uses the y delta, and a horizontal-only rect uses whichever delta is larger.
- **When it updates:**
  - Positions update in `LateUpdate`, using unscaled delta time for the movement. The
    scrollbars are updated, and `onValueChanged` fires when the normalized position changes.
  - It also updates during `Rebuild(PostLayout)`.

  KScrollRect overrides `LateUpdate`.
- **Layout:** it is an `ILayoutElement` (all -1) and an `ILayoutGroup`, so it lays out its
  scrollbars and viewport.

**ScrollRect: normalized position and scrollbars, measured** (2026-10-22, `tools/UnityProbe`,
the `UIS scroll` lines, in the original player and in ours; a 200 × 100 viewport unless
stated). This implementation already does all of this, on these measurements; keep it.
- **Bounds.** Both are boxes in the viewport's local space (the viewport, or the scroll rect's
  own rect when there is none), kept as a centre and extents, with min = centre − extents:
  - the view: the viewport's rect;
  - the content: the box around the content's four world corners
    (`RectTransform.GetWorldCorners`), each taken into the viewport's space with its
    `worldToLocalMatrix`. Scale and rotation count: content of 400 × 400 at scale 0.25 is
    100 × 100.
- **Content smaller than the view on an axis** is grown to the view's size about its pivot:
  the centre moves by −(view size − content size) × (pivot − 0.5) and the size becomes the
  view's. Content exactly the view's size is not changed.
- **The normalized position on an axis** (0 at the left or bottom of the scrollable range, 1
  at the right or top):
  - when the (grown) content is no larger than the view, or its size equals the view's as
    `Mathf.Approximately` compares them: 1 when the view's min is greater than the content's
    min, else 0. That comparison of the mins is exact, with no tolerance. Without the
    approximate test, content an ulp larger than the view gives fractions such as −0.5 or
    0.25, which the original never reported;
  - otherwise (view min − content min) / (content size − view size), not clamped. Measured
    on 400 × 300 content, offset −250 … 250: −1.25 to 2.25, both axes.
  - The getter computes the bounds afresh from the current transforms, so it is valid before
    the first `LateUpdate`, and whichever of `horizontal`, `vertical` and `movementType`.
  - This formula reproduces all 172 axis values the probe recorded in the original, from the
    logged inputs, bit for bit (`tools/UnityProbe/uis-check.py fit`), the fractional ones
    included.
- **What the exactness means.** Where the content's size equals the viewport's (the content
  stretched to it, or laid out to its width, as Sweep By Type's list is), 0 or 1 is decided
  by the rounding of the transformed corners:
  - Content as wide as the view and offset by −0.1, −0.01, −0.001 and −0.0001 reports 1; by
    0, +0.00001 and +0.001 it reports 0; by −0.00001 either, by the layout.
  - In 48 random exactly-fitting layouts (sizes 80–600, fractional positions, half of them
    under a parent at scale 0.86) the original reported 1 on about 40 % of the axes.
  - Both players apply this formula. Their difference on such axes comes from the engine's
    corners and matrices, 1 to 3 ulps apart in 57 of 86 cases. That belongs to the engine's
    transform arithmetic, not to this package; do not add a tolerance here to hide it.
- **Scrollbars.** On an update that finds the view bounds, the content bounds or the content's
  position changed since the previous one (always on the first):
  - each assigned scrollbar's `size` becomes clamp01((view size − |overshoot|) / content
    size), with the (grown) content size and the overshoot as found at the start of that
    update, before the content is moved back. Example: Clamped, 100 × 50 content offset by 30: size 0.85 though the content
    is clamped in the same update; it stays 0.85, since nothing changes afterwards;
  - its `value` becomes the normalized position: 0 or 1 when the content fits, as above.
  - Before that update the scrollbar keeps its own values (a new one: value 0, size 0.2).
  - Setting `value` invokes the scrollbar's `onValueChanged`, to which the scroll rect listens,
    so it sets the normalized position on that axis. When the content fits, setting 1 moves
    the grown content's min onto the view's min. Measured: 100 × 50 content offset (−20,
    −10), Unrestricted: the first update sets both scrollbars to 1 and the content snaps to
    (0, 0). With offsets ≥ 0 the value is 0, which the new scrollbar already holds, so
    nothing is invoked and the content stays where it is.
- **Movement types** with content that fits (100 × 50 offset by (30, −20), pivots (0,1),
  (0.5,0.5), (1,0)):
  - Clamped puts it back at once, in the first update (anchoredPosition (0, 0));
  - Elastic springs back over time (`SmoothDamp` with `elasticity` and unscaled time; after
    0.21 s, 2.4 of the 30 remained);
  - Unrestricted leaves it, except through the scrollbar feedback above.
- **Content resized at run time** (400 × 300 scrolled to (0.5, 0.5), then shrunk to 100 × 50,
  then grown back): the normalized position follows the new bounds in the next update. While
  the content fits, setting `normalizedPosition`, or either axis, does not move it.
- **Not measured:** dragging and the scroll wheel with content that fits; `AutoHide`
  scrollbar visibility.

### InputField and Dropdown (legacy)

**`InputField`:**
- **Settings:**
  - `text` and `textComponent` (a legacy `Text`), `placeholder`;
  - `characterLimit`, `contentType` and the line, input, keyboard and character validation
    types it sets;
  - `caretBlinkRate`, `caretWidth`, `selectionColor`, `readOnly`;
  - `onValueChanged`, `onEndEdit` and `onSubmit`.
- **Editing:** on select it starts editing: focus and caret. Keys come from `Event.PopEvent`
  in `OnUpdateSelected`, with the documented editing keys and the clipboard through
  `GUIUtility.systemCopyBuffer`.
- **Display:** the caret and selection are drawn as a child graphic.

  The game has one InputField in its data.

**`Dropdown`:**
- **Settings:** `options` (`OptionData`: text and image), `captionText`, `captionImage`,
  `itemText`, `itemImage`, `template` and `value`.
- **Events:** `onValueChanged`.
- **Showing the list:** `Show()` instantiates the template under a new canvas with a blocker
  behind it, one item per option, and selects the current value. A click on an item sets the
  value and hides the list. `Hide()` removes it after an alpha fade of 0.15 s.

  The game uses Dropdown in three places.

### DefaultControls (measured)

Mods build controls from code with `DefaultControls` (Ony's shared library: `CreateDropdown` and
`CreateScrollView`). `src/UnityEngine.UI/package-required-types.tsv` brings its shapes into the
generated files. Its public surface, from the package's metadata:
- `DefaultControls.Resources`, a struct of seven public `Sprite` fields: `standard`, `background`,
  `inputField`, `knob`, `checkmark`, `dropdown`, `mask`;
- `DefaultControls.IFactoryControls`, an interface with
  `GameObject CreateGameObject(string name, params Type[] components)`;
- `static IFactoryControls factory { get; }`: a factory that makes a new GameObject with that
  name and those components added in that order;
- `CreatePanel`, `CreateButton`, `CreateText`, `CreateImage`, `CreateRawImage`, `CreateSlider`,
  `CreateScrollbar`, `CreateToggle`, `CreateInputField`, `CreateDropdown`, `CreateScrollView`,
  each `static GameObject (Resources)`.

Each method returns a new root GameObject with no parent, on layer 0, all of its objects
active except where stated, every object made through `factory`. The hierarchies below were
measured in the original player (PerfProbe's measure mode), each sprite field named after
itself; with empty `Resources` the result is the same with every sprite null. Children are listed
in sibling order. Components are listed in order. Every object that draws has a `CanvasRenderer`
right after its `RectTransform`, with `cullTransparentMesh` true.

**Notation.** `rect` gives anchorMin–anchorMax, pivot, sizeDelta and anchoredPosition.
- Images: material default, raycast target on, fill centre on, preserve aspect off,
  pixels-per-unit multiplier 1, colour white unless given.
- Texts: the built-in font `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`, size 14,
  style Normal, colour (50, 50, 50, 255), rich text on, raycast target on, horizontal overflow
  Wrap, vertical overflow Truncate, line spacing 1, best fit off, align by geometry off, unless
  given.
- Selectables: transition ColorTint, with colours normal (255, 255, 255, 255), highlighted
  (245, 245, 245, 255), pressed (200, 200, 200, 255), selected (245, 245, 245, 255), disabled
  (200, 200, 200, 128), colour multiplier 1, fade 0.1 s, navigation Automatic.

- **CreatePanel:** "Panel": rect (0,0)–(1,1), pivot (0.5,0.5), size (0,0), position (0,0).
  Image: sprite `background`, Sliced, colour (255, 255, 255, 100).
- **CreateButton:** "Button (Legacy)": rect centre anchors (0.5,0.5), size (160,30). Image:
  `standard`, Sliced. Button: target graphic its own Image.
  - "Text (Legacy)": stretched (0,0)–(1,1), size (0,0). Text "Button", aligned MiddleCenter.
- **CreateText:** "Text (Legacy)": centre anchors, size (160,30). Text "New Text", UpperLeft.
- **CreateImage:** "Image": centre anchors, size (100,100). Image: no sprite, Simple.
- **CreateRawImage:** "RawImage": centre anchors, size (100,100). RawImage: no texture, white,
  uvRect (0,0,1,1).
- **CreateSlider:** "Slider": centre anchors, size (160,20). It has no CanvasRenderer or Image.
  Slider: direction LeftToRight, min 0, max 1, value 0, not whole numbers; fill rect "Fill";
  handle rect "Handle"; target graphic the handle's Image.
  - "Background": anchors (0,0.25)–(1,0.75), size (0,0). Image `background`, Sliced.
  - "Fill Area": anchors (0,0.25)–(1,0.75), size (−20,0), position (−5,0). No graphic.
    - "Fill": anchors (0,0)–(0,1), size (10,0). Image `standard`, Sliced.
  - "Handle Slide Area": stretched (0,0)–(1,1), size (−20,0). No graphic.
    - "Handle": anchors (0,0)–(0,1), size (20,0). Image `knob`, Simple.
- **CreateScrollbar:** "Scrollbar": centre anchors, size (160,20). Image `background`, Sliced.
  Scrollbar: direction LeftToRight, size 0.2, value 0, steps 0; handle rect "Handle"; target
  graphic the handle's Image.
  - "Sliding Area": stretched, size (−20,−20).
    - "Handle": anchors (0,0)–(0.2,1), size (20,20). Image `standard`, Sliced.
- **CreateToggle:** "Toggle": centre anchors, size (160,20). No graphic. Toggle: on; graphic
  "Checkmark"; target graphic "Background"'s Image; no group; toggle transition Fade.
  - "Background": anchors (0,1)–(0,1), size (20,20), position (10,−10). Image `standard`,
    Sliced.
    - "Checkmark": centre anchors, size (20,20). Image `checkmark`, Simple.
  - "Label": stretched, size (−28,−3), position (9,−0.5). Text "Toggle", UpperLeft.
- **CreateInputField:** "InputField (Legacy)": centre anchors, size (160,30). Image
  `inputField`, Sliced. InputField:
  - text component "Text (Legacy)", placeholder "Placeholder", text empty;
  - content type Standard, line type SingleLine, character limit 0;
  - caret blink rate 0.85, caret width 1, selection colour (168, 206, 255, 192), not read-only;
  - target graphic its own Image.

  Children:
  - "Placeholder": stretched, size (−20,−13), position (0,−0.5). Text "Enter text...", style
    Italic, colour (50, 50, 50, 128), UpperLeft, **no font** (null).
  - "Text (Legacy)": same rect. Text empty, UpperLeft, rich text **off**, horizontal overflow
    **Overflow**.
- **CreateDropdown:** "Dropdown (Legacy)": centre anchors, size (160,30). Image `standard`,
  Sliced. Dropdown:
  - template "Template", caption text "Label", item text "Item Label", no caption or item
    image;
  - options "Option A", "Option B", "Option C", value 0;
  - target graphic its own Image, alpha fade speed 0.15.

  Children:
  - "Label": stretched, size (−35,−13), position (−7.5,−0.5). Text "Option A", MiddleLeft.
  - "Arrow": anchors (1,0.5)–(1,0.5), size (20,20), position (−15,0). Image `dropdown`,
    Simple.
  - "Template", **inactive**: anchors (0,0)–(1,0), pivot (0.5,1), size (0,150), position
    (0,2). Image `standard`, Sliced. ScrollRect:
    - content "Content", viewport "Viewport";
    - vertical only, vertical scrollbar "Scrollbar", horizontal visibility Permanent,
      vertical visibility AutoHideAndExpandViewport, vertical spacing −3, horizontal spacing 0;
    - movement Clamped, elasticity 0.1, inertia on, deceleration 0.135, sensitivity 1.

    Its children:
    - "Viewport": stretched, pivot (0,1), size (−18,0). Image `mask`, Sliced. Mask with the
      graphic hidden.
      - "Content": anchors (0,1)–(1,1), pivot (0.5,1), size (0,28). No graphic.
        - "Item": anchors (0,0.5)–(1,0.5), size (0,20). No graphic. Toggle: on; graphic
          "Item Checkmark"; target "Item Background"'s Image; toggle transition Fade.
          - "Item Background": stretched, size (0,0). Image, no sprite, Simple, colour
            (245, 245, 245, 255).
          - "Item Checkmark": anchors (0,0.5)–(0,0.5), size (20,20), position (10,0). Image
            `checkmark`, Simple.
          - "Item Label": stretched, size (−30,−3), position (5,−0.5). Text "Option A",
            MiddleLeft.
    - "Scrollbar": anchors (1,0)–(1,1), pivot (1,1), size (20,0). Image `background`,
      Sliced. Scrollbar: BottomToTop, size 0.2, value 0, handle "Handle", target its Image.
      - "Sliding Area": stretched, size (−20,−20).
        - "Handle": anchors (0,0)–(1,0.2), size (20,20). Image `standard`, Sliced.
- **CreateScrollView:** "Scroll View": centre anchors, size (200,200). Image `background`,
  Sliced, colour (255, 255, 255, 100). ScrollRect:
  - content "Content", viewport "Viewport";
  - horizontal and vertical, scrollbars "Scrollbar Horizontal" and "Scrollbar Vertical", both
    visibilities AutoHideAndExpandViewport, both spacings −3;
  - movement Elastic, elasticity 0.1, inertia on, deceleration 0.135, sensitivity 1.

  Children:
  - "Viewport": stretched, pivot (0,1), size (0,0). Image `mask`, Sliced. Mask with the graphic
    hidden.
    - "Content": anchors (0,1)–(1,1), pivot (0,1), size (0,300). No graphic.
  - "Scrollbar Horizontal": anchors (0,0)–(1,0), pivot (0,0), size (0,20). Image `background`,
    Sliced. Scrollbar LeftToRight, size 0.2, value 0, handle "Handle".
    - "Sliding Area": stretched, size (−20,−20).
      - "Handle": anchors (0,0)–(0.2,1), size (20,20). Image `standard`, Sliced.
  - "Scrollbar Vertical": anchors (1,0)–(1,1), pivot (1,1), size (20,0). Image `background`,
    Sliced. Scrollbar BottomToTop, size 0.2, value 0, handle "Handle".
    - "Sliding Area": stretched, size (−20,−20).
      - "Handle": anchors (0,0)–(1,0.2), size (20,20). Image `standard`, Sliced.

Every rect not stated otherwise has pivot (0.5,0.5) and position (0,0). Where a size is stated,
anchors not given are the centre (0.5,0.5).

### Pointer move events (measured 2026-10-23)

Found in a mod play-test (issue #2): Resource Dashboard (Workshop 3671379574) does not load.
Its graph view implements `IPointerMoveHandler`, which this package lacks, so loading the mod's
types fails with a `TypeLoadException`. The game's `Assets.OnPrefabInit` then fails on the same
types, and the player exits while loading. No game assembly names the interface. Add it to
`src/UnityEngine.UI/package-required-types.tsv` and regenerate (`scripts/regen-packages.sh`):

```
UnityEngine.UI	UnityEngine.EventSystems.IPointerMoveHandler	# mods (Resource Dashboard's graph view): OnPointerMove
```

**Interface, from the package's metadata:**
- `IPointerMoveHandler : IEventSystemHandler`, with `void OnPointerMove(PointerEventData
  eventData)`;
- `ExecuteEvents.pointerMoveHandler`, a static `EventFunction<IPointerMoveHandler>` property
  beside `moveHandler`. It is not null.

**When it is sent.** Measured with `PROBE_SCENARIO=pointermove` (tools/UnityProbe,
`PointerMove.cs`, lines `PM …`). It drives the game's own current module, a
`StandaloneInputModule` (Klei's `VirtualInputModule` is there but disabled), through
`inputOverride`, a frame at a time. The test hierarchy is on an overlay canvas: P (400 × 300,
with enter, exit and move handlers) holds A (enter, exit and move handlers, and a second move
handler on another component), A's child A1 (no handler), B (move handler only), C (no
handler), D (move handler, not a raycast target) and E (a disabled move handler).

Move events are sent from `HandlePointerExitAndEnter(eventData, newEnterTarget)`, together with
enter and exit, with `ExecuteEvents.Execute` on each object (not `ExecuteHierarchy`). The
mouse's left-button data passes through it each frame, as before. Three cases:

1. **The new target is null, or the data's `pointerEnter` is null.** Every object in `hovered`,
   in list order, gets move and then exit. Move is sent here even when the pointer has not
   moved. The list is cleared. When the new target is null, `pointerEnter` becomes null and
   nothing more happens. Otherwise go on with case 3's enter part.
   - Measured: from A (hovered [P, canvas, A]) to a point off the screen, P gets move then exit,
     then A gets move and its second handler's move, then exit. Each sees `hovered` still
     [P, canvas, A].
   - Measured: with no target and nothing hovered, nothing is sent, moving or not.
2. **The new target is the current `pointerEnter`.** When the pointer is moving
   (`IsPointerMoving()`: a non-zero `delta`), each object in `hovered`, in list order, gets
   move. Nothing else is sent. When it is still, nothing is sent.
   - Measured: on A, moving: A (both handlers), then P. On A1 (hovered [A, P, canvas, A1]):
     A, then P. A1 has no handler, and the event does not bubble from it.
   - Measured: after entering B from P, `hovered` is [P, canvas, B], and a move goes to P
     before B. Entered objects are appended to the list.
3. **The target changed.** Find the common root, as before.
   - **Exit:** walk from the old `pointerEnter` up to the common root, not including it. Each
     object gets move, then exit, and is then removed from `hovered`. Move is sent even with
     a zero delta: measured when A moved away from under a still pointer (A gets move, then
     exit, with `delta` (0,0) and `IsPointerMoving()` false).
   - **Enter:** set `pointerEnter` to the new target. Walk from the new target up to the
     common root, not including it. Each object gets enter, then move, and is then appended
     to `hovered`. Move is sent even with a zero delta: measured for a press without
     movement, and for A moving back under a still pointer.
   - **Objects at or above the common root get no move in that frame,** although the pointer
     moved. Measured from A1 to P's bare area: A1 and A get move then exit; P gets nothing.
     From A to its child A1: nothing is sent to A or P.
   - Measured: from B to A (common root P), B gets move then exit, then A gets enter, then
     move.
   - Measured: from A to another canvas's object (no common root), A gets move then exit, then
     P gets move then exit. Each is removed as it goes: P sees `hovered` [P, canvas].

The event data in each call: `position` and `delta` of this frame's pointer data;
`pointerEnter` is still the old target during the exit walk, and the new one during the enter
walk; `pointerCurrentRaycast` is this frame's hit. `used` is false. A disabled handler
component, or one on an inactive object, gets nothing, and the other components still run
(`Execute`'s rule). An object with `raycastTarget` false is never the target, so it gets
nothing (D).

**Pressed buttons change nothing:** with the left button held, the same events are sent as
without it (measured from A across P to B while held). Releasing the button without moving
sends nothing more. Not measured: a press on an object with a drag handler. The probe's objects
have none, so `dragging` stays false and `pointerDrag` null.

This replaces the description of `HandlePointerExitAndEnter` under Input modules. That said
that exit goes to the whole `hovered` list when the enter is unchanged. Instead, an unchanged
enter sends only moves, and only while the pointer moves.

### Moved children of layout groups (measured 2026-10-23)

Found in a mod play-test: DraggableUI (Workshop 1870540864) moves a window by Alt + drag. Its
`DraggablePanel.Update` sets the screen's own `RectTransform.anchoredPosition` (the component's
`GetComponentInParent<RectTransform>()`, which is the screen's) every frame of the drag, from the
pointer minus the offset it had at the press, and stops on release. Over the details panel the
drag moves two screens, `RootMenuRight` and its child `DetailsScreen`. In the original both stay
where they were put (`DetailsScreen` 302 → −58). In ours `DetailsScreen` is back at 302 right
after the release. The hierarchy, from the original's dump:

- `BottomPanelRight`: `VerticalLayoutGroup`, `ContentSizeFitter`;
- `RootMenuRight`: `RootMenu`, `LayoutElement`, `ToggleGroup`, `VerticalLayoutGroup`,
  `ContentSizeFitter`, `DraggablePanel`;
- `DetailsScreen`: `DetailsScreen`, `VerticalLayoutGroup`, `LayoutElement`, `Image`,
  `DraggablePanel`.

**Measured** with `PROBE_SCENARIO=layoutcull` (tools/UnityProbe, `LayoutCull.cs`, lines `LC lay
…`; `lc-check.py`). The setup is a `VerticalLayoutGroup` G (300 × 400, spacing 10, upper left, no
force-expand) on an overlay canvas, with children A, B and C. Each child is an `Image` with a
`LayoutElement` (preferred 120 × 50; its own size 80 × 40). G counts its `SetLayoutHorizontal`
calls, and B and G count their `OnRectTransformDimensionsChange`. The configurations: with and
without a `ContentSizeFitter` on G (vertical preferred), child size control on both axes, on
neither and on one, B with `ignoreLayout`, and B with a `ContentSizeFitter` of its own. In
each, one change is made, then two frames are watched phase by phase. Then A's preferred
height changes (a re-layout from elsewhere), and one more frame is watched.

1. **Moving does not re-lay out.** Setting a child's `anchoredPosition`, `localPosition`,
   `position` or `localScale`, or the group's `anchoredPosition`, sends no
   `OnRectTransformDimensionsChange` to the child or the group, and marks nothing for rebuild:
   the group's layout calls stay at their count, for every configuration. The child stays
   where it was put, frame after frame. A drag that sets B's `anchoredPosition` every frame
   for three frames leaves B at the last value in every configuration. That includes B with
   its own `ContentSizeFitter`. A hierarchy made in that frame has one deferred re-layout of
   its own, in its third frame, which a drag in progress overrides at once.
2. **Changing a size re-lays out in that frame.** Setting a child's `sizeDelta`, `pivot` or
   anchors, or the group's `sizeDelta`, sends `OnRectTransformDimensionsChange` from the setter
   (once; twice for anchors, one per property). The group then re-lays out in that frame's
   canvas update, before `Canvas.willRenderCanvases` returns. A size the group controls is set
   back, and every position the group drives is set again.
3. **The next re-layout puts a moved child back.** When the group re-lays out for any reason,
   B is placed at its layout position again, in the canvas update of the frame the rebuild was
   asked in. This holds with child size control on or off, with or without a fitter on the
   group, and the moved axis does not matter. A child whose `LayoutElement` has `ignoreLayout`
   keeps both its position and its size. Measured to put B back:
   - `LayoutRebuilder.MarkLayoutForRebuild` on B or on G;
   - `SetLayoutDirty()` on B's `Image`, or disabling that `Image`;
   - a changed `LayoutElement` value on B or A;
   - a changed group setting (`spacing`);
   - `SetActive(false)` on a sibling, a new child, `SetAsLastSibling()` on B, and disabling
     and enabling the group;
   - `LayoutRebuilder.ForceRebuildLayoutImmediate(G)`, at once.
   - Nested groups: G (`VerticalLayoutGroup` + fitter) > D (`VerticalLayoutGroup`) > T1
     (`LayoutElement`) and T2 (`TextMeshProUGUI`). Moving D, then changing T1's preferred
     height or T2's text, re-lays out from G and puts D back. With a `LayoutElement` in
     place of D's group, T1's change re-lays out nothing (the root search stops at D), and D
     stays moved.

   Not putting B back: `SetVerticesDirty()`, a colour change, setting a `LayoutElement` value
   or a group setting to the value it has, setting a text to the text it has, and
   `Canvas.ForceUpdateCanvases()` with nothing marked.
4. **`OnRectTransformDimensionsChange` is sent once, from the change, and only for the rect's
   size**, on a canvas that is not pixel perfect (the probe's canvas; on a pixel-perfect one
   a move is told after the canvas update, see "Moves on pixel-perfect canvases" below). This
   includes changes made during the canvas update:
   - A rect resized or re-pivoted from a `Canvas.willRenderCanvases` handler gets the message
     once, from the setter. It gets nothing after the update.
   - A rect moved there (`anchoredPosition`, `localPosition`, `localScale`) gets none.
   - A child resized by its group during the layout rebuild gets one message, during the
     rebuild (`CanvasUpdateRegistry.IsRebuildingLayout()` true).
   - A child moved back by its group gets none, during the rebuild or after it. Across the
     whole run, B's message never came after the canvas update: 0 lines in phase R, against
     419 during the layout rebuild.

**Where ours deviates.** `CanvasSystem.RunCanvasUpdate` (`src/OniBrutal.UnityEngine/Engine/UI/
CanvasSystem.cs`) sends `OnRectTransformDimensionsChange` again, after the update, to every
`RectTransform` that `RectTransformNative.Resolve` saw moved or resized during it
(`NoteChangedInUpdate`). That breaks rule 4 twice:
- **Moves.** A child moved back by its group gets a message after the update (B's count rises by
  one on every re-layout that moves it). When the child has a component that marks the layout
  on that message, such as a `ContentSizeFitter`, the layout is rebuilt again the next frame.
  While something keeps moving the child, the loop never ends. Measured in ours with B carrying
  a `ContentSizeFitter`: during the drag G re-lays out every frame (`SetLayoutHorizontal` + 1 a
  frame), and B is back at 40 in each frame's canvas update, where the original leaves it at
  −260. In the game, `RootMenuRight` (with its `ContentSizeFitter`) is moved by
  `BottomPanelRight`'s group. So once any re-layout of `BottomPanelRight` happens during the
  drag, it repeats every frame, and the one after the release puts `RootMenuRight` and
  `DetailsScreen` back. In the original nothing re-lays out after the release, so the moved
  windows stay.
- **Resizes.** Every re-layout that resizes or moves a rect is followed by another one: G's layout
  calls are one higher than the original's after each size change and each poke. A rect resized
  from a `willRenderCanvases` handler gets two messages (the original, one). One moved there
  through `anchoredPosition` gets one (the original, none).

**What to do.**
- Send `OnRectTransformDimensionsChange` only when the rect's size or its pivot-relative rect
  changes, from the change itself, and never again after the canvas update.
- A position change (`anchoredPosition`, `localPosition`, `position`, scale) sends nothing,
  in the canvas update or out of it.
- `CanvasTests.ARectChangedDuringTheCanvasUpdateHearsOfItAgainAfterIt` asserts the re-send. The
  news-tile fix of 2026-10-20 (PLAN.md, from PerfProbe `layout.txt`) relied on it, and that
  trace attributed the tiles' second message to the re-send. This probe sees no second message
  for a rect resized in the update. Re-check the tiles before deciding: what sends their second
  message in the original, and whether it reaches only rects whose size changed. Moves must not
  be re-sent in any case.

| Case (`lay …`) | original: B after the change, after the re-layout | ours before |
|---|---|---|
| csf.cwh, B `anchoredPosition.x` −200 | −140 held, no layout call; back at 60 on A's change | the same |
| csf.cwh, B `localPosition`, `position`, `localScale`; G `anchoredPosition` | held, no layout call | the same |
| csf.cwh, B `sizeDelta.x` +30 | width back to 120 in that frame's update; 1 message to B | the same, 2 layout calls and 2 messages |
| csf.none, B `sizeDelta.x` +30 (size not controlled) | width 110 kept, x 55 in that frame's update | the same |
| csf.cwh.ignB, B moved −200 | held, also after A's change | the same |
| csf.none.Bcsf, drag (B set each frame, three frames) | −260 held, layout calls unchanged | back at 40 every frame, one layout call a frame |
| any, drag without a fitter on B | the last value held | the same |
| poke `SetVerticesDirty`, colour, same values, `ForceUpdateCanvases` | B stays moved | the same |
| poke `MarkLayoutForRebuild(B)`, `SetLayoutDirty`, sibling inactive, `SetAsLastSibling` | B put back that frame | the same, with one extra layout pass |
| nested, D moved, T2 text changed | D put back | the same, with one extra layout pass |
| solo rect, `anchoredPosition` set in `willRenderCanvases` | no message | 1 message after the update |
| solo rect, `sizeDelta` or `pivot` set in `willRenderCanvases` | 1 message | 2 messages |
| DraggableUI, `DetailsScreen.anchoredPosition.x` after the drag | −58 (from 302) | 302 |

### Moves on pixel-perfect canvases (measured 2026-10-24)

Found in QA's dogfood `intro`: after the Spaced Out intro video, the main menu's news tiles
(`MOTD`, a `HorizontalLayoutGroup` of three `MotdBox` tiles) sat stacked at the row's right
edge. Each tile's `ImageButton` has a `MotdBox_ImageButtonLayoutElement`, whose preferred width
is its height × a ratio, set on `OnRectTransformDimensionsChange`. The row first lays out with
the buttons' heights still unset, so with widths of 0. PerfProbe's `layout.txt` trace in the
original shows the buttons hearing `OnRectTransformDimensionsChange` again after each frame's
canvas update, from the engine (the managed stack holds only the handler), with
`IsRebuildingLayout()` false; each marks the layout, and the row lays out again in the next
frame, three frames in all, until nothing moves. The probe above saw no such message: its
canvas is not pixel perfect. The front end's canvas (`FrontEndManager`) is.

**Measured** with `PROBE_SCENARIO=childdims` (tools/UnityProbe, `ChildDimensions.cs`, lines
`CD …`; `PROBE_CD_PART=extra` for the second set). A rect P (200 × 100) holds C (50 × 50,
centre anchors), which holds GC; P also holds S (stretched). Each has a spy. One change to P
per case, made in a coroutine, in `Canvas.willRenderCanvases` or during the layout rebuild (an
`ICanvasElement` registered for it); on canvases at scale 1 and 0.86, pixel perfect or not;
with P an `Image`, C a `RawImage` and GC a `TextMeshProUGUI`, or with no graphics. Then P as a
child of a `HorizontalLayoutGroup`, resized or moved by it; then a copy of the news row.

- **Not pixel perfect:** no message beyond the size messages of rule 4, in any case.
- **Pixel perfect:** every active GameObject that has a `CanvasRenderer` and whose transform
  moved gets `OnRectTransformDimensionsChange` once, after the canvas update (after the
  `willRenderCanvases` handlers, before the end of the frame), whenever in the frame the move
  was made: before the update, in it, during the layout rebuild or in a `willRenderCanvases`
  handler. A move made after rendering (end of frame) is told in the next frame's.
  - A move is any change of the transform's place: `anchoredPosition`, `localPosition`,
    `position`, `localScale`, `localRotation`, a parent set (with or without keeping the world
    position), a sub-pixel move (0.3), and a move undone in the same frame. Two moves in a
    frame are told once. A drag is told every frame.
  - It reaches the moved rect and every descendant whose place moves with it: C and GC when
    P moves. A pivot change moves the children's rects but not P's transform: C and GC are
    told after the update, P only by the setter (rule 4).
  - A size change alone is not a move: P resized in place hears it from the setter only; C
    (centre anchors) does not move and is told nothing. P resized and moved by its group
    hears both: the setter's message during the rebuild and one after the update.
  - Only GameObjects with a `CanvasRenderer`: an `Image`, a `RawImage`, a text, a
    `CanvasRenderer` alone, a disabled `Image`, one with alpha 0 and a culled one all count. A
    rect without one (S, a `LayoutElement` alone) is told nothing. Inactive objects are told
    nothing.
  - Not a move: setting `anchoredPosition` to its value, `SetAsLastSibling`, activating an
    object, turning `pixelPerfect` on. An object made or cloned in the frame (a new object, an
    `Instantiate` into a parent) is not told of its placement.
  - The canvas that decides is the nearest one, as `Canvas.pixelPerfect` reads: a nested
    canvas that overrides `pixelPerfect` off is not told under a pixel-perfect root; one that
    overrides it on is told under a root that is not. A nested canvas that inherits follows its
    root.
  - Changing the root canvas's `scaleFactor` moves everything under it: P is told.
- **The news row:** a copy (row > tiles with a `VerticalLayoutGroup` > a header and a button
  whose preferred width follows its height) stays at the row's first layout (preferred width
  56) on a canvas that is not pixel perfect, at scale 1 and 0.86, with a `Mask` and a
  `RawImage` child with an `AspectRatioFitter` too. On a pixel-perfect canvas the buttons are
  told after the update, and the row reaches 908.5 in the next frames, as in the game.

**Where ours deviated.** Nothing was sent for a move on any canvas, so the news row kept its
first layout whenever nothing else re-laid it out (the intro path; the `menu` scenario happened
to get another layout). The re-send withdrawn on 2026-10-23 had covered it, but for every rect
on every canvas, sizes included.

### RectMask2D culling (measured 2026-10-23)

Found in a mod play-test: Modify Difficulty Settings (Workshop 2962220957). The label
`…/CustomGameSettingsChanger/Content/ScrollRectContainer/TogglePrefab#10/Label` ("Care Packages",
a `LocText`, GRAYSTROKE REGULAR SDF 14 pt, alignment `Left`) has its `CanvasRenderer.cull` true
in the original and false in ours. Its rect runs from world y 78.26 to 109.22. The
`RectMask2D` on `Content` has its rect's bottom at 94.26 and a `padding` of (0, 8, 0, 8), so
the clip's bottom is 94.26 + 8 × 1.032 = 102.52 (canvas scale 1.032). The label's mesh
(local y −7.94 to 6.34, pivot at world y 93.74) ends at 100.29, below the clip. The rect's top
reaches above it. Its sibling `Background` (an `Image`, 80.84 to 106.64) and that one's
`Checkmark` (84.26 to 103.23) cross the edge and are not culled in either player. The pixels
match: what is culled lies under the clip anyway.

**Measured** with `PROBE_SCENARIO=layoutcull` (lines `LC cull …`), at canvas scale 1 and 1.032.
Each rect is 200 × 30, placed with its centre at the mask's bottom (or top) edge plus d, for d
from −70 to 70 in steps of 0.5 (and ±0.01, ±0.1, ±0.25). The mask is a `RectMask2D` of 600 ×
200 at the canvas centre, also with a bottom padding of 10, a top padding of 10, or a softness
of 20. The kinds:
- `Image`s at pivots (0, 0.5), (0.5, 0.5) and (0.5, 1);
- `TextMeshProUGUI`s in NotoSans-Regular 14 pt, with extra padding and kerning: "Care
  Packages" aligned left, top left, bottom left and centre, at pivots (0, 0.5), (0.5, 1) and
  (0.5, 0); "", " " and "AAA"; and a 100-high rect, aligned left and top left.

- **The clip rect** is the mask's rect in the root canvas's space, shrunk by `padding` (x left, y
  bottom, z right, w top), intersected with the enclosing masks' rects, as already described.
  `softness` does not change it: with softness 20 the cull offsets are those without it.
- **The test is `Rect.Overlaps` with exclusive edges:** a graphic is culled when its cull rect
  does not overlap the clip rect. Touching edges count as not overlapping. An overlap of any
  size, however partial, keeps the graphic. Measured: an `Image` whose top lies exactly on the
  clip's bottom (d = −15) is culled; at d = −14.5 it is not. At the top edge, d = 15 is culled
  and 14.5 is not. With a bottom padding of 10, the edge moves up by 10 (d = −5 culled). At
  scale 1.032 the exact-touch case falls either way, through float rounding.
- **An `Image` (the `MaskableGraphic` default) culls with its rect:** the four corners of its
  `RectTransform` rect in the root canvas's space. The pivot makes no difference.
- **A `TextMeshProUGUI` culls with its mesh's bounds,** not with its rect and not with
  `textBounds`. The bounds are the axis-aligned box of all the mesh's vertices in its local
  space, mapped to the root canvas's space. Vertices TMP leaves unused (a space's quad, the
  slack of the vertex buffer: "Care Packages" has 52 vertices, "AAA" 16) sit at the local
  origin. So the box always includes the pivot point. An empty text, or one of white space
  alone, is a point at the pivot (4 vertices).
  - Measured for every text kind: the offset at which it is culled is the one where that box
    stops overlapping, to the 0.5 step. "Care Packages" centred vertically has a box from
    −12.29 to 8.71 about the pivot and is culled from d = −9 at the bottom edge, where its rect
    would be from d = −15. Aligned to the top of a 100-high rect, its glyphs run from 28.2 to
    49.2 above the pivot, but the box starts at 0. So it is culled at the top edge from d = 0,
    not from d = −28.
  - In the game, the box explains the label above: it ends 2.2 below the padded clip.
- **When the cull is evaluated:** in the clipping step of the canvas update (after the layout
  rebuild, before the graphic rebuild), for each graphic under the mask, when:
  - the clip rect changed (the mask moved, measured: culled in that frame);
  - the graphic moved, through its own `anchoredPosition` or `localPosition` or a parent's;
  - its scale or its rect's size changed (an `Image` grown into the clip is shown in that
    frame, one shrunk out of it is culled in that frame);
  - a text's mesh changed. After a text or font-size change, the cull follows the new mesh in
    the same frame's update. After an alignment change, it follows one frame later. A text set
    to "" keeps its cull. Nothing else re-evaluates it: a culled text whose mesh stays put stays
    culled.
  - A text made in place is culled with its first mesh from its first canvas update. That
    mesh comes from the same update, not from the empty mesh before it: a text whose pivot
    lies 0.5 below the clip, with glyphs reaching into it, is not culled.

**Where ours deviates.**
- `TextMeshProUGUI` has no `Cull` of its own, so it culls with its rect
  (`MaskableGraphic.Cull`, `RootCanvasRect`). Every text kind is culled at the `Image`'s offsets
  (d = −15 at the bottom, d = 15 at the top; −50 and 50 for the 100-high rect). Give
  `TMP_Text`/`TextMeshProUGUI` a `Cull(clipRect, validRect)` that tests the bounds of the mesh
  its canvas renderer was given. Our TMP builds that mesh as the original does: the probe's
  `meshLocal` boxes match the original's, unused vertices at the origin included. Map the
  corners of the box with the text's transform into the root canvas's space, as
  `RootCanvasRect` maps the rect's. Then re-evaluate it when the mesh changes, as above.
- `RectMask2D.PerformClipping` (`src/UnityEngine.UI/Core/Mask.cs`) re-culls a graphic only when
  `canvasRenderer.hasMoved`, which follows the transform, not the rect. An `Image` resized
  across the edge keeps its old cull (`time img size in` / `size out`). A size change must
  count as a move here.
- Two values the probe logged on the way: our `TMP_Text.bounds` is the glyphs' box, but the
  original's equals the mesh's bounds (pivot included). Our empty text has 0 vertices, where
  the original has 4. The original's `textBounds` of an empty text is the sentinel box
  (±2147483648); ours is a point at the pivot. Neither is used by the cull above.

| Case (`cull …`) | original: culled from | ours before |
|---|---|---|
| `Image`, any pivot, bottom edge | d ≤ −15 (top on the edge) | the same |
| `Image`, top edge | d ≥ 15 | the same |
| `Image`, bottom padding 10 | d ≤ −5 | the same |
| `Image`, softness 20 | d ≤ −15 | the same |
| "Care Packages", left (box −12.29..8.71) | d ≤ −9 | d ≤ −15 |
| the same, top left (box −6.80..14.20) | d ≤ −14.5 | d ≤ −15 |
| the same, bottom left (box −17.77..3.23) | d ≤ −3.5 | d ≤ −15 |
| the same, top edge | d ≥ 12.5 | d ≥ 15 |
| "AAA" (box −8.93..8.09) | d ≤ −8.5 | d ≤ −15 |
| "" and " " (a point at the pivot) | d ≤ 0 (bottom), d ≥ 0 (top) | d ≤ −15, d ≥ 15 |
| 100-high rect, top left, top edge (glyphs 28.2..49.2, box 0..49.2) | d ≥ 0 | d ≥ 50 |
| "Care Packages", bottom padding 10 | d ≤ 1 | d ≤ −5 |
| `Image` grown across the edge (`sizeDelta.y` +100) | shown in that frame | stays culled |
| text whose box is out, rect in, nothing changes | culled | not culled |
| text whose box is out after an alignment change | culled one frame later | not culled |
| Modify Difficulty Settings, "Care Packages" label | culled; shown when scrolled 100 up, culled again back | not culled |

**An exception: TextMeshPro's caret graphic** (measured 2026-10-24). `TMP_SelectionCaret`, the
graphic a `TMP_InputField` draws its caret and selection with, is registered with the masks
and clipped like any `MaskableGraphic`, but its `Cull` ignores its rect and mesh: it is culled
only when the clip rect is invalid. `textmeshpro.md` ("The caret graphic and RectMask2D")
records the measurement. The same probe found that the original re-culls an `Image` a frame
after a direct `Cull` call un-culled it, with nothing moved.

### Scrollbar presses on the track (measured 2026-10-23)

Found through the vanilla Consumables screen (`tests/e2e/consumables-scroll.yaml`). Its header
row has a horizontal `Scrollbar` (`…/prefab_row_header/Border/HorizontalScrollbar`, 760 wide,
`size` 0.4496, `value` 0) driving the header's `KScrollRect`. A click on the track right of the
handle, at the track's centre, moved the value to 0.49994 in the original: the handle centred
on the click. Ours moved it by one `size`, to 0.4496, and kept stepping while the button was
held. (The rows of that table did not follow the header in ours either; that is the layout
re-send described under "Moved children of layout groups", not this.)

**Measured** with `PROBE_SCENARIO=scrollbar` (tools/UnityProbe, `ScrollbarPress.cs`, lines `SB
…`). Each case builds a fresh `Scrollbar` on an overlay canvas of scale 1: the track an `Image`
of 400 × 20 (20 × 400 vertical) at the screen centre, a "Sliding Area" child stretched over it,
and a "Handle" child `Image` in that as `handleRect`. The case sets `direction`, `size`,
`value` and `numberOfSteps`, waits two frames, then drives the game's `StandaloneInputModule`
through `inputOverride`: a press at a fraction p of the track's length (0 at the left or bottom
end), held for 1 to 60 frames (at about 250 frames a second, so up to about 0.25 s), sometimes
moving while held, then a release. Every frame logs `value` and the handle's anchors; every
`onValueChanged` logs its argument.

Write the track position of the press along the scrollbar's direction as q: q = p for
LeftToRight and BottomToTop, q = 1 − p for RightToLeft and TopToBottom.

1. **A press on the handle** changes nothing. Pressing it and moving drags it, keeping the
   offset at which it was grabbed: `size` 0.45, `value` 0, pressed at p = 0.1 and moved to
   p = 0.5 gives 0.72727 (the handle's start moved by 0.4 of the track; 0.4 / 0.55).
2. **A press on the track outside the handle** sets, in the frame of the press, the value that
   puts the handle's centre on the press point: `value` = clamp01((q − `size` / 2) / (1 −
   `size`)), then `numberOfSteps` rounding as for any value set. `onValueChanged` is invoked
   once, with that value (not when the value is unchanged).
3. **While the button is held,** the handle follows the pointer as a drag from its centre: the
   value is again (q − `size` / 2) / (1 − `size`) for the pointer's current q, clamped, each
   frame the pointer moves. This holds below the event system's drag threshold (20 px):
   a move of 10 px gave the followed value. A pointer that stays still changes nothing: there
   is no stepping repeat, however long the press is held (the press point is re-applied,
   rule 4, which leaves the value as it is). The release changes nothing.

| Case | original | ours before |
|---|---|---|
| size 0.4496, value 0, p 0.5, held 2 frames | 0.5 at the press | 0.4496 |
| size 0.45, value 0, p 0.5, held 1 or 30 frames | 0.5 | 0.45 |
| size 0.45, value 0, p 0.6 | 0.68182 | 0.45 |
| size 0.45, value 0, p 0.75 | 0.95455 | 0.45, then 0.9 on the 2nd held frame |
| size 0.45, value 0, p 0.9 or 0.99, held 2 or 60 | 1 | 0.45, 0.9, then 1 |
| size 0.45, value 0, p 0.46 (just right of the handle) | 0.42727 | 0.45 |
| size 0.45, value 0, p 0.2 (on the handle) | 0, nothing invoked | the same |
| size 0.2, value 0, p 0.3 | 0.25 | 0.2 |
| size 0.2, value 0, p 0.9, held 60 | 1 | 0.2, 0.4, 0.6, 0.8, 1 a frame apart |
| size 0.2, value 0.5, p 0.1 | 0 | 0.3, then 0.1 |
| size 0.2, value 0.5, p 0.9 | 1 | 0.7, then 0.9 |
| size 0.2, value 1, p 0.1, held 60 | 0 | 0.8 down to 0 |
| size 0.1, value 0, p 0.55, held 60 | 0.55556 | 0.1 up to 0.5 |
| size 0.45, value 0, steps 5, p 0.75 | 1 (0.95455 rounded) | 0.5, then 1 |
| size 0.2, value 0, steps 3, p 0.9, held 60 | 1 | 0, nothing invoked |
| RightToLeft, size 0.45, value 0, p 0.25 (q 0.75) | 0.95455 | 0.45, then 0.9 |
| BottomToTop, size 0.3, value 0, p 0.8 | 0.92857 | 0.3, 0.6, then 0.9 |
| TopToBottom, size 0.3, value 0, p 0.8 (q 0.2, on the handle) | 0, nothing invoked | the same |
| size 0.2, value 0, p 0.9, then moved to p 0.3 while held | 1, then 0.25 | 0.2, then 0.25 |
| size 0.2, value 0, p 0.9, then moved to p 2 (off the track) while held | 1, stays 1 | 0.2, then 1 |
| size 0.2, value 0, p 0.5, then moved 10 px (p 0.525) | 0.5, then 0.53125 | 0.2, then 0.53125 |
| size 0.2, value 0, p 0.5, then moved 30 px (p 0.575) | 0.5, then 0.59375 | 0.2, then 0.59375 |

What follows a value change (the handle's anchors, `onValueChanged`, a linked `ScrollRect`
setting its normalized position) is as described above; only the value a press on the track
produces, and the repeat, differ. Not measured: touch input, a press with a button other than
the left, and presses on a scrollbar whose `interactable` is false.

**The press while held, and the protected `ClickRepeat` and `Update` (measured 2026-10-23,**
`PROBE_SB_PART=clickrepeat` and `clickrepeat2`, a `Scrollbar` subclass of size 0.2 calling
both `ClickRepeat` overloads by hand, one `MoveNext` a frame, and through `StartCoroutine`).
The press of rule 2 is not applied once only:

4. **A press holds its point.** From a press (on the handle or not) until the release or the
   start of a drag, the press point is re-applied immediately and then at the end of every
   frame: when it lies outside the handle, the value becomes the one rule 2 gives for it. With
   nothing else moving the value this changes nothing (rule 3's still pointer). A value set
   while the button is held is put back at the end of that frame when the handle no longer
   covers the press point: pressed on the track at 0.75 (0.8125), then set to 0.2 from code,
   the value is 0.8125 again by the next frame, with `onValueChanged` for 0.2 and then
   0.8125. Pressed on the handle (value 0.5, at 0.5), a value of 0.45 stays (the handle still
   covers 0.5); a value of 0 is put back to 0.5.
5. **`ClickRepeat(Vector2 screenPosition, Camera camera)`** returns an enumerator. While a
   press of rule 4 holds, each `MoveNext` applies `screenPosition` as rule 4 applies the press
   point (outside the handle: the value of rule 2 for it, `onValueChanged` when it changes;
   on the handle: nothing), and returns true with `Current` a `WaitForEndOfFrame`. Once the
   press has ended (released, or a drag begun), `MoveNext` returns false and changes nothing.
   On a scrollbar that has never been pressed, the first `MoveNext` throws a
   `NullReferenceException` ("routine is null") and changes nothing, wherever the point is.
   Both repeats run at the end of each frame, the press's first: while the button was held
   still on the handle (value 0.5) a subclass's `ClickRepeat` at 0.9 gave `onValueChanged` 1,
   then 0.5 at the end of the frame, then 1 again from the subclass's next step; its value
   was 1 when read in the next frame's `Update`. Under `StartCoroutine` the first step runs
   inside the call.
6. **`ClickRepeat(PointerEventData eventData)`** is the same with the point
   `eventData.pointerPressRaycast.screenPosition`; `position` and `pressPosition` are not
   read. (The camera was not measured; every measurement used an overlay canvas.)
   The handle's anchors along the axis start at v − v × `size` (v the clamped value; float
   arithmetic) and end `size` further: in all 945 left-to-right states these probes logged,
   v × (1 − `size`) differed in the last bit 79 times (v 0.8125, size 0.2: 0.65 to
   0.849999964, not 0.65000004 to 0.85).
7. **`Update`** (protected, virtual) has no measured effect: a subclass whose override does not
   call `base.Update()` gave the same values, anchors and `onValueChanged` for a value and a
   size set from code and for a press on the track held and released.

### Measured on the shipped package

Found by the M9 differential runs; they refine the sections above.
- **Sliced images:** borders are scaled to the pixel-snapped rect first, then shrunk together
  where they do not fit; each line is offset from the rect's start. Quads are emitted column
  by column, and empty quads are left out. Trimmed sprite padding is left out of the quad.
- **Radial fills** collapse the corners past the cut.
- **Rebuild queues:** outside a rebuild pass, an element leaving a queue is replaced by the
  queue's last element (this decides the order a dynamic font packs glyphs in).
- **Dropdown:** the blocker carries a `CanvasGroup`.
- **Raycast padding:** positive padding shrinks the hit area (left, bottom, right, top).

## Verification

1. **Build:** `scripts/build-packages.sh` builds the package and runs ApiCheck: nothing missing.
2. **Unit tests**, in `tests/OniBrutal.UnityEngine.Tests`, with a new reference to the package
   project:
   - the canvas update order;
   - each Image type's vertices;
   - layout groups for alignment, control, force-expand and padding;
   - the fitters;
   - `ExecuteEvents`;
   - the pointer pipeline, driven through `BaseInput` with a test input;
   - the selectable state;
   - the toggle groups;
   - the slider and scrollbar values;
   - ScrollRect dragging and clamping;
   - the raycast order.

   Where the documentation leaves a number open, measure it against the shipped package
   through a scenario run, not the shipped code.
3. **Differential runs** (`scripts/m9-compare.sh`), loading this uGUI with the shipped
   TextMeshPro:
   - `menu` and `colony` with no differences beyond the tolerance;
   - new scenarios as you need them, for example a new game, the research screen, the
     management screens, a dropdown and the scroll views. Add them to `scripts/m9-scenarios/`.

   Explain every remaining difference.
4. **Performance:** DeCrew's frame rate is not worse than with the shipped package
   (`scripts/m9-compare.sh colony`; compare the frame-stats lines).
