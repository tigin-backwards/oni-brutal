using System;
using System.Collections.Generic;
using UnityEngine;

namespace OniMoono.Engine.Rendering
{
    /// Shader.PropertyToID: one process-wide id per property name.
    internal static class ShaderPropertyIds
    {
        static readonly Dictionary<string, int> ids = new(StringComparer.Ordinal);
        static readonly List<string> names = new();

        public static int Get(string name)
        {
            name ??= "";
            lock (ids)
            {
                if (ids.TryGetValue(name, out int id)) return id;
                id = names.Count + 1;
                ids.Add(name, id);
                names.Add(name);
                return id;
            }
        }

        public static string NameOf(int id)
        {
            lock (ids) return id >= 1 && id <= names.Count ? names[id - 1] : null;
        }
    }

    /// A texture binding with its tiling: scale in xy, offset in zw (the "_ST" vector).
    internal struct TextureBinding
    {
        public Texture Texture;
        public Vector4 ScaleOffset;

        public static readonly Vector4 IdentityScaleOffset = new(1f, 1f, 0f, 0f);
    }

    /// Property values of a material, property block or the global shader state. Colours and
    /// vectors share one store, as do SetFloat and SetInt (Unity's SetInt writes a float).
    internal sealed class PropertySheet
    {
        public readonly Dictionary<int, float> Floats = new();
        public readonly Dictionary<int, int> Ints = new();
        public readonly Dictionary<int, Vector4> Vectors = new();
        public readonly Dictionary<int, Matrix4x4> Matrices = new();
        public readonly Dictionary<int, TextureBinding> Textures = new();
        public readonly Dictionary<int, float[]> FloatArrays = new();
        public readonly Dictionary<int, Vector4[]> VectorArrays = new();
        public readonly Dictionary<int, Matrix4x4[]> MatrixArrays = new();

        /// Bumped on every change, so dependents (the device, TMP's CRC checks) can tell.
        public int Version;

        /// The render epoch until which a queued draw shares this sheet (MaterialPropertyBlock).
        public int SharedEpoch = -1;

        /// Counts rendered frames: draws queued during an epoch are consumed when it ends
        /// (FrameRenderer.EndFrame). Main thread only.
        public static int RenderEpoch;

        public bool IsEmpty => Floats.Count == 0 && Ints.Count == 0 && Vectors.Count == 0 && Matrices.Count == 0 && Textures.Count == 0
                               && FloatArrays.Count == 0 && VectorArrays.Count == 0 && MatrixArrays.Count == 0;

        public void SetFloat(int id, float v) { Floats[id] = v; Version++; }
        public void SetInt(int id, int v) { Ints[id] = v; Version++; }
        public void SetVector(int id, Vector4 v) { Vectors[id] = v; Version++; }
        public void SetMatrix(int id, Matrix4x4 v) { Matrices[id] = v; Version++; }

        public void SetTexture(int id, Texture texture)
        {
            var binding = Textures.TryGetValue(id, out var b) ? b : new TextureBinding { ScaleOffset = TextureBinding.IdentityScaleOffset };
            binding.Texture = texture;
            Textures[id] = binding;
            Version++;
        }

        public void SetTextureScaleOffset(int id, Vector4 scaleOffset)
        {
            var binding = Textures.TryGetValue(id, out var b) ? b : default;
            binding.ScaleOffset = scaleOffset;
            Textures[id] = binding;
            Version++;
        }

        /// Arrays keep the first `count` elements; an array property's size is fixed by its first
        /// assignment (Unity: later, larger arrays are truncated to that size).
        public void SetFloatArray(int id, float[] values, int count)
        {
            Floats.Remove(id);
            FloatArrays[id] = FixedArray(FloatArrays, id, values, count);
            Version++;
        }

        public void SetVectorArray(int id, Vector4[] values, int count)
        {
            VectorArrays[id] = FixedArray(VectorArrays, id, values, count);
            Version++;
        }

        public void SetMatrixArray(int id, Matrix4x4[] values, int count)
        {
            MatrixArrays[id] = FixedArray(MatrixArrays, id, values, count);
            Version++;
        }

        static T[] FixedArray<T>(Dictionary<int, T[]> store, int id, T[] values, int count)
        {
            int size = store.TryGetValue(id, out var existing) ? existing.Length : count;
            var result = new T[size];
            Array.Copy(values, result, Math.Min(size, count));
            return result;
        }

