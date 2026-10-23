using System;
using UnityEngine;
using UnityEngine.TextCore;

namespace TMPro
{
    // Layout: the parsed elements measured at a font size, broken into lines, aligned in the
    // rect and recorded in textInfo.
    public abstract partial class TMP_Text
    {
        static readonly TextParser s_Parser = new();

        // What a layout needs per character beyond textInfo.
        private protected struct Placement
        {
            public int Element;
            public float X;              // pen position on its line (before alignment)
            public float Advance;        // pen advance: the glyph's and the spacing after it
            public float GlyphAdvance;   // the glyph's own advance (what xAdvance and line lengths use)
            public float PreSpace;       // <space> before the character
            public float SetPen;         // <pos> before the character (NaN: none)
            public bool PageBreak;       // <page> before the character
            public float Scale;          // font units → pixels for the glyph
            public float FontScale;      // of the font at the character's size
            public float XScale;         // horizontal factor (character width adjustment)
            public float BaselineShift;  // superscript, subscript and <voffset>
            public float Ascender, Descender, Gap;  // font metrics at the size (Gap: line height − ascender)
            public float Top, Bottom, Left, Right;  // glyph box (pixels, from origin and baseline)
            public float MonoOffset;
            public float KernY;          // glyph placement from kerning pairs (y)
            public float PointSize;
            public bool Visible, Whitespace, LineBreak, Bold, Italic, NoBreak, BreakAfter;
            // Checked against the rect's height: every character but white space (a tab is
            // checked, though it is not visible).
            public bool Checked;
            // A soft hyphen's advance: it takes no room, and is drawn only where a line breaks
            // after it (0: not a soft hyphen).
            public float SoftHyphen;
            // A tab's stop interval (0: not a tab).
            public float TabStop;
            // An empty record where truncation stopped (textInfo holds a blank character there).
            public bool Blank;
            public float LineHeightOverride;  // NaN: none
            public float MarginLeft, MarginRight, Indent, LineIndent;
            public float WidthLimit;     // NaN: none
            public HorizontalAlignmentOptions Align;
            public float JustifyOffset;
            // Set by placement: the pen and baseline relative to the line, and the line's offset
            // into the rect.
            public float Pen, BaselineRel, OffsetX, OffsetY;
            // The font's line height at the character's size.
            public float FontLineHeight;
        }

        private protected struct LineData
        {
            public int First, Last;
            public int FirstVisible, LastVisible;
            public int LastChecked;      // the last checked character (-1: none)
            // Whether the line's descender is below the rect (its characters do not fit the
            // height).
            public bool Below;
            public float Ascender, Descender, Gap, MaxAscent, FontLineHeight;
            // The descender without a soft hyphen drawn where the line breaks: what the line
            // must fit in the rect (the hyphen is drawn after the line was found to fit).
            public float FitDescender;
            public float Length;         // pen advance to the end of the last visible character
            // The last tab's start and end, from the line's start (-1: no tab).
            public float TabStart, TabEnd;
            public float Width;          // the width available to the line
            public float MarginLeft, MarginRight, StartX;
            public float Baseline;
            public float LineHeight;
            public bool EndsWithLineFeed;
            public HorizontalAlignmentOptions Align;
            public int Page;
            public float OffsetX;
            public int Spaces, Visible;
        }

        private protected static Placement[] s_Placements = new Placement[64];
        private protected static LineData[] s_Lines = new LineData[8];
        private protected static int s_LineCount;

        // ----- Entry points -----

        private protected void EnsureParsed()
        {
            if (!m_ParseRequired) return;
            ConvertAlignment();
            string source = m_text ?? "";
            if (m_TextPreprocessor != null) source = m_TextPreprocessor.PreprocessText(source);
            s_Parser.Parse(this, source, m_Parsed);
            m_SourceCache = m_text;
            m_ParseRequired = false;
        }

        private protected ParsedText Parsed => m_Parsed;

