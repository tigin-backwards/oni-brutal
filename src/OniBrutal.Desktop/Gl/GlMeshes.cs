using System;
using System.Collections.Generic;
using OniBrutal.Engine.Rendering;
using Brutal.OpenGlApi;
using UnityEngine;
using N = Brutal.OpenGlApi.OpenGl.Native;
using static OniBrutal.Desktop.Gl.GlConst;

namespace OniBrutal.Desktop.Gl
{
    /// Vertex channels in shader input naming order: in_POSITION0, in_NORMAL0, in_TANGENT0,
    /// in_COLOR0, in_TEXCOORD0…7.
    internal enum Channel
    {
        Position, Normal, Tangent, Color, Uv0, Uv1, Uv2, Uv3, Uv4, Uv5, Uv6, Uv7,
        Count,
    }

    /// A mesh on the device: one buffer per channel, one index buffer holding every submesh.
    internal sealed class DeviceMesh
    {
        public int Version = -1;
        public readonly uint[] Buffers = new uint[(int)Channel.Count];
        /// Allocated sizes in bytes; uploads that fit reuse the storage (glBufferSubData).
        public readonly long[] Capacities = new long[(int)Channel.Count];
        public long IndexCapacity;
        public readonly int[] Components = new int[(int)Channel.Count];
        /// Colours given as Color32 are unsigned bytes, normalized by the shader input.
        public bool ByteColors;
        public uint IndexBuffer;
        public (int Offset, int Count, uint Mode, int BaseVertex)[] SubMeshes = Array.Empty<(int, int, uint, int)>();
        /// Changes when the channels present or their formats change (vertex arrays are rebuilt).
        public int LayoutVersion;
        /// Vertex arrays by program attribute layout, with the LayoutVersion they were built for.
        public Dictionary<int, (uint VertexArray, int Version)> VertexArrays;

        public void Delete()
        {
            if (VertexArrays != null)
            {
                foreach (var (vao, _) in VertexArrays.Values) GlBindings.DeleteVertexArray(vao);
                VertexArrays = null;
            }
            for (int i = 0; i < Buffers.Length; i++)
                if (Buffers[i] != 0)
                {
                    GlBindings.DeleteBuffer(Buffers[i]);
                    Buffers[i] = 0;
                    Capacities[i] = 0;
                }
            if (IndexBuffer != 0) GlBindings.DeleteBuffer(IndexBuffer);
            IndexBuffer = 0;
            IndexCapacity = 0;
        }
    }

    internal static unsafe class GlMeshes
    {
        public static DeviceMesh Ensure(MeshNative m)
        {
            if (m.Gpu is not DeviceMesh d)
            {
                d = new DeviceMesh();
                m.Gpu = d;
                GraphicsDevice.Adopt(m, d);
            }
            if (d.Version == m.Version) return d;
            uint usage = m.Dynamic ? DynamicDraw : StaticDraw;
            Span<int> before = stackalloc int[(int)Channel.Count];
            d.Components.CopyTo(before);
            bool byteColorsBefore = d.ByteColors;
            Upload(d, Channel.Position, m.Positions, 3, usage);
            Upload(d, Channel.Normal, m.Normals, 3, usage);
            Upload(d, Channel.Tangent, m.Tangents, 4, usage);
            if (m.Colors != null && m.ColorsAreBytes)
            {
                var bytes = new Color32[m.Colors.Length];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = m.Colors[i];
                Store(d, Channel.Color, bytes, 4, usage);
                d.ByteColors = true;
            }
            else
            {
                Upload(d, Channel.Color, m.Colors, 4, usage);
                d.ByteColors = false;
            }
            for (int u = 0; u < MeshNative.UvChannels; u++)
            {
                var uv = m.Uvs[u];
                Upload(d, Channel.Uv0 + u, uv, uv == null ? 0 : Math.Max(1, m.UvDimensions[u]), usage, stride4: true);
            }
            UploadIndices(d, m, usage);
            if (!before.SequenceEqual(d.Components) || byteColorsBefore != d.ByteColors) d.LayoutVersion++;
            d.Version = m.Version;
            return d;
        }

