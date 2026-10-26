using System.Diagnostics;

namespace UnityEngine.TextCore.LowLevel
{
    // Serialized in TMP_FontFeatureTable.m_GlyphPairAdjustmentRecords; field order is Unity's.
    [DebuggerDisplay("First glyphIndex = {m_FirstAdjustmentRecord.glyphIndex},  Second glyphIndex = {m_SecondAdjustmentRecord.glyphIndex}")]
    public partial struct GlyphPairAdjustmentRecord
    {
        [SerializeField] private GlyphAdjustmentRecord m_FirstAdjustmentRecord;
        [SerializeField] private GlyphAdjustmentRecord m_SecondAdjustmentRecord;
        [SerializeField] private FontFeatureLookupFlags m_FeatureLookupFlags;

        public GlyphPairAdjustmentRecord(GlyphAdjustmentRecord firstAdjustmentRecord, GlyphAdjustmentRecord secondAdjustmentRecord)
        {
            m_FirstAdjustmentRecord = firstAdjustmentRecord;
            m_SecondAdjustmentRecord = secondAdjustmentRecord;
            m_FeatureLookupFlags = FontFeatureLookupFlags.None;
        }

        public GlyphAdjustmentRecord firstAdjustmentRecord { get => m_FirstAdjustmentRecord; set => m_FirstAdjustmentRecord = value; }
        public GlyphAdjustmentRecord secondAdjustmentRecord { get => m_SecondAdjustmentRecord; set => m_SecondAdjustmentRecord = value; }
        public FontFeatureLookupFlags featureLookupFlags { get => m_FeatureLookupFlags; set => m_FeatureLookupFlags = value; }

        // Unity: Equals/GetHashCode are the runtime's value-type defaults; == compares the two
        // adjustment records and ignores the lookup flags.
        public override int GetHashCode() => base.GetHashCode();
        public override bool Equals(object obj) => base.Equals(obj);
        public bool Equals(GlyphPairAdjustmentRecord other) => base.Equals(other);

        public static bool operator ==(GlyphPairAdjustmentRecord lhs, GlyphPairAdjustmentRecord rhs) =>
            lhs.m_FirstAdjustmentRecord == rhs.m_FirstAdjustmentRecord && lhs.m_SecondAdjustmentRecord == rhs.m_SecondAdjustmentRecord;

        public static bool operator !=(GlyphPairAdjustmentRecord lhs, GlyphPairAdjustmentRecord rhs) => !(lhs == rhs);
    }
}
