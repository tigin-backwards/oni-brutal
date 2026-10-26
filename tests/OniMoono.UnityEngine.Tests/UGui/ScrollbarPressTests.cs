using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;

namespace OniMoono.Tests.UGui
{
    // Scrollbar presses on the track (docs/cleanroom/ugui.md, "Scrollbar presses on the track"),
    // on the cases of tools/UnityProbe's scrollbar scenario: a fresh scrollbar per case, its track
    // an Image of 400 x 20 (20 x 400 vertical) at the screen centre with a stretched "Sliding
    // Area" and a "Handle" Image in it, pressed through the StandaloneInputModule at a fraction p
    // of the track's length (0 at the left or bottom end), held, sometimes moved, and released.
    public class ScrollbarPressTests : UGuiTest
    {
        static readonly Vector2 Centre = new(512f, 384f);
        const float None = float.NaN;

        sealed class Run
        {
            public float Press;
            public readonly List<float> Frames = new();
            public readonly List<float> Changed = new();
        }

        static Run Drive(Scrollbar.Direction dir, float size, float value, int steps, float at, int hold, float moveTo)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            var module = esGo.AddComponent<StandaloneInputModule>();
            var input = esGo.AddComponent<TestInput>();
            module.inputOverride = input;
            var canvas = NewCanvas();
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            bool vertical = dir == Scrollbar.Direction.BottomToTop || dir == Scrollbar.Direction.TopToBottom;
            var track = NewRect("Track", canvas.transform, vertical ? 20f : 400f, vertical ? 400f : 20f, typeof(Image));
            var area = NewRect("Sliding Area", track, 0f, 0f);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.sizeDelta = Vector2.zero;
            var handle = NewRect("Handle", area, 0f, 0f, typeof(Image));
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.direction = dir;
            bar.numberOfSteps = steps;
            bar.size = size;
            bar.value = value;
            var run = new Run();
            bar.onValueChanged.AddListener(v => run.Changed.Add(v));
            input.Mouse = new Vector2(-50f, -50f);
            Frame();
            Frame();

            Vector2 At(float f) => vertical ? Centre + new Vector2(0f, (f - 0.5f) * 400f) : Centre + new Vector2((f - 0.5f) * 400f, 0f);
            input.Mouse = At(at);
            input.Down = input.Held = true;
            Frame();
            run.Press = bar.value;
            input.Down = false;
            for (int i = 0; i < hold; i++)
            {
                if (!float.IsNaN(moveTo)) input.Mouse = At(moveTo);
                Frame();
                run.Frames.Add(bar.value);
            }
            input.Held = false;
            input.Up = true;
            Frame();
            run.Frames.Add(bar.value);
            input.Up = false;
            for (int i = 0; i < 3; i++)
            {
                Frame();
                run.Frames.Add(bar.value);
            }
            return run;
        }

        static void Near(float expected, float actual) => Assert.True(Mathf.Abs(expected - actual) < 1e-5f, $"{actual} is not {expected}");

