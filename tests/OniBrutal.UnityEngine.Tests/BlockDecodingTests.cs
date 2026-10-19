using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OniBrutal.Engine.Rendering;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    /// Texel reads of BC6H, BC7, ETC1, ETC2 and EAC blocks against independent decoders: random
    /// blocks (every BC6H and BC7 mode) with their reference texels in TestData/BlockDecoding.bin
    /// (tools/BlockDecodingFixtures/make.py: texture2ddecoder, and Pillow for BC6H and BC7). The
    /// references give 8-bit channels. Where texture2ddecoder departs from the specifications, the
    /// specification is followed and the comparison says so: reserved BC7 blocks (all zeroes, as
    /// the Direct3D specification says; both references return opaque black), BC6H (Pillow agrees with this decoder on every texel; texture2ddecoder
    /// differs on about 100 of 28,800), transparent punch-through texels (all channels 0) and
    /// signed EAC's base of -128 (read as -127).
    public class BlockDecodingTests
    {
        sealed record Case(string Format, byte[] Block, byte[] Reference, byte[] Pillow);

        static List<Case> Load()
        {
            var cases = new List<Case>();
            using var r = new BinaryReader(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "BlockDecoding.bin")));
            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                string name = Encoding.ASCII.GetString(r.ReadBytes(r.ReadByte()));
                int size = r.ReadInt32(), count = r.ReadInt32();
                for (int i = 0; i < count; i++) cases.Add(new Case(name, r.ReadBytes(size), r.ReadBytes(64), r.ReadBytes(64)));
            }
            return cases;
        }

        static int To8(float v) => (int)MathF.Round(Math.Clamp(v, 0f, 1f) * 255f);

        /// Mismatching texels per format (and BC6H/BC7 mode), with a first example of each.
        /// `reference` gives the reference channels for a texel, or null to skip it.
        static string Compare(string format, Func<Color, int[]> expectedChannels, Func<Case, int, int, int[]> reference, int tolerance, bool saturatingReference)
        {
            var report = new StringBuilder();
            var failures = new SortedDictionary<string, (int Count, string Example)>();
            int checkedTexels = 0;
            foreach (var c in Load())
            {
                if (c.Format != format) continue;
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        var color = TextureFormats.Decode(Enum.Parse<TextureFormat>(format), c.Block, 4, x, y);
                        var ours = expectedChannels(color);
                        var theirs = reference(c, x, y);
                        if (theirs == null) continue;
                        checkedTexels++;
                        bool same = true;
                        for (int k = 0; k < ours.Length; k++)
                        {
                            if (Math.Abs(ours[k] - theirs[k]) <= tolerance) continue;
                            // A reference that clamps to 8 bits cannot tell values above 1 apart.
                            if (saturatingReference && theirs[k] == 255 && ours[k] == 255) continue;
                            same = false;
                        }
                        if (same) continue;
                        string key = format + ModeOf(format, c.Block);
                        failures.TryGetValue(key, out var f);
                        failures[key] = (f.Count + 1, f.Example ?? $"block {Convert.ToHexString(c.Block)} texel ({x},{y}): ours {string.Join(",", ours)} ({color}), reference {string.Join(",", theirs)}");
                    }
            }
            Assert.True(checkedTexels > 0, $"no fixture texels for {format}");
            foreach (var (key, f) in failures) report.AppendLine($"{key}: {f.Count} texels differ, e.g. {f.Example}");
            return report.ToString();
        }

        static string ModeOf(string format, byte[] block)
        {
            if (format == "BC7") return $" mode {System.Numerics.BitOperations.TrailingZeroCount(block[0] | 0x100)}";
            if (format == "BC6H") return $" mode bits {((block[0] & 3) < 2 ? block[0] & 3 : block[0] & 31):X2}";
            return "";
        }

        static int[] Bgra(byte[] texels, int x, int y) { int i = (y * 4 + x) * 4; return [texels[i + 2], texels[i + 1], texels[i], texels[i + 3]]; }
        static int[] Rgba(byte[] texels, int x, int y) { int i = (y * 4 + x) * 4; return [texels[i], texels[i + 1], texels[i + 2], texels[i + 3]]; }

        static int[] Rgba8(Color c) => [To8(c.r), To8(c.g), To8(c.b), To8(c.a)];
        static int[] Rgb8(Color c) => [To8(c.r), To8(c.g), To8(c.b)];

        [Fact]
        public void Bc7MatchesTheReferenceDecoders()
        {
            string report = Compare("BC7", Rgba8, (c, x, y) => c.Block[0] == 0 ? null : Bgra(c.Reference, x, y), 0, false)
                            + Compare("BC7", Rgba8, (c, x, y) => c.Block[0] == 0 ? null : Rgba(c.Pillow, x, y), 0, false);
            Assert.True(report.Length == 0, report);
        }

        [Fact]
        public void Bc6hMatchesTheReferenceDecoders()
        {
            string report = Compare("BC6H", Rgb8, (c, x, y) => Rgba(c.Pillow, x, y)[..3], 1, true);
            Assert.True(report.Length == 0, report);
        }

        [Theory]
        [InlineData("ETC_RGB4")]
        [InlineData("ETC2_RGB")]
        [InlineData("ETC2_RGBA1")]
        [InlineData("ETC2_RGBA8")]
        public void EtcMatchesTheReferenceDecoder(string format)
        {
            string report = Compare(format, Rgba8, (c, x, y) =>
            {
                var r = Bgra(c.Reference, x, y);
                return format == "ETC2_RGBA1" && r[3] == 0 ? [0, 0, 0, 0] : r;
            }, 0, false);
            Assert.True(report.Length == 0, report);
        }

        [Theory]
        [InlineData("EAC_R")]
        [InlineData("EAC_RG")]
        [InlineData("EAC_R_SIGNED")]
        [InlineData("EAC_RG_SIGNED")]
        public void EacMatchesTheReferenceDecoder(string format)
        {
            int channels = format.StartsWith("EAC_RG") ? 2 : 1;
            bool signed = format.EndsWith("SIGNED");
            // The reference maps signed values [-1, 1] linearly onto bytes.
            int Byte(float v) => signed ? (int)MathF.Round((v * 1023f + 1023f) * 255f / 2046f) : To8(v);
            string report = Compare(format, c => channels == 2 ? [Byte(c.r), Byte(c.g)] : [Byte(c.r)],
                (c, x, y) => signed && (c.Block[0] == 0x80 || (channels == 2 && c.Block[8] == 0x80)) ? null : Bgra(c.Reference, x, y)[..channels], 1, false);
            Assert.True(report.Length == 0, report);
        }

        [Fact]
        public void ReservedBc7BlocksReadAsZeroes()
        {
            var block = new byte[16];
            block[5] = 0xA7;
            Assert.Equal(new Color(0f, 0f, 0f, 0f), TextureFormats.Decode(TextureFormat.BC7, block, 4, 2, 1));
        }

        [Fact]
        public void SignedEacReadsABaseOfMinus128AsMinus127()
        {
            // Base -128, multiplier 0, modifier table 13 (whose index 4 adds 0), every index 4.
            byte[] block = [0x80, 0x0D, 0x92, 0x49, 0x24, 0x92, 0x49, 0x24];
            Assert.Equal(-127f * 8f / 1023f, TextureFormats.Decode(TextureFormat.EAC_R_SIGNED, block, 4, 0, 0).r, 5);
        }
    }
}
