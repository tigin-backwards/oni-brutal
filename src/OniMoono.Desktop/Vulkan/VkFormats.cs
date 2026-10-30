using System;
using Brutal.VulkanApi;
using OniMoono.Engine.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace OniMoono.Desktop.Vulkan
{
    /// How texels are converted on upload when Vulkan has no format that reads them as OpenGL
    /// does: each conversion is exact (the same normalized values).
    internal enum TexelConversion
    {
        None,
        /// RGB24 to RGBA8 with alpha 255 (OpenGL's RGB8 reads alpha 1).
        Rgb24ToRgba32,
        /// RGB48 to RGBA64 with alpha 65535.
        Rgb48ToRgba64,
        /// 4-bit channels widened to 8 (v * 17 is v / 15 exactly); ARGB4444's channel order as
        /// OpenGL's BGRA with UNSIGNED_SHORT_4_4_4_4_REV reads it.
        Argb4444ToRgba32,
        Rgba4444ToRgba32,
        /// A block-compressed format the device cannot sample, decoded as OpenGL's device
        /// decodes it (TextureFormats.DecodeLevel): to RGBA8, BC6H to half floats.
        Decode,
    }

    /// A texture format on this device: the Vulkan format, the view's swizzle (as OpenGL's
    /// texture swizzle) and the upload conversion.
    internal readonly record struct VkTextureFormat(VkFormat Format, VkComponentMapping Swizzle, TexelConversion Conversion, int BlockBytes, int BlockSize);

    internal static class VkFormats
    {
        static readonly VkComponentMapping Identity = new()
        {
            R = VkComponentSwizzle.Identity, G = VkComponentSwizzle.Identity, B = VkComponentSwizzle.Identity, A = VkComponentSwizzle.Identity,
        };

        /// Unity's ARGB32 stored as its bytes and read (r, g, b, a) = (byte 1, 2, 3, 0).
        static readonly VkComponentMapping ArgbBytes = new() { R = VkComponentSwizzle.G, G = VkComponentSwizzle.B, B = VkComponentSwizzle.A, A = VkComponentSwizzle.R };
        /// Alpha8 as a red texture read (0, 0, 0, a).
        static readonly VkComponentMapping AlphaOnly = new() { R = VkComponentSwizzle.Zero, G = VkComponentSwizzle.Zero, B = VkComponentSwizzle.Zero, A = VkComponentSwizzle.R };

        /// Whether the device samples a format (filtered) and can copy into it.
        public static Func<VkFormat, bool> Sampled = _ => true;

        /// The device format for a Unity texture format, as OpenGL's device stores it (GlTextures.Of).
        public static VkTextureFormat Of(TextureFormat f, bool bcSampled)
        {
            switch (f)
            {
                case TextureFormat.Alpha8: return new(VkFormat.R8UNorm, AlphaOnly, TexelConversion.None, 1, 1);
                case TextureFormat.R8: return new(VkFormat.R8UNorm, Identity, TexelConversion.None, 1, 1);
                case TextureFormat.RG16: return new(VkFormat.R8G8UNorm, Identity, TexelConversion.None, 2, 1);
                case TextureFormat.RGB24: return new(VkFormat.R8G8B8A8UNorm, Identity, TexelConversion.Rgb24ToRgba32, 4, 1);
                case TextureFormat.RGBA32: return new(VkFormat.R8G8B8A8UNorm, Identity, TexelConversion.None, 4, 1);
                case TextureFormat.ARGB32: return new(VkFormat.R8G8B8A8UNorm, ArgbBytes, TexelConversion.None, 4, 1);
                case TextureFormat.BGRA32: return new(VkFormat.B8G8R8A8UNorm, Identity, TexelConversion.None, 4, 1);
                case TextureFormat.ARGB4444: return new(VkFormat.R8G8B8A8UNorm, Identity, TexelConversion.Argb4444ToRgba32, 4, 1);
                case TextureFormat.RGBA4444: return new(VkFormat.R8G8B8A8UNorm, Identity, TexelConversion.Rgba4444ToRgba32, 4, 1);
                // OpenGL's RGB / UNSIGNED_SHORT_5_6_5: red in the top bits, as Vulkan's R5G6B5.
                case TextureFormat.RGB565: return new(VkFormat.R5G6B5UNormPack16, Identity, TexelConversion.None, 2, 1);
                case TextureFormat.R16: return new(VkFormat.R16UNorm, Identity, TexelConversion.None, 2, 1);
                case TextureFormat.RG32: return new(VkFormat.R16G16UNorm, Identity, TexelConversion.None, 4, 1);
                case TextureFormat.RGB48: return new(VkFormat.R16G16B16A16UNorm, Identity, TexelConversion.Rgb48ToRgba64, 8, 1);
                case TextureFormat.RGBA64: return new(VkFormat.R16G16B16A16UNorm, Identity, TexelConversion.None, 8, 1);
                case TextureFormat.RHalf: return new(VkFormat.R16SFloat, Identity, TexelConversion.None, 2, 1);
                case TextureFormat.RGHalf: return new(VkFormat.R16G16SFloat, Identity, TexelConversion.None, 4, 1);
                case TextureFormat.RGBAHalf: return new(VkFormat.R16G16B16A16SFloat, Identity, TexelConversion.None, 8, 1);
                case TextureFormat.RFloat: return new(VkFormat.R32SFloat, Identity, TexelConversion.None, 4, 1);
                case TextureFormat.RGFloat: return new(VkFormat.R32G32SFloat, Identity, TexelConversion.None, 8, 1);
                case TextureFormat.RGBAFloat: return new(VkFormat.R32G32B32A32SFloat, Identity, TexelConversion.None, 16, 1);
            }
            if (bcSampled)
                switch (f)
                {
                    // OpenGL's COMPRESSED_RGB_S3TC_DXT1: alpha reads 1, as Vulkan's BC1 RGB.
                    case TextureFormat.DXT1: return new(VkFormat.Bc1RGBUNormBlock, Identity, TexelConversion.None, 8, 4);
                    case TextureFormat.DXT5: return new(VkFormat.Bc3UNormBlock, Identity, TexelConversion.None, 16, 4);
                    case TextureFormat.BC4: return new(VkFormat.Bc4UNormBlock, Identity, TexelConversion.None, 8, 4);
                    case TextureFormat.BC5: return new(VkFormat.Bc5UNormBlock, Identity, TexelConversion.None, 16, 4);
                    case TextureFormat.BC6H: return new(VkFormat.Bc6HUfloatBlock, Identity, TexelConversion.None, 16, 4);
                    case TextureFormat.BC7: return new(VkFormat.Bc7UNormBlock, Identity, TexelConversion.None, 16, 4);
                }
            if (TextureFormats.IsBlockCompressed(f) && TextureFormats.CanDecode(f))
                return f == TextureFormat.BC6H
                    ? new(VkFormat.R16G16B16A16SFloat, Identity, TexelConversion.Decode, 8, 1)
                    : new(VkFormat.R8G8B8A8UNorm, Identity, TexelConversion.Decode, 4, 1);
            throw new NotSupportedException($"Texture format {f} has no Vulkan equivalent in this device.");
        }

        /// Render texture colour formats, as OpenGL's device has them (GlTextures.InternalOf).
        public static VkFormat Color(GraphicsFormat f) => f switch
        {
            GraphicsFormat.R8G8B8A8_UNorm or GraphicsFormat.R8G8B8A8_SRGB or GraphicsFormat.B8G8R8A8_UNorm or GraphicsFormat.B8G8R8A8_SRGB => VkFormat.R8G8B8A8UNorm,
            GraphicsFormat.R16G16B16A16_SFloat => VkFormat.R16G16B16A16SFloat,
            GraphicsFormat.R32G32B32A32_SFloat => VkFormat.R32G32B32A32SFloat,
            GraphicsFormat.R32G32_SFloat => VkFormat.R32G32SFloat,
            GraphicsFormat.R16G16_SFloat => VkFormat.R16G16SFloat,
            GraphicsFormat.R32_SFloat => VkFormat.R32SFloat,
            GraphicsFormat.R16_SFloat => VkFormat.R16SFloat,
            GraphicsFormat.R8_UNorm => VkFormat.R8UNorm,
            GraphicsFormat.R8G8_UNorm => VkFormat.R8G8UNorm,
            GraphicsFormat.R16_UNorm => VkFormat.R16UNorm,
            GraphicsFormat.R16G16_UNorm => VkFormat.R16G16UNorm,
            GraphicsFormat.R16G16B16A16_UNorm => VkFormat.R16G16B16A16UNorm,
            GraphicsFormat.B5G6R5_UNormPack16 => VkFormat.R5G6B5UNormPack16,
            GraphicsFormat.B4G4R4A4_UNormPack16 => VkFormat.R4G4B4A4UNormPack16,
            GraphicsFormat.B5G5R5A1_UNormPack16 => VkFormat.R5G5B5A1UNormPack16,
            GraphicsFormat.A2B10G10R10_UNormPack32 => VkFormat.A2B10G10R10UNormPack32,
            GraphicsFormat.B10G11R11_UFloatPack32 => VkFormat.B10G11R11UfloatPack32,
            // Integer formats are not offered (as on OpenGL: no game target uses them).
            _ => throw new NotSupportedException($"Render texture format {f} has no Vulkan equivalent in this device."),
        };

        /// Converts one level's texels for upload; returns the bytes to copy (the input itself
        /// when nothing changes).
        public static unsafe ReadOnlySpan<byte> Convert(TexelConversion c, TextureFormat source, byte* texels, long length, int width, int height, ref byte[] scratch)
        {
            if (c == TexelConversion.None) return new ReadOnlySpan<byte>(texels, checked((int)length));
            long pixels = (long)width * height;
            int outBytes = c switch { TexelConversion.Rgb48ToRgba64 => 8, TexelConversion.Decode when source == TextureFormat.BC6H => 8, _ => 4 };
            long size = pixels * outBytes;
            if (scratch == null || scratch.Length < size) scratch = new byte[size];
            fixed (byte* o = scratch)
            {
                switch (c)
                {
                    case TexelConversion.Rgb24ToRgba32:
                        for (long i = 0; i < pixels; i++)
                        {
                            o[i * 4] = texels[i * 3];
                            o[i * 4 + 1] = texels[i * 3 + 1];
                            o[i * 4 + 2] = texels[i * 3 + 2];
                            o[i * 4 + 3] = 255;
                        }
                        break;
                    case TexelConversion.Rgb48ToRgba64:
                    {
                        var s = (ushort*)texels;
                        var d = (ushort*)o;
                        for (long i = 0; i < pixels; i++)
                        {
                            d[i * 4] = s[i * 3];
                            d[i * 4 + 1] = s[i * 3 + 1];
                            d[i * 4 + 2] = s[i * 3 + 2];
                            d[i * 4 + 3] = 65535;
                        }
                        break;
                    }
                    case TexelConversion.Argb4444ToRgba32:
                    case TexelConversion.Rgba4444ToRgba32:
                    {
                        var s = (ushort*)texels;
                        for (long i = 0; i < pixels; i++)
                        {
                            int v = s[i];
                            int n0 = v >> 12 & 15, n1 = v >> 8 & 15, n2 = v >> 4 & 15, n3 = v & 15;
                            // RGBA / 4_4_4_4: r g b a from the top nibble down. BGRA /
                            // 4_4_4_4_REV: b g r a from the bottom nibble up.
                            var (r, g, b, a) = c == TexelConversion.Rgba4444ToRgba32 ? (n0, n1, n2, n3) : (n1, n2, n3, n0);
                            o[i * 4] = (byte)(r * 17);
                            o[i * 4 + 1] = (byte)(g * 17);
                            o[i * 4 + 2] = (byte)(b * 17);
                            o[i * 4 + 3] = (byte)(a * 17);
                        }
                        break;
                    }
                    case TexelConversion.Decode:
                        TextureFormats.DecodeLevel(source, texels, length, width, height, o, halfFloat: source == TextureFormat.BC6H);
                        break;
                }
            }
            return new ReadOnlySpan<byte>(scratch, 0, (int)size);
        }

        /// One texel of a readback in OpenGL's ReadPixels(RGBA, FLOAT) terms: missing green and
        /// blue read 0, missing alpha 1.
        public static unsafe UnityEngine.Color ReadTexel(VkFormat format, byte* p)
        {
            static float U8(byte v) => v / 255f;
            static float U16(ushort v) => v / 65535f;
            static float H(ushort v) => (float)BitConverter.UInt16BitsToHalf(v);
            switch (format)
            {
                case VkFormat.R8G8B8A8UNorm: return new(U8(p[0]), U8(p[1]), U8(p[2]), U8(p[3]));
                case VkFormat.B8G8R8A8UNorm: return new(U8(p[2]), U8(p[1]), U8(p[0]), U8(p[3]));
                case VkFormat.R8UNorm: return new(U8(p[0]), 0, 0, 1);
                case VkFormat.R8G8UNorm: return new(U8(p[0]), U8(p[1]), 0, 1);
                case VkFormat.R16UNorm: return new(U16(((ushort*)p)[0]), 0, 0, 1);
                case VkFormat.R16G16UNorm: return new(U16(((ushort*)p)[0]), U16(((ushort*)p)[1]), 0, 1);
                case VkFormat.R16G16B16A16UNorm: { var s = (ushort*)p; return new(U16(s[0]), U16(s[1]), U16(s[2]), U16(s[3])); }
                case VkFormat.R16SFloat: return new(H(((ushort*)p)[0]), 0, 0, 1);
                case VkFormat.R16G16SFloat: return new(H(((ushort*)p)[0]), H(((ushort*)p)[1]), 0, 1);
                case VkFormat.R16G16B16A16SFloat: { var s = (ushort*)p; return new(H(s[0]), H(s[1]), H(s[2]), H(s[3])); }
                case VkFormat.R32SFloat: return new(((float*)p)[0], 0, 0, 1);
                case VkFormat.R32G32SFloat: return new(((float*)p)[0], ((float*)p)[1], 0, 1);
                case VkFormat.R32G32B32A32SFloat: { var s = (float*)p; return new(s[0], s[1], s[2], s[3]); }
                case VkFormat.R5G6B5UNormPack16: { int v = ((ushort*)p)[0]; return new((v >> 11) / 31f, (v >> 5 & 63) / 63f, (v & 31) / 31f, 1); }
                case VkFormat.R4G4B4A4UNormPack16: { int v = ((ushort*)p)[0]; return new((v >> 12) / 15f, (v >> 8 & 15) / 15f, (v >> 4 & 15) / 15f, (v & 15) / 15f); }
                case VkFormat.R5G5B5A1UNormPack16: { int v = ((ushort*)p)[0]; return new((v >> 11) / 31f, (v >> 6 & 31) / 31f, (v >> 1 & 31) / 31f, v & 1); }
                case VkFormat.A2B10G10R10UNormPack32: { uint v = ((uint*)p)[0]; return new((v & 1023) / 1023f, (v >> 10 & 1023) / 1023f, (v >> 20 & 1023) / 1023f, (v >> 30) / 3f); }
                case VkFormat.B10G11R11UfloatPack32:
                {
                    uint v = ((uint*)p)[0];
                    return new(UFloat(v & 0x7FF, 6), UFloat(v >> 11 & 0x7FF, 6), UFloat(v >> 22 & 0x3FF, 5), 1);
                }
                default: throw new NotSupportedException($"reading back {format}");
            }
        }

        /// An unsigned small float (5-bit exponent, `mantissaBits` mantissa).
        static float UFloat(uint v, int mantissaBits)
        {
            uint e = v >> mantissaBits, m = v & ((1u << mantissaBits) - 1);
            if (e == 0) return m / (float)(1 << mantissaBits) * MathF.Pow(2, -14);
            if (e == 31) return m == 0 ? float.PositiveInfinity : float.NaN;
            return (1 + m / (float)(1 << mantissaBits)) * MathF.Pow(2, (int)e - 15);
        }

        /// Bytes per texel of the formats render textures and readbacks use.
        public static int TexelBytes(VkFormat f) => f switch
        {
            VkFormat.R8UNorm => 1,
            VkFormat.R8G8UNorm or VkFormat.R16UNorm or VkFormat.R16SFloat or VkFormat.R5G6B5UNormPack16 or VkFormat.R4G4B4A4UNormPack16 or VkFormat.R5G5B5A1UNormPack16 => 2,
            VkFormat.R16G16B16A16UNorm or VkFormat.R16G16B16A16SFloat or VkFormat.R32G32SFloat => 8,
            VkFormat.R32G32B32A32SFloat => 16,
            _ => 4,
        };
    }
}
