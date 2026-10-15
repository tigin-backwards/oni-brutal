namespace UnityEngine.TextCore.LowLevel
{
    // Internal in Unity; TextMeshPro reaches it as an InternalsVisibleTo friend.
    partial struct LigatureSubstitutionRecord
    {
        [SerializeField] private uint[] m_ComponentGlyphIDs;
        [SerializeField] private uint m_LigatureGlyphID;

        public uint[] componentGlyphIDs { get => m_ComponentGlyphIDs; set => m_ComponentGlyphIDs = value; }
        public uint ligatureGlyphID { get => m_LigatureGlyphID; set => m_LigatureGlyphID = value; }

        public bool Equals(LigatureSubstitutionRecord other) => this == other;
        public override bool Equals(object obj) => obj is LigatureSubstitutionRecord other && this == other;

        // Unity hashes the component array by reference identity (so equal records can hash
        // differently) and throws NullReferenceException when the array is null.
        public override int GetHashCode() => m_ComponentGlyphIDs.GetHashCode();

        // Equal when the component sequences are element-wise equal (two null arrays are equal,
        // null and non-null are not) and the ligature glyph matches.
        public static bool operator ==(LigatureSubstitutionRecord lhs, LigatureSubstitutionRecord rhs)
        {
            uint[] a = lhs.m_ComponentGlyphIDs, b = rhs.m_ComponentGlyphIDs;
            if (a == null || b == null)
            {
                if (a != b) return false;
            }
            else
            {
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) return false;
            }
            return lhs.m_LigatureGlyphID == rhs.m_LigatureGlyphID;
        }

        public static bool operator !=(LigatureSubstitutionRecord lhs, LigatureSubstitutionRecord rhs) => !(lhs == rhs);
    }
}
