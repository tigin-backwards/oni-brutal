using System.Collections.Generic;

namespace UnityEngine.UI
{
    /// The settings of a legacy Text.
    public partial class FontData
    {
        public FontData() { }

        public static FontData defaultFontData => new FontData
        {
            m_FontSize = 14,
            m_LineSpacing = 1f,
            m_FontStyle = FontStyle.Normal,
            m_BestFit = false,
            m_MinSize = 10,
            m_MaxSize = 40,
            m_Alignment = TextAnchor.UpperLeft,
            m_HorizontalOverflow = HorizontalWrapMode.Wrap,
            m_VerticalOverflow = VerticalWrapMode.Truncate,
            m_RichText = true,
            m_AlignByGeometry = false,
        };

        public Font font { get => m_Font; set => m_Font = value; }
        public int fontSize { get => m_FontSize; set => m_FontSize = value; }
        public FontStyle fontStyle { get => m_FontStyle; set => m_FontStyle = value; }
        public bool bestFit { get => m_BestFit; set => m_BestFit = value; }
        public int minSize { get => m_MinSize; set => m_MinSize = value; }
        public int maxSize { get => m_MaxSize; set => m_MaxSize = value; }
        public TextAnchor alignment { get => m_Alignment; set => m_Alignment = value; }
        public bool alignByGeometry { get => m_AlignByGeometry; set => m_AlignByGeometry = value; }
        public bool richText { get => m_RichText; set => m_RichText = value; }
        public HorizontalWrapMode horizontalOverflow { get => m_HorizontalOverflow; set => m_HorizontalOverflow = value; }
        public VerticalWrapMode verticalOverflow { get => m_VerticalOverflow; set => m_VerticalOverflow = value; }
        public float lineSpacing { get => m_LineSpacing; set => m_LineSpacing = value; }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        /// Brings serialized sizes into range: 0 to 300, the minimum not above the maximum.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_FontSize = Mathf.Clamp(m_FontSize, 0, 300);
            m_MinSize = Mathf.Clamp(m_MinSize, 0, m_FontSize);
            m_MaxSize = Mathf.Clamp(m_MaxSize, m_FontSize, 300);
        }
    }

    /// Texts redraw when their font's texture is rebuilt (dynamic fonts add glyphs to it).
    public static class FontUpdateTracker
    {
        static readonly Dictionary<Font, HashSet<Text>> byFont = new();
        static bool hooked;

        public static void TrackText(Text t)
        {
            if (t == null || t.font == null) return;
            if (!byFont.TryGetValue(t.font, out var texts))
            {
                if (!hooked)
                {
                    Font.textureRebuilt += Rebuilt;
                    hooked = true;
                }
                byFont[t.font] = texts = new HashSet<Text>();
            }
            texts.Add(t);
        }

        public static void UntrackText(Text t)
        {
            if (t == null || t.font == null) return;
            if (!byFont.TryGetValue(t.font, out var texts)) return;
            texts.Remove(t);
            if (texts.Count == 0) byFont.Remove(t.font);
        }

        static void Rebuilt(Font font)
        {
            if (!byFont.TryGetValue(font, out var texts)) return;
            foreach (var t in new List<Text>(texts)) t.FontTextureChanged();
        }
    }

    /// Legacy text, laid out by the engine's TextGenerator.
    public partial class Text
    {
        protected static Material s_DefaultText = null;

        [System.NonSerialized] protected bool m_DisableFontTextureRebuiltCallback = false;

        TextGenerator meshGenerator;
        TextGenerator layoutGenerator;
        readonly UIVertex[] quad = new UIVertex[4];

        protected Text()
        {
            m_FontData = FontData.defaultFontData;
            m_Text = string.Empty;
            useLegacyMeshGeneration = false;
        }

        public TextGenerator cachedTextGenerator => meshGenerator ??= m_Text.Length != 0 ? new TextGenerator(m_Text.Length) : new TextGenerator();

        public TextGenerator cachedTextGeneratorForLayout => layoutGenerator ??= new TextGenerator();

        /// The font's texture.
        public override Texture mainTexture
        {
            get
            {
                if (font != null && font.material != null && font.material.mainTexture != null) return font.material.mainTexture;
                if (m_Material != null) return m_Material.mainTexture;
                return base.mainTexture;
            }
        }

        /// The font texture changed: the text lays out again.
        public void FontTextureChanged()
        {
            if (this == null) return;
            if (m_DisableFontTextureRebuiltCallback) return;
            cachedTextGenerator.Invalidate();
            if (!IsActive()) return;
            if (CanvasUpdateRegistry.IsRebuildingGraphics() || CanvasUpdateRegistry.IsRebuildingLayout()) UpdateGeometry();
            else
            {
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }

        public Font font
        {
            get => m_FontData.font;
            set
            {
                if (m_FontData.font == value) return;
                if (isActiveAndEnabled) FontUpdateTracker.UntrackText(this);
                m_FontData.font = value;
                if (isActiveAndEnabled) FontUpdateTracker.TrackText(this);
                SetAllDirty();
            }
        }

        public virtual string text
        {
            get => m_Text;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    if (string.IsNullOrEmpty(m_Text)) return;
                    m_Text = "";
                    SetVerticesDirty();
                }
                else if (m_Text != value)
                {
                    m_Text = value;
                    SetVerticesDirty();
                    SetLayoutDirty();
                }
            }
        }

        public bool supportRichText
        {
            get => m_FontData.richText;
            set { if (m_FontData.richText == value) return; m_FontData.richText = value; SetVerticesDirty(); SetLayoutDirty(); }
        }

        public bool resizeTextForBestFit
        {
            get => m_FontData.bestFit;
            set { if (m_FontData.bestFit == value) return; m_FontData.bestFit = value; SetVerticesDirty(); SetLayoutDirty(); }
        }

        public int resizeTextMinSize
        {
            get => m_FontData.minSize;
            set { if (m_FontData.minSize == value) return; m_FontData.minSize = value; SetVerticesDirty(); SetLayoutDirty(); }
        }

        public int resizeTextMaxSize
        {
            get => m_FontData.maxSize;
            set { if (m_FontData.maxSize == value) return; m_FontData.maxSize = value; SetVerticesDirty(); SetLayoutDirty(); }
        }

        public TextAnchor alignment
        {
            get => m_FontData.alignment;
            set { if (m_FontData.alignment == value) return; m_FontData.alignment = value; SetVerticesDirty(); SetLayoutDirty(); }
        }

        public bool alignByGeometry
        {
            get => m_FontData.alignByGeometry;
            set { if (m_FontData.alignByGeometry == value) return; m_FontData.alignByGeometry = value; SetVerticesDirty(); }
        }

        public int fontSize
        {
            get => m_FontData.fontSize;
            set { if (m_FontData.fontSize == value) return; m_FontData.fontSize = value; SetVerticesDirty(); SetLayoutDirty(); }
        }

        public HorizontalWrapMode horizontalOverflow
        {
            get => m_FontData.horizontalOverflow;
            set { if (m_FontData.horizontalOverflow == value) return; m_FontData.horizontalOverflow = value; SetVerticesDirty(); SetLayoutDirty(); }
        }

        public VerticalWrapMode verticalOverflow
        {
            get => m_FontData.verticalOverflow;
            set { if (m_FontData.verticalOverflow == value) return; m_FontData.verticalOverflow = value; SetVerticesDirty(); SetLayoutDirty(); }
        }

        public float lineSpacing
        {
            get => m_FontData.lineSpacing;
            set { if (m_FontData.lineSpacing == value) return; m_FontData.lineSpacing = value; SetVerticesDirty(); SetLayoutDirty(); }
        }

        public FontStyle fontStyle
        {
            get => m_FontData.fontStyle;
            set { if (m_FontData.fontStyle == value) return; m_FontData.fontStyle = value; SetVerticesDirty(); SetLayoutDirty(); }
        }

        /// Font pixels per canvas unit: the canvas scale for dynamic fonts; for a fixed-size font,
        /// its size over the text's.
        public float pixelsPerUnit
        {
            get
            {
                var c = canvas;
                if (c == null) return 1f;
                if (font == null || font.dynamic) return c.scaleFactor;
                if (m_FontData.fontSize <= 0 || font.fontSize <= 0) return 1f;
                return font.fontSize / (float)m_FontData.fontSize;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            cachedTextGenerator.Invalidate();
            FontUpdateTracker.TrackText(this);
        }

        protected override void OnDisable()
        {
            FontUpdateTracker.UntrackText(this);
            base.OnDisable();
        }

        protected override void UpdateGeometry()
        {
            if (font != null) base.UpdateGeometry();
        }

        public TextGenerationSettings GetGenerationSettings(Vector2 extents)
        {
            var s = new TextGenerationSettings { generationExtents = extents };
            if (font != null && font.dynamic)
            {
                s.fontSize = m_FontData.fontSize;
                s.resizeTextMinSize = m_FontData.minSize;
                s.resizeTextMaxSize = m_FontData.maxSize;
            }
            s.textAnchor = m_FontData.alignment;
            s.alignByGeometry = m_FontData.alignByGeometry;
            s.scaleFactor = pixelsPerUnit;
            s.color = color;
            s.font = font;
            s.pivot = rectTransform.pivot;
            s.richText = m_FontData.richText;
            s.lineSpacing = m_FontData.lineSpacing;
            s.fontStyle = m_FontData.fontStyle;
            s.resizeTextForBestFit = m_FontData.bestFit;
            s.updateBounds = false;
            s.horizontalOverflow = m_FontData.horizontalOverflow;
            s.verticalOverflow = m_FontData.verticalOverflow;
            return s;
        }

        public static Vector2 GetTextAnchorPivot(TextAnchor anchor) => anchor switch
        {
            TextAnchor.LowerLeft => new Vector2(0f, 0f),
            TextAnchor.LowerCenter => new Vector2(0.5f, 0f),
            TextAnchor.LowerRight => new Vector2(1f, 0f),
            TextAnchor.MiddleLeft => new Vector2(0f, 0.5f),
            TextAnchor.MiddleCenter => new Vector2(0.5f, 0.5f),
            TextAnchor.MiddleRight => new Vector2(1f, 0.5f),
            TextAnchor.UpperLeft => new Vector2(0f, 1f),
            TextAnchor.UpperCenter => new Vector2(0.5f, 1f),
            TextAnchor.UpperRight => new Vector2(1f, 1f),
            _ => Vector2.zero,
        };

        /// The generator's quads, scaled from font pixels to canvas units; on a pixel-perfect
        /// canvas shifted so the first vertex lands on a pixel.
        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            if (font == null) return;
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                var settings = GetGenerationSettings(rectTransform.rect.size);
                cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
                var verts = cachedTextGenerator.verts;
                float unitsPerPixel = 1f / pixelsPerUnit;
                int count = verts.Count;
                toFill.Clear();
                if (count <= 0) return;
                var first = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
                var shift = PixelAdjustPoint(first) - first;
                for (int i = 0; i < count; i++)
                {
                    var v = verts[i];
                    v.position *= unitsPerPixel;
                    v.position.x += shift.x;
                    v.position.y += shift.y;
                    quad[i & 3] = v;
                    if ((i & 3) == 3) toFill.AddUIVertexQuad(quad);
                }
            }
            finally { m_DisableFontTextureRebuiltCallback = false; }
        }

        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }

        public virtual float minWidth => 0f;

        /// The unwrapped width of the text.
        public virtual float preferredWidth => cachedTextGeneratorForLayout.GetPreferredWidth(m_Text, GetGenerationSettings(Vector2.zero)) / pixelsPerUnit;

        public virtual float flexibleWidth => -1f;

        public virtual float minHeight => 0f;

        /// The height of the text wrapped at the current width.
        public virtual float preferredHeight => cachedTextGeneratorForLayout.GetPreferredHeight(m_Text, GetGenerationSettings(new Vector2(GetPixelAdjustedRect().size.x, 0f))) / pixelsPerUnit;

        public virtual float flexibleHeight => -1f;

        public virtual int layoutPriority => 0;

    }
}
