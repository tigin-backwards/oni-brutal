namespace UnityEngine.TextCore.LowLevel
{
    public partial struct GlyphValueRecord
    {
        [SerializeField] private float m_XPlacement;
        [SerializeField] private float m_YPlacement;
        [SerializeField] private float m_XAdvance;
        [SerializeField] private float m_YAdvance;

        public GlyphValueRecord(float xPlacement, float yPlacement, float xAdvance, float yAdvance)
        {
            m_XPlacement = xPlacement;
            m_YPlacement = yPlacement;
            m_XAdvance = xAdvance;
            m_YAdvance = yAdvance;
        }

        public float xPlacement { get => m_XPlacement; set => m_XPlacement = value; }
        public float yPlacement { get => m_YPlacement; set => m_YPlacement = value; }
        public float xAdvance { get => m_XAdvance; set => m_XAdvance = value; }
        public float yAdvance { get => m_YAdvance; set => m_YAdvance = value; }

        public static GlyphValueRecord operator +(GlyphValueRecord a, GlyphValueRecord b) => new GlyphValueRecord(
            a.m_XPlacement + b.m_XPlacement,
            a.m_YPlacement + b.m_YPlacement,
            a.m_XAdvance + b.m_XAdvance,
            a.m_YAdvance + b.m_YAdvance);

        public static GlyphValueRecord operator *(GlyphValueRecord a, float emScale) => new GlyphValueRecord(
            a.m_XPlacement * emScale,
            a.m_YPlacement * emScale,
            a.m_XAdvance * emScale,
            a.m_YAdvance * emScale);

        // Unity: Equals/GetHashCode are the runtime's value-type defaults; == compares the floats.
        public override int GetHashCode() => base.GetHashCode();
        public override bool Equals(object obj) => base.Equals(obj);
        public bool Equals(GlyphValueRecord other) => base.Equals(other);

        public static bool operator ==(GlyphValueRecord lhs, GlyphValueRecord rhs) =>
            lhs.m_XPlacement == rhs.m_XPlacement
            && lhs.m_YPlacement == rhs.m_YPlacement
            && lhs.m_XAdvance == rhs.m_XAdvance
            && lhs.m_YAdvance == rhs.m_YAdvance;

        public static bool operator !=(GlyphValueRecord lhs, GlyphValueRecord rhs) => !(lhs == rhs);
    }
}
