using OniMoono.Desktop.Gl;
using Xunit;
using static OniMoono.Desktop.Gl.GlPresenter;

namespace OniMoono.Tests
{
    /// A present never shows a back buffer the frame did not finish drawing into: one allocated
    /// anew after the frame drew (a resize), one nothing was drawn into this frame (it holds an
    /// older frame), or one never drawn (undefined, black). The last frame shown stays instead.
    public class PresentChoiceTests
    {
        [Fact]
        public void ADrawnFrameIsPresented()
            => Assert.Equal(PresentChoice.Frame, Choose(allocated: true, drawnSinceAllocation: true, drawnThisFrame: true, anyShown: true));

        [Theory]
        [InlineData(true, false, false)] // allocated anew (resized) and nothing drawn since
        [InlineData(true, true, false)]  // nothing drawn this frame: an older frame's content
        [InlineData(false, false, false)] // no buffer yet
        public void AnIncompleteFrameShowsTheLastFrameAgain(bool allocated, bool drawnSinceAllocation, bool drawnThisFrame)
        {
            Assert.Equal(PresentChoice.LastFrameAgain, Choose(allocated, drawnSinceAllocation, drawnThisFrame, anyShown: true));
            Assert.Equal(PresentChoice.Nothing, Choose(allocated, drawnSinceAllocation, drawnThisFrame, anyShown: false));
        }
    }
}
