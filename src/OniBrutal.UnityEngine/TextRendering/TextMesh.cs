using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine;
using OniBrutal.Engine.Serialization;
using OniBrutal.Engine.Text;

namespace UnityEngine
{
    public partial class TextMesh
    {
        TextMeshNative Data => (TextMeshNative)NativeOrThrow;

        public string text
        {
            get => Data.Text;
            set => Data.Text = value ?? "";
        }

        public Font font
        {
            get => Data.Font;
            set => Data.Font = value;
        }

        /// 0 uses the font's own size.
        public int fontSize
        {
            get => Data.FontSize;
            set => Data.FontSize = value;
        }

        public FontStyle fontStyle
        {
            get => Data.FontStyle;
            set => Data.FontStyle = value;
        }

        public float offsetZ
        {
            get => Data.OffsetZ;
            set => Data.OffsetZ = value;
        }

        public TextAnchor anchor
        {
            get => Data.Anchor;
            set => Data.Anchor = value;
        }

        public float characterSize
        {
            get => Data.CharacterSize;
            set => Data.CharacterSize = value;
        }

        public float lineSpacing
        {
            get => Data.LineSpacing;
            set => Data.LineSpacing = value;
        }

        public float tabSize
        {
            get => Data.TabSize;
            set => Data.TabSize = value;
        }

        public bool richText
        {
            get => Data.RichText;
            set => Data.RichText = value;
        }

        public Color color
        {
            get => Data.Color;
            set => Data.Color = value;
        }
    }
}

namespace OniBrutal.Engine.Text
{
    using UnityEngine;

    /// Engine side of TextMesh (native class 102). Defaults are those of a TextMesh added at
    /// runtime.
    internal sealed class TextMeshNative : ComponentNative
    {
        public string Text = "Hello World";
        public float OffsetZ;
        public float CharacterSize = 1f;
        public float LineSpacing = 1f;
        public TextAnchor Anchor = TextAnchor.UpperLeft;
        /// TextAlignment (Left, Center, Right) of lines within the block.
        public int Alignment;
        public float TabSize = 4f;
        public int FontSize;
        public FontStyle FontStyle;
        public bool RichText = true;
        public Font Font;
        public Color Color = Color.white;

        public override void CopyStateFrom(NativeObject source)
        {
            var t = (TextMeshNative)source;
            Text = t.Text;
            OffsetZ = t.OffsetZ;
            CharacterSize = t.CharacterSize;
            LineSpacing = t.LineSpacing;
            Anchor = t.Anchor;
            Alignment = t.Alignment;
            TabSize = t.TabSize;
            FontSize = t.FontSize;
            FontStyle = t.FontStyle;
            RichText = t.RichText;
            Font = t.Font;
            Color = t.Color;
        }

        public override void Bind(Fields f, IReferenceResolver resolver)
        {
            Serialized = null;
            Text = f.String("m_Text");
            OffsetZ = f.Float("m_OffsetZ");
            CharacterSize = f.Float("m_CharacterSize");
            LineSpacing = f.Float("m_LineSpacing");
            Anchor = (TextAnchor)f.Get<short>("m_Anchor");
            Alignment = f.Get<short>("m_Alignment");
            TabSize = f.Float("m_TabSize");
            FontSize = f.Int("m_FontSize");
            FontStyle = (FontStyle)f.Int("m_FontStyle");
            RichText = f.Bool("m_RichText");
            Font = resolver.Resolve(f.PPtr("m_Font")) as Font;
            // ColorRGBA: packed bytes, red in the lowest.
            uint rgba = f.Struct("m_Color").Get<uint>("rgba");
            Color = new Color32((byte)rgba, (byte)(rgba >> 8), (byte)(rgba >> 16), (byte)(rgba >> 24));
        }
    }
}