        // The material slots of the text's existing sub-mesh objects, which keep their indices.

        // The rect the text lays out in (local space).
        private protected abstract Rect LayoutRect { get; }

        // Lays the text out in its rect at the size auto-size chooses (or the font size), and
        // records the result in textInfo.
        private protected void LayoutForRendering()
        {
            EnsureParsed();
            var rect = LayoutRect;
            m_Padding = MaterialPadding();
            float size = ChooseFontSize(rect);
            if (m_enableAutoSizing) m_fontSize = size;
            Layout(size, rect.width, rect.height);
            Place(rect);
        }

        float MaterialPadding() => m_sharedMaterial == null ? 0f : ShaderUtilities.GetPadding(m_sharedMaterial, m_enableExtraPadding, (m_fontStyle & FontStyles.Bold) != 0);

        // ----- Font size -----

        // Auto-size: the largest size, in steps of 0.05 between the minimum and the maximum, at
        // which the text fits in the rect, as documented: at a size, lines may first move closer by
        // up to the line spacing adjustment (em/100) and characters narrow by up to the character
        // width adjustment (%), the least that makes the text fit. The minimum (fully adjusted)
        // when nothing fits.
        float ChooseFontSize(Rect rect)
        {
            m_LineSpacingAdjustment = 0f;
            m_CharWidthAdjustment = 0f;
            if (!m_enableAutoSizing) return m_fontSizeBase > 0f ? m_fontSizeBase : m_fontSize;
            // An empty text keeps its size.
            if (m_Parsed.Count == 0) return m_fontSize;
            m_FittingAutoSize = true;
            try { return ChooseFittingSize(rect); }
            finally { m_FittingAutoSize = false; }
        }

        // While auto-size searches, a word that has to be broken because it is wider than its
        // line does not fit.
        bool m_FittingAutoSize;

        float ChooseFittingSize(Rect rect)
        {
            float min = Mathf.Min(m_fontSizeMin, m_fontSizeMax), max = Mathf.Max(m_fontSizeMin, m_fontSizeMax);
            if (FitsAdjusted(max, rect)) return max;
            int lo = Mathf.CeilToInt(min * 20f - 0.001f), hi = Mathf.FloorToInt(max * 20f + 0.001f) - 1;
            int best = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (FitsAdjusted(mid / 20f, rect))
                {
                    best = mid;
                    lo = mid + 1;
                }
                else hi = mid - 1;
            }
            float size = best >= 0 ? best / 20f : min;
            if (!FitsAdjusted(size, rect))
            {
                m_LineSpacingAdjustment = m_lineSpacingMax;
                m_CharWidthAdjustment = m_charWidthMaxAdj / 100f;
            }
            return size;
        }

        // Whether the text fits at a size, with the least line spacing and character width
        // adjustments it needs (left in effect).
        bool FitsAdjusted(float size, Rect rect)
        {
            m_LineSpacingAdjustment = 0f;
            m_CharWidthAdjustment = 0f;
            if (Layout(size, rect.width, rect.height)) return true;
            if (m_lineSpacingMax > 0f)
            {
                m_LineSpacingAdjustment = m_lineSpacingMax;
                if (Layout(size, rect.width, rect.height))
                {
                    float a = 0f, b = m_lineSpacingMax;
                    for (int k = 0; k < 12; k++)
                    {
                        float m = (a + b) * 0.5f;
                        m_LineSpacingAdjustment = m;
                        if (Layout(size, rect.width, rect.height)) b = m;
                        else a = m;
                    }
                    m_LineSpacingAdjustment = b;
                    return true;
                }
            }
            if (m_charWidthMaxAdj > 0f)
            {
                float maxAdjust = m_charWidthMaxAdj / 100f;
                m_CharWidthAdjustment = maxAdjust;
                if (Layout(size, rect.width, rect.height))
                {
                    float a = 0f, b = maxAdjust;
                    for (int k = 0; k < 12; k++)
                    {
                        float m = (a + b) * 0.5f;
                        m_CharWidthAdjustment = m;
                        if (Layout(size, rect.width, rect.height)) b = m;
                        else a = m;
                    }
                    m_CharWidthAdjustment = b;
                    return true;
                }
            }
            m_LineSpacingAdjustment = 0f;
            m_CharWidthAdjustment = 0f;
            return false;
        }

