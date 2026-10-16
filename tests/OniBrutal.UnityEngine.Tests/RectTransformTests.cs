using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
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
    }
}
