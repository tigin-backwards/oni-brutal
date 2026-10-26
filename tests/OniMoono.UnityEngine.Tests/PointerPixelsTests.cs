using OniMoono.Engine.Platform;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    public class PointerPixelsTests
    {
        // The original player, pointer on column 640, row 360 (from the top) of a 1280x720 screen,
        // the mouse never moved: Input.mousePosition (640, 359), Event.mousePosition (640, 361).
        [Fact]
        public void APointerPixelGivesUnitysScreenAndGuiPositions()
        {
            var screen = PointerPixels.Screen(new Vector2(640f, 360f), 720f);
            Assert.Equal(new Vector2(640f, 359f), screen);
            Assert.Equal(new Vector2(640f, 361f), PointerPixels.Gui(screen, 720f));
        }

        [Fact]
        public void TheTopRowIsTheHighestScreenRow()
        {
            Assert.Equal(719f, PointerPixels.Screen(new Vector2(0f, 0f), 720f).y);
            Assert.Equal(0f, PointerPixels.Screen(new Vector2(0f, 719f), 720f).y);
        }
    }
}
