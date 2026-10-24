using OniBrutal.Engine.IMGUI;

namespace UnityEngine
{
    /// The automatically laid-out versions of the GUI controls: each reserves its rect in the
    /// current group (GUILayoutUtility.GetRect) and runs the GUI control there.
    public partial class GUILayout
    {
        static GUISkin Skin => GUI.skin;

        // ----- Labels and boxes -----

        public static void Label(string text, params GUILayoutOption[] options) => Label(GUIContent.Scratch(text), Skin.label, options);
        public static void Label(Texture image, params GUILayoutOption[] options) => Label(GUIContent.Scratch(image), Skin.label, options);
        public static void Label(GUIContent content, params GUILayoutOption[] options) => Label(content, Skin.label, options);
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) => Label(GUIContent.Scratch(text), style, options);
        public static void Label(Texture image, GUIStyle style, params GUILayoutOption[] options) => Label(GUIContent.Scratch(image), style, options);

        public static void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
            => GUI.Label(GUILayoutUtility.GetRect(content, style, options), content, style);

        public static void Box(string text, params GUILayoutOption[] options) => Box(GUIContent.Scratch(text), Skin.box, options);
        public static void Box(Texture image, params GUILayoutOption[] options) => Box(GUIContent.Scratch(image), Skin.box, options);
        public static void Box(GUIContent content, params GUILayoutOption[] options) => Box(content, Skin.box, options);
        public static void Box(string text, GUIStyle style, params GUILayoutOption[] options) => Box(GUIContent.Scratch(text), style, options);
        public static void Box(Texture image, GUIStyle style, params GUILayoutOption[] options) => Box(GUIContent.Scratch(image), style, options);

        public static void Box(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
            => GUI.Box(GUILayoutUtility.GetRect(content, style, options), content, style);

        // ----- Buttons and toggles -----

        public static bool Button(string text, params GUILayoutOption[] options) => Button(GUIContent.Scratch(text), Skin.button, options);
        public static bool Button(Texture image, params GUILayoutOption[] options) => Button(GUIContent.Scratch(image), Skin.button, options);
        public static bool Button(GUIContent content, params GUILayoutOption[] options) => Button(content, Skin.button, options);
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) => Button(GUIContent.Scratch(text), style, options);
        public static bool Button(Texture image, GUIStyle style, params GUILayoutOption[] options) => Button(GUIContent.Scratch(image), style, options);

        public static bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
            => GUI.Button(GUILayoutUtility.GetRect(content, style, options), content, style);

        public static bool RepeatButton(string text, params GUILayoutOption[] options) => RepeatButton(GUIContent.Scratch(text), Skin.button, options);
        public static bool RepeatButton(Texture image, params GUILayoutOption[] options) => RepeatButton(GUIContent.Scratch(image), Skin.button, options);
        public static bool RepeatButton(GUIContent content, params GUILayoutOption[] options) => RepeatButton(content, Skin.button, options);
        public static bool RepeatButton(string text, GUIStyle style, params GUILayoutOption[] options) => RepeatButton(GUIContent.Scratch(text), style, options);
        public static bool RepeatButton(Texture image, GUIStyle style, params GUILayoutOption[] options) => RepeatButton(GUIContent.Scratch(image), style, options);

        public static bool RepeatButton(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
            => GUI.RepeatButton(GUILayoutUtility.GetRect(content, style, options), content, style);

        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => Toggle(value, GUIContent.Scratch(text), Skin.toggle, options);
        public static bool Toggle(bool value, Texture image, params GUILayoutOption[] options) => Toggle(value, GUIContent.Scratch(image), Skin.toggle, options);
        public static bool Toggle(bool value, GUIContent content, params GUILayoutOption[] options) => Toggle(value, content, Skin.toggle, options);
        public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options) => Toggle(value, GUIContent.Scratch(text), style, options);
        public static bool Toggle(bool value, Texture image, GUIStyle style, params GUILayoutOption[] options) => Toggle(value, GUIContent.Scratch(image), style, options);

