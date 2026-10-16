using System;
using Brutal.OpenGlApi;
using OniBrutal.Engine.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using N = Brutal.OpenGlApi.OpenGl.Native;
using static OniBrutal.Desktop.Gl.GlConst;

namespace OniBrutal.Desktop.Gl
{
    /// The device side of a texture or render texture.
    internal sealed class DeviceTexture
    {
        public uint Id;
        public uint Target = TexTarget2D;
        public int ContentVersion = -1;
        public int SamplerVersion = -1;
        public int Width, Height, Depth;
        public int Levels = 1;
        /// The format the 2D storage was specified with (uploads into unchanged storage update it
        /// in place).
        public TextureFormat StorageFormat = (TextureFormat)(-1);

        // Render textures.
        public uint Framebuffer;
        public uint DepthRenderbuffer;
        public uint MultisampleColor;
        public uint MultisampleFramebuffer;
        public int Samples = 1;
        public bool DepthOnly;
        /// Rendered to since the last resolve / mip generation.
        public bool Dirty;
        public RenderTextureNative Description;

        public void Delete()
        {
            if (Id != 0) GlApi.DeleteTexture(Id);
            if (Framebuffer != 0) GlApi.DeleteFramebuffer(Framebuffer);
            if (MultisampleFramebuffer != 0) GlApi.DeleteFramebuffer(MultisampleFramebuffer);
            if (DepthRenderbuffer != 0) GlApi.DeleteRenderbuffer(DepthRenderbuffer);
            if (MultisampleColor != 0) GlApi.DeleteRenderbuffer(MultisampleColor);
            Id = Framebuffer = MultisampleFramebuffer = DepthRenderbuffer = MultisampleColor = 0;
        }
    }

    /// How a Unity texture format is stored and uploaded in GL.
    internal readonly record struct GlFormat(uint Internal, uint Format, uint Type, bool Compressed, uint[] Swizzle = null);

    internal static unsafe class GlTextures
    {
        static readonly uint[] AlphaOnly = [Zero, Zero, Zero, Red];
        static readonly uint[] ArgbBytes = [Green, Blue, Alpha, Red];

        /// Unity's texture formats in GL: ARGB32 is stored as its bytes and swizzled; Alpha8 is a
        /// red-channel texture read as (0, 0, 0, a); gamma colour space uses no sRGB formats.
        public static GlFormat Of(TextureFormat f) => f switch
        {
            TextureFormat.Alpha8 => new(R8, RedFormat, UnsignedByte, false, AlphaOnly),
            TextureFormat.R8 => new(R8, RedFormat, UnsignedByte, false),
            TextureFormat.RG16 => new(Rg8, Rg, UnsignedByte, false),
            TextureFormat.RGB24 => new(Rgb8, Rgb, UnsignedByte, false),
            TextureFormat.RGBA32 => new(Rgba8, Rgba, UnsignedByte, false),
            TextureFormat.ARGB32 => new(Rgba8, Rgba, UnsignedByte, false, ArgbBytes),
            TextureFormat.BGRA32 => new(Rgba8, Bgra, UnsignedByte, false),
            TextureFormat.ARGB4444 => new(Rgba4, Bgra, UnsignedShort4444Rev, false),
            TextureFormat.RGBA4444 => new(Rgba4, Rgba, UnsignedShort4444, false),
            TextureFormat.RGB565 => new(Rgb565, Rgb, UnsignedShort565, false),
            TextureFormat.R16 => new(R16, RedFormat, UnsignedShort, false),
            TextureFormat.RG32 => new(Rg16, Rg, UnsignedShort, false),
            TextureFormat.RGB48 => new(Rgb16, Rgb, UnsignedShort, false),
            TextureFormat.RGBA64 => new(Rgba16, Rgba, UnsignedShort, false),
            TextureFormat.RHalf => new(R16F, RedFormat, HalfFloat, false),
            TextureFormat.RGHalf => new(Rg16F, Rg, HalfFloat, false),
            TextureFormat.RGBAHalf => new(Rgba16F, Rgba, HalfFloat, false),
            TextureFormat.RFloat => new(R32F, RedFormat, Float, false),
            TextureFormat.RGFloat => new(Rg32F, Rg, Float, false),
            TextureFormat.RGBAFloat => new(Rgba32F, Rgba, Float, false),
            TextureFormat.DXT1 => new(CompressedRgbS3tcDxt1, 0, 0, true),
            TextureFormat.DXT5 => new(CompressedRgbaS3tcDxt5, 0, 0, true),
            TextureFormat.BC4 => new(CompressedRedRgtc1, 0, 0, true),
            TextureFormat.BC5 => new(CompressedRgRgtc2, 0, 0, true),
            TextureFormat.BC6H => new(CompressedRgbBptcUnsignedFloat, 0, 0, true),
            TextureFormat.BC7 => new(CompressedRgbaBptcUnorm, 0, 0, true),
            _ => throw new NotSupportedException($"Texture format {f} has no OpenGL equivalent in this device."),
        };

