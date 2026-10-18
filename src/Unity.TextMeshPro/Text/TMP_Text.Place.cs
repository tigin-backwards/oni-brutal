using System;
using UnityEngine;
using UnityEngine.TextCore;

namespace TMPro
{
    // Placement: the laid-out lines aligned in the rect, and the character, line, word, link and
    // page records of textInfo.
    public abstract partial class TMP_Text
    {
        // Where the visible part ends (the overflow modes and the visibility limits).
        int m_FirstHiddenLine = int.MaxValue;

        void Place(Rect rect)
        {
            var info = textInfo;
            var parsed = m_Parsed;
            var pl = s_Placements;
            var lines = s_Lines;
            int count = m_LayoutCharacterCount;
            int lineCount = s_LineCount;
            info.Clear();
            info.EnsureCharacters(count + 2);
            info.EnsureLines(lineCount + 1);
            info.characterCount = count;
            info.lineCount = lineCount;
            info.materialCount = parsed.Materials.Count;
            m_firstOverflowCharacterIndex = m_LayoutOverflowIndex;
            m_IsTextTruncated = false;

            // Which lines are shown: the page on display, and none after an overflow that hides.
            int overflowLine = -1;
            if (m_LayoutOverflowIndex >= 0)
                for (int k = 0; k < lineCount; k++)
                    if (lines[k].First <= m_LayoutOverflowIndex && m_LayoutOverflowIndex <= lines[k].Last) { overflowLine = k; break; }
            int pages = lineCount == 0 ? 1 : lines[lineCount - 1].Page + 1;
            int shownPage = m_overflowMode == TextOverflowModes.Page ? Mathf.Clamp(m_pageToDisplay - 1, 0, pages - 1) : -1;
            m_FirstHiddenLine = lineCount;
            bool hidesOverflow = m_overflowMode == TextOverflowModes.Truncate || m_overflowMode == TextOverflowModes.Ellipsis || m_overflowMode == TextOverflowModes.Linked;
            if (hidesOverflow && overflowLine >= 0)
            {
                // The first line is shown even when it alone overflows.
                m_FirstHiddenLine = Mathf.Max(overflowLine, 1);
                if (overflowLine == 0 && lineCount > 0 && pl[lines[0].First].Visible == false) m_FirstHiddenLine = 1;
            }
            if (m_MaxVisibleLines < m_FirstHiddenLine) m_FirstHiddenLine = Mathf.Max(0, m_MaxVisibleLines);

            int firstLine = 0, lastLine = Mathf.Min(lineCount, m_FirstHiddenLine) - 1;
            if (shownPage >= 0)
            {
                firstLine = -1;
                for (int k = 0; k < lineCount; k++)
                    if (lines[k].Page == shownPage) { if (firstLine < 0) firstLine = k; lastLine = k; }
                if (firstLine < 0) firstLine = 0;
            }

            // Vertical alignment of the shown lines.
            float top = rect.yMax - m_margin.y, bottom = rect.yMin + m_margin.w;
            float offsetY = 0f;
            if (lineCount > 0 && lastLine >= firstLine)
            {
                float textTop = lines[firstLine].Baseline + lines[firstLine].Ascender;
                float textBottom = lines[lastLine].Baseline + lines[lastLine].Descender;
                switch (m_VerticalAlignment)
                {
                    case VerticalAlignmentOptions.Top: offsetY = top - textTop; break;
                    case VerticalAlignmentOptions.Middle: offsetY = (top + bottom) * 0.5f - (textTop + textBottom) * 0.5f; break;
                    case VerticalAlignmentOptions.Bottom: offsetY = bottom - textBottom; break;
                    case VerticalAlignmentOptions.Baseline: offsetY = (top + bottom) * 0.5f - lines[firstLine].Baseline; break;
                    case VerticalAlignmentOptions.Geometry:
                    {
                        GlyphExtentsY(firstLine, lastLine, out float gTop, out float gBottom);
                        offsetY = (top + bottom) * 0.5f - (gTop + gBottom) * 0.5f;
                        break;
                    }
                    case VerticalAlignmentOptions.Capline:
                    {
                        float cap = CapLineOf(firstLine);
                        offsetY = (top + bottom) * 0.5f - (lines[firstLine].Baseline + cap * 0.5f);
                        break;
                    }
                    default: offsetY = top - textTop; break;
                }
            }

            if (m_overflowMode == TextOverflowModes.Ellipsis && m_LayoutOverflowIndex >= 0 && lineCount > 0)
            {
                int last = Mathf.Clamp(m_FirstHiddenLine - 1, 0, lineCount - 1);
                InsertEllipsis(last, ref count, ref lineCount);
                info.EnsureCharacters(count + 1);
                info.characterCount = count;
                info.lineCount = lineCount;
                m_FirstHiddenLine = lineCount;
            }

            // Right-to-left lines mirror their pen positions.
            if (m_isRightToLeft)
                for (int k = 0; k < lineCount; k++)
                {
                    ref var line = ref lines[k];
                    float end = line.StartX + line.Length;
                    for (int i = line.First; i <= line.Last; i++)
                    {
                        ref var p = ref pl[i];
                        p.X = line.StartX + end - (p.X + p.GlyphAdvance);
                    }
                }

            // Horizontal alignment per line.
            float left = rect.xMin + m_margin.x;
            for (int k = 0; k < lineCount; k++)
            {
                ref var line = ref lines[k];
                // Lines align in their width plus 0.0001 (the tolerance that lets a line fill its
                // width exactly).
                float available = line.Width + 0.0001f;
                float used = line.StartX + line.Length;
                var align = line.Align;
                bool lastOfParagraph = line.EndsWithLineFeed || k == lineCount - 1;
                float extra = available - used;
                line.OffsetX = 0f;
                switch (align)
                {
                    case HorizontalAlignmentOptions.Center: line.OffsetX = extra * 0.5f; break;
                    case HorizontalAlignmentOptions.Right: line.OffsetX = extra; break;
                    case HorizontalAlignmentOptions.Geometry:
                    {
                        GlyphExtentsX(k, out float gl, out float gr);
                        line.OffsetX = available * 0.5f - (gl + gr) * 0.5f;
                        break;
                    }
                    case HorizontalAlignmentOptions.Justified:
                    case HorizontalAlignmentOptions.Flush:
                        if ((align == HorizontalAlignmentOptions.Flush || !lastOfParagraph) && extra > 0f) Justify(ref line, extra);
                        break;
                }
            }

            // Records.
            var chars = info.characterInfo;
            m_TextExtents = Extents.uninitialized;
            int visibleSeen = 0;
            for (int k = 0; k < lineCount; k++)
            {
                ref var line = ref lines[k];
                float baseline = offsetY + line.Baseline;
                float lineLeft = left + line.MarginLeft + line.OffsetX;
                bool shown = k >= firstLine && k <= lastLine;
                var extents = Extents.uninitialized;
                int visibleOnLine = 0, spacesOnLine = 0;
                for (int i = line.First; i <= line.Last && i < count; i++)
                {
                    ref var p = ref pl[i];
                    ref var el = ref parsed.Elements[p.Element];
                    ref var ci = ref chars[i];
                    ci = default;
                    ci.elementType = el.Kind == ElementKind.Sprite ? TMP_TextElementType.Sprite : TMP_TextElementType.Character;
                    ci.character = (char)el.Unicode;
                    ci.index = el.SourceIndex;
                    ci.stringLength = el.SourceLength;
                    ci.textElement = el.Element;
                    ci.fontAsset = el.Asset as TMP_FontAsset ?? el.State.Font ?? m_fontAsset;
                    ci.material = parsed.Materials[el.MaterialIndex].Material;
                    ci.materialReferenceIndex = el.MaterialIndex;
                    ci.isUsingAlternateTypeface = el.AlternateTypeface;
                    ci.pointSize = p.PointSize;
                    ci.lineNumber = k;
                    ci.pageNumber = line.Page;
                    ci.scale = p.Scale;
                    ci.style = el.State.Style;
                    float origin = (float)((double)lineLeft + p.X + p.JustifyOffset);
                    ci.origin = origin;
                    ci.xAdvance = origin + p.GlyphAdvance;
                    ci.baseLine = baseline + p.BaselineShift;
                    ci.ascender = baseline + p.Ascender;
                    ci.descender = baseline + p.Descender;
                    float x0 = origin + p.MonoOffset + p.Left, x1 = origin + p.MonoOffset + p.Right;
                    float y1 = baseline + p.Top + p.KernY, y0 = baseline + p.Bottom + p.KernY;
                    float shear = p.Italic && !el.AlternateTypeface ? ItalicShear(ci.fontAsset) : 0f;
                    ci.bottomLeft = new Vector3(x0 + shear * (p.Bottom - p.BaselineShift), y0, 0f);
                    ci.topLeft = new Vector3(x0 + shear * (p.Top - p.BaselineShift), y1, 0f);
                    ci.topRight = new Vector3(x1 + shear * (p.Top - p.BaselineShift), y1, 0f);
                    ci.bottomRight = new Vector3(x1 + shear * (p.Bottom - p.BaselineShift), y0, 0f);
                    ci.aspectRatio = y1 - y0 != 0f ? (x1 - x0) / (y1 - y0) : 0f;
                    ci.color = CharacterColor(ref el);
                    ci.underlineColor = Opaque(el.State.UnderlineColor, ci.color);
                    ci.strikethroughColor = Opaque(el.State.StrikethroughColor, ci.color);
                    ci.highlightState = el.State.Highlight;
                    ci.highlightColor = el.State.Highlight.color;
                    ci.vertexIndex = -1;
                    bool visible = p.Visible && shown && IsShown(i, k, visibleSeen);
                    if (p.Visible) visibleSeen++;
                    ci.isVisible = visible;
                    if (p.Visible)
                    {
                        visibleOnLine++;
                        extents.min.x = Mathf.Min(extents.min.x, x0);
                        extents.min.y = Mathf.Min(extents.min.y, y0);
                        extents.max.x = Mathf.Max(extents.max.x, x1);
                        extents.max.y = Mathf.Max(extents.max.y, y1);
                    }
                    // Line feeds count as spaces.
                    if (p.Whitespace) spacesOnLine++;
                    if (el.Kind == ElementKind.Sprite) info.spriteCount++;
                    if (p.Whitespace) info.spaceCount++;
                    if (visible)
                    {
                        m_TextExtents.min.x = Mathf.Min(m_TextExtents.min.x, x0);
                        m_TextExtents.min.y = Mathf.Min(m_TextExtents.min.y, y0);
                        m_TextExtents.max.x = Mathf.Max(m_TextExtents.max.x, x1);
                        m_TextExtents.max.y = Mathf.Max(m_TextExtents.max.y, y1);
                    }
                }
                ref var li = ref info.lineInfo[k];
                li = default;
                li.firstCharacterIndex = line.First;
                li.lastCharacterIndex = line.Last;
                li.characterCount = line.Last - line.First + 1;
                li.firstVisibleCharacterIndex = line.FirstVisible >= 0 ? line.FirstVisible : line.First;
                li.lastVisibleCharacterIndex = line.LastVisible >= 0 ? line.LastVisible : line.First;
                li.visibleCharacterCount = visibleOnLine;
                li.spaceCount = spacesOnLine;
                li.visibleSpaceCount = spacesOnLine;
                li.ascender = baseline + line.Ascender;
                li.baseline = baseline;
                li.descender = baseline + line.Descender;
                li.lineHeight = line.Ascender - line.Descender + (k > 0 ? 0f : 0f);
                li.length = line.Length;
                li.maxAdvance = line.LastVisible >= 0 ? chars[line.LastVisible].xAdvance - lineLeft : 0f;
                li.width = line.Width;
                li.marginLeft = line.MarginLeft;
                li.marginRight = line.MarginRight;
                li.alignment = line.Align;
                li.lineExtents = extents.max.x < extents.min.x ? Extents.zero : extents;
            }
            if (m_TextExtents.max.x < m_TextExtents.min.x) m_TextExtents = Extents.zero;

            if (m_FirstHiddenLine < lineCount) m_IsTextTruncated = true;

            RecordWords(count);
            RecordLinks(count);
            RecordPages(lineCount, pages);

            if (m_overflowMode == TextOverflowModes.Linked && m_linkedTextComponent != null)
            {
                int next = m_FirstHiddenLine < lineCount ? lines[m_FirstHiddenLine].First : count;
                var linked = m_linkedTextComponent;
                if (linked.text != m_text) linked.text = m_text;
                linked.firstVisibleCharacter = next;
            }
        }

