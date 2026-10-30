using System.Collections.Generic;
using System.IO;
using System.Linq;
using OniMoono.Assets.Files;
using OniMoono.Assets.TypeTrees;
using Xunit;

namespace OniMoono.Assets.Tests
{
    /// Every native object in the game's data parses to exactly its byte size with the release
    /// type trees (MonoBehaviour payloads are checked by the engine, which knows script layouts).
    public class CorpusTests
    {
        const int MonoBehaviourId = 114;

        [Fact]
        public void EveryNativeObjectParsesExactly()
        {
            var failures = new List<string>();
            int parsed = 0;
            foreach (var name in GameData.SerializedFiles)
            {
                var file = SerializedFile.Open(Path.Combine(GameData.DataDir, name));
                if (!name.StartsWith("Resources/")) Assert.Equal(TypeTreeDatabase.UnityVersion, file.UnityVersion); // built-in resources carry Unity's own build stamp
                foreach (var obj in file.Objects)
                {
                    var type = file.TypeOf(obj);
                    if (type.ClassId == MonoBehaviourId) continue;
                    var info = TypeTreeDatabase.Get(type.ClassId);
                    try
                    {
                        TypeTreeReader.ReadNative(type.ClassId, file.OpenObject(obj));
                        parsed++;
                    }
                    catch (System.Exception e)
                    {
                        failures.Add($"{name}:{obj.PathId} {info.Name}: {e.Message}");
                    }
                }
            }
            Assert.True(failures.Count == 0, $"{failures.Count} failures (parsed {parsed}):\n" + string.Join("\n", failures.Take(30)));
            Assert.True(parsed > 10000);
        }

        [Fact]
        public void HiResBundleOpensAndItsEmbeddedTreesMatchItsObjects()
        {
            var archive = UnityFsArchive.Open(Path.Combine(GameData.DataDir, "StreamingAssets", "hires_base_bundle"));
            var cabNode = archive.Nodes.First(n => !n.Path.EndsWith(".resS"));
            var file = SerializedFile.Parse(cabNode.Path, archive.Read(cabNode));
            Assert.True(file.EnableTypeTree);
            foreach (var obj in file.Objects)
                TypeTreeReader.ReadObject(file.TypeOf(obj).Tree!, file.OpenObject(obj));
            // 390 high-resolution textures and their bundle's own objects: 395 in U59, 396 in U60.
            Assert.InRange(file.Objects.Count, 395, 396);
        }
    }
}
