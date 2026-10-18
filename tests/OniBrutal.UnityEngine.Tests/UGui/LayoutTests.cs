using UnityEngine;
using UnityEngine.UI;
using Xunit;

namespace OniBrutal.Tests.UGui
{
    public class LayoutTests : UGuiTest
    {
        static LayoutElement Sized(RectTransform rect, float min, float preferred, float flexible)
        {
            var e = rect.gameObject.AddComponent<LayoutElement>();
            e.minWidth = min;
            e.preferredWidth = preferred;
            e.flexibleWidth = flexible;
            e.minHeight = 10;
            e.preferredHeight = 20;
            return e;
        }

        /// A 300x100 horizontal group with three children.
        (HorizontalLayoutGroup Group, RectTransform[] Children) Row(float[] min, float[] preferred, float[] flexible)
        {
            var canvas = NewCanvas();
            var group = NewRect("Group", canvas.transform, 300, 100, typeof(HorizontalLayoutGroup)).GetComponent<HorizontalLayoutGroup>();
            var children = new RectTransform[min.Length];
            for (int i = 0; i < min.Length; i++)
            {
                children[i] = NewRect("Child" + i, group.transform, 1, 1);
                Sized(children[i], min[i], preferred[i], flexible[i]);
            }
            return (group, children);
        }

        static float Left(RectTransform r) => r.localPosition.x + r.rect.xMin;
        static float Top(RectTransform r) => r.localPosition.y + r.rect.yMax;

        [Fact]
        public void SpaceBetweenMinimumAndPreferredIsShared()
        {
            var (group, c) = Row([10, 10, 10], [110, 110, 110], [0, 0, 0]);
            group.childForceExpandWidth = false;
            group.spacing = 0;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            // 300 lies at 90% of the way from 30 to 330.
            foreach (var r in c) Assert.Equal(100f, r.rect.width, 3);
            Assert.Equal(-150f, Left(c[0]), 3);
            Assert.Equal(330f, group.preferredWidth);
            Assert.Equal(30f, group.minWidth);
        }

        [Fact]
        public void PaddingSpacingAndAlignmentPlaceUncontrolledFlexibleSpace()
        {
            var (group, c) = Row([0, 0], [50, 50], [0, 0]);
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.padding = new RectOffset(10, 20, 5, 0);
            group.spacing = 4;
            group.childAlignment = TextAnchor.MiddleCenter;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            // Preferred 10 + 50 + 4 + 50 + 20 = 134; the 166 left over is split by the alignment.
            Assert.Equal(134f, group.preferredWidth);
            Assert.Equal(-150f + 10f + 83f, Left(c[0]), 3);
            Assert.Equal(Left(c[0]) + 54f, Left(c[1]), 3);
            // Across: the preferred height 20, centred in the 95 below the top padding.
            Assert.Equal(20f, c[0].rect.height, 3);
            Assert.Equal(50f - 5f - (95f - 20f) / 2f, Top(c[0]), 3);
        }

        [Fact]
        public void FlexibleAndForceExpandShareTheSurplus()
        {
            var (group, c) = Row([0, 0], [50, 50], [1, 3]);
            group.childForceExpandWidth = false;
            group.spacing = 0;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            Assert.Equal(50f + 200f * 0.25f, c[0].rect.width, 3);
            Assert.Equal(50f + 200f * 0.75f, c[1].rect.width, 3);

            // Force-expand counts each child as at least 1 flexible.
            var (group2, d) = Row([0, 0], [50, 50], [0, 0]);
            group2.childForceExpandWidth = true;
            group2.spacing = 0;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group2.transform);
            Assert.Equal(150f, d[0].rect.width, 3);
            Assert.Equal(150f, d[1].rect.width, 3);
            // Force-expanded height: the child fills the group across.
            Assert.Equal(100f, d[0].rect.height, 3);
        }

        [Fact]
        public void UncontrolledChildrenKeepTheirSize()
        {
            var (group, c) = Row([0, 0], [50, 50], [0, 0]);
            group.childControlWidth = false;
            group.childForceExpandWidth = false;
            c[0].sizeDelta = new Vector2(30, 10);
            c[1].sizeDelta = new Vector2(70, 10);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            Assert.Equal(30f, c[0].rect.width);
            Assert.Equal(70f, c[1].rect.width);
            Assert.Equal(-150f, Left(c[0]), 3);
            Assert.Equal(-120f, Left(c[1]), 3);
        }

        [Fact]
        public void IgnoredAndInactiveChildrenAreLeftOut()
        {
            var (group, c) = Row([0, 0, 0], [50, 50, 50], [0, 0, 0]);
            group.childForceExpandWidth = false;
            c[1].GetComponent<LayoutElement>().ignoreLayout = true;
            c[2].gameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            Assert.Equal(50f, group.preferredWidth);
        }

