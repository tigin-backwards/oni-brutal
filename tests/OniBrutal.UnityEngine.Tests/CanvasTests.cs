using System;
using OniBrutal.Engine;
using OniBrutal.Engine.Assets;
using OniBrutal.Engine.Rendering;
using UnityEngine;
using Xunit;
using Object = UnityEngine.Object;

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

        sealed class HierarchyListener : MonoBehaviour
        {
            public int Changes;
            void OnCanvasHierarchyChanged() => Changes++;
        }

        [Fact]
        public void EnablingOrDisablingACanvasTellsTheScriptsBelowIt()
        {
            var root = new GameObject("Root", typeof(RectTransform)).AddComponent<Canvas>();
            var nestedGo = new GameObject("Nested", typeof(RectTransform));
            nestedGo.transform.SetParent(root.transform, false);
            var nested = nestedGo.AddComponent<Canvas>();
            var child = new GameObject("Child", typeof(RectTransform));
            child.transform.SetParent(nestedGo.transform, false);
            var onCanvas = nestedGo.AddComponent<HierarchyListener>();
            var below = child.AddComponent<HierarchyListener>();
            var outside = root.gameObject.AddComponent<HierarchyListener>();

            nested.enabled = false;
            Assert.Equal(1, onCanvas.Changes);
            Assert.Equal(1, below.Changes);
            nested.enabled = true;
            Assert.Equal(2, below.Changes);
            // Scripts above the canvas are not told.
            Assert.Equal(0, outside.Changes);
        }

        // A root canvas in a screen-space mode is laid out (anchors, pivot, size, position, scale)
        // the moment it becomes enabled, as measured in the original player (PerfProbe measure.txt):
        // the game's hover card is built in the same frame its canvas, saved at scale zero, comes on.
        static GameObject ZeroScaledOverlay(string name, bool active)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(active);
            go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.transform.localScale = Vector3.zero;
            return go;
        }

        static void AssertLaidOut(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            var screen = new Vector2(Screen.width, Screen.height);
            Assert.Equal(Vector3.one, rt.localScale);
            Assert.Equal(screen, rt.sizeDelta);
            Assert.Equal(Vector2.zero, rt.anchorMin);
            Assert.Equal(Vector2.zero, rt.anchorMax);
            Assert.Equal(new Vector2(0.5f, 0.5f), rt.pivot);
            Assert.Equal(screen * 0.5f, rt.anchoredPosition);
        }

        [Fact]
        public void ARootScreenSpaceCanvasIsLaidOutWhenActivated()
        {
            var go = ZeroScaledOverlay("Activated", active: false);
            go.SetActive(true);
            AssertLaidOut(go);

            var again = ZeroScaledOverlay("Reactivated", active: true);
            again.SetActive(false);
            again.SetActive(true);
            AssertLaidOut(again);
        }

        [Fact]
        public void ARootScreenSpaceCanvasIsLaidOutWhenItsComponentIsEnabled()
        {
            var go = ZeroScaledOverlay("Toggled", active: true);
            var canvas = go.GetComponent<Canvas>();
            canvas.enabled = false;
            canvas.enabled = true;
            AssertLaidOut(go);
        }

        [Fact]
        public void AnInstantiatedRootScreenSpaceCanvasIsLaidOut()
        {
            AssertLaidOut(Object.Instantiate(ZeroScaledOverlay("ActiveSource", active: true)));
            var fromInactive = Object.Instantiate(ZeroScaledOverlay("InactiveSource", active: false));
            Assert.Equal(Vector3.zero, fromInactive.transform.localScale);
            fromInactive.SetActive(true);
            AssertLaidOut(fromInactive);
        }

        [Fact]
        public void AChildParentedKeepingItsWorldScaleUnderAJustActivatedCanvasKeepsScaleOne()
        {
            // HoverTextDrawer's pools: new GameObject, a RectTransform, SetParent(screen).
            var canvas = ZeroScaledOverlay("HoverCanvas", active: false);
            canvas.SetActive(true);
            var root = new GameObject("PoolRoot");
            root.AddComponent<RectTransform>().SetParent(canvas.transform);
            Assert.Equal(Vector3.one, root.transform.localScale);
        }

        [Fact]
        public void NestedWorldSpaceOrMovedCanvasesAreNotLaidOutOnEnable()
        {
            var host = new GameObject("Host", typeof(RectTransform));
            host.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var nested = ZeroScaledOverlay("Nested", active: false);
            nested.transform.SetParent(host.transform, false);
            nested.transform.localScale = Vector3.zero;
            nested.SetActive(true);
            Assert.Equal(Vector3.zero, nested.transform.localScale);

            // AddComponent<Canvas> starts in world space: a Dropdown's template canvas, cloned at
            // the root and nested again, keeps its anchoring.
            var worldSpace = new GameObject("WorldSpace", typeof(RectTransform));
            worldSpace.SetActive(false);
            worldSpace.AddComponent<Canvas>();
            worldSpace.transform.localScale = Vector3.zero;
            worldSpace.SetActive(true);
            Assert.Equal(Vector3.zero, worldSpace.transform.localScale);

            // Moving an enabled canvas to the root does not lay it out.
            var moved = ZeroScaledOverlay("Moved", active: true);
            moved.transform.SetParent(host.transform, false);
            moved.transform.localScale = Vector3.zero;
            moved.transform.SetParent(null, false);
            Assert.Equal(Vector3.zero, moved.transform.localScale);
        }

        sealed class DimensionsListener : MonoBehaviour
        {
            public static bool InUpdate;
            public readonly System.Collections.Generic.List<bool> Received = new();
            void OnRectTransformDimensionsChange() => Received.Add(InUpdate);
        }

        // As measured in the original player (PerfProbe layout.txt on the main menu's news tiles):
        // a RectTransform resized or moved during the canvas update (uGUI's layout pass) gets
        // OnRectTransformDimensionsChange then and again once the update is over, so a layout
        // element that changes its preferred size on it can ask for another layout pass.
        [Fact]
        public void ARectChangedDuringTheCanvasUpdateHearsOfItAgainAfterIt()
        {
            OniBrutal.Engine.UI.CanvasSystem.Install();
            var canvas = new GameObject("Canvas", typeof(RectTransform));
            canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var child = new GameObject("Child", typeof(RectTransform));
            child.transform.SetParent(canvas.transform, false);
            var listener = child.AddComponent<DimensionsListener>();
            Frame();
            listener.Received.Clear();
            Canvas.WillRenderCanvases resize = () =>
            {
                DimensionsListener.InUpdate = true;
                ((RectTransform)child.transform).sizeDelta += new Vector2(10f, 0f);
                DimensionsListener.InUpdate = false;
            };
            Canvas.willRenderCanvases += resize;
            try { Frame(); }
            finally { Canvas.willRenderCanvases -= resize; }
            Assert.Equal([true, false], listener.Received);
            Frame();
            Assert.Equal([true, false], listener.Received);
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

        [Fact]
        public void AnElementDestroyedOrClearedAfterTheCanvasBuildIsNotDrawn()
        {
            // The game's video screen, ending on its own, destroys its elements at once after the
            // canvases were built for the frame (both in PostLateUpdate); the frame still renders.
            var game = Environment.GetEnvironmentVariable("ONI_GAME_DIR")
                       ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects", "oni-linux");
            AssetSystem.Initialize(System.IO.Path.Combine(game, "OxygenNotIncluded_Data"));
            ProjectSettings.Load();
            FrameRenderer.Install();
            OniBrutal.Engine.UI.CanvasSystem.Install();
            var device = new RecordingDevice();
            var previous = GraphicsDevice.Current;
            GraphicsDevice.Install(device);
            var canvasGo = new GameObject("Overlay", typeof(RectTransform));
            Action destroy = null;
            int exceptions = 0;
            Application.LogCallback count = (_, _, type) => { if (type == LogType.Exception) exceptions++; };
            Application.logMessageReceived += count;
            try
            {
                canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var element = new GameObject("Element", typeof(RectTransform));
                element.transform.SetParent(canvasGo.transform, false);
                var renderer = element.AddComponent<CanvasRenderer>();
                renderer.SetMesh(new Mesh { vertices = [Vector3.zero, new Vector3(0f, 100f), new Vector3(100f, 0f)], triangles = [0, 1, 2] });
                renderer.materialCount = 1;
                renderer.SetMaterial(new Material(Shader.Find("UI/Default")), 0);
                Frame();
                Assert.NotEmpty(device.MeshDraws);

                // Cleared after the build (VideoScreen.Stop clears its RawImage's texture).
                device.MeshDraws.Clear();
                destroy = () => renderer.Clear();
                PlayerLoop.PostLateUpdate += destroy;
                Frame();
                Assert.Empty(device.MeshDraws);
                PlayerLoop.PostLateUpdate -= destroy;
                renderer.SetMesh(new Mesh { vertices = [Vector3.zero, new Vector3(0f, 100f), new Vector3(100f, 0f)], triangles = [0, 1, 2] });
                renderer.materialCount = 1;
                renderer.SetMaterial(new Material(Shader.Find("UI/Default")), 0);
                Frame();
                Assert.NotEmpty(device.MeshDraws);

                // Destroyed after the build.
                device.MeshDraws.Clear();
                destroy = () => Object.DestroyImmediate(element);
                PlayerLoop.PostLateUpdate += destroy;
                Frame();
                Assert.Empty(device.MeshDraws);
                Assert.Equal(0, exceptions);
            }
            finally
            {
                Application.logMessageReceived -= count;
                if (destroy != null) PlayerLoop.PostLateUpdate -= destroy;
                Object.DestroyImmediate(canvasGo);
                GraphicsDevice.Install(previous);
            }
        }
    }
}