        public static bool Toggle(bool value, GUIContent content, GUIStyle style, params GUILayoutOption[] options)
            => GUI.Toggle(GUILayoutUtility.GetRect(content, style, options), value, content, style);

        // ----- Toolbars -----

        public static int Toolbar(int selected, string[] texts, params GUILayoutOption[] options) => Toolbar(selected, Controls.Contents(texts), Skin.button, options);
        public static int Toolbar(int selected, Texture[] images, params GUILayoutOption[] options) => Toolbar(selected, Controls.Contents(images), Skin.button, options);
        public static int Toolbar(int selected, GUIContent[] contents, params GUILayoutOption[] options) => Toolbar(selected, contents, Skin.button, options);
        public static int Toolbar(int selected, string[] texts, GUIStyle style, params GUILayoutOption[] options) => Toolbar(selected, Controls.Contents(texts), style, options);
        public static int Toolbar(int selected, Texture[] images, GUIStyle style, params GUILayoutOption[] options) => Toolbar(selected, Controls.Contents(images), style, options);

        /// A toolbar that takes the available width (unless the options say otherwise) and the
        /// height of one row of its buttons.
        public static int Toolbar(int selected, GUIContent[] contents, GUIStyle style, params GUILayoutOption[] options)
        {
            style ??= GUIStyle.none;
            var pass = GUILayoutUtility.Pass;
            Rect rect;
            if (pass.Recording)
            {
                float width = 0f, height = 0f;
                if (contents != null)
                {
                    foreach (var c in contents)
                    {
                        var size = style.CalcSize(c ?? GUIContent.none);
                        width += size.x;
                        height = Mathf.Max(height, size.y);
                    }
                }
                // It stretches by default; options given after this one still override it.
                var all = new GUILayoutOption[(options?.Length ?? 0) + 1];
                all[0] = ExpandWidth(true);
                options?.CopyTo(all, 1);
                var item = LayoutEntry.Leaf(style, width, width, height, height, all);
                // Measured: the row sits flush with its area (no leading or side margins), and
                // the style's bottom margin separates it from what follows.
                item.MarginLeft = item.MarginRight = item.MarginTop = 0;
                rect = pass.Leaf(item);
            }
            else
            {
                rect = pass.Leaf(null);
            }
            return GUI.Toolbar(rect, selected, contents, style);
        }

        // ----- Text -----

        public static string TextField(string text, params GUILayoutOption[] options) => TextField(text, -1, Skin.textField, options);
        public static string TextField(string text, int maxLength, params GUILayoutOption[] options) => TextField(text, maxLength, Skin.textField, options);
        public static string TextField(string text, GUIStyle style, params GUILayoutOption[] options) => TextField(text, -1, style, options);

        public static string TextField(string text, int maxLength, GUIStyle style, params GUILayoutOption[] options)
            => TextFields.Edit(GUILayoutUtility.GetRect(GUIContent.Scratch(text ?? ""), style, options), text, maxLength, style, false);

        public static string TextArea(string text, params GUILayoutOption[] options) => TextArea(text, -1, Skin.textArea, options);
        public static string TextArea(string text, int maxLength, params GUILayoutOption[] options) => TextArea(text, maxLength, Skin.textArea, options);
        public static string TextArea(string text, GUIStyle style, params GUILayoutOption[] options) => TextArea(text, -1, style, options);

        public static string TextArea(string text, int maxLength, GUIStyle style, params GUILayoutOption[] options)
            => TextFields.Edit(GUILayoutUtility.GetRect(GUIContent.Scratch(text ?? ""), style, options), text, maxLength, style, true);

        // ----- Sliders and scrollbars -----

        public static float HorizontalSlider(float value, float leftValue, float rightValue, params GUILayoutOption[] options)
            => HorizontalSlider(value, leftValue, rightValue, Skin.horizontalSlider, Skin.horizontalSliderThumb, options);

