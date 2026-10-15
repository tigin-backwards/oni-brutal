using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using OniBrutal.Assets.Files;

namespace OniBrutal.Engine.Assets
{
    /// The asset files the player can read: the data directory's SerializedFiles, the built-in
    /// resource files, and the contents of mounted AssetBundles. Files open lazily by name, as
    /// they are referenced through PPtr externals.
    internal static class AssetSystem
    {
        static string dataDir = "";
        static readonly object gate = new();
        static readonly Dictionary<string, SerializedFile> files = new(StringComparer.Ordinal);
        static readonly Dictionary<string, byte[]> bundleNodes = new(StringComparer.Ordinal);
        static readonly ConcurrentDictionary<string, MemoryMappedViewAccessor> streams = new(StringComparer.Ordinal);

        public static string DataDir => dataDir;

        public static void Initialize(string gameDataDir) => dataDir = gameDataDir;

        /// Maps a name as it appears in externals or loads (`sharedassets0.assets`,
        /// `library/unity default resources`, `archive:/CAB-…/CAB-…`) to a file key.
        public static string Canonical(string name)
        {
            var n = name.Replace('\\', '/');
            if (n.StartsWith("archive:/", StringComparison.OrdinalIgnoreCase))
                return "archive:/" + n["archive:/".Length..];
            var file = Path.GetFileName(n);
            if (file.Equals("unity default resources", StringComparison.OrdinalIgnoreCase)) return "Resources/unity default resources";
            if (file.Equals("unity_builtin_extra", StringComparison.OrdinalIgnoreCase)) return "Resources/unity_builtin_extra";
            return file;
        }

        public static SerializedFile Get(string name)
        {
            var key = Canonical(name);
            lock (gate)
            {
                if (files.TryGetValue(key, out var f)) return f;
                if (key.StartsWith("archive:/", StringComparison.Ordinal))
                {
                    if (!bundleNodes.TryGetValue(key, out var bytes)) throw new FileNotFoundException("Asset file not mounted: " + name);
                    f = SerializedFile.Parse(key, bytes);
                }
                else
                {
                    f = SerializedFile.Open(Path.Combine(dataDir, key));
                }
                files[key] = f;
                return f;
            }
        }

        /// The canonical key of a file (as used in object identities).
        public static string KeyOf(SerializedFile file) => Canonical(file.Name);

        /// The file a PPtr's fileId designates from `from`.
        public static SerializedFile ResolveExternal(SerializedFile from, int fileId)
            => fileId == 0 ? from : Get(from.Externals[fileId - 1].Path);

        /// Mounts an AssetBundle's nodes (SerializedFiles and resource streams).
        public static List<string> MountBundle(UnityFsArchive archive)
        {
            var serialized = new List<string>();
            lock (gate)
            {
                foreach (var node in archive.Nodes)
                {
                    var key = "archive:/" + node.Path;
                    bundleNodes[key] = archive.Read(node);
                    if (!node.Path.EndsWith(".resS", StringComparison.Ordinal) && !node.Path.EndsWith(".resource", StringComparison.Ordinal))
                        serialized.Add(key);
                }
            }
            return serialized;
        }

        /// Reads `size` bytes at `offset` of a streamed resource file (.resS / .resource).
        public static byte[] ReadStream(string path, long offset, long size)
        {
            var key = Canonical(path);
            var result = new byte[size];
            lock (gate)
            {
                if (bundleNodes.TryGetValue(key, out var node))
                {
                    Array.Copy(node, offset, result, 0, size);
                    return result;
                }
            }
            var view = streams.GetOrAdd(key, k =>
            {
                var file = MemoryMappedFile.CreateFromFile(Path.Combine(dataDir, k), FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                return file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            });
            view.ReadArray(offset, result, 0, (int)size);
            return result;
        }
    }
}
