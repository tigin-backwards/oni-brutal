using System;
using System.Collections.Generic;
using UnityEngine;

namespace OniBrutal.Engine.IMGUI
{
    /// The GUILayout entries of one container (a behaviour's OnGUI or a window's function).
    /// During a Layout event the GUILayout calls record entries; once the call has returned
    /// they are laid out; during the following events the same calls, in the same order, take
    /// the computed rects back.
    internal sealed class LayoutPass
    {
        LayoutBox root;
        readonly List<LayoutBox> areas = new();
        // The open groups, innermost last, with the entry each stands for in its parent (a
        // scroll view's content stands for the scroll view).
        readonly List<(LayoutBox Box, LayoutItem Entry)> open = new();
        bool recording;
        bool measured;
        Rect lastRect;

        /// The rect GUILayout calls return during Layout, when nothing is computed yet.
        public static readonly Rect Dummy = new(0f, 0f, 1f, 1f);

        public bool Recording => recording;

        public LayoutBox Root => root;

        LayoutBox Current => open[^1].Box;

        /// Starts recording: the root is an empty vertical group with `style` at `rect`.
        public void BeginRecord(Rect rect, GUIStyle style, GUILayoutOption[] options = null)
        {
            root = new LayoutBox(true, style, options) { Rect = rect };
            areas.Clear();
            open.Clear();
            open.Add((root, root));
            recording = true;
            lastRect = Dummy;
        }

        /// Starts replaying the last recording.
        public void BeginReplay()
        {
            open.Clear();
            recording = false;
            lastRect = Dummy;
            if (root == null) return;
            root.Cursor = 0;
            open.Add((root, root));
        }

        /// Lays out what was recorded: the root at its rect, each area at its own.
        public void Compute()
        {
            if (root == null) return;
            root.LayOut(root.Rect);
            LayOutAreas();
        }

        /// GUILayout.Window: the root takes the requested size within the range its content
        /// and options allow (any size not below its minimum, when it stretches); returns the
        /// size it got.
        public Vector2 ComputeFitted(Vector2 requested)
        {
            if (root == null) return requested;
            root.MeasureWidth();
            float width = Fit(requested.x, root.MinWidth, root.MaxWidth, root.StretchWidth);
            root.PlaceX(0f, width);
            root.MeasureHeight();
            float height = Fit(requested.y, root.MinHeight, root.MaxHeight, root.StretchHeight);
            root.PlaceY(0f, height);
            LayOutAreas();
            measured = true;
            return new Vector2(width, height);
        }

        /// The size ComputeFitted gave for `requested` at the last layout, from the ranges it
        /// measured then; `requested` itself before any layout.
        public Vector2 Fit(Vector2 requested)
        {
            if (root == null || !measured) return requested;
            return new Vector2(Fit(requested.x, root.MinWidth, root.MaxWidth, root.StretchWidth), Fit(requested.y, root.MinHeight, root.MaxHeight, root.StretchHeight));
        }

        static float Fit(float requested, float min, float max, bool stretch)
            => stretch ? Mathf.Max(requested, min) : Mathf.Clamp(requested, min, Mathf.Max(min, max));

        void LayOutAreas()
        {
            foreach (var area in areas) area.LayOut(new Rect(0f, 0f, area.Rect.width, area.Rect.height));
        }

        public Rect LastRect => recording ? Dummy : lastRect;

        // ----- Entries -----

        public Rect Leaf(LayoutItem item)
        {
            if (recording)
            {
                Current.Children.Add(item);
                return Dummy;
            }
            var next = Next();
            if (next is LayoutBox or ScrollBox) throw Mismatch();
            lastRect = next.Rect;
            return next.Rect;
        }

        /// The orientation of the group that entries currently go to.
        public bool InVertical => open.Count == 0 || Current.Vertical;

        public LayoutBox BeginGroup(bool vertical, GUIStyle style, GUILayoutOption[] options)
        {
            if (recording)
            {
                var box = new LayoutBox(vertical, style, options);
                Current.Children.Add(box);
                open.Add((box, box));
                return box;
            }
            if (Next() is not LayoutBox { IsArea: false } group || group.Vertical != vertical) throw Mismatch();
            group.Cursor = 0;
            open.Add((group, group));
            return group;
        }

        public LayoutBox EndGroup()
        {
            if (open.Count <= 1) throw new ArgumentException("GUILayout: an End call has no matching Begin call.");
            var (box, entry) = open[^1];
            open.RemoveAt(open.Count - 1);
            lastRect = entry.Rect;
            return box;
        }

        public LayoutBox BeginArea(Rect rect, GUIStyle style)
        {
            if (recording)
            {
                var box = new LayoutBox(true, style, null, isArea: true) { Rect = rect };
                Current.Children.Add(box);
                areas.Add(box);
                open.Add((box, box));
                return box;
            }
            if (Next() is not LayoutBox { IsArea: true } area) throw Mismatch();
            area.Cursor = 0;
            open.Add((area, area));
            return area;
        }

        public ScrollBox BeginScroll(bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar, GUIStyle background, GUILayoutOption[] options)
        {
            if (recording)
            {
                var box = new ScrollBox(alwaysShowHorizontal, alwaysShowVertical, horizontalScrollbar, verticalScrollbar, background, options);
                Current.Children.Add(box);
                open.Add((box.Content, box));
                return box;
            }
            if (Next() is not ScrollBox scroll) throw Mismatch();
            scroll.Content.Cursor = 0;
            open.Add((scroll.Content, scroll));
            return scroll;
        }

        LayoutItem Next()
        {
            if (open.Count == 0) throw Mismatch();
            var group = Current;
            if (group.Cursor >= group.Children.Count) throw Mismatch();
            return group.Children[group.Cursor++];
        }

        Exception Mismatch()
        {
            var group = open.Count > 0 ? Current : null;
            int index = group?.Cursor ?? 0, count = group?.Children.Count ?? 0;
            var type = Event.current != null ? EventTypeNames.Of(Event.current.rawType) : "no event";
            return new ArgumentException($"GUILayout mismatch: entry {index} was asked for in a group of {count} entries during {type}. The GUILayout calls of an event must be those of the Layout event before it.");
        }
    }
}
