namespace TMPro
{
    // Data of TextMeshPro's older asset format, kept so that such assets deserialize. The current
    // tables (glyphTable, characterTable, fontFeatureTable) are what text layout reads.
    public partial class TMP_Glyph
    {
    }

    public partial class TMP_Sprite
    {
    }

    public partial class KerningTable
    {
        public KerningTable()
        {
            kerningPairs = new System.Collections.Generic.List<KerningPair>();
        }
    }

    public partial class KerningPair
    {
        public uint firstGlyph { get => m_FirstGlyph; set => m_FirstGlyph = value; }
        public GlyphValueRecord_Legacy firstGlyphAdjustments => m_FirstGlyphAdjustments;
        public uint secondGlyph { get => m_SecondGlyph; set => m_SecondGlyph = value; }
        public GlyphValueRecord_Legacy secondGlyphAdjustments => m_SecondGlyphAdjustments;
        public bool ignoreSpacingAdjustments => m_IgnoreSpacingAdjustments;
    }
}