        // ----- Layout -----

        // Measures and breaks the text into lines at a font size, for a rect of that size.
        // Returns whether the text fits (no overflow of the height, or of the width without
        // wrapping). Place then records the result in textInfo.
        private protected bool Layout(float baseSize, float rectWidth, float rectHeight)
        {
            var parsed = m_Parsed;
            int n = parsed.Count;
            if (s_Placements.Length < n + 1) Array.Resize(ref s_Placements, Mathf.NextPowerOfTwo(n + 1));
            var pl = s_Placements;
            float width = rectWidth - m_margin.x - m_margin.z;
            float height = rectHeight - m_margin.y - m_margin.w;
            bool wrap = m_TextWrappingMode == TextWrappingModes.Normal || m_TextWrappingMode == TextWrappingModes.PreserveWhitespace;
            if (float.IsInfinity(width) || float.IsNaN(width)) wrap = false;

            // 1. Characters.
            int count = 0;
            bool endedAtStart = false;
            float space = 0f, pos = float.NaN;
            bool page = false;
            var rules = TMP_Settings.linebreakingRules;
            for (int e = 0; e < n; e++)
            {
                ref var el = ref parsed.Elements[e];
                switch (el.Kind)
                {
                    case ElementKind.Space:
                        space += el.Amount.Resolve(PointSizeOf(el.State, baseSize), width);
                        continue;
                    case ElementKind.Position:
                        pos = el.Amount.Resolve(PointSizeOf(el.State, baseSize), width);
                        continue;
                    case ElementKind.PageBreak:
                        page = true;
                        continue;
                }
                // U+0003 ends the text: nothing after it is laid out, and as the first character
                // it leaves no characters at all (measured on the shipped package).
                bool end3 = el.Kind == ElementKind.Character && el.Unicode == 3;
                if (end3 && count == 0) { endedAtStart = true; break; }
                ref var p = ref pl[count++];
                Measure(ref el, ref p, baseSize, width);
                p.Element = e;
                p.PreSpace = space;
                p.SetPen = pos;
                p.PageBreak = page;
                space = 0f;
                pos = float.NaN;
                page = false;
                if (end3) break;
            }
            // Kerning: the font's pair adjustments between consecutive glyphs of the same font
            // move the first glyph's advance and placement and the second glyph's placement.
            if (m_enableKerning)
                for (int i = 0; i + 1 < count; i++)
                {
                    ref var a = ref parsed.Elements[pl[i].Element];
                    ref var b = ref parsed.Elements[pl[i + 1].Element];
                    if (a.Kind != ElementKind.Character || b.Kind != ElementKind.Character || a.Element == null || b.Element == null) continue;
                    if (a.Asset != b.Asset || a.Asset is not TMP_FontAsset font || !font.HasPairAdjustments) continue;
                    if (!font.TryGetPairAdjustment(a.Element.glyphIndex, b.Element.glyphIndex, out var pair)) continue;
                    var first = pair.firstAdjustmentRecord.glyphValueRecord;
                    var second = pair.secondAdjustmentRecord.glyphValueRecord;
                    ref var pa = ref pl[i];
                    ref var pb = ref pl[i + 1];
                    pa.Advance += first.xAdvance * pa.Scale * pa.XScale;
                    pa.GlyphAdvance += first.xAdvance * pa.Scale * pa.XScale;
                    pa.MonoOffset += first.xPlacement * pa.Scale * pa.XScale;
                    pa.KernY += first.yPlacement * pa.Scale;
                    pb.Advance += second.xAdvance * pb.Scale * pb.XScale;
                    pb.GlyphAdvance += second.xAdvance * pb.Scale * pb.XScale;
                    pb.MonoOffset += second.xPlacement * pb.Scale * pb.XScale;
                    pb.KernY += second.yPlacement * pb.Scale;
                }

            // Break opportunities: after whitespace but the no-break space U+00A0 (U+2007 and
            // U+202F allow one; measured on the shipped package), and around CJK characters as
            // the line breaking rules allow.
            for (int i = 0; i < count; i++)
            {
                ref var p = ref pl[i];
                if (p.NoBreak) { p.BreakAfter = false; continue; }
                uint c = parsed.Elements[p.Element].Unicode;
                if ((p.Whitespace && c != 0xA0) || c == 0x200B) { p.BreakAfter = true; continue; }
                // A line may break after a hyphen-minus or a soft hyphen (not after the hyphen
                // U+2010 or the non-breaking hyphen U+2011; measured on the shipped package).
                if (c == 0x2D || c == 0xAD) { p.BreakAfter = true; continue; }
                if (i + 1 >= count) continue;
                uint next = parsed.Elements[pl[i + 1].Element].Unicode;
                if ((IsCjk(c) || IsCjk(next)) && !pl[i + 1].NoBreak)
                {
                    // Leading characters (opening brackets) stay with what follows them;
                    // following characters (closing punctuation) with what precedes them.
                    bool keepWithNext = rules != null && rules.leadingCharacters != null && rules.leadingCharacters.Contains(c);
                    bool keepWithPrevious = rules != null && rules.followingCharacters != null && rules.followingCharacters.Contains(next);
                    p.BreakAfter = !keepWithNext && !keepWithPrevious;
                }
            }

            // 2. Lines.
            if (s_Lines.Length < count + 1) Array.Resize(ref s_Lines, Mathf.NextPowerOfTwo(count + 1));
            var lines = s_Lines;
            int lineCount = 0;
            bool fits = true;
            int overflowIndex = -1;
            // Truncate and Ellipsis cut the text at the first visible character whose pen after
            // it passes its line's width: without wrapping, or with wrapping one that is still too
            // wide on the line it wraps to.
            bool cutWide = m_overflowMode == TextOverflowModes.Truncate || m_overflowMode == TextOverflowModes.Ellipsis;
            int horizontalCut = -1;
            int start = 0;
            while (start < count || (lineCount == 0 && count == 0))
            {
                if (count == 0) break;
                ref var line = ref lines[lineCount];
                line = default;
                line.First = start;
                ref var first = ref pl[start];
                line.MarginLeft = first.MarginLeft;
                line.MarginRight = first.MarginRight;
                line.Align = first.Align;
                bool paragraphStart = start == 0 || pl[start - 1].LineBreak;
                line.StartX = first.Indent + (paragraphStart ? first.LineIndent : 0f);
                float lineWidth = width - line.MarginLeft - line.MarginRight;
                if (!float.IsNaN(first.WidthLimit)) lineWidth = Mathf.Min(lineWidth, first.WidthLimit);
                line.Width = lineWidth;
                float x = line.StartX;
                int lastBreak = -1;
                int end = count - 1;
                bool lineFeed = false;
                for (int i = start; i < count; i++)
                {
                    ref var p = ref pl[i];
                    if (i > start && p.PageBreak) { end = i - 1; break; }
                    x += p.PreSpace;
                    if (!float.IsNaN(p.SetPen)) x = line.StartX + p.SetPen;
                    if (p.LineBreak)
                    {
                        p.X = x;
                        end = i;
                        lineFeed = true;
                        break;
                    }
                    if (parsed.Elements[p.Element].Unicode == 13 && parsed.Elements[p.Element].Kind == ElementKind.Character)
                    {
                        // CR keeps the pen where it is; its record still spans its glyph's
                        // advance (measured on the shipped package: "\rA" has its "A" at 0).
                        p.X = x;
                        if (p.BreakAfter) lastBreak = i;
                        continue;
                    }
                    if (p.TabStop > 0f)
                    {
                        // Tabs advance to the next stop, a multiple of the tab interval from the
                        // line's start (from a stop, to the next one); the tab's advance is the
                        // distance to it.
                        float stop = p.TabStop;
                        p.X = x;
                        x = (Mathf.Floor((x - line.StartX) / stop + 0.0001f) + 1f) * stop + line.StartX;
                        p.Advance = p.GlyphAdvance = x - p.X;
                        if (p.BreakAfter) lastBreak = i;
                        continue;
                    }
                    // A visible character that would advance the pen past the line's width
                    // starts a new line: at the last break opportunity, or before it when there
                    // is none.
                    if (wrap && p.Visible && i > start && x + p.GlyphAdvance > lineWidth + 0.0001f)
                    {
                        if (lastBreak >= start)
                        {
                            end = lastBreak;
                            // A soft hyphen where the line breaks is drawn when it fits.
                            ref var last = ref pl[end];
                            if (last.SoftHyphen > 0f && last.X + last.SoftHyphen <= lineWidth + 0.0001f)
                            {
                                last.Visible = true;
                                last.GlyphAdvance = last.Advance = last.SoftHyphen;
                            }
                            break;
                        }
                        end = i - 1;
                        if (m_FittingAutoSize) fits = false;
                        break;
                    }
                    if (cutWide && horizontalCut < 0 && p.Visible && x + p.GlyphAdvance > lineWidth + 0.0001f) horizontalCut = i;
                    p.X = x;
                    x += p.Advance;
                    if (p.BreakAfter) lastBreak = i;
                }
                line.Last = end;
                line.EndsWithLineFeed = lineFeed;
                FinishLine(ref line, pl, baseSize);
                lineCount++;
                start = end + 1;
            }
            if (count == 0)
            {
                lineCount = 0;
                // An empty text still takes a line of its font: it does not fit a rect lower than
                // that, but it does not overflow (measured: an empty label in a 17-unit rect at
                // size 14 reports isTextOverflowing false).
                var face = m_fontAsset.faceInfo;
                float scale = baseSize / face.pointSize * face.scale;
                if ((face.ascentLine - face.descentLine) * scale > height + 0.0001f) fits = false;
            }

            // 3. Vertical placement: baselines relative to the first line's (0).
            float y = 0f;
            int pageIndex = 0;
            float textTop = lineCount > 0 ? lines[0].Ascender : 0f;
            float pageTop = textTop;
            for (int k = 0; k < lineCount; k++)
            {
                ref var line = ref lines[k];
                if (k == 0) y = 0f;
                else
                {
                    ref var prev = ref lines[k - 1];
                    float interval = prev.Gap + line.MaxAscent;
                    if (!float.IsNaN(pl[line.First].LineHeightOverride)) interval = pl[line.First].LineHeightOverride;
                    interval += (m_lineSpacing - m_LineSpacingAdjustment) * 0.01f * baseSize;
                    if (prev.EndsWithLineFeed) interval += m_paragraphSpacing * 0.01f * baseSize;
                    y -= interval;
                    if (m_overflowMode == TextOverflowModes.Page && pl[line.First].PageBreak)
                    {
                        pageIndex++;
                        pageTop = y + line.Ascender;
                    }
                }
                line.Baseline = y;
                line.LineHeight = line.Ascender - line.Descender;
                // Vertical overflow: at the first checked character whose line's descender is
                // below the rect. White space does not overflow (a line of it alone, or before
                // and after the checked characters), though its lines take their height. A line
                // below the rect still does not fit, for auto-size.
                line.Below = (double)textTop - ((double)y + line.FitDescender) > height + 0.0001;
                if (line.Below && m_overflowMode != TextOverflowModes.Page)
                {
                    fits = false;
                    if (overflowIndex < 0)
                        for (int i = line.First; i <= line.Last; i++)
                            if (pl[i].Checked) { overflowIndex = i; break; }
                }
                if (m_overflowMode == TextOverflowModes.Page && k > 0 && (double)pageTop - ((double)y + line.Descender) > height + 0.0001 && !pl[line.First].PageBreak)
                {
                    pageIndex++;
                    pageTop = y + line.Ascender;
                }
                line.Page = pageIndex;
            }
            // A line wider than the rect without wrapping does not fit (for auto-size), but it is
            // not an overflow: isTextOverflowing stays false (measured on the shipped package).
            if (!wrap)
                for (int k = 0; k < lineCount; k++)
                    if (lines[k].Length > lines[k].Width + 0.0001f) fits = false;
            s_LineCount = lineCount;
            m_LayoutOverflowIndex = overflowIndex;
            m_LayoutHorizontalCut = horizontalCut;
            m_LayoutCharacterCount = count;
            m_LayoutEndedAtStart = endedAtStart;
            m_LayoutBaseSize = baseSize;
            return fits;
        }

