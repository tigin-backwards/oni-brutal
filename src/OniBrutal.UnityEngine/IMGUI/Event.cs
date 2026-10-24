using System;
using System.Collections.Generic;
using System.Globalization;
using OniBrutal.Engine.IMGUI;

namespace UnityEngine
{
    /// An IMGUI event: one input occurrence, or a Layout or Repaint pass. The platform layer
    /// queues input events (Enqueue); OnGUI receives them through Event.current, and uGUI's input
    /// modules read the same events with PopEvent.
    public sealed partial class Event
    {
        EventType kind;
        // In GUI space (origin top left, y down) before GUI.matrix and the clip stack; the
        // mousePosition property gives it in the coordinates of the current clip.
        Vector2 screenPosition;
        Vector2 motion;
        int pressedButton;
        int clicks;
        float force;
        KeyCode key;
        char typed;
        EventModifiers flags;
        string commandText;
        int display;

        static Event currentEvent;

        public Event() { }

        public Event(int displayIndex)
        {
            display = displayIndex;
        }

        public Event(Event other)
        {
            if (other == null) throw new ArgumentException("Event to copy from is null.");
            CopyFrom(other);
        }

        /// The event being processed, or null outside OnGUI.
        public static Event current
        {
            get => currentEvent;
            set => currentEvent = value;
        }

        /// While GUI.enabled is false inside OnGUI, input events read as Ignore.
        public EventType type
        {
            get => kind != EventType.Ignore && (Masked && kind != EventType.Used || currentEvent == this && GUIStateData.InGUI && !GUIStateData.Enabled && IsInput(kind)) ? EventType.Ignore : kind;
            set => kind = value;
        }

        /// The event reads as Ignore while rawType keeps its type: how a mouse release outside
        /// the window whose control holds the mouse reaches that window's function.
        internal bool Masked;

        /// The type regardless of GUI.enabled.
        public EventType rawType => kind;

        /// The mouse position in the current clip's coordinates while OnGUI handles this event;
        /// in GUI space otherwise.
        public Vector2 mousePosition
        {
            get => Dispatching ? ClipStack.ScreenToLocal(screenPosition) : screenPosition;
            set => screenPosition = Dispatching ? ClipStack.LocalToScreen(value) : value;
        }

        /// The mouse movement since the previous mouse event.
        public Vector2 delta
        {
            get => Dispatching ? ClipStack.ScreenToLocalVector(motion) : motion;
            set => motion = Dispatching ? ClipStack.LocalToScreenVector(value) : value;
        }

        public int button { get => pressedButton; set => pressedButton = value; }
        public int clickCount { get => clicks; set => clicks = value; }
        public float pressure { get => force; set => force = value; }
        public KeyCode keyCode { get => key; set => key = value; }
        public char character { get => typed; set => typed = value; }
        public EventModifiers modifiers { get => flags; set => flags = value; }
        public string commandName { get => commandText ?? ""; set => commandText = value; }
        public int displayIndex { get => display; set => display = value; }

        public bool shift { get => Has(EventModifiers.Shift); set => Set(EventModifiers.Shift, value); }
        public bool control { get => Has(EventModifiers.Control); set => Set(EventModifiers.Control, value); }
        public bool alt { get => Has(EventModifiers.Alt); set => Set(EventModifiers.Alt, value); }
        public bool command { get => Has(EventModifiers.Command); set => Set(EventModifiers.Command, value); }
        public bool capsLock { get => Has(EventModifiers.CapsLock); set => Set(EventModifiers.CapsLock, value); }
        public bool numeric { get => Has(EventModifiers.Numeric); set => Set(EventModifiers.Numeric, value); }
        public bool functionKey { get => Has(EventModifiers.FunctionKey); set => Set(EventModifiers.FunctionKey, value); }

        public bool isKey => kind is EventType.KeyDown or EventType.KeyUp;

        public bool isMouse => kind is EventType.MouseMove or EventType.MouseDown or EventType.MouseUp or EventType.MouseDrag
            or EventType.ContextClick or EventType.MouseEnterWindow or EventType.MouseLeaveWindow;

        public bool isScrollWheel => kind == EventType.ScrollWheel;

        bool Has(EventModifiers m) => (flags & m) != 0;

