using System.Collections.Generic;
using UnityEngine;

namespace OniMoono.Engine.IMGUI
{
    /// GUI.BeginScrollView / EndScrollView: the part of a content rect at the scroll position,
    /// shown through a clip, with scrollbars where the content does not fit.
    internal static class ScrollViews
    {
        /// Pixels scrolled per scroll-wheel line (the platform reports three lines a notch).
        const float WheelLine = 10f;

        /// Scroll positions set by the wheel, ScrollTo or ScrollTowards; the next
        /// BeginScrollView of the view returns them.
        sealed class Pending
        {
            public bool Set;
            public Vector2 Scroll;
        }

        sealed class Open
        {
            public Rect Position;
            public Rect ViewRect;
            public Vector2 ClipSize;
            public Vector2 Scroll;
            /// GUI-space position of the view rect's origin.
            public Vector2 ContentOrigin;
            public Pending Pending;
        }

        static readonly List<Open> open = new();

        public static void Reset() => open.Clear();

        /// The room a scrollbar takes across: its style's fixed size (else its padding) plus its
        /// leading margin (measured: the vertical bar's 15 + 1 of the default skin).
        public static float BarThickness(GUIStyle style, bool vertical)
        {
            if (style == null) return 0f;
            float fixedSize = vertical ? style.fixedWidth : style.fixedHeight;
            float size = fixedSize != 0f ? fixedSize : vertical ? style.padding.horizontal : style.padding.vertical;
            return size + (vertical ? style.margin.left : style.margin.top);
        }

        public static Vector2 Begin(Rect position, Vector2 scrollPosition, Rect viewRect, bool alwaysShowHorizontal, bool alwaysShowVertical,
            GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar, GUIStyle background)
        {
            GUIStateData.RequireGUI();
            int id = GUIUtility.GetControlID(ControlHints.ScrollView, FocusType.Passive);
            var pending = (Pending)GUIStateData.GetStateObject(typeof(Pending), id);
            if (pending.Set)
            {
                scrollPosition = pending.Scroll;
                pending.Set = false;
            }
            float barH = BarThickness(horizontalScrollbar, false), barV = BarThickness(verticalScrollbar, true);
            bool needH = alwaysShowHorizontal, needV = alwaysShowVertical;
            for (int i = 0; i < 2; i++)
            {
                needH = alwaysShowHorizontal || viewRect.width > position.width - (needV ? barV : 0f);
                needV = alwaysShowVertical || viewRect.height > position.height - (needH ? barH : 0f);
            }
            var clip = new Vector2(Mathf.Max(0f, position.width - (needV ? barV : 0f)), Mathf.Max(0f, position.height - (needH ? barH : 0f)));
            var e = Event.current;
            if (e.type == EventType.Repaint && background != null && background != GUIStyle.none)
                background.Draw(position, false, false, false, false);
            if (needH && horizontalScrollbar != null)
            {
                var bar = new Rect(position.x, position.yMax - barH + horizontalScrollbar.margin.top, clip.x, barH - horizontalScrollbar.margin.top);
                scrollPosition.x = Controls.Scrollbar(bar, scrollPosition.x, Mathf.Min(clip.x, viewRect.width), 0f, Mathf.Max(viewRect.width, clip.x), horizontalScrollbar, true);
            }
            if (needV && verticalScrollbar != null)
            {
                var bar = new Rect(position.xMax - barV + verticalScrollbar.margin.left, position.y, barV - verticalScrollbar.margin.left, clip.y);
                scrollPosition.y = Controls.Scrollbar(bar, scrollPosition.y, Mathf.Min(clip.y, viewRect.height), 0f, Mathf.Max(viewRect.height, clip.y), verticalScrollbar, false);
            }
            scrollPosition = Clamp(scrollPosition, viewRect.size, clip);
            var offset = -scrollPosition - viewRect.position;
            ClipStack.Push(new Rect(position.position, clip), offset, Vector2.zero, false);
            open.Add(new Open
            {
                Position = position,
                ViewRect = viewRect,
                ClipSize = clip,
                Scroll = scrollPosition,
                ContentOrigin = ClipStack.Unclip(viewRect.position),
                Pending = pending,
            });
            return scrollPosition;
        }

        /// GUILayout.BeginScrollView during Layout: nothing is laid out yet, so the scroll
        /// position is returned as given (or as the wheel or ScrollTo set it).
        public static Vector2 BeginMeasuring(Vector2 scrollPosition)
        {
            GUIStateData.RequireGUI();
            int id = GUIUtility.GetControlID(ControlHints.ScrollView, FocusType.Passive);
            var pending = (Pending)GUIStateData.GetStateObject(typeof(Pending), id);
            if (pending.Set)
            {
                scrollPosition = pending.Scroll;
                pending.Set = false;
            }
            var dummy = LayoutPass.Dummy;
            ClipStack.Push(dummy, Vector2.zero, Vector2.zero, false);
            open.Add(new Open { Position = dummy, ViewRect = dummy, ClipSize = dummy.size, Scroll = scrollPosition, ContentOrigin = ClipStack.Unclip(Vector2.zero), Pending = pending });
            return scrollPosition;
        }

        static Vector2 Clamp(Vector2 scroll, Vector2 content, Vector2 clip)
            => new(Mathf.Clamp(scroll.x, 0f, Mathf.Max(0f, content.x - clip.x)), Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, content.y - clip.y)));

        /// Ends the innermost scroll view (with none open, nothing happens). A scroll wheel event
        /// over it that its content did not use scrolls it.
        public static void End(bool handleScrollWheel)
        {
            GUIStateData.RequireGUI();
            if (open.Count == 0) return;
            var s = open[^1];
            open.RemoveAt(open.Count - 1);
            ClipStack.PopOrReport();
            var e = Event.current;
            if (!handleScrollWheel || e.type != EventType.ScrollWheel || !s.Position.Contains(e.mousePosition)) return;
            var target = Clamp(s.Scroll + e.delta * WheelLine, s.ViewRect.size, s.ClipSize);
            if (target == s.Scroll) return;
            s.Pending.Scroll = target;
            s.Pending.Set = true;
            e.Use();
        }

        /// The scroll position of the innermost view that shows `position` (current
        /// coordinates), moved by at most `maxDelta` (negative: all the way).
        static bool Reveal(Rect position, float maxDelta)
        {
            if (open.Count == 0) return false;
            var s = open[^1];
            var abs = ClipStack.Unclip(position);
            var r = new Rect(abs.position - s.ContentOrigin, abs.size);
            var target = s.Scroll;
            for (int axis = 0; axis < 2; axis++)
            {
                float min = r.min[axis], max = r.max[axis], view = s.ClipSize[axis];
                float visibleMin = target[axis];
                if (max - min > view || min < visibleMin) target[axis] = min;
                else if (max > visibleMin + view) target[axis] = max - view;
            }
            target = Clamp(target, s.ViewRect.size, s.ClipSize);
            if (maxDelta >= 0f) target = Vector2.MoveTowards(s.Scroll, target, maxDelta);
            if (target == s.Scroll) return false;
            s.Scroll = target;
            s.Pending.Scroll = target;
            s.Pending.Set = true;
            return true;
        }

        public static void ScrollTo(Rect position) => Reveal(position, -1f);

        public static bool ScrollTowards(Rect position, float maxDelta) => Reveal(position, maxDelta);
    }
}