        int m_LayoutOverflowIndex = -1, m_LayoutHorizontalCut = -1, m_LayoutCharacterCount;
        // The text began with U+0003: it has no characters, but is not empty.
        bool m_LayoutEndedAtStart;
        float m_LayoutBaseSize;

        static bool IsCjk(uint c) =>
            (c >= 0x2E80 && c <= 0x9FFF) || (c >= 0xAC00 && c <= 0xD7AF) || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFF00 && c <= 0xFFEF) || (c >= 0x20000 && c <= 0x2FA1F) || (c >= 0x3000 && c <= 0x303F);

        static void FinishLine(ref LineData line, Placement[] pl, float baseSize)
        {
            float asc = float.NegativeInfinity, desc = float.PositiveInfinity, gap = float.NegativeInfinity, maxAscent = float.NegativeInfinity;
            float fitDesc = float.PositiveInfinity;
            line.FirstVisible = -1;
            line.LastVisible = -1;
            line.LastChecked = -1;
            line.TabStart = line.TabEnd = -1f;
            line.FontLineHeight = 0f;
            line.Visible = line.Spaces = 0;
            // A soft hyphen that is not drawn takes no part in the line's metrics (measured on
            // the shipped package), unless the line has nothing else.
            bool onlySoftHyphens = true;
            for (int i = line.First; i <= line.Last; i++)
                if (pl[i].SoftHyphen <= 0f) { onlySoftHyphens = false; break; }
            for (int i = line.First; i <= line.Last; i++)
            {
                ref var p = ref pl[i];
                if (p.SoftHyphen > 0f && !p.Visible && !onlySoftHyphens) continue;
                if (p.SoftHyphen <= 0f || onlySoftHyphens) fitDesc = Mathf.Min(fitDesc, p.Descender);
                asc = Mathf.Max(asc, p.Ascender);
                desc = Mathf.Min(desc, p.Descender);
                gap = Mathf.Max(gap, p.Gap);
                maxAscent = Mathf.Max(maxAscent, p.Ascender);
                if (p.Checked) line.LastChecked = i;
                if (p.TabStop > 0f)
                {
                    line.TabStart = p.X - line.StartX;
                    line.TabEnd = p.X + p.Advance - line.StartX;
                }
                line.FontLineHeight = Mathf.Max(line.FontLineHeight, p.FontLineHeight);
                if (p.Visible)
                {
                    if (line.FirstVisible < 0) line.FirstVisible = i;
                    line.LastVisible = i;
                    line.Visible++;
                }
                else if (p.Whitespace) line.Spaces++;
            }
            line.Ascender = asc;
            line.Descender = desc;
            line.FitDescender = fitDesc;
            line.Gap = gap;
            line.MaxAscent = maxAscent;
            line.Length = line.LastVisible >= 0 ? pl[line.LastVisible].X + pl[line.LastVisible].GlyphAdvance - line.StartX : 0f;
        }

        float PointSizeOf(TextState state, float baseSize) => state.SizeScale * baseSize + state.SizeOffset;

        // White space as .NET classifies it (the zero-width space is not; it is a break
        // opportunity without width).
        static bool IsWhitespace(uint c) => c < 0x10000 && char.IsWhiteSpace((char)c);

        static bool IsLineBreak(uint c) => c == 10 || c == 11 || c == 0x2028 || c == 0x2029;

        // The size, advance and glyph box of a character or sprite.
        // metricsFont, when given, scales the glyph instead of its own font.
        void Measure(ref ParsedElement el, ref Placement p, float baseSize, float width, TMP_FontAsset metricsFont = null)
        {
            p = default;
            var state = el.State;
            // Small caps draw lower-case letters smaller; the character's size and its line
            // metrics stay those of the text (as the shipped package reports them).
            float fullSize = PointSizeOf(state, baseSize);
            float size = fullSize * el.CaseScale;
            float shift = 0f;
            var font = el.Asset as TMP_FontAsset ?? state.Font ?? m_fontAsset;
            var face = (metricsFont ?? font).faceInfo;
            float fontScale0 = size / face.pointSize * face.scale;
            if ((state.Style & FontStyles.Superscript) != 0)
            {
                shift += face.superscriptOffset * fontScale0;
                size *= face.superscriptSize > 0f ? face.superscriptSize : 1f;
                fullSize *= face.superscriptSize > 0f ? face.superscriptSize : 1f;
            }
            else if ((state.Style & FontStyles.Subscript) != 0)
            {
                shift += face.subscriptOffset * fontScale0;
                size *= face.subscriptSize > 0f ? face.subscriptSize : 1f;
                fullSize *= face.subscriptSize > 0f ? face.subscriptSize : 1f;
            }
            if (!state.VerticalOffset.IsZero) shift += state.VerticalOffset.Resolve(size, 0f);
            float fontScale = size / face.pointSize * face.scale;
            float metricScale = el.CaseScale != 1f ? fullSize / face.pointSize * face.scale : fontScale;
            p.PointSize = fullSize;
            p.FontScale = fontScale;
            p.BaselineShift = shift;
            p.XScale = (1f - m_CharWidthAdjustment) * m_characterHorizontalScale;
            p.Ascender = face.ascentLine * metricScale + shift;
            p.Descender = face.descentLine * metricScale + shift;
            p.Gap = (face.lineHeight - face.ascentLine) * metricScale - shift;
            p.FontLineHeight = face.lineHeight * metricScale;
            p.SetPen = float.NaN;
            p.LineHeightOverride = state.HasLineHeight ? state.LineHeight.Resolve(size, face.lineHeight * fontScale) : float.NaN;
            p.WidthLimit = state.HasWidth ? state.Width.Resolve(size, width) : float.NaN;
            p.MarginLeft = state.MarginLeft.Resolve(size, width);
            p.MarginRight = state.MarginRight.Resolve(size, width);
            p.Indent = state.Indent.Resolve(size, width);
            p.LineIndent = state.LineIndent.Resolve(size, width);
            p.Align = state.Align != 0 ? state.Align : m_HorizontalAlignment;
            p.NoBreak = state.NoBreak;
            p.Italic = (state.Style & FontStyles.Italic) != 0;
            uint c = el.Unicode;
            p.LineBreak = el.Kind == ElementKind.Character && IsLineBreak(c);
            p.Whitespace = el.Kind == ElementKind.Character && (IsWhitespace(c) || p.LineBreak || c == 13 || c == 3);
            // White space is not checked; a tab is. The zero-width space is checked only where
            // it is drawn (below).
            p.Checked = !p.Whitespace || (el.Kind == ElementKind.Character && c == 9);
            // The spacing properties are in hundredths of the text's font size, whatever size
            // tags say (measured on the shipped package).
            float em = baseSize * 0.01f;
            if (p.LineBreak || c == 3) return;

            float advance, spacing = 0f;
            if (el.Kind == ElementKind.Sprite)
            {
                var sprite = (TMP_SpriteCharacter)el.Element;
                var glyph = sprite.glyph;
                var sa = (TMP_SpriteAsset)el.Asset;
                var sface = sa.faceInfo;
                // A sprite asset without face metrics is sized to the font: its glyphs' height
                // in sheet pixels maps to the font's ascender line.
                float spriteScale = sface.pointSize > 0f
                    ? size / sface.pointSize * sface.scale
                    : fontScale * (face.ascentLine / (glyph != null && glyph.metrics.height > 0f ? glyph.metrics.height : face.ascentLine));
                float s = spriteScale * sprite.scale * (glyph?.scale ?? 1f);
                p.Scale = s;
                var gm = glyph?.metrics ?? default;
                p.Left = gm.horizontalBearingX * s * p.XScale;
                p.Right = (gm.horizontalBearingX + gm.width) * s * p.XScale;
                p.Top = gm.horizontalBearingY * s + shift;
                p.Bottom = (gm.horizontalBearingY - gm.height) * s + shift;
                advance = gm.horizontalAdvance * s * p.XScale;
                p.Visible = true;
            }
            else
            {
                var character = el.Element;
                var glyph = character?.glyph;
                float s = fontScale * (character?.scale ?? 1f) * (glyph?.scale ?? 1f);
                p.Scale = s;
                var gm = glyph?.metrics ?? default;
                p.Bold = (state.Style & FontStyles.Bold) != 0 && !el.AlternateTypeface || (state.Weight >= FontWeight.Bold && !el.AlternateTypeface);
                p.Left = gm.horizontalBearingX * s * p.XScale;
                p.Right = (gm.horizontalBearingX + gm.width) * s * p.XScale;
                p.Top = gm.horizontalBearingY * s + shift;
                p.Bottom = (gm.horizontalBearingY - gm.height) * s + shift;
                advance = gm.horizontalAdvance * s * p.XScale;
                // Every character that is not white space draws its glyph's quad; the zero-width
                // space only where white space is preserved (measured on the shipped package).
                p.Visible = !p.Whitespace && glyph != null && (c != 0x200B || m_TextWrappingMode == TextWrappingModes.PreserveWhitespace || m_TextWrappingMode == TextWrappingModes.PreserveWhitespaceNoWrap);
                float styleSpacing = p.Bold ? font.boldSpacing : font.normalSpacingOffset;
                spacing = (m_characterSpacing + styleSpacing) * em;
                if (c == 32 || c == 0xA0) spacing += m_wordSpacing * em;
                if (c == 9 && el.Kind == ElementKind.Character)
                {
                    // T = tabSize x the face's tabWidth, at the size (measured on the shipped
                    // package: 364.58333 for NotoSans 14 pt, 300 for GRAYSTROKE 12 pt).
                    var tabFace = font.faceInfo;
                    advance = font.tabSize * tabFace.tabWidth * (size / tabFace.pointSize * tabFace.scale);
                    p.TabStop = advance;
                    spacing = 0f;
                }
            }
            if (!state.CharacterSpacing.IsZero) spacing += state.CharacterSpacing.Resolve(size, 0f);
            if (!state.Monospace.IsZero)
            {
                float mono = state.Monospace.Resolve(size, 0f);
                p.MonoOffset = (mono - advance) * 0.5f;
                advance = mono;
            }
            p.GlyphAdvance = advance;
            p.Advance = advance + spacing;
            // The zero-width space is not checked against the height where it is not drawn
            // (measured on the shipped package); where white space is preserved it is drawn, and
            // checked as a visible character (an empty input field's text, U+200B alone, is one
            // line high).
            if (c == 0x200B && el.Kind == ElementKind.Character) p.Checked = p.Visible;
            if (c == 0xAD && el.Kind == ElementKind.Character && p.Visible)
            {
                p.SoftHyphen = advance;
                p.Visible = false;
                p.GlyphAdvance = p.Advance = 0f;
            }
        }
    }
}
