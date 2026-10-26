using TMPro;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    // Texts as three Workshop mods build them (docs/cleanroom/textmeshpro.md, "A font set to
    // null", "The ellipsis glyph in Bold and Italic texts", "Line gaps and preferred heights with
    // fallback glyphs"). The values are the shipped package's, from tools/UnityProbe's modtext
    // probe.
    public class TmpModTextTests : EngineTest
    {
        static Transform Canvas()
        {
            TmpTestSupport.LoadGameData();
            var cgo = new GameObject("Canvas", typeof(RectTransform));
            cgo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return cgo.transform;
        }

        static TextMeshProUGUI NewText(Vector2 size, bool active = true)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            if (!active) go.SetActive(false);
            go.transform.SetParent(Canvas(), false);
            ((RectTransform)go.transform).sizeDelta = size;
            return go.AddComponent<TextMeshProUGUI>();
        }

        static TMP_FontAsset Default => TMP_Settings.defaultFontAsset;

        // ----- A font set to null (Smart Coolers) -----

        [Fact]
        public void NullOnAWokenTextIsTheDefaultFont()
        {
            var t = NewText(new Vector2(400f, 50f));
            t.fontSize = 36f;
            t.text = "Cool Contents";
            t.font = null;
            Assert.Equal("LiberationSans SDF", Default.name);
            Assert.Same(Default, t.font);
            Assert.Same(Default.material, t.fontSharedMaterial);
            Assert.Equal(228.56f, t.preferredWidth);
            t.ForceMeshUpdate();
            Assert.Equal(13, t.textInfo.characterCount);
        }

        [Fact]
        public void NullAfterAnotherFontTakesTheDefaultAtOnce()
        {
            var t = NewText(new Vector2(400f, 50f));
            t.font = TmpTestSupport.FindAsset<TMP_FontAsset>("NotoSans-Regular");
            t.fontSize = 36f;
            t.text = "Cool Contents";
            t.ForceMeshUpdate();
            Assert.Equal(237.57f, t.preferredWidth);
            t.font = null;
            Assert.Same(Default, t.font);
            Assert.Same(Default.material, t.fontSharedMaterial);
            Assert.Equal(228.56f, t.preferredWidth);
        }

        // PLib's checkbox label: added to an inactive object, its font set to null before it
        // wakes; it takes the default font when it is activated.
        [Fact]
        public void NullBeforeWakingStaysNullUntilTheTextWakes()
        {
            var t = NewText(new Vector2(200f, 50f), active: false);
            t.font = null;
            Assert.Null(t.font);
            t.fontSize = 14f;
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.gameObject.SetActive(true);
            Assert.Same(Default, t.font);
            t.alignment = TextAlignmentOptions.Center;
            t.text = "Cool Contents";
            t.ForceMeshUpdate();
            Assert.Equal(13, t.textInfo.characterCount);
            Assert.Equal(100.65f, t.preferredWidth);
            Assert.Equal(15.65f, t.preferredHeight);
        }

        // ----- The ellipsis glyph in Bold and Italic texts (Rookie Quest Guide) -----

        static TextMeshProUGUI Header(string text, FontStyles style, float width, TextOverflowModes mode = TextOverflowModes.Ellipsis)
        {
            var t = NewText(new Vector2(width, 22f));
            t.text = text;
            t.fontSize = 12f;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.fontStyle = style;
            t.overflowMode = mode;
            t.ForceMeshUpdate();
            return t;
        }

        static TMP_SubMeshUI[] SubMeshes(TMP_Text t) => t.GetComponentsInChildren<TMP_SubMeshUI>(true);

        [Theory]
        [InlineData(FontStyles.Bold, "Roboto-Bold SDF")]
        [InlineData(FontStyles.Italic, "Roboto-Italic SDF")]
        [InlineData(FontStyles.Bold | FontStyles.Italic, "Roboto-BoldItalic SDF")]
        public void AStyledTextThatFitsHasTheEllipsisTypefacesMaterial(FontStyles style, string typeface)
        {
            var t = Header("Pitcher Pump", style, 238f);
            Assert.Equal(12, t.textInfo.characterCount);
            Assert.Equal(2, t.textInfo.materialCount);
            var subs = SubMeshes(t);
            Assert.Single(subs);
            Assert.Equal(typeface, subs[0].fontAsset.name);
            Assert.Equal(4, subs[0].mesh.vertexCount);
        }

        [Fact]
        public void AnEmptyBoldEllipsisTextHasTwoMaterials()
        {
            var t = Header("", FontStyles.Bold, 238f);
            Assert.Equal(0, t.textInfo.characterCount);
            Assert.Equal(2, t.textInfo.materialCount);
            var subs = SubMeshes(t);
            Assert.Single(subs);
            Assert.Equal("Roboto-Bold SDF", subs[0].fontAsset.name);
            Assert.Equal(4, subs[0].mesh.vertexCount);
        }

        // The ellipsis's material comes ahead of a fallback the characters need; its sub-mesh is
        // made after the fallback's.
        [Fact]
        public void TheEllipsisMaterialComesAheadOfTheCharacters()
        {
            var t = Header("▪ Pitcher Pump", FontStyles.Bold, 238f);
            Assert.Equal(3, t.textInfo.materialCount);
            Assert.Equal("LiberationSans SDF - Fallback", t.textInfo.characterInfo[0].fontAsset.name);
            Assert.Equal(2, t.textInfo.characterInfo[0].materialReferenceIndex);
            var subs = SubMeshes(t);
            Assert.Equal(2, subs.Length);
            Assert.Equal("LiberationSans SDF - Fallback", subs[0].fontAsset.name);
            Assert.Equal("Roboto-Bold SDF", subs[1].fontAsset.name);
        }

        [Theory]
        [InlineData(FontStyles.Bold, 30f, "Pit", "Roboto-Bold SDF", 4.965f)]
        [InlineData(FontStyles.Bold, 40f, "Pitch", "Roboto-Bold SDF", 4.965f)]
        [InlineData(FontStyles.Italic, 30f, "Pitc", "Roboto-Italic SDF", 3.6453495f)]
        public void ACutTextDrawsTheTypefacesEllipsis(FontStyles style, float width, string shown, string typeface, float advance)
        {
            var t = Header("Pitcher Pump", style, width);
            var info = t.textInfo;
            Assert.Equal(shown.Length + 2, info.characterCount);
            for (int i = 0; i < shown.Length; i++) Assert.Equal(shown[i], info.characterInfo[i].character);
            var e = info.characterInfo[shown.Length];
            Assert.Equal('…', e.character);
            Assert.Equal(typeface, e.fontAsset.name);
            Assert.Equal(1, e.materialReferenceIndex);
            Assert.Equal(advance, e.xAdvance - e.origin, 3);
            var blank = info.characterInfo[shown.Length + 1];
            Assert.Equal('\u0003', blank.character);
            Assert.Same(Default, blank.fontAsset);
            Assert.Equal(4, SubMeshes(t)[0].mesh.vertexCount);
        }

        [Fact]
        public void ATypedEllipsisIsAnOrdinaryCharacter()
        {
            var t = Header("…", FontStyles.Bold, 238f, TextOverflowModes.Overflow);
            var e = t.textInfo.characterInfo[0];
            Assert.Equal("LiberationSans SDF", e.fontAsset.name);
            Assert.Equal(0, e.materialReferenceIndex);
            Assert.Equal(12.84f, e.xAdvance - e.origin, 3);
            Assert.Equal(1, t.textInfo.materialCount);
            Assert.Empty(SubMeshes(t));
        }

        [Fact]
        public void ARegularEllipsisTextHasNoExtraMaterial()
        {
            var t = Header("Pitcher Pump", FontStyles.Normal, 238f);
            Assert.Equal(1, t.textInfo.materialCount);
            Assert.Empty(SubMeshes(t));
            t = Header("Pitcher Pump", FontStyles.Normal, 40f);
            var e = t.textInfo.characterInfo[t.textInfo.characterCount - 2];
            Assert.Equal('…', e.character);
            Assert.Equal("LiberationSans SDF", e.fontAsset.name);
        }

        // ----- Line gaps and preferred heights with fallback glyphs (Social Life, Rookie
        // Quest Guide) -----

        static TextMeshProUGUI Lines(string font, float size, string text, float width, bool wrap = true)
        {
            var t = NewText(new Vector2(width, 0f));
            if (font != null) t.font = TmpTestSupport.FindAsset<TMP_FontAsset>(font);
            t.fontSize = size;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.text = text;
            return t;
        }

        [Theory]
        [InlineData("·\nB", 35.76f)]
        [InlineData("·\nBB", 37.31f)]
        [InlineData("·\nB B", 37.31f)]
        [InlineData("·\nB ", 35.76f)]
        [InlineData("·\n B", 37.31f)]
        [InlineData("·\nBB·", 44.23f)]
        [InlineData("·\nB\nBB", 49.39f)]
        [InlineData("B\n·\nBB", 50.94f)]
        [InlineData("·\n\nBB", 49.39f)]
        [InlineData("·\nB\nB\nBB", 63.03f)]
        [InlineData("B\nBB", 27.28f)]
        [InlineData("<size=150%>B</size>\nBB", 40.27f)]
        [InlineData("<size=150%>B</size>\nB", 34.1f)]
        [InlineData("B<voffset=0.5em>B</voffset>\nBB", 40.28f)]
        [InlineData("·\nB<size=150%>B</size>", 43.48f)]
        [InlineData("·\nB<size=60%>B</size>", 37.31f)]
        public void GraystrokeWithAFallbackDot(string text, float height)
        {
            foreach (bool wrap in new[] { true, false })
                Assert.Equal(height, Lines("GRAYSTROKE REGULAR SDF", 13f, text, 740f, wrap).preferredHeight);
        }

        [Theory]
        [InlineData("免\nBB", 25.98f)]
        [InlineData("免\nB ", 23.69f)]
        [InlineData("免\nBB免", 28.28f)]
        [InlineData("免\n免B", 26.96f)]
        [InlineData("B\n免\nBB", 36.33f)]
        [InlineData("免\nB\nB\nBB", 44.38f)]
        [InlineData("B\nBB", 20.41f)]
        [InlineData("<size=150%>B</size>\nBB", 29.51f)]
        [InlineData("B<voffset=0.5em>B</voffset>\nBB", 29.41f)]
        [InlineData("免\nB<size=150%>B</size>", 30.06f)]
        public void LiberationSansWithAFallbackCjkCharacter(string text, float height)
        {
            foreach (bool wrap in new[] { true, false })
                Assert.Equal(height, Lines(null, 9f, text, 1212f, wrap).preferredHeight);
        }

        // A line's height is its tallest character's ascender less descender (the fallback's
        // own, not its font's line height) plus the primary font's line gap.
        [Fact]
        public void ALineWithAFallbackDotIsAsHighAsTheDotPlusTheGap()
        {
            var t = Lines("GRAYSTROKE REGULAR SDF", 13f, "·\nB", 740f);
            t.rectTransform.sizeDelta = new Vector2(740f, 1000f);
            t.ForceMeshUpdate();
            var info = t.textInfo;
            Assert.Equal("NotoNaskhArabic-Regular", info.characterInfo[0].fontAsset.name);
            Assert.Equal(22.1086826f, info.lineInfo[0].lineHeight, 4);
            Assert.Equal(13.6355848f, info.lineInfo[1].lineHeight, 4);
            // The interval: line 0's height less its ascender, plus line 1's ascender.
            float interval = info.lineInfo[0].baseline - info.lineInfo[1].baseline;
            Assert.Equal(20.558228f, interval, 4);
        }

        [Fact]
        public void TheDisclaimersLinesSpaceByTheCjkHeight()
        {
            var t = Lines(null, 9f, "免责声明\nAB", 1212f);
            t.rectTransform.sizeDelta = new Vector2(1212f, 1000f);
            t.ForceMeshUpdate();
            var info = t.textInfo;
            Assert.Equal(13.6256838f, info.lineInfo[0].lineHeight, 4);
            Assert.Equal(11.32939f, info.lineInfo[0].baseline - info.lineInfo[1].baseline, 4);
        }

        [Fact]
        public void TheModsTexts()
        {
            const string summary = "Couples: 0 · Cold wars: 0 · Best friends: 0\nColony: Neutral (0)\nLiving duplicants: 0\nRelationships: 0\nMatches: 0 · Blocks: 0";
            Assert.Equal(85.14f, Lines("GRAYSTROKE REGULAR SDF", 13f, summary, 740f).preferredHeight);
            Assert.Equal(37.31f, Lines("GRAYSTROKE REGULAR SDF", 13f, "Couples: 0 · Cold wars: 0\nColony: Neutral (0)", 740f).preferredHeight);
            const string disclaimer = "免责声明：本模组为无偿、非商业的教学辅助工具；任务内容仅对B站UP主「大叔追云彩」《缺氧新手活过100天》系列视频作提纲式引用，不含画面/音频/完整文案转载，著作权归原作者；本模组与 Klei Entertainment 无隶属关系，不修改付费内容；若涉侵权，请通过工坊留言或 GitHub Issues 联系，本人将立即删除相应功能。";
            Assert.Equal(26.96f, Lines(null, 9f, disclaimer, 1212f).preferredHeight);
        }
    }
}
