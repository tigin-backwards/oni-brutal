#pragma warning disable CS0618 // enableKerning, as the game's SetTextStyleSetting sets it
using System.Linq;
using TMPro;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    // Lines that grow part-way (docs/cleanroom/textmeshpro.md, "Lines that grow part-way"): each
    // character is checked against its line as it stands when it is placed, Truncate and
    // Ellipsis lay out the cut line with its characters before the cut, and white space in a
    // <size> takes no part in the line's height. GRAYSTROKE REGULAR SDF 14 pt unless named, set up
    // as the game's SetTextStyleSetting does (kerning, extra padding).
    public class TmpTallLineTests : EngineTest
    {
        const string Noto = "NotoSans-Regular";
        const string Graystroke = "GRAYSTROKE REGULAR SDF";
        // A geyser's name: its U+2011 comes from a taller fallback font.
        const string Geyser = "Liquid Sulfur Geyser NJ24\u20117";

        static TextMeshProUGUI Label(string text, float w, float h, TextOverflowModes mode, bool wrap = false, string font = Graystroke, float size = 14f,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform));
            canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("Tl", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.enableKerning = true;
            t.extraPadding = true;
            t.fontSize = size;
            t.font = TmpTestSupport.FindAsset<TMP_FontAsset>(font);
            t.alignment = align;
            t.overflowMode = mode;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.text = text;
            t.ForceMeshUpdate(true);
            return t;
        }

        static string Shown(TMP_Text t)
        {
            var info = t.textInfo;
            return new string(Enumerable.Range(0, info.characterCount).Select(i => info.characterInfo[i].character).ToArray());
        }

        static int Index(string text, float h, TextOverflowModes mode = TextOverflowModes.Overflow, bool wrap = false, string font = Graystroke, float w = 400f, float size = 14f) =>
            Label(text, w, h, mode, wrap, font, size).firstOverflowCharacterIndex;

        // The section's table.

        [Fact]
        public void TheGeyserTitleIsTruncatedAtItsTallerHyphen()
        {
            var t = Label(Geyser, 175.13f, 22f, TextOverflowModes.Truncate);
            Assert.True(t.isTextOverflowing);
            Assert.Equal(25, t.firstOverflowCharacterIndex);
            Assert.Equal("Liquid Sulfur Geyser NJ24\u0003", Shown(t));
            Assert.Equal(26, t.textInfo.characterCount);
            Assert.Equal(1, t.textInfo.lineCount);
            var line = t.textInfo.lineInfo[0];
            Assert.Equal(14.68f, line.lineHeight, 0.01f);
            Assert.Equal(11f, line.ascender, 0.01f);
            Assert.Equal(-3.69f, line.descender, 0.01f);
            Assert.Equal(-2.29f, line.baseline, 0.01f);
        }

        [Fact]
        public void TheGeyserTitleWithEllipsis()
        {
            var t = Label(Geyser, 175.13f, 22f, TextOverflowModes.Ellipsis);
            Assert.Equal(25, t.firstOverflowCharacterIndex);
            Assert.Equal("Liquid Sulfur Geyser NJ2\u2026\u0003", Shown(t));
        }

        [Fact]
        public void TheGeyserTitleWithOverflowKeepsAllItsCharacters()
        {
            var t = Label(Geyser, 175.13f, 22f, TextOverflowModes.Overflow);
            Assert.Equal(25, t.firstOverflowCharacterIndex);
            Assert.Equal(27, t.textInfo.characterCount);
            Assert.Equal(Geyser, Shown(t));
        }

        [Fact]
        public void ATallerCharacterOverflowsAtItself()
        {
            var t = Label("AAAA<size=150%>B</size>AA", 400f, 20f, TextOverflowModes.Truncate);
            Assert.Equal(4, t.firstOverflowCharacterIndex);
            Assert.Equal("AAAA\u0003", Shown(t));
        }

        [Fact]
        public void OnALaterLineTheCutLineKeepsItsBaselineBeforeTheTallerCharacter()
        {
            var t = Label("AA\nAAAA<size=150%>B</size>AA", 400f, 30f, TextOverflowModes.Truncate);
            Assert.Equal(7, t.firstOverflowCharacterIndex);
            Assert.Equal("AA\nAAAA\u0003", Shown(t));
            Assert.Equal(2, t.textInfo.lineCount);
            Assert.Equal(-12.97f, t.textInfo.lineInfo[1].baseline, 0.01f);
            // Overflow lays the whole line out: its baseline is lower.
            t = Label("AA\nAAAA<size=150%>B</size>AA", 400f, 30f, TextOverflowModes.Overflow);
            Assert.Equal(-19.62f, t.textInfo.lineInfo[1].baseline, 0.01f);
        }

        [Fact]
        public void SpacesInASizeTagTakeNoPartInTheLineHeight()
        {
            var t = Label("AAAA <size=150%> </size>AA", 400f, 20f, TextOverflowModes.Truncate);
            Assert.False(t.isTextOverflowing);
            Assert.Equal(-1, t.firstOverflowCharacterIndex);
            Assert.Equal(8, t.textInfo.characterCount);
            // GRAYSTROKE 12 pt: as high as "AAAA AA", and it never overflows a rect 12.75 high.
            var a = Label("AAAA <size=150%> </size>AA", 400f, 100f, TextOverflowModes.Overflow, size: 12f);
            var b = Label("AAAA AA", 400f, 100f, TextOverflowModes.Overflow, size: 12f);
            Assert.Equal(12.6f, a.preferredHeight, 0.001f);
            Assert.Equal(b.preferredHeight, a.preferredHeight);
            Assert.Equal(-1, Index("AAAA <size=150%> </size>AA", 12.75f, size: 12f));
            // The space reports the text's own ascender and descender.
            Assert.Equal(b.textInfo.characterInfo[4].ascender, a.textInfo.characterInfo[5].ascender, 0.001f);
            Assert.Equal(b.textInfo.characterInfo[4].descender, a.textInfo.characterInfo[5].descender, 0.001f);
        }

        // The rule's examples.

        [Theory]
        [InlineData(14.5f, 0)]
        [InlineData(14.75f, 25)]
        [InlineData(23.81f, 25)]
        [InlineData(24f, -1)]
        public void TheGeyserNameUnwrapped(float h, int index) => Assert.Equal(index, Index(Geyser, h));

        [Theory]
        [InlineData("AAAA<size=150%>B</size>AA", 14.75f, 4)]
        [InlineData("AAAA<size=150%>B</size>AA", 22.03f, 4)]
        [InlineData("<size=150%>B</size>AAAA", 22.03f, 0)]
        [InlineData("AAAA<voffset=0.4em>B</voffset>AA", 14.75f, 4)]
        [InlineData("AAAA<voffset=-0.4em>B</voffset>AA", 20.25f, 4)]
        [InlineData("AA<size=130%>B</size>AA<size=160%>C</size>AA", 14.75f, 2)]
        [InlineData("AA<size=130%>B</size>AA<size=160%>C</size>AA", 19.25f, 5)]
        [InlineData("AA<size=130%>B</size>AA<size=160%>C</size>AA", 23.75f, -1)]
        public void TheFirstLineGrowsAsItsCharactersArePlaced(string text, float h, int index) => Assert.Equal(index, Index(text, h));

        [Theory]
        [InlineData(19.25f, 2)]
        [InlineData(24.75f, 5)]
        [InlineData(30.5f, -1)]
        public void TwoSizesInNotoSans(float h, int index) => Assert.Equal(index, Index("AA<size=130%>B</size>AA<size=160%>C</size>AA", h, font: Noto));

        [Theory]
        [InlineData("AA\nAAAA<size=150%>B</size>AA", 14.5f, 0)]
        [InlineData("AA\nAAAA<size=150%>B</size>AA", 29.25f, 3)]
        [InlineData("AA\nAAAA<size=150%>B</size>AA", 29.5f, 7)]
        [InlineData("AA\nAAAA<size=150%>B</size>AA", 36.5f, 7)]
        [InlineData("AA\nAAAA<size=150%>B</size>AA", 36.75f, -1)]
        [InlineData("AA\nAAAA\u2011AA", 29.5f, 7)]
        [InlineData("AA\nAAAA\u2011AA", 38.5f, 7)]
        [InlineData("AA\nAAAA\u2011AA", 38.75f, -1)]
        [InlineData("AA\n<size=150%>B</size>AAAA", 14.75f, 3)]
        [InlineData("AA\n<size=150%>B</size>AAAA", 36.5f, 3)]
        [InlineData("AA\n<size=150%>B</size>AAAA", 36.75f, -1)]
        public void ALaterLineIsLoweredAsItGrows(string text, float h, int index)
        {
            Assert.Equal(index, Index(text, h));
            Assert.Equal(index, Index(text, h, TextOverflowModes.Truncate));
            Assert.Equal(index, Index(text, h, TextOverflowModes.Overflow, wrap: true));
        }

        [Theory]
        [InlineData(14.5f, 0)]
        [InlineData(14.75f, 5)]
        [InlineData(29.5f, 9)]
        [InlineData(36.75f, 11)]
        [InlineData(51.5f, -1)]
        public void ALineBegunByWrappingIsCheckedTheSameWay(float h, int index) =>
            Assert.Equal(index, Index("AAAA AAAA<size=150%>B</size>AA", h, wrap: true, w: 60f));
    }
}
