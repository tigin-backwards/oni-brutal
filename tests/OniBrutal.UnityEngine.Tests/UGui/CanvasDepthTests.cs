using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests.UGui
{
    /// The canvas system's depths, as the original player gives them (measured with PerfProbe's
    /// measure.txt): an element without geometry (the game's NonDrawingGraphic) and one under a
    /// CanvasGroup at alpha 0 are numbered and hit by raycasts; a transparent subtree that also
    /// stops raycasts is not numbered.
    public class CanvasDepthTests : UGuiTest
    {
        /// Klei's NonDrawingGraphic: a raycast target that draws nothing.
        sealed class EmptyGraphic : Graphic
        {
            public override void SetMaterialDirty() { }
            public override void SetVerticesDirty() { }
            protected override void OnPopulateMesh(VertexHelper vh) => vh.Clear();
        }

        static string FirstHit(EventSystem system, Vector2 position)
        {
            var hits = new List<RaycastResult>();
            system.RaycastAll(new PointerEventData(system) { position = position }, hits);
            return hits.Count > 0 ? hits[0].gameObject.name : "none";
        }

        [Fact]
        public void EmptyAndTransparentGraphicsAreNumberedAndHit()
        {
            var system = new GameObject("EventSystem").AddComponent<EventSystem>();
            var canvas = NewCanvas();
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var empty = NewRect("Empty", canvas.transform, 80, 80, typeof(EmptyGraphic));
            empty.anchoredPosition = new Vector2(-200, 0);
            var group = NewRect("Group", canvas.transform, 1024, 768);
            group.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            var hidden = NewRect("Hidden", group, 80, 80, typeof(Image));
            hidden.anchoredPosition = new Vector2(200, 0);
            var shown = NewRect("Shown", canvas.transform, 80, 80, typeof(Image));
            shown.anchoredPosition = new Vector2(0, 200);
            var quiet = NewRect("Quiet", canvas.transform, 1024, 768);
            var quietGroup = quiet.gameObject.AddComponent<CanvasGroup>();
            quietGroup.alpha = 0f;
            quietGroup.blocksRaycasts = false;
            var unreachable = NewRect("Unreachable", quiet, 80, 80, typeof(Image));
            unreachable.anchoredPosition = new Vector2(0, -200);
            Frame();
            Frame();

            int e = empty.GetComponent<Graphic>().depth, h = hidden.GetComponent<Graphic>().depth, s = shown.GetComponent<Graphic>().depth;
            Assert.True(e >= 0 && e < h && h < s, $"depths {e} {h} {s}");
            Assert.Equal(-1, unreachable.GetComponent<Graphic>().depth);
            Assert.Equal("Empty", FirstHit(system, new Vector2(312, 384)));
            Assert.Equal("Hidden", FirstHit(system, new Vector2(712, 384)));
            Assert.Equal("Shown", FirstHit(system, new Vector2(512, 584)));
            Assert.Equal("none", FirstHit(system, new Vector2(512, 184)));

            // Letting raycasts through the quiet group numbers its element; stopping them again
            // takes it out.
            quietGroup.blocksRaycasts = true;
            Frame();
            Assert.True(unreachable.GetComponent<Graphic>().depth >= 0);
            Assert.Equal("Unreachable", FirstHit(system, new Vector2(512, 184)));
            quietGroup.blocksRaycasts = false;
            Frame();
            Assert.Equal(-1, unreachable.GetComponent<Graphic>().depth);
        }
    }
}