        void Set(EventModifiers m, bool on) => flags = on ? flags | m : flags & ~m;

        /// Coordinates are clip-local while OnGUI (or a window function) handles this event.
        bool Dispatching => currentEvent == this && GUIStateData.InGUI;

        /// Events that come from input devices (masked while the GUI is disabled).
        internal static bool IsInput(EventType t) => t is EventType.MouseDown or EventType.MouseUp or EventType.MouseMove or EventType.MouseDrag
            or EventType.KeyDown or EventType.KeyUp or EventType.ScrollWheel or EventType.DragUpdated or EventType.DragPerform
            or EventType.DragExited or EventType.ContextClick or EventType.MouseEnterWindow or EventType.MouseLeaveWindow
            or EventType.TouchDown or EventType.TouchUp or EventType.TouchMove or EventType.TouchEnter or EventType.TouchLeave or EventType.TouchStationary;

        /// Mouse events in the sense of control capture: a control holding the mouse gets them all.
        internal static bool IsPointer(EventType t) => t is EventType.MouseDown or EventType.MouseUp or EventType.MouseMove or EventType.MouseDrag
            or EventType.ContextClick or EventType.ScrollWheel;

        /// The event's type as the control `controlId` should see it: while a control holds the
        /// mouse, mouse events are only for it; while one has keyboard focus, key events are only
        /// for it.
        public EventType GetTypeForControl(int controlId)
        {
            var t = type;
            int hot = GUIStateData.HotControl;
            if (hot != 0 && IsPointer(t) && t != EventType.ScrollWheel && controlId != hot) return EventType.Ignore;
            int keyboard = GUIStateData.KeyboardControl;
            if (keyboard != 0 && (t is EventType.KeyDown or EventType.KeyUp) && controlId != keyboard) return EventType.Ignore;
            return t;
        }

        /// Marks the event as handled: later controls and behaviours see it as Used.
        public void Use()
        {
            if (kind is EventType.Repaint or EventType.Layout)
            {
                Debug.LogError($"Event.Use() is not allowed for {EventTypeNames.Of(kind)} events.");
                return;
            }
            kind = EventType.Used;
        }

        // ----- The input queue -----

        /// Platform input: one event per occurrence, appended to the queue that PopEvent reads
        /// and to the events the frame's OnGUI pass sends.
        internal static void Enqueue(Event e) => InputEvents.Add(e);

        /// End of frame: events nobody popped are dropped.
        internal static void DiscardUnconsumed() => InputEvents.EndFrame();

        /// Copies the oldest queued event into `outEvent` and removes it from the queue.
        public static bool PopEvent(Event outEvent)
        {
            if (outEvent == null) throw new ArgumentNullException(nameof(outEvent));
            var next = InputEvents.Pop();
            if (next == null) return false;
            outEvent.CopyFrom(next);
            return true;
        }

        public static int GetEventCount() => InputEvents.PendingCount;

        internal void CopyFrom(Event other)
        {
            kind = other.kind;
            screenPosition = other.screenPosition;
            motion = other.motion;
            pressedButton = other.pressedButton;
            clicks = other.clicks;
            force = other.force;
            key = other.key;
            typed = other.typed;
            flags = other.flags;
            commandText = other.commandText;
            display = other.display;
            Masked = other.Masked;
        }

        /// Makes this a fresh event of `type` (the dispatcher's Layout and Repaint events).
        internal void Prepare(EventType type, Vector2 position, EventModifiers modifiers)
        {
            kind = type;
            screenPosition = position;
            motion = default;
            pressedButton = 0;
            clicks = 0;
            force = 0f;
            key = KeyCode.None;
            typed = '\0';
            flags = modifiers;
            commandText = null;
            display = 0;
        }

        /// The position in GUI space, whatever clip is current.
        internal Vector2 ScreenPosition => screenPosition;

        /// The mouse movement in GUI space, whatever clip is current.
        internal Vector2 ScreenDelta => motion;

        // ----- Key strings -----