        /// Render texture colour formats.
        public static uint InternalOf(GraphicsFormat f) => f switch
        {
            GraphicsFormat.R8G8B8A8_UNorm or GraphicsFormat.R8G8B8A8_SRGB or GraphicsFormat.B8G8R8A8_UNorm or GraphicsFormat.B8G8R8A8_SRGB => Rgba8,
            GraphicsFormat.R16G16B16A16_SFloat => Rgba16F,
            GraphicsFormat.R32G32B32A32_SFloat => Rgba32F,
            GraphicsFormat.R32G32_SFloat => Rg32F,
            GraphicsFormat.R16G16_SFloat => Rg16F,
            GraphicsFormat.R32_SFloat => R32F,
            GraphicsFormat.R16_SFloat => R16F,
            GraphicsFormat.R8_UNorm => R8,
            GraphicsFormat.R8G8_UNorm => Rg8,
            GraphicsFormat.R16_UNorm => R16,
            GraphicsFormat.R16G16_UNorm => Rg16,
            GraphicsFormat.R16G16B16A16_UNorm => Rgba16,
            GraphicsFormat.B5G6R5_UNormPack16 => Rgb565,
            GraphicsFormat.B4G4R4A4_UNormPack16 => Rgba4,
            GraphicsFormat.B5G5R5A1_UNormPack16 => Rgb5A1,
            GraphicsFormat.A2B10G10R10_UNormPack32 => Rgb10A2,
            GraphicsFormat.B10G11R11_UFloatPack32 => R11G11B10F,
            GraphicsFormat.R32G32B32A32_SInt => Rgba32I,
            GraphicsFormat.R32G32_SInt => Rg32I,
            GraphicsFormat.R32_SInt => R32I,
            GraphicsFormat.R16G16B16A16_UInt => Rgba16UI,
            _ => throw new NotSupportedException($"Render texture format {f} has no OpenGL equivalent in this device."),
        };

        /// The device texture for an engine texture, created and (re)uploaded as needed.
        public static DeviceTexture Ensure(TextureNative t)
        {
            if (t.Gpu is not DeviceTexture g)
            {
                g = new DeviceTexture();
                t.Gpu = g;
                GraphicsDevice.Adopt(t, g);
            }
            switch (t)
            {
                case RenderTextureNative rt:
                    EnsureRenderTexture(rt, g);
                    break;
                case Texture2DNative t2 when g.ContentVersion != t2.ContentVersion:
                    Upload2D(t2, g);
                    break;
                case Texture2DArrayNative ta when g.ContentVersion != ta.ContentVersion:
                    UploadArray(ta, g);
                    break;
                case Texture3DNative t3 when g.ContentVersion != t3.ContentVersion:
                    Upload3D(t3, g);
                    break;
            }
            if (g.SamplerVersion != t.SamplerVersion) ApplySampler(t, g);
            return g;
        }

        static void Begin(DeviceTexture g, uint target)
        {
            if (g.Id == 0 || g.Target != target)
            {
                if (g.Id != 0) GlApi.DeleteTexture(g.Id);
                g.Id = GlApi.GenTexture();
                g.Target = target;
                g.SamplerVersion = -1;
            }
            GlApi.BindTexture(target, g.Id);
            N.glPixelStorei(UnpackAlignment, 1);
        }

        static void Swizzle(uint target, uint[] s)
        {
            s ??= [Red, Green, Blue, Alpha];
            N.glTexParameteri(target, TextureSwizzleR, (int)s[0]);
            N.glTexParameteri(target, TextureSwizzleG, (int)s[1]);
            N.glTexParameteri(target, TextureSwizzleB, (int)s[2]);
            N.glTexParameteri(target, TextureSwizzleA, (int)s[3]);
        }

