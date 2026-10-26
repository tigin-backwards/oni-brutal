using OniMoono.Engine.IMGUI;

namespace UnityEngine
{
    /// The GUI clip stack: conversion between GUI space and the coordinates of the topmost
    /// clip (a group, area, scroll view or window). Internal, as in Unity; the NodeEditor's
    /// GUIScaleUtility reflects on Clip(Rect) and GetTopRect(), and finding no public
    /// topmostRect, uses its compatibility mode as it does in the original player.
    internal static class GUIClip
    {
        /// Whether a clip is pushed.
        public static bool enabled => ClipStack.Count > 0;

        /// The visible part of the topmost clip in its own coordinates.
        public static Rect visibleRect => ClipStack.LocalVisible;

        /// The visible part of the topmost clip in GUI space.
        internal static Rect topmostRect => ClipStack.AbsoluteVisible;

        /// The topmost clip's rect as it was pushed; (-10000, -10000, 40000, 40000) when
        /// nothing is pushed.
        internal static Rect GetTopRect() => ClipStack.TopRect;

        /// GUI space → the topmost clip's coordinates.
        public static Vector2 Clip(Vector2 absolutePos) => ClipStack.Clip(absolutePos);

        public static Rect Clip(Rect absoluteRect) => ClipStack.Clip(absoluteRect);

        /// The topmost clip's coordinates → GUI space.
        public static Vector2 Unclip(Vector2 pos) => ClipStack.Unclip(pos);

        public static Rect Unclip(Rect rect) => ClipStack.Unclip(rect);

        /// The current window's coordinates → the topmost clip's coordinates.
        public static Vector2 ClipToWindow(Vector2 absolutePos) => ClipStack.ClipToWindow(absolutePos);

        public static Rect ClipToWindow(Rect absoluteRect) => ClipStack.ClipToWindow(absoluteRect);

        /// The topmost clip's coordinates → the current window's coordinates.
        public static Vector2 UnclipToWindow(Vector2 pos) => ClipStack.UnclipToWindow(pos);

        public static Rect UnclipToWindow(Rect rect) => ClipStack.UnclipToWindow(rect);
    }
}
