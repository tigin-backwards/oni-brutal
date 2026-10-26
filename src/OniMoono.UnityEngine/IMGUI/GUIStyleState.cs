namespace UnityEngine
{
    /// One state of a GUIStyle (normal, hover, active, focused and their "on" variants): its
    /// background and text colour.
    public sealed partial class GUIStyleState
    {
        // Filled by the serializer's GUIStyle reader (ScriptData.ReadGUIStyle).
        internal Texture2D m_Background;
        internal Color m_TextColor;
        Texture2D[] scaled = System.Array.Empty<Texture2D>();

        public GUIStyleState() { }

        public Texture2D background
        {
            get => m_Background;
            set => m_Background = value;
        }

        public Color textColor
        {
            get => m_TextColor;
            set => m_TextColor = value;
        }

        /// Backgrounds for higher pixel densities; the player draws at one pixel per point, so
        /// only `background` is drawn.
        public Texture2D[] scaledBackgrounds
        {
            get => scaled;
            set => scaled = value ?? System.Array.Empty<Texture2D>();
        }

        internal void CopyFrom(GUIStyleState other)
        {
            m_Background = other.m_Background;
            m_TextColor = other.m_TextColor;
            scaled = (Texture2D[])other.scaled.Clone();
        }
    }
}
