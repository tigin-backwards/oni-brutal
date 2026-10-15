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
    /// -screen-fullscreen, -window-mode).
    internal sealed record WindowSettings(string Title, int Width, int Height, FullScreenMode Mode);

    /// The player window (GLFW, OpenGL 4.5 core context) and its input devices.
    internal sealed unsafe class GlfwPlatform : IPlatform, IDisposable
    {
        readonly GlfwWindow window;
        readonly InputStateFeed feed = new();
        FullScreenMode mode;
        int2 windowedPosition, windowedSize;
        bool cursorVisible = true;
        CursorLockMode cursorLock;
        GlfwCursor customCursor;
        bool closeRequested;

        public GlfwWindow Window => window;

        public GlfwPlatform(WindowSettings settings)
        {
            Glfw.ErrorCallback += (code, description) => Debug.LogWarning($"GLFW {code}: {description}");
            if (!Glfw.Init()) throw new InvalidOperationException("GLFW could not initialize (no display available).");
            Glfw.DefaultWindowHints();
            Glfw.WindowHint(GlfwWindowHint.ClientApi, GLFW_OPENGL_API);
            Glfw.WindowHint(GlfwWindowHint.ContextVersionMajor, 4);
            Glfw.WindowHint(GlfwWindowHint.ContextVersionMinor, 5);
            Glfw.WindowHint(GlfwWindowHint.OpenGlProfile, GLFW_OPENGL_CORE_PROFILE);
            Glfw.WindowHint(GlfwWindowHint.OpenGlForwardCompat, GLFW_TRUE);
            Glfw.WindowHint(GlfwWindowHint.DepthBits, 24);
            Glfw.WindowHint(GlfwWindowHint.StencilBits, 8);
            Glfw.WindowHint(GlfwWindowHint.Doublebuffer, GLFW_TRUE);
            Glfw.WindowHint(GlfwWindowHint.Visible, GLFW_FALSE);

            var monitor = Glfw.PrimaryMonitor;
            var desktop = monitor.VideoMode;
            mode = settings.Mode;
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
            if (mode == FullScreenMode.MaximizedWindow) window.Maximize();
            window.MakeContextCurrent();
            window.Visible = true;
            window.Focus();

            window.OnKey += OnKey;
            window.OnChar += (_, codepoint) => feed.Text((uint)codepoint, Modifiers());
            window.OnMouseButton += OnMouseButton;
            window.OnCursorPos += (_, pos) => feed.MouseMoved(ToPixels(pos));
            window.OnScroll += (_, offset) => feed.Scrolled(new Vector2((float)offset.X, (float)offset.Y), MouseGuiPosition());
            window.OnCursorEnter += (_, entered) => InputState.Instance.MousePresent = entered;
            window.OnClose += _ => closeRequested = true;
            InputState.Instance.MousePresent = true;
        }

        // ----- IPlatform -----

        public bool HasDisplay => true;
        public int FramebufferWidth => window.FramebufferSize.X;
        public int FramebufferHeight => window.FramebufferSize.Y;
        public FullScreenMode FullScreenMode => mode;
        public bool Focused => window.IsFocused;

        /// Monitor DPI from its physical size (0 when the monitor does not report one, as Unity).
        public float Dpi
        {
            get
            {
                var m = CurrentMonitor;
                var mm = m.PhysicalSize;
                return mm.X <= 0 ? 0f : m.Size.X / (mm.X / 25.4f);
            }
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

        public OniBrutal.Engine.Platform.DisplayMode DesktopMode => ModeOf(CurrentMonitor.VideoMode);

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
                return list.ToArray();
            }
        }

        public void SetResolution(int width, int height, FullScreenMode newMode, uint refreshNumerator, uint refreshDenominator)
        {
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
        }

        public bool PollEvents(InputState input)
        {
            Glfw.PollEvents();
            return !closeRequested;
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

        public void SwapBuffers() => window.SwapBuffers();

        public void Dispose()
        {
            if (!customCursor.IsNull()) customCursor.Dispose();
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
            return new Vector2((float)pos.X * sx, fb.Y - (float)pos.Y * sy);
        }

        Vector2 MouseGuiPosition()
        {
            var p = InputState.Instance.MousePosition;
            return new Vector2(p.x, FramebufferHeight - p.y);
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
            var e = new Event { type = type, rawType = type, modifiers = modifiers, mousePosition = guiPosition };
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
            var gui = new Vector2(pixels.x, OniBrutal.Engine.Platform.PlatformServices.Current.FramebufferHeight - pixels.y);
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
        public static bool IsFunction(KeyCode k) => k is >= KeyCode.UpArrow and <= KeyCode.F15 or KeyCode.Delete or KeyCode.Insert;

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
