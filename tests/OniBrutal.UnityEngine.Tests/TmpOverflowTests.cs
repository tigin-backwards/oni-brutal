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
    }
}
