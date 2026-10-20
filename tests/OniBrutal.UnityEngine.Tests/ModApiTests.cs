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
    }
}
