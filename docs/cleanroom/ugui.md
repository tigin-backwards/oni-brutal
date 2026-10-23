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
  batching (`Engine/UI/CanvasSystem.cs`), not this package's: with the shipped uGUI on our
  engine the numbers are ours, not the original's.
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
  - **Culling:** elements wholly outside the clip are culled.
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
  called during a rebuild.

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
  `deselectHandler`, `moveHandler`, `submitHandler` and `cancelHandler`.
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
- **`HandlePointerExitAndEnter(eventData, newEnter)`:**
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
- **Input:** dragging the handle moves the value. Pressing the track outside the handle moves
  towards the press by one `size` per step, repeating while held, through a coroutine.

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
