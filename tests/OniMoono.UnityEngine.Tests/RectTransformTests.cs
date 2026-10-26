using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    public class RectTransformTests : EngineTest
    {
        [Fact]
        public void InsetAndSizeFromParentEdgePlacesTheRectInsideThatEdge()
        {
            var parent = new GameObject("Parent", typeof(RectTransform)).GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(200, 100);
            var child = new GameObject("Child", typeof(RectTransform)).GetComponent<RectTransform>();
            child.SetParent(parent, false);
            child.pivot = new Vector2(0.5f, 0f);

            child.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, 10, 40);
            child.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 5, 20);

            Assert.Equal(new Vector2(1, 1), child.anchorMin);
            Assert.Equal(new Vector2(1, 1), child.anchorMax);
            Assert.Equal(new Vector2(40, 20), child.sizeDelta);
            Assert.Equal(new Vector2(-30, -25), child.anchoredPosition);
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Assert.Equal(new Vector3(50, 25, 0), corners[0]); // parent spans (-100, -50)..(100, 50)
            Assert.Equal(new Vector3(90, 45, 0), corners[2]);

            child.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 10, 40);
            child.GetWorldCorners(corners);
            Assert.Equal(new Vector2(0, 1), child.anchorMin);
            Assert.Equal(-90f, corners[0].x);
            Assert.Equal(-50f, corners[2].x);
        }

        [Theory]
        [InlineData(RectTransform.Edge.Left, 0f)]
        [InlineData(RectTransform.Edge.Left, 0.25f)]
        [InlineData(RectTransform.Edge.Right, 0f)]
        [InlineData(RectTransform.Edge.Right, 1f)]
        [InlineData(RectTransform.Edge.Top, 0.5f)]
        [InlineData(RectTransform.Edge.Top, 0.75f)]
        [InlineData(RectTransform.Edge.Bottom, 0.5f)]
        [InlineData(RectTransform.Edge.Bottom, 1f)]
        public void InsetAndSizeFromEachEdgeAndPivot(RectTransform.Edge edge, float pivot)
        {
            var parent = new GameObject("Parent", typeof(RectTransform)).GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(200, 100);
            var child = new GameObject("Child", typeof(RectTransform)).GetComponent<RectTransform>();
            child.SetParent(parent, false);
            child.anchorMin = new Vector2(0.2f, 0.3f);
            child.anchorMax = new Vector2(0.6f, 0.7f);
            child.sizeDelta = new Vector2(7, 9);
            child.anchoredPosition = new Vector2(3, 4);
            child.pivot = new Vector2(pivot, pivot);

            child.SetInsetAndSizeFromParentEdge(edge, 10, 30);

            int axis = edge is RectTransform.Edge.Left or RectTransform.Edge.Right ? 0 : 1;
            int other = 1 - axis;
            bool far = edge is RectTransform.Edge.Right or RectTransform.Edge.Top;
            Assert.Equal(far ? 1f : 0f, child.anchorMin[axis]);
            Assert.Equal(far ? 1f : 0f, child.anchorMax[axis]);
            Assert.Equal(30f, child.sizeDelta[axis]);
            Assert.Equal(far ? -10f - 30f * (1f - pivot) : 10f + 30f * pivot, child.anchoredPosition[axis], 4);
            // The other axis is untouched.
            Assert.Equal(new Vector2(0.2f, 0.3f)[other], child.anchorMin[other]);
            Assert.Equal(new Vector2(0.6f, 0.7f)[other], child.anchorMax[other]);
            Assert.Equal(new Vector2(7, 9)[other], child.sizeDelta[other]);
            Assert.Equal(new Vector2(3, 4)[other], child.anchoredPosition[other]);

            // The near side sits 10 from the edge (parent spans -100..100 by -50..50).
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            float half = axis == 0 ? 100f : 50f;
            float near = far ? corners[2][axis] : corners[0][axis];
            Assert.Equal(far ? half - 10f : -half + 10f, near, 3);
        }
    }
}