        public static float HorizontalSlider(float value, float leftValue, float rightValue, GUIStyle slider, GUIStyle thumb, params GUILayoutOption[] options)
            => GUI.HorizontalSlider(GUILayoutUtility.GetRect(GUIContent.none, slider, options), value, leftValue, rightValue, slider, thumb);

        public static float VerticalSlider(float value, float topValue, float bottomValue, params GUILayoutOption[] options)
            => VerticalSlider(value, topValue, bottomValue, Skin.verticalSlider, Skin.verticalSliderThumb, options);

        public static float VerticalSlider(float value, float topValue, float bottomValue, GUIStyle slider, GUIStyle thumb, params GUILayoutOption[] options)
            => GUI.VerticalSlider(GUILayoutUtility.GetRect(GUIContent.none, slider, options), value, topValue, bottomValue, slider, thumb);

        public static float HorizontalScrollbar(float value, float size, float leftValue, float rightValue, params GUILayoutOption[] options)
            => HorizontalScrollbar(value, size, leftValue, rightValue, Skin.horizontalScrollbar, options);

        public static float HorizontalScrollbar(float value, float size, float leftValue, float rightValue, GUIStyle style, params GUILayoutOption[] options)
            => GUI.HorizontalScrollbar(GUILayoutUtility.GetRect(GUIContent.none, style, options), value, size, leftValue, rightValue, style);

        public static float VerticalScrollbar(float value, float size, float topValue, float bottomValue, params GUILayoutOption[] options)
            => VerticalScrollbar(value, size, topValue, bottomValue, Skin.verticalScrollbar, options);

        public static float VerticalScrollbar(float value, float size, float topValue, float bottomValue, GUIStyle style, params GUILayoutOption[] options)
            => GUI.VerticalScrollbar(GUILayoutUtility.GetRect(GUIContent.none, style, options), value, size, topValue, bottomValue, style);

        // ----- Space -----

        /// A fixed gap along the current group's axis. It takes no margin: it starts where the
        /// entry before it ends, and the entry after it keeps its leading margin.
        public static void Space(float pixels)
        {
            var pass = GUILayoutUtility.Pass;
            pass.Leaf(pass.Recording ? LayoutEntry.Spacer(pass.InVertical, pixels, false) : null);
        }

        /// An empty entry that takes the space left along the current group's axis.
        public static void FlexibleSpace()
        {
            var pass = GUILayoutUtility.Pass;
            pass.Leaf(pass.Recording ? LayoutEntry.Spacer(pass.InVertical, 0f, true) : null);
        }

        // ----- Groups -----

        public static void BeginHorizontal(params GUILayoutOption[] options) => BeginHorizontal(GUIContent.none, GUIStyle.none, options);
        public static void BeginHorizontal(GUIStyle style, params GUILayoutOption[] options) => BeginHorizontal(GUIContent.none, style, options);
        public static void BeginHorizontal(string text, GUIStyle style, params GUILayoutOption[] options) => BeginHorizontal(GUIContent.Scratch(text), style, options);
        public static void BeginHorizontal(Texture image, GUIStyle style, params GUILayoutOption[] options) => BeginHorizontal(GUIContent.Scratch(image), style, options);

        /// Starts a group laying its entries out left to right; a style draws behind it.
        public static void BeginHorizontal(GUIContent content, GUIStyle style, params GUILayoutOption[] options) => BeginGroup(false, content, style, options);

        public static void EndHorizontal() => GUILayoutUtility.Pass.EndGroup();

        public static void BeginVertical(params GUILayoutOption[] options) => BeginVertical(GUIContent.none, GUIStyle.none, options);
        public static void BeginVertical(GUIStyle style, params GUILayoutOption[] options) => BeginVertical(GUIContent.none, style, options);
        public static void BeginVertical(string text, GUIStyle style, params GUILayoutOption[] options) => BeginVertical(GUIContent.Scratch(text), style, options);
        public static void BeginVertical(Texture image, GUIStyle style, params GUILayoutOption[] options) => BeginVertical(GUIContent.Scratch(image), style, options);

