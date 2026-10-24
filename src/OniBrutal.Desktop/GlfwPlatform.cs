using System.Linq;
using System;
using System.Collections.Generic;
using Brutal.GlfwApi;
using Brutal.Numerics;
using OniBrutal.Engine.Platform;
using UnityEngine;
using static Brutal.GlfwApi.Glfw.Constants;

namespace OniBrutal.Desktop
{
    /// Window startup settings: Unity's player defaults from PlayerSettings, overridable with
    /// Unity's standard command-line arguments (-screen-width, -screen-height,
    /// -screen-fullscreen, -window-mode, -monitor).
    internal sealed record WindowSettings(string Title, int Width, int Height, FullScreenMode Mode)
    {
        /// Development runs driven by scripted input ignore the window's own mouse and keyboard.
        public bool IgnoreUserInput { get; init; }
        /// Diagnostics (--gl-debug): a debug context, whose driver reports errors and
        /// performance problems through GL debug output.
        public bool DebugContext { get; init; }
        /// -monitor N: the monitor to open on, 1-based as in Unity (0: the primary one).
        public int Monitor { get; init; }
    }

    /// The player window (GLFW, OpenGL 4.5 core context) and its input devices.
    internal sealed unsafe class GlfwPlatform : IPlatform, IDisposable
    {
        readonly GlfwWindow window, renderWindow;
        readonly InputStateFeed feed = new();
        FullScreenMode mode;
        int2 windowedPosition, windowedSize;
        bool cursorVisible = true;
        CursorLockMode cursorLock;
        GlfwCursor customCursor;
        bool closeRequested;

        public GlfwWindow Window => window;
        /// The hidden window whose context the main thread renders with.
        public GlfwWindow RenderWindow => renderWindow;

        readonly bool scripted;

        const int GLFW_ANY_PLATFORM = 0x00060000;
        // GLFW 3.4's string hint, which the binding's GlfwWindowHint does not name.
        const GlfwWindowHint GLFW_WAYLAND_APP_ID = (GlfwWindowHint)0x00026001;
        /// The original Linux player's executable name, its X11 WM_CLASS.
        const string ApplicationId = "OxygenNotIncluded";

        public GlfwPlatform(WindowSettings settings)
        {
            Glfw.ErrorCallback += (code, description) => Debug.LogWarning($"GLFW {code}: {description}");
            // Unity's Linux player is an X11 client (XWayland under Wayland compositors), where
            // window positions exist; prefer GLFW's X11 backend, else whatever GLFW finds.
            bool initialized = false;
            if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("DISPLAY") is { Length: > 0 }
                && Glfw.IsPlatformSupported(Brutal.GlfwApi.GlfwPlatform.X11))
            {
                Glfw.SetInitHint(GlfwInitHint.Platform, (int)Brutal.GlfwApi.GlfwPlatform.X11);
                initialized = Glfw.Init();
                if (!initialized) Glfw.SetInitHint(GlfwInitHint.Platform, GLFW_ANY_PLATFORM);
            }
            if (!initialized && !Glfw.Init()) throw new InvalidOperationException("GLFW could not initialize (no display available).");
            Glfw.DefaultWindowHints();
            Glfw.WindowHint(GlfwWindowHint.ClientApi, GLFW_OPENGL_API);
            Glfw.WindowHint(GlfwWindowHint.ContextVersionMajor, 4);
            Glfw.WindowHint(GlfwWindowHint.ContextVersionMinor, 5);
            Glfw.WindowHint(GlfwWindowHint.OpenGlProfile, GLFW_OPENGL_CORE_PROFILE);
            Glfw.WindowHint(GlfwWindowHint.OpenGlForwardCompat, GLFW_TRUE);
            Glfw.WindowHint(GlfwWindowHint.OpenGlDebugContext, settings.DebugContext ? GLFW_TRUE : GLFW_FALSE);
            Glfw.WindowHint(GlfwWindowHint.DepthBits, 24);
            Glfw.WindowHint(GlfwWindowHint.StencilBits, 8);
            Glfw.WindowHint(GlfwWindowHint.Doublebuffer, GLFW_TRUE);
            Glfw.WindowHint(GlfwWindowHint.Visible, GLFW_FALSE);
            // Scripted runs take no input from the desktop and do not take its focus either.
            Glfw.WindowHint(GlfwWindowHint.FocusOnShow, settings.IgnoreUserInput ? GLFW_FALSE : GLFW_TRUE);
            // The window identifies itself as the original's does, so desktops group it with,
            // and take its icon from, the same launchers. Unity's Linux player names the X11
            // class and instance after its executable (WM_CLASS "OxygenNotIncluded",
            // "OxygenNotIncluded" as shipped); GLFW would use the title. Unity's Wayland app ID
            // is unmeasured here, so ours takes the X11 class, the name a .desktop entry's
            // StartupWMClass already matches.
            Glfw.WindowHint(GlfwWindowHint.X11ClassName, ApplicationId);
            Glfw.WindowHint(GlfwWindowHint.X11InstanceName, ApplicationId);
            Glfw.WindowHint(GLFW_WAYLAND_APP_ID, ApplicationId);

