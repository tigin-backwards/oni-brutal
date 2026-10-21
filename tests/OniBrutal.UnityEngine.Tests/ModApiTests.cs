using System;
using System.IO;
using System.Linq;
using OniBrutal.Engine.Assets;
using UnityEngine;
using Xunit;
using Object = UnityEngine.Object;

namespace OniBrutal.Tests
{
    // Unity API that mods call although the game itself does not (tools/ApiCheck over the mod
    // assemblies of a real profile): BlueprintsV2's shared UtilLibs looks a bundle up among the
    // loaded ones and makes temporary render textures with a read/write mode.
    public class ModApiTests : EngineTest
    {
        static string DataDir => Path.Combine(Environment.GetEnvironmentVariable("ONI_GAME_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects", "oni-linux"), "OxygenNotIncluded_Data");

        [Fact]
        public void LoadedAssetBundlesAreListedUntilUnloaded()
        {
            AssetSystem.Initialize(DataDir);
            var bundle = AssetBundle.LoadFromFile(Path.Combine(DataDir, "StreamingAssets", "hires_base_bundle"));
            Assert.NotNull(bundle);
            try
            {
                Assert.Contains(bundle, AssetBundle.GetAllLoadedAssetBundles());
            }
            finally
            {
                bundle.Unload(true);
            }
            Assert.DoesNotContain(AssetBundle.GetAllLoadedAssetBundles(), b => ReferenceEquals(b, bundle));
        }

        // In the player, DestroyImmediate removes assets without allowDestroyingAssets (measured in
        // the original player on a mod's bundle prefab and a prefab of the game's own data; the
        // refusal is the editor's). BlueprintsV2 swaps its bundle prefabs' Text for LocText so.
        [Fact]
        public void DestroyImmediateRemovesAnAssetWithoutTheFlag()
        {
            AssetSystem.Initialize(DataDir);
            var file = AssetSystem.Get("sharedassets0.assets");
            // The file's last texture (Texture2D, class 28): an asset of the game's own data.
            var info = file.Objects.Last(o => file.TypeOf(o).ClassId == 28);
            var texture = ObjectLoader.Load(file, info.PathId);
            Assert.True(texture != null);
            Object.DestroyImmediate(texture);
            Assert.True(texture == null);
        }

        [Fact]
        public void TemporaryRenderTexturesTakeTheirReadWriteMode()
        {
            var linear = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var srgb = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Assert.False(linear.sRGB);
            Assert.True(srgb.sRGB);
            RenderTexture.ReleaseTemporary(linear);
            RenderTexture.ReleaseTemporary(srgb);
        }

        // Fast Track sets these on the renderers it creates (defaults and round trip measured in the
        // original player).
        [Fact]
        public void RenderersAllowOcclusionAndGenerateObjectMotionVectorsByDefault()
        {
            var go = new GameObject("Renderer");
            var renderer = go.AddComponent<MeshRenderer>();
            Assert.True(renderer.allowOcclusionWhenDynamic);
            Assert.Equal(MotionVectorGenerationMode.Object, renderer.motionVectorGenerationMode);
            renderer.allowOcclusionWhenDynamic = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            Assert.False(renderer.allowOcclusionWhenDynamic);
            Assert.Equal(MotionVectorGenerationMode.ForceNoMotion, renderer.motionVectorGenerationMode);
            Object.DestroyImmediate(go);
        }

