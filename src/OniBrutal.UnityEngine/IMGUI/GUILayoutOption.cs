namespace UnityEngine
{
    /// A size option of a GUILayout entry (GUILayout.Width, MinHeight, ExpandWidth…).
    public sealed partial class GUILayoutOption
    {
        internal readonly OniBrutal.Engine.IMGUI.LayoutRule Rule;
        internal readonly bool Horizontal;
        /// The size, or for Expand 1 (on) / 0 (off).
        internal readonly float Value;

        internal GUILayoutOption(OniBrutal.Engine.IMGUI.LayoutRule rule, bool horizontal, float value)
        {
            Rule = rule;
            Horizontal = horizontal;
            Value = value;
        }
    }
}
