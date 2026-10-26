namespace UnityEngine.UIElements
{
    /// Navigation events (move, submit, cancel): they bubble up and trickle down.
    public abstract partial class NavigationEventBase<T>
    {
        public EventModifiers modifiers { get; protected set; }

        /// The kind of device the navigation came from.
        internal NavigationDeviceType sourceDevice { get; private set; }

        public bool shiftKey => (modifiers & EventModifiers.Shift) != 0;

        public bool ctrlKey => (modifiers & EventModifiers.Control) != 0;

        public bool commandKey => (modifiers & EventModifiers.Command) != 0;

        public bool altKey => (modifiers & EventModifiers.Alt) != 0;

        /// Command on macOS, Control elsewhere.
        public bool actionKey => OniMoono.Engine.UIEventModifiers.IsMacOS ? commandKey : ctrlKey;

        protected override void Init()
        {
            base.Init();
            SetPropagation(bubblesUp: true, trickleDown: true);
            modifiers = EventModifiers.None;
            sourceDevice = NavigationDeviceType.Unknown;
        }

        public static T GetPooled(EventModifiers modifiers = EventModifiers.None) => GetPooled(NavigationDeviceType.Unknown, modifiers);

        internal static T GetPooled(NavigationDeviceType deviceType, EventModifiers modifiers = EventModifiers.None)
        {
            T e = EventBase<T>.GetPooled();
            e.sourceDevice = deviceType;
            e.modifiers = modifiers;
            return e;
        }
    }

    public partial class NavigationMoveEvent
    {
        /// Move vectors shorter than this give no direction.
        const float DeadZone = 0.6f;

        public Direction direction { get; private set; }

        public Vector2 move { get; private set; }

        protected override void Init()
        {
            base.Init();
            direction = Direction.None;
            move = Vector2.zero;
        }

        /// A move event for moveVector; its direction is the dominant axis' sign, or None inside
        /// the dead zone.
        public static NavigationMoveEvent GetPooled(Vector2 moveVector, EventModifiers modifiers = EventModifiers.None)
            => GetPooled(moveVector, NavigationDeviceType.Unknown, modifiers);

        internal static NavigationMoveEvent GetPooled(Vector2 moveVector, NavigationDeviceType deviceType, EventModifiers modifiers = EventModifiers.None)
        {
            var e = NavigationEventBase<NavigationMoveEvent>.GetPooled(deviceType, modifiers);
            e.move = moveVector;
            e.direction = DirectionOf(moveVector);
            return e;
        }

        /// A move event in a direction, with a zero move vector.
        public static NavigationMoveEvent GetPooled(Direction direction, EventModifiers modifiers = EventModifiers.None)
            => GetPooled(direction, NavigationDeviceType.Unknown, modifiers);

        internal static NavigationMoveEvent GetPooled(Direction direction, NavigationDeviceType deviceType, EventModifiers modifiers = EventModifiers.None)
        {
            var e = NavigationEventBase<NavigationMoveEvent>.GetPooled(deviceType, modifiers);
            e.move = Vector2.zero;
            e.direction = direction;
            return e;
        }

        static Direction DirectionOf(Vector2 v)
        {
            if (v.sqrMagnitude < DeadZone * DeadZone)
                return Direction.None;
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
                return v.x > 0f ? Direction.Right : Direction.Left;
            return v.y > 0f ? Direction.Up : Direction.Down;
        }
    }

    public partial class NavigationSubmitEvent
    {
    }

    public partial class NavigationCancelEvent
    {
    }

    /// Key events: the typed character, the key and the modifiers. They bubble up and trickle
    /// down.
    public abstract partial class KeyboardEventBase<T>
    {
        public EventModifiers modifiers { get; protected set; }

        public char character { get; protected set; }

        public KeyCode keyCode { get; protected set; }

        public bool shiftKey => (modifiers & EventModifiers.Shift) != 0;

        public bool ctrlKey => (modifiers & EventModifiers.Control) != 0;

        public bool commandKey => (modifiers & EventModifiers.Command) != 0;

        public bool altKey => (modifiers & EventModifiers.Alt) != 0;

        /// Command on macOS, Control elsewhere.
        public bool actionKey => OniMoono.Engine.UIEventModifiers.IsMacOS ? commandKey : ctrlKey;

        protected override void Init()
        {
            base.Init();
            SetPropagation(bubblesUp: true, trickleDown: true);
            modifiers = EventModifiers.None;
            character = '\0';
            keyCode = KeyCode.None;
        }

        public static T GetPooled(char c, KeyCode keyCode, EventModifiers modifiers)
        {
            T e = GetPooled();
            e.character = c;
            e.keyCode = keyCode;
            e.modifiers = modifiers;
            return e;
        }
    }

    public partial class KeyDownEvent
    {
    }

    public partial class KeyUpEvent
    {
    }

    internal static partial class KeyboardEventExtensions
    {
        /// Whether an IMGUI key event moves navigation: any Tab key-down, whatever the modifiers.
        internal static bool ShouldSendNavigationMoveEventRuntime(this Event e) => e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab;

        /// Whether a key-down moves navigation: Tab (Shift+Tab goes backwards) without Control,
        /// Alt, Command or the function-key flag. Arrow keys do not.
        internal static bool ShouldSendNavigationMoveEvent(this KeyDownEvent e)
        {
            const EventModifiers excluded = EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command | EventModifiers.FunctionKey;
            return e.keyCode == KeyCode.Tab && (e.modifiers & excluded) == 0;
        }
    }

    /// Focus changes: they trickle down but do not bubble.
    public abstract partial class FocusEventBase<T>
    {
        /// The other element of the change: the one losing focus for a focus event, the one
        /// gaining it for a blur.
        public Focusable relatedTarget { get; protected set; }

        protected override void Init()
        {
            base.Init();
            SetPropagation(bubblesUp: false, trickleDown: true);
            relatedTarget = null;
        }
    }

    public partial class FocusEvent
    {
    }

    public partial class BlurEvent
    {
    }

    /// Mouse events: position (local and panel), movement, buttons and modifiers. They bubble up
    /// and trickle down.
    public abstract partial class MouseEventBase<T>
    {
        public EventModifiers modifiers { get; protected set; }

        public Vector2 mousePosition { get; protected set; }

        public Vector2 localMousePosition { get; protected set; }

        public Vector2 mouseDelta { get; protected set; }

        public int clickCount { get; protected set; }

        public int button { get; protected set; }

        public int pressedButtons { get; protected set; }

        public bool shiftKey => (modifiers & EventModifiers.Shift) != 0;

        public bool ctrlKey => (modifiers & EventModifiers.Control) != 0;

        public bool commandKey => (modifiers & EventModifiers.Command) != 0;

        public bool altKey => (modifiers & EventModifiers.Alt) != 0;

        /// Command on macOS, Control elsewhere.
        public bool actionKey => OniMoono.Engine.UIEventModifiers.IsMacOS ? commandKey : ctrlKey;

        protected override void Init()
        {
            base.Init();
            SetPropagation(bubblesUp: true, trickleDown: true);
            modifiers = EventModifiers.None;
            mousePosition = Vector2.zero;
            localMousePosition = Vector2.zero;
            mouseDelta = Vector2.zero;
            clickCount = 0;
            button = 0;
            pressedButtons = 0;
        }

        /// Takes the mouse-relevant properties of a pointer event.
        internal void CopyFromPointer(IPointerEvent pointer)
        {
            modifiers = pointer.modifiers;
            mousePosition = pointer.position;
            localMousePosition = pointer.localPosition;
            mouseDelta = pointer.deltaPosition;
            clickCount = pointer.clickCount;
            button = pointer.button;
            pressedButtons = pointer.pressedButtons;
        }
    }

    /// A scroll-wheel event: delta is the scroll amount.
    public partial class WheelEvent
    {
        public Vector3 delta { get; private set; }

        protected override void Init()
        {
            base.Init();
            delta = Vector3.zero;
        }

        /// A wheel event scrolling by delta, at the position and with the buttons and modifiers
        /// of pointerEvent (when there is one).
        internal static WheelEvent GetPooled(Vector3 delta, IPointerEvent pointerEvent)
        {
            var e = GetPooled();
            if (pointerEvent != null)
                e.CopyFromPointer(pointerEvent);
            e.delta = delta;
            return e;
        }
    }
}