        // Fast Track's tile meshes: the update flags (measured in the original player).
        // DontValidateIndices does not let fewer vertices than the triangles reference through;
        // DontRecalculateBounds keeps the bounds while the vertices change.
        [Fact]
        public void MeshUpdateFlagsKeepTheIndexCheckAndCanKeepTheBounds()
        {
            var mesh = new Mesh();
            mesh.SetVertices(new System.Collections.Generic.List<Vector3> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(5, 5, 0) });
            mesh.SetTriangles(new[] { 0, 1, 3 }, 0);
            var bounds = mesh.bounds;
            mesh.SetVertices(new System.Collections.Generic.List<Vector3> { new(0, 0, 0), new(2, 0, 0), new(0, 2, 0) }, 0, 3, UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
            Assert.Equal(4, mesh.vertexCount);
            mesh.SetVertices(new System.Collections.Generic.List<Vector3> { new(0, 0, 0), new(3, 0, 0), new(0, 3, 0), new(9, 9, 0) }, 0, 4, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            Assert.Equal(new Vector3(9, 9, 0), mesh.vertices[3]);
            Assert.Equal(bounds, mesh.bounds);
            mesh.SetUVs(0, new System.Collections.Generic.List<Vector2> { Vector2.zero, Vector2.one, Vector2.up, Vector2.right }, 0, 4, UnityEngine.Rendering.MeshUpdateFlags.DontValidateIndices);
            mesh.SetColors(new System.Collections.Generic.List<Color> { Color.red, Color.green, Color.blue, Color.white }, 0, 4, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            Assert.Equal(Vector2.one, mesh.uv[1]);
            Assert.Equal(Color.blue, mesh.colors[2]);
            Object.DestroyImmediate(mesh);
        }

        // When a mesh's bounds change (measured in the original player): with the number of vertices,
        // and by RecalculateBounds; not with new positions for as many vertices, triangles (their
        // submesh's bounds only) or Clear.
        [Fact]
        public void MeshBoundsFollowTheVertexCountOnly()
        {
            var far = new System.Collections.Generic.List<Vector3> { new(0, 0, 0), new(4, 0, 0), new(0, 4, 0), new(20, 20, 0) };
            var wide = new Vector3[] { new(0, 0, 0), new(3, 0, 0), new(0, 3, 0), new(9, 9, 0) };
            var tenTen = new Bounds(new Vector3(10, 10, 0), new Vector3(20, 20, 0));
            var mesh = new Mesh();
            mesh.SetVertices(far);
            Assert.Equal(tenTen, mesh.bounds);
            mesh.vertices = wide;
            Assert.Equal(tenTen, mesh.bounds);
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.triangles = new[] { 0, 1, 3 };
            Assert.Equal(tenTen, mesh.bounds);
            mesh.RecalculateBounds();
            Assert.Equal(new Bounds(new Vector3(4.5f, 4.5f, 0), new Vector3(9, 9, 0)), mesh.bounds);
            mesh.Clear();
            Assert.Equal(new Bounds(new Vector3(4.5f, 4.5f, 0), new Vector3(9, 9, 0)), mesh.bounds);
            mesh.SetVertices(far);
            mesh.SetVertices(new System.Collections.Generic.List<Vector3> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) });
            Assert.Equal(new Bounds(new Vector3(0.5f, 0.5f, 0), new Vector3(1, 1, 0)), mesh.bounds);
            mesh.bounds = new Bounds(new Vector3(100, 100, 0), Vector3.one);
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0, true);
            Assert.Equal(new Vector3(100, 100, 0), mesh.bounds.center);
            Object.DestroyImmediate(mesh);
        }

        // Rocketry Expanded builds gradients with SetKeys: both key sets, sorted by time, evaluated
        // between them.
        // Measured in the original player (PerfProbe's measure mode); mods' shared UI code parses
        // its colours so.
        [Theory]
        [InlineData("#FF0000", 255, 0, 0, 255)]
        [InlineData("#f00", 255, 0, 0, 255)]
        [InlineData("#F008", 255, 0, 0, 136)]
        [InlineData("#FF000080", 255, 0, 0, 128)]
        [InlineData(" #FF0000", 255, 0, 0, 255)]
        [InlineData("#FF0000 ", 255, 0, 0, 255)]
        [InlineData("RED", 255, 0, 0, 255)]
        [InlineData("grey", 128, 128, 128, 255)]
        [InlineData("lightblue", 173, 216, 230, 255)]
        [InlineData("#abc", 170, 187, 204, 255)]
        [InlineData("#ABCDEF12", 171, 205, 239, 18)]
        public void HtmlColorsParseAsInUnity(string text, byte r, byte g, byte b, byte a)
        {
            Assert.True(ColorUtility.TryParseHtmlString(text, out var color));
            Assert.Equal(new Color32(r, g, b, a), (Color32)color);
        }

