using OniBrutal.Engine.IMGUI;

namespace UnityEngine
{
    public partial class GUI
    {
        // ----- Groups and clips -----

        public static void BeginGroup(Rect position) => BeginGroup(position, GUIContent.none, GUIStyle.none);
        public static void BeginGroup(Rect position, string text) => BeginGroup(position, GUIContent.Scratch(text), GUIStyle.none);
        public static void BeginGroup(Rect position, Texture image) => BeginGroup(position, GUIContent.Scratch(image), GUIStyle.none);
        public static void BeginGroup(Rect position, GUIContent content) => BeginGroup(position, content, GUIStyle.none);
        public static void BeginGroup(Rect position, GUIStyle style) => BeginGroup(position, GUIContent.none, style);
        public static void BeginGroup(Rect position, string text, GUIStyle style) => BeginGroup(position, GUIContent.Scratch(text), style);
        public static void BeginGroup(Rect position, Texture image, GUIStyle style) => BeginGroup(position, GUIContent.Scratch(image), style);

        /// Starts a group: the style is drawn at `position` with the content, then, until
        /// EndGroup, coordinates (the mouse's included) are relative to the group's top left
        /// and drawing is clipped to it.
        public static void BeginGroup(Rect position, GUIContent content, GUIStyle style)
        {
            GUIStateData.RequireGUI();
            style ??= GUIStyle.none;
            if (Event.current.type == EventType.Repaint && (style != GUIStyle.none || content != GUIContent.none))
                style.Draw(position, content ?? GUIContent.none, false, false, false, false);
            ClipStack.Push(position, Vector2.zero, Vector2.zero, false);
        }

        public static void EndGroup() => EndClip();

        public static void BeginClip(Rect position)
        {
            GUIStateData.RequireGUI();
            ClipStack.Push(position, Vector2.zero, Vector2.zero, false);
        }

        /// A group whose content is offset by `scrollOffset` and `renderOffset`; with
        /// `resetOffset` the clip ignores the enclosing clip's scroll offset.
        public static void BeginClip(Rect position, Vector2 scrollOffset, Vector2 renderOffset, bool resetOffset)
        {
            GUIStateData.RequireGUI();
            ClipStack.Push(position, scrollOffset, renderOffset, resetOffset);
        }

        public static void EndClip()
        {
            GUIStateData.RequireGUI();
            if (!ClipStack.Pop()) Debug.LogError("GUI Error: EndGroup or EndClip was called without a matching BeginGroup or BeginClip.");
        }

        // ----- Scroll views -----

        public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect)
            => BeginScrollView(position, scrollPosition, viewRect, false, false, skin.horizontalScrollbar, skin.verticalScrollbar, skin.scrollView);

        public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect, bool alwaysShowHorizontal, bool alwaysShowVertical)
            => BeginScrollView(position, scrollPosition, viewRect, alwaysShowHorizontal, alwaysShowVertical, skin.horizontalScrollbar, skin.verticalScrollbar, skin.scrollView);

        public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect, GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar)
            => BeginScrollView(position, scrollPosition, viewRect, false, false, horizontalScrollbar, verticalScrollbar, skin.scrollView);

        public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect, bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar)
            => BeginScrollView(position, scrollPosition, viewRect, alwaysShowHorizontal, alwaysShowVertical, horizontalScrollbar, verticalScrollbar, skin.scrollView);

        /// Shows the part of `viewRect` at `scrollPosition` inside `position`, with scrollbars
        /// when the content is larger than the view (or always, when asked). Returns the scroll
        /// position, clamped to the content.
        static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect, bool alwaysShowHorizontal, bool alwaysShowVertical,
            GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar, GUIStyle background)
            => ScrollViews.Begin(position, scrollPosition, viewRect, alwaysShowHorizontal, alwaysShowVertical, horizontalScrollbar, verticalScrollbar, background);

        public static void EndScrollView() => ScrollViews.End(true);

        /// Ends the scroll view; with `handleScrollWheel` the scroll wheel over it scrolls it.
        public static void EndScrollView(bool handleScrollWheel) => ScrollViews.End(handleScrollWheel);

        /// Scrolls the innermost scroll view to show `position`.
        public static void ScrollTo(Rect position) => ScrollViews.ScrollTo(position);

        /// Scrolls the innermost scroll view towards showing `position` by at most `maxDelta`
        /// pixels; returns whether it moved.
        public static bool ScrollTowards(Rect position, float maxDelta) => ScrollViews.ScrollTowards(position, maxDelta);

        // ----- Windows -----

        public static Rect Window(int id, Rect clientRect, WindowFunction func, string text) => Window(id, clientRect, func, GUIContent.Scratch(text), skin.window);
        public static Rect Window(int id, Rect clientRect, WindowFunction func, Texture image) => Window(id, clientRect, func, GUIContent.Scratch(image), skin.window);
        public static Rect Window(int id, Rect clientRect, WindowFunction func, GUIContent content) => Window(id, clientRect, func, content, skin.window);
        public static Rect Window(int id, Rect clientRect, WindowFunction func, string text, GUIStyle style) => Window(id, clientRect, func, GUIContent.Scratch(text), style);
        public static Rect Window(int id, Rect clientRect, WindowFunction func, Texture image, GUIStyle style) => Window(id, clientRect, func, GUIContent.Scratch(image), style);

        /// A window: drawn after the calling behaviour's OnGUI with `style` and the title, its
        /// function run for each event inside a clip at its rect. Returns its rect, moved by
        /// DragWindow since the last call.
        public static Rect Window(int id, Rect clientRect, WindowFunction func, GUIContent title, GUIStyle style)
            => Windows.Declare(id, clientRect, func, title, style, false, null);

        /// Inside a window function: a press in the whole window and a drag move it.
        public static void DragWindow() => DragWindow(new Rect(0f, 0f, 10000f, 10000f));

        /// Inside a window function: a press in `position` (window coordinates) and a drag move
        /// the window.
        public static void DragWindow(Rect position) => Windows.Drag(position);

        public static void BringWindowToFront(int windowID)
        {
            var p = GUIStateData.Participant;
            var w = p?.FindWindow(windowID);
            if (w != null) p.BringToFront(w);
        }

        public static void BringWindowToBack(int windowID)
        {
            var p = GUIStateData.Participant;
            var w = p?.FindWindow(windowID);
            if (w != null) p.SendToBack(w);
        }

        /// Makes a window the focused one (key events go to it) and brings it to the front.
        public static void FocusWindow(int windowID)
        {
            var p = GUIStateData.Participant;
            var w = p?.FindWindow(windowID);
            if (w == null) return;
            p.FocusedWindow = w;
            p.BringToFront(w);
        }

        /// No window is focused afterwards.
        public static void UnfocusWindow()
        {
            if (GUIStateData.Participant != null) GUIStateData.Participant.FocusedWindow = null;
        }
    }
}
