# IMGUI: functional specification

Unity's immediate-mode GUI: `MonoBehaviour.OnGUI`, `Event`, `GUI`, `GUILayout`,
`GUILayoutUtility`, `GUIUtility`, `GUIContent`, `GUIStyle`, `GUIStyleState`, `GUISkin`,
`GUISettings`, `TextEditor`, and the internal `GUIClip`. The code lives in
`src/OniMoono.UnityEngine/IMGUI/`, with engine-side services in the namespace
`OniMoono.Engine.IMGUI`.

Where the game uses it:
- **Debug tools** in `Assembly-CSharp-firstpass`.
- **The FMOD debug overlay** in `FMODUnity`.
- **Every text input**, through `Event.PopEvent`: uGUI's and TextMeshPro's input fields read
  their keystrokes from the event queue.
- **The clipboard**, through `TextEditor` (`Assembly-CSharp` copies text with it).

Breaking the event queue breaks typing everywhere, so it matters most. The rest needs to behave
the way Unity's documentation describes.

## Interface

### Public API

The public API is what Unity's Scripting API documents for the types above. The enums and empty
type shells are generated in `Generated/UnityEngine.IMGUIModule/UnityEngine.cs`:
- `EventType` (including its lower-case aliases), `EventModifiers`, `FocusType`, `ScaleMode`,
  `ImagePosition`, `TextClipping`;
- `GUI.WindowFunction`.

Members that are required: whatever `tools/ApiCheck` reports missing (`scripts/check.sh` runs
it). It covers the game's assemblies, FMODUnity, uGUI and TextMeshPro. uGUI and TextMeshPro may
also use members the engine keeps internal.

### Engine entry points

Other engine code calls these, so they must keep working. Their names may change if you update
the callers:
- `OniMoono.Engine.IMGUI.GUIManager.Install()`: the host calls it once at startup
  (`src/OniMoono.Host/Player.cs`).
- `GUIManager.Register(BehaviourNative)` and `Unregister(BehaviourNative)`: called when a
  behaviour becomes enabled or disabled (`Engine/BehaviourManager.cs`).
  - A behaviour takes part in IMGUI when its script declares an `OnGUI` method, of any
    visibility.
  - `BehaviourNative.Script.OnGUI` calls the method.
- `UnityEngine.Event.Enqueue(Event)` (internal): the platform layer queues one event per input
  occurrence (`src/OniMoono.Desktop/GlfwPlatform.cs`, `InputStateFeed`).
  - A key press arrives as a `KeyDown` with its key code, followed by a second `KeyDown` with
    `KeyCode.None` and the typed character.
  - Mouse positions are in GUI coordinates: origin at the top left, y downwards.
- **Per-frame hook:** the current code runs the frame's events and the repaint from a hook that
  `Install` registers (read `Engine/PlayerLoop.cs` and the frame-renderer hooks). IMGUI draws
  after the frame's cameras and canvases.
- **`Engine/GUIRenderer.cs` stays:** it is the drawing backend.
  - It draws textured quads and legacy-font text through the engine's immediate-mode device
    path, with the shaders `Hidden/Internal-GUITextureClip` (quads) and
    `Hidden/Internal-GUITextureClipText` (text).
  - It works in pixel space, origin top left.
  - You may change its API.

### Data formats

The built-in skin and any `GUISkin` or `GUIStyle` in game data are deserialized by the engine:
- **`GUISkin` and `GUISettings`** are ordinary serialized objects. These field names are fixed by
  the data:
  - `GUISkin`:
    - `m_Font`;
    - one `GUIStyle` field per standard style: `m_box`, `m_button`, `m_toggle`, `m_label`,
      `m_textField`, `m_textArea`, `m_window`, `m_horizontalSlider`, `m_horizontalSliderThumb`,
      `m_verticalSlider`, `m_verticalSliderThumb`, `m_horizontalScrollbar`,
      `m_horizontalScrollbarThumb`, `m_horizontalScrollbarLeftButton`,
      `m_horizontalScrollbarRightButton`, `m_verticalScrollbar`, `m_verticalScrollbarThumb`,
      `m_verticalScrollbarUpButton`, `m_verticalScrollbarDownButton`, `m_ScrollView`;
    - `m_CustomStyles` (`GUIStyle[]`);
    - `m_Settings`.
  - `GUISettings`: `m_DoubleClickSelectsWord`, `m_TripleClickSelectsLine`, `m_CursorColor`,
    `m_CursorFlashSpeed`, `m_SelectionColor`.
  - The data may carry thumb-extent styles as well (`m_horizontalSliderThumbExtent`,
    `m_verticalSliderThumbExtent`, `m_SliderMixed`). Check the skin's type tree through the
    serializer.
- **`GUIStyle`** is serialized with Unity's native layout. The engine's serializer has a reader
  for it (`Engine/Serialization`; search for GUIStyle) that names the fields it fills. Keep that
  reader working, or adapt it with the class.
- **`GUIContent`** is serializable, with `m_Text`, `m_Image`, `m_Tooltip`, `m_TextWithWhitespace`.
- **`RectOffset`** lives in `Math/Rect.cs` and is not part of this rewrite.

The default skin is the `GUISkin` named "GameSkin" in the built-in file
`unity default resources` (a MonoBehaviour object, class ID 114, loaded with the engine's
object loader). If the file is missing (unit tests without game data), an empty skin named
"GameSkin" stands in.

### Reflection contract

The NodeEditor's `GUIScaleUtility` (game code) reflects on `UnityEngine.GUIClip`:
- `GUIClip` must exist as an internal class of the engine assembly;
- it must have a public static method `Clip(Rect)`;
- it must have a non-public static method `GetTopRect()`;
- it must not have a public static property `topmostRect`. In Unity's version it is internal,
  so the utility falls back to its compatibility mode, as it does in the original player.

`GetTopRect()` returns the topmost clip's rect as it was pushed, or (-10000, -10000, 40000,
40000) when nothing is pushed. `Clip`, `Unclip` and their window variants convert between
absolute and clip-local coordinates, as Unity's documentation for the equivalents on
`GUIUtility` describes.

## Behaviour

### The event queue

- **Queue:** `Event.Enqueue` appends an event.
- **`Event.PopEvent(Event outEvent)`** copies the oldest queued event into `outEvent`, removes
  it, and returns true. With an empty queue it returns false.
- **`Event.GetEventCount()`** is the number of queued events.
- **Independent of OnGUI:** popping has no effect on what OnGUI receives. Every input event of a
  frame is still delivered to OnGUI.
- **End of frame:** events nobody popped are discarded, so the next frame starts with an empty
  queue.

### Event properties

