namespace UnityEngine
{
    /// A size option of a GUILayout entry (GUILayout.Width, MinHeight, ExpandWidth…).
    public sealed partial class GUILayoutOption
    {
        internal readonly OniMoono.Engine.IMGUI.LayoutRule Rule;
        internal readonly bool Horizontal;
        /// The size, or for Expand 1 (on) / 0 (off).
        internal readonly float Value;

        internal GUILayoutOption(OniMoono.Engine.IMGUI.LayoutRule rule, bool horizontal, float value)
        {
            Rule = rule;
            Horizontal = horizontal;
            Value = value;
        }
    }
}
