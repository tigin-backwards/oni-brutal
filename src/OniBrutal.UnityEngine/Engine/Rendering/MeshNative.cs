using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine.Serialization;
using UnityEngine;

namespace OniBrutal.Engine.Rendering
{
    internal sealed class SubMeshData
    {
        public int[] Indices = Array.Empty<int>();
        public MeshTopology Topology = MeshTopology.Triangles;
        public int BaseVertex;
        public Bounds Bounds;
    }

    /// A mesh's data, kept as per-attribute arrays (Unity's channel model). The device builds its
    /// buffers from them when Version changes. A channel or submesh index array is filled before
    /// it is published and never written afterwards (a change replaces it, and getters hand out
    /// copies): the device's uploads hold the arrays themselves until they run on the render
    /// thread.
    internal sealed class MeshNative : NativeObject
    {
        public const int UvChannels = 8;

        public int VertexCount;
        public Vector3[] Positions;
        public Vector3[] Normals;
        public Vector4[] Tangents;

        Color[] colors;

        /// The vertex colours. Colours given as bytes (SetColors32) are kept as given and turned
        /// into Colors only when something reads them: the device uploads the bytes.
        public Color[] Colors
        {
            get
            {
                if (colors == null && Colors32 != null)
                {
                    var expanded = new Color[Colors32.Length];
                    for (int i = 0; i < expanded.Length; i++) expanded[i] = Colors32[i];
                    colors = expanded;
                }
                return colors;
            }
            set
            {
                colors = value;
                Colors32 = null;
            }
        }

        /// The colours as given in bytes, or null when they were given otherwise.
        public Color32[] Colors32 { get; private set; }

        public void SetColors32(Color32[] values)
        {
            colors = null;
            Colors32 = values;
        }

        /// Colours were given as Color32 (UNorm8 storage).
        public bool ColorsAreBytes;
        public readonly Vector4[][] Uvs = new Vector4[UvChannels][];
        public readonly int[] UvDimensions = new int[UvChannels];
        public List<SubMeshData> SubMeshes = new() { new SubMeshData() };
        public Bounds Bounds;
        public bool IsReadable = true;
        public bool Dynamic;
        /// 0 = 16-bit, 1 = 32-bit (IndexFormat).
        public int IndexFormat;

        public int Version;
        public object Gpu;

        public void Changed() => Version++;

        public void Clear()
        {
            VertexCount = 0;
            Positions = null;
            Normals = null;
            Tangents = null;
            Colors = null;
            ColorsAreBytes = false;
            Array.Clear(Uvs);
            Array.Clear(UvDimensions);
            SubMeshes = new List<SubMeshData> { new SubMeshData() };
            // The bounds stay (measured in the original player).
            Changed();
        }

        public void RecalculateBounds()
        {
            Bounds = BoundsOf(Positions, VertexCount);
            foreach (var s in SubMeshes) s.Bounds = SubMeshBounds(s);
            Changed();
        }