        /// Starts a group laying its entries out top to bottom; a style draws behind it.
        public static void BeginVertical(GUIContent content, GUIStyle style, params GUILayoutOption[] options) => BeginGroup(true, content, style, options);

        public static void EndVertical() => GUILayoutUtility.Pass.EndGroup();

        static void BeginGroup(bool vertical, GUIContent content, GUIStyle style, GUILayoutOption[] options)
        {
            var box = GUILayoutUtility.Pass.BeginGroup(vertical, style ?? GUIStyle.none, options);
            if (box != null && Event.current.type == EventType.Repaint && style != null && style != GUIStyle.none)
                style.Draw(box.Rect, content ?? GUIContent.none, false, false, false, false);
        }

        // ----- Areas -----

        public static void BeginArea(Rect screenRect) => BeginArea(screenRect, GUIContent.none, GUIStyle.none);
        public static void BeginArea(Rect screenRect, string text) => BeginArea(screenRect, GUIContent.Scratch(text), GUIStyle.none);
        public static void BeginArea(Rect screenRect, Texture image) => BeginArea(screenRect, GUIContent.Scratch(image), GUIStyle.none);
        public static void BeginArea(Rect screenRect, GUIContent content) => BeginArea(screenRect, content, GUIStyle.none);
        public static void BeginArea(Rect screenRect, GUIStyle style) => BeginArea(screenRect, GUIContent.none, style);
        public static void BeginArea(Rect screenRect, string text, GUIStyle style) => BeginArea(screenRect, GUIContent.Scratch(text), style);
        public static void BeginArea(Rect screenRect, Texture image, GUIStyle style) => BeginArea(screenRect, GUIContent.Scratch(image), style);

        /// Lays out a vertical group inside a fixed rect, clipped to it like a group.
        public static void BeginArea(Rect screenRect, GUIContent content, GUIStyle style)
        {
            GUILayoutUtility.Pass.BeginArea(screenRect, style ?? GUIStyle.none);
            GUI.BeginGroup(screenRect, content, style);
        }

        /// Closes the area's layout group first: with no group left under it this throws
        /// (InvalidOperationException) before the clip is closed.
        public static void EndArea()
        {
            GUILayoutUtility.Pass.EndArea();
            GUI.EndGroup();
        }

