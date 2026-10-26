using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OniMoono.Engine.Rendering;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    /// Texel reads of BC6H, BC7, ETC1, ETC2, EAC and ASTC blocks against independent decoders:
    /// random blocks (every BC6H and BC7 mode), and ASTC blocks ARM's astcenc encoded from varied
    /// images at every block size, with their reference texels in TestData/BlockDecoding.bin
    /// (tools/BlockDecodingFixtures/make.py: texture2ddecoder, Pillow for BC6H and BC7, astcenc for
    /// ASTC). The
    /// references give 8-bit channels. Where texture2ddecoder departs from the specifications, the
    /// specification is followed and the comparison says so: reserved BC7 blocks (all zeroes, as
    /// the Direct3D specification says; both references return opaque black), BC6H (Pillow agrees with this decoder on every texel; texture2ddecoder
    /// differs on about 100 of 28,800), transparent punch-through texels (all channels 0) and
    /// signed EAC's base of -128 (read as -127).
    public class BlockDecodingTests
    {
        sealed record Case(string Format, int Width, int Height, byte[] Block, byte[] Reference, byte[] Pillow);

        static List<Case> Load()
        {
            var cases = new List<Case>();
            using var r = new BinaryReader(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "BlockDecoding.bin")));
            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                string name = Encoding.ASCII.GetString(r.ReadBytes(r.ReadByte()));
                int size = r.ReadInt32(), width = r.ReadInt32(), height = r.ReadInt32(), count = r.ReadInt32();
                bool pillow = r.ReadByte() != 0;
                int texelBytes = r.ReadByte();
                for (int i = 0; i < count; i++)
                    cases.Add(new Case(name, width, height, r.ReadBytes(size), r.ReadBytes(width * height * texelBytes), pillow ? r.ReadBytes(width * height * 4) : null));
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
                for (int y = 0; y < c.Height; y++)
                    for (int x = 0; x < c.Width; x++)
                    {
                        var color = TextureFormats.Decode(Enum.Parse<TextureFormat>(format.Split(' ')[0]), c.Block, c.Width, x, y);
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

        static int[] Bgra(byte[] texels, int width, int x, int y) { int i = (y * width + x) * 4; return [texels[i + 2], texels[i + 1], texels[i], texels[i + 3]]; }
        static int[] Rgba(byte[] texels, int width, int x, int y) { int i = (y * width + x) * 4; return [texels[i], texels[i + 1], texels[i + 2], texels[i + 3]]; }

        static int[] Rgba8(Color c) => [To8(c.r), To8(c.g), To8(c.b), To8(c.a)];
        static int[] Rgb8(Color c) => [To8(c.r), To8(c.g), To8(c.b)];

        [Fact]
        public void Bc7MatchesTheReferenceDecoders()
        {
            string report = Compare("BC7", Rgba8, (c, x, y) => c.Block[0] == 0 ? null : Bgra(c.Reference, c.Width, x, y), 0, false)
                            + Compare("BC7", Rgba8, (c, x, y) => c.Block[0] == 0 ? null : Rgba(c.Pillow, c.Width, x, y), 0, false);
            Assert.True(report.Length == 0, report);
        }

        [Fact]
        public void Bc6hMatchesTheReferenceDecoders()
        {
            string report = Compare("BC6H", Rgb8, (c, x, y) => Rgba(c.Pillow, c.Width, x, y)[..3], 1, true);
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
                var r = Bgra(c.Reference, c.Width, x, y);
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
                (c, x, y) => signed && (c.Block[0] == 0x80 || (channels == 2 && c.Block[8] == 0x80)) ? null : Bgra(c.Reference, c.Width, x, y)[..channels], 1, false);
            Assert.True(report.Length == 0, report);
        }

        [Theory]
        [InlineData("ASTC_4x4")]
        [InlineData("ASTC_5x5")]
        [InlineData("ASTC_6x6")]
        [InlineData("ASTC_8x8")]
        [InlineData("ASTC_10x10")]
        [InlineData("ASTC_12x12")]
        public void AstcMatchesTheReferenceDecoder(string format)
        {
            string report = Compare(format, Rgba8, (c, x, y) => Bgra(c.Reference, c.Width, x, y), 0, false);
            Assert.True(report.Length == 0, report);
        }

        [Theory]
        [InlineData("PVRTC_RGBA4 8x8")]
        [InlineData("PVRTC_RGBA4 16x16")]
        [InlineData("PVRTC_RGBA4 32x16")]
        [InlineData("PVRTC_RGBA4 16x32")]
        [InlineData("PVRTC_RGBA2 16x8")]
        [InlineData("PVRTC_RGBA2 16x16")]
        [InlineData("PVRTC_RGBA2 32x16")]
        [InlineData("PVRTC_RGBA2 16x32")]
        public void PvrtcMatchesTheReferenceDecoder(string level)
        {
            string report = Compare(level, Rgba8, (c, x, y) => Bgra(c.Reference, c.Width, x, y), 0, false);
            Assert.True(report.Length == 0, report);
        }

        static int[] HalfBits(Color c) => [Bits(c.r), Bits(c.g), Bits(c.b), Bits(c.a)];
        static int Bits(float v) => BitConverter.HalfToUInt16Bits((Half)v);

        [Theory]
        [InlineData("ASTC_HDR_4x4")]
        [InlineData("ASTC_HDR_6x6")]
        [InlineData("ASTC_HDR_8x8")]
        [InlineData("ASTC_HDR_12x12")]
        public void AstcHdrMatchesTheReferenceDecoder(string format)
        {
            // Half floats compared bit for bit.
            string report = Compare(format, HalfBits, (c, x, y) =>
            {
                int i = (y * c.Width + x) * 8;
                return [BitConverter.ToUInt16(c.Reference, i), BitConverter.ToUInt16(c.Reference, i + 2), BitConverter.ToUInt16(c.Reference, i + 4), BitConverter.ToUInt16(c.Reference, i + 6)];
            }, 0, false);
            Assert.True(report.Length == 0, report);
        }

        [Fact]
        public unsafe void LevelsDecodeForUploadAsTheirTexelReads()
        {
            // Two blocks across, one down: an ETC2 RGBA8 level of 8×4 texels.
            var level = new byte[32];
            new System.Random(3).NextBytes(level);
            var rgba = new byte[8 * 4 * 4];
            fixed (byte* l = level, o = rgba)
                TextureFormats.DecodeLevel(TextureFormat.ETC2_RGBA8, l, level.Length, 8, 4, o, halfFloat: false);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 8; x++)
                {
                    Color32 c = TextureFormats.Decode(TextureFormat.ETC2_RGBA8, level, 8, x, y);
                    int i = (y * 8 + x) * 4;
                    Assert.Equal(new Color32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]), c);
                }
        }

        [Fact]
        public void PvrtcWithoutAlphaReadsOpaque()
        {
            // Translucent colours with alpha 0 in every word.
            var level = new byte[32];
            for (int i = 0; i < 4; i++) level[i * 8 + 5] = 0x0F;
            Assert.Equal(0f, TextureFormats.Decode(TextureFormat.PVRTC_RGBA4, level, 8, 3, 3).a);
            Assert.Equal(1f, TextureFormats.Decode(TextureFormat.PVRTC_RGB4, level, 8, 3, 3).a);
        }

        static byte[] AstcVoidExtent(bool hdr, ushort r, ushort g, ushort b, ushort a)
        {
            // Block mode 0x1FC, reserved bits 10–11 set, no extent (all ones), then RGBA.
            ulong low = 0x1FCUL | (hdr ? 0x200UL : 0) | (3UL << 10) | (((1UL << 52) - 1) << 12);
            var block = new byte[16];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(block, low);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(8), r);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(10), g);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(12), b);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(14), a);
            return block;
        }

        [Fact]
        public void AstcConstantColourBlocksReadTheirColour()
        {
            var c = TextureFormats.Decode(TextureFormat.ASTC_6x6, AstcVoidExtent(false, 0x8000, 0xFFFF, 0x0000, 0x40FF), 6, 5, 3);
            Assert.Equal(new Color(128f / 255f, 1f, 0f, 64f / 255f), c);
        }

        [Fact]
        public void AstcBlocksTheLdrProfileCannotDecodeReadAsMagenta()
        {
            // An HDR constant colour.
            Assert.Equal(new Color(1f, 0f, 1f, 1f), TextureFormats.Decode(TextureFormat.ASTC_4x4, AstcVoidExtent(true, 0x3C00, 0, 0, 0x3C00), 4, 1, 1));
            // A reserved block mode (low bits 00 with R = 0).
            Assert.Equal(new Color(1f, 0f, 1f, 1f), TextureFormats.Decode(TextureFormat.ASTC_4x4, new byte[16], 4, 0, 0));
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