        [Theory]
        [InlineData("#12345")]
        [InlineData("#1234567")]
        [InlineData("FF0000")]
        [InlineData("#GG0000")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("gray")]
        [InlineData("pink")]
        [InlineData("clear")]
        public void OtherHtmlColorsAreWhiteAndRefused(string text)
        {
            Assert.False(ColorUtility.TryParseHtmlString(text, out var color));
            Assert.Equal(Color.white, color);
        }

        // LineRenderer geometry as measured in the original player (BakeMesh with an orthographic
        // camera at z -10 looking along +Z), start colour red, end colour half-transparent blue.
        static (Vector3[] Positions, Vector2[] Uvs, Color32[] Colors, int[] Indices) Line(Action<LineRenderer> setup)
        {
            var line = new GameObject("line").AddComponent<LineRenderer>();
            line.startColor = new Color(1, 0, 0, 1);
            line.endColor = new Color(0, 0, 1, 0.5f);
            setup(line);
            var mesh = new OniBrutal.Engine.Rendering.MeshNative();
            var native = (OniBrutal.Engine.Rendering.LineRendererNative)line.NativeOrThrow;
            if (!native.BuildGeometry(new Vector3(0, 0, -10), Vector3.forward, Vector3.right, Vector3.up, false, mesh)) return (Array.Empty<Vector3>(), Array.Empty<Vector2>(), Array.Empty<Color32>(), Array.Empty<int>());
            return (mesh.Positions, mesh.Uvs[0].Select(u => new Vector2(u.x, u.y)).ToArray(), mesh.Colors.Select(c => (Color32)c).ToArray(), mesh.SubMeshes[0].Indices);
        }

        static void Near(Vector3 expected, Vector3 actual) => Assert.True((expected - actual).magnitude < 1e-3f, $"expected {expected}, got {actual}");

        [Fact]
        public void LineRenderersDefaultToTwoPointsOfUnitWidth()
        {
            var line = new GameObject("line").AddComponent<LineRenderer>();
            Assert.Equal(2, line.positionCount);
            Assert.Equal(new Vector3(0, 0, 1), line.GetPosition(1));
            Assert.Equal(1f, line.startWidth);
            Assert.Equal(1f, line.endWidth);
            Assert.True(line.useWorldSpace);
            Assert.Equal(LineAlignment.View, line.alignment);
            Assert.Equal(Vector2.one, line.textureScale);
            Assert.Equal((Color32)Color.white, (Color32)line.startColor);
        }