            var monitor = Glfw.PrimaryMonitor;
            var monitors = Glfw.GetMonitors();
            if (settings.Monitor >= 1 && settings.Monitor <= monitors.Length) monitor = monitors[settings.Monitor - 1];
            var desktop = monitor.VideoMode;
            mode = Supported(settings.Mode);
            int width = settings.Width > 0 ? settings.Width : desktop.Width;
            int height = settings.Height > 0 ? settings.Height : desktop.Height;
            if (mode is FullScreenMode.FullScreenWindow) Glfw.WindowHint(GlfwWindowHint.Decorated, GLFW_FALSE);
            window = Glfw.CreateWindow(new GlfwWindow.CreateInfo
            {
                Title = settings.Title,
                Size = mode == FullScreenMode.FullScreenWindow ? new int2(desktop.Width, desktop.Height) : new int2(width, height),
                Monitor = mode == FullScreenMode.ExclusiveFullScreen ? monitor : GlfwMonitor.Null,
            });
            windowedSize = new int2(width, height);
            if (mode == FullScreenMode.FullScreenWindow) window.Position = monitor.Position;
            else if (mode != FullScreenMode.ExclusiveFullScreen) window.CenterToMonitor(monitor);
            windowedPosition = window.Position;
            if (settings.Monitor > 0)
                OniBrutal.Engine.EngineLog.Engine($"window: monitor {settings.Monitor} of {monitors.Length} at {monitor.Position.X},{monitor.Position.Y}; window at {window.Position.X},{window.Position.Y}");
            SetIcon(window);
            if (mode == FullScreenMode.MaximizedWindow) window.Maximize();
            // Rendering happens on this thread in a hidden window's context shared with the
            // visible one, whose context belongs to the presenter thread (GlPresenter).
            renderWindow = Glfw.CreateWindow(new GlfwWindow.CreateInfo
            {
                Title = settings.Title,
                Size = new int2(1, 1),
                Monitor = GlfwMonitor.Null,
                Share = window,
            });
            renderWindow.MakeContextCurrent();
            window.Visible = true;
            if (!settings.IgnoreUserInput) window.Focus();