        /// The mesh's vertex array for a program's attribute layout: buffers, formats and the
        /// index buffer recorded once, so a draw binds one object. Channels the mesh lacks are
        /// disabled and read the context's constant attribute values.
        public static uint VertexArray(DeviceMesh d, DeviceProgram p)
        {
            d.VertexArrays ??= new();
            if (d.VertexArrays.TryGetValue(p.LayoutId, out var entry) && entry.Version == d.LayoutVersion) return entry.VertexArray;
            uint vao = entry.VertexArray;
            if (vao == 0)
            {
                GlVertexArray created = default;
                N.glCreateVertexArrays(1, &created);
                vao = created.Id;
            }
            for (int c = 0; c < (int)Channel.Count; c++)
            {
                int location = p.Attributes[c];
                if (location < 0) continue;
                uint loc = (uint)location;
                int components = d.Components[c];
                if (components == 0)
                {
                    N.glDisableVertexArrayAttrib(vao, loc);
                    continue;
                }
                N.glEnableVertexArrayAttrib(vao, loc);
                if (c == (int)Channel.Color && d.ByteColors)
                {
                    N.glVertexArrayVertexBuffer(vao, loc, new GlBuffer(d.Buffers[c]), 0, 4);
                    N.glVertexArrayAttribFormat(vao, loc, 4, UnsignedByte, true, 0);
                }
                else
                {
                    int size = Math.Abs(components);
                    N.glVertexArrayVertexBuffer(vao, loc, new GlBuffer(d.Buffers[c]), 0, components < 0 ? 16 : size * 4);
                    N.glVertexArrayAttribFormat(vao, loc, size, Float, false, 0);
                }
                N.glVertexArrayAttribBinding(vao, loc, loc);
            }
            N.glVertexArrayElementBuffer(vao, new GlBuffer(d.IndexBuffer));
            d.VertexArrays[p.LayoutId] = (vao, d.LayoutVersion);
            return vao;
        }

        static void Upload<T>(DeviceMesh d, Channel c, T[] data, int components, uint usage, bool stride4 = false) where T : unmanaged
        {
            if (data == null || data.Length == 0)
            {
                d.Components[(int)c] = 0;
                return;
            }
            Store(d, c, data, components, usage);
            // UVs are stored as Vector4 whatever their dimension.
            if (stride4) d.Components[(int)c] = -components;
        }

        static void Store<T>(DeviceMesh d, Channel c, T[] data, int components, uint usage) where T : unmanaged
        {
            int i = (int)c;
            if (d.Buffers[i] == 0) d.Buffers[i] = GlApi.CreateBuffer();
            d.Capacities[i] = Fill<T>(d.Buffers[i], data, d.Capacities[i], usage);
            d.Components[i] = components;
        }

        /// Quads become two triangles each, as Unity's GL device draws them.
        static void UploadIndices(DeviceMesh d, MeshNative m, uint usage)
        {
            var all = new List<int>();
            var subs = new (int, int, uint, int)[m.SubMeshes.Count];
            for (int s = 0; s < subs.Length; s++)
            {
                var sm = m.SubMeshes[s];
                int start = all.Count;
                uint mode;
                switch (sm.Topology)
                {
                    case MeshTopology.Quads:
                        for (int q = 0; q + 3 < sm.Indices.Length; q += 4)
                            all.AddRange([sm.Indices[q], sm.Indices[q + 1], sm.Indices[q + 2], sm.Indices[q], sm.Indices[q + 2], sm.Indices[q + 3]]);
                        mode = Triangles;
                        break;
                    case MeshTopology.Lines:
                        all.AddRange(sm.Indices);
                        mode = Lines;
                        break;
                    case MeshTopology.LineStrip:
                        all.AddRange(sm.Indices);
                        mode = LineStrip;
                        break;
                    case MeshTopology.Points:
                        all.AddRange(sm.Indices);
                        mode = Points;
                        break;
                    default:
                        all.AddRange(sm.Indices);
                        mode = Triangles;
                        break;
                }
                subs[s] = (start, all.Count - start, mode, sm.BaseVertex);
            }
            if (d.IndexBuffer == 0) d.IndexBuffer = GlApi.CreateBuffer();
            d.IndexCapacity = Fill<int>(d.IndexBuffer, all.ToArray(), d.IndexCapacity, usage);
            d.SubMeshes = subs;
        }

        /// Writes data into a buffer (direct state access: no binding changes, so the bound
        /// vertex array keeps its index buffer): in place when it fits the current storage, else
        /// into new storage. Returns the storage size.
        static long Fill<T>(uint buffer, T[] data, long capacity, uint usage) where T : unmanaged
        {
            long bytes = (long)data.Length * sizeof(T);
            fixed (T* p = data)
            {
                if (bytes > 0 && bytes <= capacity)
                {
                    N.glNamedBufferSubData(new GlBuffer(buffer), 0, (nint)bytes, p);
                    return capacity;
                }
                N.glNamedBufferData(new GlBuffer(buffer), (nint)bytes, p, usage);
            }
            return bytes;
        }
    }
}