        /// A KeyDown event from Unity's key string syntax: modifier prefixes `^` Control,
        /// `%` Command, `#` Shift, `&` Alt, then a key: a character ("a", "1", "/"), a key name
        /// ("home", "page up", "f5", "escape", "return", "space", "tab"…), or a keypad key in
        /// brackets ("[0]", "[+]", "[enter]").
        public static Event KeyboardEvent(string key)
        {
            var e = new Event { kind = EventType.KeyDown };
            if (string.IsNullOrEmpty(key)) return e;
            int i = 0;
            for (; i < key.Length; i++)
            {
                var m = key[i] switch
                {
                    '^' => EventModifiers.Control,
                    '%' => EventModifiers.Command,
                    '#' => EventModifiers.Shift,
                    '&' => EventModifiers.Alt,
                    _ => EventModifiers.None,
                };
                if (m == EventModifiers.None || i == key.Length - 1) break;
                e.flags |= m;
            }
            var name = key.Substring(i);
            bool keypad = name.Length > 2 && name[0] == '[' && name[^1] == ']';
            if (keypad)
            {
                name = name.Substring(1, name.Length - 2);
                e.flags |= EventModifiers.Numeric;
                if (KeypadKeys.TryGetValue(name.ToLowerInvariant(), out var pad))
                {
                    e.key = pad.Key;
                    e.typed = pad.Character;
                    return e;
                }
                Debug.LogError($"Event.KeyboardEvent: unknown key name '{name}'");
                return e;
            }
            if (name.Length == 1)
            {
                char c = char.ToLowerInvariant(name[0]);
                e.typed = name[0];
                e.key = CharacterKey(c);
                return e;
            }
            var lower = name.ToLowerInvariant();
            if (NamedKeys.TryGetValue(lower, out var named))
            {
                e.key = named.Key;
                e.typed = named.Character;
                if (named.Function) e.flags |= EventModifiers.FunctionKey;
                return e;
            }
            if (lower.Length >= 2 && lower[0] == 'f' && int.TryParse(lower.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int f) && f is >= 1 and <= 15)
            {
                e.key = KeyCode.F1 + (f - 1);
                e.flags |= EventModifiers.FunctionKey;
                return e;
            }
            if (Enum.TryParse(name, true, out KeyCode parsed))
            {
                e.key = parsed;
                return e;
            }
            Debug.LogError($"Event.KeyboardEvent: unknown key name '{name}'");
            return e;
        }

        static KeyCode CharacterKey(char c)
        {
            if (c is >= 'a' and <= 'z') return KeyCode.A + (c - 'a');
            if (c is >= '0' and <= '9') return KeyCode.Alpha0 + (c - '0');
            return c switch
            {
                ' ' => KeyCode.Space,
                '!' => KeyCode.Exclaim,
                '"' => KeyCode.DoubleQuote,
                '#' => KeyCode.Hash,
                '$' => KeyCode.Dollar,
                '&' => KeyCode.Ampersand,
                '\'' => KeyCode.Quote,
                '(' => KeyCode.LeftParen,
                ')' => KeyCode.RightParen,
                '*' => KeyCode.Asterisk,
                '+' => KeyCode.Plus,
                ',' => KeyCode.Comma,
                '-' => KeyCode.Minus,
                '.' => KeyCode.Period,
                '/' => KeyCode.Slash,
                ':' => KeyCode.Colon,
                ';' => KeyCode.Semicolon,
                '<' => KeyCode.Less,
                '=' => KeyCode.Equals,
                '>' => KeyCode.Greater,
                '?' => KeyCode.Question,
                '@' => KeyCode.At,
                '[' => KeyCode.LeftBracket,
                '\\' => KeyCode.Backslash,
                ']' => KeyCode.RightBracket,
                '^' => KeyCode.Caret,
                '_' => KeyCode.Underscore,
                '`' => KeyCode.BackQuote,
                _ => KeyCode.None,
            };
        }

