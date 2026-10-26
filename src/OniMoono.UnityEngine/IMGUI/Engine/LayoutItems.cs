using System.Collections.Generic;
using UnityEngine;

namespace OniMoono.Engine.IMGUI
{
    /// What a GUILayout option sets on its axis.
    internal enum LayoutRule
    {
        Fixed,
        Min,
        Max,
        Expand,
    }

    /// One GUILayout entry: a size range per axis, stretch flags and margins. Layout runs in
    /// two passes, widths first (measured bottom up, then placed top down), then heights, so
    /// that entries whose height depends on their width (word wrap, aspect) know it.
    internal class LayoutEntry
    {
        public float MinWidth, MaxWidth, MinHeight, MaxHeight;
        public bool StretchWidth, StretchHeight;
        public int MarginLeft, MarginRight, MarginTop, MarginBottom;
        public Rect Rect;
        public readonly GUIStyle Style;
        public readonly GUILayoutOption[] Options;
        /// False for Space and FlexibleSpace: they take no margin and leave no gap of their
        /// own, and do not count towards a group's smallest margin.
        public bool ConsideredForMargin = true;

        /// The rect GUILayout calls return where nothing is laid out (Layout, Used, an empty
        /// group).
        public static readonly Rect Dummy = new(0f, 0f, 1f, 1f);

        /// What a call past the end of a group gets outside Repaint when the group is empty.
        public static readonly LayoutEntry DummyEntry = new(GUIStyle.none, null) { Rect = Dummy };

        public LayoutEntry(GUIStyle style, GUILayoutOption[] options)
        {
            Style = style ?? GUIStyle.none;
            Options = options;
            var m = Style.margin;
            MarginLeft = m.left;
            MarginRight = m.right;
            MarginTop = m.top;
            MarginBottom = m.bottom;
        }

        /// A leaf of the given size range; the style's fixed sizes and stretch flags, then the
        /// options, override it.
        public static LayoutEntry Leaf(GUIStyle style, float minWidth, float maxWidth, float minHeight, float maxHeight, GUILayoutOption[] options)
        {
            var e = new LayoutEntry(style, options) { MinWidth = minWidth, MaxWidth = maxWidth, MinHeight = minHeight, MaxHeight = maxHeight };
            e.ApplyStyleWidth();
            e.ApplyStyleHeight();
            e.ApplyOptions(true);
            e.ApplyOptions(false);
            return e;
        }

        /// Space and FlexibleSpace: an entry without margins along `vertical`'s axis.
        public static LayoutEntry Spacer(bool vertical, float pixels, bool flexible)
        {
            var e = new LayoutEntry(GUIStyle.none, null) { ConsideredForMargin = false };
            e.MarginLeft = e.MarginRight = e.MarginTop = e.MarginBottom = 0;
            if (vertical)
            {
                e.MinHeight = e.MaxHeight = pixels;
                e.StretchHeight = flexible;
            }
            else
            {
                e.MinWidth = e.MaxWidth = pixels;
                e.StretchWidth = flexible;
            }
            return e;
        }

        protected void ApplyStyleWidth()
        {
            if (Style.fixedWidth != 0f)
            {
                MinWidth = MaxWidth = Style.fixedWidth;
                StretchWidth = false;
            }
            else
            {
                StretchWidth = Style.stretchWidth;
            }
        }

        protected void ApplyStyleHeight()
        {
            if (Style.fixedHeight != 0f)
            {
                MinHeight = MaxHeight = Style.fixedHeight;
                StretchHeight = false;
            }
            else
            {
                StretchHeight = Style.stretchHeight;
            }
        }

