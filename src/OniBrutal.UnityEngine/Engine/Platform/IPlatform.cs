using UnityEngine;

namespace OniBrutal.Engine.Platform
{
    /// A display mode of a monitor.
    internal readonly record struct DisplayMode(int Width, int Height, uint RefreshNumerator, uint RefreshDenominator);

    /// The window, display and input devices of the machine (GLFW in a windowed player, nothing
    /// in a headless one). Input arrives through PollEvents into the engine's InputState.
    internal interface IPlatform
    {
        bool HasDisplay { get; }

        /// Size of the window's drawable area in pixels (Screen.width/height).
        int FramebufferWidth { get; }
        int FramebufferHeight { get; }
        FullScreenMode FullScreenMode { get; }
        float Dpi { get; }

        /// The current mode of the monitor the window is on, and all its modes.
        DisplayMode DesktopMode { get; }
        DisplayMode[] DisplayModes { get; }

        void SetResolution(int width, int height, FullScreenMode mode, uint refreshNumerator, uint refreshDenominator);

        /// Processes OS events, reporting input to `input`. Returns false when the user closed
        /// the window.
        bool PollEvents(InputState input);

        bool CursorVisible { get; set; }
        CursorLockMode CursorLockState { get; set; }
        void SetCursorImage(Color32[] pixels, int width, int height, Vector2 hotspot);
        void SetDefaultCursor();

        string Clipboard { get; set; }
        bool Focused { get; }
    }

    /// No window and no input devices; Screen reports the project's default resolution, as a
    /// player run without graphics does.
    internal sealed class HeadlessPlatform(int width, int height) : IPlatform
    {
        public bool HasDisplay => false;
        public int FramebufferWidth { get; private set; } = width;
        public int FramebufferHeight { get; private set; } = height;
        public FullScreenMode FullScreenMode { get; private set; } = FullScreenMode.Windowed;
        public float Dpi => 0f; // Unity reports 0 when the DPI is unknown
        public DisplayMode DesktopMode => new(FramebufferWidth, FramebufferHeight, 60, 1);
        public DisplayMode[] DisplayModes => [DesktopMode];

        public void SetResolution(int w, int h, FullScreenMode mode, uint num, uint den)
        {
            FramebufferWidth = w;
            FramebufferHeight = h;
            FullScreenMode = mode;
        }

        public bool PollEvents(InputState input) => true;
        public bool CursorVisible { get; set; } = true;
        public CursorLockMode CursorLockState { get; set; }
        public void SetCursorImage(Color32[] pixels, int width, int height, Vector2 hotspot) { }
        public void SetDefaultCursor() { }
        public string Clipboard { get; set; } = "";
        public bool Focused => true;
    }

    internal static class PlatformServices
    {
        public static IPlatform Current { get; private set; } = new HeadlessPlatform(1024, 768);

        public static void Install(IPlatform platform) => Current = platform;
    }
}
