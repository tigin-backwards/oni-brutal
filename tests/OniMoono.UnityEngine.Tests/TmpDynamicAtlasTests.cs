using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Xunit;

namespace OniMoono.Tests
{
    // Dynamic font assets filled with TryAddCharacters, as ONI Vietnamese Beautiful Font
    // (2574634278) builds its fonts: the atlas texels and glyph metrics are pinned by hash.
    public class TmpDynamicAtlasTests : EngineTest
    {
        const string DejaVuSansPath = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";

        // ASCII and the Vietnamese letters (precomposed, both cases).
        static readonly string Characters = Ascii() +
            "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúýĂăĐđĨĩŨũƠơƯư" +
            "ẠạẢảẤấẦầẨẩẪẫẬậẮắẰằẲẳẴẵẶặẸẹẺẻẼẽẾếỀềỂểỄễỆệỈỉỊịỌọỎỏỐốỒồỔổỖỗỘộỚớỜờỞởỠỡỢợỤụỦủỨứỪừỬửỮữỰựỲỳỴỵỶỷỸỹ";

        static string Ascii()
        {
            var b = new StringBuilder();
            for (char c = ' '; c <= '~'; c++) b.Append(c);
            return b.ToString();
        }

        // SHA-256 of the atlas texels, the glyphs (index, metrics, rect, atlas), the characters
        // and the kerning pairs.
        static string Hash(TMP_FontAsset a)
        {
            var b = new StringBuilder();
            foreach (var t in a.atlasTextures)
                b.Append(Convert.ToHexString(SHA256.HashData(t.GetRawTextureData()))).Append(' ').Append(t.width).Append('x').Append(t.height).Append('\n');
            foreach (var g in a.glyphTable)
            {
                var m = g.metrics;
                var r = g.glyphRect;
                b.Append($"{g.index} {m.width:R} {m.height:R} {m.horizontalBearingX:R} {m.horizontalBearingY:R} {m.horizontalAdvance:R} {r.x} {r.y} {r.width} {r.height} {g.atlasIndex}\n");
            }
            foreach (var c in a.characterTable) b.Append($"{c.unicode}:{c.glyphIndex} ");
            b.Append('\n');
            foreach (var p in a.fontFeatureTable.glyphPairAdjustmentRecords)
            {
                GlyphAdjustmentRecord f = p.firstAdjustmentRecord, s = p.secondAdjustmentRecord;
                b.Append($"{f.glyphIndex} {Value(f.glyphValueRecord)} {s.glyphIndex} {Value(s.glyphValueRecord)}\n");
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(b.ToString())));
        }

        static string Value(GlyphValueRecord v) => $"{v.xPlacement:R},{v.yPlacement:R},{v.xAdvance:R},{v.yAdvance:R}";

        static TMP_FontAsset Fill(GlyphRenderMode mode, int size, int padding, int atlas, string characters)
        {
            if (!File.Exists(DejaVuSansPath)) Assert.Skip("DejaVu Sans is not installed.");
            TmpTestSupport.LoadGameData();
            var a = TMP_FontAsset.CreateFontAsset(DejaVuSansPath, 0, size, padding, mode, atlas, atlas);
            Assert.NotNull(a);
            Assert.True(a.TryAddCharacters(characters, out string missing), missing);
            return a;
        }

        // Taken before glyphs were rendered ahead in parallel, their distance fields skipped
        // unchanged pixels and kerning was read per glyph: none of that may change the asset.
        [Theory]
        [InlineData(GlyphRenderMode.SDFAA, 90, 9, 1024, "616B51511D4D1681F9E555C84D028800B49AA682B2CA75A383163623E242EA87")]
        [InlineData(GlyphRenderMode.SDF, 90, 9, 1024, "4797C3C61CD6B741FF7617878FCE72008890D6D5DD0BD4676BBF1D9C62078C6A")]
        [InlineData(GlyphRenderMode.SDFAA_HINTED, 60, 6, 512, "A6AEE6AE39AB485D00ECF26B473E8C37B76D1534D25B80FC5A5CBFFB00F70A5B")]
        [InlineData(GlyphRenderMode.SDF8, 36, 5, 512, "7F8FAAF0A559519802B9BF148BE4348FBB5CAF5ACFCED54819E8B316497B36DB")]
        public void TheAtlasAndGlyphsOfAFilledAssetAreUnchanged(GlyphRenderMode mode, int size, int padding, int atlas, string expected)
        {
            var chars = mode == GlyphRenderMode.SDF8 ? "AgWé@ỹ" : Characters;
            var a = Fill(mode, size, padding, atlas, chars);
            Assert.NotEmpty(a.fontFeatureTable.glyphPairAdjustmentRecords);
            Assert.Equal(expected, Hash(a));
        }

        // A character at a time (each rendered when added) fills the asset as all at once does.
        [Fact]
        public void CharactersAddedOneByOneGiveTheSameAsset()
        {
            var all = Fill(GlyphRenderMode.SDFAA, 90, 9, 1024, Characters);
            var single = Fill(GlyphRenderMode.SDFAA, 90, 9, 1024, "");
            foreach (char c in Characters) Assert.True(single.TryAddCharacters(c.ToString()));
            Assert.Equal(Hash(all), Hash(single));
        }
    }
}
