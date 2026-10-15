namespace UnityEngine.TextCore
{
    public partial struct GlyphMetrics
    {
        [SerializeField] private float m_Width;
        [SerializeField] private float m_Height;
        [SerializeField] private float m_HorizontalBearingX;
        [SerializeField] private float m_HorizontalBearingY;
        [SerializeField] private float m_HorizontalAdvance;

        public GlyphMetrics(float width, float height, float bearingX, float bearingY, float advance)
        {
            m_Width = width;
            m_Height = height;
            m_HorizontalBearingX = bearingX;
            m_HorizontalBearingY = bearingY;
            m_HorizontalAdvance = advance;
        }

        public float width { get => m_Width; set => m_Width = value; }
        public float height { get => m_Height; set => m_Height = value; }
        public float horizontalBearingX { get => m_HorizontalBearingX; set => m_HorizontalBearingX = value; }
        public float horizontalBearingY { get => m_HorizontalBearingY; set => m_HorizontalBearingY = value; }
        public float horizontalAdvance { get => m_HorizontalAdvance; set => m_HorizontalAdvance = value; }

        // Unity: Equals/GetHashCode are the runtime's value-type defaults (field-wise), while ==
        // compares the five floats with IEEE equality.
        public override int GetHashCode() => base.GetHashCode();
        public override bool Equals(object obj) => base.Equals(obj);
        public bool Equals(GlyphMetrics other) => base.Equals(other);

        public static bool operator ==(GlyphMetrics lhs, GlyphMetrics rhs) =>
            lhs.m_Width == rhs.m_Width
            && lhs.m_Height == rhs.m_Height
            && lhs.m_HorizontalBearingX == rhs.m_HorizontalBearingX
            && lhs.m_HorizontalBearingY == rhs.m_HorizontalBearingY
            && lhs.m_HorizontalAdvance == rhs.m_HorizontalAdvance;

        public static bool operator !=(GlyphMetrics lhs, GlyphMetrics rhs) => !(lhs == rhs);
    }
}