        // Whether character i (on line k, the n-th visible one) is within the visibility limits.
        bool IsShown(int i, int line, int visibleIndex)
        {
            if (i < m_firstVisibleCharacter) return false;
            if (visibleIndex >= m_MaxVisibleCharacters) return false;
            if (line >= m_MaxVisibleLines) return false;
            return true;
        }

        static Color32 Opaque(Color32 tag, Color32 fallback) => tag.a == 0 && tag.r == 0 && tag.g == 0 && tag.b == 0 ? fallback : tag;

        // The vertex colour of a character: the text's colour or a colour tag, with <alpha>,
        // and for sprites white unless tinted.
        Color32 CharacterColor(ref ParsedElement el)
        {
            var state = el.State;
            Color32 c = m_overrideHtmlColors || !state.ColorFromTag ? m_fontColor32 : state.Color;
            if (!m_overrideHtmlColors && state.ColorFromTag)
            {
                // A tag's colour keeps the text's alpha unless the tag gives one.
                c = state.Color;
            }
            if (state.HasAlphaOverride) c.a = state.AlphaOverride;
            if (el.Kind == ElementKind.Sprite)
            {
                if (el.HasSpriteColor) return el.SpriteColor;
                if (!(m_tintAllSprites || el.SpriteTint)) return new Color32(255, 255, 255, c.a);
            }
            return c;
        }