        /// GUILayout options, in the order given: a fixed size sets both bounds and stops
        /// stretching; a maximum stops stretching and lowers the minimum if needed; a minimum
        /// raises the maximum if needed; Expand sets stretching.
        protected void ApplyOptions(bool horizontal)
        {
            if (Options == null) return;
            ref float min = ref horizontal ? ref MinWidth : ref MinHeight;
            ref float max = ref horizontal ? ref MaxWidth : ref MaxHeight;
            ref bool stretch = ref horizontal ? ref StretchWidth : ref StretchHeight;
            foreach (var o in Options)
            {
                if (o == null || o.Horizontal != horizontal) continue;
                switch (o.Rule)
                {
                    case LayoutRule.Fixed:
                        min = max = o.Value;
                        stretch = false;
                        break;
                    case LayoutRule.Min:
                        min = o.Value;
                        if (max < min) max = min;
                        break;
                    case LayoutRule.Max:
                        max = o.Value;
                        if (min > max) min = max;
                        stretch = false;
                        break;
                    case LayoutRule.Expand:
                        stretch = o.Value != 0f;
                        break;
                }
            }
        }

        public virtual void CalcWidth() { }

        public virtual void CalcHeight() { }

        public virtual void SetHorizontal(float x, float width)
        {
            Rect.x = x;
            Rect.width = width;
        }

        public virtual void SetVertical(float y, float height)
        {
            Rect.y = y;
            Rect.height = height;
        }

        public float Min(bool horizontal) => horizontal ? MinWidth : MinHeight;
        public float Max(bool horizontal) => horizontal ? MaxWidth : MaxHeight;
        public bool Stretches(bool horizontal) => horizontal ? StretchWidth : StretchHeight;
        public int Leading(bool horizontal) => horizontal ? MarginLeft : MarginTop;
        public int Trailing(bool horizontal) => horizontal ? MarginRight : MarginBottom;

        public void Set(bool horizontal, float position, float size)
        {
            if (horizontal) SetHorizontal(position, size);
            else SetVertical(position, size);
        }
    }

    /// A content entry of a style without word wrap: CalcSize rounded up to whole pixels.
    /// A word-wrapping style's entry takes its width range from CalcMinMaxWidth (rounded up)
    /// and, once its width is known, the height CalcHeight gives there, unrounded.
    internal sealed class WrappedEntry : LayoutEntry
    {
        readonly GUIContent content;

        public WrappedEntry(GUIContent content, GUIStyle style, GUILayoutOption[] options) : base(style, options)
        {
            this.content = new GUIContent(content);
            Style.CalcMinMaxWidth(this.content, out float min, out float max);
            MinWidth = Mathf.Ceil(min);
            MaxWidth = Mathf.Ceil(max);
            MinHeight = MaxHeight = Style.CalcSize(this.content).y;
            ApplyStyleWidth();
            ApplyStyleHeight();
            ApplyOptions(true);
            ApplyOptions(false);
        }

        public override void CalcHeight()
        {
            if (Style.fixedHeight != 0f) return;
            MinHeight = MaxHeight = Style.CalcHeight(content, Rect.width);
            ApplyOptions(false);
        }
    }

    /// An entry that keeps a width-to-height ratio: it takes the width the layout gives it and
    /// the height that matches.
    internal sealed class AspectEntry : LayoutEntry
    {
        readonly float aspect;

        public AspectEntry(float aspect, GUIStyle style, GUILayoutOption[] options) : base(style, options)
        {
            this.aspect = aspect;
            ApplyStyleWidth();
            ApplyOptions(true);
        }

        public override void CalcHeight()
        {
            MinHeight = MaxHeight = aspect != 0f ? Rect.width / aspect : 0f;
            StretchHeight = false;
            ApplyOptions(false);
        }
    }