        [Fact]
        public void LineCornersAreMiteredAndClamped()
        {
            var two = Line(l => l.SetPositions(new[] { Vector3.zero, new Vector3(10, 0, 0) }));
            Near(new Vector3(0, -0.5f, 0), two.Positions[0]);
            Near(new Vector3(0, 0.5f, 0), two.Positions[1]);
            Assert.Equal(new[] { 0, 1, 2, 1, 3, 2 }, two.Indices);
            Assert.Equal(new Vector2(0, 1), two.Uvs[0]);
            Assert.Equal(new Color32(0, 0, 255, 128), two.Colors[3]);

            var corner = Line(l => { l.positionCount = 3; l.SetPositions(new[] { Vector3.zero, new Vector3(10, 0, 0), new Vector3(10, 10, 0) }); });
            Near(new Vector3(10.5f, -0.5f, 0), corner.Positions[2]);
            Near(new Vector3(9.5f, 0.5f, 0), corner.Positions[3]);
            Assert.Equal(new Color32(128, 0, 128, 192), corner.Colors[2]);

            var acute = Line(l => { l.positionCount = 3; l.SetPositions(new[] { Vector3.zero, new Vector3(10, 0, 0), new Vector3(0, 2, 0) }); });
            Near(new Vector3(11.4927f, -0.1478f, 0), acute.Positions[2]);
            Near(new Vector3(8.5073f, 0.1478f, 0), acute.Positions[3]);

            var loop = Line(l => { l.positionCount = 4; l.SetPositions(new[] { Vector3.zero, new Vector3(10, 0, 0), new Vector3(10, 10, 0), new Vector3(0, 10, 0) }); l.loop = true; });
            Assert.Equal(10, loop.Positions.Length);
            Near(new Vector3(-0.5f, -0.5f, 0), loop.Positions[0]);
            Near(new Vector3(0.5f, 0.5f, 0), loop.Positions[9]);
            Assert.Equal(new Vector2(0.25f, 1), loop.Uvs[2]);

            Assert.Empty(Line(l => { l.positionCount = 1; l.SetPosition(0, Vector3.one); }).Positions);
            var depth = Line(l => l.SetPositions(new[] { Vector3.zero, new Vector3(0, 0, 10) }));
            Near(new Vector3(-0.3536f, 0.3536f, 0), depth.Positions[0]);
        }