        // ----- Scroll views -----

        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options)
            => BeginScrollView(scrollPosition, false, false, Skin.horizontalScrollbar, Skin.verticalScrollbar, Skin.scrollView, options);

        public static Vector2 BeginScrollView(Vector2 scrollPosition, bool alwaysShowHorizontal, bool alwaysShowVertical, params GUILayoutOption[] options)
            => BeginScrollView(scrollPosition, alwaysShowHorizontal, alwaysShowVertical, Skin.horizontalScrollbar, Skin.verticalScrollbar, Skin.scrollView, options);

        public static Vector2 BeginScrollView(Vector2 scrollPosition, GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar, params GUILayoutOption[] options)
            => BeginScrollView(scrollPosition, false, false, horizontalScrollbar, verticalScrollbar, Skin.scrollView, options);

        public static Vector2 BeginScrollView(Vector2 scrollPosition, GUIStyle style)
            => BeginScrollView(scrollPosition, false, false, Skin.horizontalScrollbar, Skin.verticalScrollbar, style, null);

        public static Vector2 BeginScrollView(Vector2 scrollPosition, GUIStyle style, params GUILayoutOption[] options)
            => BeginScrollView(scrollPosition, false, false, Skin.horizontalScrollbar, Skin.verticalScrollbar, style, options);

        public static Vector2 BeginScrollView(Vector2 scrollPosition, bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar, params GUILayoutOption[] options)
            => BeginScrollView(scrollPosition, alwaysShowHorizontal, alwaysShowVertical, horizontalScrollbar, verticalScrollbar, Skin.scrollView, options);

        /// A group whose content is laid out at its own size and shown through a scroll view.
        public static Vector2 BeginScrollView(Vector2 scrollPosition, bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar, GUIStyle background, params GUILayoutOption[] options)
        {
            var pass = GUILayoutUtility.Pass;
            if (pass.Recording)
            {
                pass.BeginScroll(alwaysShowHorizontal, alwaysShowVertical, horizontalScrollbar, verticalScrollbar, background ?? GUIStyle.none, options);
                return ScrollViews.BeginMeasuring(scrollPosition);
            }
            // The recorded group in its place (ExitGUIException when a leaf is there); none in a
            // Used event.
            var g = pass.TakeScroll();
            if (g == null) return ScrollViews.BeginMeasuring(scrollPosition);
            var s = g as ScrollGroup;
            var view = s != null ? s.ViewRect : new Rect(0f, 0f, g.Rect.width, g.Rect.height);
            return ScrollViews.Begin(g.Rect, scrollPosition, view, s?.NeedsHorizontal ?? false, s?.NeedsVertical ?? false, horizontalScrollbar, verticalScrollbar, background);
        }

        public static void EndScrollView() => EndScrollView(true);

        internal static void EndScrollView(bool handleScrollWheel)
        {
            GUILayoutUtility.Pass.EndGroup();
            ScrollViews.End(handleScrollWheel);
        }

        // ----- Windows -----

        public static Rect Window(int id, Rect screenRect, GUI.WindowFunction func, string text, params GUILayoutOption[] options)
            => Window(id, screenRect, func, GUIContent.Scratch(text), Skin.window, options);

        public static Rect Window(int id, Rect screenRect, GUI.WindowFunction func, Texture image, params GUILayoutOption[] options)
            => Window(id, screenRect, func, GUIContent.Scratch(image), Skin.window, options);

        public static Rect Window(int id, Rect screenRect, GUI.WindowFunction func, GUIContent content, params GUILayoutOption[] options)
            => Window(id, screenRect, func, content, Skin.window, options);

        public static Rect Window(int id, Rect screenRect, GUI.WindowFunction func, string text, GUIStyle style, params GUILayoutOption[] options)
            => Window(id, screenRect, func, GUIContent.Scratch(text), style, options);

        public static Rect Window(int id, Rect screenRect, GUI.WindowFunction func, Texture image, GUIStyle style, params GUILayoutOption[] options)
            => Window(id, screenRect, func, GUIContent.Scratch(image), style, options);

        /// A window sized to fit its laid-out content, within the rect's size and the options.
        public static Rect Window(int id, Rect screenRect, GUI.WindowFunction func, GUIContent content, GUIStyle style, params GUILayoutOption[] options)
            => Windows.Declare(id, screenRect, func, content, style, true, options);

        // ----- Options -----

        public static GUILayoutOption Width(float width) => new(LayoutRule.Fixed, true, width);
        public static GUILayoutOption MinWidth(float minWidth) => new(LayoutRule.Min, true, minWidth);
        public static GUILayoutOption MaxWidth(float maxWidth) => new(LayoutRule.Max, true, maxWidth);
        public static GUILayoutOption Height(float height) => new(LayoutRule.Fixed, false, height);
        public static GUILayoutOption MinHeight(float minHeight) => new(LayoutRule.Min, false, minHeight);
        public static GUILayoutOption MaxHeight(float maxHeight) => new(LayoutRule.Max, false, maxHeight);
        public static GUILayoutOption ExpandWidth(bool expand) => new(LayoutRule.Expand, true, expand ? 1f : 0f);
        public static GUILayoutOption ExpandHeight(bool expand) => new(LayoutRule.Expand, false, expand ? 1f : 0f);
    }
}