            // Scripted runs (host --input) take input only from the script.
            scripted = settings.IgnoreUserInput;
            if (scripted) InputState.Instance.MousePresent = true;
            else
            {
                window.OnKey += (w, key, scancode, action, mods) => Report(() => OnKey(w, key, scancode, action, mods));
                window.OnChar += (_, codepoint) => Report(() => feed.Text((uint)codepoint, Modifiers()));
                window.OnMouseButton += (w, button, action, mods) => Report(() => OnMouseButton(w, button, action, mods));
                window.OnCursorPos += (_, pos) => Report(() => feed.MouseMoved(ToPixels(pos)));
                window.OnScroll += (_, offset) => Report(() => feed.Scrolled(new Vector2((float)offset.X, (float)offset.Y), MouseGuiPosition()));
                window.OnCursorEnter += (_, entered) => Report(() => InputState.Instance.MousePresent = entered);
            }
            window.OnClose += _ => Report(() => closeRequested = true);
            window.OnFramebufferSize += (_, size) => Report(() => framebufferSize = window.FramebufferSize);
            Glfw.MonitorCallback += (_, _) => Report(RefreshWindowState);
            // The window may have moved to another monitor.
            window.OnPosition += (_, _) => Report(RefreshWindowState);
            RefreshWindowState();
            InputState.Instance.MousePresent = true;
            // Unity's player starts with the pointer where it is (the game's hover card shows for
            // what is under it before the mouse first moves); GLFW reports only movements.
            if (!scripted) feed.MouseMoved(ToPixels(window.CursorPos));
        }

        // ----- IPlatform -----

        public bool HasDisplay => true;
        // Window and monitor state is cached: Screen/Display properties are read many times per
        // frame and each GLFW query is a round trip to the display server.
        int2 framebufferSize;
        OniBrutal.Engine.Platform.DisplayMode? desktopMode;
        float? dpi;

        void RefreshWindowState()
        {
            framebufferSize = window.FramebufferSize;
            desktopMode = null;
            dpi = null;
        }

        public int FramebufferWidth => framebufferSize.X;
        public int FramebufferHeight => framebufferSize.Y;
        public FullScreenMode FullScreenMode => mode;
        /// Scripted runs behave as a focused window with the mouse over it, whatever the desktop
        /// is doing.
        public bool Focused => scripted || window.IsFocused;

        /// Monitor DPI from its physical size (0 when the monitor does not report one, as Unity).
        public float Dpi => dpi ??= MonitorDpi(CurrentMonitor);

        /// The window and taskbar icon (Steela, embedded). Wayland has no window icons: the
        /// compositor takes them from the desktop entry, and GLFW reports an error if asked.
        static unsafe void SetIcon(GlfwWindow window)
        {
            if (Glfw.GetPlatform() == Brutal.GlfwApi.GlfwPlatform.Wayland) return;
            using var stream = typeof(GlfwPlatform).Assembly.GetManifestResourceStream("Steela.png");
            var data = new byte[stream.Length];
            stream.ReadExactly(data);
            using var image = Brutal.StbApi.Stb.LoadFromMemory(data, 4);
            fixed (byte* pixels = image.AsSpan())
            {
                var icon = new GlfwImage { Width = image.Width, Height = image.Height, Pixels = pixels };
                Glfw.PInvoke.SetWindowIcon(window, 1, &icon);
            }
        }

        static float MonitorDpi(GlfwMonitor m)
        {
            var mm = m.PhysicalSize;
            return mm.X <= 0 ? 0f : m.Size.X / (mm.X / 25.4f);
        }

        GlfwMonitor CurrentMonitor
        {
            get
            {
                var m = window.Monitor;
                return m.IsNull() ? Glfw.PrimaryMonitor : m;
            }
        }

        static OniBrutal.Engine.Platform.DisplayMode ModeOf(GlfwVidMode m) => new(m.Width, m.Height, (uint)m.RefreshRate, 1);

        public OniBrutal.Engine.Platform.DisplayMode DesktopMode => desktopMode ??= ModeOf(CurrentMonitor.VideoMode);

