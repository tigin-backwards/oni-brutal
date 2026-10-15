using System.Runtime.InteropServices;

namespace UnityEngine.TextCore
{
    // Unity declares Glyph with sequential layout (it is marshalled to the native font engine).
    // Serialized as the elements of TMP_FontAsset.m_GlyphTable; field order is Unity's.
    [StructLayout(LayoutKind.Sequential)]
    public partial class Glyph
    {
        [SerializeField] private uint m_Index;
        [SerializeField] private GlyphMetrics m_Metrics;
        [SerializeField] private GlyphRect m_GlyphRect;
        [SerializeField] private float m_Scale;
        [SerializeField] private int m_AtlasIndex;
        [SerializeField] private GlyphClassDefinitionType m_ClassDefinitionType;

        public uint index { get => m_Index; set => m_Index = value; }
        public GlyphMetrics metrics { get => m_Metrics; set => m_Metrics = value; }
        public GlyphRect glyphRect { get => m_GlyphRect; set => m_GlyphRect = value; }
        public float scale { get => m_Scale; set => m_Scale = value; }
        public int atlasIndex { get => m_AtlasIndex; set => m_AtlasIndex = value; }
        public GlyphClassDefinitionType classDefinitionType { get => m_ClassDefinitionType; set => m_ClassDefinitionType = value; }

        // A new glyph has scale 1; everything else is zero / Undefined.
        public Glyph() : this(0u, default, default, 1f, 0) { }

        // Unity's copy constructor copies index, metrics, rect, scale and atlas index only;
        // the class definition type is left Undefined.
        public Glyph(Glyph glyph) : this(glyph.index, glyph.metrics, glyph.glyphRect, glyph.scale, glyph.atlasIndex) { }

        public Glyph(uint index, GlyphMetrics metrics, GlyphRect glyphRect) : this(index, metrics, glyphRect, 1f, 0) { }

        public Glyph(uint index, GlyphMetrics metrics, GlyphRect glyphRect, float scale, int atlasIndex)
        {
            m_Index = index;
            m_Metrics = metrics;
            m_GlyphRect = glyphRect;
            m_Scale = scale;
            m_AtlasIndex = atlasIndex;
        }

        /// Same fields as the copy constructor; metrics and rect use their exact == operators.
        public bool Compare(Glyph other) =>
            m_Index == other.index
            && m_Metrics == other.metrics
            && m_GlyphRect == other.glyphRect
            && m_Scale == other.scale
            && m_AtlasIndex == other.atlasIndex;
    }

}
