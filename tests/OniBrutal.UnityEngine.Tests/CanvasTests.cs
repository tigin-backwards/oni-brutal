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