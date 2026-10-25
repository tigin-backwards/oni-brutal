#pragma warning disable CS0618 // enableKerning, as the game's SetTextStyleSetting sets it
using TMPro;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    // Heights in the translations' fonts (docs/cleanroom/textmeshpro.md, "Heights in the
    // translations' fonts"): labels set up as SetTextStyleSetting does, top left in a 400 × 60
    // rect with its top at y 30 (pivot 0.5), as the measurements were.
    public class TmpTranslationHeightTests : EngineTest
    {
        static TextMeshProUGUI Label(string font, float size, string text, float pivotY = 0.5f, bool wrap = false)
        {
            var cgo = new GameObject("Canvas", typeof(RectTransform));
            cgo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(cgo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, pivotY);
            rt.sizeDelta = new Vector2(400f, 60f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.enableKerning = true;
            t.extraPadding = true;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.fontSize = size;
            t.font = TmpTestSupport.FindAsset<TMP_FontAsset>(font);
            t.text = text;
            return t;
        }

        // The line is 35.5499992 high as a float at 24 pt (NotoSansCJKjp's 34.8125 and -9.625 at
        // point size 30, by 0.8): 3554.99992 floors to 3554, so 35.55, not 35.56.
        [Theory]
        [InlineData("NotoSansCJKjp-Regular", "全般", 0.5f, false)]
        [InlineData("NotoSansCJKjp-Regular", "全般", 1f, false)]
        [InlineData("NotoSansCJKjp-Regular", "全般", 0.63f, false)]
        [InlineData("NotoSansCJKjp-Regular", "温度単位", 0.5f, true)]
        [InlineData("NotoSansCJKjp-Regular", "セーブデータ", 1f, true)]
        [InlineData("NotoSansCJKkr-Regular", "일반", 0.5f, false)]
        [InlineData("NotoSans-Regular", "저장", 0.63f, false)]
        public void A24PointLineOfTheCjkFontsIs3555High(string font, string text, float pivotY, bool wrap)
        {
            var t = Label(font, 24f, text, pivotY, wrap);
            Assert.Equal(35.55f, t.preferredHeight);
            Assert.Equal(35.55f, t.GetPreferredValues(text, 400f, 60f).y);
            // With the rect's top at 22.2 the logged line is in the rect's float coordinates.
            if (pivotY == 0.63f) return;
            t.ForceMeshUpdate(true);
            var line = t.textInfo.lineInfo[0];
            Assert.Equal(35.5499992f, line.ascender - line.descender);
        }

        [Fact]
        public void LatinAt24PointIsUnchanged() => Assert.Equal(32.63f, Label("NotoSans-Regular", 24f, "General").preferredHeight);

        // A space of the primary font between letters from a fallback adds nothing to the line,
        // and reports the line's ascender and descender.
        [Fact]
        public void ASpaceOfThePrimaryFontAmongFallbackLettersAddsNothing()
        {
            const string text = "Единицы температуры";
            var t = Label("NotoSansCJKsc-Regular", 24f, text);
            Assert.Equal(32.63f, t.preferredHeight);
            t.ForceMeshUpdate(true);
            var info = t.textInfo;
            var line = info.lineInfo[0];
            Assert.Equal(30f, line.ascender);
            Assert.Equal(-2.625f, line.descender);
            Assert.Equal(4.375f, line.baseline);
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                Assert.Equal(30f, ch.ascender);
                Assert.Equal(-2.625f, ch.descender);
                Assert.Equal(ch.character == ' ' ? "NotoSansCJKsc-Regular" : "NotoSans-Regular", ch.fontAsset.name);
            }
        }

        [Theory]
        [InlineData("NotoSansCJKkr-Regular", false)]
        [InlineData("NotoSansCJKkr-Regular", true)]
        [InlineData("NotoSansThai-Regular", false)]
        public void At12PointTheLineIsTheFallbacks(string font, bool wrap)
            => Assert.Equal(16.32f, Label(font, 12f, "Единицы температуры", wrap: wrap).preferredHeight);
    }
}
