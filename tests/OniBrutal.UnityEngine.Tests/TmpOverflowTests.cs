#pragma warning disable CS0618 // enableKerning, as the game's SetTextStyleSetting sets it
using System.Linq;
using TMPro;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    // Overflow at white space (docs/cleanroom/textmeshpro.md, "Overflow at white space"): which
    // characters are checked against the height, the overflow index, the cuts of Truncate and
    // Ellipsis at the width and at the height, and the preferred height. Labels are set up as the
    // game's SetTextStyleSetting does (kerning, extra padding), centred, as the measurements were.
    public class TmpOverflowTests : EngineTest
    {
        const string Noto = "NotoSans-Regular";
        const string Graystroke = "GRAYSTROKE REGULAR SDF";

        static TextMeshProUGUI Label(string text, float w, float h, TextOverflowModes mode, bool wrap = false, string font = Noto, float size = 14f)
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform));
            canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("Ov", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.enableKerning = true;
            t.extraPadding = true;
            t.fontSize = size;
            t.font = TmpTestSupport.FindAsset<TMP_FontAsset>(font);
            t.alignment = TextAlignmentOptions.Center;
            t.overflowMode = mode;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.text = text;
            t.ForceMeshUpdate(true);
            return t;
        }

        // Each character as the probe logs it: code, visible (v) or not (-), line.
        static string Chars(TMP_Text t)
        {
            var info = t.textInfo;
            return string.Join(" ", Enumerable.Range(0, info.characterCount).Select(i =>
            {
                var c = info.characterInfo[i];
                return $"{(int)c.character}:{(c.isVisible ? "v" : "-")}{c.lineNumber}";
            }));
        }

        static void Check(TMP_Text t, bool overflowing, int index, string chars, bool? truncated = null, int? lines = null)
        {
            Assert.Equal(overflowing, t.isTextOverflowing);
            Assert.Equal(index, t.firstOverflowCharacterIndex);
            Assert.Equal(chars, Chars(t));
            if (truncated is bool tr) Assert.Equal(tr, t.isTextTruncated);
            if (lines is int lc) Assert.Equal(lc, t.textInfo.lineCount);
        }

        // The verification table of the section, NotoSans-Regular 14 pt unless named.

        [Fact]
        public void AOneSpaceLabelInItsPreferredSizeDoesNotOverflow()
        {
            // Butcher & Fishing Stations: " " in its preferred size, 0.01 x 0.01.
            var t = Label(" ", 400f, 100f, TextOverflowModes.Overflow);
            Assert.Equal(0.01f, t.preferredWidth);
            Assert.Equal(0.01f, t.preferredHeight);
            t = Label(" ", 0.01f, 0.01f, TextOverflowModes.Overflow);
            Check(t, false, -1, "32:-0", truncated: false, lines: 1);
        }

        [Theory]
        [InlineData(" ", TextOverflowModes.Truncate, false, "32:-0")]
        [InlineData("\u00A0", TextOverflowModes.Ellipsis, false, "160:-0")]
        [InlineData(" \n ", TextOverflowModes.Overflow, true, "32:-0 10:-0 32:-1")]
        [InlineData("  ", TextOverflowModes.Masking, false, "32:-0 32:-0")]
        [InlineData("\n", TextOverflowModes.Truncate, true, "10:-0")]
        public void WhiteSpaceAloneNeverOverflowsAndKeepsItsCharacters(string text, TextOverflowModes mode, bool wrap, string chars)
        {
            var t = Label(text, 0f, 0f, mode, wrap);
            Check(t, false, -1, chars, truncated: false);
        }

        [Fact]
        public void ExactlyAtTheHeightFits()
        {
            // The first line ends at 19.03125.
            Check(Label("A", 400f, 19.03f, TextOverflowModes.Overflow), true, 0, "65:v0");
            Check(Label("A", 400f, 19.03125f, TextOverflowModes.Overflow), false, -1, "65:v0");
        }

        [Fact]
        public void ATabIsCheckedButNotVisible()
        {
            Check(Label("\t", 400f, 19.03125f, TextOverflowModes.Overflow), false, -1, "9:-0");
            Check(Label("\t", 400f, 19.03f, TextOverflowModes.Overflow), true, 0, "9:-0");
            var t = Label("\t", 400f, 100f, TextOverflowModes.Overflow);
            Assert.Equal(0.01f, t.preferredWidth);
            Assert.Equal(19.04f, t.preferredHeight);
            // A tab is never cut at the width.
            Check(Label("\t", 0f, 100f, TextOverflowModes.Truncate), false, -1, "9:-0", truncated: false);
        }

        [Fact]
        public void WhiteSpaceBeforeTheFirstCheckedCharacterDoesNotOverflow()
        {
            Check(Label(" A", 400f, 0f, TextOverflowModes.Overflow), true, 1, "32:-0 65:v0");
            Check(Label(" A", 400f, 0f, TextOverflowModes.Masking), true, 1, "32:-0 65:v0");
            Check(Label("\nA", 400f, 0f, TextOverflowModes.Overflow), true, 1, "10:-0 65:v1");
            Check(Label(" \n\nA", 400f, 0f, TextOverflowModes.Overflow), true, 3, "32:-0 10:-0 10:-1 65:v2");
        }

        [Fact]
        public void LinesOfWhiteSpaceAfterTheLastCheckedCharacterDoNotOverflow()
        {
            Check(Label("A\n ", 400f, 19.03125f, TextOverflowModes.Overflow), false, -1, "65:v0 10:-0 32:-1");
            Check(Label("A \n ", 400f, 19.03125f, TextOverflowModes.Truncate), false, -1, "65:v0 32:-0 10:-0 32:-1", truncated: false);
        }

        [Fact]
        public void LinesOfWhiteSpaceTakeTheirHeight()
        {
            // The "A" of " \nA" ends at 38.098953, of " \n\nA" at 57.16666.
            Assert.False(Label(" \nA", 400f, 38.1f, TextOverflowModes.Overflow).isTextOverflowing);
            Assert.Equal(1 + 1, Label(" \nA", 400f, 38.09f, TextOverflowModes.Overflow).firstOverflowCharacterIndex);
            Assert.False(Label(" \n\nA", 400f, 57.17f, TextOverflowModes.Overflow).isTextOverflowing);
            Assert.Equal(3, Label(" \n\nA", 400f, 57.16f, TextOverflowModes.Overflow).firstOverflowCharacterIndex);
        }

        [Fact]
        public void TruncateKeepsWhatPrecedesTheCutAndPutsTheRecordInItsPlace()
        {
            Check(Label(" A", 400f, 0f, TextOverflowModes.Truncate), true, 1, "32:-0 3:-0", truncated: true, lines: 1);
            // After a line feed the record is on the cut character's line.
            Check(Label("\nA", 400f, 0f, TextOverflowModes.Truncate), true, 1, "10:-0 3:-1", truncated: true, lines: 2);
            // At the first character nothing is shown.
            var t = Label("A", 400f, 0f, TextOverflowModes.Truncate);
            Check(t, true, 0, "", truncated: true, lines: 0);
        }

        [Fact]
        public void TheRecordOfALineThatBeganByWrappingStaysOnTheLineBefore()
        {
            Check(Label("A B", 21.69f, 19.04f, TextOverflowModes.Truncate, wrap: true), true, 2, "65:v0 32:-0 3:-0", truncated: true, lines: 1);
            Check(Label("A B", 21.69f, 19.04f, TextOverflowModes.Ellipsis, wrap: true), true, 2, "65:v0 8230:v0 3:-0", truncated: true, lines: 1);
        }

        [Fact]
        public void EllipsisGoesWhereItFitsTheHeightAndTheWidth()
        {
            // At the cut, " A" at height 0: line 0 does not fit, nothing is shown.
            Check(Label(" A", 400f, 0f, TextOverflowModes.Ellipsis), true, 1, "", truncated: true, lines: 0);
            // 0 wide: "…" does not fit at the space's pen 0.
            Check(Label(" A", 0f, 100f, TextOverflowModes.Ellipsis), false, -1, "", truncated: true, lines: 0);
            // 12.57 wide: "…" (11.083328) in place of the space.
            Check(Label(" A", 12.57f, 100f, TextOverflowModes.Ellipsis), false, -1, "8230:v0 3:-0", truncated: true, lines: 1);
            Check(Label("  A  ", 16.22f, 100f, TextOverflowModes.Ellipsis), false, -1, "32:-0 8230:v0 3:-0", truncated: true, lines: 1);
            // In place of the line feed before the overflowing line.
            Check(Label("\nA", 400f, 38.09f, TextOverflowModes.Ellipsis), true, 1, "8230:v0 3:-0", truncated: true, lines: 1);
            // The second line does not fit: in place of the first line feed.
            Check(Label(" \n\nA", 400f, 19.03125f, TextOverflowModes.Ellipsis), true, 3, "32:-0 8230:v0 3:-0", truncated: true, lines: 1);
            // GRAYSTROKE 12 pt: at the line feed's pen (2.28) "…" (7.078125) does not fit 7.28;
            // at the space's it does.
            Check(Label(" \nA", 7.279f, 12.592742f, TextOverflowModes.Ellipsis, font: Graystroke, size: 12f), true, 2, "8230:v0 3:-0", truncated: true, lines: 1);
        }

        [Fact]
        public void WithWrappingACharacterTooWideForAnyLineIsCut()
        {
            Check(Label("A", 0f, 100f, TextOverflowModes.Truncate, wrap: true), false, -1, "", truncated: true, lines: 0);
            Check(Label("A", 0f, 100f, TextOverflowModes.Overflow, wrap: true), false, -1, "65:v0", truncated: false);
            Check(Label(" A", 0f, 100f, TextOverflowModes.Truncate, wrap: true), false, -1, "32:-0 3:-0", truncated: true, lines: 1);
        }

        [Fact]
        public void TheCutAtTheWidthIsNotAnOverflow()
        {
            Check(Label("AB", 9f, 100f, TextOverflowModes.Truncate), false, -1, "65:v0 3:-0", truncated: true, lines: 1);
            // The earlier cut wins: here the overflow at the height, at the first character.
            Check(Label("AB", 9f, 0f, TextOverflowModes.Truncate), true, 0, "", truncated: true, lines: 0);
        }

        [Fact]
        public void AFallbackNoBreakSpaceDoesNotOverflow()
        {
            // GRAYSTROKE's U+00A0 comes from a fallback 20.41 high.
            Check(Label("\u00A0", 400f, 12.592742f, TextOverflowModes.Overflow, font: Graystroke, size: 12f), false, -1, "160:-0");
        }

        [Theory]
        [InlineData("A\n ", 8.94f, 19.04f)]
        [InlineData("A \n ", 8.94f, 19.04f)]
        [InlineData(" \nA", 8.94f, 38.1f)]
        [InlineData(" \n\nA", 8.94f, 57.17f)]
        [InlineData("\nA", 8.94f, 38.1f)]
        [InlineData(" \n ", 0.01f, 0.01f)]
        [InlineData("\n", 0.01f, 0.01f)]
        [InlineData("", 0f, 0f)]
        public void ThePreferredHeightGoesDownToTheLastLineWithACheckedCharacter(string text, float width, float height)
        {
            var t = Label(text, 400f, 100f, TextOverflowModes.Overflow);
            Assert.Equal(width, t.preferredWidth);
            Assert.Equal(height, t.preferredHeight);
        }

        // "Further characters": the first verification table (NotoSans-Regular 14 pt, centred).
        // Each row: the character; whether a text of it alone overflows in 0 x 0 (index); "xA" in
        // 400 x 0; "A\nx" in 400 x 19.03125; the preferred size of x and of "A\nx" (NaN: the
        // table's value is a fallback height the section leaves unexplained, not checked here).
        [Theory]
        [InlineData(0x0D, false, -1, 1, -1, 0.01f, 0.01f, 8.94f, 19.04f)]
        [InlineData(0x0B, false, -1, 1, -1, 0.01f, 0.01f, 8.94f, 19.04f)]
        [InlineData(0x2028, false, -1, 1, -1, 0.01f, 0.01f, 8.94f, 19.04f)]
        [InlineData(0x2029, false, -1, 1, -1, 0.01f, 0.01f, 8.94f, 19.04f)]
        [InlineData(0x2000, false, -1, 1, -1, 0.01f, 0.01f, 8.94f, 19.04f)]
        [InlineData(0x2005, false, -1, 1, -1, 0.01f, 0.01f, 8.94f, 19.04f)]
        [InlineData(0x200A, false, -1, 1, -1, 0.01f, 0.01f, 8.94f, 19.04f)]
        [InlineData(0x200B, false, -1, 1, -1, 0.01f, 0.01f, 8.94f, 19.04f)]
        [InlineData(0x3000, false, -1, 1, -1, 0.01f, 0.01f, 8.94f, float.NaN)]
        [InlineData(0x09, true, 0, 0, 2, 0.01f, 19.04f, 8.94f, 38.1f)]
        [InlineData(0x0C, true, 0, 0, 2, 14.01f, 20.74f, 14.01f, 39.81f)]
        [InlineData(0x180E, true, 0, 0, 2, 14.01f, 20.74f, 14.01f, 39.81f)]
        [InlineData(0xFEFF, true, 0, 0, 2, 0.01f, 23.82f, 8.94f, 42.89f)]
        public void FurtherCharactersAreCheckedOrNotAsMeasured(int code, bool alone, int aloneIndex, int beforeA, int afterLine,
            float width, float height, float lineWidth, float lineHeight)
        {
            string x = ((char)code).ToString();
            var t = Label(x, 0f, 0f, TextOverflowModes.Overflow);
            Assert.Equal(alone, t.isTextOverflowing);
            Assert.Equal(aloneIndex, t.firstOverflowCharacterIndex);
            // Truncate keeps an unchecked character and leaves nothing of a checked one.
            t = Label(x, 0f, 0f, TextOverflowModes.Truncate);
            Assert.Equal(alone ? 0 : 1, t.textInfo.characterCount);
            Assert.Equal(beforeA, Label(x + "A", 400f, 0f, TextOverflowModes.Overflow).firstOverflowCharacterIndex);
            Assert.Equal(afterLine, Label("A\n" + x, 400f, 19.03125f, TextOverflowModes.Overflow).firstOverflowCharacterIndex);
            t = Label(x, 400f, 100f, TextOverflowModes.Overflow);
            Assert.Equal(width, t.preferredWidth);
            Assert.Equal(height, t.preferredHeight);
            t = Label("A\n" + x, 400f, 100f, TextOverflowModes.Overflow);
            Assert.Equal(lineWidth, t.preferredWidth);
            if (!float.IsNaN(lineHeight)) Assert.Equal(lineHeight, t.preferredHeight);
        }

        [Theory]
        [InlineData(0x0D, 2)]
        [InlineData(0x0B, 3)]
        [InlineData(0x2028, 3)]
        [InlineData(0x2029, 3)]
        [InlineData(0x0A, 3)]
        [InlineData(0x2003, 2)]
        public void LineFeedVerticalTabAndTheSeparatorsBreakTheLineButCrDoesNot(int code, int lines)
        {
            var t = Label((char)code + "\nA", 400f, 100f, TextOverflowModes.Overflow);
            Assert.Equal(lines, t.textInfo.lineCount);
        }

        [Fact]
        public void CrIsAnInvisibleCharacterAsWideAsItsGlyph()
        {
            var t = Label("\rA", 400f, 100f, TextOverflowModes.Overflow);
            var cr = t.textInfo.characterInfo[0];
            Assert.Equal('\r', cr.character);
            Assert.False(cr.isVisible);
            Assert.Equal(7.146f, cr.xAdvance - cr.origin, 3);
            // GRAYSTROKE has no CR: it is 0 wide.
            t = Label("\r", 400f, 100f, TextOverflowModes.Overflow, font: Graystroke, size: 12f);
            Assert.Equal(0f, t.textInfo.characterInfo[0].xAdvance - t.textInfo.characterInfo[0].origin);
        }

        [Fact]
        public void FormFeedAndU180EAreTheMissingGlyphAndUFEFFIsVisible()
        {
            foreach (int code in new[] { 0x0C, 0x180E })
            {
                var c = Label(((char)code).ToString(), 400f, 100f, TextOverflowModes.Overflow).textInfo.characterInfo[0];
                Assert.Equal('□', c.character);
                Assert.True(c.isVisible);
            }
            var feff = Label("﻿", 400f, 100f, TextOverflowModes.Overflow).textInfo.characterInfo[0];
            Assert.Equal('﻿', feff.character);
            Assert.True(feff.isVisible);
            Assert.Equal(0f, feff.xAdvance - feff.origin);
        }

        [Theory]
        [InlineData("\u0003", 0)]
        [InlineData("\u0003\u0003", 0)]
        [InlineData("\u0003A", 0)]
        [InlineData("\u0003\nA", 0)]
        [InlineData("A\u0003", 2)]
        [InlineData("A\n\u0003", 3)]
        public void U0003EndsTheText(string text, int characters)
        {
            foreach (var mode in new[] { TextOverflowModes.Overflow, TextOverflowModes.Truncate })
            {
                var info = Label(text, 400f, 100f, mode).textInfo;
                Assert.Equal(characters, info.characterCount);
                if (characters > 0)
                {
                    Assert.Equal('\u0003', info.characterInfo[characters - 1].character);
                    Assert.False(info.characterInfo[characters - 1].isVisible);
                }
                else Assert.Equal(0, info.lineCount);
            }
            // None of them overflows.
            Assert.False(Label(text, 400f, 19.03125f, TextOverflowModes.Overflow).isTextOverflowing);
            if (characters == 0) Assert.False(Label(text, 0f, 0f, TextOverflowModes.Overflow).isTextOverflowing);
            var p = Label(text, 400f, 100f, TextOverflowModes.Overflow);
            if (characters == 0)
            {
                Assert.Equal(0.01f, p.preferredWidth);
                Assert.Equal(0.01f, p.preferredHeight);
            }
            else
            {
                Assert.Equal(8.94f, p.preferredWidth);
                // "A\n\u0003" is 4.09 high, an anomaly the section does not explain.
                if (characters == 2) Assert.Equal(19.04f, p.preferredHeight);
            }
        }

        static TextMeshProUGUI Tabbed(string text, string font, float size, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, float width = 400f)
        {
            var t = Label(text, width, 100f, TextOverflowModes.Overflow, font: font, size: size);
            t.alignment = align;
            t.ForceMeshUpdate(true);
            return t;
        }

        // The second verification table, and the tab's stops: multiples of tabSize x tabWidth at
        // the size, from the line's start, whatever the rect.
        [Theory]
        [InlineData(Noto, 14f, 364.58333f)]
        [InlineData(Noto, 28f, 729.16667f)]
        [InlineData(Graystroke, 12f, 300f)]
        [InlineData(Graystroke, 24f, 600f)]
        public void ATabEndsAtTheNextStop(string font, float size, float stop)
        {
            foreach (float width in new[] { 50f, 400f, 1000f })
            {
                var c = Tabbed("\t", font, size, width: width).textInfo.characterInfo;
                Assert.Equal(stop, c[0].xAdvance - c[0].origin, 3);
                c = Tabbed("\t\t", font, size, width: width).textInfo.characterInfo;
                Assert.Equal(2 * stop, c[1].xAdvance - c[0].origin, 3);
                c = Tabbed("A\t", font, size, width: width).textInfo.characterInfo;
                Assert.Equal(stop, c[1].xAdvance - c[0].origin, 3);
                c = Tabbed(" \t", font, size, width: width).textInfo.characterInfo;
                Assert.Equal(stop, c[1].xAdvance - c[0].origin, 3);
                c = Tabbed("\tA", font, size, width: width).textInfo.characterInfo;
                Assert.Equal(stop, c[1].origin - c[0].origin, 3);
            }
            // Centred, the line is laid out the same and then centred: "\t" from -T/2 to T/2.
            var centred = Tabbed("\t", font, size, TextAlignmentOptions.Top).textInfo.characterInfo[0];
            Assert.Equal(-stop / 2f, centred.origin, 3);
            Assert.Equal(stop / 2f, centred.xAdvance, 3);
        }

        [Theory]
        [InlineData("\t", 0.01f)]
        [InlineData("\t\t", 364.59f)]
        [InlineData("A\t", 8.94f)]
        [InlineData(" \t", 3.65f)]
        [InlineData("\tA", 373.52f)]
        public void ThePreferredWidthCountsATabAtItsStart(string text, float width)
        {
            Assert.Equal(width, Tabbed(text, Noto, 14f).preferredWidth);
        }
    }
}
