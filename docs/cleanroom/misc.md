# Smaller regions: functional specification

A provenance audit of the engine found these regions written too closely after Unity's own
managed code. That code is either Unity's published reference source or a decompilation. The
signs were Unity's private names, its local variable names, its typos and its statement
order. Each region is rewritten from the behaviour below.

The public API and the internal members other assemblies use stay as they are. To find which
internal members uGUI uses, run `tools/ApiCheck` (`scripts/check.sh`), which reports missing
members after a change. Private structure, private names and statement order are yours.

All paths are under `src/OniMoono.UnityEngine/`.

## 1. UI Toolkit events and runtime (`UIElements/EventBase.cs`, `PointerEvents.cs`, `OtherEvents.cs`, `Runtime.cs`, `Panels.cs`)

`UIElements/README-scope.cs` explains why these types exist. It describes this project's
design, not Unity's code, and stays.

In short:
- uGUI carries a bridge to UI Toolkit, which must load and bind.
- The game has no UI Toolkit content, so no panel ever exists and no event is ever dispatched.
- The events are data holders with pooling.

Keep the scope that file describes. Rewrite the five files' bodies and private structure.

**Events**, as Unity's documentation describes them:
- **Type ids:** `EventBase<T>.TypeId()` and `eventTypeId` give each event type a distinct id.
  The ids are assigned in order of first use, starting at 1. A base `EventBase` reports -1.
- **Event ids:** every event gets a fresh internal event id each time it is initialised. These
  ids come from one increasing counter.
- **`GetPooled()`:**
  - It returns an event of type `T`, taken from a per-type pool or created when the pool is
    empty.
  - The event is reset to its initial state, marked as pooled, and holds one reference.
  - The pool keeps at most 100 events; any release beyond that drops the event.
  - `SetCreateFunction` replaces the pool's factory.
- **The internal `GetPooled(EventBase e)`** also records `e`'s event id as the trigger event id.
- **`Dispose()`:**
  - It drops one reference.
  - When none are left and the event is pooled, the event is reset and returned to its pool.
  - An event created with `new` is not pooled, so it is never put in a pool.
  - If an event is reset while it still holds references, the engine logs "Event improperly
    released." and the count goes back to 0.
- **Initial state:**
  - timestamp 0, trigger id 0, no propagation flags, no target, propagation not stopped;
  - target is always null, because only VisualElements can be targets and none exist.
- **Propagation:**
  - `StopPropagation` sets `isPropagationStopped`.
  - `StopImmediatePropagation` sets both flags.
  - `bubbles` and `tricklesDown` reflect the propagation flags.
  - The internal flag `skipDisabledElements` is a separate bit.
- **Pointer events** (`PointerEventBase<T>`, with `PointerDownEvent`, `PointerUpEvent`,
  `PointerMoveEvent`, `PointerCancelEvent`):
  - **Properties:** as documented.
  - **Initial state:** button -1, pointer type unknown (""), everything else zero or false, and
    propagation bubbles and trickles down.
  - **`GetPooled(IPointerEvent trigger)`** copies every pointer property of the trigger. A null
    trigger copies nothing.
  - **The internal `GetPooled(trigger, position, pointerId)`** copies the trigger when there is
    one. Otherwise it sets `position` and `localPosition` to the position, and sets the pointer
    id, with the type derived from that id.
  - **Angles:** `altitudeAngle`, `azimuthAngle` and `tilt` relate as in Unity's documentation
    for pen input. The formulas below are standard spherical trigonometry.
    - A value set directly is returned as set.
    - Altitude and azimuth not set since the last tilt change are derived from tilt on first
      read:
      - altitude = π/2 − acos(cos tx · cos ty);
      - azimuth = 0 when tx = 0;
      - otherwise azimuth = π/2 − atan2(−cos tx · sin ty, cos ty · sin tx), wrapped into
        [0, 2π) and then rotated by −π/2 (mod 2π).
    - On platforms other than Windows, a touch pointer's tilt is derived once from altitude a
      and azimuth z:
      - tx = atan(cos z · cos a / sin z);
      - ty = atan(cos z · sin a / sin z).
  - **Modifier helpers:** `shiftKey`, `ctrlKey`, `commandKey`, `altKey`. `actionKey` is Command
    on macOS and Control elsewhere.
