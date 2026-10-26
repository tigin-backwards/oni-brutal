using OniMoono.Engine.Platform;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    // The legacy input state latches the platform's key events once a frame. Several edges of one
    // key can arrive between two frames (fast double-clicks at a low frame rate); they apply in
    // the order they happened.
    public class InputStateTests
    {
        [Fact]
        public void ReleaseThenPressInOneFrameLeavesTheKeyHeld()
        {
            var input = new InputState();
            input.KeyPressed(KeyCode.Mouse0);
            input.BeginFrame(0.05f);
            Assert.True(input.KeyDown(KeyCode.Mouse0));

            input.KeyReleased(KeyCode.Mouse0);
            input.KeyPressed(KeyCode.Mouse0);
            input.BeginFrame(0.05f);
            Assert.True(input.KeyUp(KeyCode.Mouse0));
            Assert.True(input.KeyDown(KeyCode.Mouse0));
            Assert.True(input.Key(KeyCode.Mouse0));

            input.KeyReleased(KeyCode.Mouse0);
            input.BeginFrame(0.05f);
            Assert.True(input.KeyUp(KeyCode.Mouse0));
            Assert.False(input.Key(KeyCode.Mouse0));
        }

        [Fact]
        public void PressReleasePressInOneFrameEndsHeld()
        {
            var input = new InputState();
            input.KeyPressed(KeyCode.A);
            input.KeyReleased(KeyCode.A);
            input.KeyPressed(KeyCode.A);
            input.BeginFrame(0.05f);
            Assert.True(input.KeyDown(KeyCode.A));
            Assert.True(input.KeyUp(KeyCode.A));
            Assert.True(input.Key(KeyCode.A));
        }

        [Fact]
        public void PressAndReleaseInOneFrameReportsBothEdges()
        {
            var input = new InputState();
            input.KeyPressed(KeyCode.Space);
            input.KeyReleased(KeyCode.Space);
            input.BeginFrame(0.05f);
            Assert.True(input.KeyDown(KeyCode.Space));
            Assert.True(input.KeyUp(KeyCode.Space));
            Assert.False(input.Key(KeyCode.Space));

            input.BeginFrame(0.05f);
            Assert.False(input.KeyDown(KeyCode.Space));
            Assert.False(input.KeyUp(KeyCode.Space));
        }

        [Fact]
        public void EdgesHoldForOneFrameOnly()
        {
            var input = new InputState();
            input.KeyPressed(KeyCode.B);
            input.BeginFrame(0.05f);
            input.BeginFrame(0.05f);
            Assert.False(input.KeyDown(KeyCode.B));
            Assert.True(input.Key(KeyCode.B));
        }
    }
}