    /// A horizontal or vertical group: BeginHorizontal, BeginVertical, an area, a window's
    /// content, the root of an OnGUI call, and (ScrollGroup) a scroll view.
    ///
    /// Along its axis a group places its entries one after the other from an unrounded running
    /// position, rounding each entry's position and size to whole pixels; between neighbours
    /// the gap is the larger of the first one's trailing and the next one's leading margin
    /// (Space and FlexibleSpace leave none of their own). Across its axis sizes are not
    /// rounded.
    ///
    /// A group with a style (anything but GUIStyle.none) insets each entry by the larger of
    /// its padding and the entry's own margin, side by side, and its own margin is the
    /// style's. A group without one passes its entries' margins out: along its axis its margins
    /// are the first entry's leading and the last one's trailing margin, across it the smallest
    /// of its entries'; each entry is inset by its margin less that smallest one.
    internal class LayoutGroup : LayoutEntry
    {
        public readonly bool Vertical;
        public readonly List<LayoutEntry> Entries = new();
        /// The next entry the calls of a later event take.
        public int Cursor;
        /// An area (BeginArea): laid out on its own at its rect's size.
        public bool IsArea;
        public Vector2 AreaSize;

        public LayoutGroup(bool vertical, GUIStyle style, GUILayoutOption[] options) : base(style, options)
        {
            Vertical = vertical;
        }

        protected virtual bool Styled => !ReferenceEquals(Style, GUIStyle.none);

        protected virtual RectOffset Padding => Style.padding;

        // The children's size ranges as last measured, before the group's own fixed sizes and
        // options: what arranging them needs.
        float childMinX, childMaxX, childMinY, childMaxY;

        protected float ChildMin(bool horizontal) => horizontal ? childMinX : childMinY;
        protected float ChildMax(bool horizontal) => horizontal ? childMaxX : childMaxY;

        /// The next entry for a call in an event after Layout. Past the end it throws during
        /// Repaint; in other events it gives the last entry again (the dummy rect when there is
        /// none) without moving on.
        public LayoutEntry GetNext()
        {
            if (Cursor < Entries.Count) return Entries[Cursor++];
            if (Event.current != null && Event.current.type == EventType.Repaint)
                GUILayoutUtility.ThrowPastTheEnd(Cursor, Entries.Count);
            return Entries.Count > 0 ? Entries[^1] : DummyEntry;
        }

        /// The rect of the entry the last call took; straight after the group began (nothing
        /// taken yet), an error during Repaint and the dummy rect.
        public Rect GetLast()
        {
            if (Cursor == 0)
            {
                if (Event.current != null && Event.current.type == EventType.Repaint)
                    Debug.LogError("You cannot call GetLast immediately after beginning a group.");
                return Dummy;
            }
            return Entries[Mathf.Min(Cursor, Entries.Count) - 1].Rect;
        }

        public override void CalcWidth()
        {
            foreach (var e in Entries) e.CalcWidth();
            Measure(true);
            FinishMeasure(true);
        }

        public override void CalcHeight()
        {
            foreach (var e in Entries) e.CalcHeight();
            Measure(false);
            FinishMeasure(false);
        }

        /// The group's own fixed size (its style's) and options on one axis.
        protected void FinishMeasure(bool horizontal)
        {
            float fixedSize = horizontal ? Style.fixedWidth : Style.fixedHeight;
            if (fixedSize != 0f)
            {
                if (horizontal)
                {
                    MinWidth = MaxWidth = fixedSize;
                    StretchWidth = false;
                }
                else
                {
                    MinHeight = MaxHeight = fixedSize;
                    StretchHeight = false;
                }
            }
            ApplyOptions(horizontal);
        }

        /// The group's size range on one axis: its entries', with their gaps and the group's
        /// edges. A group stretches when its style does or one of its entries does.
        void Measure(bool horizontal)
        {
            MeasureEntries(horizontal);
            bool stretch = horizontal ? Style.stretchWidth : Style.stretchHeight;
            foreach (var e in Entries) stretch |= e.Stretches(horizontal);
            if (horizontal)
            {
                MinWidth = childMinX;
                MaxWidth = childMaxX;
                StretchWidth = stretch;
            }
            else
            {
                MinHeight = childMinY;
                MaxHeight = childMaxY;
                StretchHeight = stretch;
            }
        }