        float ItalicShear(TMP_FontAsset font) => (font != null ? font.italicStyle : 35) * 0.01f;

        void Justify(ref LineData line, float extra)
        {
            var pl = s_Placements;
            int spaces = 0, glyphs = 0;
            for (int i = line.First; i <= line.LastVisible && i >= 0; i++)
            {
                if (pl[i].Whitespace && !pl[i].LineBreak) spaces++;
                else if (pl[i].Visible) glyphs++;
            }
            float ratio = m_wordWrappingRatios;
            float perSpace = 0f, perGlyph = 0f;
            if (spaces > 0)
            {
                perSpace = extra * (1f - ratio) / spaces;
                perGlyph = glyphs > 1 ? extra * ratio / (glyphs - 1) : 0f;
                if (glyphs <= 1) perSpace = extra / spaces;
            }
            else if (glyphs > 1) perGlyph = extra / (glyphs - 1);
            float acc = 0f;
            bool firstGlyph = true;
            for (int i = line.First; i <= line.Last; i++)
            {
                ref var p = ref pl[i];
                if (p.Visible)
                {
                    if (!firstGlyph) acc += perGlyph;
                    firstGlyph = false;
                }
                p.JustifyOffset = acc;
                if (p.Whitespace && !p.LineBreak && i < line.LastVisible) acc += perSpace;
            }
        }

