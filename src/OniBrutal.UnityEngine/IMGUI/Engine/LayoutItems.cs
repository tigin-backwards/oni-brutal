using System.Collections.Generic;
using UnityEngine;

namespace OniBrutal.Engine.IMGUI
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
    /// two passes, widths first (top down after measuring bottom up), then heights, so that
    /// entries whose height depends on their width (word wrap, aspect) know it.
    internal class LayoutItem
    {
        public float MinWidth, MaxWidth, MinHeight, MaxHeight;
        public bool StretchWidth, StretchHeight;
        public int MarginLeft, MarginRight, MarginTop, MarginBottom;
        public Rect Rect;
        public readonly GUIStyle Style;
        public readonly GUILayoutOption[] Options;

        public LayoutItem(GUIStyle style, GUILayoutOption[] options)
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
        public static LayoutItem Leaf(GUIStyle style, float minWidth, float maxWidth, float minHeight, float maxHeight, GUILayoutOption[] options)
        {
            var item = new LayoutItem(style, options) { MinWidth = minWidth, MaxWidth = maxWidth, MinHeight = minHeight, MaxHeight = maxHeight };
            item.ApplyStyleWidth();
            item.ApplyStyleHeight();
            item.ApplyOptions(true);
            item.ApplyOptions(false);
            return item;
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

        public virtual void MeasureWidth() { }

        public virtual void PlaceX(float x, float width)
        {
            Rect.x = x;
            Rect.width = width;
        }

        public virtual void MeasureHeight() { }

        public virtual void PlaceY(float y, float height)
        {
            Rect.y = y;
            Rect.height = height;
        }

        public float Min(bool horizontal) => horizontal ? MinWidth : MinHeight;
        public float Max(bool horizontal) => horizontal ? MaxWidth : MaxHeight;
        public bool Stretches(bool horizontal) => horizontal ? StretchWidth : StretchHeight;
        public int Leading(bool horizontal) => horizontal ? MarginLeft : MarginTop;
        public int Trailing(bool horizontal) => horizontal ? MarginRight : MarginBottom;

        public void Place(bool horizontal, float position, float size)
        {
            if (horizontal) PlaceX(position, size);
            else PlaceY(position, size);
        }
    }

    /// A word-wrapping content entry: its width range comes from CalcMinMaxWidth, its height
    /// from CalcHeight at the width it gets.
    internal sealed class WrappedItem : LayoutItem
    {
        readonly GUIContent content;

        public WrappedItem(GUIContent content, GUIStyle style, GUILayoutOption[] options) : base(style, options)
        {
            this.content = new GUIContent(content);
            Style.CalcMinMaxWidth(this.content, out MinWidth, out MaxWidth);
            var size = Style.CalcSize(this.content);
            MinHeight = MaxHeight = size.y;
            ApplyStyleWidth();
            ApplyStyleHeight();
            ApplyOptions(true);
            ApplyOptions(false);
        }

        public override void MeasureHeight()
        {
            if (Style.fixedHeight != 0f) return;
            MinHeight = MaxHeight = Style.CalcHeight(content, Rect.width);
            ApplyOptions(false);
        }
    }

    /// An entry that keeps a width-to-height ratio: it takes the width the layout gives it and
    /// the height that matches.
    internal sealed class AspectItem : LayoutItem
    {
        readonly float aspect;

        public AspectItem(float aspect, GUIStyle style, GUILayoutOption[] options) : base(style, options)
        {
            this.aspect = aspect;
            ApplyStyleWidth();
            ApplyOptions(true);
        }

        public override void MeasureHeight()
        {
            MinHeight = MaxHeight = aspect != 0f ? Rect.width / aspect : 0f;
            StretchHeight = false;
            ApplyOptions(false);
        }
    }

    /// A horizontal or vertical group (BeginHorizontal, BeginVertical, an area's content, a
    /// window's content, the root). Its style's padding insets the children. An area is
    /// recorded among its parent's children (so that the calls match up), but does not take
    /// part in the parent's layout: it is laid out on its own at its fixed rect.
    internal class LayoutBox : LayoutItem
    {
        public readonly bool Vertical;
        public readonly bool IsArea;
        public readonly List<LayoutItem> Children = new();
        /// The next child the replaying calls take.
        public int Cursor;

        public LayoutBox(bool vertical, GUIStyle style, GUILayoutOption[] options, bool isArea = false) : base(style, options)
        {
            Vertical = vertical;
            IsArea = isArea;
        }

        RectOffset Padding => Style.padding;

        public override void MeasureWidth()
        {
            foreach (var c in Children)
                if (c is not LayoutBox { IsArea: true }) c.MeasureWidth();
            Measure(true);
            ApplyStyleFixed(true);
            ApplyOptions(true);
        }

        public override void MeasureHeight()
        {
            foreach (var c in Children)
                if (c is not LayoutBox { IsArea: true }) c.MeasureHeight();
            Measure(false);
            ApplyStyleFixed(false);
            ApplyOptions(false);
        }

        void ApplyStyleFixed(bool horizontal)
        {
            float fixedSize = horizontal ? Style.fixedWidth : Style.fixedHeight;
            if (fixedSize == 0f) return;
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

        /// Size range on one axis: along the group's axis the children's sizes and the gaps
        /// between them add up; across it the widest child counts. The group stretches when a
        /// child does.
        void Measure(bool horizontal)
        {
            bool along = horizontal != Vertical;
            float pad = horizontal ? Padding.horizontal : Padding.vertical;
            float min = 0f, max = 0f;
            bool stretch = false;
            if (along)
            {
                min = max = Gaps(horizontal);
                foreach (var c in Children)
                {
                    if (c is LayoutBox { IsArea: true }) continue;
                    min += c.Min(horizontal);
                    max += c.Max(horizontal);
                    stretch |= c.Stretches(horizontal);
                }
            }
            else
            {
                foreach (var c in Children)
                {
                    if (c is LayoutBox { IsArea: true }) continue;
                    float margins = c.Leading(horizontal) + c.Trailing(horizontal);
                    min = Mathf.Max(min, c.Min(horizontal) + margins);
                    max = Mathf.Max(max, c.Max(horizontal) + margins);
                    stretch |= c.Stretches(horizontal);
                }
            }
            min += pad;
            max = Mathf.Max(min, max + pad);
            if (horizontal)
            {
                MinWidth = min;
                MaxWidth = max;
                StretchWidth = stretch;
            }
            else
            {
                MinHeight = min;
                MaxHeight = max;
                StretchHeight = stretch;
            }
        }

        /// The first child's leading margin, the larger margin between neighbours, and the
        /// last child's trailing margin.
        float Gaps(bool horizontal)
        {
            float total = 0f;
            int previousTrailing = -1;
            foreach (var c in Children)
            {
                if (c is LayoutBox { IsArea: true }) continue;
                int leading = c.Leading(horizontal);
                total += previousTrailing < 0 ? leading : Mathf.Max(previousTrailing, leading);
                previousTrailing = c.Trailing(horizontal);
            }
            if (previousTrailing > 0) total += previousTrailing;
            return total;
        }

        public override void PlaceX(float x, float width)
        {
            base.PlaceX(x, width);
            Arrange(true, x + Padding.left, width - Padding.horizontal);
        }

        public override void PlaceY(float y, float height)
        {
            base.PlaceY(y, height);
            Arrange(false, y + Padding.top, height - Padding.vertical);
        }

        void Arrange(bool horizontal, float start, float inner)
        {
            if (horizontal != Vertical) ArrangeAlong(horizontal, start, inner);
            else ArrangeAcross(horizontal, start, inner);
        }

        /// Along the axis: every child gets its minimum; the space left first brings the
        /// children towards their maximums (the same fraction of the way for each), and what
        /// remains is shared equally by the children that stretch.
        void ArrangeAlong(bool horizontal, float start, float inner)
        {
            float sumMin = 0f, room = 0f;
            int stretching = 0;
            foreach (var c in Children)
            {
                if (c is LayoutBox { IsArea: true }) continue;
                sumMin += c.Min(horizontal);
                room += Mathf.Max(0f, c.Max(horizontal) - c.Min(horizontal));
                if (c.Stretches(horizontal)) stretching++;
            }
            float extra = inner - Gaps(horizontal) - sumMin;
            float t = 0f;
            if (extra > 0f && room > 0f)
            {
                t = Mathf.Min(1f, extra / room);
                extra -= t * room;
            }
            float share = extra > 0f && stretching > 0 ? extra / stretching : 0f;
            float position = start;
            int previousTrailing = -1;
            foreach (var c in Children)
            {
                if (c is LayoutBox { IsArea: true }) continue;
                int leading = c.Leading(horizontal);
                position += previousTrailing < 0 ? leading : Mathf.Max(previousTrailing, leading);
                float min = c.Min(horizontal);
                float size = min + t * Mathf.Max(0f, c.Max(horizontal) - min) + (c.Stretches(horizontal) ? share : 0f);
                c.Place(horizontal, position, size);
                position += size;
                previousTrailing = c.Trailing(horizontal);
            }
        }

        /// Across the axis: a stretching child spans the content size, the others keep their
        /// size within their range, at the start.
        void ArrangeAcross(bool horizontal, float start, float inner)
        {
            foreach (var c in Children)
            {
                if (c is LayoutBox { IsArea: true }) continue;
                float available = inner - c.Leading(horizontal) - c.Trailing(horizontal);
                float min = c.Min(horizontal);
                float size = c.Stretches(horizontal) ? Mathf.Max(available, min) : Mathf.Clamp(available, min, Mathf.Max(min, c.Max(horizontal)));
                c.Place(horizontal, start + c.Leading(horizontal), size);
            }
        }

        /// Lays the group out at a rect of its own (the root, an area, a window's content).
        public void LayOut(Rect rect)
        {
            MeasureWidth();
            PlaceX(rect.x, rect.width);
            MeasureHeight();
            PlaceY(rect.y, rect.height);
        }
    }

    /// GUILayout.BeginScrollView: an entry in its parent that shows its content, laid out at the
    /// content's own size, through a scroll view. Scrollbars take room when the content does
    /// not fit (or always, when asked).
    internal sealed class ScrollBox : LayoutItem
    {
        public readonly LayoutBox Content = new(true, GUIStyle.none, null);
        readonly bool alwaysHorizontal, alwaysVertical;
        readonly float horizontalBar, verticalBar;

        public ScrollBox(bool alwaysShowHorizontal, bool alwaysShowVertical, GUIStyle horizontalScrollbar, GUIStyle verticalScrollbar, GUIStyle background, GUILayoutOption[] options)
            : base(background, options)
        {
            alwaysHorizontal = alwaysShowHorizontal;
            alwaysVertical = alwaysShowVertical;
            horizontalBar = ScrollViews.BarThickness(horizontalScrollbar, false);
            verticalBar = ScrollViews.BarThickness(verticalScrollbar, true);
        }

        public bool NeedsHorizontal { get; private set; }
        public bool NeedsVertical { get; private set; }

        /// The content's rect in the scroll view's own coordinates.
        public Rect ViewRect => new(0f, 0f, Content.Rect.width, Content.Rect.height);

        RectOffset Padding => Style.padding;

        public override void MeasureWidth()
        {
            Content.MeasureWidth();
            MinWidth = Padding.horizontal + verticalBar;
            MaxWidth = Mathf.Max(MinWidth, Content.MaxWidth + Padding.horizontal + verticalBar);
            ApplyStyleWidth();
            if (Style.fixedWidth == 0f) StretchWidth = true;
            ApplyOptions(true);
        }

        public override void PlaceX(float x, float width)
        {
            base.PlaceX(x, width);
            PlaceContentX(width - Padding.horizontal - (alwaysVertical ? verticalBar : 0f));
        }

        void PlaceContentX(float viewWidth) => Content.PlaceX(0f, Mathf.Max(Content.MinWidth, viewWidth));

        public override void MeasureHeight()
        {
            Content.MeasureHeight();
            MinHeight = Padding.vertical + horizontalBar;
            MaxHeight = Mathf.Max(MinHeight, Content.MaxHeight + Padding.vertical + horizontalBar);
            ApplyStyleHeight();
            if (Style.fixedHeight == 0f) StretchHeight = true;
            ApplyOptions(false);
        }

        public override void PlaceY(float y, float height)
        {
            base.PlaceY(y, height);
            float innerWidth = Rect.width - Padding.horizontal, innerHeight = height - Padding.vertical;
            bool needH = alwaysHorizontal, needV = alwaysVertical;
            for (int i = 0; i < 2; i++)
            {
                needV = alwaysVertical || Content.MinHeight > innerHeight - (needH ? horizontalBar : 0f);
                needH = alwaysHorizontal || Content.MinWidth > innerWidth - (needV ? verticalBar : 0f);
            }
            if (needV && !alwaysVertical)
            {
                // The vertical bar takes width the content was laid out with.
                PlaceContentX(innerWidth - verticalBar);
                Content.MeasureHeight();
            }
            NeedsHorizontal = needH;
            NeedsVertical = needV;
            Content.PlaceY(0f, Mathf.Max(Content.MinHeight, innerHeight - (needH ? horizontalBar : 0f)));
        }
    }
}
