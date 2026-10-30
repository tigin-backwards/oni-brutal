using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace OniMoono.Assets.TypeTrees
{
    /// Unity 6000.3.17f1 (U60) release type trees for native classes, by class id. Player files strip
    /// their type trees; these describe the bytes (Data/typetrees-*.json.gz, from Unity's type
    /// metadata dump for this exact editor version).
    public static class TypeTreeDatabase
    {
        public sealed record ClassInfo(string Name, int Id, string Base, bool IsAbstract, TypeTreeNode? Tree);

        static readonly Dictionary<int, ClassInfo> byId = new();
        static readonly Dictionary<string, ClassInfo> byName = new();
        internal static readonly Dictionary<int, string> CommonStrings = new();

        public static string UnityVersion { get; }

        static TypeTreeDatabase()
        {
            using var stream = typeof(TypeTreeDatabase).Assembly.GetManifestResourceStream("typetrees.json.gz")!;
            using var gz = new GZipStream(stream, CompressionMode.Decompress);
            using var doc = JsonDocument.Parse(gz);
            var root = doc.RootElement;
            UnityVersion = root.GetProperty("Version").GetString()!;
            foreach (var s in root.GetProperty("Strings").EnumerateObject()) CommonStrings[int.Parse(s.Name)] = s.Value.GetString()!;
            foreach (var c in root.GetProperty("Classes").EnumerateArray())
            {
                var treeElement = c.GetProperty("Tree");
                var info = new ClassInfo(c.GetProperty("Name").GetString()!, c.GetProperty("Id").GetInt32(), c.GetProperty("Base").GetString()!,
                    c.GetProperty("Abstract").GetBoolean(), treeElement.ValueKind == JsonValueKind.Null ? null : Build(treeElement));
                byId[info.Id] = info;
                byName[info.Name] = info;
            }
        }

        static TypeTreeNode Build(JsonElement e)
        {
            var node = new TypeTreeNode
            {
                TypeName = e.GetProperty("T").GetString()!,
                Name = e.GetProperty("N").GetString()!,
                ByteSize = e.GetProperty("S").GetInt32(),
                Version = e.GetProperty("V").GetInt32(),
                TypeFlags = e.GetProperty("F").GetInt32(),
                MetaFlag = e.GetProperty("M").GetUInt32(),
            };
            foreach (var c in e.GetProperty("C").EnumerateArray()) node.Children.Add(Build(c));
            return node;
        }

        /// Classes whose data in this game's Linux player is longer than the release tree describes,
        /// by exactly this many trailing bytes. PlayerSettings (129) ends with one int (value 3)
        /// after allowedHttpConnections that the 6000.3.5f2 dump does not list; every field the
        /// engine reads precedes it.
        static readonly Dictionary<int, int> knownTrailingBytes = new() { [129] = 4 };

        public static int TrailingBytes(int classId) => knownTrailingBytes.TryGetValue(classId, out var n) ? n : 0;

        public static ClassInfo Get(int classId) => byId.TryGetValue(classId, out var c) ? c : throw new InvalidDataException($"Unknown class id {classId}.");
        public static bool TryGet(int classId, out ClassInfo info) => byId.TryGetValue(classId, out info!);
        public static ClassInfo GetByName(string name) => byName[name];

        /// Whether `classId` is `baseName` or derives from it.
        public static bool IsA(int classId, string baseName)
        {
            for (var c = Get(classId); ; c = byName[c.Base])
            {
                if (c.Name == baseName) return true;
                if (string.IsNullOrEmpty(c.Base)) return false;
            }
        }
    }

    static class CommonStrings
    {
        public static string Get(int offset) => TypeTreeDatabase.CommonStrings.TryGetValue(offset, out var s) ? s : throw new InvalidDataException($"Unknown common string {offset}.");
    }
}
