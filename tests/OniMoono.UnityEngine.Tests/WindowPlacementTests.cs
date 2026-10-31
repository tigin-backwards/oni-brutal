using Brutal.Numerics;
using OniMoono.Desktop;
using Xunit;

namespace OniMoono.Tests
{
    /// Where the window goes on Windows: windowed requests too tall for the work area are
    /// maximized.
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
    }
}
