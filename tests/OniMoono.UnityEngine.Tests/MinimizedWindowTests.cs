using Brutal.Numerics;
using OniMoono.Desktop;
using Xunit;

namespace OniMoono.Tests
{
    /// Screen sizes of a minimized window (issue #29): the original keeps the size the window had
    /// while Windows reports its framebuffer as 0x0.
    public class MinimizedWindowTests
    {
        [Fact]
        public void MinimizedWindowKeepsItsSize()
        {
            var size = GlfwPlatform.KeptFramebufferSize(new int2(1280, 720), default);
            Assert.Equal(new int2(1280, 720), size);
            size = GlfwPlatform.KeptFramebufferSize(new int2(0, 0), size);
            Assert.Equal(new int2(1280, 720), size);
            // Restored: the window's size again.
            Assert.Equal(new int2(1280, 720), GlfwPlatform.KeptFramebufferSize(new int2(1280, 720), size));
        }

        [Fact]
        public void AnEmptyDimensionKeepsTheSizeToo()
        {
            Assert.Equal(new int2(2560, 1440), GlfwPlatform.KeptFramebufferSize(new int2(2560, 0), new int2(2560, 1440)));
            Assert.Equal(new int2(2560, 1440), GlfwPlatform.KeptFramebufferSize(new int2(0, 1440), new int2(2560, 1440)));
        }

        [Fact]
        public void ResizesAreTakenAsTheyCome()
        {
            Assert.Equal(new int2(1024, 768), GlfwPlatform.KeptFramebufferSize(new int2(1024, 768), new int2(1280, 720)));
        }

        [Fact]
        public void WithNoSizeYetTheReportedOneStands()
        {
            Assert.Equal(new int2(0, 0), GlfwPlatform.KeptFramebufferSize(new int2(0, 0), default));
        }
    }
}
