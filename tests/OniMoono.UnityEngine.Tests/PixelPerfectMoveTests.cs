using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    /// Moves on pixel-perfect canvases, as measured in the original player (UnityProbe
    /// PROBE_SCENARIO=childdims; docs/cleanroom/ugui.md, "Moves on pixel-perfect canvases"): a
    /// GameObject with a CanvasRenderer whose transform moved gets OnRectTransformDimensionsChange
    /// once, after the canvas update, when its canvas is pixel perfect. The main menu's news tiles
    /// lay out again on it: their buttons' preferred widths follow their heights.
    public class PixelPerfectMoveTests : EngineTest
    {
        static readonly List<string> log = new();
        static bool afterCanvasUpdate;

        sealed class Listener : MonoBehaviour
        {
            void OnRectTransformDimensionsChange() => log.Add(name + (afterCanvasUpdate ? " after" : " during"));
        }

        static void WillRender() => afterCanvasUpdate = true;

        /// Runs a frame and returns the messages it sent, marking those after the canvas update.
        static string[] Run()
        {
            log.Clear();
            afterCanvasUpdate = false;
            Canvas.willRenderCanvases += WillRender;
            try { Frame(); }
            finally
            {
                Canvas.willRenderCanvases -= WillRender;
                afterCanvasUpdate = false;
            }
            return log.ToArray();
        }

        static RectTransform Rect(string name, Transform parent, bool renderer)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(50f, 40f);
            if (renderer) go.AddComponent<CanvasRenderer>();
            go.AddComponent<Listener>();
            return (RectTransform)go.transform;
        }

        sealed class Setup
        {
            public Canvas Canvas;
            public RectTransform P, C, Bare;
        }

        /// P (a CanvasRenderer) holding C (a CanvasRenderer) and Bare (no renderer), on an
        /// overlay canvas, two frames old.
        static Setup Build(bool pixelPerfect)
        {
            OniMoono.Engine.UI.CanvasSystem.Install();
            var s = new Setup();
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            s.Canvas = canvasGo.AddComponent<Canvas>();
            s.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            s.Canvas.pixelPerfect = pixelPerfect;
            s.P = Rect("P", canvasGo.transform, true);
            s.C = Rect("C", s.P, true);
            s.Bare = Rect("Bare", s.P, false);
            Frame();
            Assert.Empty(Run());
            log.Clear();
            return s;
        }

        [Fact]
        public void AMoveIsToldToEveryRendererMovedAfterTheCanvasUpdate()
        {
            var s = Build(pixelPerfect: true);
            s.P.anchoredPosition += new Vector2(10f, 0f);
            Assert.Equal(["P after", "C after"], Run());
            Assert.Empty(Run());
        }

        [Theory]
        [InlineData("localPosition")]
        [InlineData("localScale")]
        [InlineData("localRotation")]
        [InlineData("subpixel")]
        [InlineData("undone")]
        [InlineData("twice")]
        [InlineData("reparent")]
        public void EachKindOfMoveCountsOnce(string kind)
        {
            var s = Build(pixelPerfect: true);
            switch (kind)
            {
                case "localPosition": s.P.localPosition += new Vector3(0f, 10f, 0f); break;
                case "localScale": s.P.localScale = new Vector3(1.1f, 1.1f, 1f); break;
                case "localRotation": s.P.localRotation = Quaternion.Euler(0f, 0f, 10f); break;
                case "subpixel": s.P.anchoredPosition += new Vector2(0.3f, 0f); break;
                case "undone":
                    s.P.anchoredPosition += new Vector2(10f, 0f);
                    s.P.anchoredPosition -= new Vector2(10f, 0f);
                    break;
                case "twice":
                    s.P.anchoredPosition += new Vector2(10f, 0f);
                    s.P.anchoredPosition += new Vector2(10f, 0f);
                    break;
                default:
                    var other = new GameObject("Other", typeof(RectTransform));
                    other.transform.SetParent(s.Canvas.transform, false);
                    ((RectTransform)other.transform).anchoredPosition = new Vector2(30f, 0f);
                    Frame();
                    s.C.SetParent(other.transform, false);
                    Assert.Equal(["C after"], Run());
                    return;
            }
            Assert.Equal(["P after", "C after"], Run());
        }

        [Fact]
        public void AMoveMadeDuringTheCanvasUpdateIsToldAfterIt()
        {
            var s = Build(pixelPerfect: true);
            Canvas.WillRenderCanvases move = () => { if (!afterCanvasUpdate) s.C.anchoredPosition += new Vector2(5f, 0f); };
            Canvas.willRenderCanvases += move;
            try { Assert.Equal(["C after"], Run()); }
            finally { Canvas.willRenderCanvases -= move; }
        }

        [Fact]
        public void AValueSetToItselfIsNotAMove()
        {
            var s = Build(pixelPerfect: true);
            s.P.anchoredPosition = s.P.anchoredPosition;
            s.P.localPosition = s.P.localPosition;
            s.P.position = s.P.position;
            s.P.localScale = s.P.localScale;
            s.P.localRotation = s.P.localRotation;
            s.P.SetParent(s.P.parent, false);
            s.P.SetAsLastSibling();
            Assert.Empty(Run());
        }

        [Fact]
        public void AResizeInPlaceIsToldOnlyByTheSetter()
        {
            var s = Build(pixelPerfect: true);
            s.P.sizeDelta += new Vector2(10f, 0f);
            Assert.Equal(["P during"], log);
            Assert.Empty(Run());
        }

        [Fact]
        public void NothingIsToldOnACanvasThatIsNotPixelPerfect()
        {
            var s = Build(pixelPerfect: false);
            s.P.anchoredPosition += new Vector2(10f, 0f);
            s.P.localScale = new Vector3(1.1f, 1.1f, 1f);
            Assert.Empty(Run());
        }

        [Fact]
        public void ANestedCanvasThatOverridesPixelPerfectDecides()
        {
            var s = Build(pixelPerfect: true);
            var nested = s.C.gameObject.AddComponent<Canvas>();
            // overridePixelPerfect is not part of the surface (the game does not use it); its
            // serialized flag is set on the canvas's engine data.
            nested.Data.OverridePixelPerfect = true;
            nested.pixelPerfect = false;
            Frame();
            s.P.anchoredPosition += new Vector2(10f, 0f);
            Assert.Equal(["P after"], Run());

            s.Canvas.pixelPerfect = false;
            nested.pixelPerfect = true;
            Frame();
            s.P.anchoredPosition += new Vector2(10f, 0f);
            Assert.Equal(["C after"], Run());
        }

        [Fact]
        public void InactiveObjectsAreNotTold()
        {
            var s = Build(pixelPerfect: true);
            s.C.gameObject.SetActive(false);
            s.P.anchoredPosition += new Vector2(10f, 0f);
            Assert.Equal(["P after"], Run());

            // Activation is not a move.
            s.C.gameObject.SetActive(true);
            Assert.Empty(Run());
        }

        [Fact]
        public void AMoveWhileInactiveIsNotToldOnceActivated()
        {
            // born.inactiveActivatedMoved: moved while inactive, activated in the same frame.
            var s = Build(pixelPerfect: true);
            s.C.gameObject.SetActive(false);
            s.C.anchoredPosition += new Vector2(10f, 0f);
            s.C.gameObject.SetActive(true);
            Assert.Empty(Run());
        }

        [Fact]
        public void ANewObjectIsNotToldOfItsPlacement()
        {
            // born.control: made, parented and sized before its CanvasRenderer exists.
            var s = Build(pixelPerfect: true);
            Rect("Fresh", s.P, true);
            Assert.Empty(Run());
        }

        [Fact]
        public void ANewObjectMovedInTheFrameItWasMadeIsTold()
        {
            // born.moveNow, born.parentMoved: a move after the renderer exists is told after
            // that frame's canvas update, the frame it was made in.
            var s = Build(pixelPerfect: true);
            var fresh = Rect("Fresh", s.P, true);
            fresh.anchoredPosition += new Vector2(3f, 0f);
            Assert.Equal(["Fresh after"], Run());

            var other = Rect("Other", s.P, true);
            s.P.anchoredPosition += new Vector2(10f, 0f);
            Assert.Equal(["P after", "C after", "Fresh after", "Other after"], Run());
        }

        [Fact]
        public void AMoveBeforeTheRendererExistsIsNotTold()
        {
            // born.imageAfterMove.
            var s = Build(pixelPerfect: true);
            var fresh = Rect("Fresh", s.P, false);
            fresh.anchoredPosition += new Vector2(3f, 0f);
            fresh.gameObject.AddComponent<CanvasRenderer>();
            Assert.Empty(Run());
        }

        [Fact]
        public void ANewObjectPlacedByItsLayoutGroupInTheFrameItWasMadeIsTold()
        {
            // born.group: a child added to a layout group is placed in that frame's canvas
            // update, and told after it. The main menu's news tiles rely on this when their data
            // arrives in the frame the menu is made.
            var s = Build(pixelPerfect: true);
            var group = Rect("G", s.Canvas.transform, false);
            group.sizeDelta = new Vector2(500f, 100f);
            var layout = group.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleRight;
            Frame();
            log.Clear();
            var fresh = Rect("Fresh", group, true);
            var element = fresh.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.preferredWidth = 30f;
            element.preferredHeight = 30f;
            var messages = Run();
            Assert.Contains("Fresh during", messages);
            Assert.Equal("Fresh after", messages[^1]);
        }

        [Fact]
        public void AClonedObjectIsNotToldOfItsPlacementButOfItsMoves()
        {
            // born.clone, born.cloneMoved.
            var s = Build(pixelPerfect: true);
            Object.Instantiate(s.P.gameObject, s.Canvas.transform).name = "Clone";
            Assert.Empty(Run());

            var clone = (RectTransform)Object.Instantiate(s.P.gameObject, s.Canvas.transform).transform;
            clone.name = "Moved";
            clone.anchoredPosition += new Vector2(5f, 0f);
            Assert.Equal(["Moved after", "C after"], Run());
        }
    }
}
