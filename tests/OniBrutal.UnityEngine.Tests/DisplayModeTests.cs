using System.Linq;
using OniBrutal.Desktop;
using OniBrutal.Engine.Platform;
using Xunit;

namespace OniBrutal.Tests
{
    public class DisplayModeTests
    {
        // Screen.resolutions on a display reporting only its current mode, as the original player
        // lists them on a 1280x1024 Xvfb screen (PerfProbe measure.txt).
        [Fact]
        public void StandardModesAreUnitysOnAVirtualDisplay()
        {
            var modes = GlfwPlatform.StandardModes(new DisplayMode(1280, 1024, 60, 1)).Select(m => $"{m.Width}x{m.Height}");
            Assert.Equal("640x480 720x480 768x480 800x480 854x480 960x540 1024x576 800x600 1024x600 960x640 1024x640 1136x640 "
                         + "960x720 1152x720 1280x720 1024x768 1152x768 1280x768 1280x800 1152x864 1280x864 1280x960 1280x1024",
                string.Join(" ", modes));
        }
    }
}