        public void Clear()
        {
            Floats.Clear();
            Ints.Clear();
            Vectors.Clear();
            Matrices.Clear();
            Textures.Clear();
            FloatArrays.Clear();
            VectorArrays.Clear();
            MatrixArrays.Clear();
            Version++;
        }

        public void CopyFrom(PropertySheet other)
        {
            Clear();
            foreach (var kv in other.Floats) Floats[kv.Key] = kv.Value;
            foreach (var kv in other.Ints) Ints[kv.Key] = kv.Value;
            foreach (var kv in other.Vectors) Vectors[kv.Key] = kv.Value;
            foreach (var kv in other.Matrices) Matrices[kv.Key] = kv.Value;
            foreach (var kv in other.Textures) Textures[kv.Key] = kv.Value;
            foreach (var kv in other.FloatArrays) FloatArrays[kv.Key] = (float[])kv.Value.Clone();
            foreach (var kv in other.VectorArrays) VectorArrays[kv.Key] = (Vector4[])kv.Value.Clone();
            foreach (var kv in other.MatrixArrays) MatrixArrays[kv.Key] = (Matrix4x4[])kv.Value.Clone();
            Version++;
        }

        /// Content hash: equal sheets hash equally regardless of insertion order.
        public void Hash(Crc32 crc)
        {
            using var stream = new System.IO.MemoryStream();
            using (var w = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                void V(Vector4 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); w.Write(v.w); }
                void M(Matrix4x4 m) { for (int i = 0; i < 16; i++) w.Write(m[i]); }
                foreach (var id in Sorted(Floats.Keys)) { w.Write(id); w.Write(Floats[id]); }
                foreach (var id in Sorted(Ints.Keys)) { w.Write(id); w.Write(Ints[id]); }
                foreach (var id in Sorted(Vectors.Keys)) { w.Write(id); V(Vectors[id]); }
                foreach (var id in Sorted(Matrices.Keys)) { w.Write(id); M(Matrices[id]); }
                foreach (var id in Sorted(Textures.Keys))
                {
                    var t = Textures[id];
                    w.Write(id);
                    w.Write(t.Texture != null ? t.Texture.GetInstanceID() : 0);
                    V(t.ScaleOffset);
                }
                foreach (var id in Sorted(FloatArrays.Keys)) { w.Write(id); foreach (var f in FloatArrays[id]) w.Write(f); }
                foreach (var id in Sorted(VectorArrays.Keys)) { w.Write(id); foreach (var v in VectorArrays[id]) V(v); }
                foreach (var id in Sorted(MatrixArrays.Keys)) { w.Write(id); foreach (var m in MatrixArrays[id]) M(m); }
            }
            crc.Append(stream.GetBuffer().AsSpan(0, (int)stream.Length));
        }

        static int[] Sorted(IEnumerable<int> keys)
        {
            var list = new List<int>(keys);
            list.Sort();
            return list.ToArray();
        }
    }

    /// Global shader properties and keywords (Shader.SetGlobal*, Shader.EnableKeyword).
    internal static class ShaderGlobals
    {
        public static readonly PropertySheet Properties = new();
        public static readonly KeywordSet Keywords = new();
        public static int MaximumLOD = int.MaxValue;
    }

    /// An ordered set of enabled shader keywords.
    internal sealed class KeywordSet
    {
        readonly List<string> list = new();
        public int Version;

        public bool Contains(string keyword) => list.Contains(keyword);

        public void Enable(string keyword)
        {
            if (string.IsNullOrEmpty(keyword) || list.Contains(keyword)) return;
            list.Add(keyword);
            Version++;
        }

        public void Disable(string keyword)
        {
            if (list.Remove(keyword)) Version++;
        }

        public string[] ToArray() => list.ToArray();

        /// Replaces the set; Unity drops empty names and duplicates.
        public void Set(IEnumerable<string> keywords)
        {
            list.Clear();
            if (keywords != null)
                foreach (var k in keywords)
                    if (!string.IsNullOrEmpty(k) && !list.Contains(k)) list.Add(k);
            Version++;
        }

        public IReadOnlyList<string> Items => list;
    }
}
