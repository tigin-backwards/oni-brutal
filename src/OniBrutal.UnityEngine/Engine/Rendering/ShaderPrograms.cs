using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OniBrutal.Assets.Compression;
using OniBrutal.Assets.TypeTrees;

namespace OniBrutal.Engine.Rendering
{
    /// One compiled program variant of a pass: GLSL stages as Unity's GL device compiles them.
    internal sealed class ShaderVariant
    {
        public int BlobIndex;
        public string[] Keywords;
        public string VertexSource;
        public string FragmentSource;
        /// Null for programs without a geometry stage.
        public string GeometrySource;
        /// Vertex channels the program reads (bit per channel: position 0x1, normal 0x2,
        /// tangent 0x4, color 0x8, texcoord n 0x10 << n).
        public uint ChannelMask;
    }

    /// The OpenGLCore programs of a shader (platform 15): LZ4-compressed segments holding a
    /// table of (offset, length, segment) entries. Program entries are: version, GPU program
    /// type, four stats, the variant's keywords, the GLSL source of all stages (sections
    /// `#ifdef VERTEX` … `#ifdef FRAGMENT` …), then the vertex channel mask.
    internal sealed class ShaderPrograms
    {
        const int GlCorePlatformIndexNotFound = -1;

        readonly byte[][] segments;
        readonly (int Offset, int Length, int Segment)[] entries;
        readonly Dictionary<int, ShaderVariant> variants = new();

        ShaderPrograms(byte[][] segments)
        {
            this.segments = segments;
            var table = segments[0];
            int count = BitConverter.ToInt32(table, 0);
            entries = new (int, int, int)[count];
            for (int i = 0; i < count; i++)
                entries[i] = (BitConverter.ToInt32(table, 4 + 12 * i), BitConverter.ToInt32(table, 8 + 12 * i), BitConverter.ToInt32(table, 12 + 12 * i));
        }

        public static ShaderPrograms Load(Fields shader)
        {
            var platforms = shader.Get<uint[]>("platforms");
            int p = Array.IndexOf(platforms, ShaderNative.OpenGLCorePlatform);
            if (p == GlCorePlatformIndexNotFound) return null;
            var blob = shader.Get<byte[]>("compressedBlob");
            var offsets = (uint[])shader.Array("offsets")[p];
            var compressed = (uint[])shader.Array("compressedLengths")[p];
            var decompressed = (uint[])shader.Array("decompressedLengths")[p];
            var segs = new byte[offsets.Length][];
            for (int s = 0; s < segs.Length; s++)
            {
                segs[s] = new byte[decompressed[s]];
                Lz4.Decode(blob.AsSpan((int)offsets[s], (int)compressed[s]), segs[s]);
            }
            return new ShaderPrograms(segs);
        }

        public ShaderVariant Variant(int blobIndex)
        {
            lock (variants)
            {
                if (variants.TryGetValue(blobIndex, out var v)) return v;
                v = Decode(blobIndex);
                variants[blobIndex] = v;
                return v;
            }
        }

        ShaderVariant Decode(int blobIndex)
        {
            var (offset, length, segment) = entries[blobIndex];
            var data = segments[segment].AsSpan(offset, length);
            int pos = 24;
            int keywordCount = BitConverter.ToInt32(data[pos..]);
            pos += 4;
            var keywords = new string[keywordCount];
            for (int i = 0; i < keywordCount; i++)
            {
                int len = BitConverter.ToInt32(data[pos..]);
                keywords[i] = Encoding.UTF8.GetString(data.Slice(pos + 4, len));
                pos += 4 + ((len + 3) & ~3);
            }
            int textLength = BitConverter.ToInt32(data[pos..]);
            var source = Encoding.UTF8.GetString(data.Slice(pos + 4, textLength));
            pos += 4 + ((textLength + 3) & ~3);
            uint channels = BitConverter.ToUInt32(data[pos..]);
            var stages = Split(source);
            return new ShaderVariant
            {
                BlobIndex = blobIndex,
                Keywords = keywords,
                VertexSource = stages["VERTEX"],
                FragmentSource = stages["FRAGMENT"],
                GeometrySource = stages.GetValueOrDefault("GEOMETRY"),
                ChannelMask = channels,
            };
        }

        static readonly string[] stageNames = ["VERTEX", "FRAGMENT", "GEOMETRY", "HULL", "DOMAIN"];

        /// The stage sections of Unity's combined GLSL: the text inside each `#ifdef STAGE`
        /// section, which ends at the last `#endif` before the next section (or the end).
        public static Dictionary<string, string> Split(string source)
        {
            var starts = new List<(int Index, string Stage)>();
            foreach (var stage in stageNames)
            {
                int i = source.IndexOf("#ifdef " + stage + "\n", StringComparison.Ordinal);
                if (i >= 0) starts.Add((i, stage));
            }
            starts.Sort((a, b) => a.Index.CompareTo(b.Index));
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int k = 0; k < starts.Count; k++)
            {
                int start = starts[k].Index + ("#ifdef " + starts[k].Stage + "\n").Length;
                int end = k + 1 < starts.Count ? starts[k + 1].Index : source.Length;
                int close = source.LastIndexOf("#endif", end - 1, end - start, StringComparison.Ordinal);
                if (close < start) throw new InvalidDataException("Unterminated GLSL stage section.");
                result[starts[k].Stage] = source[start..close];
            }
            if (!result.ContainsKey("VERTEX") || !result.ContainsKey("FRAGMENT")) throw new InvalidDataException("GLSL program without VERTEX and FRAGMENT sections.");
            return result;
        }

        /// GPU program types of the OpenGLCore platform (GLCore32, GLCore41, GLCore43). Player
        /// program lists hold every platform's programs, told apart by type.
        public static bool IsGlCore(int gpuProgramType) => gpuProgramType is 6 or 7 or 8;

        /// The blob index of the variant Unity selects for a pass given the enabled keywords:
        /// among the pass's OpenGLCore player programs, an exact match of the enabled keywords the pass
        /// knows; otherwise the one with most matches, then fewest extra keywords, then the
        /// earliest.
        public static int SelectVariant(Fields pass, string[] shaderKeywords, Func<string, bool> isEnabled)
        {
            var program = pass.Struct("progVertex");
            int best = -1, bestMatches = -1, bestExtras = int.MaxValue;
            foreach (var list in program.Array("m_PlayerSubPrograms"))
                foreach (Fields sub in (object[])list)
                {
                    if (!IsGlCore(sub.Get<sbyte>("m_GpuProgramType"))) continue;
                    var indices = sub.Get<ushort[]>("m_KeywordIndices") ?? Array.Empty<ushort>();
                    int matches = 0, extras = 0;
                    foreach (var k in indices)
                        if (isEnabled(shaderKeywords[k])) matches++;
                        else extras++;
                    if (matches > bestMatches || (matches == bestMatches && extras < bestExtras))
                    {
                        best = (int)sub.Get<uint>("m_BlobIndex");
                        bestMatches = matches;
                        bestExtras = extras;
                    }
                }
            return best;
        }
    }
}
