using System;

namespace UnityEngine
{
    public partial struct RefreshRate
    {
        public uint numerator;
        public uint denominator;

        public double value => denominator == 0 ? double.NaN : (double)numerator / denominator;

        public bool Equals(RefreshRate other)
        {
            if (denominator == 0) return other.denominator == 0;
            if (other.denominator == 0) return false;
            return (ulong)numerator * other.denominator == (ulong)denominator * other.numerator;
        }

        public int CompareTo(RefreshRate other)
        {
            if (denominator == 0) return other.denominator == 0 ? 0 : 1;
            if (other.denominator == 0) return -1;
            return ((ulong)numerator * other.denominator).CompareTo((ulong)denominator * other.numerator);
        }

        public override string ToString() => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public partial struct SecondarySpriteTexture
    {
        public string name;
        public Texture2D texture;

        public override bool Equals(object obj) => obj is SecondarySpriteTexture other && Equals(other);
        public bool Equals(SecondarySpriteTexture other) => name == other.name && texture == other.texture;
        public override int GetHashCode() => HashCode.Combine(name, texture);
        public static bool operator ==(SecondarySpriteTexture lhs, SecondarySpriteTexture rhs) => lhs.Equals(rhs);
        public static bool operator !=(SecondarySpriteTexture lhs, SecondarySpriteTexture rhs) => !lhs.Equals(rhs);
    }

    public partial struct RenderBuffer
    {
        internal int m_RenderTextureInstanceID;
        internal IntPtr m_BufferPtr;
    }
}

namespace UnityEngine.SceneManagement
{
    public partial struct SceneHandle
    {
        internal EntityId m_Value;

        internal SceneHandle(int value) => m_Value = value;

        public bool Equals(SceneHandle other) => m_Value == other.m_Value;
        public override bool Equals(object obj) => obj is SceneHandle other && Equals(other);
        public override int GetHashCode() => m_Value.GetHashCode();
        public static bool operator ==(SceneHandle a, SceneHandle b) => a.m_Value == b.m_Value;
        public static bool operator !=(SceneHandle a, SceneHandle b) => a.m_Value != b.m_Value;
        public override string ToString() => m_Value.ToString();
    }
}

namespace UnityEngine.Rendering
{
    public partial struct RenderTargetIdentifier
    {
        private int m_Type; // BuiltinRenderTextureType
        private int m_NameID;
        private int m_InstanceID;
        private IntPtr m_BufferPointer;
        private int m_MipLevel;
        private int m_CubeFace; // CubemapFace
        private int m_DepthSlice;

        public bool Equals(RenderTargetIdentifier rhs)
            => m_Type == rhs.m_Type && m_NameID == rhs.m_NameID && m_InstanceID == rhs.m_InstanceID && m_BufferPointer == rhs.m_BufferPointer
            && m_MipLevel == rhs.m_MipLevel && m_CubeFace == rhs.m_CubeFace && m_DepthSlice == rhs.m_DepthSlice;

        public override bool Equals(object obj) => obj is RenderTargetIdentifier other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_Type, m_NameID, m_InstanceID, m_BufferPointer, m_MipLevel, m_CubeFace, m_DepthSlice);
        public static bool operator ==(RenderTargetIdentifier lhs, RenderTargetIdentifier rhs) => lhs.Equals(rhs);
        public static bool operator !=(RenderTargetIdentifier lhs, RenderTargetIdentifier rhs) => !lhs.Equals(rhs);
    }
}

namespace UnityEngine.UIElements
{
    partial struct StyleValueHandle
    {
        [SerializeField] private StyleValueType m_ValueType;
        [SerializeField] internal int valueIndex;

        internal StyleValueHandle(int valueIndex, StyleValueType valueType)
        {
            this.valueIndex = valueIndex;
            m_ValueType = valueType;
        }

        public bool Equals(StyleValueHandle other) => m_ValueType == other.m_ValueType && valueIndex == other.valueIndex;
        public override bool Equals(object obj) => obj is StyleValueHandle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(m_ValueType, valueIndex);
    }
}

namespace UnityEngine.UIElements.StyleSheets
{
    partial struct Dimension
    {
        public Unit unit;
        public float value;

        public Dimension(float value, Unit unit)
        {
            this.unit = unit;
            this.value = value;
        }

        public bool Equals(Dimension other) => unit == other.unit && value.Equals(other.value);
        public override bool Equals(object obj) => obj is Dimension other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(unit, value);
    }
}
