using System;
using Brutal.VulkanApi;
using OniMoono.Desktop.Vulkan;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    /// The Vulkan device's texture formats: each reads the values OpenGL's format reads.
    public unsafe class VkFormatsTests
    {
        static byte[] Convert(TexelConversion c, TextureFormat f, byte[] texels, int w, int h)
        {
            byte[] scratch = null;
            fixed (byte* p = texels) return VkFormats.Convert(c, f, p, texels.Length, w, h, ref scratch).ToArray();
        }

        [Fact]
        public void Rgb24GainsAnOpaqueAlpha()
        {
            Assert.Equal([1, 2, 3, 255, 4, 5, 6, 255], Convert(TexelConversion.Rgb24ToRgba32, TextureFormat.RGB24, [1, 2, 3, 4, 5, 6], 2, 1));
            Assert.Equal(TexelConversion.Rgb24ToRgba32, VkFormats.Of(TextureFormat.RGB24, true).Conversion);
        }

        [Fact]
        public void FourBitChannelsWidenExactly()
        {
            // RGBA4444 (OpenGL RGBA, 4_4_4_4): red in the top nibble.
            ushort rgba = 0x1F80; // r 1, g 15, b 8, a 0
            Assert.Equal([17, 255, 136, 0], Convert(TexelConversion.Rgba4444ToRgba32, TextureFormat.RGBA4444, BitConverter.GetBytes(rgba), 1, 1));
            // ARGB4444 (OpenGL BGRA, 4_4_4_4_REV): blue in the bottom nibble, alpha on top.
            ushort argb = 0xA123; // a 10, r 1, g 2, b 3
            Assert.Equal([17, 34, 51, 170], Convert(TexelConversion.Argb4444ToRgba32, TextureFormat.ARGB4444, BitConverter.GetBytes(argb), 1, 1));
        }

        [Fact]
        public void SwizzlesMatchOpenGLs()
        {
            var argb = VkFormats.Of(TextureFormat.ARGB32, true).Swizzle;
            Assert.Equal((VkComponentSwizzle.G, VkComponentSwizzle.B, VkComponentSwizzle.A, VkComponentSwizzle.R), (argb.R, argb.G, argb.B, argb.A));
            var alpha = VkFormats.Of(TextureFormat.Alpha8, true).Swizzle;
            Assert.Equal((VkComponentSwizzle.Zero, VkComponentSwizzle.Zero, VkComponentSwizzle.Zero, VkComponentSwizzle.R), (alpha.R, alpha.G, alpha.B, alpha.A));
            // DXT1 reads alpha 1, as OpenGL's COMPRESSED_RGB_S3TC_DXT1.
            Assert.Equal(VkFormat.Bc1RGBUNormBlock, VkFormats.Of(TextureFormat.DXT1, true).Format);
            // Without BC sampling, decoded as OpenGL's device decodes them.
            Assert.Equal(TexelConversion.Decode, VkFormats.Of(TextureFormat.DXT5, false).Conversion);
        }

        [Fact]
        public void ReadbacksAreOpenGLsFloats()
        {
            byte[] rgba = [255, 0, 51, 128];
            fixed (byte* p = rgba) Assert.Equal(new Color(1f, 0f, 0.2f, 128 / 255f), VkFormats.ReadTexel(VkFormat.R8G8B8A8UNorm, p));
            byte[] r = [51];
            fixed (byte* p = r) Assert.Equal(new Color(0.2f, 0f, 0f, 1f), VkFormats.ReadTexel(VkFormat.R8UNorm, p));
            var half = BitConverter.GetBytes(BitConverter.HalfToUInt16Bits((Half)0.5f));
            fixed (byte* p = half) Assert.Equal(new Color(0.5f, 0f, 0f, 1f), VkFormats.ReadTexel(VkFormat.R16SFloat, p));
        }
    }
}
