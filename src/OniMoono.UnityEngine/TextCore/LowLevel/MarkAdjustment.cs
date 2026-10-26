namespace UnityEngine.TextCore.LowLevel
{
    // Mark positioning records (OpenType GPOS mark-to-base / mark-to-mark). Internal in Unity;
    // TextMeshPro reaches them as an InternalsVisibleTo friend. Serialized in
    // TMP_FontFeatureTable; field orders are Unity's.

    partial struct GlyphAnchorPoint
    {
        [SerializeField] private float m_XCoordinate;
        [SerializeField] private float m_YCoordinate;

        public float xCoordinate { get => m_XCoordinate; set => m_XCoordinate = value; }
        public float yCoordinate { get => m_YCoordinate; set => m_YCoordinate = value; }
    }

    partial struct MarkPositionAdjustment
    {
        [SerializeField] private float m_XPositionAdjustment;
        [SerializeField] private float m_YPositionAdjustment;

        public MarkPositionAdjustment(float x, float y)
        {
            m_XPositionAdjustment = x;
            m_YPositionAdjustment = y;
        }

        public float xPositionAdjustment { get => m_XPositionAdjustment; set => m_XPositionAdjustment = value; }
        public float yPositionAdjustment { get => m_YPositionAdjustment; set => m_YPositionAdjustment = value; }
    }

    partial struct MarkToBaseAdjustmentRecord
    {
        [SerializeField] private uint m_BaseGlyphID;
        [SerializeField] private GlyphAnchorPoint m_BaseGlyphAnchorPoint;
        [SerializeField] private uint m_MarkGlyphID;
        [SerializeField] private MarkPositionAdjustment m_MarkPositionAdjustment;

        public uint baseGlyphID { get => m_BaseGlyphID; set => m_BaseGlyphID = value; }
        public GlyphAnchorPoint baseGlyphAnchorPoint { get => m_BaseGlyphAnchorPoint; set => m_BaseGlyphAnchorPoint = value; }
        public uint markGlyphID { get => m_MarkGlyphID; set => m_MarkGlyphID = value; }
        public MarkPositionAdjustment markPositionAdjustment { get => m_MarkPositionAdjustment; set => m_MarkPositionAdjustment = value; }
    }

    partial struct MarkToMarkAdjustmentRecord
    {
        [SerializeField] private uint m_BaseMarkGlyphID;
        [SerializeField] private GlyphAnchorPoint m_BaseMarkGlyphAnchorPoint;
        [SerializeField] private uint m_CombiningMarkGlyphID;
        [SerializeField] private MarkPositionAdjustment m_CombiningMarkPositionAdjustment;

        public uint baseMarkGlyphID { get => m_BaseMarkGlyphID; set => m_BaseMarkGlyphID = value; }
        public GlyphAnchorPoint baseMarkGlyphAnchorPoint { get => m_BaseMarkGlyphAnchorPoint; set => m_BaseMarkGlyphAnchorPoint = value; }
        public uint combiningMarkGlyphID { get => m_CombiningMarkGlyphID; set => m_CombiningMarkGlyphID = value; }
        public MarkPositionAdjustment combiningMarkPositionAdjustment { get => m_CombiningMarkPositionAdjustment; set => m_CombiningMarkPositionAdjustment = value; }
    }
}