- **Navigation, keyboard, focus, mouse and wheel events:** the same model, with properties as
  documented.
  - `NavigationMoveEvent.GetPooled(Vector2 move)` sets `move` and derives `direction`:
    - None when the vector is shorter than the dead zone 0.6;
    - otherwise Left or Right when |x| > |y|, and Up or Down when not.
  - `GetPooled(Direction)` sets the direction, with a zero move.
  - Keyboard events carry character, key code and modifiers.
  - The internal `ShouldSendNavigationMoveEvent…` helpers:
    - For a `KeyDownEvent`, the answer is true for Tab without Control, Alt, Command or the
      function-key flag; Shift is allowed. Arrow keys answer false.
    - For an IMGUI `Event` (the runtime variant), the answer is true for any Tab `KeyDown`,
      whatever the modifiers.
- **`PointerId` and `PointerType` constants** are public API. Keep their values:
  - `maxPointers` 32, `mousePointerId` 0, `invalidPointerId` -1;
  - touch ids start at 1 (20 of them), pen ids follow (2), tracked ids follow (8);
  - type names: "mouse", "touch", "pen", "tracked", "".

**Runtime state:**
- **The uGUI EventSystem registry:** `RegisterEventSystem` makes the given object the active
  one. If a different `EventSystem` was already active, it logs the warning "There can be only
  one active Event System." `UnregisterEventSystem` clears it if it was the active one.
- **`GetSortedPlayerPanels()`** returns an empty list, and the maximum resolved sorting index
  is -1.
- **`PointerDeviceState`:**
  - **Positions:** a pointer's last position and its panel, plus the delta from that position
    (zero when the last panel is null).
  - **Buttons:** a bit mask of pressed buttons per pointer, with press, release, release all,
    and "other buttons pressed".
  - **Soft capture:** the runtime panel and camera of the element pressed, per pointer.
  - **Panel removal:** removing a panel forgets the positions and captures that refer to it.
- **Panels** (`Panels.cs`) have no reachable constructor. Keep their members' observable
  results the same:
  - `Pick` returns null;
  - `PickAll` returns null and adds nothing;
  - the dispatch state reports no capture.
- **Serialized fields:** these names are data.
  - `PanelInputConfiguration` holds `m_Settings`.
  - `Settings` has, in this order: `m_ProcessWorldSpaceInput` (bool), `m_InteractionLayers`
    (LayerMask), `m_MaxInteractionDistance` (float), `m_DefaultEventCameraIsMainCamera`
    (bool), `m_EventCameras` (Camera[]), `m_PanelInputRedirection` (PanelInputRedirection),
    `m_AutoCreatePanelComponents` (bool).
  - **`Settings.Default`:**
    - world-space input on;
    - interaction layers -5, which is every layer but Ignore Raycast (layer 2);
    - infinite distance;
    - the main camera as the event camera, with an empty camera array;
    - AutoSwitch;
    - auto-create on.
  - Each field has a read-only property in camelCase.
- **`ClickDetector.s_DoubleClickTime`** (internal, used by uGUI) is -1, meaning "not read yet":
  a panel's click detector would read the system setting, and none exists.

## 2. TextGenerator's caching front (`TextRendering/TextGenerator.cs`, the `TextGenerator` class up to `Generate`)

The layout engine, from `Generate` on, is this project's own. It stays, apart from moving its
inputs.

Behaviour of `TextGenerator`, as documented:
- **Caching:** `Populate(str, settings)` regenerates only when the string or the settings
  differ from the last generation, or after `Invalidate()`. Otherwise it returns the previous
  result. It returns true on success.
- **`PopulateWithErrors(str, settings, context)`** does the same, and on failure logs an error
  against `context`. The two errors it knows are a size or a style override on a non-dynamic
  font:
  - "Font '{font}' is not dynamic, which is required to override its size";
  - "Font '{font}' is not dynamic, which is required to override its style".

  Generation drops those overrides first (below), so these errors do not occur in practice.
  Generating with a non-dynamic font and overrides succeeds, with the warnings only, and
  `PopulateWithErrors` returns true. Only a missing font fails.
- **Non-dynamic fonts:**
  - A font size or style override is dropped (size 0, style Normal), with the warning
    "Font size and style overrides are only supported for dynamic fonts. Font '{name}' is not
    dynamic." logged against the font.
  - Best fit is turned off, with "BestFit is only supported for dynamic fonts. Font '{name}' is
    not dynamic.".
  - A null font fails generation, which has nothing to output.
- **`GetPreferredWidth(str, settings)`** generates with horizontal and vertical overflow and
  bound updates on, and returns the extents' width.
