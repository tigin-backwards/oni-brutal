# IMGUI: functional specification

Unity's immediate-mode GUI: `MonoBehaviour.OnGUI`, `Event`, `GUI`, `GUILayout`,
`GUILayoutUtility`, `GUIUtility`, `GUIContent`, `GUIStyle`, `GUIStyleState`, `GUISkin`,
`GUISettings`, `TextEditor`, and the internal `GUIClip`. The code lives in
`src/OniBrutal.UnityEngine/IMGUI/`, with engine-side services in the namespace
`OniBrutal.Engine.IMGUI`.

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
- `OniBrutal.Engine.IMGUI.GUIManager.Install()`: the host calls it once at startup
  (`src/OniBrutal.Host/Player.cs`).
- `GUIManager.Register(BehaviourNative)` and `Unregister(BehaviourNative)`: called when a
  behaviour becomes enabled or disabled (`Engine/BehaviourManager.cs`).
  - A behaviour takes part in IMGUI when its script declares an `OnGUI` method, of any
    visibility.
  - `BehaviourNative.Script.OnGUI` calls the method.
- `UnityEngine.Event.Enqueue(Event)` (internal): the platform layer queues one event per input
  occurrence (`src/OniBrutal.Desktop/GlfwPlatform.cs`, `InputStateFeed`).
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

These rules are pinned by tests in `tests/OniBrutal.UnityEngine.Tests/IMGUITests.cs`.

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
- **Focus:** a `MouseDown` inside a window focuses it, brings it to the front, and is used.
- **Lifetime:** a window that its behaviour did not call during a frame's Layout is gone.
- **Return value:** the window's rect, moved by dragging since the last call; otherwise the
  rect passed in.
- **State:** the colours, enabled state and matrix in effect at the call apply inside the
  window. Each window has its own control IDs.
- **Test:** the call order for one frame is behaviour Layout, window Layout, behaviour Repaint,
  window Repaint.
- **`GUILayout.Window`** sizes the window to fit its laid-out content, within the rect's size
  and its options.

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
- A mismatch is an error, as Unity's documentation describes for changing the control count
  between Layout and Repaint.

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
- `CalcMinMaxWidth(content, out min, out max)`: `max` is `CalcSize(content).x`; `min` is the
  widest word when wrapping.
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
   and event, with its windows right after it). Keep that order unless a mod needs this one;
   the rules above hold within any order of calls.
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

## Tests

`tests/OniBrutal.UnityEngine.Tests/IMGUITests.cs` already pins the rules marked "Test" above.
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
