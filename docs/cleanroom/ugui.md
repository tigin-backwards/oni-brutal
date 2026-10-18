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
- `UnRegisterCanvasElementFor…` and `DisableCanvasElementForRebuild` remove an element.
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
- `depth`: `canvasRenderer.absoluteDepth`.
- `defaultMaterial`, `mainTexture`, `rectTransform`, `canvasRenderer`.

**Methods:**
- `GetPixelAdjustedRect()`: the rect snapped to pixels when the canvas is pixel-perfect,
  through `RectTransformUtility.PixelAdjustRect`; otherwise the rect.
- `PixelAdjustPoint`.
- `Raycast(Vector2 sp, Camera eventCamera)`: walks up the hierarchy, from this object to the
  root. Every enabled `ICanvasRaycastFilter` component found on the way must accept the point.
  A `CanvasGroup` with `ignoreParentGroups` stops the walk above it, after itself, and a
  `CanvasGroup` that does not block raycasts fails it.
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
    target, has a non-zero depth (has been drawn) and is not culled, the pointer must be
    inside the graphic's rect, including `raycastPadding`, using the canvas's event camera
    for non-overlay canvases. The graphic's `Raycast` must pass.
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
  - Pressed while the pointer that pressed it is down and inside.
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
