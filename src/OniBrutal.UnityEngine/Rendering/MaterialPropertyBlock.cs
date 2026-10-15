using System.Collections.Generic;
using OniBrutal.Engine.Rendering;

namespace UnityEngine
{
    /// Per-renderer overrides of material properties.
    public partial class MaterialPropertyBlock
    {
        internal readonly PropertySheet Sheet = new();

        public MaterialPropertyBlock() { }

        public bool isEmpty => Sheet.IsEmpty;

        public void Clear() => Sheet.Clear();

        public void SetFloat(string name, float value) => Sheet.SetFloat(Shader.PropertyToID(name), value);
        public void SetFloat(int nameID, float value) => Sheet.SetFloat(nameID, value);
        // SetInt writes a float property, as on materials.
        public void SetInt(string name, int value) => Sheet.SetFloat(Shader.PropertyToID(name), value);
        public void SetInt(int nameID, int value) => Sheet.SetFloat(nameID, value);
        public void SetInteger(string name, int value) => Sheet.SetInt(Shader.PropertyToID(name), value);
        public void SetInteger(int nameID, int value) => Sheet.SetInt(nameID, value);
        public void SetColor(string name, Color value) => Sheet.SetVector(Shader.PropertyToID(name), value);
        public void SetColor(int nameID, Color value) => Sheet.SetVector(nameID, value);
        public void SetVector(string name, Vector4 value) => Sheet.SetVector(Shader.PropertyToID(name), value);
        public void SetVector(int nameID, Vector4 value) => Sheet.SetVector(nameID, value);
        public void SetMatrix(string name, Matrix4x4 value) => Sheet.SetMatrix(Shader.PropertyToID(name), value);
        public void SetMatrix(int nameID, Matrix4x4 value) => Sheet.SetMatrix(nameID, value);
        public void SetTexture(string name, Texture value) => Sheet.SetTexture(Shader.PropertyToID(name), value);
        public void SetTexture(int nameID, Texture value) => Sheet.SetTexture(nameID, value);

        public void SetFloatArray(string name, float[] values) => SetFloatArray(Shader.PropertyToID(name), values);
        public void SetFloatArray(int nameID, float[] values)
        {
            Material.CheckArray(values, values?.Length ?? 0);
            Sheet.SetFloatArray(nameID, values, values.Length);
        }

        public void SetFloatArray(string name, List<float> values) => SetFloatArray(Shader.PropertyToID(name), values?.ToArray());
        public void SetFloatArray(int nameID, List<float> values) => SetFloatArray(nameID, values?.ToArray());

        public void SetVectorArray(string name, Vector4[] values) => SetVectorArray(Shader.PropertyToID(name), values);
        public void SetVectorArray(int nameID, Vector4[] values)
        {
            Material.CheckArray(values, values?.Length ?? 0);
            Sheet.SetVectorArray(nameID, values, values.Length);
        }

        public void SetVectorArray(string name, List<Vector4> values) => SetVectorArray(Shader.PropertyToID(name), values?.ToArray());
        public void SetVectorArray(int nameID, List<Vector4> values) => SetVectorArray(nameID, values?.ToArray());

        public float GetFloat(string name) => GetFloat(Shader.PropertyToID(name));
        public float GetFloat(int nameID) => Sheet.Floats.GetValueOrDefault(nameID);
        public int GetInt(string name) => (int)GetFloat(Shader.PropertyToID(name));
        public int GetInt(int nameID) => (int)GetFloat(nameID);
        public Vector4 GetVector(string name) => GetVector(Shader.PropertyToID(name));
        public Vector4 GetVector(int nameID) => Sheet.Vectors.GetValueOrDefault(nameID);
        public Color GetColor(string name) => GetVector(Shader.PropertyToID(name));
        public Color GetColor(int nameID) => GetVector(nameID);
        public Texture GetTexture(string name) => GetTexture(Shader.PropertyToID(name));
        public Texture GetTexture(int nameID) => Sheet.Textures.TryGetValue(nameID, out var t) && t.Texture != null ? t.Texture : null;
    }
}
