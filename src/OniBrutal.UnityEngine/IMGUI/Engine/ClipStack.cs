using System.Collections.Generic;
using UnityEngine;

namespace OniBrutal.Engine.IMGUI
{
    /// The GUI clip stack of the OnGUI call (or window function) being run. Each clip maps its
    /// local coordinates to GUI space (the space GUI.matrix transforms to the screen) and limits
    /// what is visible. Groups, areas, scroll views and windows push clips.
    internal static class ClipStack
    {
        /// One pushed clip. `Origin` is where local (0, 0) lies in GUI space, scroll offset
        /// included; `Visible` is the visible part in GUI space, clipped by every parent.
        readonly struct Frame(Rect pushed, Vector2 origin, Vector2 unscrolledOrigin, Rect visible)
        {
            public readonly Rect Pushed = pushed;
            public readonly Vector2 Origin = origin;
            public readonly Vector2 UnscrolledOrigin = unscrolledOrigin;
            public readonly Rect Visible = visible;
        }

        /// GetTopRect's answer when nothing is pushed: practically unbounded.
        public static readonly Rect Unbounded = new(-10000f, -10000f, 40000f, 40000f);

        static readonly List<Frame> frames = new();

        public static int Count => frames.Count;

        /// Each OnGUI call and window function starts with an empty stack.
        public static void Clear() => frames.Clear();

        static Vector2 TopOrigin => Count > 0 ? frames[^1].Origin : Vector2.zero;

        static Vector2 TopUnscrolledOrigin => Count > 0 ? frames[^1].UnscrolledOrigin : Vector2.zero;

        /// The visible part of the topmost clip in GUI space.
        public static Rect AbsoluteVisible => Count > 0 ? frames[^1].Visible : Unbounded;

        /// The topmost clip's rect as it was pushed.
        public static Rect TopRect => Count > 0 ? frames[^1].Pushed : Unbounded;

        /// Pushes a clip at `rect` (in the current clip's coordinates). Its content is offset by
        /// `scrollOffset` and `renderOffset`; with `resetOffset` the rect is placed ignoring the
        /// enclosing clip's scroll offset.
        public static void Push(Rect rect, Vector2 scrollOffset, Vector2 renderOffset, bool resetOffset)
        {
            var parentOrigin = resetOffset ? TopUnscrolledOrigin : TopOrigin;
            var abs = new Rect(rect.position + parentOrigin, rect.size);
            var parent = AbsoluteVisible;
            var visible = Intersect(parent, abs);
            frames.Add(new Frame(rect, abs.position + scrollOffset + renderOffset, abs.position, visible));
        }

        /// Pushes a clip given directly in GUI space (a window's rect).
        public static void PushAbsolute(Rect abs)
        {
            frames.Add(new Frame(abs, abs.position, abs.position, Intersect(AbsoluteVisible, abs)));
        }

        public static bool Pop()
        {
            if (Count == 0) return false;
            frames.RemoveAt(frames.Count - 1);
            return true;
        }

        static Rect Intersect(Rect a, Rect b)
        {
            float x0 = Mathf.Max(a.xMin, b.xMin), y0 = Mathf.Max(a.yMin, b.yMin);
            float x1 = Mathf.Min(a.xMax, b.xMax), y1 = Mathf.Min(a.yMax, b.yMax);
            return Rect.MinMaxRect(x0, y0, Mathf.Max(x0, x1), Mathf.Max(y0, y1));
        }

        // ----- Coordinate conversion -----

        /// Local coordinates of the topmost clip → GUI space.
        public static Vector2 Unclip(Vector2 local) => local + TopOrigin;

        public static Rect Unclip(Rect local) => new(local.position + TopOrigin, local.size);

        /// GUI space → local coordinates of the topmost clip.
        public static Vector2 Clip(Vector2 abs) => abs - TopOrigin;

        public static Rect Clip(Rect abs) => new(abs.position - TopOrigin, abs.size);

        /// The visible part of the topmost clip in its own coordinates; the screen when nothing
        /// is pushed.
        public static Rect LocalVisible => Count > 0 ? Clip(frames[^1].Visible) : new Rect(0f, 0f, Screen.width, Screen.height);

        /// GUI space → screen pixels through GUI.matrix.
        public static Vector2 ToScreen(Vector2 abs) => GUIStateData.Matrix.MultiplyPoint3x4(abs);

        /// Screen pixels → GUI space.
        public static Vector2 FromScreen(Vector2 screen) => GUIStateData.InverseMatrix.MultiplyPoint3x4(screen);

        public static Vector2 ScreenToLocal(Vector2 screen) => Clip(FromScreen(screen));

        public static Vector2 LocalToScreen(Vector2 local) => ToScreen(Unclip(local));

        public static Vector2 ScreenToLocalVector(Vector2 v) => GUIStateData.InverseMatrix.MultiplyVector(v);

        public static Vector2 LocalToScreenVector(Vector2 v) => GUIStateData.Matrix.MultiplyVector(v);

        // ----- Window-relative coordinates -----

        /// The origin of the window whose function is running (its clip is the first), or of
        /// the screen.
        static Vector2 WindowOrigin => frames.Count > 0 && GUIStateData.InWindow ? frames[0].Origin : Vector2.zero;

        /// Local coordinates of the topmost clip → coordinates of the enclosing window.
        public static Vector2 UnclipToWindow(Vector2 local) => Unclip(local) - WindowOrigin;

        public static Rect UnclipToWindow(Rect local) => new(UnclipToWindow(local.position), local.size);

        /// Coordinates of the enclosing window → local coordinates of the topmost clip.
        public static Vector2 ClipToWindow(Vector2 windowPoint) => Clip(windowPoint + WindowOrigin);

        public static Rect ClipToWindow(Rect windowRect) => new(ClipToWindow(windowRect.position), windowRect.size);
    }
}