        public OniBrutal.Engine.Platform.DisplayMode[] DisplayModes
        {
            get
            {
                var list = new List<OniBrutal.Engine.Platform.DisplayMode>();
                foreach (var m in CurrentMonitor.VideoModes)
                {
                    var mode = ModeOf(m);
                    if (!list.Contains(mode)) list.Add(mode);
                }
                if (list.Count > 1)
                    return list.OrderBy(m => m.Width).ThenBy(m => m.Height).ThenBy(m => (double)m.RefreshNumerator / Math.Max(1u, m.RefreshDenominator)).ToArray();
                // A display that reports only its current mode (virtual displays such as Xvfb or
                // WSLg): Unity's Linux player then offers its table of standard resolutions.
                var standard = StandardModes(DesktopMode);
                return standard.Length > 0 ? standard : list.ToArray();
            }
        }

        /// Unity's standard resolutions that fit the desktop, at 60 Hz, in its order (measured:
        /// Screen.resolutions in the original player on a 1280x1024 Xvfb screen, PerfProbe
        /// measure.txt).
        internal static OniBrutal.Engine.Platform.DisplayMode[] StandardModes(OniBrutal.Engine.Platform.DisplayMode desktop)
        {
            var standard = new List<OniBrutal.Engine.Platform.DisplayMode>();
            foreach (var (w, h) in StandardResolutions)
                if (w <= desktop.Width && h <= desktop.Height) standard.Add(new(w, h, 60, 1));
            return standard.ToArray();
        }

        static readonly (int Width, int Height)[] StandardResolutions =
        [
            (640, 480), (720, 480), (768, 480), (800, 480), (854, 480), (960, 540), (1024, 576), (800, 600),
            (1024, 600), (960, 640), (1024, 640), (1136, 640),
            (960, 720), (1152, 720), (1280, 720), (1024, 768), (1152, 768), (1280, 768), (1366, 768), (1280, 800),
            (1152, 864), (1280, 864), (1440, 900), (1600, 900), (1280, 960), (1440, 960), (1280, 1024), (1600, 1024),
            (1400, 1050), (1680, 1050), (1920, 1080), (2560, 1080), (2048, 1152), (1600, 1200), (1920, 1200),
            (1792, 1344), (1856, 1392), (1920, 1440), (2160, 1440), (2304, 1440), (2560, 1440), (3440, 1440),
            (2048, 1536), (2560, 1600), (2880, 1800), (3200, 1800), (2560, 1920), (2560, 2048), (3200, 2048), (3840, 2160),
        ];

        /// Unity supports exclusive fullscreen only in its Windows player; elsewhere a request for
        /// it gives a fullscreen window (as the Linux player reports: FullScreenWindow).
        static FullScreenMode Supported(FullScreenMode mode)
            => mode == FullScreenMode.ExclusiveFullScreen && !OperatingSystem.IsWindows() ? FullScreenMode.FullScreenWindow : mode;

        public void SetResolution(int width, int height, FullScreenMode newMode, uint refreshNumerator, uint refreshDenominator)
        {
            newMode = Supported(newMode);
            var monitor = CurrentMonitor;
            var desktop = monitor.VideoMode;
            if (mode is FullScreenMode.Windowed or FullScreenMode.MaximizedWindow)
            {
                windowedPosition = window.Position;
                windowedSize = window.Size;
            }
            switch (newMode)
            {
                case FullScreenMode.ExclusiveFullScreen:
                {
                    int refresh = refreshDenominator == 0 || refreshNumerator == 0 ? desktop.RefreshRate : (int)Math.Round((double)refreshNumerator / refreshDenominator);
                    window.SetFullscreen(monitor, new int2(width, height), refresh);
                    break;
                }
                case FullScreenMode.FullScreenWindow:
                    window.SetAttribute(GlfwWindowAttribute.Decorated, GLFW_FALSE);
                    window.SetWindowed(monitor.Position, new int2(desktop.Width, desktop.Height));
                    break;
                default:
                    window.SetAttribute(GlfwWindowAttribute.Decorated, GLFW_TRUE);
                    if (mode is FullScreenMode.ExclusiveFullScreen or FullScreenMode.FullScreenWindow)
                        window.SetWindowed(windowedPosition, new int2(width, height));
                    else window.Size = new int2(width, height);
                    if (newMode == FullScreenMode.MaximizedWindow) window.Maximize();
                    break;
            }
            mode = newMode;
            window.Refresh();
            RefreshWindowState();
        }