        static readonly Dictionary<string, (KeyCode Key, char Character)> KeypadKeys = new()
        {
            ["0"] = (KeyCode.Keypad0, '0'), ["1"] = (KeyCode.Keypad1, '1'), ["2"] = (KeyCode.Keypad2, '2'),
            ["3"] = (KeyCode.Keypad3, '3'), ["4"] = (KeyCode.Keypad4, '4'), ["5"] = (KeyCode.Keypad5, '5'),
            ["6"] = (KeyCode.Keypad6, '6'), ["7"] = (KeyCode.Keypad7, '7'), ["8"] = (KeyCode.Keypad8, '8'),
            ["9"] = (KeyCode.Keypad9, '9'), ["."] = (KeyCode.KeypadPeriod, '.'), ["/"] = (KeyCode.KeypadDivide, '/'),
            ["*"] = (KeyCode.KeypadMultiply, '*'), ["-"] = (KeyCode.KeypadMinus, '-'), ["+"] = (KeyCode.KeypadPlus, '+'),
            ["="] = (KeyCode.KeypadEquals, '='), ["enter"] = (KeyCode.KeypadEnter, '\n'), ["equals"] = (KeyCode.KeypadEquals, '='),
        };

        static readonly Dictionary<string, (KeyCode Key, char Character, bool Function)> NamedKeys = new()
        {
            ["up"] = (KeyCode.UpArrow, '\0', true),
            ["down"] = (KeyCode.DownArrow, '\0', true),
            ["left"] = (KeyCode.LeftArrow, '\0', true),
            ["right"] = (KeyCode.RightArrow, '\0', true),
            ["insert"] = (KeyCode.Insert, '\0', true),
            ["home"] = (KeyCode.Home, '\0', true),
            ["end"] = (KeyCode.End, '\0', true),
            ["pgup"] = (KeyCode.PageUp, '\0', true),
            ["page up"] = (KeyCode.PageUp, '\0', true),
            ["pgdown"] = (KeyCode.PageDown, '\0', true),
            ["page down"] = (KeyCode.PageDown, '\0', true),
            ["backspace"] = (KeyCode.Backspace, '\0', true),
            ["delete"] = (KeyCode.Delete, '\0', true),
            ["tab"] = (KeyCode.Tab, '\0', false),
            ["escape"] = (KeyCode.Escape, '\0', false),
            ["esc"] = (KeyCode.Escape, '\0', false),
            ["return"] = (KeyCode.Return, '\0', false),
            ["enter"] = (KeyCode.Return, '\0', false),
            ["space"] = (KeyCode.Space, ' ', false),
        };

        // ----- Comparison and display -----

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj is not Event other) return false;
            if (kind != other.kind || flags != other.flags) return false;
            if (isKey) return key == other.key;
            if (isMouse) return screenPosition == other.screenPosition;
            return true;
        }

        public override int GetHashCode()
        {
            int hash = (int)flags;
            if (isKey) hash |= (int)key << 8;
            if (isMouse) hash ^= screenPosition.GetHashCode() << 8;
            return hash;
        }

        public override string ToString()
        {
            var name = EventTypeNames.Of(kind);
            if (isKey) return $"Event {name}: key {key}, character {(typed == '\0' ? "none" : $"'{typed}' ({(int)typed})")}, modifiers {flags}";
            if (isMouse) return $"Event {name}: position {screenPosition}, button {pressedButton}, modifiers {flags}";
            if (kind is EventType.ExecuteCommand or EventType.ValidateCommand) return $"Event {name}: command \"{commandName}\"";
            return "Event " + name;
        }
    }

    /// EventType has lower-case aliases with the same values, so Enum.ToString may pick either;
    /// messages name the documented upper-case member.
    internal static class EventTypeNames
    {
        public static string Of(EventType t) => t switch
        {
            EventType.MouseDown => "MouseDown",
            EventType.MouseUp => "MouseUp",
            EventType.MouseMove => "MouseMove",
            EventType.MouseDrag => "MouseDrag",
            EventType.KeyDown => "KeyDown",
            EventType.KeyUp => "KeyUp",
            EventType.ScrollWheel => "ScrollWheel",
            EventType.Repaint => "Repaint",
            EventType.Layout => "Layout",
            EventType.DragUpdated => "DragUpdated",
            EventType.DragPerform => "DragPerform",
            EventType.DragExited => "DragExited",
            EventType.Ignore => "Ignore",
            EventType.Used => "Used",
            EventType.ValidateCommand => "ValidateCommand",
            EventType.ExecuteCommand => "ExecuteCommand",
            EventType.ContextClick => "ContextClick",
            EventType.MouseEnterWindow => "MouseEnterWindow",
            EventType.MouseLeaveWindow => "MouseLeaveWindow",
            _ => ((int)t).ToString(CultureInfo.InvariantCulture),
        };
    }
}
