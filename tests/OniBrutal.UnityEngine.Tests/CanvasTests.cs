using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class CanvasTests : EngineTest
    {
        [Fact]
        public void NestedCanvasSortsWithTheNearestOverridingAncestor()
        {
            var root = new GameObject("Root", typeof(RectTransform)).AddComponent<Canvas>();
            root.sortingOrder = 3;

            var overriding = new GameObject("Overriding", typeof(RectTransform));
            overriding.transform.SetParent(root.transform, false);
            var overridingCanvas = overriding.AddComponent<Canvas>();
            overridingCanvas.overrideSorting = true;
            overridingCanvas.sortingOrder = 20;

            // A plain object between the canvases does not interrupt inheritance.
            var plain = new GameObject("Plain", typeof(RectTransform));
            plain.transform.SetParent(overriding.transform, false);
            var nested = new GameObject("Nested", typeof(RectTransform));
            nested.transform.SetParent(plain.transform, false);
            var nestedCanvas = nested.AddComponent<Canvas>();
            nestedCanvas.sortingOrder = 7;

            var direct = new GameObject("Direct", typeof(RectTransform));
            direct.transform.SetParent(root.transform, false);
            var directCanvas = direct.AddComponent<Canvas>();
            directCanvas.sortingOrder = 9;

            Assert.Equal(20, overridingCanvas.sortingOrder);
            Assert.Equal(20, nestedCanvas.sortingOrder);
            Assert.Equal(3, directCanvas.sortingOrder);
            Assert.Equal(3, root.sortingOrder);

            // Turning the override on makes a canvas use its own value.
            nestedCanvas.overrideSorting = true;
            Assert.Equal(7, nestedCanvas.sortingOrder);
        }

        [Fact]
        public void InstantiateRemapsTheCanvasCameraIntoTheClone()
        {
            // The game's virtual cursor: a camera-space canvas whose camera is in the same prefab.
            var root = new GameObject("Controller");
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            var outside = new GameObject("Outside").AddComponent<Camera>();

            var clone = Object.Instantiate(root);
            var cloneCanvas = clone.GetComponentInChildren<Canvas>();
            var cloneCamera = clone.GetComponentInChildren<Camera>();
            Assert.Same(cloneCamera, cloneCanvas.worldCamera);
            Assert.NotSame(camera, cloneCamera);

            // A reference to an object outside the cloned hierarchy is kept.
            canvas.worldCamera = outside;
            Assert.Same(outside, Object.Instantiate(root).GetComponentInChildren<Canvas>().worldCamera);
        }
    }
}