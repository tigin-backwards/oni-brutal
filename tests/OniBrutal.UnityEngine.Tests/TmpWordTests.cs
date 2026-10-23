#pragma warning disable CS0618 // enableKerning, as the game's SetTextStyleSetting sets it
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    // Words and their separators (docs/cleanroom/textmeshpro.md, "Words and their separators"):
    // which characters make words, how apostrophes join, which line counts a word, the empty word
    // and texts without words, and the line breaks after U+2007 and U+202F. Texts are set up as
    // the probe set them (SetTextStyleSetting's kerning and extra padding, 400 × 100 unwrapped).
    public class TmpWordTests : EngineTest
    {
        const string Noto = "NotoSans-Regular";
        const string Graystroke = "GRAYSTROKE REGULAR SDF";
        const string Geyser = "Liquid Sulfur Geyser NJ24‑7";

        static TextMeshProUGUI Make(string font = Noto, float size = 14f)
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform));
            canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("Wc", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 100);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.enableKerning = true;
            t.extraPadding = true;
            t.fontSize = size;
            t.font = TmpTestSupport.FindAsset<TMP_FontAsset>(font);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        // Lays out the text, unwrapped in 400 × 100 or wrapped in the given width.
        static TMP_TextInfo Lay(TextMeshProUGUI t, string text, float wrapWidth = 0f)
        {
            t.textWrappingMode = wrapWidth > 0f ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            ((RectTransform)t.transform).sizeDelta = new Vector2(wrapWidth > 0f ? wrapWidth : 400f, wrapWidth > 0f ? 400f : 100f);
            t.text = text;
            t.ForceMeshUpdate();
            return t.textInfo;
        }

        static string Words(TMP_TextInfo info) =>
            string.Join(" ", info.wordInfo.Take(info.wordCount).Select(w => $"{w.firstCharacterIndex}:{w.lastCharacterIndex}"));

        static string LineWords(TMP_TextInfo info) =>
            string.Join(",", info.lineInfo.Take(info.lineCount).Select(l => l.wordCount));

        // The characters of each line, "|" between lines.
        static string Lines(TMP_TextInfo info)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < info.lineCount; k++)
            {
                if (k > 0) sb.Append('|');
                var li = info.lineInfo[k];
                for (int i = li.firstCharacterIndex; i <= li.lastCharacterIndex && i < info.characterCount; i++)
                    sb.Append(info.characterInfo[i].character);
            }
            return sb.ToString();
        }

        [Theory]
        // The four hyphens are word characters anywhere in a word.
        [InlineData("a-b", "0:2")]
        [InlineData("a‑b", "0:2")]
        [InlineData("a‐b", "0:2")]
        [InlineData("a­b", "0:2")]
        [InlineData("-a", "0:1")]
        [InlineData("‑a", "0:1")]
        [InlineData("a‑", "0:1")]
        [InlineData("--", "0:1")]
        [InlineData("-", "0:0")]
        [InlineData("a ‑ b", "0:0 2:2 4:4")]
        [InlineData("NJ24‑7", "0:5")]
        [InlineData("24-7", "0:3")]
        // The other dashes, "_", the non-breaking spaces and combining marks separate.
        [InlineData("a‒b", "0:0 2:2")]
        [InlineData("a–b", "0:0 2:2")]
        [InlineData("a—b", "0:0 2:2")]
        [InlineData("a―b", "0:0 2:2")]
        [InlineData("a−b", "0:0 2:2")]
        [InlineData("a_b", "0:0 2:2")]
        [InlineData("a b", "0:0 2:2")]
        [InlineData("a b", "0:0 2:2")]
        [InlineData("a b", "0:0 2:2")]
        [InlineData("éa", "0:0 2:2")]
        [InlineData("1,000", "0:0 2:4")]
        [InlineData("ab中cd", "0:4")]
        [InlineData("中。文", "0:0 2:2")]
        [InlineData("ab<b>cd</b>", "0:3")]
        // Apostrophes join only between letters or digits.
        [InlineData("don't", "0:4")]
        [InlineData("don’t", "0:4")]
        [InlineData("a'1", "0:2")]
        [InlineData("1'a", "0:2")]
        [InlineData("a''b", "0:0 3:3")]
        [InlineData("ab'-", "0:1 3:3")]
        [InlineData("ab-'", "0:2")]
        [InlineData("'tis", "1:3")]
        [InlineData("a‘b", "0:0 2:2")]
        [InlineData("a`b", "0:0 2:2")]
        // The empty word: a first separator that is not punctuation, or a single character that
        // is not a word character.
        [InlineData(" ab", "0:-1 1:2")]
        [InlineData("+ a", "0:-1 2:2")]
        [InlineData("(a)", "1:1")]
        [InlineData("%a", "1:1")]
        [InlineData("#1", "1:1")]
        [InlineData("(", "0:-1")]
        [InlineData(".", "0:-1")]
        [InlineData("+", "0:-1")]
        [InlineData(" ", "0:-1")]
        [InlineData("'", "0:-1")]
        // § and ¶ are symbols, not punctuation.
        [InlineData("§a", "0:-1 1:1")]
        [InlineData("¶a", "0:-1 1:1")]
        public void WordsFollowTheMeasuredRule(string text, string expected)
        {
            var t = Make();
            Assert.Equal(expected, Words(Lay(t, text)));
        }

        [Fact]
        public void TheGeysersNameIsFourWords()
        {
            var t = Make();
            var info = Lay(t, Geyser);
            Assert.Equal(4, info.wordCount);
            Assert.Equal("0:5 7:12 14:19 21:26", Words(info));
            Assert.Equal(6, info.wordInfo[3].characterCount);
            Assert.Equal("4", LineWords(info));
        }

        [Fact]
        public void AnEmptyTextHasNoWords()
        {
            var t = Make();
            Lay(t, "ab");
            Assert.Equal(0, Lay(t, "").wordCount);
        }

        [Fact]
        public void ATextWithoutWordsKeepsThePreviousRecord()
        {
            var t = Make();
            Lay(t, "#1");
            var info = Lay(t, "..");
            Assert.Equal(1, info.wordCount);
            Assert.Equal(1, info.wordInfo[0].firstCharacterIndex);
            Assert.Equal(1, info.wordInfo[0].lastCharacterIndex);
            Assert.Equal("0", LineWords(info));

            Lay(t, "(");
            info = Lay(t, "()");
            Assert.Equal("0:-1", Words(info));
            Assert.Equal("0", LineWords(info));

            Lay(t, "#1");
            info = Lay(t, "+.");
            Assert.Equal("0:-1", Words(info));
            Assert.Equal("1", LineWords(info));
        }

        [Fact]
        public void AnApostropheAtTheEndReadsTheSlotPastTheText()
        {
            var t = Make();
            // The slot after "ab cd'" holds a letter: the last word never ends.
            Lay(t, "abcdefgh");
            var info = Lay(t, "ab cd'");
            Assert.Equal(1, info.wordCount);
            Assert.Equal("0:1", Words(info));
            Assert.Equal("1", LineWords(info));

            // The only word never ends: one word, the previous record, no line counts it.
            Lay(t, "a bca");
            info = Lay(t, "ab'");
            Assert.Equal(1, info.wordCount);
            Assert.Equal("0:0", Words(info));
            Assert.Equal(1, info.wordInfo[0].characterCount);
            Assert.Equal("0", LineWords(info));

            // The slot holds "'": the apostrophe ends the word.
            Lay(t, "''''");
            Assert.Equal("0:0", Words(Lay(t, "a'")));
            Assert.Equal("1", LineWords(t.textInfo));
            Lay(t, "ab'.");
            Assert.Equal("0:1", Words(Lay(t, "ab'")));
        }

        [Theory]
        // A word is counted on the line of the character that ended it.
        [InlineData(Noto, 14f, Geyser, 40f, "Liqui|d |Sulfur |Geyse|r |NJ24‑|7", "0,1,1,0,1,0,1")]
        [InlineData(Noto, 14f, "aa-bb-cc-dd", 21.75f, "aa-|bb-|cc-|dd", "0,0,0,1")]
        [InlineData(Noto, 14f, "aa‑bb‑cc‑dd", 28.5f, "aa‑|bb‑c|c‑dd", "0,0,1")]
        [InlineData(Graystroke, 12f, "aaaa/bbbb", 13f, "aa|aa|/b|bb|b", "0,0,1,0,1")]
        // U+2007 and U+202F allow a break after them; U+00A0 does not.
        [InlineData(Graystroke, 12f, "aaaa bbbb", 19.5f, "aaa|a |bbb|b", "0,1,0,1")]
        [InlineData(Graystroke, 12f, "aaaa bbbb", 19.5f, "aaa|a |bbb|b", "0,1,0,1")]
        [InlineData(Graystroke, 12f, "aaaa bbbb", 19.5f, "aaa|a b|bbb", "0,1,1")]
        public void WrappedWordsCountOnTheLineWhereTheyEnd(string font, float size, string text, float width, string lines, string lineWords)
        {
            var t = Make(font, size);
            var info = Lay(t, text, width);
            Assert.Equal(lines, Lines(info));
            Assert.Equal(lineWords, LineWords(info));
        }

        [Fact]
        public void AWordBrokenAfterAHyphenCountsOnce()
        {
            var t = Make();
            float width = 1f;
            TMP_TextInfo info = null;
            for (; width < 80f; width += 0.25f)
            {
                info = Lay(t, "aaaa-bbbb", width);
                if (Lines(info) == "aaaa-|bbbb") break;
            }
            Assert.Equal("aaaa-|bbbb", Lines(info));
            Assert.Equal("0:8", Words(info));
            Assert.Equal("0,1", LineWords(info));
        }

        [Fact]
        public void ADrawnSoftHyphenReportsAHyphenMinus()
        {
            var t = Make();
            var info = Lay(t, "aaaa­bbbb", 36f);
            Assert.Equal("aaaa-|bbbb", Lines(info));
            Assert.True(info.characterInfo[4].isVisible);
            Assert.Equal('-', info.characterInfo[4].character);
            Assert.Equal("0:8", Words(info));
            Assert.Equal("0,1", LineWords(info));
            // Hidden, it keeps U+00AD.
            info = Lay(t, "aaaa­bbbb");
            Assert.False(info.characterInfo[4].isVisible);
            Assert.Equal('­', info.characterInfo[4].character);
        }
    }
}
