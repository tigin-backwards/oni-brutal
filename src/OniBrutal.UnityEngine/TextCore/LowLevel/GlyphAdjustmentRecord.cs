namespace UnityEngine.TextCore.LowLevel
{
    public partial struct GlyphAdjustmentRecord
    {
        [SerializeField] private uint m_GlyphIndex;
        [SerializeField] private GlyphValueRecord m_GlyphValueRecord;

        public GlyphAdjustmentRecord(uint glyphIndex, GlyphValueRecord glyphValueRecord)
        {
            m_GlyphIndex = glyphIndex;
            m_GlyphValueRecord = glyphValueRecord;
        }

        public uint glyphIndex { get => m_GlyphIndex; set => m_GlyphIndex = value; }
        public GlyphValueRecord glyphValueRecord { get => m_GlyphValueRecord; set => m_GlyphValueRecord = value; }

        // Unity: Equals/GetHashCode are the runtime's value-type defaults; == compares the index
        // and the value record with its ==.
        public override int GetHashCode() => base.GetHashCode();
        public override bool Equals(object obj) => base.Equals(obj);
        public bool Equals(GlyphAdjustmentRecord other) => base.Equals(other);

        public static bool operator ==(GlyphAdjustmentRecord lhs, GlyphAdjustmentRecord rhs) =>
            lhs.m_GlyphIndex == rhs.m_GlyphIndex && lhs.m_GlyphValueRecord == rhs.m_GlyphValueRecord;

        public static bool operator !=(GlyphAdjustmentRecord lhs, GlyphAdjustmentRecord rhs) => !(lhs == rhs);
    }
}
