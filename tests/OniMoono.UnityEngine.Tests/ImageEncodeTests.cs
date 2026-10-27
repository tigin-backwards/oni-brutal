using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Xunit;

namespace OniMoono.Tests
{
    /// ImageConversion's JPG and TGA encoders (and EncodeToPNG's refusals), as the original player
    /// gives them (tools/UnityProbe/JpgEncode.cs).
    public class ImageEncodeTests : EngineTest
    {
        // Red runs with x, green with y, blue is high in the top quarter, alpha runs with x + y
        // from 0 to 255 (the probe's gradient).
        static Texture2D Gradient(int w, int h, TextureFormat format)
        {
            var t = new Texture2D(w, h, format, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color((float)x / (w - 1), (float)y / (h - 1), y >= h * 3 / 4 ? 0.9f : 0.1f, (float)(x + y) / (w + h - 2));
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// The JPEG frame header's component count and the first component's sampling factors.
        static (int components, int sampling) Frame(byte[] jpg)
        {
            int i = 2;
            while (i + 4 <= jpg.Length && jpg[i] == 0xFF)
            {
                int marker = jpg[i + 1], length = (jpg[i + 2] << 8) | jpg[i + 3];
                if (marker is 0xC0 or 0xC1 or 0xC2) return (jpg[i + 9], jpg[i + 11]);
                i += 2 + length;
            }
            throw new InvalidOperationException("no frame header");
        }

        static List<string> CaptureLogs(Action action)
        {
            var logs = new List<string>();
            Application.LogCallback log = (message, _, _) => logs.Add(message);
            Application.logMessageReceived += log;
            try { action(); }
            finally { Application.logMessageReceived -= log; }
            return logs;
        }

        // The JPEG decodes to the texture's colours within JPEG error, top row first in the file
        // (LoadImage puts it back at the top), alpha dropped rather than blended.
        [Fact]
        public void JpgDecodesToTheTexturesColoursWithoutAlpha()
        {
            var source = Gradient(64, 48, TextureFormat.RGBA32);
            var jpg = source.EncodeToJPG();
            Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, jpg[..3]);
            Assert.Equal((3, 0x22), Frame(jpg));
            var loaded = new Texture2D(2, 2);
            Assert.True(loaded.LoadImage(jpg));
            Assert.Equal(TextureFormat.RGB24, loaded.format);
            Assert.Equal((64, 48), (loaded.width, loaded.height));
            var a = source.GetPixels32();
            var b = loaded.GetPixels32();
            long total = 0;
            int max = 0;
            for (int i = 0; i < a.Length; i++)
                foreach (var (x, y) in new[] { (a[i].r, b[i].r), (a[i].g, b[i].g), (a[i].b, b[i].b) })
                {
                    int d = Math.Abs(x - y);
                    total += d;
                    max = Math.Max(max, d);
                }
            Assert.True(total / (3.0 * a.Length) < 4, $"mean error {total / (3.0 * a.Length)}");
            Assert.True(max < 64, $"max error {max}");
            // The bottom-left texel (alpha 0) keeps its colour: dark red, dark green, blue 0.1.
            Assert.InRange(b[0].r, 0, 24);
            Assert.InRange(b[0].g, 0, 24);
            Assert.InRange(b[0].b, 10, 40);
            Assert.InRange(b[^1].b, 200, 255);
        }

        // Quality defaults to 75 and is clamped to 1..100 (the original's bytes are equal for -5,
        // 0 and 1, and for 100, 101 and 1000).
        [Fact]
        public void JpgQualityDefaultsTo75AndIsClamped()
        {
            var t = Gradient(64, 48, TextureFormat.RGBA32);
            Assert.Equal(t.EncodeToJPG(75), t.EncodeToJPG());
            Assert.Equal(t.EncodeToJPG(1), t.EncodeToJPG(0));
            Assert.Equal(t.EncodeToJPG(1), t.EncodeToJPG(-5));
            Assert.Equal(t.EncodeToJPG(100), t.EncodeToJPG(101));
            Assert.Equal(t.EncodeToJPG(100), t.EncodeToJPG(1000));
            Assert.True(t.EncodeToJPG(1).Length < t.EncodeToJPG(75).Length);
            Assert.True(t.EncodeToJPG(75).Length < t.EncodeToJPG(100).Length);
        }

        // Single-channel red formats are greyscale JPEGs; other formats RGB; Alpha8 black. The
        // original writes grey as one component; stb writes three, the chroma flat.
        [Theory]
        [InlineData(TextureFormat.R8, 1)]
        [InlineData(TextureFormat.R16, 1)]
        [InlineData(TextureFormat.RHalf, 1)]
        [InlineData(TextureFormat.RFloat, 1)]
        [InlineData(TextureFormat.RGB24, 3)]
        [InlineData(TextureFormat.RG16, 3)]
        [InlineData(TextureFormat.RGB565, 3)]
        [InlineData(TextureFormat.RGBA4444, 3)]
        [InlineData(TextureFormat.ARGB32, 3)]
        [InlineData(TextureFormat.BGRA32, 3)]
        [InlineData(TextureFormat.RGBAHalf, 3)]
        [InlineData(TextureFormat.RGBAFloat, 3)]
        [InlineData(TextureFormat.Alpha8, 3)]
        public void JpgComponentsFollowTheFormat(TextureFormat format, int components)
        {
            var t = Gradient(16, 16, format);
            var jpg = t.EncodeToJPG();
            Assert.Equal(3, Frame(jpg).components);
            var loaded = new Texture2D(2, 2);
            Assert.True(loaded.LoadImage(jpg));
            var top = loaded.GetPixels32()[^1]; // top right: red 1, green 1
            if (format == TextureFormat.Alpha8) Assert.True(top.r < 8 && top.g < 8 && top.b < 8, top.ToString());
            else Assert.True(top.r > 200, top.ToString());
            if (components == 1)
                foreach (var c in loaded.GetPixels32())
                    Assert.True(Math.Abs(c.r - c.g) <= 2 && Math.Abs(c.r - c.b) <= 2, c.ToString());
            else if (format != TextureFormat.Alpha8) Assert.True(top.g > 200, top.ToString());
        }

        // Compressed formats are refused with the original's error; unreadable textures throw.
        [Fact]
        public void EncodersRefuseCompressedAndUnreadableTextures()
        {
            var dxt = new Texture2D(8, 8, TextureFormat.DXT1, false);
            const string error = "Unsupported texture format - Texture2D::EncodeTo functions do not support compressed texture formats.";
            Assert.Equal(new[] { error }, CaptureLogs(() => Assert.Null(dxt.EncodeToJPG())));
            Assert.Equal(new[] { error }, CaptureLogs(() => Assert.Null(dxt.EncodeToTGA())));
            Assert.Equal(new[] { error }, CaptureLogs(() => Assert.Null(dxt.EncodeToPNG())));
            var locked = Gradient(4, 4, TextureFormat.RGBA32);
            locked.name = "locked";
            locked.Apply(false, true);
            var e = Assert.Throws<ArgumentException>(() => locked.EncodeToJPG());
            Assert.Equal("Texture 'locked' is not readable. For this reason, scripts cannot access the memory allocated to it. You can make this texture readable in the Texture Import Settings.", e.Message);
            Assert.Throws<ArgumentException>(() => locked.EncodeToTGA());
            Assert.Throws<ArgumentException>(() => locked.EncodeToPNG());
        }

        // Uncompressed TGA, rows bottom to top in BGR(A), no alpha bits in the descriptor; grey
        // (type 3) for single-channel red formats (the original's headers and sizes).
        [Fact]
        public void TgaIsUncompressedBottomUpBgra()
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.SetPixels32(new Color32[] { new(1, 2, 3, 4), new(5, 6, 7, 8), new(9, 10, 11, 12), new(13, 14, 15, 16) });
            t.Apply();
            Assert.Equal(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 2, 0, 32, 0,
                3, 2, 1, 4, 7, 6, 5, 8, 11, 10, 9, 12, 15, 14, 13, 16 }, t.EncodeToTGA());
            var rgb = new Texture2D(1, 1, TextureFormat.RGB24, false);
            rgb.SetPixels32(new Color32[] { new(0, 0, 230, 255) });
            rgb.Apply();
            Assert.Equal(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 24, 0, 230, 0, 0 }, rgb.EncodeToTGA());
            var tga = Gradient(16, 16, TextureFormat.RHalf).EncodeToTGA();
            Assert.Equal(18 + 256, tga.Length);
            Assert.Equal(3, tga[2]);
            Assert.Equal(8, tga[16]);
            Assert.Equal(1042, Gradient(16, 16, TextureFormat.Alpha8).EncodeToTGA().Length);
            Assert.Equal(786, Gradient(16, 16, TextureFormat.RG16).EncodeToTGA().Length);
            Assert.Equal(new byte[] { 0, 0, 0, 128 }, Gradient(16, 16, TextureFormat.Alpha8).EncodeToTGA()[(18 + 4 * 15)..(18 + 4 * 16)]);
        }

        // The encoders read two-channel formats with blue 1, RGB565 with its bits repeated
        // (5-bit 28 is 231) and float channels rounded half up (17/30 is 145), as the original's
        // TGAs and PNGs of the probe's textures have them.
        [Fact]
        public void EncodersReadTexelsAsTheOriginal()
        {
            static byte[] Body(TextureFormat format, byte[] raw)
            {
                var t = new Texture2D(1, 1, format, false);
                t.LoadRawTextureData(raw);
                t.Apply();
                return t.EncodeToTGA()[18..];
            }
            Assert.Equal(new byte[] { 255, 20, 10 }, Body(TextureFormat.RG16, new byte[] { 10, 20 }));
            Assert.Equal(new byte[] { 231, 0, 0 }, Body(TextureFormat.RGB565, new byte[] { 28, 0 }));
            Assert.Equal(new byte[] { 0, 255, 0 }, Body(TextureFormat.RGB565, new byte[] { 0xE0, 0x07 }));
            var f = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            f.SetPixels(new[] { new Color(0, 0, 0, 17 / 30f) });
            f.Apply();
            Assert.Equal(145, f.EncodeToTGA()[21]);
            var png = new Texture2D(1, 1);
            Assert.True(png.LoadImage(Gradient(16, 16, TextureFormat.RG16).EncodeToPNG()));
            Assert.Equal(255, png.GetPixels32()[0].b);
        }

        // The array encoders read raw rows as EncodeArrayToPNG does.
        [Fact]
        public void ArrayEncodersReadRawRows()
        {
            var rows = new byte[] { 255, 0, 0, 0, 255, 0, 9, 9, 0, 0, 255, 10, 20, 30, 9, 9 };
            Assert.Equal(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 2, 0, 24, 0, 0, 0, 255, 0, 255, 0, 255, 0, 0, 30, 20, 10 },
                ImageConversion.EncodeArrayToTGA(rows, GraphicsFormat.R8G8B8_UNorm, 2, 2, 8));
            var jpg = ImageConversion.EncodeArrayToJPG(rows, GraphicsFormat.R8G8B8_UNorm, 2, 2, 8);
            var loaded = new Texture2D(1, 1);
            Assert.True(loaded.LoadImage(jpg));
            Assert.Equal((2, 2), (loaded.width, loaded.height));
            Assert.Equal(ImageConversion.EncodeArrayToJPG(rows, GraphicsFormat.R8G8B8_UNorm, 2, 2, 8, 75), jpg);
        }
    }
}