        [Fact]
        public void VerticalGroupStacksFromTheTop()
        {
            var canvas = NewCanvas();
            var group = NewRect("Group", canvas.transform, 100, 300, typeof(VerticalLayoutGroup)).GetComponent<VerticalLayoutGroup>();
            group.childForceExpandHeight = false;
            var a = NewRect("A", group.transform, 1, 1);
            var b = NewRect("B", group.transform, 1, 1);
            Sized(a, 0, 0, 0);
            Sized(b, 0, 0, 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            Assert.Equal(150f, Top(a), 3);
            Assert.Equal(130f, Top(b), 3);
            Assert.Equal(100f, a.rect.width, 3);
        }

        [Fact]
        public void GridPlacesCellsFromTheStartCorner()
        {
            var canvas = NewCanvas();
            var grid = NewRect("Grid", canvas.transform, 250, 250, typeof(GridLayoutGroup)).GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(50, 40);
            grid.spacing = new Vector2(10, 10);
            var cells = new RectTransform[5];
            for (int i = 0; i < 5; i++) cells[i] = NewRect("Cell" + i, grid.transform, 1, 1);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)grid.transform);
            // Four columns of 60 fit in 250.
            Assert.Equal(new Vector2(50, 40), cells[0].rect.size);
            Assert.Equal(-125f, Left(cells[0]), 3);
            Assert.Equal(125f, Top(cells[0]), 3);
            Assert.Equal(-125f + 180f, Left(cells[3]), 3);
            Assert.Equal(-125f, Left(cells[4]), 3);
            Assert.Equal(125f - 50f, Top(cells[4]), 3);

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.startCorner = GridLayoutGroup.Corner.UpperRight;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)grid.transform);
            Assert.Equal(-125f + 60f, Left(cells[0]), 3);
            Assert.Equal(-125f, Left(cells[1]), 3);
            Assert.Equal(2 * (50f + 10f) - 10f, grid.preferredWidth);
            Assert.Equal(3 * 50f - 10f, grid.preferredHeight);
        }

        [Fact]
        public void LayoutPropertiesTakeTheHighestPriorityThatSetsThem()
        {
            var canvas = NewCanvas();
            var rect = NewRect("Element", canvas.transform, 10, 10, typeof(Image));
            var image = rect.GetComponent<Image>();
            image.sprite = NewSprite(40, 30);
            Assert.Equal(40f, LayoutUtility.GetPreferredWidth(rect));
            var e = rect.gameObject.AddComponent<LayoutElement>();
            e.preferredWidth = 25;
            Assert.Equal(25f, LayoutUtility.GetPreferredWidth(rect));
            // Unset (-1): the image's value stands.
            e.preferredWidth = -1;
            Assert.Equal(40f, LayoutUtility.GetPreferredWidth(rect));
            // The preferred size is at least the minimum.
            e.minWidth = 60;
            Assert.Equal(60f, LayoutUtility.GetPreferredWidth(rect));
            // Nothing sets a flexible width: the default, 0.
            Assert.Equal(0f, LayoutUtility.GetFlexibleWidth(rect));
        }

        [Fact]
        public void ContentSizeFitterSizesToThePreferredSizeKeepingThePivot()
        {
            var canvas = NewCanvas();
            var rect = NewRect("Fitted", canvas.transform, 10, 10, typeof(ContentSizeFitter));
            rect.pivot = new Vector2(0, 1);
            var e = rect.gameObject.AddComponent<LayoutElement>();
            e.preferredWidth = 120;
            e.minHeight = 30;
            var fitter = rect.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.MinSize;
            Canvas.ForceUpdateCanvases();
            Assert.Equal(new Vector2(120, 30), rect.rect.size);
            Assert.Equal(Vector2.zero, rect.anchoredPosition);
        }

        [Fact]
        public void AspectRatioFitterModes()
        {
            var canvas = NewCanvas();
            var parent = NewRect("Parent", canvas.transform, 400, 100);
            var rect = NewRect("Fitted", parent, 200, 10, typeof(AspectRatioFitter));
            var fitter = rect.GetComponent<AspectRatioFitter>();
            fitter.aspectRatio = 2;
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            Assert.Equal(100f, rect.rect.height, 3);
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            Assert.Equal(200f, rect.rect.width, 3);
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            Assert.Equal(new Vector2(200, 100), rect.rect.size);
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            Assert.Equal(new Vector2(400, 200), rect.rect.size);
            Assert.Equal(Vector2.zero, rect.anchoredPosition);
        }

        [Fact]
        public void CanvasScalerScalesWithTheScreen()
        {
            var canvas = NewCanvas();
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(512, 768);
            scaler.matchWidthOrHeight = 0.5f;
            Canvas.ForceUpdateCanvases();
            // log2 of 2 (width) and 1 (height), blended half way: 2^0.5.
            Assert.Equal(Mathf.Sqrt(2f), canvas.scaleFactor, 4);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
            Canvas.ForceUpdateCanvases();
            Assert.Equal(2f, canvas.scaleFactor, 4);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1.5f;
            Canvas.ForceUpdateCanvases();
            Assert.Equal(1.5f, canvas.scaleFactor, 4);
        }
    }
}
