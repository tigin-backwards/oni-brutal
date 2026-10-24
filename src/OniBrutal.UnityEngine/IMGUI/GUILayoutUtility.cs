using System;
using System.Runtime.CompilerServices;
using OniBrutal.Engine.IMGUI;

namespace UnityEngine
{
    /// Reserves rects in the current GUILayout group. During the Layout event the calls
    /// record entries and return a dummy rect; during the following events the same calls
    /// return the rects the layout computed.
    public partial class GUILayoutUtility
    {
        /// The calling container's layout; GUILayout needs OnGUI and useGUILayout.
        internal static LayoutPass Pass
        {
            get
            {
                GUIStateData.RequireGUI();
                return GUIStateData.Layout ?? throw new ArgumentException("GUILayout functions cannot be used while the behaviour's useGUILayout is false.");
            }
        }

        /// A call past the last entry of its group during Repaint. It is thrown here, in the
        /// UnityEngine namespace, because the player's logged trace starts with the frame that
        /// throws.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowPastTheEnd(int index, int count)
            => throw new ArgumentException($"Getting control {index}'s position in a group with only {count} controls when doing {Event.current.type}\nAborting");

        public static Rect GetRect(float width, float height) => GetRect(width, height, GUIStyle.none, null);

        public static Rect GetRect(float width, float height, GUIStyle style) => GetRect(width, height, style, null);

        public static Rect GetRect(float width, float height, params GUILayoutOption[] options) => GetRect(width, height, GUIStyle.none, options);

        /// A rect of the given size (the style's fixed sizes and the options may change it).
        public static Rect GetRect(float width, float height, GUIStyle style, params GUILayoutOption[] options)
            => GetRect(width, width, height, height, style, options);

        public static Rect GetRect(float minWidth, float maxWidth, float minHeight, float maxHeight)
            => GetRect(minWidth, maxWidth, minHeight, maxHeight, GUIStyle.none, null);

        public static Rect GetRect(float minWidth, float maxWidth, float minHeight, float maxHeight, GUIStyle style)
            => GetRect(minWidth, maxWidth, minHeight, maxHeight, style, null);

        public static Rect GetRect(float minWidth, float maxWidth, float minHeight, float maxHeight, params GUILayoutOption[] options)
            => GetRect(minWidth, maxWidth, minHeight, maxHeight, GUIStyle.none, options);

        /// A rect within the given size range.
        public static Rect GetRect(float minWidth, float maxWidth, float minHeight, float maxHeight, GUIStyle style, params GUILayoutOption[] options)
        {
            var pass = Pass;
            return pass.Leaf(pass.Recording ? LayoutEntry.Leaf(style, minWidth, Mathf.Max(minWidth, maxWidth), minHeight, Mathf.Max(minHeight, maxHeight), options) : null);
        }

        public static Rect GetRect(GUIContent content, GUIStyle style) => GetRect(content, style, null);

        /// A rect for showing `content` with `style`: its CalcSize rounded up to whole pixels,
        /// or for a word-wrapping style a width within CalcMinMaxWidth (rounded up) and the
        /// height CalcHeight gives at that width.
        public static Rect GetRect(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            var pass = Pass;
            if (!pass.Recording) return pass.Leaf(null);
            content ??= GUIContent.none;
            style ??= GUIStyle.none;
            if (style.isHeightDependantOnWidth) return pass.Leaf(new WrappedEntry(content, style, options));
            var size = style.CalcSize(content);
            float w = Mathf.Ceil(size.x), h = Mathf.Ceil(size.y);
            return pass.Leaf(LayoutEntry.Leaf(style, w, w, h, h, options));
        }

        /// The rect of the entry the last GUILayout call of the current group took; the dummy
        /// rect during Layout and Used events, and (with an error during Repaint) straight
        /// after a Begin call.
        public static Rect GetLastRect() => Pass.LastRect;

        public static Rect GetAspectRect(float aspect) => GetAspectRect(aspect, GUIStyle.none, null);

        public static Rect GetAspectRect(float aspect, GUIStyle style) => GetAspectRect(aspect, style, null);

        public static Rect GetAspectRect(float aspect, params GUILayoutOption[] options) => GetAspectRect(aspect, GUIStyle.none, options);

        /// A rect whose width-to-height ratio is `aspect`: as wide as the layout makes it.
        public static Rect GetAspectRect(float aspect, GUIStyle style, params GUILayoutOption[] options)
        {
            var pass = Pass;
            return pass.Leaf(pass.Recording ? new AspectEntry(aspect, style, options) : null);
        }
    }
}
