using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Xunit;

namespace OniBrutal.Tests
{
    // TMP_FontAsset.CreateFontAsset from OS fonts, by family and style name and by file path
    // (docs/cleanroom/textmeshpro.md, "Font assets from OS fonts"), on this machine's DejaVu,
    // FreeFont, TLWG and IPA fonts.
    public class TmpOsFontAssetTests : EngineTest
    {
        const string DejaVuSansPath = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";

        static void RequireDejaVu()
        {
            if (!File.Exists(DejaVuSansPath)) Assert.Skip("DejaVu Sans is not installed.");
            TmpTestSupport.LoadGameData();
        }

        // What Application.logMessageReceived gets while `action` runs.
        static List<(string Message, LogType Type)> Logged(System.Action action)
        {
            var logs = new List<(string, LogType)>();
            Application.LogCallback record = (message, _, type) => logs.Add((message, type));
            Application.logMessageReceived += record;
            try { action(); }
            finally { Application.logMessageReceived -= record; }
            return logs;
        }

        [Theory]
        [InlineData("Segoe UI Symbol", "Regular")]
        [InlineData("Arial", "Regular")]
        [InlineData("Malgun Gothic", "Regular")]
        [InlineData("Tahoma", "Regular")]
        [InlineData("DejaVu Sans", "Regular")]
        [InlineData("DejaVuSans", "Book")]
        [InlineData("FreeSans", "Medium")]
        [InlineData("", "Regular")]
        [InlineData(null, "Regular")]
        public void AFamilyAndStyleNotInstalledReturnNullAndLogOneLine(string family, string style)
        {
            RequireDejaVu();
            TMP_FontAsset asset = null;
            var logs = Logged(() => asset = TMP_FontAsset.CreateFontAsset(family, style, 60));
            Assert.Null(asset);
            var line = $"Unable to find a font file with the specified Family Name [{family}] and Style [{style}].";
            Assert.Equal(new[] { (line, LogType.Log) }, logs);
        }

        [Fact]
        public void ANullFamilyAndAnEmptyStylePrintAsEmptyBrackets()
        {
            RequireDejaVu();
            var logs = Logged(() => Assert.Null(TMP_FontAsset.CreateFontAsset(null, "", 60)));
            Assert.Equal(new[] { ("Unable to find a font file with the specified Family Name [] and Style [].", LogType.Log) }, logs);
        }