        [Fact]
        public void LineWidthsColoursAndUvsFollowTheLength()
        {
            var curve = Line(l => { l.positionCount = 3; l.SetPositions(new[] { Vector3.zero, new Vector3(4, 0, 0), new Vector3(10, 0, 0) }); l.widthMultiplier = 2; l.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.2f)); });
            Near(new Vector3(4, 0.7184f, 0), curve.Positions[3]);
            Near(new Vector3(10, 0.2f, 0), curve.Positions[5]);
            Assert.Equal(new Color32(153, 0, 102, 204), curve.Colors[2]);

            var widths = Line(l => { l.startWidth = 2; l.endWidth = 0.5f; l.SetPositions(new[] { Vector3.zero, new Vector3(10, 0, 0) }); });
            Near(new Vector3(0, 1, 0), widths.Positions[1]);
            Near(new Vector3(10, 0.25f, 0), widths.Positions[3]);

            Vector3[] three = { Vector3.zero, new Vector3(4, 0, 0), new Vector3(10, 0, 0) };
            var tile = Line(l => { l.positionCount = 3; l.SetPositions(three); l.widthMultiplier = 0.5f; l.textureMode = LineTextureMode.Tile; l.textureScale = new Vector2(2, 1); });
            Assert.Equal(new[] { 0f, 0f, 8f, 8f, 20f, 20f }, tile.Uvs.Select(u => u.x));
            var repeat = Line(l => { l.positionCount = 3; l.SetPositions(three); l.textureMode = LineTextureMode.RepeatPerSegment; });
            Assert.Equal(new[] { 0f, 0f, 1f, 1f, 2f, 2f }, repeat.Uvs.Select(u => u.x));
            var distribute = Line(l => { l.positionCount = 3; l.SetPositions(three); l.textureMode = LineTextureMode.DistributePerSegment; });
            Assert.Equal(new[] { 0f, 0f, 0.5f, 0.5f, 1f, 1f }, distribute.Uvs.Select(u => u.x));

            var gradient = Line(l =>
            {
                l.positionCount = 3;
                l.SetPositions(new[] { Vector3.zero, new Vector3(2, 0, 0), new Vector3(10, 0, 0) });
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.red, 0), new GradientColorKey(Color.green, 0.5f), new GradientColorKey(Color.blue, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.2f, 1) });
                l.colorGradient = g;
            });
            Assert.Equal(new Color32(153, 102, 0, 214), gradient.Colors[2]);
            Assert.Equal(new Color32(0, 0, 255, 51), gradient.Colors[4]);

            var local = Line(l => { l.useWorldSpace = false; l.transform.position = new Vector3(5, 5, 0); l.SetPositions(new[] { Vector3.zero, new Vector3(10, 0, 0) }); });
            Near(new Vector3(5, 4.5f, 0), local.Positions[0]);
        }

        [Fact]
        public void GradientsTakeColorAndAlphaKeys()
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.blue, 1f), new GradientColorKey(Color.red, 0f) },
                new[] { new GradientAlphaKey(0f, 1f), new GradientAlphaKey(1f, 0f) });
            Assert.Equal(0f, gradient.colorKeys[0].time);
            Assert.Equal(1f, gradient.alphaKeys[0].alpha);
            var middle = gradient.Evaluate(0.5f);
            Assert.Equal(0.5f, middle.r, 3);
            Assert.Equal(0.5f, middle.b, 3);
            Assert.Equal(0.5f, middle.a, 3);
        }

        // Fast Save's timelapse images (measured in the original player): RGBA32's graphics format
        // whatever isSRGB says, ARGB32's unnamed 88; PNGs from raw rows (bottom row first) with a
        // row stride, BGRA swizzled and R8 as grey.
        [Fact]
        public void PngsOfRawTexelsDecodeTheirFormat()
        {
            Assert.Equal(UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetGraphicsFormat(TextureFormat.RGBA32, true));
            Assert.Equal((UnityEngine.Experimental.Rendering.GraphicsFormat)88, UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetGraphicsFormat(TextureFormat.ARGB32, false));
            Assert.Equal(UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8_UNorm, UnityEngine.Experimental.Rendering.GraphicsFormatUtility.GetGraphicsFormat(TextureFormat.RGB24, false));
            var check = new Texture2D(1, 1);
            var rgb = ImageConversion.EncodeArrayToPNG(new byte[] { 255, 0, 0, 0, 255, 0, 9, 9, 0, 0, 255, 10, 20, 30, 9, 9 }, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8_UNorm, 2, 2, 8);
            Assert.True(check.LoadImage(rgb));
            Assert.Equal(new Color32[] { new(255, 0, 0, 255), new(0, 255, 0, 255), new(0, 0, 255, 255), new(10, 20, 30, 255) }, check.GetPixels32());
            Assert.True(check.LoadImage(ImageConversion.EncodeArrayToPNG(new byte[] { 255, 0, 0, 255 }, UnityEngine.Experimental.Rendering.GraphicsFormat.B8G8R8A8_UNorm, 1, 1)));
            Assert.Equal(new Color32(0, 0, 255, 255), check.GetPixels32()[0]);
            Assert.True(check.LoadImage(ImageConversion.EncodeArrayToPNG(new byte[] { 0, 100, 200, 255 }, UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm, 2, 2)));
            Assert.Equal(new Color32(100, 100, 100, 255), check.GetPixels32()[1]);
            Object.DestroyImmediate(check);
        }

        // A read-back is done three frames after the request (callback then) and its data is gone
        // the frame after (measured in the original player).
        [Fact]
        public void ReadbacksAreDoneThreeFramesLaterAndReadableForThatFrame()
        {
            var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false);
            texture.SetPixels32(new Color32[] { new(10, 20, 30, 40), new(50, 60, 70, 80) });
            texture.Apply();
            int calls = 0;
            var request = UnityEngine.Rendering.AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBA32, _ => calls++);
            Assert.False(request.done);
            Assert.False(request.hasError);
            Assert.Equal(2, request.width);
            Frame();
            Frame();
            Assert.False(request.done);
            Frame();
            Assert.True(request.done);
            Assert.Equal(1, calls);
            Assert.Equal(new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 }, request.GetData<byte>().ToArray());
            Frame();
            Assert.Throws<InvalidOperationException>(() => request.GetData<byte>());
            Object.DestroyImmediate(texture);
        }
    }
}