        /// The size range of the entries on one axis, with their gaps and the group's edges;
        /// for a group without a style, its own margins too.
        protected void MeasureEntries(bool horizontal)
        {
            float min, max;
            if (horizontal != Vertical) MeasureAlong(horizontal, out min, out max);
            else MeasureAcross(horizontal, out min, out max);
            if (horizontal)
            {
                childMinX = min;
                childMaxX = max;
            }
            else
            {
                childMinY = min;
                childMaxY = max;
            }
        }

        int PadStart(bool horizontal) => horizontal ? Padding.left : Padding.top;
        int PadEnd(bool horizontal) => horizontal ? Padding.right : Padding.bottom;

        /// The gaps along the axis: the leading inset of each entry (the first's edge, then the
        /// gap after the one before), and the trailing edge after the last.
        float[] Gaps(bool horizontal, out float trailingEdge, out int firstLeading, out int lastTrailing)
        {
            var leads = new float[Entries.Count];
            bool styled = Styled;
            bool first = true;
            int last = 0;
            firstLeading = 0;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                float gap;
                if (e.ConsideredForMargin)
                {
                    int lead = e.Leading(horizontal);
                    if (first)
                    {
                        gap = styled ? Mathf.Max(PadStart(horizontal), lead) : 0f;
                        firstLeading = lead;
                    }
                    else
                    {
                        gap = Mathf.Max(last, lead);
                    }
                    last = e.Trailing(horizontal);
                }
                else
                {
                    // A space starts where the entry before it ends; the margin of that entry
                    // still separates it from the next one (Line, Space(4), a scroll view of
                    // margin 0: 4 between them).
                    gap = first && styled ? PadStart(horizontal) : 0f;
                }
                first = false;
                leads[i] = gap;
            }
            lastTrailing = last;
            trailingEdge = styled ? (Entries.Count > 0 ? Mathf.Max(PadEnd(horizontal), last) : PadStart(horizontal) + PadEnd(horizontal)) : 0f;
            return leads;
        }