- **`GetPreferredHeight`** turns only vertical overflow on, and returns the height.
- **Read-only accessors** to the output: `verts`, `characters`, `lines`, the counts,
  `characterCountVisible` (`characterCount` − 1), `rectExtents`, `fontSizeUsedForBestFit`, the
  `Get…` copy methods and the `…Array` methods.
- **Constructors:** the parameterless constructor and `TextGenerator(int initialCapacity)`
  pre-size the output lists. The sizes are yours. `Dispose` does nothing.

## 3. Object pools (`Core/Collections.cs`, `UnityEngine.Pool`: `ObjectPool<T>`, `PooledObject<T>`, `CollectionPool<,>`)

As documented. `ListPool`, `HashSetPool` and the others build on `CollectionPool`.

**`ObjectPool<T>(createFunc, actionOnGet, actionOnRelease, actionOnDestroy, collectionCheck = true, defaultCapacity = 10, maxSize = 10000)`:**
- **Construction:**
  - A null `createFunc` throws `ArgumentNullException`.
  - `maxSize <= 0` throws `ArgumentException` with "Max Size must be greater than 0".
- **`Get()`:**
  - It returns the most recently released inactive object, or creates one, which counts
    towards `CountAll`.
  - It then calls `actionOnGet`.
- **`Release(x)`:**
  - With `collectionCheck`, releasing an object that is already inactive in the pool throws
    `InvalidOperationException`: "Trying to release an object that has already been released to
    the pool.".
  - It then calls `actionOnRelease`.
  - When fewer than `maxSize` objects are inactive, the object is kept. Otherwise
    `actionOnDestroy` is called on it and `CountAll` drops by one.
- **Counts:** `CountInactive` is the number of kept objects; `CountActive` is `CountAll` minus
  that.
- **`Clear()`** calls `actionOnDestroy` on every inactive object, empties the pool and sets
  `CountAll` to 0. `Dispose()` is `Clear()`.
- **`Get(out T v)`** returns a `PooledObject<T>` whose disposal releases `v` back to the pool.

**`CollectionPool<TCollection, TItem>`:** a static pool per type, whose collections are
cleared on release.

`NativeArray<T>` in the same file is low concern and stays.

## 4. Colour conversion (`Math/Color.cs`: `RGBToHSV`, `HSVToRGB`)

**`RGBToHSV(rgb, out H, out S, out V)`:**
- V is the largest component, and H and S are as in the standard hexcone model.
- The dominant component decides H's sector:
  - blue, when it exceeds both others;
  - else green, when it exceeds red;
  - else red.
- **Values:**
  - With d = V − min: S = d / V.
  - H = (offset + (a − b) / d) / 6, where (offset, a, b) is (0, g, b) for red, (2, b, r) for
    green and (4, r, g) for blue; 1 is added when H is negative.
  - When d = 0, S = 0 and H = (offset + (a − b)) / 6, adjusted the same way.
  - When V = 0, H and S are 0.

**`HSVToRGB(H, S, V[, hdr = true])`:**
- S = 0 gives grey V.
- Otherwise, V = 0 gives black.
- Otherwise, with h = 6H, the sector is i = floor(h), f = h − i, p = V(1 − S), q = V(1 − Sf),
  t = V(1 − S(1 − f)):

  | Sector | Result |
  |---|---|
  | 0 and 6 | (V, t, p) |
  | 1 | (q, V, p) |
  | 2 | (p, V, t) |
  | 3 | (p, q, V) |
  | 4 | (t, p, V) |
  | 5 and −1 | (V, p, q) |
  | any other sector | white |
- Without `hdr`, components are clamped to [0, 1].
- Alpha is 1.

## 5. Smooth damping (`Math/Mathf.cs` `SmoothDamp`, the `Vector2` and `Vector3` `SmoothDamp` overloads)

**The spring:** a critically damped spring, after Game Programming Gems 4, §1.10 (published).
- The smooth time is at least 0.0001; ω = 2 / smoothTime and x = ω·dt.
- The decay is k = 1 / (1 + x + 0.48x² + 0.235x³).
- The change is c = current − target. Its length is limited to maxSpeed·smoothTime: a clamp
  for floats, and the vector's length for vectors.
- The working target is current − c.
- With u = (velocity + ω·c)·dt, the new velocity is (velocity − ω·u)·k, and the result is
  working target + (c + u)·k.

**Overshoot:** if the result passes the original target (for floats, when the target lies above
current exactly when the result lies above the target; for vectors, when the dot product of
(target − current) and (result − target) is positive), the result is the original target and
the velocity is 0.

**Defaults:** the overloads without `deltaTime` use `Time.deltaTime`, and those without
`maxSpeed` use infinity.