        static void Upload2D(Texture2DNative t, DeviceTexture g)
        {
            bool fresh = g.Id == 0 || g.Target != TexTarget2D;
            Begin(g, TexTarget2D);
            var f = Of(t.Format);
            var memory = t.Memory;
            int levels = Math.Max(1, t.MipCount);
            // Content changes into storage of the same size, format and mip count update it in
            // place: respecifying storage reallocates it on the driver side.
            bool inPlace = !fresh && memory != null && g.StorageFormat == t.Format && g.Width == t.Width && g.Height == t.Height && g.Levels == levels;
            for (int m = 0; m < levels; m++)
            {
                int w = Math.Max(1, t.Width >> m), h = Math.Max(1, t.Height >> m);
                byte* p = memory == null ? null : memory.Pointer + t.MipOffset(m);
                int size = (int)t.MipSize(m);
                if (inPlace)
                {
                    if (f.Compressed) N.glCompressedTexSubImage2D(TexTarget2D, m, 0, 0, w, h, f.Internal, size, p);
                    else N.glTexSubImage2D(TexTarget2D, m, 0, 0, w, h, f.Format, f.Type, p);
                }
                else if (f.Compressed)
                {
                    if (p != null) N.glCompressedTexImage2D(TexTarget2D, m, f.Internal, w, h, 0, size, p);
                }
                else N.glTexImage2D(TexTarget2D, m, (int)f.Internal, w, h, 0, f.Format, f.Type, p);
            }
            g.StorageFormat = t.Format;
            N.glTexParameteri(TexTarget2D, TextureBaseLevel, 0);
            N.glTexParameteri(TexTarget2D, TextureMaxLevel, levels - 1);
            Swizzle(TexTarget2D, f.Swizzle);
            g.Width = t.Width;
            g.Height = t.Height;
            g.Levels = levels;
            g.ContentVersion = t.ContentVersion;
            // Unity keeps no CPU copy of textures that are not readable.
            if (!t.Readable) t.DropTexels();
        }

        static void UploadArray(Texture2DArrayNative t, DeviceTexture g)
        {
            Begin(g, TexTarget2DArray);
            var f = Of(t.Format);
            var memory = t.Memory;
            int levels = Math.Max(1, t.MipCount);
            long sliceSize = t.SliceSize;
            for (int m = 0; m < levels; m++)
            {
                int w = Math.Max(1, t.Width >> m), h = Math.Max(1, t.Height >> m);
                long mipOffset = TextureFormats.MipOffset(t.Format, t.Width, t.Height, m);
                int mipSize = (int)TextureFormats.MipSize(t.Format, w, h);
                if (f.Compressed) N.glCompressedTexImage3D(TexTarget2DArray, m, f.Internal, w, h, t.Depth, 0, mipSize * t.Depth, null);
                else N.glTexImage3D(TexTarget2DArray, m, (int)f.Internal, w, h, t.Depth, 0, f.Format, f.Type, null);
                if (memory == null) continue;
                // Slices are stored one after another, each with its whole mip chain.
                for (int s = 0; s < t.Depth; s++)
                {
                    byte* p = memory.Pointer + s * sliceSize + mipOffset;
                    if (f.Compressed) N.glCompressedTexSubImage3D(TexTarget2DArray, m, 0, 0, s, w, h, 1, f.Internal, mipSize, p);
                    else N.glTexSubImage3D(TexTarget2DArray, m, 0, 0, s, w, h, 1, f.Format, f.Type, p);
                }
            }
            N.glTexParameteri(TexTarget2DArray, TextureBaseLevel, 0);
            N.glTexParameteri(TexTarget2DArray, TextureMaxLevel, levels - 1);
            Swizzle(TexTarget2DArray, f.Swizzle);
            g.Width = t.Width;
            g.Height = t.Height;
            g.Depth = t.Depth;
            g.Levels = levels;
            g.ContentVersion = t.ContentVersion;
            if (!t.Readable) t.DropTexels();
        }

        static void Upload3D(Texture3DNative t, DeviceTexture g)
        {
            Begin(g, TexTarget3D);
            var f = Of(t.Format);
            var memory = t.Memory;
            int levels = Math.Max(1, t.MipCount);
            for (int m = 0; m < levels; m++)
            {
                int w = Math.Max(1, t.Width >> m), h = Math.Max(1, t.Height >> m), d = Math.Max(1, t.Depth >> m);
                byte* p = memory == null ? null : memory.Pointer + t.MipOffset(m);
                if (f.Compressed) N.glCompressedTexImage3D(TexTarget3D, m, f.Internal, w, h, d, 0, (int)t.MipSize(m), p);
                else N.glTexImage3D(TexTarget3D, m, (int)f.Internal, w, h, d, 0, f.Format, f.Type, p);
            }
            N.glTexParameteri(TexTarget3D, TextureBaseLevel, 0);
            N.glTexParameteri(TexTarget3D, TextureMaxLevel, levels - 1);
            Swizzle(TexTarget3D, f.Swizzle);
            g.Width = t.Width;
            g.Height = t.Height;
            g.Depth = t.Depth;
            g.Levels = levels;
            g.ContentVersion = t.ContentVersion;
            if (!t.Readable) t.DropTexels();
        }

