namespace UnityEngine
{
    /// A skin's text-field behaviour and colours.
    public sealed partial class GUISettings
    {
        [SerializeField] bool m_DoubleClickSelectsWord = true;
        [SerializeField] bool m_TripleClickSelectsLine = true;
        [SerializeField] Color m_CursorColor = Color.white;
        [SerializeField] float m_CursorFlashSpeed = -1f;
        [SerializeField] Color m_SelectionColor = new(0.5f, 0.5f, 1f);

        public bool doubleClickSelectsWord
        {
            get => m_DoubleClickSelectsWord;
            set => m_DoubleClickSelectsWord = value;
        }

        public bool tripleClickSelectsLine
        {
            get => m_TripleClickSelectsLine;
            set => m_TripleClickSelectsLine = value;
        }

        public Color cursorColor
        {
            get => m_CursorColor;
            set => m_CursorColor = value;
        }

        /// Cursor flashes per second. A negative setting means the system's rate (the
        /// desktop default: one 530 ms on/off phase each, so 1 / 1.06 s).
        public float cursorFlashSpeed
        {
            get => m_CursorFlashSpeed >= 0f ? m_CursorFlashSpeed : 1f / 1.06f;
            set => m_CursorFlashSpeed = value;
        }

        public Color selectionColor
        {
            get => m_SelectionColor;
            set => m_SelectionColor = value;
        }
    }
}
