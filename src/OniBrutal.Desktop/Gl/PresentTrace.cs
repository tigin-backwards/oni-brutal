using System;

namespace OniBrutal.Desktop.Gl
{
    /// Diagnostics (ONI_PRESENT_TRACE=1): what reaches the screen. Logs, with frame numbers, every
    /// window event (focus, iconify, size, framebuffer size, refresh, mode change), back buffers
    /// allocated, frames handed over without a completed render to the back buffer (nothing
    /// drawn, only a clear, a buffer allocated after the frame drew), frames left unpresented,
    /// presents that follow a stall, and frames the window shows near black after one that was
    /// not (a probe of the window's back buffer before each swap).
    internal static class PresentTrace
    {
        public static readonly bool Enabled = Environment.GetEnvironmentVariable("ONI_PRESENT_TRACE") == "1";

        public static void Log(string text) => OniBrutal.Engine.EngineLog.Engine("present trace: " + text);
    }
}
