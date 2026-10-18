namespace UnityEngine
{
    /// How GUI.DrawTexture fits a texture into its rect.
    public enum ScaleMode
    {
        /// The texture covers the whole rect, distorted to its aspect.
        StretchToFill = 0,
        /// The texture keeps its aspect and covers the rect; what overhangs is cropped.
        ScaleAndCrop = 1,
        /// The texture keeps its aspect and fits inside the rect.
        ScaleToFit = 2,
    }

    /// Where a GUIContent's image goes relative to its text.
    public enum ImagePosition
    {
        ImageLeft = 0,
        ImageAbove = 1,
        ImageOnly = 2,
        TextOnly = 3,
    }

    /// What happens to text larger than its content rect.
    public enum TextClipping
    {
        Overflow = 0,
        Clip = 1,
    }

    /// Thrown by GUIUtility.ExitGUI to leave the current OnGUI call; the dispatcher catches it.
    public sealed class ExitGUIException : System.Exception
    {
        public ExitGUIException() { }

        internal ExitGUIException(string message) : base(message) { }
    }
}
