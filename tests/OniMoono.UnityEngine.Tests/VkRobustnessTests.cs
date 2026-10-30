using System;
using System.Linq;
using OniMoono.Desktop;
using OniMoono.Desktop.Vulkan;
using Xunit;

namespace OniMoono.Tests
{
    /// The Vulkan device's handling of damaged input and failures that need no GPU: SPIR-V
    /// reflection of a damaged module, the loss marker's meaning for the next start, and the
    /// fault-injection settings.
    public class VkRobustnessTests
    {
        static byte[] Module(params uint[] instructions)
        {
            uint[] header = [0x07230203, 0x00010000, 0, 100, 0];
            return header.Concat(instructions).SelectMany(BitConverter.GetBytes).ToArray();
        }

        /// A damaged mod bundle's module whose array type is its own element: an error, not a
        /// device thread spinning for ever.
        [Fact]
        public void ASelfReferencingArrayIsRefused()
        {
            var module = Module(
                (4u << 16) | 28, 10, 10, 11,      // %10 = OpTypeArray %10 %11
                (4u << 16) | 32, 12, 0, 10,       // %12 = OpTypePointer UniformConstant %10
                (4u << 16) | 59, 12, 13, 0,       // %13 = OpVariable %12 UniformConstant
                (4u << 16) | 71, 13, 33, 0);      // OpDecorate %13 Binding 0
            Assert.Throws<FormatException>(() => SpirvReflection.Interface(module));
        }

        [Fact]
        public void APresenterFailureCostsOneOpenGLStart()
        {
            const string driver = "Vulkan 1.4.341, driver NVIDIA 610.88";
            string[] lost = [driver, "RTX", "2026-10-30T00:00:00Z", "VK_ERROR_DEVICE_LOST"];
            Assert.StartsWith("the Vulkan device was lost", Renderers.MarkerVerdict(lost, driver, out bool remove));
            Assert.False(remove);
            string[] presenting = [driver, "RTX", "2026-10-30T00:00:00Z", "presenting failed: VK_ERROR_SURFACE_LOST_KHR"];
            Assert.StartsWith("presenting to the window failed in the last run", Renderers.MarkerVerdict(presenting, driver, out remove));
            Assert.True(remove);
            // Another driver: Vulkan again, the marker goes.
            Assert.Null(Renderers.MarkerVerdict(lost, "Vulkan 1.4.341, driver NVIDIA 611.00", out remove));
            Assert.True(remove);
        }

        [Fact]
        public void FaultSettingsParse()
        {
            var f = VkFault.Parse("presenter-create, alloc-every:40,present-after:600");
            Assert.Equal("", f["presenter-create"]);
            Assert.Equal("40", f["alloc-every"]);
            Assert.Equal("600", f["present-after"]);
            Assert.Empty(VkFault.Parse(null));
        }
    }
}
