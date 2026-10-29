using OniMoono.Desktop.Gl;
using Xunit;

namespace OniMoono.Tests
{
    /// The shader warm-up leaves out XR stereo variants (GlDevice.StereoOnly, issue #20).
    public class StereoVariantTests
    {
        [Theory]
        [InlineData(new string[0], false)]
        [InlineData(new[] { "UNITY_UI_CLIP_RECT" }, false)]
        [InlineData(new[] { "STEREO_INSTANCING_ON" }, true)]
        [InlineData(new[] { "STEREO_MULTIVIEW_ON" }, true)]
        [InlineData(new[] { "UNITY_SINGLE_PASS_STEREO" }, true)]
        [InlineData(new[] { "UNITY_UI_ALPHACLIP", "STEREO_INSTANCING_ON" }, true)]
        public void OnlyStereoVariantsAreLeftOut(string[] keywords, bool stereo)
            => Assert.Equal(stereo, GlDevice.StereoOnly(keywords));
    }
}
