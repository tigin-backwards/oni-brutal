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
