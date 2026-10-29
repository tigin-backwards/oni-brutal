using OniMoono.Desktop;
using Xunit;

namespace OniMoono.Tests
{
    /// Which GPU the player asks for on a laptop with two (src/OniMoono.Desktop/GpuPreference.cs):
    /// the parts of it that can be checked without a machine of that kind.
    public class GpuPreferenceTests
    {
        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("high", true)]
        [InlineData("discrete", true)]
        [InlineData("High", true)]
        [InlineData("default", false)]
        [InlineData("low", false)]
        [InlineData("integrated", false)]
        [InlineData("power-saving", false)]
        [InlineData("system", false)]
        [InlineData("off", false)]
        [InlineData("0", false)]
        public void TheSettingChoosesWhetherToAsk(string setting, bool wanted)
            => Assert.Equal(wanted, GpuPreference.Wanted(setting));

        [Fact]
        public void NvidiasDriverAsksForRenderOffload()
        {
            // NVIDIA's proprietary driver: both variables, whether or not there is a second card
            // (PRIME render offload is what makes the discrete one render then).
            Assert.Equal(new[] { ("__NV_PRIME_RENDER_OFFLOAD", "1"), ("__GLX_VENDOR_LIBRARY_NAME", "nvidia") },
                GpuPreference.Variables(nvidiaDriver: true, displayCards: 2));
            Assert.Equal(new[] { ("__NV_PRIME_RENDER_OFFLOAD", "1"), ("__GLX_VENDOR_LIBRARY_NAME", "nvidia") },
                GpuPreference.Variables(nvidiaDriver: true, displayCards: 1));
        }

        [Fact]
        public void MesaAsksForTheSecondCardOnlyWhenThereIsOne()
        {
            Assert.Equal(new[] { ("DRI_PRIME", "1") }, GpuPreference.Variables(nvidiaDriver: false, displayCards: 2));
            Assert.Empty(GpuPreference.Variables(nvidiaDriver: false, displayCards: 1));
            // No display card at all (a virtual machine, or sysfs unreadable): nothing to name.
            Assert.Empty(GpuPreference.Variables(nvidiaDriver: false, displayCards: 0));
        }

        [Theory]
        [InlineData("", "GpuPreference=2;")]
        [InlineData("GpuPreference=2;", null)]
        [InlineData("GpuPreference=1;", null)]
        [InlineData("GpuPreference=0;", null)]
        [InlineData("gpuPreference=2;", null)]
        // The entry's other settings (Windows writes them itself) are kept, in their list.
        [InlineData("SwapEffectUpgradeEnable=1;", "SwapEffectUpgradeEnable=1;GpuPreference=2;")]
        [InlineData("SwapEffectUpgradeEnable=1", "SwapEffectUpgradeEnable=1;GpuPreference=2;")]
        public void TheWindowsPreferenceIsAddedUnlessTheEntryNamesAGpu(string existing, string expected)
            => Assert.Equal(expected, GpuPreference.WithHighPerformance(existing));
    }
}