## 6. Small routines

- **`Core/Behaviours.cs`, `WaitForSecondsRealtime`:**
  - `waitTime` is settable.
  - On the first `keepWaiting` read after construction or `Reset()`, the deadline becomes
    `Time.realtimeSinceStartup + waitTime`.
  - `keepWaiting` is true while real time is before the deadline. When it turns false, the
    instruction resets itself so it can be yielded again.
  - Also check `WaitUntil` and `WaitWhile` (predicate wrappers) for Unity's private names, and
    rename them.
  - The deadline is shared by every coroutine that yields the same instance. The game caches
    instances and yields one from several coroutines at once, and they then share one deadline.
    The deadline is armed by the first query and cleared once it has passed.
- **`UI/RectTransform.cs`, `SetInsetAndSizeFromParentEdge(edge, inset, size)`:**
  - Both anchors are set to the edge on that edge's axis: 0 for Left and Bottom, 1 for Right
    and Top.
  - The size along that axis is set to `size`.
  - The anchored position on that axis is chosen so the rect's near side is `inset` from the
    edge, taking the pivot into account:
    - for Left and Bottom: inset + size × pivot;
    - for Right and Top: −inset − size × (1 − pivot).
  - The other axis is untouched.
- **`Core/Object.cs`, `CompareBaseObjects`:** `UnityEngine.Object` equality, as documented:
  - Two references are equal when they are the same object.
  - A destroyed object, or one with no native object, equals null.
  - Two destroyed objects are equal to each other only as null.

  Rename the helper. Behaviour must not change; the existing tests cover it.
- **`Math/Quaternion.cs`:**
  - `==` is true when the dot product exceeds 1 − `kEpsilon`.
  - `eulerAngles` wraps each angle with a tolerance t = 0.0001 rad, in degrees:
    - an angle below −t gets 360° added;
    - an angle above 360° − t gets 360° subtracted, so an angle just below 360° comes out as a
      small negative angle;
    - any other angle stays as it is.

  Restructure the private helpers that do this (`IsEqualUsingDot`, `MakePositive`) under your
  own names.
- **Renames only** of Unity-named private members:
  - `Core/Attributes.cs` (around lines 32, 85–86, 121);
  - `Playables/Animation/AnimationPlayables.cs` (`m_NullPlayable`), and the same names in
    `Playable.cs`, `ScriptPlayable.cs` and `PlayableOutput.cs` (`m_NullPlayable`,
    `m_NullPlayableOutput`);
  - `TextCore/GlyphRect.cs` (`s_ZeroGlyphRect`; its `[SerializeField]` names are data and stay).

## Verification

- `scripts/check.sh`:
  - the build succeeds;
  - both test suites have 0 failures;
  - ApiCheck shows no more than 13 missing members;
  - the host checks report no failures.
- `dotnet test tests/OniMoono.UnityEngine.Tests` passes.
- New tests, where there are none yet:
  - pools: counts, the release check, the max size;
  - HSV round trips and edge cases: grey, black, H = 1, negative H;
  - `SmoothDamp`: convergence and no overshoot;
  - `WaitForSecondsRealtime` reuse;
  - `SetInsetAndSizeFromParentEdge` for each edge and pivot;
  - event pooling: reuse, the reference count, the 100 cap;
  - navigation direction;
  - pointer angle derivation.

## Note on TextGenerator

The whole `TextGenerator` class is removed in the rewrite branch, so the generation step must be
rewritten as well.

- Generation clears the outputs and fails without a font.
- Layout is done with the project's `OniMoono.Engine.Text.TextLayout`, in the same file. Its
  interface:
  - a constructor taking the font's native object, the string, the settings, the scale factor
    (`scaleFactor`, or 1 when it is not positive) and the tab size;
  - `Run(size)` lays the text out at a size;
  - `Fits` says whether the last run fitted;
  - `Emit(verts, characters, lines, out extents)` writes the output, and returns true when the
    font's texture was rebuilt. In that case, call `Font.InvokeTextureRebuilt_Internal(font)`.
- **Size:**
  - The base size is `settings.fontSize` for a dynamic font with a size set, and otherwise the
    font's own size.
  - With best fit on a dynamic font, the size is the largest integer in
    [`resizeTextMinSize`, max(`resizeTextMaxSize`, min)] at which `Fits`; it is the minimum
    when none fits.
  - `fontSizeUsedForBestFit` reports the size used.
- **`TabSize`** (internal, default 4, set by `TextMesh`) is the tab stop in multiples of the
  space advance.