        /// Point: nearest; Bilinear: linear within a level, nearest level; Trilinear: linear
        /// across levels.
        static void ApplySampler(TextureNative t, DeviceTexture g)
        {
            var target = g.Target;
            if (target == TexTarget2DMultisample) return;
            GlApi.BindTexture(target, g.Id);
            bool mips = g.Levels > 1;
            uint min = t.FilterMode switch
            {
                FilterMode.Point => mips ? NearestMipmapNearest : Nearest,
                FilterMode.Bilinear => mips ? LinearMipmapNearest : Linear,
                _ => mips ? LinearMipmapLinear : Linear,
            };
            N.glTexParameteri(target, TextureMinFilter, (int)min);
            N.glTexParameteri(target, TextureMagFilter, (int)(t.FilterMode == FilterMode.Point ? Nearest : Linear));
            N.glTexParameteri(target, TextureWrapS, (int)Wrap(t.WrapU));
            N.glTexParameteri(target, TextureWrapT, (int)Wrap(t.WrapV));
            if (target is TexTarget3D) N.glTexParameteri(target, TextureWrapR, (int)Wrap(t.WrapW));
            N.glTexParameterf(target, TextureLodBias, t.MipBias);
            if (GlDevice.MaxAnisotropy > 1f)
                N.glTexParameterf(target, TextureMaxAnisotropy, t.FilterMode == FilterMode.Point ? 1f : Math.Clamp(t.Aniso, 1, GlDevice.MaxAnisotropy));
            g.SamplerVersion = t.SamplerVersion;
        }

        static uint Wrap(TextureWrapMode m) => m switch
        {
            TextureWrapMode.Clamp => ClampToEdge,
            TextureWrapMode.Mirror => MirroredRepeat,
            TextureWrapMode.MirrorOnce => MirrorClampToEdge,
            _ => Repeat,
        };

