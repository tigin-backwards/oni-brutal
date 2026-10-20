using UnityEngine;

namespace OniBrutal.Engine.Platform
{
    /// Unity's pointer coordinates for a pointer on a pixel given from the top left, as the
    /// original player reports them (PerfProbe pointer.txt: column 640, row 360 of a 1280x720
    /// screen is Input.mousePosition (640, 359) and Event.mousePosition (640, 361)).
    internal static class PointerPixels
    {
        /// Input.mousePosition: from the bottom left, the bottom row 0.
        public static Vector2 Screen(Vector2 topLeft, float height) => new(topLeft.x, height - 1f - topLeft.y);

        /// Event.mousePosition: the screen height less the screen position's y.
        public static Vector2 Gui(Vector2 screen, float height) => new(screen.x, height - screen.y);
    }
}
