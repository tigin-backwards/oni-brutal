using System;
using System.IO;
using OniMoono.Assets.Compression;
using OniMoono.Assets.TypeTrees;

namespace OniMoono.Assets.Shaders
{
    /// ShaderCompilerPlatform values of the graphics APIs this project reads programs for.
    public static class ShaderCompilerPlatform
    {
        public const uint Metal = 14;
        public const uint OpenGLCore = 15;
        public const uint Vulkan = 18;
    }

    /// The GPU program types (`m_GpuProgramType`) of a player subprogram. Player program lists hold
    /// every platform's programs, told apart by type.
    public static class GpuProgramTypes
    {
        public const int GLCore32 = 6, GLCore41 = 7, GLCore43 = 8;
        public const int MetalVertex = 23, MetalFragment = 24;
        /// Vulkan's SPIR-V: one program holds every stage (measured: in U60's data every
        /// subprogram of this type names an entry of the platform-18 table, and the Vulkan table
        /// has no other kind).
        public const int SpirV = 25;

        public static bool IsGlCore(int type) => type is GLCore32 or GLCore41 or GLCore43;

        public static bool IsMetal(int type) => type is MetalVertex or MetalFragment;

        /// The platform whose blob table holds a program of this type (0 for other platforms).
        public static uint PlatformOf(int type) => IsGlCore(type) ? ShaderCompilerPlatform.OpenGLCore : IsMetal(type) ? ShaderCompilerPlatform.Metal
            : type == SpirV ? ShaderCompilerPlatform.Vulkan : 0u;
    }

    /// One platform's program data in a Shader (class 48): LZ4-compressed segments, the first of
    /// which starts with a table of (offset, length, segment) entries. Program entries and parameter
    /// blobs are both entries of the table; subprograms name them by index (`m_BlobIndex`,
    /// `m_ParameterBlobIndices`).
    public sealed class ShaderProgramTable
    {
        readonly byte[][] segments;
        readonly (int Offset, int Length, int Segment)[] entries;

        public ShaderProgramTable(byte[][] segments)
        {
            if (segments.Length == 0) throw new InvalidDataException("Shader platform without segments.");
            this.segments = segments;
            var table = segments[0];
            int count = BitConverter.ToInt32(table, 0);
            if (count < 0 || 4 + 12L * count > table.Length) throw new InvalidDataException($"Shader blob table of {count} entries does not fit its segment.");
            entries = new (int, int, int)[count];
            for (int i = 0; i < count; i++)
                entries[i] = (BitConverter.ToInt32(table, 4 + 12 * i), BitConverter.ToInt32(table, 8 + 12 * i), BitConverter.ToInt32(table, 12 + 12 * i));
        }

        /// The platform's table, or null when the shader carries no programs for it.
        public static ShaderProgramTable? Load(Fields shader, uint platform)
        {
            var platforms = shader.Get<uint[]>("platforms");
            int p = Array.IndexOf(platforms, platform);
            if (p < 0) return null;
            var blob = shader.Get<byte[]>("compressedBlob");
            var offsets = (uint[])shader.Array("offsets")[p]!;
            var compressed = (uint[])shader.Array("compressedLengths")[p]!;
            var decompressed = (uint[])shader.Array("decompressedLengths")[p]!;
            var segs = new byte[offsets.Length][];
            for (int s = 0; s < segs.Length; s++)
            {
                segs[s] = new byte[decompressed[s]];
                Lz4.Decode(blob.AsSpan((int)offsets[s], (int)compressed[s]), segs[s]);
            }
            return new ShaderProgramTable(segs);
        }

        public int Count => entries.Length;

        /// The decompressed size of all segments.
        public long Bytes
        {
            get
            {
                long n = 0;
                foreach (var s in segments) n += s.Length;
                return n;
            }
        }

        /// Whether entry `index` lies inside its segment (Unity's Linux build ships some built-in
        /// shaders' Metal tables half-stripped, with entries past their data).
        public bool Contains(int index)
        {
            if ((uint)index >= (uint)entries.Length) return false;
            var (offset, length, segment) = entries[index];
            return (uint)segment < (uint)segments.Length && offset >= 0 && length >= 0 && (long)offset + length <= segments[segment].Length;
        }

        public ReadOnlySpan<byte> Get(int index)
        {
            if (!Contains(index)) throw new InvalidDataException($"Shader blob {index} is not in the platform's table of {entries.Length}.");
            var (offset, length, segment) = entries[index];
            return segments[segment].AsSpan(offset, length);
        }
    }
}
