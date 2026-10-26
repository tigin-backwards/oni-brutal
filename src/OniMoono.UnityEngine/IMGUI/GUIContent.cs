namespace UnityEngine
{
    /// What a control shows: text, an image, and a tooltip.
    public partial class GUIContent
    {
        [SerializeField] string m_Text = string.Empty;
        [SerializeField] Texture m_Image;
        [SerializeField] string m_Tooltip = string.Empty;
        [SerializeField] string m_TextWithWhitespace = string.Empty;

        /// Empty content.
        public static GUIContent none = new("");

        // Reused for the string and texture overloads of the controls, which draw or measure
        // the content at once and do not keep it.
        static readonly GUIContent scratch = new();

        public GUIContent() { }

        public GUIContent(string text) : this(text, null, string.Empty) { }

        public GUIContent(Texture image) : this(string.Empty, image, string.Empty) { }

        public GUIContent(string text, Texture image) : this(text, image, string.Empty) { }

        public GUIContent(string text, string tooltip) : this(text, null, tooltip) { }

        public GUIContent(Texture image, string tooltip) : this(string.Empty, image, tooltip) { }

        public GUIContent(string text, Texture image, string tooltip)
        {
            this.text = text;
            m_Image = image;
            this.tooltip = tooltip;
        }

        public GUIContent(GUIContent src)
        {
            if (src != null) CopyFrom(src);
        }

        internal void CopyFrom(GUIContent src)
        {
            m_Text = src.m_Text;
            m_Image = src.m_Image;
            m_Tooltip = src.m_Tooltip;
            m_TextWithWhitespace = src.m_TextWithWhitespace;
        }

        public string text
        {
            get => m_Text;
            set
            {
                m_Text = value ?? string.Empty;
                m_TextWithWhitespace = m_Text;
            }
        }

        public Texture image
        {
            get => m_Image;
            set => m_Image = value;
        }

        public string tooltip
        {
            get => m_Tooltip;
            set => m_Tooltip = value ?? string.Empty;
        }

        internal static GUIContent Scratch(string text)
        {
            scratch.text = text;
            scratch.m_Image = null;
            scratch.m_Tooltip = string.Empty;
            return scratch;
        }

        internal static GUIContent Scratch(Texture image)
        {
            scratch.text = string.Empty;
            scratch.m_Image = image;
            scratch.m_Tooltip = string.Empty;
            return scratch;
        }
    }
}
