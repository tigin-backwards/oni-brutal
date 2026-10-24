using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests.UGui
{
    /// Children of layout groups moved from code (ugui.md, "Moved children of layout groups",
    /// measured with UnityProbe's layoutcull scenario, `LC lay` lines): a move re-lays out
    /// nothing and stays until the group re-lays out for another reason; a size change re-lays
    /// out in that frame.
    public class MovedLayoutChildTests : UGuiTest
    {
        sealed class CountingGroup : VerticalLayoutGroup
        {
            public int Horizontal;
            public override void SetLayoutHorizontal()
            {
                Horizontal++;
                base.SetLayoutHorizontal();
            }
        }

        sealed class DimensionsCounter : MonoBehaviour
        {
            public int Count;
            void OnRectTransformDimensionsChange() => Count++;
        }

        CountingGroup group;
        RectTransform a, b, c;
        DimensionsCounter bDims;

        /// G: a 300 x 400 vertical group (spacing 10, upper left, no force-expand) with A, B and
        /// C, each an Image with a LayoutElement (preferred 120 x 50) and its own size 80 x 40.
        void Build(bool fitter, bool controlSize, bool bFitter = false, bool bIgnored = false)
        {
            var canvas = NewCanvas();
            var g = NewRect("G", canvas.transform, 300, 400);
            group = g.gameObject.AddComponent<CountingGroup>();
            group.spacing = 10;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
            group.childControlWidth = group.childControlHeight = controlSize;
            if (fitter) g.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RectTransform Child(string name)
            {
                var r = NewRect(name, g, 80, 40, typeof(Image));
                var e = r.gameObject.AddComponent<LayoutElement>();
                e.preferredWidth = 120;
                e.preferredHeight = 50;
                return r;
            }
            a = Child("A");
            b = Child("B");
            c = Child("C");
            bDims = b.gameObject.AddComponent<DimensionsCounter>();
            if (bFitter) b.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (bIgnored) b.GetComponent<LayoutElement>().ignoreLayout = true;
            for (int i = 0; i < 4; i++) Frame();
            bDims.Count = 0;
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void AMovedChildStaysWithoutALayoutPass(bool fitter, bool controlSize)
        {
            Build(fitter, controlSize);
            var laidOut = b.anchoredPosition;
            int calls = group.Horizontal;
            b.anchoredPosition = laidOut + new Vector2(-200, 0);
            b.localPosition += new Vector3(0, 7, 0);
            b.localScale = new Vector3(1.5f, 1.5f, 1f);
            ((RectTransform)group.transform).anchoredPosition += new Vector2(13, 0);
            var moved = b.anchoredPosition;
            for (int i = 0; i < 3; i++) Frame();
            Assert.Equal(moved, b.anchoredPosition);
            Assert.Equal(calls, group.Horizontal);
            Assert.Equal(0, bDims.Count);

            // The next layout pass, for any reason, puts it back (below A, 10 taller when the
            // group controls the heights).
            a.GetComponent<LayoutElement>().preferredHeight = 60;
            Frame();
            Assert.Equal(laidOut.x, b.anchoredPosition.x, 3);
            Assert.Equal(laidOut.y - (controlSize ? 10 : 0), b.anchoredPosition.y, 3);
        }

        // The DraggableUI case: B with a ContentSizeFitter of its own, set every frame of a drag.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ADraggedChildWithItsOwnFitterIsNotLaidOutAgain(bool controlSize)
        {
            Build(fitter: false, controlSize, bFitter: true);
            var start = b.anchoredPosition;
            int calls = group.Horizontal;
            for (int i = 1; i <= 3; i++)
            {
                b.anchoredPosition = start + new Vector2(-100 * i, 0);
                Frame();
            }
            Frame();
            Frame();
            Assert.Equal(start + new Vector2(-300, 0), b.anchoredPosition);
            Assert.Equal(calls, group.Horizontal);
        }

        [Fact]
        public void AnIgnoredChildKeepsItsPlaceThroughALayoutPass()
        {
            Build(fitter: true, controlSize: true, bIgnored: true);
            b.anchoredPosition += new Vector2(-200, 0);
            var moved = b.anchoredPosition;
            a.GetComponent<LayoutElement>().preferredHeight = 60;
            Frame();
            Assert.Equal(moved, b.anchoredPosition);
        }

        [Fact]
        public void AResizedChildIsLaidOutInThatFrameAndHearsOfItOnce()
        {
            Build(fitter: true, controlSize: true);
            int calls = group.Horizontal;
            b.sizeDelta += new Vector2(30, 0);
            Assert.Equal(1, bDims.Count);
            Frame();
            Assert.Equal(120f, b.rect.width, 3);
            Assert.Equal(calls + 1, group.Horizontal);
            // From the setter, then once from the group's setting it back.
            Assert.Equal(2, bDims.Count);
            Frame();
            Assert.Equal(calls + 1, group.Horizontal);
            Assert.Equal(2, bDims.Count);
        }

        [Fact]
        public void AnUncontrolledSizeIsKeptAndTheChildPlacedAgain()
        {
            Build(fitter: true, controlSize: false);
            b.sizeDelta += new Vector2(30, 0);
            Frame();
            Assert.Equal(110f, b.rect.width, 3);
            // Upper left: the group's left edge (-150) plus half the width, the pivot at its centre.
            Assert.Equal(-150f + 55f, b.localPosition.x, 3);
        }

        [Fact]
        public void SettingAValueItHasDoesNotPutAMovedChildBack()
        {
            Build(fitter: true, controlSize: true);
            b.anchoredPosition += new Vector2(-200, 0);
            var moved = b.anchoredPosition;
            b.GetComponent<Image>().SetVerticesDirty();
            b.GetComponent<Image>().color = Color.red;
            b.GetComponent<LayoutElement>().preferredHeight = 50;
            group.spacing = 10;
            Canvas.ForceUpdateCanvases();
            Frame();
            Assert.Equal(moved, b.anchoredPosition);
        }
    }
}