- **Data:**
  - `type` and `rawType`;
  - `mousePosition` (in the current clip's coordinates while OnGUI runs) and `delta`;
  - `button`, `clickCount`, `pressure`, `keyCode`, `character`;
  - `modifiers`, with the flags `shift`, `control`, `alt`, `command`, `capsLock`, `numeric`,
    `functionKey`;
  - `commandName`, `displayIndex`.
- **Derived:** `isKey`, `isMouse`, `isScrollWheel`.
- **`Event.current`:** the event being processed, or null outside OnGUI.
- **Copying:** `new Event(Event other)` copies every field.
- **`Use()`:** sets `type` to `Used`. On a `Repaint` or `Layout` event it logs an error and
  does nothing.
- **Disabled GUI:** inside OnGUI, while `GUI.enabled` is false, an input event's `type` reads as
  `Ignore`; `rawType` still gives the original.
- **`GetTypeForControl(int id)`:**
  - While some control holds the mouse (`GUIUtility.hotControl != 0`), mouse events read as
    `Ignore` for every control but that one.
  - Key events read as `Ignore` for every control but the keyboard control, when there is one.
  - Otherwise it returns `type`.
- **`Event.KeyboardEvent(string)`** parses Unity's key-string syntax, as documented: `^` is
  Control, `%` is Command, `#` is Shift, `&` is Alt, followed by a key name.
- **Equality:** `Equals` and `GetHashCode` compare type, modifiers, key and mouse position as
  documented. `ToString` prints the type and the relevant data.

### Dispatch of OnGUI

These rules are pinned by tests in `tests/OniMoono.UnityEngine.Tests/IMGUITests.cs`.

**Each frame:**
1. For every queued input event, in order, the event is sent to the behaviours.
2. Then a `Repaint` event is sent.

**Sending an event:**
- Each behaviour first gets a `Layout` event, then the event itself. A frame with no input calls
  each behaviour with Layout, Repaint. A frame with one mouse press calls Layout, MouseDown,
  Layout, Repaint.
- A behaviour with `useGUILayout == false` gets no Layout events, and its GUILayout calls are
  errors, as documented.
- **Order:**
  - `GUI.depth` orders the behaviours: a lower depth is in front. Equal depths keep the order
    in which the behaviours were enabled.
  - Repaint and Layout go back to front.
  - Input events go front to back, and stop at the first behaviour whose handling `Use`s the
    event.
  - A behaviour's `GUI.depth` is the value it last set during its OnGUI.
- **Skipped behaviours:** disabled, inactive or destroyed behaviours are not called.
- **Clicking outside:** a `MouseDown` that no behaviour uses clears `GUIUtility.keyboardControl`.
  This is how a click outside a text field ends editing.

**State at the start of each OnGUI call:**
- `GUI.skin` is the default skin.
- `GUI.color`, `GUI.backgroundColor` and `GUI.contentColor` are white.
- `GUI.enabled` is true.
- `GUI.matrix` is the identity.
- `GUI.changed` is false.
- The clip stack is empty: the screen is the clip.
- The GUILayout root is an empty vertical group covering the screen.

**Errors:**
- An exception escaping OnGUI is logged with the behaviour as context; the next behaviour still
  runs.
- `GUIUtility.ExitGUI()` throws `ExitGUIException`. The dispatcher catches it silently and skips
  the rest of that OnGUI call. `GUIUtility.guiIsExiting` is true until then.
- If an OnGUI call ends with clips still pushed, the dispatcher logs
  "GUI Error: You are pushing more GUIClips than you are popping. Make sure they are balanced."
  and clears them.

**Outside OnGUI:** calling a GUI function that needs OnGUI throws `ArgumentException` ("You can
only call GUI functions from inside OnGUI."). These include `GUI.skin`, the controls,
`GUIUtility.GetControlID` and the GUILayout calls.

### Control IDs and focus

**`GUIUtility.GetControlID(hint, focusType[, rect])`** gives the same ID to the same control
from one event to the next:
- Each container keeps its own list of IDs across events. A container is one behaviour's OnGUI,
  or one window's function.
- The n-th call of a pass gets the n-th ID of the list when its hint matches.
- On a mismatch, the call takes the next later entry with the same hint.
- When there is none, a new ID is appended.
- So a control skipped in one pass does not change the IDs of the controls after it, as long
  as their hints differ.
- IDs are unique across containers.
- The overloads without a hint use hint 0. The overloads taking `GUIContent` hash its text.

**Mouse and keyboard control:**
- `GUIUtility.hotControl` is the control holding the mouse; 0 is none.
- `GUIUtility.keyboardControl` is the control with keyboard focus; 0 is none.
- Only `FocusType.Keyboard` controls can take keyboard focus.

**Tab:**
- A Tab `KeyDown` that no control used moves keyboard focus to the next keyboard-focusable
  control of the container, in ID order; Shift+Tab moves to the previous one.
- The event is then used.
- The `'\t'` character event of the same press is used too.

**State objects:** `GUIUtility.GetStateObject(Type, id)` returns the object kept for that
control ID, and creates one with the type's parameterless constructor if it is missing or of
another type. `QueryStateObject` returns it without creating, or null.

**Other `GUIUtility` members:**
- `systemCopyBuffer` reads and writes the platform clipboard. Use the engine's platform layer;
  find it through how the old code was called, by searching for "Clipboard".
- `GUIToScreenPoint`, `GUIToScreenRect`, `ScreenToGUIPoint`, `ScreenToGUIRect` convert through
  the clip stack and `GUI.matrix`.
- `RotateAroundPivot` and `ScaleAroundPivot` multiply `GUI.matrix` as documented.
- `hasModalWindow` is true while a modal window (`GUI.ModalWindow`) is shown, false otherwise.
- `pixelsPerPoint` is 1.

### Groups, clipping and the matrix

**`GUI.BeginGroup(rect)` / `EndGroup()`:**
- Inside the group, coordinates, including `Event.current.mousePosition`, are relative to the
  group's top left.
- Drawing is clipped to the group.
- A nested group's visible area is clipped by its parents.
- Test: a group at (100, 50, 200 × 100) holds a group at (150, 80, 200 × 100). For a mouse at
  (130, 70), the mouse reads (30, 20) in the outer group and (-120, -60) in the inner one, and
  the inner group's `GUIClip.visibleRect` is (0, 0, 50 × 20).

**`GUI.BeginClip(rect, scrollOffset, renderOffset, resetOffset)`:** the same as a group, with a
scroll offset added to the local coordinates.

**`GUI.matrix`:**
- It transforms GUI drawing to the screen.
- `Event.current.mousePosition` is given through its inverse.
- Test: after `ScaleAroundPivot((2, 2), (100, 100))`, a mouse at (140, 160) reads (120, 130),
  and `GUIToScreenPoint((200, 200))` is (300, 300).

**`GUI.BeginScrollView(position, scrollPosition, viewRect, …)` / `EndScrollView`:**
- Shows the part of `viewRect` at `scrollPosition` inside `position`.
- Scrollbars appear when the content is larger than the view, or always when asked.
- The scroll wheel scrolls the view under the mouse.
- `ScrollTo(rect)` and `ScrollTowards(rect, maxDelta)` scroll the innermost scroll view to show
  a rect.
- The returned position is clamped to the content.

### Controls

All controls draw only on `Repaint`, and react to input only when their rect is hit. A rect is
hit when it contains the mouse position.

| Control | Behaviour |
|---|---|
| `Label`, `Box` | Draw the content with the style; no input. |
| `DrawTexture`, `DrawTextureWithTexCoords` | Draw a texture. |
| `Button` | Returns true on the `MouseUp` that releases a press that started inside it, if the release is inside. During the press it holds `hotControl`. It returns false on `MouseDown`, and false when released outside. It also triggers on Space or Return when it has keyboard focus. |
| `RepeatButton` | True on each `Repaint` while held with the mouse over it, and on the release inside. |
| `Toggle` | As `Button`, but returns the flipped value on a click. |
| `TextField`, `TextArea` | Edit a string through a `TextEditor` state object (see below); `maxLength` truncates. |
| `HorizontalSlider`, `VerticalSlider` | Drag the thumb, or click the track to jump; return the value clamped to the range. |
| `HorizontalScrollbar`, `VerticalScrollbar` | A slider with a thumb sized by `size`, plus step buttons. |

For the controls above:
- **`GUI.changed`** becomes true whenever a control changes its value.
- **States:** each control draws with its style state: normal, hover (mouse over), active
  (held), focused (keyboard focus), and the `on…` states for a toggle that is on.
- **Disabled:** while `GUI.enabled` is false, controls draw but take no input.
- **`DrawTexture`:** `ScaleMode` places the texture as documented: StretchToFill,
  ScaleAndCrop, ScaleToFit. It is tinted by `GUI.color`.

`DrawTexture` draws during Repaint only.

### Windows

**`GUI.Window(id, rect, func, title[, style])`** and **`GUILayout.Window`:**
- Windows belong to the behaviour that calls them.
- Each frame they are drawn after that behaviour's OnGUI: its background with the window
  style, then its function inside a clip at the window's rect.
- The window function is called for each event, including Layout.
- **Order:** Layout and Repaint run back to front. Input reaches the windows front to back,
  before the behaviour's own OnGUI handles it.
  - Mouse events go to the window under the mouse, or to the window whose control holds the
    mouse.
  - Key events go to the focused window, or to the window holding the keyboard control.
- **Focus:** a `MouseDown` inside a window focuses it and brings it to the front. The order
  of calls, the stacking of windows and whether the press is used were measured: "GUILayout,
  measured", part 4, holds where it differs from this list.
- **Lifetime:** a window that its behaviour did not call during a frame's Layout is gone.
- **Return value:** the window's rect, moved by dragging since the last call; otherwise the
  rect passed in.
- **State:** the colours, enabled state and matrix in effect at the call apply inside the
  window. Each window has its own control IDs.
- **Test:** the call order for one frame is behaviour Layout, window Layout, behaviour Repaint,
  window Repaint.
- **`GUILayout.Window`** sizes the window from its laid-out content, the rect's size and its
  options, by the measured rule in "GUILayout, measured", part 3.

**Window functions:**
- **`GUI.DragWindow(rect)`**, inside a window function: a press in `rect` and a drag move the
  window by the mouse delta. Without an argument, the whole window is the handle. Called
  outside a window function, it logs "Dragwindow can only be called within a window callback".
- **Test:** a press at (60, 60) on a window at (50, 50, 200 × 100), dragged to (90, 75), leaves
  it at (80, 65, 200 × 100).
- **`BringWindowToFront`, `BringWindowToBack`, `FocusWindow`, `UnfocusWindow`:** as
  documented.

**`GUI.ModalWindow(id, rect, func, text[, style])`** (mods use it; measured in the original
player through PerfProbe):
- It is a window as above: its function is called after the calling behaviour's OnGUI, once per
  event (Layout and Repaint each frame), and the call returns the rect (unchanged unless dragged).
- As Unity documents: only one modal window is shown at a time, it is drawn in front of every
  other window, and while it is shown, input goes to it alone: other windows and the controls
  outside it get no mouse or keyboard events.
- The style defaults to the skin's `window` style.

**`GUI.Toolbar(rect, selected, contents[, style])`** and **`GUILayout.Toolbar(selected,
contents[, style], options)`**, with string, texture and GUIContent overloads:
- A row of buttons, one per content, splitting the rect's width equally; each button is drawn with
  the style (default: the skin's `button` style; the game's skin has no separate end-button
  styles). The button at `selected` is drawn in its "on" state.
- A click on a button returns its index (and sets `GUI.changed` when it differs from `selected`);
  otherwise the call returns `selected`.
- **Layout (measured):** the GUILayout version takes the available width (it stretches) and the
  style's height for one line: in a 400-wide area it took (0, 0, 400 × 19) with the default skin,
  and the next control started at y = 23 (the style's margin); with `GUILayout.Width(300)` it is
  300 wide.

### GUILayout

**Passes:**
- During a `Layout` event, GUILayout calls record entries. Each entry carries a minimum and
  maximum size, stretch flags, and margins.
- After the Layout pass of each OnGUI call (and of each `GUILayout.Window`), the entries are
  laid out.
- During the following event of the same OnGUI call, the same calls in the same order return
  the computed rects.
- What a mismatch does, how big entries are and how they are spaced, and how a
  `GUILayout.Window` is sized were measured: see "GUILayout, measured" below. Where this
  section and that one disagree, that one holds.

**Groups:**
- `BeginHorizontal`/`EndHorizontal` and `BeginVertical`/`EndVertical` nest groups.
- `BeginArea(rect)`/`EndArea` lays out a vertical group inside a fixed rect, and clips to it,
  like a group.
- `BeginScrollView`/`EndScrollView` is a group whose content is laid out at its own size and
  shown through a scroll view.

**Entries and options:**
- **Size of an entry:**
  - An entry's size comes from `style.CalcSize(content)`, or from the `GetRect` arguments.
  - A style's `fixedWidth` or `fixedHeight`, when non-zero, fixes that size.
- **Options** (`GUILayout.Width`, `Height`, `MinWidth`, `MaxWidth`, `MinHeight`, `MaxHeight`,
  `ExpandWidth`, `ExpandHeight`) override the size:
  - a fixed size sets both minimum and maximum and turns stretching off;
  - a maximum also turns stretching off on that axis;
  - a minimum raises the maximum if needed, and a maximum lowers the minimum if needed;
  - `Expand…` sets stretching explicitly.
- **Default stretching:** an entry stretches on an axis when its style stretches on that axis
  and has no fixed size there. `GUIStyle.none` stretches horizontally, not vertically.

**Layout along the group's axis:**
- Entries are placed one after the other.
- Between two neighbouring entries, the gap is the larger of the first one's trailing margin
  and the next one's leading margin.
- The group's own style padding insets its content.
- **Extra space:** space beyond the entries' minimum sizes goes first to bring entries towards
  their maximum sizes. The rest is shared equally among the entries that stretch.
- `Space(pixels)` is a fixed gap.
- `FlexibleSpace()` is a stretching empty entry.

**Across the axis:** each entry spans the group's content size if it stretches. Otherwise it
keeps its own size, clamped to its range, at the start.

**Tests:**
- In an area of 200 × 100, a horizontal group of `GetRect(20, 10)` and `GetRect(40, 10)` with no
  style gets (0, 0, 90 × 10) and (90, 0, 110 × 10).
- In an area at (10, 20, 300 × 200), a horizontal group of fixed 50 × 20 and 30 × 40 followed by
  a vertical group of two fixed 100 × 10 entries lays out at:
  - (0, 0, 50 × 20) and (50, 0, 30 × 40);
  - (0, 40, 100 × 10) and (0, 50, 100 × 10).

  These rects are in the area's coordinates, and the area's origin unclips to (10, 20).

**`GUILayoutUtility`:**
- `GetRect(…)` reserves a rect: from a size, a size range, content and style, or an aspect
  ratio.
- `GetLastRect()` returns the last reserved rect; during Layout it returns a dummy
  (0, 0, 1 × 1).
- `GetAspectRect(aspect)` keeps the width-to-height ratio.

**Word wrap:** a word-wrapping style's entries use `CalcMinMaxWidth` for their width range and
`CalcHeight(content, width)` for their height at the width given.

### GUIStyle

**Copying and assignment:**
- The properties are as documented.
- Assigning a `GUIStyleState` or a `RectOffset` to a style copies its values into the style.
  The getters return the style's own objects, so changing them changes the style.
- `new GUIStyle(other)` deep-copies `other`. A null `other` logs an error and copies the skin's
  error style.
- `GUIStyle.none` is an empty style.
- A string converts implicitly to the current skin's style of that name.

**Font:** the style's font, else the current skin's font, else the built-in `LegacyRuntime.ttf`
(`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`). A font size of 0 means the font's
own size.

**Measurement, with the legacy text generator (`TextRendering/`):**
- `CalcSize(content)` is the content's size plus the padding. A fixed width or height replaces
  that axis.
- For an empty content, the width is the horizontal padding and the height is one line plus the
  vertical padding.
- Tests:
  - Padding (3, 5, 2, 4) and empty content give width 8 and height above 4.
  - "Hello Hello" is more than 1.5 × "Hello" wide, at the same height.
  - Two lines are more than one line plus 8 tall.
- `CalcHeight(content, width)` wraps at `width` minus the padding when `wordWrap` is set.
- `CalcMinMaxWidth(content, out min, out max)`: `max` is `CalcSize(content).x`. With
  `wordWrap`, `min` is the smaller of 32 and `max`, not the widest word: see "GUILayout,
  measured", part 5.
- `lineHeight` is the font's line height.
- `GetCursorPixelPosition` and `GetCursorStringIndex` map between string indices and positions,
  through the same layout as drawing.

**`Draw(position, content, isHover, isActive, on, hasKeyboardFocus)`** and its overloads,
during Repaint:
- **State:**
  - A pressed control (active) uses `active`.
  - Otherwise a control with keyboard focus uses `focused`.
  - Otherwise hover uses `hover`.
  - Otherwise it uses `normal`.
  - With `on`, the `on…` variants are used.
  - A state without a background falls back to `normal`'s background (or `onNormal`'s);
    without a text colour it falls back the same way.
- **Background:**
  - The state's texture is drawn nine-sliced by `border`, over `position` grown by `overflow`.
  - It is tinted by `GUI.color × GUI.backgroundColor`.
- **Content:**
  - The content goes in `position` shrunk by `padding` and shifted by `contentOffset`.
  - `imagePosition` places the image and the text: image left, image above, image only, or
    text only.
  - The text is aligned by `alignment`, coloured by the state's text colour × `GUI.color` ×
    `GUI.contentColor`, and wrapped with `wordWrap`.
  - Rich text is interpreted when `richText` is set.
  - `TextClipping.Clip` clips text to the content rect; `Overflow` does not.
- **Clipping:** everything is clipped by the current clip.
- **`DrawCursor` and `DrawWithTextSelection`:** draw a text field's text with its cursor and
  selection, in the skin's `settings.cursorColor` and `settings.selectionColor`.
- **Test:** a `GUI.Box` draws the box background as nine slices: 36 vertices, all inside the
  box rect. It uses `Hidden/Internal-GUITextureClip`, with a projection mapping pixel (0, 0) to
  clip (-1, 1). A long label inside a 30 × 20 group draws its glyphs with
  `Hidden/Internal-GUITextureClipText`, all inside the group.

### GUISkin and GUIContent

**`GUISkin`:**
- Properties for each standard style and for `font`, `customStyles` and `settings`.
- **`GetStyle(name)`:** looks up standard and custom styles by name, ignoring case. A missing
  name logs a warning ("Unable to find style '…' in skin '…' …") and returns the error style.
- **`FindStyle(name)`:** returns null when the name is missing.
- **Enumeration:** `GetEnumerator` enumerates all styles.
- **Test:** `GetStyle("Label")` is the same object as `skin.label`.
- **Built-in "GameSkin":** its button and window have background textures, and its button has
  a non-zero border. `settings.doubleClickSelectsWord` is true.

**`GUIContent`:** text, image and tooltip, with the documented constructors. `GUIContent.none`
is an empty content.

### TextEditor

Public API as documented:
- **Members:** `text`, `cursorIndex`, `selectIndex`, `SelectedText`, `hasSelection`, `position`,
  `style`, `multiline`, `controlID`, `scrollOffset`, `keyboardOnScreen`.
- **Moving and selecting:** `MoveLeft`, `MoveRight`, `MoveUp`, `MoveDown`, `MoveWordLeft`,
  `MoveWordRight`, `MoveLineStart`, `MoveLineEnd`, `MoveTextStart`, `MoveTextEnd`, the
  `Select…` variants, `SelectAll`, `SelectNone`, `SelectCurrentWord`,
  `SelectCurrentParagraph`.
- **Editing:** `Insert`, `Delete`, `Backspace`, `DeleteWordBack`, `DeleteWordForward`,
  `ReplaceSelection`, `Cut`, `Copy`, `Paste`.
- **Undo:** `SaveBackup` and `Undo`.
- **Events:** `HandleKeyEvent`, `OnFocus`, `OnLostFocus`, `DrawCursor`.
- Indices are string indices.

**Rules:**
- **Movement:**
  - Moving left or right with a selection collapses it to the selection's start or end.
  - A word is a run of letters and digits; moving by word skips spaces and punctuation as text
    editors on Windows and Linux do.
- **Selection:** `SelectCurrentWord` selects the word at the cursor.
- **Clipboard:** `Copy` puts the selection, or the whole text when nothing is selected, on the
  clipboard. `Paste` replaces the selection with the clipboard. Single-line editors replace new
  lines with spaces.

**Test:**
1. `SelectAll` and `Copy` on "copy me" put "copy me" on the clipboard and set `selectIndex` to 7.
2. `MoveLeft` then puts the cursor at 0.
3. `MoveWordRight` puts it at 4.
4. `MoveLeft` and `SelectCurrentWord` then give "copy".

**Keys** (`HandleKeyEvent`, Windows and Linux bindings):

| Key | Action |
|---|---|
| Left, Right, Up, Down | move; with Shift, select |
| Ctrl+Left, Ctrl+Right | move by word; with Shift, select |
| Home, End | line start and end |
| Ctrl+Home, Ctrl+End | text start and end |
| Backspace, Delete | delete the character or the selection |
| Ctrl+Backspace, Ctrl+Delete | delete by word |
| Ctrl+A | select all |
| Ctrl+C, Ctrl+X, Ctrl+V | copy, cut, paste |
| Ctrl+Z | undo |

The function-key flag set by the platform does not prevent matching.

### Text fields

`GUI.TextField(rect, text[, maxLength][, style])`, `TextArea` (multi-line) and their GUILayout
forms:
- **Focus:** a press inside takes `hotControl` and keyboard focus and places the cursor at the
  mouse. Focusing by a click selects the whole text.
- **Clicks:** a double click selects a word, when the skin's settings allow it; a triple click
  selects a line, likewise.
- **Dragging** selects. The release frees `hotControl`.
- **Keys:** with keyboard focus, key events go to the editor's `HandleKeyEvent` first.
- **Typed characters** (`KeyCode.None` events with a character) are inserted when the field's
  font has the character.
  - `'\n'` is inserted only in multi-line fields.
  - `'\t'` is not inserted.
- **Results:** a change sets `GUI.changed` and is returned. `maxLength` truncates.
- **Drawing:** without focus the field draws as its style. With focus it draws the text with
  cursor and selection, scrolled to keep the cursor visible.
- **Test sequence:**

  | Step | Result |
  |---|---|
  | Click to focus "abc", then End, 'd', 'e', Backspace, Home, Delete | "bcd" |
  | Ctrl+A, Ctrl+C | clipboard "bcd" |
  | Typing 'x' | "x" (it replaces the selection) |
  | Ctrl+V | "xbcd" |
  | A click outside | `keyboardControl` is 0 |

### GUI.tooltip (measured 2026-10-24)

Found by a mod spec: Customize Duplicant traits (Workshop 3687060621,
`tests/e2e/mods/3687060621-customize-duplicant-traits.yaml`). Its editor is a `GUI.Window`
whose function lays out labels whose `GUIContent` carries a tooltip, many of them inside a
`GUILayout.BeginScrollView`. At the end of the function it reads `GUI.tooltip` and shows it in
a `GUILayout.Label` in a second scroll view, or a placeholder ("Hover over an item to see its
description...") when the value is empty. This implementation has no `GUI.tooltip`. The window
function throws `MissingMethodException: Method not found: 'System.String
UnityEngine.GUI.get_tooltip()'` on every event, so the window draws nothing and the crash
screen opens. No game assembly uses the member, so `tools/ApiCheck` does not report it.

**Interface** (`UnityEngine.IMGUIModule`):
- `public static string GUI.tooltip { get; set; }`;
- `protected static Rect GUI.tooltipRect { get; set; }`. It is protected in a class with a
  public constructor, so a subclass of `GUI` can read it. No mod uses it.

**Measured** with `PROBE_SCENARIO=tooltip` (tools/UnityProbe, `Tooltip.cs`, lines `TT …`, 10 s
into the main menu, about a minute and a half). One behaviour draws controls whose
`GUIContent` has a tooltip ("tA" for control A) or none:
- `GUI.Button` A and B (B without a tooltip), `GUI.Label` L, `GUI.Box` X, `GUI.Toggle` T;
- two buttons O1 and O2 that overlap by 60 pixels, and a button H drawn with `GUI.enabled`
  false;
- `GUILayout.Button` C and `GUILayout.Label` D in a `GUILayout.BeginArea`, with a 20-pixel
  `Space` between them;
- a button E in a `GUI.BeginGroup`, and buttons F and G in a `GUI.BeginScrollView` 80 high,
  G at y 100 (scrolled out of view);
- a button Z under window 101.

It opens three windows:
- **101 (`GUI.Window`):** buttons W1a, W1c and W1d, a label W1b without a tooltip, a button W1g
  in a group, a title `GUIContent` with a tooltip, and a `GUI.Window` 103 called from inside
  its function;
- **102 (`GUILayout.Window`):** laid out like the mod's window. It has a button W2a, a label
  W2b without a tooltip, a `GUILayout.BeginScrollView` 100 high of six labels W2s0 to W2s5 with
  tooltips (W2s5 out of view), and a label showing `GUI.tooltip` as read after them;
- **104 (`GUI.Window`):** over part of 101, with a button W4a that overlaps W1d.

A second behaviour, enabled after the first, only reads. The probe moves the real mouse with
XTest over the centre of each control (its screen rect measured during Repaint), and between
them. Two frames are recorded at each point. Every OnGUI call and window function logs one
line per event: `GUI.tooltip` at its start, after each control and at its end, with
`tooltipRect`, `hotControl` and `keyboardControl`. Update, LateUpdate and the end of the frame
read the value as well. The points are then visited again in mode "reset": every call first
sets `GUI.tooltip = "-"`, so its line shows exactly which of its own controls set the value.
Then come modes that set the value from code:
- at the start of OnGUI;
- at the end of its Repaint;
- inside window 101;
- at the end of window 102's Repaint;
- to null.

Further modes give button A or toggle T keyboard focus, and the mouse button is pressed over
A, L, W2a, W1a and W4a: clicked, then held, dragged off and released off.
`tools/UnityProbe/tt-check.py rule LOG` checks the rules below. They hold for all 3,643 calls
of the original's log (278 points where a control was expected to set the value).
`tt-check.py diff ORIG OURS` compares two players' logs per step, and `tt-check.py show LOG`
prints them.

1. **`GUI.tooltip` is one global string that only controls and user code write.**
   - **Initial value:** it reads "" before any control has set it, including in the probe's
     `Awake` at mod load.
   - **Never null:** a set to null reads back "".
   - **Outside OnGUI:** it reads the same value in Update, LateUpdate and at the end of the
     frame, without an error.
   - **Persistence:** the value carries over unchanged between events, between OnGUI calls,
     between behaviours, into and out of window functions, and from frame to frame.
   - **Nothing clears it.** The engine never resets it at the start of a frame, an event, an
     OnGUI call or a window function. Moving the mouse off every control leaves the last
     tooltip in place, whether onto a control without a tooltip (B, W1b, W2b), onto empty
     window space, or off every window. So the mod's description box keeps showing the last
     description hovered.
2. **A control sets it while it draws during a Repaint, when the control is hovered and its
   content's tooltip is not empty.** The value becomes that tooltip at the point the control
   is drawn. Controls drawn later in the same call read the new value.
   - **Hovered** means two things. The mouse (`Event.current.mousePosition`, in the control's
     own coordinates) is inside the control's rect, as `Rect.Contains` decides: (21, 21) in A's
     (20, 20, 100 × 30) sets it, (19, 19) does not. And the mouse is inside the visible area of
     the clip the control is drawn in.
     - The clip may be a group, an area, a scroll view or the window: a button or label
       scrolled out of a scroll view does not set the value with the mouse over its unclipped
       rect. That held for G in a `GUI` scroll view and for W2s5 in a `GUILayout` one.
     - Windows do not hide what lies under them. Button Z under window 101 sets the value. So
       do W1d and W4a, overlapping in windows 101 and 104: each sets it as it is drawn.
   - **The control holding the mouse** (its ID is `GUIUtility.hotControl`) also sets the value
     on each Repaint while the mouse is elsewhere. A pressed on A and dragged off the screen
     sets "tA" each frame until the release.
   - **Which controls:** `GUI.Button`, `GUI.Toggle`, `GUI.Label`, `GUI.Box`, a disabled
     `GUI.Button` (`GUI.enabled` false), and `GUILayout.Button` and `GUILayout.Label`, at the
     rect the layout gave them. GUI and GUILayout controls behave the same.
   - **Empty tooltips write nothing.** A control whose content has an empty tooltip, under the
     mouse, leaves the value as it was.
   - **A window's title** `GUIContent` with a tooltip, under the mouse on the title bar, does
     not set it.
   - **Keyboard focus does not count.** With `keyboardControl` set to A's or T's ID, and the
     mouse elsewhere, neither sets it.
   - **No other event sets it.** Layout, MouseDown, MouseUp, MouseDrag, Used and Ignore
     events change nothing, even with the mouse over a control. (The player sent no MouseMove
     events.)
3. **Several hovered controls: the last one drawn wins.** Over the overlap of O1 and O2, O1
   sets "tO1" and then O2 sets "tO2". Over W1d and W4a, W4a's window is drawn first and W1d's
   last, so the value ends as "tW1d". The window drawn last is the front window (rule 6).
4. **A read in a window function after its controls** (the mod's pattern) gives the tooltip of
   the control hovered in that window. During Repaint it reads the value set by the window's
   own hovered label. During Layout it reads the value the previous frame's Repaint left. So
   both passes read the same string, and the window lays out the same controls in both.
5. **A set from code sticks until the next control sets the value, or the next set.**
   - Set to "S" at the start of every OnGUI call, with the mouse on empty space, it reads "S"
     everywhere: in the same call, in the second behaviour, in every window and outside
     OnGUI.
   - With the mouse over A, A replaces it in Repaint ("tA"), and "tA" is what the windows,
     the end of the frame and the next frame's Layout read, until the next set.
   - Set at the end of the Repaint call ("E"), it is what the windows drawn after it read
     (unless one of their controls is hovered), and what every call of the next frame reads
     until a hovered control sets it again.
   - Set inside window 101's function ("W"), it is what the windows drawn after it and the
     next frame read.
   - Leaving the mode leaves the last set value in place.
6. **The order of calls in a frame**, as measured with two behaviours (enabled in the order
   gui, gui2) and three windows of the first one. It decides which value a call reads at its
   start and which control wins:
   - Layout: gui, gui2, then the windows from front to back. At first that is 101, 102, 104
     (101 in front); after a click on 102 it is 102, 101, 104.
   - Repaint: gui2, gui (the reverse), then the windows from back to front: 104, 102, 101 at
     first, and 104, 101, 102 after the click on 102.
   - An input event over a window goes to that window first. Clicking W2a sent MouseDown to
     102, and then gui, its behaviour, got it as `Used`.

   This is not the order "Dispatch of OnGUI" and "Windows" describe (each behaviour's Layout
   and event, with its windows right after it). "GUILayout, measured" (part 4) measured it
   in full and settles it: the windows of every behaviour come after all the behaviours, in
   every event.
7. **`tooltipRect`** starts as (0, 0, 0 × 0). Only a label sets it: `GUI.Label` or
   `GUILayout.Label` with a non-empty tooltip, hovered, during Repaint. It becomes a rect of
   size 0 at the mouse position in the label's own coordinates:
   - (70, 116) for L;
   - (100, 55) for D in the area at (20, 180), with the mouse at (120, 235);
   - (132, 14) for W2s0 inside window 102's scroll view.

   Buttons, boxes and toggles leave it alone. It persists like the tooltip.

**Seen in passing** (not tooltip rules; for the implementer's information):
- **Nested windows:** a `GUI.Window` called inside another window's function (103 inside
  101) never shows in the original. Its function is never called, and the call returns
  (0, 0, 0 × 0).
- **Release outside a window:** the original delivers a mouse release outside the window
  whose control holds the mouse to that window's function as `Ignore` (`rawType` MouseUp).
  `hotControl` stays set until the next press. While it stays, that control keeps setting
  the tooltip (rule 2): after W1a was pressed and released off the window, "tW1a" was set on
  every Repaint.
- **`GUILayout.Window`** kept its given height (300) in the original, although its content is
  about 190 high.

**Where this implementation deviates**, from the same probe (`tt-check.py diff` finds all 157
steps different, as `GUI.tooltip` is missing):

| Case | original | this implementation |
|---|---|---|
| `GUI.tooltip` read anywhere (Awake, Update, OnGUI, a window function) | "" until a control sets it | `MissingMethodException` |
| mouse over button A (tooltip "tA"), Repaint | "tA" from A's draw on | missing |
| then over button B (no tooltip), or over nothing | stays "tA" | missing |
| Layout, MouseDown, MouseUp, MouseDrag over A | unchanged | missing |
| label L, box X, toggle T, disabled button H | each sets its tooltip | missing |
| `GUILayout.Button` C / `GUILayout.Label` D in an area; the space between them | set; not set | missing |
| button F in a scroll view; G scrolled out of view | set; not set | missing |
| O1 and O2 both under the mouse | "tO1", then "tO2" (last drawn) | missing |
| button Z under window 101 | sets "tZ" | missing |
| W1d (window 101, front) over W4a (window 104) | "tW4a", then "tW1d" | missing |
| window 101's title, tooltip "tWin1" | not set | missing |
| keyboard focus on A, mouse elsewhere | not set | missing |
| A pressed and dragged off | "tA" on each Repaint while held | missing |
| set "S" at OnGUI start, mouse over A | "S" until A's draw in Repaint, then "tA" | missing |
| set null | reads "" | missing |
| window 102 reads it after its hovered label W2s0 | "tW2s0" in Layout and Repaint | missing; the window throws as well (below) |
| `tooltipRect` after hovering label L | (70, 116, 0 × 0) | missing |
| order of calls (two behaviours, windows) | rule 6 | gui Layout, its windows' Layout, gui Repaint, its windows' Repaint, then gui2 |
| `GUI.Window` 103 inside window 101's function | no window; returns (0, 0, 0 × 0) | shown at (450, 620, 200 × 80), relative to 101; its function runs |
| `GUILayout.Window` 102 | called for Layout and Repaint from the first frame | its function gets no Layout event until window 101 is clicked; each Repaint throws "GUILayout mismatch: entry 0 was asked for in a group of 0 entries during Repaint" (1,016 times in the run) |
| mouse release outside window 101 while W1a holds the mouse | `Ignore`; `hotControl` stays | `MouseUp`; `hotControl` released |
| `GUILayout` area and labels | C at (20, 180, 200 × 20), labels 19–20 high | C at (24, 184, 192 × 20.95), labels 20.95 high |

The three rows after the `tooltipRect` row are not about the tooltip. The 102 failure
probably follows from window 103, which this implementation shows; that was not tried
separately. Not measured: `GUI.tooltip` with `GUI.ModalWindow`, tooltips on `TextField` or
slider content (those controls take no `GUIContent`), `GUI.Toolbar` and `SelectionGrid`
buttons, `GUIStyle.Draw` called directly with a content, and a matrix other than the
identity.

### GUILayout, measured (2026-10-24)

Found by the same mod spec as "GUI.tooltip": Customize Duplicant traits (Workshop 3687060621,
`tests/e2e/mods/3687060621-customize-duplicant-traits.yaml`). Its editor is a `GUI.Window`
whose function lays out, with GUILayout, a scroll view of traits, a text field and a SAVE
button in a horizontal group, then either a label ("No presets saved yet.", `Height(40)`) or
a `GUILayout.BeginScrollView(Height(80))` of preset rows (a label, LOAD and DEL), then a
second scroll view and [ CLOSE EDITOR ]. The SAVE handler writes a preset file part-way
through the MouseUp event, so the calls after it ask for a scroll view where the Layout
before them recorded a label. This implementation then throws `ArgumentException: GUILayout
mismatch: entry 7 was asked for in a group of 13 entries during Used` from
`GUILayout.BeginScrollView`, and the crash screen opens. Its entries are also taller (labels
20.95 against 19 or 20), so DEL sits at about y 367 against 355.

**Measured** with `PROBE_SCENARIO=guilayout` (tools/UnityProbe, `GuiLayout.cs`, lines `GL …`,
10 s into the main menu, about 80 s; `PROBE_GL_PART=size`, `wsize`, `order` or `mm` runs one
part). The skin is `GUI.skin` as the game has it (the built-in "GameSkin", font
`LegacyRuntime` at size 13). `tools/UnityProbe/gl-check.py rule LOG` checks the rules below
(3,112 checks hold on the original's log), `gl-check.py diff ORIG OURS` lists every
difference, and `gl-check.py show LOG [size|ws|ord|mm]` prints a part. Values are in the
coordinates `GUILayoutUtility.GetLastRect` gives: those of the innermost clip (an area, a
scroll view's content, a window), not of the group.

The skin's styles, as `(left, right, top, bottom)`:

| style | padding | margin | border | other |
|---|---|---|---|---|
| label | 0,0,3,3 | 4,4,4,4 | 0 | word wrap, rich text |
| button | 6,6,3,3 | 4,4,4,4 | 6,6,6,4 | centred |
| toggle | 15,0,3,0 | 4,4,4,4 | 14,0,14,0 | overflow -1,0,-4,0 |
| textField | 3,3,3,3 | 4,4,4,4 | 4,4,4,4 | |
| textArea | 3,3,3,3 | 4,4,4,4 | 4,4,4,4 | word wrap |
| box | 4,4,4,4 | 4,4,4,4 | 6,6,6,6 | |
| window | 10,10,20,10 | 0 | 8,8,18,8 | content offset (0, -18) |
| scrollView | 0 | 0 | 0 | |
| verticalScrollbar | 0,0,1,1 | 1,4,4,4 | 0,0,9,9 | fixed width 15 |
| horizontalScrollbar | 0 | 4,4,1,4 | 9,9,0,0 | fixed height 15 |

Every style's font is null (the skin's font); `lineHeight` reads 14 for all of them.

#### 1. Calls that differ from the Layout before them

During Layout, GUILayout records an entry per call (a leaf for a control, `Space`,
`FlexibleSpace` or `GetRect`; a group for `BeginHorizontal`, `BeginVertical`,
`BeginScrollView`; areas go in a list of their own). In every later event of the same OnGUI
call or window function, each call takes the next recorded entry of the current group, by
position only. What the entry was recorded for does not matter, with one exception (a
group). Cases measured in OnGUI, in a scroll view in OnGUI, in a `GUI.Window`, in a scroll
view in a `GUI.Window` and in a `GUILayout.Window`, all alike:

1. **A leaf call takes the next entry's rect, whatever it was.**
   - Content or size arguments are ignored: a label whose text grew to two lines, or a
     `GetRect(200, 60)` recorded as `GetRect(20, 10)`, gets the recorded rect.
   - An inserted label, `Space` or `FlexibleSpace` takes the rect of the entry recorded where
     it now stands, and every later call shifts by one.
   - A leaf call where a group was recorded gets the group's whole rect (a label in place of
     a horizontal group or a scroll view gets that group's rect). The group's children are
     never read, and the calls after it go on with the group's next sibling.
2. **Fewer calls than entries:** nothing happens. Entries left over are ignored, and
   `EndHorizontal`/`EndVertical`/`EndScrollView` close the group without an error.
3. **More calls than entries:** it depends on the event.
   - **Repaint:** the call throws `ArgumentException` with the message `Getting control N's
     position in a group with only N controls when doing repaint` + "\n" + `Aborting`, where
     N is the entry count of the group (also its index: "control 3 ... only 3"). The same
     holds for `BeginArea` when no area was recorded ("control 0 ... only 0").
   - **Other events** (MouseDown, MouseUp): no exception. The call gets the rect of the
     group's last entry again.
4. **A Begin call where a leaf was recorded** (`BeginHorizontal`, `BeginVertical` or
   `BeginScrollView` in place of a label) throws `ExitGUIException` with the message
   `GUILayout: Mismatched LayoutGroup.` followed by the event type as `EventType.ToString()`
   gives it: `repaint`, `mouseDown`, `mouseUp` (the lower-case aliases). The group is not
   opened. A Begin call where any group was recorded takes it, even of another kind: a
   `BeginHorizontal` in place of a recorded `BeginVertical` lays its children out
   vertically, as recorded.
5. **During an event whose type is `Used`, nothing is checked.** Every GUILayout call returns
   the dummy rect (0, 0, 1 × 1), as during Layout; Begin and End calls succeed, more or fewer
   or other calls than recorded raise nothing, and no entry is consumed. This is what a
   button handler meets: `GUILayout.Button` uses the MouseUp that clicks it, so every call
   after a click (SAVE adding the preset scroll view, DEL removing a row) runs in a `Used`
   event and gets (0, 0, 1 × 1). The same after `Event.current.Use()` in a MouseDown, and
   in the behaviour's OnGUI after its window used the event. Controls before the click got
   their real rects in that event.
6. **What the player does with an exception that escapes:**
   - The `ArgumentException` of rule 3 is logged as an exception (`LogType.Exception`, its
     message as above). `ExitGUIException` (rule 4) is not logged at all.
   - Either ends that OnGUI call or window function only. The other behaviours and the
     other windows are still called for the same event, and the window keeps being shown.
   - In a window function left inside a `BeginScrollView`, the player also logs the error
     `GUI Error: You are pushing more GUIClips than you are popping. Make sure they are
     balanced.`
   - The next event starts afresh: the next Layout records the calls as made, and the
     frames after run as before.
7. **When the caller catches the exception** and goes on: after an `ArgumentException` the
   position does not advance (each later call in that group throws too, `End` still closes
   it). After the `ExitGUIException` of rule 4 the group was not opened: the calls that
   follow take the parent's next entries, and the matching `End` closes the parent (an
   `EndArea` with no area left throws `InvalidOperationException: Stack empty.`, and an
   `EndArea` that closes a scroll view's clip logs `Invalid GUIClip stack popping`).
8. **`GUILayoutUtility.GetLastRect` straight after a Begin call** logs the error `You cannot
   call GetLast immediately after beginning a group.` and returns (0, 0, 1 × 1). After an
   End call it returns the group's rect.

#### 2. Entry sizes and spacing

**Text height.** With the legacy font at size `s` (the style's `fontSize`, or 13 when it
is 0), one line of text is `s + 0.01` high and each further line adds `1.1 × s`:
13.01, 27.31, 41.61 for one to three lines at 13; 8.01 and 16.81 at 8; 30.01 and 63.01 at
30. Bold and rich text are the same height. An empty text is one `lineHeight` (14).
`CalcSize` adds the padding: a label "x" is 6.19 × 19.01, a button 18.19 × 19.01, a toggle
21.19 × 16.01, a box 14.19 × 21.01. Border and overflow do not count.

**An entry's size** (at the default skin, in a 400-wide area):
- **Width:** an entry stretches to the group's width when its style stretches (all of the
  skin's text styles do). Otherwise, and as its minimum, it is `ceil(CalcSize.x)` ("x" in a
  label: 7; in a button: 19). `Width(w)` fixes it. A word-wrapping style takes
  `CalcMinMaxWidth` (min the smaller of 32 and the whole text, max the whole text; part 5).
- **Height, styles without word wrap** (button, toggle, textField, box):
  `ceil(CalcSize.y)`: button 20, 34, 48 for one to three lines; toggle 17; box 22.
- **Height, word-wrapping styles** (label, textArea): `CalcHeight(content, width)` at the
  entry's final width, unrounded (19.01, 33.31, 47.61), and the group rounds it (below).
- `fixedHeight` replaces the height (a label with 50 is 50); `Height(h)` likewise; a
  `Box("", Height(2), ExpandWidth(true))` is 2 high.

**Rounding.** A group places its entries along its axis from an unrounded running position
(sizes as above, unrounded), and rounds each entry's position and size on that axis to the
nearest whole number. Across the axis the size is not rounded. So labels are 19 high in a
vertical group and 19.01 in a horizontal one, and fifteen labels in a window put the last at
`round(20 + 14 × (19.01 + 4)) = 342`.

**Spacing along a group's axis.** Between two neighbours the gap is the larger of the first
one's trailing margin and the next one's leading margin (label after label: 4, so labels sit
at y 0, 23, 46; margins 10 then 3: 10; 2 then 20: 20). `Space(n)` and `FlexibleSpace` take no
margin and no gap of their own: they start where the previous entry ends, and the next entry
keeps its leading margin after them (label, `Space(10)`, label: 0, 19, 33). A `Space` entry's
rect reads x -4 (its zero margin less the group's, below) and width 0.

**Edges of a group without a style** (`BeginVertical()`, `BeginHorizontal()`, an area
without a style, and the root of OnGUI). The group passes its children's margins out:
- Its own margin is, across its axis, the smallest of its children's margins on that side,
  and along its axis the first child's leading margin and the last child's trailing margin.
- Each child is inset across the axis by its margin less that smallest one, and the first
  starts at 0. So labels (margin 4) in an area start at (0, 0) and span the full 400; a
  label with margin 8 among them starts at x 4 and is 392 wide; after a margin-0 label,
  margin-4 labels start at x 4.
- An area or the OnGUI root drops these margins (it has no parent).
- Example (`grp.vertical.mixed`): a label, then a group of a margin-0 label and a margin-4
  label, then a label: the first label at x 4 (the area's smallest margin is the group's 0),
  the group at (0, 23, 400 × 42), its children at x 0 and 4, the last label at y 69.

**Edges of a group with a style** (`BeginVertical(GUI.skin.box)`, `BeginArea(rect, style)`,
a window's content, a scroll view's content). Each child is inset by the larger of the
style's padding and its own margin, side by side; between children the margin rule above
holds. The group's own margin is its style's margin.
- In a box (padding 4) a label sits at (4, 4) and is 392 wide; the group is 400 × 27.
- Padding 2: a label at (4, 4); a label with margin 8 at (8, 8), the next label at x 4.
- Padding (5, 6, 7, 8): a label at (5, 7), 389 wide; the group is 34 high (7 + 19 + 8).
- An area with the window style: (10, 20).

**The OnGUI root** is a vertical group from (0, 0) that is not as wide as the screen: it is
as wide as its widest entry's minimum, and stretching entries take that width. A label
"root0" and a button "root1" are both 42 wide; add a `Label(…, Width(300))` and all are 300.

**`BeginArea(rect)`** places its content at the rect's origin: a `GUILayout.Button` first in
an area at (20, 180) is at (0, 0) in the area, (20, 180) on the screen, as wide as the area.

**Scroll views** (`GUILayout.BeginScrollView`):
- The view is an entry of the parent with the `scrollView` style (no padding, no margin).
  Without options it stretches both ways: in a 400 × 400 area after nothing, a scroll view of
  two labels is 400 × 377 (the label after it takes the rest). `Height(h)` fixes it;
  `MinHeight(50), MaxHeight(70)` gives 70 when there is room, whatever the content.
- The content is a group with a style (no padding), so its entries keep their full margins:
  labels at (4, 4) and 392 wide in a 400-wide view; a margin-0 label at (0, 0).
- When the content is taller than the view, the vertical scrollbar takes its fixed width
  plus its left margin (15 + 1) from the content: labels are 376 wide. Asking for it
  (`alwaysShowVertical`) does the same.
- Content wider than the view (`Width(600)`) keeps its width.
- A background style passed in is the view's style: a box background gives the view the
  box's margin.

**Windows.** A `GUILayout.Window` and the GUILayout content of a `GUI.Window` are laid out in
a group with the window style: the first label at (10, 20) in window coordinates (the
padding), 280 wide in a 300-wide window, the next at y 43. With a style of padding (5, 6,
7, 8): (5, 7), 289 wide. (With `GUIStyle.none` the first label read (4, 0), 292 wide:
measured, not explained by the rule above.)

#### 3. `GUILayout.Window`'s rect

- **During Layout** the call returns the rect passed in, unchanged.
- **In every other event** it returns the laid-out rect: the position passed in, and per
  axis the larger of the content's minimum size and the size passed in after the options:
  `height = max(contentMin, clamp(given.height, MinHeight, MaxHeight))`, with `Height(h)`
  standing for the given height and `ExpandHeight` changing nothing; the same for the width
  with `Width`, `MinWidth`, `MaxWidth`.
  - The content's minimum height is the padding (20 + 10) plus the entries' unrounded
    heights and gaps: two labels give 30 + 19.01 + 4 + 19.01 = 72.02, fifteen give 371.15.
  - The content never shrinks below what it needs: `Height(100)` or `MaxHeight(100)` with
    371.15 of content returns 371.15; with 72.02 of content `MaxHeight(100)` on a given 300
    returns 100.
  - Measured: given 300 × 300 and two labels: 300 × 300 (the mod's case: it keeps 300). Given
    100 × 50: 100 × 72.02. Given 0 × 0: 42 × 72.02. `Width(250)`: 250; `MinWidth(400)`: 400;
    `Height(250)` or `MinHeight(250)`: 250; `Width(100), Height(100)` on 300 × 300: 100 × 100.
    Labels wrap to the given width rather than widening the window (100 wide: 100 ×
    800.15).
- **Between frames:** each frame's result depends only on that frame's rect and content.
  Fed back (`rect = GUILayout.Window(...)`), it grows with the content and shrinks with it
  down to the rect fed in, which by then is the earlier result: content growing from one to
  sixteen labels and back, on 200 × 50 fed back, returned 50, 118.04, ... 479.96 and then
  stayed at 479.96. Not fed back, it followed the content (back to 50). A given size growing
  or shrinking each frame is returned as given once it exceeds the content.
- The window lays its content out at the returned size.

#### 4. Order of calls and windows

Measured with two behaviours (`gui`, enabled first, and `gui2`) and `GUI.Window`s declared
by them (`gui` in the order 402, 401, 403; `gui2` 405), then windows added, dropped and
reordered. The rule settles "GUI.tooltip" rule 6.

1. **Each event** is sent to every behaviour's OnGUI, and then to the windows of all the
   behaviours:
   - Layout: `gui`, `gui2`, then every window from front to back.
   - Repaint: `gui2`, `gui` (the reverse), then every window from back to front.
   - MouseDown and MouseUp: first the front window under the mouse, if any (only that one),
     then `gui`, `gui2`. A press on a window's empty area reaches the behaviours as
     `MouseDown`, not `Used`; a press a control uses reaches them as `Used`.
   - The player sends no MouseMove events.
   - Each mouse event is preceded by its own Layout (behaviours, then windows).
2. **Stacking:**
   - A window declared in a frame that it was not declared in the frame before goes in
     front of all the others. Several new in one frame go in front in the order they were
     declared, the first declared frontmost: at first 402, 401, 403, 405 from front to back.
   - So the first-declared window is in front when all appear together, and a window
     declared later is in front of those already shown: 404 added later went in front, then
     400 (declared first in code but new) in front of it; 402 dropped for a frame and
     declared again came back in front; 406 of `gui2` in front of `gui`'s.
   - A window not declared during a frame's Layout is gone (it loses its place).
   - A press on a window brings it to the front at once: its next Layout has it first.
3. **`GUI.BringWindowToFront(id)` and `BringWindowToBack(id)`**, called in OnGUI during
   Layout, take effect in that same Layout's window pass (the windows run after the
   behaviours). Called during Repaint, they take effect from the next frame. Called outside
   OnGUI (in Update), `BringWindowToFront` neither throws nor changes anything.
4. **`GUI.FocusWindow(id)` and `GUI.UnfocusWindow()`** do not change the order.

#### 5. Too little width, and word wrap (measured 2026-10-29)

Found by ModTools (Workshop 3241644477, `tests/e2e/mods/3241644477-modtools.yaml`). Its Runtime
Console is a `GUI.Window` (620, 120, 650 × 500) whose function lays out, in a `BeginVertical()`,
a `BeginHorizontal()` toolbar ("X - Close Window" and "Clear" buttons, a label "Count: N", a
`TextField(MinWidth(220))`, five toggles and two `Space(10)`), then a
`BeginScrollView(Width(650 - 30))` of rows, each a `BeginHorizontal()` of a "Copy" button
(`Width(44), Height(24)`) and a `Label(line, Height(24))` of a log line. In the original the toolbar
is wider than the window and runs off its right edge, "Count: 59" breaks inside the word
("Coun" / "t: 59"), and long log lines wrap inside the scroll view, which has no horizontal
scrollbar.

**Measured** with `PROBE_SCENARIO=guilayout PROBE_GL_PART=wrap` (`tools/UnityProbe/GuiLayoutWrap.cs`,
lines `GL wrap …`; a full run leaves this part out). Each entry's rect is logged as
`GetLastRect` gives it, and a label's line breaks as the character indices where
`GetCursorPixelPosition` moves to a new line (`brk=`). A scroll view is given the scroll
position (100000, 100000); the position it returns is how far its content overflows the view.
The window part draws the mod's window with six fixed log lines. Skin and coordinates are those
of the parts above.

1. **`CalcMinMaxWidth` with `wordWrap`:** `max` is `CalcSize(content).x`; `min` is 32, or `max`
   when that is smaller. It does not depend on the words: "Count: 59" 32 and 54.72; "Count:"
   32 and 37.02 (wider than 32); "X - Close Window" 32 and 100.55; "Show when Start" 32 and
   96.06; a 92-character log line 32 and 535.33; "Clear" 30.31 and 30.31.
2. **Where wrapped text breaks** (label, padding 0 across): at a space when the line's text up
   to it fits; a word wider than the whole line is broken after its last character that fits,
   and the rest goes on the next line. "Count: 59" at width 60 and more is one line (19.01
   high); at 45 and 50 it breaks before "59" (index 7; 33.31 high); at 40, at index 6; at 35,
   after "Count" (index 5); at 32, after "Coun" (index 4: "Coun" / "t: 59"); at 25 and 30,
   "Cou" / "nt: " / "59" (indices 3 and 7, 47.61 high); at 20, indices 2, 5, 7; at 10, every
   character but one. "X - Close Window" at 60 and 80: one break, before "Window" (index 10).
   The 92-character line at 400: one break, at index 59; at 564 (the console's label width):
   none. The probe's `measure` lines give the breaks at 15 widths for seven texts.
3. **A horizontal group narrower than its entries' minimum widths** gives every entry its
   minimum width and lays them out from its left edge with the usual gaps; the entries run past
   the group's right edge and nothing is shrunk further or clipped by the layout. The group's
   own rect keeps the width it was given; its height is the tallest entry, rounded. In a
   120- or 200-wide area: "X - Close Window" button 113, "Clear" 43, the label "Count: 59" 32
   (two lines, 33.31 high, broken as in rule 2), a `TextField(MinWidth(80))` 80, at x 0, 117,
   164 and 200 (the field ends at 280). Six buttons "button N" are 58 wide each at x 0, 62, …,
   310. The mod's toolbar in a 630- or 400-wide area: 113, 43, 32, 220 (the field's
   `MinWidth`), the toggles 112, 161, 75, 44, 63, 37, the spaces 10, ending at x 956; the row
   reads (0, 0, 630 × 33) or (0, 0, 400 × 33).
4. **With room to spare** the rule of "Layout along the group's axis" holds, the label's range
   being 32 to its `max`: in a 400-wide area the same four entries (minimums 113 + 43 + 32 + 80
   and three gaps of 4, 280 in all) get 137, 67, 79 and 104: the label first grows to 54.72,
   then the remaining 97.28 is shared equally (24.32 each), positions and widths rounded. In a
   1000-wide area the toolbar's entries each get about 2 more than their `max` (close 115,
   label 57 on one line, field 222).
5. **A scroll view with a fixed width** lays its content out at that width: wrapping labels
   take the width that is left and wrap in it, so the content is no wider than the view and the
   returned horizontal position is 0. In a `BeginScrollView(Width(380), Height(150))`, a row of
   the Copy button and a log line gives the button (4, 4, 44 × 24) and the label (52, 4, 324 ×
   24), the line broken at index 51; a label alone is (4, 32, 372 × 33). With eight such rows
   in a view 120 high, the labels are 308 wide (the vertical scrollbar takes 16) and the
   returned position is (0, 108). Without options in a 400 × 300 area: the view is 400 × 300 and
   the label 344 wide.
6. **`Height(h)` on a wrapping label** fixes its height whatever the lines: a log line broken
   in two in a `Height(24)` label is 24 high, and the next row starts 28 lower.
7. **The console window** (`GUI.Window`, 650 × 500): the vertical group is (10, 20, 630 × 470);
   the toolbar row (14, 20, 622 × 33) holds "X - Close Window" (14, 20, 113 × 20), "Clear" (131,
   20, 43 × 20), "Count: 59" (178, 20, 32 × 33.31, broken after "Coun"), the field (214, 20, 220
   × 20) and the toggles from x 438 to 970 (y 20, 17 high); the scroll view (10, 57, 620 × 433);
   in it each row is (4, y, 612 × 24), y = 4, 32, 60, …, with the Copy button (4, y, 44 × 24)
   and the label (52, y, 564 × 24); the lines of 92 to 110 characters wrap once (indices 63 and
   91) and the returned horizontal position is 0. The insets follow "Edges of a group without
   a style" and "with a style" above (the scroll view's margin 0 makes the vertical group's
   left margin 0, so the toolbar is inset by its 4).

**This implementation** (the same probe, `gl-wrap` lines of both players compared line by line):

| Case | original | this implementation |
|---|---|---|
| `CalcMinMaxWidth` min, word wrap | `min(32, max)`: "Count: 59" 32, "X - Close Window" 32, the log lines 32 | the widest word: 37, 45, 130 and 80 |
| text widths (`CalcSize.x`) | fractional: "Count: 59" 54.72, "X - Close Window" 100.55, "Clear" 30.31, a log line 535.33 and 639.7 | whole numbers, and smaller: 54, 99, 30, 535, 632 |
| a word wider than the line | broken inside the word: "Count: 59" at 32 is "Coun" / "t: 59" | moved whole to the next line and left overflowing: at 37, "Count: " / "59"; at 30, "Coun" / "t: 59" only once the word alone is wider |
| a horizontal group with too little room | every entry at its minimum, the rest runs past the edge (labels "label N" at 32 in a 120-wide area) | labels squeezed below their minimum (30 at 200, 27 at 120) |
| the console's toolbar | close 113, Clear 43, "Count: 59" 32 wide on two lines | 111, 42, 37 |

The rects of the scroll view and its rows, the window insets (toolbar at (14, 20)) and the row
heights agree. In the mod's own console the log holds lines with long unbroken words (paths and
long identifiers), so this implementation's minimum of the widest word makes the scroll
content wider than its view: lines no longer wrap and a horizontal scrollbar appears, where the
original's minimum of 32 keeps the content at the view's width. The toolbar's 1-pixel offset
seen in the e2e shot is not in the layout (the rects agree); it was not measured further.

**Requirement:** rules 1 to 3 and 5 above: a word-wrapping style's minimum width is the smaller
of 32 and its full width; wrapping breaks inside a word that is wider than the line; a
horizontal group never gives an entry less than its minimum; text widths are not rounded to
whole pixels. Check them by diffing the two players' `GL wrap` lines (the `measure` lines'
`mm=`, `calc=` and breaks, then every rect) and with the ModTools spec.

#### Where this implementation deviates

From the same probe. In a full run the size part stopped at once on a missing member
(`Font.lineHeight`), so it was run again on its own (`PROBE_GL_PART=size`). `gl-check.py
rule` fails 1,468 of 3,374 checks of the other parts and 183 of 194 size checks; `diff` finds
989 differences in the other parts and 145 size and style lines:

| Case | original | this implementation |
|---|---|---|
| a Button's handler adds a scroll view (the mod's SAVE) | the rest of the event gets (0, 0, 1 × 1); no error | `ArgumentException: GUILayout mismatch: entry 3 was asked for in a group of 9 entries during Used` from `BeginScrollView`; logged, window function aborted |
| any mismatch while the type is `Used` | nothing checked; every call (0, 0, 1 × 1) | real rects, and the mismatch rules of the other events, with its own exception |
| more calls than entries, MouseDown/MouseUp | the last entry's rect again | `ArgumentException` ("GUILayout mismatch: entry N was asked for in a group of N entries during MouseDown. ...") |
| more calls than entries, Repaint | `ArgumentException`, "Getting control N's position in a group with only N controls when doing repaint\nAborting" | `ArgumentException` with the message above |
| a leaf call where a group was recorded | the group's rect | `ArgumentException` ("GUILayout mismatch: entry 2 ...") |
| a Begin call where a leaf was recorded | `ExitGUIException` "GUILayout: Mismatched LayoutGroup.repaint", not logged | `ArgumentException`, logged |
| `BeginHorizontal` where a vertical group was recorded | takes it, laid out as recorded | `ArgumentException` |
| `BeginArea` with no area recorded | `ArgumentException` ("control 0 ... only 0") | `ArgumentException` ("entry 2 ... of 3"): the area counts as an entry of the parent |
| `GetLastRect` after a Begin call | logs an error, (0, 0, 1 × 1) | a rect (the enclosing area's 400 × 400 or 400 × 200 here), no error |
| `Font.lineHeight` | 14 | `MissingMethodException` (no member) |
| a style's `lineHeight` | 14 | 14.94873 |
| one line of text at size 13; two; three | 13.01; 27.31; 41.61 (`s + 0.01 + 1.1 s` per line) | 14.95; 29.90; 44.85 |
| a label's height in a vertical group | `round(CalcHeight)`: 19, 33, 48 | 20.94873, 35.89746, 50.84619 (unrounded) |
| a button's, toggle's, box's height | `ceil(CalcSize.y)`: 20, 17, 22 | 20.94873, 17.94873, 22.94873 |
| label "x" width with `ExpandWidth(false)` | 7 (`ceil` of 6.19) | 7 |
| positions along a group | rounded per entry | unrounded (y 28.94873, 53.89746, …) |
| first label in an area at (20, 20), no style | (0, 0, 400 × 19) | (4, 4, 392 × 20.95): margins kept at the edges |
| the OnGUI root | from (0, 0), as wide as its widest entry (42, or 300) | from (4, 4), 1272 wide (the screen less margins) |
| a group or area with a style (box) | children at max(padding, margin): (4, 4) | padding + margin: (8, 8); a vertical box group (12, 12) |
| a group without a style | margins passed out to the parent (`grp.vertical.mixed`: group at (0, 23, 400 × 42)) | group at (0, 28.95, 400 × 49.9), children inset by their own margins |
| `Space(10)` between labels | rect (-4, 19, 0 × 10); next label at 33 | (0, 28.95, 0 × 10); next at 42.95 |
| scroll view without options, two labels, 400 × 400 area | 400 × 377 | 400 × 371.05 |
| scroll content with a vertical scrollbar | 376 wide (bar 15 + 1) | 372 wide |
| window content (`GUILayout.Window` and `GUI.Window`) | first label (10, 20), 280 wide | (14, 24), 272 wide |
| `GUILayout.Window` during Layout | the rect passed in | the laid-out rect |
| `GUILayout.Window` 300 × 300 with two labels | 300 × 300 (given is the minimum) | 300 × 83.9 (shrinks to content) |
| `GUILayout.Window` with `Height(100)` or `MaxHeight(100)`, content 371 | 371.15 (content wins) | 100 |
| order of calls (two behaviours with windows) | `gui`, `gui2`, then all windows, each event (part 4) | `gui`, its windows, then `gui2`, its windows, per event; Layout and Repaint both in declaration order |
| a new window | in front; several new together: the first declared frontmost | drawn in declaration order, so the last declared is in front (402, 401, 403 at first: 403 in front) |
| `GUI.FocusWindow(401)` | no change of order | brings 401 to the front |
| mouse events | front window under the mouse, then `gui`, `gui2` | `gui2` first, then `gui`'s window under the mouse, then `gui` unless the window used it; MouseMove events are sent |
| a press on a window's empty area | the behaviours get `MouseDown` | the behaviour gets nothing |

The mod's failure is the first row; its DEL offset comes from the label, button and margin
rows (each row about 2 pixels taller, and the window content inset 4 more). The rows on
order and windows were probed with `GUI.Window`; `GUI.ModalWindow`, `GUI.depth`, nested
`GUILayout.BeginArea` inside scroll views, keyboard events and `GUILayout.Toolbar` were not
covered.

## Tests

`tests/OniMoono.UnityEngine.Tests/IMGUITests.cs` already pins the rules marked "Test" above.
Keep every assertion. You may change the test setup lines that name engine internals, such as
`GUIManager.Install()`, `GUIManager.Reset()`, `GUIStateData.Reset()` and `Event.Enqueue`, to
match your design. Add tests for these rules:
- the end-of-frame queue drop with `PopEvent`;
- `useGUILayout = false`;
- `ExitGUI`;
- Tab focus;
- scroll views;
- `GUILayout.Window` sizing;
- `GetStyle` fallbacks;
- `Event.KeyboardEvent`;
- `GUI.tooltip`, rules 1 to 5 of its section, with the mouse placed through the event queue.
- "GUILayout, measured": the mismatch rules 1 to 5 (Repaint, MouseDown and Used), the entry
  heights and gaps of part 2 (label 19, button 20, toggle 17, box 22; labels at 0, 23, 46),
  the margin and padding insets, `GUILayout.Window`'s rect (part 3) and the order of calls
  and windows (part 4).
