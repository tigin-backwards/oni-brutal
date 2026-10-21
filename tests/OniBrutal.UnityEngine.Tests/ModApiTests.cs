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
    }
}