        [Theory]
        [InlineData("DejaVu Sans", "Book", "DejaVu Sans", "Book")]
        [InlineData("dejavu sans", "book", "DejaVu Sans", "Book")]
        [InlineData("DEJAVU SANS", "BOOK", "DejaVu Sans", "Book")]
        [InlineData("DejaVu Sans", "Bold", "DejaVu Sans", "Bold")]
        [InlineData("FreeSans", "", "FreeSans", "Regular")]
        [InlineData("FreeSans", "Regular", "FreeSans", "Regular")]
        [InlineData("Loma", "", "Loma", "Regular")]
        [InlineData("IPAGothic", "", "IPAGothic", "Regular")]
        [InlineData("Unifont", "Regular", "Unifont", "Regular")]
        public void FamilyAndStyleFindTheFaceWithoutRegardToCase(string family, string style, string foundFamily, string foundStyle)
        {
            RequireDejaVu();
            if (!FontEngine.TryGetSystemFontReference(foundFamily, foundStyle, out _)) Assert.Skip(foundFamily + " is not installed.");
            TMP_FontAsset asset = null;
            var logs = Logged(() => asset = TMP_FontAsset.CreateFontAsset(family, style, 60));
            Assert.Empty(logs);
            Assert.NotNull(asset);
            Assert.Equal(foundFamily, asset.faceInfo.familyName);
            Assert.Equal(foundStyle, asset.faceInfo.styleName);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void AnEmptyStyleFindsSomeFaceOfAFamilyWithoutRegular(string style)
        {
            // Unity's player gave Book; which face is the engine's lookup's choice.
            RequireDejaVu();
            var a = TMP_FontAsset.CreateFontAsset("DejaVu Sans", style, 60);
            Assert.NotNull(a);
            Assert.Equal("DejaVu Sans", a.faceInfo.familyName);
        }

        [Fact]
        public void ALocalizedFamilyNameFindsNothing()
        {
            RequireDejaVu();
            Assert.Null(TMP_FontAsset.CreateFontAsset("IPAゴシック", "", 60));
        }

        [Fact]
        public void ANewAssetByNameHasTheMeasuredAtlasMaterialSettingsAndTables()
        {
            RequireDejaVu();
            var a = TMP_FontAsset.CreateFontAsset("dejavu sans", "book", 60);
            Assert.NotNull(a);
            Assert.Equal("", a.name);
            Assert.Equal(HideFlags.None, a.hideFlags);
            Assert.Equal("1.1.0", a.version);
            Assert.Null(a.sourceFontFile);

            var fi = a.faceInfo;
            Assert.Equal("DejaVu Sans", fi.familyName);
            Assert.Equal("Book", fi.styleName);
            Assert.Equal(60f, fi.pointSize);
            Assert.Equal(1f, fi.scale);
            Assert.Equal(69.84375f, fi.lineHeight, 3);
            Assert.Equal(55.6933556f, fi.ascentLine, 3);
            Assert.Equal(-14.15039f, fi.descentLine, 3);
            Assert.Equal(44f, fi.capLine, 3);
            Assert.Equal(33f, fi.meanLine, 3);
            Assert.Equal(19f, fi.tabWidth, 3);

            Assert.Equal(1024, a.atlasWidth);
            Assert.Equal(1024, a.atlasHeight);
            Assert.Equal(9, a.atlasPadding);
            Assert.Equal(GlyphRenderMode.SDFAA, a.atlasRenderMode);
            Assert.Equal(AtlasPopulationMode.DynamicOS, a.atlasPopulationMode);
            Assert.True(a.isMultiAtlasTexturesEnabled);
            var tex = Assert.Single(a.atlasTextures);
            Assert.Same(tex, a.atlasTexture);
            Assert.Equal(1, tex.width);
            Assert.Equal(1, tex.height);
            Assert.Equal(TextureFormat.Alpha8, tex.format);
            Assert.Equal("", tex.name);

            var m = a.material;
            Assert.Equal("TextMeshPro/Mobile/Distance Field", m.shader.name);
            Assert.Equal("TextMeshPro/Mobile/Distance Field", m.name);
            Assert.Same(tex, m.GetTexture("_MainTex"));
            Assert.Equal(1024f, m.GetFloat("_TextureWidth"));
            Assert.Equal(1024f, m.GetFloat("_TextureHeight"));
            Assert.Equal(10f, m.GetFloat("_GradientScale"));
            Assert.Equal(0f, m.GetFloat("_WeightNormal"));
            Assert.Equal(0.75f, m.GetFloat("_WeightBold"));

            var cs = a.creationSettings;
            Assert.True(string.IsNullOrEmpty(cs.sourceFontFileGUID));
            Assert.Equal(0, cs.pointSize);
            Assert.Equal(0, cs.padding);
            Assert.Equal(0, cs.atlasWidth);
            Assert.Equal(0, cs.atlasHeight);
            Assert.Equal(0, cs.renderMode);
            Assert.False(cs.includeFontFeatures);

            Assert.Empty(a.characterTable);
            Assert.Empty(a.glyphTable);
            Assert.Null(a.fallbackFontAssetTable);
            Assert.Equal(0f, a.normalStyle);
            Assert.Equal(0f, a.normalSpacingOffset);
            Assert.Equal(0.75f, a.boldStyle);
            Assert.Equal(7f, a.boldSpacing);
            Assert.Equal(35, a.italicStyle);
            Assert.Equal(10, a.tabSize);
            Assert.Equal(10, a.fontWeightTable.Length);
        }

        [Fact]
        public void EachCallMakesANewAssetWithItsOwnAtlas()
        {
            RequireDejaVu();
            var a = TMP_FontAsset.CreateFontAsset("DejaVu Sans", "Book");
            var b = TMP_FontAsset.CreateFontAsset("DejaVu Sans", "Book");
            Assert.NotSame(a, b);
            Assert.NotSame(a.atlasTexture, b.atlasTexture);
            // The default point size is 90.
            Assert.Equal(90f, a.faceInfo.pointSize);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void APointSizeOfZeroOrBelowLoadsTheFaceAtItsUnitsPerEm(int pointSize)
        {
            RequireDejaVu();
            var a = TMP_FontAsset.CreateFontAsset("DejaVu Sans", "Book", pointSize);
            Assert.NotNull(a);
            Assert.Equal(2048f, a.faceInfo.pointSize);
            Assert.Equal(2384f, a.faceInfo.lineHeight, 3);
            Assert.Equal(1901f, a.faceInfo.ascentLine, 3);
        }

        [Theory]
        [InlineData(60, 6, 512, 7f)]
        [InlineData(90, 9, 1024, 10f)]
        public void AnAssetFromAFileIsDynamicWithTheGivenAtlas(int size, int padding, int atlas, float gradientScale)
        {
            RequireDejaVu();
            TMP_FontAsset a = null;
            var logs = Logged(() => a = TMP_FontAsset.CreateFontAsset(DejaVuSansPath, 0, size, padding, GlyphRenderMode.SDFAA, atlas, atlas));
            Assert.Empty(logs);
            Assert.NotNull(a);
            Assert.Equal("", a.name);
            Assert.Null(a.sourceFontFile);
            Assert.Equal(AtlasPopulationMode.Dynamic, a.atlasPopulationMode);
            Assert.Equal((float)size, a.faceInfo.pointSize);
            Assert.Equal("DejaVu Sans", a.faceInfo.familyName);
            Assert.Equal(atlas, a.atlasWidth);
            Assert.Equal(atlas, a.atlasHeight);
            Assert.Equal(padding, a.atlasPadding);
            Assert.True(a.isMultiAtlasTexturesEnabled);
            var tex = Assert.Single(a.atlasTextures);
            Assert.Equal(1, tex.width);
            Assert.Equal(1, tex.height);
            Assert.Equal(TextureFormat.Alpha8, tex.format);
            Assert.Equal(gradientScale, a.material.GetFloat("_GradientScale"));
            Assert.Equal((float)atlas, a.material.GetFloat("_TextureWidth"));
            Assert.Equal((float)atlas, a.material.GetFloat("_TextureHeight"));
            Assert.Equal("TextMeshPro/Mobile/Distance Field", a.material.name);
            Assert.Equal(0, a.creationSettings.pointSize);
            Assert.Null(a.fallbackFontAssetTable);
        }

        [Theory]
        [InlineData("/usr/share/fonts/truetype/dejavu/NoSuchFont.ttf", 0)]
        [InlineData("/etc/hostname", 0)]
        [InlineData("", 0)]
        [InlineData(DejaVuSansPath, 1)]
        public void AFileThatCannotBeLoadedReturnsNullAndLogsOneLine(string path, int faceIndex)
        {
            RequireDejaVu();
            TMP_FontAsset a = null;
            var logs = Logged(() => a = TMP_FontAsset.CreateFontAsset(path, faceIndex, 60, 6, GlyphRenderMode.SDFAA, 512, 512));
            Assert.Null(a);
            Assert.Equal(new[] { ($"Unable to load font face from [{path}].", LogType.Log) }, logs);
        }

        [Fact]
        public void ABitmapRenderModeThrowsArgumentNull()
        {
            RequireDejaVu();
            var e = Assert.Throws<System.ArgumentNullException>(() => TMP_FontAsset.CreateFontAsset(DejaVuSansPath, 0, 32, 4, GlyphRenderMode.RASTER_HINTED, 256, 256));
            Assert.Equal("Value cannot be null.", e.Message);
        }

        static TextMeshProUGUI NoWrapText(string text, TMP_FontAsset font)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(200f, 50f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = 16f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.text = text;
            t.ForceMeshUpdate(false, false);
            return t;
        }

        static string[] Table(TMP_FontAsset a) => a.characterTable.Select(c => "U+" + c.unicode.ToString("X4")).ToArray();

        [Fact]
        public void TheFirstTextAddsEllipsisAndUnderscoreThenItsCharacters()
        {
            RequireDejaVu();
            var a = TMP_FontAsset.CreateFontAsset("DejaVu Sans", "Book", 60);

            var t = NoWrapText("↻", a);
            Assert.Equal(new[] { "U+2026", "U+005F", "U+21BB" }, Table(a));
            Assert.Equal(1024, a.atlasTexture.width);
            Assert.Equal(1024, a.atlasTexture.height);
            Assert.Single(a.atlasTextures);
            Assert.Equal(1, t.textInfo.characterCount);
            var c = t.textInfo.characterInfo[0];
            Assert.Same(a, c.fontAsset);
            Assert.Equal(13.4083328f, c.xAdvance - c.origin, 2);
            Assert.Equal(13.41f, t.preferredWidth, 2);
            Assert.Equal(18.63f, t.preferredHeight, 2);

            var t2 = NoWrapText("A", a);
            Assert.Equal(new[] { "U+2026", "U+005F", "U+21BB", "U+0041" }, Table(a));
            Assert.Equal(10.95f, t2.preferredWidth, 2);
        }

        [Fact]
        public void ACharacterTheFaceLacksComesFromTheGlobalFallbacksAndIsNotAdded()
        {
            RequireDejaVu();
            var a = TMP_FontAsset.CreateFontAsset("DejaVu Sans", "Book", 60);
            NoWrapText("↻", a);
            var before = Table(a);
            var t = NoWrapText("가", a);
            Assert.Equal(before, Table(a));
            var c = t.textInfo.characterInfo[0];
            Assert.NotSame(a, c.fontAsset);

            Assert.True(a.HasCharacter('↻'));
            Assert.True(a.TryAddCharacters("↻Z", out string missing));
            Assert.Equal("", missing);
            Assert.False(a.TryAddCharacters("가", out missing));
            Assert.Equal("가", missing);
        }
    }
}
