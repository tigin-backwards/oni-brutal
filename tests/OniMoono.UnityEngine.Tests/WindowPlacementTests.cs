using Brutal.Numerics;
using OniMoono.Desktop;
using Xunit;

namespace OniMoono.Tests
{
    /// Where the window goes on Windows: windowed requests too tall for the work area are
    /// maximized, and the borderless fullscreen window is one row taller than its monitor.
    public class WindowPlacementTests
    {
        // A 3840x2160 monitor at 150% scaling, whose title bar and bottom border measured 45 + 11.
        static readonly int2 WorkArea = new(3840, 2160);
        const int FrameHeight = 56;

        [Fact]
        public void ANativeResolutionWindowDoesNotFit()
        {
            Assert.False(GlfwPlatform.FitsWorkArea(new int2(3840, 2160), FrameHeight, WorkArea));
            // What Windows made of it, saved by the game and asked for again on the next start.
            Assert.False(GlfwPlatform.FitsWorkArea(new int2(3840, 2130), FrameHeight, WorkArea));
        }

        [Fact]
        public void AWindowWithItsFrameOnScreenFits()
        {
            Assert.True(GlfwPlatform.FitsWorkArea(new int2(2560, 1440), FrameHeight, WorkArea));
            Assert.True(GlfwPlatform.FitsWorkArea(new int2(3840, 2160 - FrameHeight), FrameHeight, WorkArea));
            Assert.False(GlfwPlatform.FitsWorkArea(new int2(3840, 2160 - FrameHeight + 1), FrameHeight, WorkArea));
        }

        [Fact]
        public void AWindowWiderThanTheWorkAreaDoesNotFit()
            => Assert.False(GlfwPlatform.FitsWorkArea(new int2(3841, 1440), FrameHeight, WorkArea));

        [Fact]
        public void BorderlessIsOneRowTallerOnWindows()
        {
            var (position, size) = GlfwPlatform.BorderlessRect(new int2(-3840, 0), new int2(3840, 2160), windows: true);
            Assert.Equal(new int2(-3840, -1), position);
            Assert.Equal(new int2(3840, 2161), size);
        }

        [Fact]
        public void BorderlessCoversTheMonitorElsewhere()
        {
            var (position, size) = GlfwPlatform.BorderlessRect(new int2(1920, 0), new int2(2560, 1440), windows: false);
            Assert.Equal(new int2(1920, 0), position);
            Assert.Equal(new int2(2560, 1440), size);
        }
    }
}
