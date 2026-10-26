using OniMoono.Engine;
using OniMoono.Engine.Rendering;

namespace UnityEngine
{
    public partial class ScreenCapture
    {
        /// Saves the frame being rendered as a PNG once rendering is complete, rendered at
        /// superSize times the screen resolution. A player without graphics captures nothing.
        public static void CaptureScreenshot(string filename, int superSize)
        {
            Threads.AssertMainThread("CaptureScreenshot");
            FrameRenderer.RequestCapture(filename, superSize);
        }

        public static void CaptureScreenshot(string filename) => CaptureScreenshot(filename, 1);
    }
}