        void GlyphExtentsY(int firstLine, int lastLine, out float top, out float bottom)
        {
            var pl = s_Placements;
            var lines = s_Lines;
            top = float.NegativeInfinity;
            bottom = float.PositiveInfinity;
            for (int k = firstLine; k <= lastLine; k++)
                for (int i = lines[k].First; i <= lines[k].Last; i++)
                {
                    if (!pl[i].Visible) continue;
                    top = Mathf.Max(top, lines[k].Baseline + pl[i].Top);
                    bottom = Mathf.Min(bottom, lines[k].Baseline + pl[i].Bottom);
                }
            if (float.IsInfinity(top))
            {
                top = lines[firstLine].Baseline + lines[firstLine].Ascender;
                bottom = lines[lastLine].Baseline + lines[lastLine].Descender;
            }
        }

        void GlyphExtentsX(int lineIndex, out float left, out float right)
        {
            var pl = s_Placements;
            ref var line = ref s_Lines[lineIndex];
            left = float.PositiveInfinity;
            right = float.NegativeInfinity;
            for (int i = line.First; i <= line.Last; i++)
            {
                if (!pl[i].Visible) continue;
                left = Mathf.Min(left, pl[i].X + pl[i].MonoOffset + pl[i].Left);
                right = Mathf.Max(right, pl[i].X + pl[i].MonoOffset + pl[i].Right);
            }
            if (float.IsInfinity(left)) left = right = 0f;
        }

        float CapLineOf(int lineIndex)
        {
            var pl = s_Placements;
            ref var line = ref s_Lines[lineIndex];
            float cap = 0f;
            for (int i = line.First; i <= line.Last; i++)
            {
                var font = m_Parsed.Elements[pl[i].Element].Asset as TMP_FontAsset ?? m_fontAsset;
                cap = Mathf.Max(cap, font.faceInfo.capLine * pl[i].FontScale);
            }
            return cap;
        }

        // Text that does not fit ends in "…" (U+2026) on its last shown line: the ellipsis
        // follows the last character after which it fits, and the text ends there.
        void InsertEllipsis(int lineIndex, ref int count, ref int lineCount)
        {
            var parsed = m_Parsed;
            if (parsed.Ellipsis.Element == null) return;
            ref var line = ref s_Lines[lineIndex];
            var pl = s_Placements;
            int index = parsed.Count;
            if (parsed.Elements.Length <= index) System.Array.Resize(ref parsed.Elements, index + 1);
            parsed.Elements[index] = parsed.Ellipsis;
            var ellipsis = default(Placement);
            Measure(ref parsed.Elements[index], ref ellipsis, m_LayoutBaseSize, line.Width);
            ellipsis.Element = index;
            int keep = Mathf.Min(line.Last, count - 1);
            while (keep >= line.First && (!pl[keep].Visible || pl[keep].X + pl[keep].Advance + ellipsis.GlyphAdvance > line.Width + 0.0001f)) keep--;
            int at = keep + 1;
            if (s_Placements.Length <= at) System.Array.Resize(ref s_Placements, at + 1);
            pl = s_Placements;
            ellipsis.X = keep >= line.First ? pl[keep].X + pl[keep].Advance : line.StartX;
            pl[at] = ellipsis;
            line.Last = at;
            FinishLine(ref line, pl, m_LayoutBaseSize);
            count = at + 1;
            lineCount = lineIndex + 1;
            m_IsTextTruncated = true;
        }