        static Bounds BoundsOf(Vector3[] points, int count)
        {
            if (points == null || count == 0) return default;
            Vector3 min = points[0], max = points[0];
            for (int i = 1; i < count; i++)
            {
                min = Vector3.Min(min, points[i]);
                max = Vector3.Max(max, points[i]);
            }
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        public Bounds SubMeshBounds(SubMeshData s)
        {
            if (Positions == null || s.Indices.Length == 0) return default;
            Vector3 min = Positions[s.Indices[0] + s.BaseVertex], max = min;
            foreach (int i in s.Indices)
            {
                var p = Positions[i + s.BaseVertex];
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        public void CopyFrom(MeshNative o)
        {
            VertexCount = o.VertexCount;
            Positions = (Vector3[])o.Positions?.Clone();
            Normals = (Vector3[])o.Normals?.Clone();
            Tangents = (Vector4[])o.Tangents?.Clone();
            if (o.Colors32 != null) SetColors32((Color32[])o.Colors32.Clone());
            else Colors = (Color[])o.Colors?.Clone();
            ColorsAreBytes = o.ColorsAreBytes;
            for (int i = 0; i < UvChannels; i++)
            {
                Uvs[i] = (Vector4[])o.Uvs[i]?.Clone();
                UvDimensions[i] = o.UvDimensions[i];
            }
            SubMeshes = o.SubMeshes.ConvertAll(s => new SubMeshData { Indices = (int[])s.Indices.Clone(), Topology = s.Topology, BaseVertex = s.BaseVertex, Bounds = s.Bounds });
            Bounds = o.Bounds;
            IndexFormat = o.IndexFormat;
            Dynamic = o.Dynamic;
            Changed();
        }

        /// Mesh (class 43): vertex streams, the index buffer and submeshes.
        public override void Bind(Fields f, IReferenceResolver resolver)
        {
            Serialized = null;
            if (f.Get<byte>("m_MeshCompression") != 0) throw new InvalidDataException($"Mesh {Name}: compressed meshes are not supported.");
            if (f.Struct("m_StreamData").String("path").Length > 0) throw new InvalidDataException($"Mesh {Name}: streamed mesh data is not supported.");
            IsReadable = f.Bool("m_IsReadable");
            IndexFormat = f.Int("m_IndexFormat");
            var vertices = VertexData.Decode(f.Struct("m_VertexData"));
            vertices.ApplyTo(this);
            var indexBytes = f.Get<byte[]>("m_IndexBuffer");
            int indexSize = IndexFormat == 0 ? 2 : 4;
            SubMeshes = new List<SubMeshData>();
            foreach (Fields s in f.Array("m_SubMeshes"))
            {
                int first = (int)s.Get<uint>("firstByte") / indexSize;
                int count = (int)s.Get<uint>("indexCount");
                var indices = new int[count];
                for (int i = 0; i < count; i++)
                    indices[i] = indexSize == 2
                        ? BinaryPrimitives.ReadUInt16LittleEndian(indexBytes.AsSpan((first + i) * 2))
                        : (int)BinaryPrimitives.ReadUInt32LittleEndian(indexBytes.AsSpan((first + i) * 4));
                SubMeshes.Add(new SubMeshData
                {
                    Indices = indices,
                    Topology = (MeshTopology)s.Int("topology"),
                    BaseVertex = (int)s.Get<uint>("baseVertex"),
                    Bounds = ReadAabb(s.Struct("localAABB")),
                });
            }
            if (SubMeshes.Count == 0) SubMeshes.Add(new SubMeshData());
            Bounds = ReadAabb(f.Struct("m_LocalAABB"));
            Changed();
        }

        internal static Bounds ReadAabb(Fields aabb)
        {
            var c = aabb.Struct("m_Center");
            var e = aabb.Struct("m_Extent");
            return new Bounds(new Vector3(c.Float("x"), c.Float("y"), c.Float("z")), 2f * new Vector3(e.Float("x"), e.Float("y"), e.Float("z")));
        }

        public void ReleaseResources() => GraphicsDevice.Release(ref Gpu);
    }

    /// Unity's serialized VertexData: channels (Position, Normal, Tangent, Color, TexCoord0-7,
    /// BlendWeight, BlendIndices) laid out in streams; each stream holds its channels interleaved
    /// and starts 16-byte aligned after the previous one.
    internal sealed class VertexData
    {
        public int VertexCount;
        public Vector3[] Positions;
        public Vector3[] Normals;
        public Vector4[] Tangents;
        public Color[] Colors;
        public bool ColorsAreBytes;
        public readonly Vector4[][] Uvs = new Vector4[MeshNative.UvChannels][];
        public readonly int[] UvDimensions = new int[MeshNative.UvChannels];

        readonly record struct Channel(int Stream, int Offset, int Format, int Dimension);

        static int FormatSize(int format) => format switch
        {
            0 => 4,                 // Float32
            1 => 2,                 // Float16
            2 or 3 or 6 or 7 => 1,  // UNorm8, SNorm8, UInt8, SInt8
            4 or 5 or 8 or 9 => 2,  // UNorm16, SNorm16, UInt16, SInt16
            10 or 11 => 4,          // UInt32, SInt32
            _ => throw new InvalidDataException("Unknown vertex attribute format " + format),
        };

        static float Component(ReadOnlySpan<byte> p, int format) => format switch
        {
            0 => BinaryPrimitives.ReadSingleLittleEndian(p),
            1 => (float)BinaryPrimitives.ReadHalfLittleEndian(p),
            2 => p[0] / 255f,
            3 => Math.Max((sbyte)p[0] / 127f, -1f),
            4 => BinaryPrimitives.ReadUInt16LittleEndian(p) / 65535f,
            5 => Math.Max(BinaryPrimitives.ReadInt16LittleEndian(p) / 32767f, -1f),
            6 => p[0],
            7 => (sbyte)p[0],
            8 => BinaryPrimitives.ReadUInt16LittleEndian(p),
            9 => BinaryPrimitives.ReadInt16LittleEndian(p),
            10 => BinaryPrimitives.ReadUInt32LittleEndian(p),
            11 => BinaryPrimitives.ReadInt32LittleEndian(p),
            _ => throw new InvalidDataException("Unknown vertex attribute format " + format),
        };

        public static VertexData Decode(Fields vertexData)
        {
            var result = new VertexData { VertexCount = (int)vertexData.Get<uint>("m_VertexCount") };
            var channels = new List<Channel>();
            foreach (Fields c in vertexData.Array("m_Channels"))
                channels.Add(new Channel(c.Get<byte>("stream"), c.Get<byte>("offset"), c.Get<byte>("format"), c.Get<byte>("dimension") & 0xF));
            var data = vertexData.Get<byte[]>("m_DataSize");
            int n = result.VertexCount;

            int streamCount = 0;
            foreach (var c in channels) if (c.Dimension > 0) streamCount = Math.Max(streamCount, c.Stream + 1);
            var strides = new int[streamCount];
            foreach (var c in channels) if (c.Dimension > 0) strides[c.Stream] += FormatSize(c.Format) * c.Dimension;
            var starts = new int[streamCount];
            int offset = 0;
            for (int s = 0; s < streamCount; s++)
            {
                starts[s] = offset;
                offset += strides[s] * n;
                offset = (offset + 15) & ~15;
            }

            Vector4 Read(Channel c, int vertex)
            {
                int size = FormatSize(c.Format);
                var p = data.AsSpan(starts[c.Stream] + vertex * strides[c.Stream] + c.Offset);
                var v = new Vector4();
                for (int k = 0; k < c.Dimension && k < 4; k++) v[k] = Component(p[(k * size)..], c.Format);
                return v;
            }

            for (int ci = 0; ci < channels.Count; ci++)
            {
                var c = channels[ci];
                if (c.Dimension == 0) continue;
                switch (ci)
                {
                    case 0:
                        result.Positions = new Vector3[n];
                        for (int i = 0; i < n; i++) result.Positions[i] = Read(c, i);
                        break;
                    case 1:
                        result.Normals = new Vector3[n];
                        for (int i = 0; i < n; i++) result.Normals[i] = Read(c, i);
                        break;
                    case 2:
                        result.Tangents = new Vector4[n];
                        for (int i = 0; i < n; i++) result.Tangents[i] = Read(c, i);
                        break;
                    case 3:
                        result.Colors = new Color[n];
                        result.ColorsAreBytes = c.Format == 2;
                        for (int i = 0; i < n; i++) result.Colors[i] = Read(c, i);
                        break;
                    case >= 4 and <= 11:
                    {
                        int uv = ci - 4;
                        var values = new Vector4[n];
                        for (int i = 0; i < n; i++) values[i] = Read(c, i);
                        result.Uvs[uv] = values;
                        result.UvDimensions[uv] = c.Dimension;
                        break;
                    }
                    // Skinning channels: the game has no skinned meshes.
                }
            }
            return result;
        }

        public void ApplyTo(MeshNative mesh)
        {
            mesh.VertexCount = VertexCount;
            mesh.Positions = Positions;
            mesh.Normals = Normals;
            mesh.Tangents = Tangents;
            mesh.Colors = Colors;
            mesh.ColorsAreBytes = ColorsAreBytes;
            for (int i = 0; i < MeshNative.UvChannels; i++)
            {
                mesh.Uvs[i] = Uvs[i];
                mesh.UvDimensions[i] = UvDimensions[i];
            }
        }
    }
}