        /// Inside PumpEvents: what the callbacks report waits for the next PollEvents.
        bool pumping;
        readonly List<Action> pumped = new();

        // Inside a GLFW callback, or the frame's own poll: GLFW must not be polled from a
        // callback, and what a callback runs may log, which pumps (MainThreadPump).
        int inGlfw;

        void Report(Action report)
        {
            if (pumping) pumped.Add(report);
            else
            {
                inGlfw++;
                try { report(); }
                finally { inGlfw--; }
            }
        }

        public void PumpEvents()
        {
            if (pumping || inGlfw > 0) return;
            pumping = true;
            try
            {
                Glfw.PollEvents();
            }
            finally
            {
                pumping = false;
            }
        }

        public bool PollEvents(InputState input)
        {
            // What came in while the last frame pumped messages, in the order it came, then the
            // messages since.
            inGlfw++;
            try
            {
                if (pumped.Count > 0)
                {
                    var reports = pumped.ToArray();
                    pumped.Clear();
                    foreach (var report in reports) report();
                }
                Glfw.PollEvents();
            }
            finally
            {
                inGlfw--;
            }
            framebufferSize = window.FramebufferSize;
            if (!scripted) FollowCursorOutside();
            return !closeRequested;
        }

        /// Unity's Input.mousePosition follows the pointer outside the window too, relative to the
        /// window (measured in the original player: (1400, -133) for a pointer beside a 1024x768
        /// window at the origin, then following it as it moves); GLFW reports movements only over
        /// the window, so outside it the position is read each frame. No GUI events come from it.
        void FollowCursorOutside()
        {
            // The window's CursorPos is the last position its callbacks reported: asked of GLFW.
            Glfw.PInvoke.GetCursorPos(window, out double x, out double y);
            var size = window.Size;
            if (x >= 0 && y >= 0 && x < size.X && y < size.Y) return;
            var pixels = ToPixels(new double2(x, y));
            if (pixels != (Vector2)InputState.Instance.MousePosition) InputState.Instance.MouseMoved(pixels);
        }

        public bool CursorVisible
        {
            get => cursorVisible;
            set
            {
                cursorVisible = value;
                ApplyCursorMode();
            }
        }

        public CursorLockMode CursorLockState
        {
            get => cursorLock;
            set
            {
                cursorLock = value;
                ApplyCursorMode();
            }
        }

        /// Locked: hidden and kept centred (GLFW disabled mode); Confined: kept inside the window.
        void ApplyCursorMode()
        {
            var glfwMode = cursorLock switch
            {
                CursorLockMode.Locked => GlfwCursorMode.Disabled,
                CursorLockMode.Confined => cursorVisible ? GlfwCursorMode.Captured : GlfwCursorMode.Hidden,
                _ => cursorVisible ? GlfwCursorMode.Normal : GlfwCursorMode.Hidden,
            };
            window.SetInputMode(GlfwInputMode.Cursor, (int)glfwMode);
        }