        // Letters, digits and symbols make words; apostrophes and hyphens join word characters.
        static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || char.IsSymbol(c);

        static bool IsJoiner(char c) => c == '\'' || c == '\u2019' || c == '-';

        void RecordWords(int count)
        {
            var info = textInfo;
            var chars = info.characterInfo;
            int words = 0;
            int start = -1;
            for (int i = 0; i <= count; i++)
            {
                bool word = i < count && (IsWordCharacter(chars[i].character) || (IsJoiner(chars[i].character) && start >= 0 && i + 1 < count && IsWordCharacter(chars[i + 1].character)));
                if (word && chars[i].elementType == TMP_TextElementType.Sprite) word = false;
                if (word && start < 0) start = i;
                if (!word && start >= 0)
                {
                    info.EnsureWords(words + 1);
                    info.wordInfo[words] = new TMP_WordInfo { textComponent = this, firstCharacterIndex = start, lastCharacterIndex = i - 1, characterCount = i - start };
                    words++;
                    start = -1;
                }
            }
            // A text without word characters is one word.
            if (words == 0 && count > 0)
            {
                info.EnsureWords(1);
                info.wordInfo[0] = new TMP_WordInfo { textComponent = this, firstCharacterIndex = 0, lastCharacterIndex = count - 1, characterCount = count };
                words = 1;
            }
            info.wordCount = words;
            var lines = s_Lines;
            for (int k = 0; k < info.lineCount; k++)
            {
                int w = 0;
                for (int j = 0; j < words; j++)
                    if (info.wordInfo[j].firstCharacterIndex >= lines[k].First && info.wordInfo[j].firstCharacterIndex <= lines[k].Last) w++;
                info.lineInfo[k].wordCount = w;
            }
        }

        void RecordLinks(int count)
        {
            var info = textInfo;
            var links = m_Parsed.Links;
            info.linkCount = 0;
            if (links.Count == 0) return;
            info.EnsureLinks(links.Count);
            var chars = info.characterInfo;
            for (int l = 0; l < links.Count; l++)
            {
                int first = -1, last = -1;
                for (int i = 0; i < count; i++)
                {
                    if (m_Parsed.Elements[s_Placements[i].Element].State.Link != l) continue;
                    if (first < 0) first = i;
                    last = i;
                }
                ref var li = ref info.linkInfo[l];
                li = default;
                li.textComponent = this;
                li.hashCode = TextHash.Of(links[l].Id);
                li.linkIdFirstCharacterIndex = links[l].IdSourceIndex;
                li.linkIdLength = links[l].IdLength;
                li.linkTextfirstCharacterIndex = first < 0 ? 0 : first;
                li.linkTextLength = first < 0 ? 0 : last - first + 1;
            }
            info.linkCount = links.Count;
        }

        // The ID of the parsed link whose tag starts at that source index.
        internal string LinkId(int idSourceIndex, int hash)
        {
            string fallback = null;
            foreach (var link in m_Parsed.Links)
            {
                if (link.IdSourceIndex != idSourceIndex) continue;
                if (TextHash.Of(link.Id) == hash) return link.Id;
                fallback ??= link.Id;
            }
            return fallback ?? "";
        }

        void RecordPages(int lineCount, int pages)
        {
            var info = textInfo;
            var lines = s_Lines;
            info.EnsurePages(pages);
            info.pageCount = lineCount == 0 ? 0 : pages;
            for (int pg = 0; pg < pages && lineCount > 0; pg++)
            {
                int first = -1, last = -1;
                for (int k = 0; k < lineCount; k++)
                    if (lines[k].Page == pg) { if (first < 0) first = k; last = k; }
                if (first < 0) continue;
                info.pageInfo[pg] = new TMP_PageInfo
                {
                    firstCharacterIndex = lines[first].First,
                    lastCharacterIndex = lines[last].Last,
                    ascender = info.lineInfo[first].ascender,
                    baseLine = info.lineInfo[first].baseline,
                    descender = info.lineInfo[last].descender,
                };
            }
        }
    }
}
