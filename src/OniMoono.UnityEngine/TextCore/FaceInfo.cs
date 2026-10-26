namespace UnityEngine.TextCore
{
    // Serialized inside TMP_FontAsset (m_FaceInfo). Field names, types and order are Unity's
    // and are what the asset loader matches against the type tree; do not reorder.
    public partial struct FaceInfo
    {
        [SerializeField] private int m_FaceIndex;
        [SerializeField] private string m_FamilyName;
        [SerializeField] private string m_StyleName;
        [SerializeField] private float m_PointSize;
        [SerializeField] private float m_Scale;
        [SerializeField] private int m_UnitsPerEM;
        [SerializeField] private float m_LineHeight;
        [SerializeField] private float m_AscentLine;
        [SerializeField] private float m_CapLine;
        [SerializeField] private float m_MeanLine;
        [SerializeField] private float m_Baseline;
        [SerializeField] private float m_DescentLine;
        [SerializeField] private float m_SuperscriptOffset;
        [SerializeField] private float m_SuperscriptSize;
        [SerializeField] private float m_SubscriptOffset;
        [SerializeField] private float m_SubscriptSize;
        [SerializeField] private float m_UnderlineOffset;
        [SerializeField] private float m_UnderlineThickness;
        [SerializeField] private float m_StrikethroughOffset;
        [SerializeField] private float m_StrikethroughThickness;
        [SerializeField] private float m_TabWidth;

        // Internal in Unity (TextMeshPro reaches it as an InternalsVisibleTo friend).
        internal int faceIndex { get => m_FaceIndex; set => m_FaceIndex = value; }
        public string familyName { get => m_FamilyName; set => m_FamilyName = value; }
        public string styleName { get => m_StyleName; set => m_StyleName = value; }
        public float pointSize { get => m_PointSize; set => m_PointSize = value; }
        public float scale { get => m_Scale; set => m_Scale = value; }
        // Internal in Unity, like faceIndex.
        internal int unitsPerEM { get => m_UnitsPerEM; set => m_UnitsPerEM = value; }
        public float lineHeight { get => m_LineHeight; set => m_LineHeight = value; }
        public float ascentLine { get => m_AscentLine; set => m_AscentLine = value; }
        public float capLine { get => m_CapLine; set => m_CapLine = value; }
        public float meanLine { get => m_MeanLine; set => m_MeanLine = value; }
        public float baseline { get => m_Baseline; set => m_Baseline = value; }
        public float descentLine { get => m_DescentLine; set => m_DescentLine = value; }
        public float superscriptOffset { get => m_SuperscriptOffset; set => m_SuperscriptOffset = value; }
        public float superscriptSize { get => m_SuperscriptSize; set => m_SuperscriptSize = value; }
        public float subscriptOffset { get => m_SubscriptOffset; set => m_SubscriptOffset = value; }
        public float subscriptSize { get => m_SubscriptSize; set => m_SubscriptSize = value; }
        public float underlineOffset { get => m_UnderlineOffset; set => m_UnderlineOffset = value; }
        public float underlineThickness { get => m_UnderlineThickness; set => m_UnderlineThickness = value; }
        public float strikethroughOffset { get => m_StrikethroughOffset; set => m_StrikethroughOffset = value; }
        public float strikethroughThickness { get => m_StrikethroughThickness; set => m_StrikethroughThickness = value; }
        public float tabWidth { get => m_TabWidth; set => m_TabWidth = value; }

        /// Unity's rule: names, face index and point size compare exactly; every other metric
        /// (including the integer units-per-EM) compares with an absolute tolerance of 0.001.
        public bool Compare(FaceInfo other)
        {
            if (m_FamilyName != other.m_FamilyName || m_StyleName != other.m_StyleName) return false;
            if (m_FaceIndex != other.m_FaceIndex || m_PointSize != other.m_PointSize) return false;
            return Near(m_Scale, other.m_Scale)
                && Near(m_UnitsPerEM, other.m_UnitsPerEM)
                && Near(m_LineHeight, other.m_LineHeight)
                && Near(m_AscentLine, other.m_AscentLine)
                && Near(m_CapLine, other.m_CapLine)
                && Near(m_MeanLine, other.m_MeanLine)
                && Near(m_Baseline, other.m_Baseline)
                && Near(m_DescentLine, other.m_DescentLine)
                && Near(m_SuperscriptOffset, other.m_SuperscriptOffset)
                && Near(m_SuperscriptSize, other.m_SuperscriptSize)
                && Near(m_SubscriptOffset, other.m_SubscriptOffset)
                && Near(m_SubscriptSize, other.m_SubscriptSize)
                && Near(m_UnderlineOffset, other.m_UnderlineOffset)
                && Near(m_UnderlineThickness, other.m_UnderlineThickness)
                && Near(m_StrikethroughOffset, other.m_StrikethroughOffset)
                && Near(m_StrikethroughThickness, other.m_StrikethroughThickness)
                && Near(m_TabWidth, other.m_TabWidth);
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;
    }
}