        /// Texture pixels run bottom-up; cursor images top-down.
        public void SetCursorImage(Color32[] pixels, int width, int height, Vector2 hotspot)
        {
            var rgba = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var c = pixels[(height - 1 - y) * width + x];
                    int o = (y * width + x) * 4;
                    rgba[o] = c.r;
                    rgba[o + 1] = c.g;
                    rgba[o + 2] = c.b;
                    rgba[o + 3] = c.a;
                }
            fixed (byte* p = rgba)
            {
                var cursor = Glfw.CreateCursor(new GlfwCursor.CreateInfo
                {
                    Image = new GlfwImage { Width = width, Height = height, Pixels = p },
                    Hot = new int2((int)hotspot.x, (int)hotspot.y),
                });
                window.SetCursor(cursor);
                if (!customCursor.IsNull()) customCursor.Dispose();
                customCursor = cursor;
            }
        }

        public void SetDefaultCursor()
        {
            window.SetCursor(default);
            if (!customCursor.IsNull()) customCursor.Dispose();
            customCursor = default;
        }

        public string Clipboard
        {
            get => Glfw.Clipboard ?? "";
            set => Glfw.SetClipboard(value ?? "");
        }

        /// Runs first when the platform is disposed, while the windows still exist.
        public event Action Closing;

        public void Dispose()
        {
            Closing?.Invoke();
            if (!customCursor.IsNull()) customCursor.Dispose();
            renderWindow.Dispose();
            window.Dispose();
            Glfw.Terminate();
        }

        // ----- Input -----

        /// GLFW reports the cursor in window coordinates from the top left; Unity in framebuffer
        /// pixels from the bottom left.
        Vector2 ToPixels(double2 pos)
        {
            var size = window.Size;
            var fb = window.FramebufferSize;
            float sx = size.X == 0 ? 1f : (float)fb.X / size.X, sy = size.Y == 0 ? 1f : (float)fb.Y / size.Y;
            return PointerPixels.Screen(new Vector2((float)pos.X * sx, (float)pos.Y * sy), fb.Y);
        }

        Vector2 MouseGuiPosition()
        {
            return PointerPixels.Gui(InputState.Instance.MousePosition, FramebufferHeight);
        }

        EventModifiers Modifiers()
        {
            EventModifiers m = 0;
            bool Down(GlfwKey k) => window.GetKey(k) == GlfwKeyAction.Press;
            if (Down(GlfwKey.LeftShift) || Down(GlfwKey.RightShift)) m |= EventModifiers.Shift;
            if (Down(GlfwKey.LeftControl) || Down(GlfwKey.RightControl)) m |= EventModifiers.Control;
            if (Down(GlfwKey.LeftAlt) || Down(GlfwKey.RightAlt)) m |= EventModifiers.Alt;
            if (Down(GlfwKey.LeftSuper) || Down(GlfwKey.RightSuper)) m |= EventModifiers.Command;
            return m;
        }

        void OnKey(GlfwWindow w, GlfwKey key, int scancode, GlfwKeyAction action, GlfwModifier mods)
        {
            var code = KeyMap.ToUnity(key);
            if (code == KeyCode.None) return;
            var modifiers = Modifiers();
            if ((mods & GlfwModifier.CapsLock) != 0) modifiers |= EventModifiers.CapsLock;
            if (KeyMap.IsKeypad(code)) modifiers |= EventModifiers.Numeric;
            if (KeyMap.IsFunction(code)) modifiers |= EventModifiers.FunctionKey;
            switch (action)
            {
                case GlfwKeyAction.Press:
                    feed.KeyDown(code, modifiers, repeat: false, MouseGuiPosition());
                    break;
                case GlfwKeyAction.Repeat:
                    feed.KeyDown(code, modifiers, repeat: true, MouseGuiPosition());
                    break;
                case GlfwKeyAction.Release:
                    feed.KeyUp(code, modifiers, MouseGuiPosition());
                    break;
            }
        }

        void OnMouseButton(GlfwWindow w, GlfwMouseButton button, GlfwButtonAction action, GlfwModifier mods)
        {
            int index = (int)button;
            if (index > 6) return;
            if (action == GlfwButtonAction.Press) feed.MouseDown(index, Modifiers(), MouseGuiPosition());
            else feed.MouseUp(index, Modifiers(), MouseGuiPosition());
        }
    }

    /// Turns platform input into Unity's input state and IMGUI event stream. Unity sends a key
    /// press as a KeyDown event with the key code, and the character it types as a second
    /// KeyDown event with KeyCode.None (Return and Tab type '\n' and '\t').
    internal sealed class InputStateFeed
    {
        static InputState State => InputState.Instance;
        int clickCount;
        float lastClickTime = -1f;
        int lastClickButton = -1;
        Vector2 dragStart;
        readonly bool[] buttonsDown = new bool[7];

        static void Queue(EventType type, EventModifiers modifiers, Vector2 guiPosition, Action<Event> fill = null)
        {
            var e = new Event { type = type, modifiers = modifiers, mousePosition = guiPosition };
            fill?.Invoke(e);
            Event.Enqueue(e);
        }

        public void KeyDown(KeyCode code, EventModifiers modifiers, bool repeat, Vector2 guiPosition)
        {
            if (!repeat) State.KeyPressed(code);
            Queue(EventType.KeyDown, modifiers, guiPosition, e => e.keyCode = code);
            char typed = code switch
            {
                KeyCode.Return or KeyCode.KeypadEnter => '\n',
                KeyCode.Tab => '\t',
                _ => '\0',
            };
            if (typed != '\0') Queue(EventType.KeyDown, modifiers, guiPosition, e => e.character = typed);
        }

        public void KeyUp(KeyCode code, EventModifiers modifiers, Vector2 guiPosition)
        {
            State.KeyReleased(code);
            Queue(EventType.KeyUp, modifiers, guiPosition, e => e.keyCode = code);
        }

        /// Typed text: one character event per UTF-16 unit.
        public void Text(uint codepoint, EventModifiers modifiers)
        {
            foreach (var ch in char.ConvertFromUtf32((int)codepoint))
                Queue(EventType.KeyDown, modifiers, Vector2.zero, e => e.character = ch);
        }

        public void MouseMoved(Vector2 pixels)
        {
            var previous = State.MousePosition;
            State.MouseMoved(pixels);
            var gui = PointerPixels.Gui(pixels, OniBrutal.Engine.Platform.PlatformServices.Current.FramebufferHeight);
            var delta = new Vector2(pixels.x - previous.x, -(pixels.y - previous.y));
            int dragging = Array.IndexOf(buttonsDown, true);
            Queue(dragging >= 0 ? EventType.MouseDrag : EventType.MouseMove, 0, gui, e =>
            {
                e.delta = delta;
                e.button = Math.Max(0, dragging);
            });
        }

        public void Scrolled(Vector2 delta, Vector2 guiPosition)
        {
            State.Scrolled(delta);
            // IMGUI scroll deltas point down for wheel-down, in lines (three per notch).
            Queue(EventType.ScrollWheel, 0, guiPosition, e => e.delta = new Vector2(-delta.x, -delta.y) * 3f);
        }

        public void MouseDown(int button, EventModifiers modifiers, Vector2 guiPosition)
        {
            buttonsDown[button] = true;
            State.KeyPressed(KeyCode.Mouse0 + button);
            float now = Time.realtimeSinceStartup;
            // Double clicks: same button within the system interval and a few pixels.
            if (button == lastClickButton && now - lastClickTime < 0.5f && (guiPosition - dragStart).sqrMagnitude < 25f) clickCount++;
            else clickCount = 1;
            lastClickTime = now;
            lastClickButton = button;
            dragStart = guiPosition;
            int clicks = clickCount;
            Queue(EventType.MouseDown, modifiers, guiPosition, e =>
            {
                e.button = button;
                e.clickCount = clicks;
            });
        }

        public void MouseUp(int button, EventModifiers modifiers, Vector2 guiPosition)
        {
            buttonsDown[button] = false;
            State.KeyReleased(KeyCode.Mouse0 + button);
            int clicks = clickCount;
            Queue(EventType.MouseUp, modifiers, guiPosition, e =>
            {
                e.button = button;
                e.clickCount = clicks;
            });
        }
    }

    internal static class KeyMap
    {
        public static bool IsKeypad(KeyCode k) => k is >= KeyCode.Keypad0 and <= KeyCode.KeypadEquals;
        // Unity flags Backspace as a function key too (its text editing key bindings expect it).
        public static bool IsFunction(KeyCode k) => k is >= KeyCode.UpArrow and <= KeyCode.F15 or KeyCode.Delete or KeyCode.Insert or KeyCode.Backspace;

        public static KeyCode ToUnity(GlfwKey key) => key switch
        {
            GlfwKey.Space => KeyCode.Space,
            GlfwKey.Apostrophe => KeyCode.Quote,
            GlfwKey.Comma => KeyCode.Comma,
            GlfwKey.Minus => KeyCode.Minus,
            GlfwKey.Period => KeyCode.Period,
            GlfwKey.Slash => KeyCode.Slash,
            >= GlfwKey.Number0 and <= GlfwKey.Number9 => KeyCode.Alpha0 + (key - GlfwKey.Number0),
            GlfwKey.Semicolon => KeyCode.Semicolon,
            GlfwKey.Equal => KeyCode.Equals,
            >= GlfwKey.A and <= GlfwKey.Z => KeyCode.A + (key - GlfwKey.A),
            GlfwKey.LeftBracket => KeyCode.LeftBracket,
            GlfwKey.Backslash => KeyCode.Backslash,
            GlfwKey.RightBracket => KeyCode.RightBracket,
            GlfwKey.GraveAccent => KeyCode.BackQuote,
            GlfwKey.Escape => KeyCode.Escape,
            GlfwKey.Enter => KeyCode.Return,
            GlfwKey.Tab => KeyCode.Tab,
            GlfwKey.Backspace => KeyCode.Backspace,
            GlfwKey.Insert => KeyCode.Insert,
            GlfwKey.Delete => KeyCode.Delete,
            GlfwKey.Right => KeyCode.RightArrow,
            GlfwKey.Left => KeyCode.LeftArrow,
            GlfwKey.Down => KeyCode.DownArrow,
            GlfwKey.Up => KeyCode.UpArrow,
            GlfwKey.PageUp => KeyCode.PageUp,
            GlfwKey.PageDown => KeyCode.PageDown,
            GlfwKey.Home => KeyCode.Home,
            GlfwKey.End => KeyCode.End,
            GlfwKey.CapsLock => KeyCode.CapsLock,
            GlfwKey.ScrollLock => KeyCode.ScrollLock,
            GlfwKey.NumLock => KeyCode.Numlock,
            GlfwKey.PrintScreen => KeyCode.Print,
            GlfwKey.Pause => KeyCode.Pause,
            >= GlfwKey.F1 and <= GlfwKey.F15 => KeyCode.F1 + (key - GlfwKey.F1),
            >= GlfwKey.Kp0 and <= GlfwKey.Kp9 => KeyCode.Keypad0 + (key - GlfwKey.Kp0),
            GlfwKey.KpDecimal => KeyCode.KeypadPeriod,
            GlfwKey.KpDivide => KeyCode.KeypadDivide,
            GlfwKey.KpMultiply => KeyCode.KeypadMultiply,
            GlfwKey.KpSubtract => KeyCode.KeypadMinus,
            GlfwKey.KpAdd => KeyCode.KeypadPlus,
            GlfwKey.KpEnter => KeyCode.KeypadEnter,
            GlfwKey.KpEqual => KeyCode.KeypadEquals,
            GlfwKey.LeftShift => KeyCode.LeftShift,
            GlfwKey.LeftControl => KeyCode.LeftControl,
            GlfwKey.LeftAlt => KeyCode.LeftAlt,
            GlfwKey.LeftSuper => KeyCode.LeftWindows,
            GlfwKey.RightShift => KeyCode.RightShift,
            GlfwKey.RightControl => KeyCode.RightControl,
            GlfwKey.RightAlt => KeyCode.RightAlt,
            GlfwKey.RightSuper => KeyCode.RightWindows,
            GlfwKey.Menu => KeyCode.Menu,
            _ => KeyCode.None,
        };
    }
}
