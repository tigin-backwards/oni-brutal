using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests.UGui
{
    /// The canvas system's depths, as the original player gives them (measured with UnityProbe's
    /// UiSemantics, `UIS depth`, and before it PerfProbe's measure.txt): every CanvasRenderer on
    /// an active object is numbered when the canvas renders, depth first in hierarchy order, and
    /// the canvas renumbers only when its structure changes.
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
        public void EmptyAndTransparentGraphicsAreNumberedAndHitByTheirGroups()
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
            Assert.Equal([0, 1, 2, 3], new[] { e, h, s, unreachable.GetComponent<Graphic>().depth });
            Assert.Equal("Empty", FirstHit(system, new Vector2(312, 384)));
            Assert.Equal("Hidden", FirstHit(system, new Vector2(712, 384)));
            Assert.Equal("Shown", FirstHit(system, new Vector2(512, 584)));
            // Numbered, but its group lets raycasts through.
            Assert.Equal("none", FirstHit(system, new Vector2(512, 184)));

            quietGroup.blocksRaycasts = true;
            Frame();
            Assert.Equal("Unreachable", FirstHit(system, new Vector2(512, 184)));
            quietGroup.blocksRaycasts = false;
            Frame();
            Assert.Equal(3, unreachable.GetComponent<Graphic>().depth);
            Assert.Equal("none", FirstHit(system, new Vector2(512, 184)));
        }

        /// The probe's canvas (UiSemantics.Depth), less its TextMeshPro elements: renderers of
        /// every kind on one overlay canvas.
        sealed class Layout
        {
            public Canvas Canvas, Nested, Override;
            public RectTransform Root;
            public readonly List<(string Name, CanvasRenderer Renderer)> Watched = new();
            public CanvasGroup Quiet;
            public RectMask2D RectMask;
            public Mask Mask;
            public Image this[string name] => Renderer(name).GetComponent<Image>();
            public CanvasRenderer Renderer(string name) => Watched.Last(w => w.Name == name).Renderer;

            public Image Img(string name, Transform parent, Vector2 position, Vector2 size)
            {
                var rt = NewRect(name, parent, size.x, size.y);
                rt.anchoredPosition = position;
                var image = rt.gameObject.AddComponent<Image>();
                Watched.Add((name, image.canvasRenderer));
                return image;
            }

            public RectTransform Group(string name, Transform parent) => NewRect(name, parent, 50, 50);

            /// "name=abs" for each watched renderer, with "/rel" where they differ.
            public string Depths() => string.Join(" ", Watched.Select(w =>
                w.Renderer == null ? $"{w.Name}=destroyed"
                : w.Renderer.relativeDepth == w.Renderer.absoluteDepth ? $"{w.Name}={w.Renderer.absoluteDepth}"
                : $"{w.Name}={w.Renderer.absoluteDepth}/{w.Renderer.relativeDepth}"));
        }

        static Layout BuildLayout()
        {
            var l = new Layout { Canvas = NewCanvas() };
            l.Root = NewRect("Depth", l.Canvas.transform, 1024, 768);
            var root = l.Root;
            l.Img("A", root, new Vector2(-300, 0), new Vector2(20, 20));
            var b = l.Group("B", root);
            l.Watched.Add(("B", b.gameObject.AddComponent<CanvasRenderer>()));
            l.Img("B2child", l.Group("B2", root), Vector2.zero, new Vector2(10, 10));
            l.Img("C", root, new Vector2(-280, 0), new Vector2(20, 20)).color = new Color(1, 1, 1, 0);
            l.Img("D", root, new Vector2(-260, 0), new Vector2(20, 20)).enabled = false;
            l.Img("E", root, new Vector2(-240, 0), new Vector2(20, 20)).gameObject.SetActive(false);
            var f = l.Group("F", root);
            var blocking = f.gameObject.AddComponent<CanvasGroup>();
            blocking.alpha = 0;
            l.Img("F1", f, Vector2.zero, new Vector2(10, 10));
            var g = l.Group("G", root);
            l.Quiet = g.gameObject.AddComponent<CanvasGroup>();
            l.Quiet.alpha = 0;
            l.Quiet.blocksRaycasts = false;
            l.Img("G1", g, Vector2.zero, new Vector2(10, 10));
            var h = NewRect("H", root, 20, 20);
            h.anchoredPosition = new Vector2(-200, 0);
            l.RectMask = h.gameObject.AddComponent<RectMask2D>();
            l.Img("H1", h, new Vector2(200, 200), new Vector2(10, 10));
            l.Img("H2", h, Vector2.zero, new Vector2(10, 10));
            l.Img("I", root, new Vector2(5000, 5000), new Vector2(20, 20));
            var j = l.Group("J", root);
            l.Nested = j.gameObject.AddComponent<Canvas>();
            l.Img("J1", j, Vector2.zero, new Vector2(10, 10));
            var k = l.Group("K", root);
            l.Override = k.gameObject.AddComponent<Canvas>();
            l.Override.overrideSorting = true;
            l.Override.sortingOrder = 5;
            l.Img("K1", k, Vector2.zero, new Vector2(10, 10));
            l.Img("K2", k, Vector2.zero, new Vector2(10, 10));
            l.Img("P", root, Vector2.zero, Vector2.zero);
            var q = l.Img("Q", root, new Vector2(40, 0), new Vector2(30, 30));
            l.Mask = q.gameObject.AddComponent<Mask>();
            l.Img("Q1", q.transform, Vector2.zero, new Vector2(10, 10));
            l.Img("R", root, new Vector2(80, 0), new Vector2(30, 30));
            l.Img("S1", root, new Vector2(150, 0), new Vector2(10, 10));
            l.Img("S2", root, new Vector2(200, 0), new Vector2(10, 10));
            l.Img("Z", root, new Vector2(300, 0), new Vector2(10, 10));
            return l;
        }

        const string Numbered = "A=0 B=1 B2child=2 C=3 D=4 E=-1 F1=5 G1=6 H1=7 H2=8 I=9 J1=10/0 K1=0 K2=1 P=11 Q=12 Q1=13 R=15 S1=16 S2=17 Z=18";

        /// Every renderer on an active object is numbered: without a Graphic, a disabled Graphic,
        /// alpha 0 by colour or through a group (blocking raycasts or not), culled by a
        /// RectMask2D, off-screen, zero-size. A Mask takes one more number after its subtree
        /// (Q1=13, R=15). A nested canvas continues its parent's numbers, its relativeDepth from
        /// 0; an override-sorting canvas numbers its own from 0.
        [Fact]
        public void EveryRendererOnAnActiveObjectIsNumberedInHierarchyOrder()
        {
            var l = BuildLayout();
            Frame();
            Frame();
            Assert.True(l.Renderer("H1").cull);
            Assert.Equal(Numbered, l.Depths());
            Assert.Equal(10, l["J1"].depth);
        }

        /// Numbers are given when the canvas renders: a new renderer reads -1 through the frame's
        /// willRenderCanvases, then its number.
        [Fact]
        public void ARendererIsNumberedWhenTheCanvasRenders()
        {
            var l = BuildLayout();
            Frame();
            var y = l.Img("Y", l.Root, new Vector2(-320, 0), new Vector2(10, 10));
            y.transform.SetAsFirstSibling();
            var seen = new List<int>();
            Canvas.WillRenderCanvases look = () => seen.Add(y.canvasRenderer.absoluteDepth);
            Canvas.willRenderCanvases += look;
            try { Frame(); }
            finally { Canvas.willRenderCanvases -= look; }
            Assert.Equal([-1], seen);
            Assert.Equal(0, y.depth);
            Assert.Equal(1, l["A"].depth);
        }

        /// None of these renumber the canvas: a deactivated object keeps its number, and the
        /// others keep theirs (its gap stays).
        [Fact]
        public void ChangesThatLeaveTheStructureAloneDoNotRenumber()
        {
            var l = BuildLayout();
            Frame();
            var steps = new (string, System.Action)[]
            {
                ("colour", () => l["A"].color = Color.red),
                ("sprite", () => l["A"].sprite = NewSprite(4, 4)),
                ("moved", () => l["A"].rectTransform.anchoredPosition += new Vector2(3, 0)),
                ("group alpha", () => l.Quiet.alpha = 1),
                ("group raycasts", () => l.Quiet.blocksRaycasts = true),
                ("graphic disabled", () => l["A"].enabled = false),
                ("showMaskGraphic", () => l.Mask.showMaskGraphic = false),
                ("RectMask2D disabled", () => l.RectMask.enabled = false),
                ("deactivated", () => l["B2child"].gameObject.SetActive(false)),
            };
            foreach (var (what, act) in steps)
            {
                act();
                Frame();
                Frame();
                Assert.True(Numbered == l.Depths(), $"{what}: {l.Depths()}");
            }
        }

        /// Each of these renumbers the whole canvas.
        [Fact]
        public void StructuralChangesRenumberTheWholeCanvas()
        {
            var l = BuildLayout();
            Frame();

            // Deactivated: stale until something renumbers, then -1 and the gap closes.
            l["B2child"].gameObject.SetActive(false);
            Frame();
            Assert.Equal(2, l["B2child"].depth);
            l.Img("X", l.Root, new Vector2(320, 0), new Vector2(10, 10));
            Assert.Equal(-1, l.Renderer("X").absoluteDepth);
            Frame();
            Assert.Equal("A=0 B=1 B2child=-1 C=2 D=3 E=-1 F1=4 G1=5 H1=6 H2=7 I=8 J1=9/0 K1=0 K2=1 P=10 Q=11 Q1=12 R=14 S1=15 S2=16 Z=17 X=18", l.Depths());

            // Activated.
            l["B2child"].gameObject.SetActive(true);
            l["E"].gameObject.SetActive(true);
            Frame();
            Assert.Equal("A=0 B=1 B2child=2 C=3 D=4 E=5 F1=6 G1=7 H1=8 H2=9 I=10 J1=11/0 K1=0 K2=1 P=12 Q=13 Q1=14 R=16 S1=17 S2=18 Z=19 X=20", l.Depths());

            // Destroyed.
            Object.Destroy(l["C"].gameObject);
            Frame();
            Assert.Equal("A=0 B=1 B2child=2 C=destroyed D=3 E=4 F1=5 G1=6 H1=7 H2=8 I=9 J1=10/0 K1=0 K2=1 P=11 Q=12 Q1=13 R=15 S1=16 S2=17 Z=18 X=19", l.Depths());

            // A sibling order.
            l["A"].transform.SetSiblingIndex(6);
            Frame();
            Assert.Equal("A=6 B=0 B2child=1 C=destroyed D=2 E=3 F1=4 G1=5 H1=7 H2=8 I=9 J1=10/0 K1=0 K2=1 P=11 Q=12 Q1=13 R=15 S1=16 S2=17 Z=18 X=19", l.Depths());

            // The Mask disabled: its extra number goes.
            l.Mask.enabled = false;
            Frame();
            Assert.Equal("A=6 B=0 B2child=1 C=destroyed D=2 E=3 F1=4 G1=5 H1=7 H2=8 I=9 J1=10/0 K1=0 K2=1 P=11 Q=12 Q1=13 R=14 S1=15 S2=16 Z=17 X=18", l.Depths());

            // A renderer added to an active object.
            var b2 = l.Root.Find("B2").gameObject;
            l.Watched.Add(("B2", b2.AddComponent<Image>().canvasRenderer));
            Frame();
            Assert.Equal("A=7 B=0 B2child=2 C=destroyed D=3 E=4 F1=5 G1=6 H1=8 H2=9 I=10 J1=11/0 K1=0 K2=1 P=12 Q=13 Q1=14 R=15 S1=16 S2=17 Z=18 X=19 B2=1", l.Depths());

            // A nested canvas disabled: its elements read -1 at once.
            l.Nested.enabled = false;
            Assert.Equal(-1, l["J1"].depth);
            Assert.Equal(12, l["P"].depth);
            Frame();
            Assert.Equal("A=7 B=0 B2child=2 C=destroyed D=3 E=4 F1=5 G1=6 H1=8 H2=9 I=10 J1=-1 K1=0 K2=1 P=11 Q=12 Q1=13 R=14 S1=15 S2=16 Z=17 X=18 B2=1", l.Depths());
            l.Nested.enabled = true;
            Assert.Equal(-1, l["J1"].depth);
            Frame();
            Assert.Equal("A=7 B=0 B2child=2 C=destroyed D=3 E=4 F1=5 G1=6 H1=8 H2=9 I=10 J1=11/0 K1=0 K2=1 P=12 Q=13 Q1=14 R=15 S1=16 S2=17 Z=18 X=19 B2=1", l.Depths());

            // The canvas disabled and enabled: every element reads -1 until it renders.
            l.Canvas.enabled = false;
            l.Canvas.enabled = true;
            Assert.All(l.Watched.Where(w => w.Renderer != null), w => Assert.Equal(-1, w.Renderer.absoluteDepth));
            Frame();
            Assert.Equal("A=7 B=0 B2child=2 C=destroyed D=3 E=4 F1=5 G1=6 H1=8 H2=9 I=10 J1=11/0 K1=0 K2=1 P=12 Q=13 Q1=14 R=15 S1=16 S2=17 Z=18 X=19 B2=1", l.Depths());
        }

        /// The probe's late object: created active at the end, deactivated, moved to sibling 3,
        /// then activated, deactivated (keeping its number) and activated again.
        [Fact]
        public void AnObjectDeactivatedKeepsItsNumberUntilTheNextRenumbering()
        {
            var l = BuildLayout();
            Frame();
            var w = l.Img("W", l.Root, new Vector2(-330, 0), new Vector2(10, 10));
            w.gameObject.SetActive(false);
            w.transform.SetSiblingIndex(3);
            Frame();
            Assert.Equal(-1, w.depth);
            Assert.Equal(Numbered, string.Join(" ", l.Depths().Split(' ').Where(s => !s.StartsWith("W="))));
            w.gameObject.SetActive(true);
            Frame();
            Assert.Equal(3, w.depth);
            Assert.Equal(4, l["C"].depth);
            w.gameObject.SetActive(false);
            Frame();
            Assert.Equal(3, w.depth);
            Assert.Equal(4, l["C"].depth);
            w.gameObject.SetActive(true);
            w.enabled = false;
            Frame();
            Assert.Equal(3, w.depth);
            Assert.Equal(4, l["C"].depth);
        }

        /// A renderer moved under another parent of the same canvas is renumbered in its new place.
        [Fact]
        public void AReparentedRendererIsRenumbered()
        {
            var l = BuildLayout();
            Frame();
            l["A"].transform.SetParent(l["Z"].transform, true);
            Frame();
            Assert.Equal(17, l["Z"].depth);
            Assert.Equal(18, l["A"].depth);
            Assert.Equal(0, l.Renderer("B").absoluteDepth);
        }
    }
}
