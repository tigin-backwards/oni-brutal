using System;
using System.Collections.Generic;
using OniBrutal.Engine.Rendering;
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

        public void Delete()
        {
            for (int i = 0; i < Buffers.Length; i++)
                if (Buffers[i] != 0)
                {
                    GlApi.DeleteBuffer(Buffers[i]);
                    Buffers[i] = 0;
                    Capacities[i] = 0;
                }
            if (IndexBuffer != 0) GlApi.DeleteBuffer(IndexBuffer);
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
            d.Version = m.Version;
            return d;
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
            if (d.Buffers[i] == 0) d.Buffers[i] = GlApi.GenBuffer();
            GlApi.BindBuffer(ArrayBuffer, d.Buffers[i]);
            d.Capacities[i] = Fill<T>(ArrayBuffer, data, d.Capacities[i], usage);
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
            if (d.IndexBuffer == 0) d.IndexBuffer = GlApi.GenBuffer();
            GlApi.BindBuffer(ElementArrayBuffer, d.IndexBuffer);
            d.IndexCapacity = Fill<int>(ElementArrayBuffer, all.ToArray(), d.IndexCapacity, usage);
            d.SubMeshes = subs;
        }

        /// Writes data into the bound buffer: in place when it fits the current storage, else
        /// into new storage. Returns the storage size.
        static long Fill<T>(uint target, T[] data, long capacity, uint usage) where T : unmanaged
        {
            long bytes = (long)data.Length * sizeof(T);
            if (bytes > 0 && bytes <= capacity)
            {
                GlApi.BufferSubData<T>(target, 0, data);
                return capacity;
            }
            GlApi.BufferData<T>(target, data, usage);
            return bytes;
        }
    }
}