        /// A still pointer: the press sets the value once, and holding (however long) and the
        /// release change nothing.
        [Theory]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.4496488f, 0f, 0, 0.5f, 2, 0.5f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0, 0.5f, 1, 0.5f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0, 0.5f, 30, 0.5f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0, 0.6f, 2, 0.6818182f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0, 0.75f, 2, 0.9545454f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0, 0.9f, 2, 1f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0, 0.9f, 60, 1f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0, 0.99f, 2, 1f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0, 0.99f, 60, 1f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0, 0.46f, 2, 0.4272727f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.2f, 0f, 0, 0.3f, 2, 0.25f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.2f, 0f, 0, 0.9f, 60, 1f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.2f, 0.5f, 0, 0.1f, 2, 0f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.2f, 0.5f, 0, 0.9f, 2, 1f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.2f, 1f, 0, 0.1f, 60, 0f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.1f, 0f, 0, 0.55f, 60, 0.5555556f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 5, 0.75f, 2, 1f)]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.2f, 0f, 3, 0.9f, 60, 1f)]
        [InlineData(Scrollbar.Direction.RightToLeft, 0.45f, 0f, 0, 0.25f, 2, 0.9545454f)]
        [InlineData(Scrollbar.Direction.BottomToTop, 0.3f, 0f, 0, 0.8f, 2, 0.9285714f)]
        public void APressOnTheTrackCentresTheHandleOnceAndDoesNotRepeat(Scrollbar.Direction dir, float size, float value, int steps, float at, int hold, float expected)
        {
            var run = Drive(dir, size, value, steps, at, hold, None);
            Near(expected, run.Press);
            Assert.Single(run.Changed);
            Near(expected, run.Changed[0]);
            Assert.All(run.Frames, v => Assert.Equal(run.Press, v));
        }

        [Fact]
        public void TheHandleFollowsTheCentringFormula()
        {
            // size 0.45, q 0.75: (0.75 - 0.225) / 0.55; the handle spans 0.525 to 0.975.
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            var input = esGo.AddComponent<TestInput>();
            esGo.AddComponent<StandaloneInputModule>().inputOverride = input;
            var canvas = NewCanvas();
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var track = NewRect("Track", canvas.transform, 400f, 20f, typeof(Image));
            var area = NewRect("Sliding Area", track, 0f, 0f);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.sizeDelta = Vector2.zero;
            var handle = NewRect("Handle", area, 0f, 0f, typeof(Image));
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.size = 0.45f;
            bar.value = 0f;
            Frame();
            input.Mouse = Centre + new Vector2(100f, 0f);
            input.Down = input.Held = true;
            Frame();
            Near(0.525f, handle.anchorMin.x);
            Near(0.975f, handle.anchorMax.x);
        }

        /// A press on the handle changes nothing and invokes nothing.
        [Theory]
        [InlineData(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0.2f)]
        [InlineData(Scrollbar.Direction.TopToBottom, 0.3f, 0f, 0.8f)]
        public void APressOnTheHandleChangesNothing(Scrollbar.Direction dir, float size, float value, float at)
        {
            var run = Drive(dir, size, value, 0, at, 2, None);
            Assert.Equal(value, run.Press);
            Assert.Empty(run.Changed);
            Assert.All(run.Frames, v => Assert.Equal(value, v));
        }

        /// While held, the handle follows the pointer by its centre, under the drag threshold
        /// too; off the track the value stays clamped.
        [Theory]
        [InlineData(0.9f, 0.3f, 1f, 0.25f)]
        [InlineData(0.9f, 2f, 1f, 1f)]
        [InlineData(0.5f, 0.525f, 0.5f, 0.53125f)]
        [InlineData(0.5f, 0.575f, 0.5f, 0.59375f)]
        public void WhileHeldTheHandleFollowsThePointer(float at, float moveTo, float pressed, float followed)
        {
            var run = Drive(Scrollbar.Direction.LeftToRight, 0.2f, 0f, 0, at, 10, moveTo);
            Near(pressed, run.Press);
            Assert.All(run.Frames, v => Near(followed, v));
            Assert.Equal(pressed == followed ? 1 : 2, run.Changed.Count);
            Near(pressed, run.Changed[0]);
            Near(followed, run.Changed[^1]);
        }

        /// A press on the handle, off its centre, then a drag keeps the grabbed offset: the
        /// handle's start moves by 0.4 of the track, 0.4 / 0.55 of the value.
        [Fact]
        public void DraggingTheHandleKeepsTheGrabbedOffset()
        {
            var run = Drive(Scrollbar.Direction.LeftToRight, 0.45f, 0f, 0, 0.1f, 10, 0.5f);
            Assert.Equal(0f, run.Press);
            Assert.All(run.Frames, v => Near(0.7272727f, v));
            Assert.Single(run.Changed);
        }
    }

    /// A subclass reaching the protected members a mod's subclass can.
    public class ProbeScrollbar : Scrollbar
    {
        public bool SkipBaseUpdate;
        public int Updates;
        public System.Collections.IEnumerator FromEvent(PointerEventData eventData) => ClickRepeat(eventData);
        public System.Collections.IEnumerator FromPoint(Vector2 screenPosition, Camera camera) => ClickRepeat(screenPosition, camera);
        protected override void Update()
        {
            Updates++;
            if (!SkipBaseUpdate) base.Update();
        }
    }

    // The press while held, ClickRepeat and Update (docs/cleanroom/ugui.md, "Scrollbar presses on
    // the track", rules 4 to 7), on the cases of the probe's clickrepeat parts: size 0.2, a 400 x 20
    // track at the screen centre.
    public class ScrollbarHeldPressTests : UGuiTest
    {
        static readonly Vector2 Centre = new(512f, 384f);
        static Vector2 At(float f) => Centre + new Vector2((f - 0.5f) * 400f, 0f);
        TestInput input;
        ProbeScrollbar bar;
        RectTransform track;
        readonly List<float> changed = new();

        void Setup(float value)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            input = esGo.AddComponent<TestInput>();
            esGo.AddComponent<StandaloneInputModule>().inputOverride = input;
            var canvas = NewCanvas();
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            track = NewRect("Track", canvas.transform, 400f, 20f, typeof(Image));
            var area = NewRect("Sliding Area", track, 0f, 0f);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.sizeDelta = Vector2.zero;
            var handle = NewRect("Handle", area, 0f, 0f, typeof(Image));
            bar = track.gameObject.AddComponent<ProbeScrollbar>();
            bar.handleRect = handle;
            bar.size = 0.2f;
            bar.value = value;
            bar.onValueChanged.AddListener(v => changed.Add(v));
            input.Mouse = new Vector2(-50f, -50f);
            Frame();
            Frame();
        }

        void Press(float at)
        {
            input.Mouse = At(at);
            input.Down = input.Held = true;
            Frame();
            input.Down = false;
        }

        void Release()
        {
            input.Held = false;
            input.Up = true;
            Frame();
            input.Up = false;
        }

        static void Near(float expected, float actual) => Assert.True(Mathf.Abs(expected - actual) < 1e-5f, $"{actual} is not {expected}");

        static void Sequence(float[] expected, List<float> actual)
        {
            Assert.Equal(expected.Length, actual.Count);
            for (int i = 0; i < expected.Length; i++) Near(expected[i], actual[i]);
        }

        [Fact]
        public void AValueSetWhileHeldOnTheTrackIsPutBackAtTheEndOfTheFrame()
        {
            Setup(0f);
            Press(0.75f);
            Near(0.8125f, bar.value);
            Frame();
            bar.value = 0.2f;
            Near(0.2f, bar.value);
            Frame();
            Near(0.8125f, bar.value);
            Sequence(new[] { 0.8125f, 0.2f, 0.8125f }, changed);
            Release();
            bar.value = 0.2f;
            Frame();
            Near(0.2f, bar.value);
        }

        [Fact]
        public void AValueSetWhileHeldOnTheHandleStaysWhileTheHandleCoversThePress()
        {
            Setup(0.5f);
            Press(0.5f);
            Frame();
            bar.value = 0.45f;
            Frame();
            Frame();
            Near(0.45f, bar.value);
            bar.value = 0f;
            Frame();
            Near(0.5f, bar.value);
            Sequence(new[] { 0.45f, 0f, 0.5f }, changed);
        }

        [Fact]
        public void ClickRepeatOnANeverPressedScrollbarThrows()
        {
            Setup(0f);
            foreach (var it in new[] { bar.FromPoint(At(0.9f), null), bar.FromPoint(At(0.1f), null),
                         bar.FromEvent(new PointerEventData(null) { pointerPressRaycast = new RaycastResult { screenPosition = At(0.9f) } }) })
            {
                var e = Assert.Throws<System.NullReferenceException>(() => it.MoveNext());
                Assert.Equal("routine is null", e.Message);
            }
            Assert.Equal(0f, bar.value);
            Assert.Empty(changed);
        }

        [Fact]
        public void ClickRepeatAppliesItsPointWhileAPressHoldsAndThePressPutsItsOwnBack()
        {
            Setup(0.5f);
            Press(0.5f); // on the handle
            var it = bar.FromPoint(At(0.9f), null);
            for (int k = 0; k < 4; k++)
            {
                Assert.True(it.MoveNext());
                Assert.IsType<WaitForEndOfFrame>(it.Current);
                Near(1f, bar.value);
                Frame();
                Near(0.5f, bar.value);
            }
            Sequence(new[] { 1f, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f, 0.5f }, changed);
            Release();
            Assert.False(it.MoveNext());
            Near(0.5f, bar.value);
        }

        [Fact]
        public void ClickRepeatAsACoroutineRunsAfterThePressAtTheEndOfTheFrame()
        {
            Setup(0.5f);
            Press(0.5f);
            bar.StartCoroutine(bar.FromPoint(At(0.9f), null));
            Near(1f, bar.value);
            for (int f = 0; f < 3; f++)
            {
                Frame();
                Near(1f, bar.value);
            }
            // The first step in StartCoroutine, then at each end of frame the press's 0.5 and the
            // subclass's 1.
            Sequence(new[] { 1f, 0.5f, 1f, 0.5f, 1f, 0.5f, 1f }, changed);
            changed.Clear();
            Release();
            Frame();
            Near(1f, bar.value);
            Assert.Empty(changed);
        }

        [Fact]
        public void ClickRepeatOnTheHandleChangesNothing()
        {
            Setup(0f);
            Press(0.75f);
            var it = bar.FromPoint(At(0.75f), null);
            for (int k = 0; k < 3; k++)
            {
                Assert.True(it.MoveNext());
                Frame();
            }
            Near(0.8125f, bar.value);
            Assert.Single(changed);
        }

        [Fact]
        public void ClickRepeatEndsAfterADrag()
        {
            Setup(0f);
            Press(0.75f);
            Frame();
            input.Mouse = At(0.3f);
            Frame();
            Near(0.25f, bar.value);
            Assert.False(bar.FromPoint(At(0.9f), null).MoveNext());
            Frame();
            Near(0.25f, bar.value);
            Sequence(new[] { 0.8125f, 0.25f }, changed);
        }

        [Fact]
        public void ClickRepeatEndsAfterARelease()
        {
            Setup(0f);
            Press(0.75f);
            Release();
            Assert.False(bar.FromPoint(At(0.9f), null).MoveNext());
            Near(0.8125f, bar.value);
        }

        [Fact]
        public void TheEventOverloadReadsThePressRaycastsScreenPosition()
        {
            Setup(0f);
            Press(0.5f);
            Near(0.5f, bar.value);
            // Only position, or only pressPosition: the press point is the screen's corner.
            Assert.True(bar.FromEvent(new PointerEventData(null) { position = At(0.9f) }).MoveNext());
            Near(0f, bar.value);
            Frame();
            Assert.True(bar.FromEvent(new PointerEventData(null) { pressPosition = At(0.9f) }).MoveNext());
            Near(0f, bar.value);
            Frame();
            Assert.True(bar.FromEvent(new PointerEventData(null) { pointerPressRaycast = new RaycastResult { screenPosition = At(0.9f) } }).MoveNext());
            Near(1f, bar.value);
        }

        /// The handle starts at v - v * size in floats: 0.65 to 0.849999964 for 0.8125 at size
        /// 0.2 (v * (1 - size) would give 0.65000004 to 0.85).
        [Fact]
        public void TheHandlesAnchorsAreWorkedOutAsMeasured()
        {
            Setup(0.8125f);
            Assert.Equal(0.65f, bar.handleRect.anchorMin.x);
            Assert.Equal(0.849999964f, bar.handleRect.anchorMax.x);
            bar.value = 0.2f;
            Assert.Equal(0.16f, bar.handleRect.anchorMin.x);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AnUpdateOverrideWithoutBaseChangesNothing(bool skip)
        {
            {
                Setup(0f);
                bar.SkipBaseUpdate = skip;
                bar.value = 0.5f;
                Frame();
                bar.size = 0.4f;
                Frame();
                Near(0.3f, bar.handleRect.anchorMin.x);
                Near(0.7f, bar.handleRect.anchorMax.x);
                bar.size = 0.2f;
                bar.value = 0f;
                Frame();
                Press(0.9f);
                Frame();
                Release();
                Near(1f, bar.value);
                Assert.True(bar.Updates > 0);
                Sequence(new[] { 0.5f, 0f, 1f }, changed);
            }
        }
    }
}
