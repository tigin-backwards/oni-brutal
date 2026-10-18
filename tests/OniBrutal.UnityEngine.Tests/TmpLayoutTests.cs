using System.Linq;
using TMPro;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    // TextMeshPro layout with the game's fonts. Geometry expectations come from the shipped
    // package's output for the same texts (measured with --dump-ui-vertices).
    public class TmpLayoutTests : EngineTest
    {
        static TextMeshProUGUI Text(string text, float size = 14f, float width = 256f, float height = 51.61f, TMP_FontAsset font = null)
            => TmpTestSupport.NewText(text, font ?? TmpTestSupport.Graystroke, size, new Vector2(width, height));

        [Fact]
        public void CentredLabelMatchesTheMeasuredGeometry()
        {
            // The main menu's QUIT button label (14 pt GRAYSTROKE, centred in a 256 × 51.61
            // rect, extra padding), as the shipped package generated it.
            var t = Text("QUIT");
            t.alignment = TextAlignmentOptions.Center;
            t.extraPadding = true;
            t.ForceMeshUpdate(true);
            var mesh = t.textInfo.meshInfo[0];
            Assert.Equal(16, mesh.vertexCount);
            // Buffers hold the visible characters and one more quad.
            Assert.Equal(5 * 4, mesh.vertices.Length);
            Assert.Equal(-15.39361f, mesh.vertices[0].x, 4);
            Assert.Equal(-7.550403f, mesh.vertices[0].y, 4);
            Assert.Equal(6.88f, mesh.vertices[1].y, 4);
            Assert.Equal(0.9985313f, mesh.vertices[6].x, 4);
            Assert.Equal(0.0762f, mesh.uvs0[0].x, 4);
            Assert.Equal(0.875f, mesh.uvs0[0].y, 4);
            Assert.Equal(new Vector2(0f, 1f), mesh.uvs2[1]);
            Assert.Equal(new Vector3(0f, 0f, -1f), mesh.normals[0]);
            Assert.Equal(new Vector4(-1f, 0f, 0f, 1f), mesh.tangents[0]);
            // The spare quad is empty.
            Assert.Equal(Vector3.zero, mesh.vertices[19]);
        }

        [Fact]
        public void WithoutExtraPaddingQuadsExtendATexelAndAQuarter()
        {
            var t = Text("QUIT");
            t.alignment = TextAlignmentOptions.Center;
            t.ForceMeshUpdate(true);
            var info = t.textInfo;
            float s = 14f / 124f;
            Assert.Equal(info.characterInfo[0].origin - 1.25f * s, info.meshInfo[0].vertices[0].x, 4);
        }

        [Fact]
        public void TextInfoRecordsCharactersWordsAndLines()
        {
            var t = Text("Hello big world", width: 1000f);
            var info = t.textInfo;
            Assert.Equal(15, info.characterCount);
            Assert.Equal(2, info.spaceCount);
            Assert.Equal(3, info.wordCount);
            Assert.Equal(1, info.lineCount);
            Assert.Equal("big", info.wordInfo[1].GetWord());
            Assert.Equal(6, info.wordInfo[1].firstCharacterIndex);
            Assert.False(info.characterInfo[5].isVisible);
            Assert.True(info.characterInfo[6].isVisible);
            Assert.Equal(0, info.lineInfo[0].firstCharacterIndex);
            Assert.Equal(14, info.lineInfo[0].lastCharacterIndex);
            Assert.Equal(13, info.lineInfo[0].visibleCharacterCount);
            Assert.Equal(1, info.materialCount);
            // Each character advances the pen by its glyph's advance.
            Assert.Equal(info.characterInfo[1].origin, info.characterInfo[0].xAdvance, 4);
            Assert.Equal('w', info.characterInfo[10].character);
            Assert.Equal(10, info.characterInfo[10].index);
        }

        [Fact]
        public void LinesBreakAtWordsThatOverflowAndAtLineFeeds()
        {
            var t = Text("one two three four five six", width: 60f, height: 500f);
            var info = t.textInfo;
            Assert.True(info.lineCount > 1);
            for (int k = 0; k < info.lineCount; k++)
            {
                var line = info.lineInfo[k];
                // A line ends with a whole word and its trailing space.
                Assert.True(line.lastCharacterIndex == info.characterCount - 1 || info.characterInfo[line.lastCharacterIndex].character == ' ');
                Assert.True(info.characterInfo[line.lastVisibleCharacterIndex].xAdvance <= 30.001f);
            }
            var lf = Text("a\nb", width: 500f, height: 500f);
            Assert.Equal(2, lf.textInfo.lineCount);
            Assert.Equal('b', lf.textInfo.characterInfo[lf.textInfo.lineInfo[1].firstCharacterIndex].character);
            // Without wrapping a long text stays on one line.
            var nowrap = Text("one two three four five six", width: 60f, height: 500f);
            nowrap.textWrappingMode = TextWrappingModes.NoWrap;
            nowrap.ForceMeshUpdate(true);
            Assert.Equal(1, nowrap.textInfo.lineCount);
        }

        [Fact]
        public void AWordWiderThanTheLineBreaksBetweenCharacters()
        {
            var t = Text("ABCDEFGHIJKLMNOP", width: 40f, height: 500f);
            Assert.True(t.textInfo.lineCount > 2);
            Assert.Equal(16, t.textInfo.lineInfo.Take(t.textInfo.lineCount).Sum(l => l.characterCount));
        }

        [Fact]
        public void NoBreakKeepsWordsTogether()
        {
            // Without <nobr> "aaaa bb cc" fits the first line; with it, "bb cc dd" moves down.
            var plain = Text("aaaa bb cc dd", width: 70f, height: 500f);
            Assert.Equal(0, plain.textInfo.characterInfo[8].lineNumber);
            Assert.Equal(1, plain.textInfo.characterInfo[12].lineNumber);
            var t = Text("aaaa <nobr>bb cc dd</nobr>", width: 70f, height: 500f);
            var info = t.textInfo;
            Assert.Equal(1, info.characterInfo[5].lineNumber);
            Assert.Equal(1, info.characterInfo[info.characterCount - 1].lineNumber);
        }

        [Fact]
        public void LineBreakingRulesKeepPunctuationWithItsText()
        {
            // The settings' leading characters (opening brackets) may not end a line, the
            // following characters (closing punctuation) may not start one.
            TmpTestSupport.LoadGameData();
            var rules = TMP_Settings.linebreakingRules;
            Assert.Contains((uint)'「', rules.leadingCharacters);
            Assert.Contains((uint)'。', rules.followingCharacters);
            // CJK characters (from the global fallback fonts) break between each other: a narrow
            // rect gives a line per few characters, and no line starts with "。" or ends with "「".
            const string text = "日本語の文章です。「改行」の規則を確かめる。";
            var t = TmpTestSupport.NewText(text, TmpTestSupport.Graystroke, 14f, new Vector2(45f, 500f));
            var info = t.textInfo;
            Assert.True(info.lineCount > 3);
            for (int k = 0; k < info.lineCount; k++)
            {
                Assert.NotEqual('。', info.characterInfo[info.lineInfo[k].firstCharacterIndex].character);
                Assert.NotEqual('「', info.characterInfo[info.lineInfo[k].lastCharacterIndex].character);
            }
        }

        [Theory]
        [InlineData(TextAlignmentOptions.TopLeft)]
        [InlineData(TextAlignmentOptions.Top)]
        [InlineData(TextAlignmentOptions.TopRight)]
        [InlineData(TextAlignmentOptions.Left)]
        [InlineData(TextAlignmentOptions.Center)]
        [InlineData(TextAlignmentOptions.Right)]
        [InlineData(TextAlignmentOptions.BottomLeft)]
        [InlineData(TextAlignmentOptions.Bottom)]
        [InlineData(TextAlignmentOptions.BottomRight)]
        [InlineData(TextAlignmentOptions.BaselineLeft)]
        public void AlignmentPlacesTheTextInTheRect(TextAlignmentOptions alignment)
        {
            var t = Text("Ab", width: 200f, height: 100f);
            t.alignment = alignment;
            t.ForceMeshUpdate(true);
            var info = t.textInfo;
            var line = info.lineInfo[0];
            float left = info.characterInfo[0].origin, right = info.characterInfo[1].xAdvance;
            float width = right - left;
            int h = (int)alignment & 0xFF, v = (int)alignment & 0xFF00;
            if (h == (int)HorizontalAlignmentOptions.Left) Assert.Equal(-100f, left, 3);
            if (h == (int)HorizontalAlignmentOptions.Center) Assert.Equal(-width / 2f, left, 3);
            if (h == (int)HorizontalAlignmentOptions.Right) Assert.Equal(100f - width, left, 3);
            if (v == (int)VerticalAlignmentOptions.Top) Assert.Equal(50f, line.ascender, 3);
            if (v == (int)VerticalAlignmentOptions.Bottom) Assert.Equal(-50f, line.descender, 3);
            if (v == (int)VerticalAlignmentOptions.Middle) Assert.Equal(0f, (line.ascender + line.descender) / 2f, 3);
            if (v == (int)VerticalAlignmentOptions.Baseline) Assert.Equal(0f, line.baseline, 3);
        }

        [Fact]
        public void StoredAlignmentsSplitIntoTheTwoAlignments()
        {
            // Older data stores one TextAlignmentOptions, whose bytes are the horizontal and
            // vertical alignments.
            var go = new GameObject("Text", typeof(RectTransform));
            go.SetActive(false);
            var t = go.AddComponent<TextMeshProUGUI>();
            typeof(TMP_Text).GetField("m_textAlignment", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(t, TextAlignmentOptions.BottomRight);
            go.SetActive(true);
            Assert.Equal(HorizontalAlignmentOptions.Right, t.horizontalAlignment);
            Assert.Equal(VerticalAlignmentOptions.Bottom, t.verticalAlignment);
            Assert.Equal(TextAlignmentOptions.BottomRight, t.alignment);
        }

        [Fact]
        public void JustifiedAndFlushStretchTheLines()
        {
            var t = Text("aa bb cc dd ee ff gg hh", width: 80f, height: 500f);
            t.alignment = TextAlignmentOptions.TopJustified;
            t.ForceMeshUpdate(true);
            var info = t.textInfo;
            Assert.True(info.lineCount >= 2);
            var first = info.lineInfo[0];
            // The first line ends at the right edge; the last line stays left aligned.
            Assert.Equal(40f, info.characterInfo[first.lastVisibleCharacterIndex].xAdvance, 1);
            var last = info.lineInfo[info.lineCount - 1];
            Assert.Equal(-40f, info.characterInfo[last.firstCharacterIndex].origin, 3);
            t.alignment = TextAlignmentOptions.TopFlush;
            t.ForceMeshUpdate(true);
            last = t.textInfo.lineInfo[t.textInfo.lineCount - 1];
            Assert.Equal(40f, t.textInfo.characterInfo[last.lastVisibleCharacterIndex].xAdvance, 1);
        }

        [Fact]
        public void OverflowModesHideOrMarkWhatDoesNotFit()
        {
            const string text = "one two three four five six seven eight nine ten";
            var t = Text(text, width: 60f, height: 20f);
            t.overflowMode = TextOverflowModes.Overflow;
            t.ForceMeshUpdate(true);
            Assert.True(t.isTextOverflowing);
            Assert.True(t.firstOverflowCharacterIndex > 0);
            int last = t.textInfo.characterCount - 1;
            Assert.True(t.textInfo.characterInfo[last].isVisible);

            // Truncation stops the text where the first line that does not fit begins, after a
            // blank record.
            t.overflowMode = TextOverflowModes.Truncate;
            t.ForceMeshUpdate(true);
            var info = t.textInfo;
            Assert.True(info.characterCount <= last);
            Assert.Equal(1, info.lineCount);
            Assert.True(info.characterInfo[0].isVisible);
            Assert.False(info.characterInfo[info.characterCount - 1].isVisible);
            Assert.True(t.isTextTruncated);

            // An ellipsis replaces the last character after which it fits.
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.ForceMeshUpdate(true);
            info = t.textInfo;
            Assert.Equal('\u2026', info.characterInfo[info.characterCount - 2].character);
            Assert.True(info.characterInfo[info.characterCount - 2].isVisible);
            Assert.False(info.characterInfo[info.characterCount - 1].isVisible);
            Assert.True(info.characterInfo[0].isVisible);

            t.overflowMode = TextOverflowModes.Masking;
            t.ForceMeshUpdate(true);
            Assert.True(t.textInfo.characterInfo[last].isVisible);

            t.overflowMode = TextOverflowModes.Page;
            t.pageToDisplay = 2;
            t.ForceMeshUpdate(true);
            Assert.True(t.textInfo.pageCount > 1);
            Assert.False(t.textInfo.characterInfo[0].isVisible);
            Assert.Contains(t.textInfo.characterInfo.Take(t.textInfo.characterCount), c => c.isVisible && c.pageNumber == 1);
        }

        [Fact]
        public void LinkedOverflowContinuesInTheLinkedText()
        {
            var t = Text("one two three four five six seven eight nine ten", width: 60f, height: 20f);
            var other = Text("", width: 60f, height: 200f);
            t.overflowMode = TextOverflowModes.Linked;
            t.linkedTextComponent = other;
            t.ForceMeshUpdate(true);
            Assert.Equal(t.text, other.text);
            Assert.True(other.firstVisibleCharacter > 0);
        }

        [Fact]
        public void AutoSizeChoosesTheLargestSizeThatFits()
        {
            // The main menu's MOTD header: 18 px high, auto-sized; the shipped package chose 17.15.
            var t = Text("It's Klei Fest!", width: 276f, height: 18f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 10f;
            t.fontSizeMax = 24f;
            t.ForceMeshUpdate(true);
            Assert.Equal(17.15f, t.fontSize, 3);
            // A rect large enough: the maximum.
            ((RectTransform)t.transform).sizeDelta = new Vector2(1000f, 100f);
            t.ForceMeshUpdate(true);
            Assert.Equal(24f, t.fontSize, 3);
        }

        [Fact]
        public void KerningAdjustsTheAdvanceOfGlyphPairs()
        {
            var liberation = TmpTestSupport.FindAsset<TMP_FontAsset>("LiberationSans SDF");
            var records = liberation.fontFeatureTable.glyphPairAdjustmentRecords;
            var record = records.First(r => r.firstAdjustmentRecord.glyphValueRecord.xAdvance != 0f);
            uint first = record.firstAdjustmentRecord.glyphIndex, second = record.secondAdjustmentRecord.glyphIndex;
            var c1 = liberation.characterTable.First(c => c.glyphIndex == first);
            var c2 = liberation.characterTable.First(c => c.glyphIndex == second);
            string pair = char.ConvertFromUtf32((int)c1.unicode) + char.ConvertFromUtf32((int)c2.unicode);
            var kerned = Text(pair, 20f, 500f, 100f, liberation);
            float withKerning = kerned.textInfo.characterInfo[1].origin - kerned.textInfo.characterInfo[0].origin;
            kerned.enableKerning = false;
            kerned.ForceMeshUpdate(true);
            float without = kerned.textInfo.characterInfo[1].origin - kerned.textInfo.characterInfo[0].origin;
            float s = 20f / liberation.faceInfo.pointSize;
            Assert.Equal(record.firstAdjustmentRecord.glyphValueRecord.xAdvance * s, withKerning - without, 3);
        }

        [Fact]
        public void PreferredSizesAreTheLaidOutText()
        {
            var t = Text("QUIT", width: 300f, height: 100f);
            float s = 14f / 124f;
            var info = t.textInfo;
            // Preferred sizes round up to hundredths (as the shipped package's do).
            float advance = info.characterInfo[3].xAdvance - info.characterInfo[0].origin;
            Assert.Equal(Mathf.Ceil(advance * 100f) / 100f, t.preferredWidth, 4);
            var face = t.font.faceInfo;
            Assert.Equal(Mathf.Ceil((face.ascentLine - face.descentLine) * s * 100f) / 100f, t.preferredHeight, 4);
            Assert.Equal(new Vector2(t.preferredWidth, t.preferredHeight), t.GetPreferredValues());
            Assert.True(t.renderedWidth > 0f && t.renderedWidth <= t.preferredWidth);
            // Margins count from the next change of the text (the preferred sizes are kept
            // until then).
            float before = t.preferredWidth;
            t.margin = new Vector4(5f, 1f, 7f, 2f);
            Assert.Equal(before, t.preferredWidth);
            t.text = "QUIT!";
            t.text = "QUIT";
            Assert.Equal(Mathf.Ceil((advance + 12f) * 100f) / 100f, t.preferredWidth, 4);
            Assert.Equal(Mathf.Ceil(((face.ascentLine - face.descentLine) * s + 3f) * 100f) / 100f, t.preferredHeight, 4);
        }

        [Fact]
        public void TheSdfScaleFollowsTheLossyScaleBeforeTheCanvasesRender()
        {
            var t = Text("abc", width: 300f, height: 100f);
            var mid = new GameObject("Mid", typeof(RectTransform));
            mid.transform.SetParent(t.canvas.transform, false);
            t.transform.SetParent(mid.transform, false);
            int generated = 0;
            System.Action<Object> counter = o => { if (o == t) generated++; };
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(counter);
            try
            {
                Frame();
                Canvas.ForceUpdateCanvases();
                float w = t.textInfo.meshInfo[0].uvs0[0].w;
                int before = generated;
                // Changes up to a factor of 1.25 either way are left alone.
                mid.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                Canvas.ForceUpdateCanvases();
                Assert.Equal(w, t.textInfo.meshInfo[0].uvs0[0].w);
                mid.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
                Canvas.ForceUpdateCanvases();
                Assert.Equal(w, t.textInfo.meshInfo[0].uvs0[0].w);
                // A larger change multiplies uv0.w, without a new layout; only y counts.
                mid.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                Canvas.ForceUpdateCanvases();
                Assert.Equal(w * 0.5f, t.textInfo.meshInfo[0].uvs0[0].w, 6);
                mid.transform.localScale = new Vector3(0.25f, 0.5f, 1f);
                Canvas.ForceUpdateCanvases();
                Assert.Equal(w * 0.5f, t.textInfo.meshInfo[0].uvs0[0].w, 6);
                Assert.Equal(before, generated);
                // To and from 0 the text is generated again.
                mid.transform.localScale = new Vector3(0f, 0f, 1f);
                Canvas.ForceUpdateCanvases();
                Assert.Equal(0f, t.textInfo.meshInfo[0].uvs0[0].w);
                mid.transform.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                Assert.Equal(w, t.textInfo.meshInfo[0].uvs0[0].w, 6);
                Assert.Equal(before + 2, generated);
                // A text whose scale is marked static is not checked.
                t.isTextObjectScaleStatic = true;
                mid.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                Canvas.ForceUpdateCanvases();
                Assert.Equal(w, t.textInfo.meshInfo[0].uvs0[0].w, 6);
            }
            finally
            {
                TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(counter);
            }
        }

        [Fact]
        public void ARectWithoutWidthDoesNotWrapThePreferredHeight()
        {
            var one = Text("Hello world foo", width: 300f, height: 100f);
            var none = Text("Hello world foo", width: 0f, height: 100f);
            var narrow = Text("Hello world foo", width: 1f, height: 100f);
            Assert.Equal(one.preferredHeight, none.preferredHeight);
            Assert.True(narrow.preferredHeight > 10f * one.preferredHeight);
            // GetPreferredValues wraps at the width it is given, and reports the unwrapped width.
            var size = none.GetPreferredValues(1f, 100f);
            Assert.Equal(narrow.preferredHeight, size.y);
            Assert.Equal(one.preferredWidth, size.x);
        }

        [Fact]
        public void TheZeroWidthSpaceDrawsNothing()
        {
            var t = Text("\u200B", width: 300f, height: 100f);
            Assert.Equal(1, t.textInfo.characterCount);
            Assert.False(t.textInfo.characterInfo[0].isVisible);
            Assert.Equal(0, t.textInfo.meshInfo[0].vertexCount);
            // Its buffer is the spare quad.
            Assert.Equal(4, t.textInfo.meshInfo[0].vertices.Length);
        }

        [Theory]
        [InlineData("kg ", "0-1")]
        [InlineData(" kg", "0--1 1-2")]
        [InlineData(". kg", "2-3")]
        [InlineData("+ See All (0)", "0--1 2-4 6-8 11-11")]
        [InlineData("it's a-b", "0-3 5-7")]
        [InlineData("'x a_b", "1-1 3-3 5-5")]
        [InlineData("...", "0-0")]
        [InlineData(" ", "0--1")]
        public void WordsAreRecordedAsTheShippedPackageRecordsThem(string text, string expected)
        {
            var t = Text(text, width: 300f, height: 100f);
            var info = t.textInfo;
            Assert.Equal(expected, string.Join(" ", info.wordInfo.Take(info.wordCount).Select(w => $"{w.firstCharacterIndex}-{w.lastCharacterIndex}")));
        }

        [Fact]
        public void NewBuffersAreWhiteAndGrownOnesZero()
        {
            var t = Text("a", width: 300f, height: 100f);
            Assert.Equal(8, t.textInfo.meshInfo[0].vertices.Length);
            Assert.Equal(255, t.textInfo.meshInfo[0].colors32[4].a);
            t.text = "abcde";
            t.ForceMeshUpdate();
            var mi = t.textInfo.meshInfo[0];
            Assert.Equal(32, mi.vertices.Length);
            Assert.Equal(0, mi.colors32[20].a);
            Assert.Equal(-1f, mi.normals[20].z);
        }
    }
}
