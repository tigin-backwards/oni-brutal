using UnityEngine.TextCore;

namespace TMPro
{
    // A character or sprite of an asset: its unicode, the glyph it draws and a scale.
    public partial class TMP_TextElement
    {
        Glyph m_Glyph;
        TMP_Asset m_TextAsset;

        public TextElementType elementType => m_ElementType;

        public uint unicode { get => m_Unicode; set => m_Unicode = value; }

        // The asset the element belongs to (set when the asset builds its lookups).
        public TMP_Asset textAsset { get => m_TextAsset; set => m_TextAsset = value; }

        public Glyph glyph { get => m_Glyph; set => m_Glyph = value; }

        public uint glyphIndex { get => m_GlyphIndex; set => m_GlyphIndex = value; }

        public float scale { get => m_Scale; set => m_Scale = value; }
    }

    public partial class TMP_Character
    {
        public TMP_Character()
        {
            m_ElementType = TextElementType.Character;
            m_Scale = 1f;
        }

        public TMP_Character(uint unicode, Glyph glyph)
        {
            m_ElementType = TextElementType.Character;
            m_Unicode = unicode;
            this.glyph = glyph;
            m_GlyphIndex = glyph.index;
            m_Scale = 1f;
        }

        public TMP_Character(uint unicode, TMP_FontAsset fontAsset, Glyph glyph) : this(unicode, glyph)
        {
            textAsset = fontAsset;
        }

        internal TMP_Character(uint unicode, uint glyphIndex)
        {
            m_ElementType = TextElementType.Character;
            m_Unicode = unicode;
            m_GlyphIndex = glyphIndex;
            m_Scale = 1f;
        }
    }
}
