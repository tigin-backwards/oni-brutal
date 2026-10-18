namespace UnityEngine.TextCore
{
    public partial struct GlyphRect
    {
        [SerializeField] private int m_X;
        [SerializeField] private int m_Y;
        [SerializeField] private int m_Width;
        [SerializeField] private int m_Height;

        private static readonly GlyphRect ZeroRect = new GlyphRect(0, 0, 0, 0);

        public GlyphRect(int x, int y, int width, int height)
        {
            m_X = x;
            m_Y = y;
            m_Width = width;
            m_Height = height;
        }

        // Each component is truncated toward zero (C# float-to-int conversion), as in Unity.
        public GlyphRect(Rect rect) : this((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height) { }

        public int x { get => m_X; set => m_X = value; }
        public int y { get => m_Y; set => m_Y = value; }
        public int width { get => m_Width; set => m_Width = value; }
        public int height { get => m_Height; set => m_Height = value; }

        public static GlyphRect zero => ZeroRect;

        // Unity: Equals/GetHashCode are the runtime's value-type defaults; == compares components.
        public override int GetHashCode() => base.GetHashCode();
        public override bool Equals(object obj) => base.Equals(obj);
        public bool Equals(GlyphRect other) => base.Equals(other);

        public static bool operator ==(GlyphRect lhs, GlyphRect rhs) =>
            lhs.m_X == rhs.m_X && lhs.m_Y == rhs.m_Y && lhs.m_Width == rhs.m_Width && lhs.m_Height == rhs.m_Height;

        public static bool operator !=(GlyphRect lhs, GlyphRect rhs) => !(lhs == rhs);
    }
}