        /// Colour texture (or depth texture for depth-only formats), depth-stencil buffer,
        /// framebuffer, and multisampled surfaces when anti-aliased.
        static void EnsureRenderTexture(RenderTextureNative rt, DeviceTexture g)
        {
            bool depthOnly = rt.ColorFormat == GraphicsFormat.None;
            int levels = rt.UseMipMap ? TextureFormats.FullMipCount(rt.Width, rt.Height) : 1;
            int samples = Math.Max(1, rt.AntiAliasing);
            if (g.Id != 0 && g.Width == rt.Width && g.Height == rt.Height && g.Levels == levels && g.Samples == samples && g.DepthOnly == depthOnly
                && g.Description != null && g.Description.ColorFormat == rt.ColorFormat && g.Description.DepthBits == rt.DepthBits)
                return;
            g.Delete();
            g.Id = GlApi.GenTexture();
            g.Target = TexTarget2D;
            g.Width = rt.Width;
            g.Height = rt.Height;
            g.Levels = levels;
            g.Samples = samples;
            g.DepthOnly = depthOnly;
            g.Description = new RenderTextureNative { ColorFormat = rt.ColorFormat, DepthBits = rt.DepthBits };
            g.SamplerVersion = -1;
            GlApi.BindTexture(TexTarget2D, g.Id);
            if (depthOnly)
                N.glTexImage2D(TexTarget2D, 0, (int)DepthComponent32F, rt.Width, rt.Height, 0, DepthComponent, Float, null);
            else
            {
                uint internalFormat = InternalOf(rt.ColorFormat);
                for (int m = 0; m < levels; m++)
                    N.glTexImage2D(TexTarget2D, m, (int)internalFormat, Math.Max(1, rt.Width >> m), Math.Max(1, rt.Height >> m), 0, Rgba, Float, null);
            }
            N.glTexParameteri(TexTarget2D, TextureMaxLevel, levels - 1);
            g.Framebuffer = GlApi.GenFramebuffer();
            GlApi.BindFramebuffer(Framebuffer, g.Framebuffer);
            if (depthOnly)
            {
                N.glFramebufferTexture2D(Framebuffer, DepthAttachment, TexTarget2D, g.Id, 0);
                N.glDrawBuffer(NoneBuffer);
                N.glReadBuffer(NoneBuffer);
            }
            else
            {
                N.glFramebufferTexture2D(Framebuffer, ColorAttachment0, TexTarget2D, g.Id, 0);
                if (rt.DepthBits > 0)
                {
                    g.DepthRenderbuffer = GlApi.GenRenderbuffer();
                    N.glBindRenderbuffer(Renderbuffer, new GlRenderbuffer(g.DepthRenderbuffer));
                    N.glRenderbufferStorage(Renderbuffer, rt.DepthBits > 24 ? Depth32FStencil8 : rt.DepthBits > 16 ? Depth24Stencil8 : DepthComponent16, rt.Width, rt.Height);
                    N.glFramebufferRenderbuffer(Framebuffer, rt.DepthBits > 16 ? DepthStencilAttachment : DepthAttachment, Renderbuffer, new GlRenderbuffer(g.DepthRenderbuffer));
                }
            }
            CheckFramebuffer(rt);
            if (samples > 1 && !depthOnly)
            {
                // Rendering goes to multisampled surfaces, resolved into the texture when it is read.
                g.MultisampleFramebuffer = GlApi.GenFramebuffer();
                GlApi.BindFramebuffer(Framebuffer, g.MultisampleFramebuffer);
                g.MultisampleColor = GlApi.GenRenderbuffer();
                N.glBindRenderbuffer(Renderbuffer, new GlRenderbuffer(g.MultisampleColor));
                N.glRenderbufferStorageMultisample(Renderbuffer, samples, InternalOf(rt.ColorFormat), rt.Width, rt.Height);
                N.glFramebufferRenderbuffer(Framebuffer, ColorAttachment0, Renderbuffer, new GlRenderbuffer(g.MultisampleColor));
                if (rt.DepthBits > 0)
                {
                    // The single-sample depth buffer is not used for multisampled rendering.
                    GlApi.DeleteRenderbuffer(g.DepthRenderbuffer);
                    g.DepthRenderbuffer = GlApi.GenRenderbuffer();
                    N.glBindRenderbuffer(Renderbuffer, new GlRenderbuffer(g.DepthRenderbuffer));
                    N.glRenderbufferStorageMultisample(Renderbuffer, samples, Depth24Stencil8, rt.Width, rt.Height);
                    N.glFramebufferRenderbuffer(Framebuffer, DepthStencilAttachment, Renderbuffer, new GlRenderbuffer(g.DepthRenderbuffer));
                }
                CheckFramebuffer(rt);
            }
            GlApi.BindFramebuffer(Framebuffer, 0);
            GlDevice.InvalidateFramebufferBinding();
        }

        static void CheckFramebuffer(RenderTextureNative rt)
        {
            var status = N.glCheckFramebufferStatus(Framebuffer).Id;
            if (status != FramebufferComplete)
                throw new InvalidOperationException($"Render texture '{rt.Name}' ({rt.Width}x{rt.Height} {rt.ColorFormat}, depth {rt.DepthBits}): framebuffer incomplete (0x{status:X}).");
        }

        /// Before a render texture is sampled: resolve multisampling and rebuild mips.
        public static void PrepareForSampling(RenderTextureNative rt, DeviceTexture g)
        {
            if (!g.Dirty) return;
            g.Dirty = false;
            if (g.MultisampleFramebuffer != 0)
            {
                GlApi.BindFramebuffer(ReadFramebuffer, g.MultisampleFramebuffer);
                GlApi.BindFramebuffer(DrawFramebuffer, g.Framebuffer);
                N.glBlitFramebuffer(0, 0, g.Width, g.Height, 0, 0, g.Width, g.Height, ColorBufferBit, Nearest);
                GlDevice.InvalidateFramebufferBinding();
            }
            if (g.Levels > 1 && rt.AutoGenerateMips)
            {
                GlApi.BindTexture(TexTarget2D, g.Id);
                N.glGenerateMipmap(TexTarget2D);
            }
        }

        /// The framebuffer to render a render texture into.
        public static uint RenderFramebuffer(DeviceTexture g) => g.MultisampleFramebuffer != 0 ? g.MultisampleFramebuffer : g.Framebuffer;
    }
}