        void MeasureAlong(bool horizontal, out float min, out float max)
        {
            var leads = Gaps(horizontal, out float edge, out int firstLeading, out int lastTrailing);
            // The entries' sizes, then the gaps between them, then the edges: the order the
            // player's sums round in (fifteen labels in a window: 371.15, not 371.15002).
            min = max = 0f;
            float gaps = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                min += Entries[i].Min(horizontal);
                max += Entries[i].Max(horizontal);
                if (i > 0) gaps += leads[i];
            }
            float edges = (Entries.Count > 0 ? leads[0] : 0f) + edge;
            min = min + gaps + edges;
            max = max + gaps + edges;
            if (!Styled)
            {
                if (horizontal)
                {
                    MarginLeft = firstLeading;
                    MarginRight = lastTrailing;
                }
                else
                {
                    MarginTop = firstLeading;
                    MarginBottom = lastTrailing;
                }
            }
        }

        /// The smallest leading and trailing margins across the axis of the entries that count
        /// for margins (0 when none does).
        void SmallestMargins(bool horizontal, out int leading, out int trailing)
        {
            bool any = false;
            leading = trailing = 0;
            foreach (var e in Entries)
            {
                if (!e.ConsideredForMargin) continue;
                if (!any)
                {
                    leading = e.Leading(horizontal);
                    trailing = e.Trailing(horizontal);
                    any = true;
                }
                else
                {
                    leading = Mathf.Min(leading, e.Leading(horizontal));
                    trailing = Mathf.Min(trailing, e.Trailing(horizontal));
                }
            }
        }

        /// The insets of an entry across the axis.
        void Insets(LayoutEntry e, bool horizontal, int smallestLeading, int smallestTrailing, out float lead, out float trail)
        {
            int l = e.ConsideredForMargin ? e.Leading(horizontal) : 0, t = e.ConsideredForMargin ? e.Trailing(horizontal) : 0;
            if (Styled)
            {
                lead = Mathf.Max(PadStart(horizontal), l);
                trail = Mathf.Max(PadEnd(horizontal), t);
            }
            else
            {
                lead = l - smallestLeading;
                trail = t - smallestTrailing;
            }
        }

        /// Across the axis the group is as large as its largest entry with that entry's margins
        /// less the smallest margins; a group with a style adds the larger of its padding and
        /// those smallest margins (a window's content holding a scroll view, of margin 0: a row
        /// of 314 with margins 4 needs 10 + 4 + 314 + 4 + 10). A horizontal group without a
        /// style is as high as its highest entry, margins left out.
        void MeasureAcross(bool horizontal, out float min, out float max)
        {
            SmallestMargins(horizontal, out int sl, out int st);
            min = max = 0f;
            bool styled = Styled;
            foreach (var e in Entries)
            {
                float extra = 0f;
                if (styled || horizontal)
                {
                    int l = e.ConsideredForMargin ? e.Leading(horizontal) : 0, t = e.ConsideredForMargin ? e.Trailing(horizontal) : 0;
                    extra = l + t - sl - st;
                }
                min = Mathf.Max(min, e.Min(horizontal) + extra);
                max = Mathf.Max(max, e.Max(horizontal) + extra);
            }
            if (styled)
            {
                float edges = Mathf.Max(PadStart(horizontal), sl) + Mathf.Max(PadEnd(horizontal), st);
                min += edges;
                max += edges;
            }
            if (!styled)
            {
                if (horizontal)
                {
                    MarginLeft = sl;
                    MarginRight = st;
                }
                else
                {
                    MarginTop = sl;
                    MarginBottom = st;
                }
            }
        }

        public override void SetHorizontal(float x, float width)
        {
            base.SetHorizontal(x, width);
            Arrange(true, x, width);
        }

        public override void SetVertical(float y, float height)
        {
            base.SetVertical(y, height);
            Arrange(false, y, height);
        }

        protected void Arrange(bool horizontal, float start, float size)
        {
            if (horizontal != Vertical) ArrangeAlong(horizontal, start, size);
            else ArrangeAcross(horizontal, start, size);
        }

        /// Along the axis: every entry gets its minimum; the space left first brings the
        /// entries towards their maximums (the same fraction of the way for each), and what
        /// remains is shared equally by the entries that stretch. Positions and sizes are
        /// rounded per entry.
        void ArrangeAlong(bool horizontal, float start, float size)
        {
            var gaps = Gaps(horizontal, out _, out _, out _);
            float room = 0f;
            int stretching = 0;
            foreach (var e in Entries)
            {
                room += Mathf.Max(0f, e.Max(horizontal) - e.Min(horizontal));
                if (e.Stretches(horizontal)) stretching++;
            }
            float extra = size - ChildMin(horizontal);
            float t = 0f;
            if (extra > 0f && room > 0f)
            {
                t = Mathf.Min(1f, extra / room);
                extra -= t * room;
            }
            float share = extra > 0f && stretching > 0 ? extra / stretching : 0f;
            float position = start;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                position += gaps[i];
                float min = e.Min(horizontal);
                float s = min + t * Mathf.Max(0f, e.Max(horizontal) - min) + (e.Stretches(horizontal) ? share : 0f);
                e.Set(horizontal, Mathf.Round(position), Mathf.Round(s));
                position += s;
            }
        }

        /// Across the axis: a stretching entry spans the group less its insets (even when that
        /// is less than its minimum: a row of 314 in a window 280 wide inside is 280), the others
        /// keep their size within their range, at the start.
        void ArrangeAcross(bool horizontal, float start, float size)
        {
            SmallestMargins(horizontal, out int sl, out int st);
            foreach (var e in Entries)
            {
                Insets(e, horizontal, sl, st, out float lead, out float trail);
                float available = size - lead - trail;
                float min = e.Min(horizontal);
                float s = e.Stretches(horizontal) ? available : Mathf.Clamp(available, min, Mathf.Max(min, e.Max(horizontal)));
                e.Set(horizontal, start + lead, s);
            }
        }

        /// Lays the group out at a rect of its own (the root, an area, a window's content).
        public void LayOut(Rect rect)
        {
            CalcWidth();
            SetHorizontal(rect.x, rect.width);
            CalcHeight();
            SetVertical(rect.y, rect.height);
        }
    }

    /// GUILayout.BeginScrollView: an entry of its parent (the view, with the background
    /// style's margins) whose entries are its content. The content is a vertical group with a
    /// style and no padding, laid out at its own size: as wide as the view less a vertical
    /// scrollbar, or wider; as high as the view less a horizontal scrollbar, or higher.
    /// A scrollbar takes room when the content does not fit (or always, when asked): its fixed
    /// size plus its leading margin.
    internal sealed class ScrollGroup : LayoutGroup
    {
        /// The view takes at most this much of its content's minimum size as its own.
        const float MinViewSize = 32f;

        static readonly RectOffset NoPadding = new();

        readonly bool alwaysHorizontal, alwaysVertical;
        readonly float horizontalBar, verticalBar;
        float contentWidth, contentHeight;

        public ScrollGroup(bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar, GUIStyle background, GUILayoutOption[] options)
            : base(true, background, options)
        {
            alwaysHorizontal = alwaysShowHorizontal;
            alwaysVertical = alwaysShowVertical;
            horizontalBar = ScrollViews.BarThickness(horizontalScrollbar, false);
            verticalBar = ScrollViews.BarThickness(verticalScrollbar, true);
        }

        protected override bool Styled => true;

        protected override RectOffset Padding => NoPadding;

        public bool NeedsHorizontal { get; private set; }
        public bool NeedsVertical { get; private set; }

        /// The content's rect in the scroll view's own coordinates.
        public Rect ViewRect => new(0f, 0f, contentWidth, contentHeight);

        public override void CalcWidth()
        {
            foreach (var e in Entries) e.CalcWidth();
            MeasureEntries(true);
            MinWidth = Mathf.Min(ChildMin(true), MinViewSize);
            MaxWidth = Mathf.Max(MinWidth, ChildMax(true));
            StretchWidth = true;
            FinishMeasure(true);
        }

        public override void CalcHeight()
        {
            foreach (var e in Entries) e.CalcHeight();
            MeasureEntries(false);
            MinHeight = Mathf.Min(ChildMin(false), MinViewSize);
            MaxHeight = Mathf.Max(MinHeight, ChildMax(false));
            StretchHeight = true;
            FinishMeasure(false);
        }

        public override void SetHorizontal(float x, float width)
        {
            Rect.x = x;
            Rect.width = width;
            ArrangeContentX(width - (alwaysVertical ? verticalBar : 0f));
        }

        void ArrangeContentX(float viewWidth)
        {
            contentWidth = Mathf.Max(ChildMin(true), viewWidth);
            Arrange(true, 0f, contentWidth);
        }

        public override void SetVertical(float y, float height)
        {
            Rect.y = y;
            Rect.height = height;
            float width = Rect.width;
            bool needH = alwaysHorizontal, needV = alwaysVertical;
            for (int i = 0; i < 2; i++)
            {
                needV = alwaysVertical || ChildMin(false) > height - (needH ? horizontalBar : 0f);
                needH = alwaysHorizontal || ChildMin(true) > width - (needV ? verticalBar : 0f);
            }
            if (needV && !alwaysVertical)
            {
                // The vertical bar takes width the content was laid out with.
                ArrangeContentX(width - verticalBar);
                foreach (var e in Entries) e.CalcHeight();
                MeasureEntries(false);
            }
            NeedsHorizontal = needH;
            NeedsVertical = needV;
            contentHeight = Mathf.Max(ChildMin(false), height - (needH ? horizontalBar : 0f));
            Arrange(false, 0f, contentHeight);
        }
    }
}
