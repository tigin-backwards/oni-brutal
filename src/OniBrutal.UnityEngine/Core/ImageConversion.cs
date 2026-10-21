using System;
using System.IO;
using System.IO.Compression;
using Brutal.StbApi;
using OniBrutal.Engine;
using OniBrutal.Engine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine
{
    public partial struct DrivenRectTransformTracker
    {
        // Driven-property tracking serves the editor's inspector; in a player Unity records
        // nothing.
        public void Add(Object driver, RectTransform rectTransform, DrivenTransformProperties drivenProperties) { }
        public void Clear() { }
        public void Clear(bool revertValues) { }
    }

    public static partial class ImageConversion
    {
        public static bool LoadImage(this Texture2D tex, byte[] data) => LoadImage(tex, data, false);

        /// Replaces the texture with a PNG or JPEG image: PNGs load as ARGB32, JPEGs as RGB24,
        /// at the image's size, keeping a mip chain if the texture had one. False (texture
        /// unchanged) when the data is not an image.
        public static bool LoadImage(this Texture2D tex, byte[] data, bool markNonReadable)
        {
            if (tex == null) throw new ArgumentNullException(nameof(tex));
            if (data == null) throw new ArgumentNullException(nameof(data));
            Threads.AssertMainThread("LoadImage");
            bool png = data.Length >= 8 && data[0] == 0x89 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G';
            bool jpeg = data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;
            if (!png && !jpeg) return false;
            using var image = Stb.LoadFromMemory(data, png ? 4 : 3);
            if (image.IsNull()) return false;
            var native = (Texture2DNative)tex.NativeOrThrow;
            var format = png ? TextureFormat.ARGB32 : TextureFormat.RGB24;
            int w = image.Width, h = image.Height;
            native.Width = w;
            native.Height = h;
            native.Format = format;
            native.MipCount = native.MipCount > 1 ? TextureFormats.FullMipCount(w, h) : 1;
            native.Readable = true;
            native.Allocate(native.SliceSize, 0);
            // stb rows run top to bottom; Unity's run bottom to top.
            var src = image.AsSpan();
            var dst = native.Memory.Span;
            int channels = png ? 4 : 3;
            for (int y = 0; y < h; y++)
            {
                var row = src.Slice((h - 1 - y) * w * channels, w * channels);
                var outRow = dst.Slice(y * w * channels, w * channels);
                if (png)
                    for (int x = 0; x < w; x++)
                    {
                        outRow[x * 4] = row[x * 4 + 3];
                        outRow[x * 4 + 1] = row[x * 4];
                        outRow[x * 4 + 2] = row[x * 4 + 1];
                        outRow[x * 4 + 3] = row[x * 4 + 2];
                    }
                else row.CopyTo(outRow);
            }
            TexelAddressing.BuildMips(native);
            if (markNonReadable) native.Readable = false;
            native.ContentChanged();
            return true;
        }

        /// PNG of level 0: RGB for formats without alpha, greyscale for single-channel red
        /// formats, RGBA otherwise; 8 bits per channel.
        public static byte[] EncodeToPNG(this Texture2D tex)
        {
            if (tex == null) throw new ArgumentNullException(nameof(tex));
            var native = (Texture2DNative)tex.NativeOrThrow;
            if (!native.Readable)
            {
                Debug.LogError($"Texture '{tex.name}' is not readable, the texture memory can not be accessed from scripts.", tex);
                return null;
            }
            var format = native.Format;
            if (!TextureFormats.CanDecode(format))
            {
                Debug.LogError($"Unable to encode texture in format {format} to PNG.", tex);
                return null;
            }
            return Encode(format, native.Memory.Slice(0, native.MipSize(0)), native.Width, native.Height);
        }

        /// PNG of raw texels whose rows run bottom to top, `rowBytes` apart (0: packed), as
        /// EncodeToPNG writes a texture of that format (measured in the original player with
        /// RGBA, sRGB RGBA, BGRA, RGB rows with padding and R8 data). Null, with an error, for a
        /// format the engine cannot decode.
        public static unsafe byte[] EncodeArrayToPNG(Array array, GraphicsFormat format, uint width, uint height, uint rowBytes = 0)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            var textureFormat = GraphicsFormatUtility.GetTextureFormat(format);
            if (textureFormat == 0 || !TextureFormats.CanDecode(textureFormat))
            {
                Debug.LogError($"Unable to encode texture in format {format} to PNG.");
                return null;
            }
            int w = (int)width, h = (int)height;
            int packed = (int)TextureFormats.MipSize(textureFormat, w, 1);
            int stride = rowBytes == 0 ? packed : (int)rowBytes;
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(array, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                var source = new ReadOnlySpan<byte>((void*)handle.AddrOfPinnedObject(), Buffer.ByteLength(array));
                if (source.Length < (long)stride * (h - 1) + packed) throw new ArgumentException("The array is too small for the image.", nameof(array));
                var texels = new byte[packed * h];
                for (int y = 0; y < h; y++) source.Slice(y * stride, packed).CopyTo(texels.AsSpan(y * packed));
                return Encode(textureFormat, texels, w, h);
            }
            finally
            {
                handle.Free();
            }
        }

        /// PNG of a level's texels (rows bottom to top): RGB for formats without alpha, greyscale
        /// for single-channel red formats, RGBA otherwise; 8 bits per channel.
        static byte[] Encode(TextureFormat format, ReadOnlySpan<byte> mip, int w, int h)
        {
            int channels = format switch
            {
                TextureFormat.RGB24 or TextureFormat.RGB565 or TextureFormat.DXT1 or TextureFormat.RGB48 => 3,
                TextureFormat.R8 or TextureFormat.R16 or TextureFormat.RHalf or TextureFormat.RFloat or TextureFormat.BC4 => 1,
                _ => 4,
            };
            var raw = new byte[h * (w * channels + 1)];
            for (int y = 0; y < h; y++)
            {
                int row = y * (w * channels + 1);
                raw[row] = 0; // filter: none
                int srcY = h - 1 - y;
                for (int x = 0; x < w; x++)
                {
                    var c = (Color32)TextureFormats.Decode(format, mip, w, x, srcY);
                    int o = row + 1 + x * channels;
                    if (channels == 1) raw[o] = c.r;
                    else
                    {
                        raw[o] = c.r;
                        raw[o + 1] = c.g;
                        raw[o + 2] = c.b;
                        if (channels == 4) raw[o + 3] = c.a;
                    }
                }
            }
            return Png.Encode(w, h, channels, raw);
        }
    }
}

namespace UnityEngine.Experimental.Rendering
{
    public partial class GraphicsFormatUtility
    {
        public static bool IsCrunchFormat(TextureFormat format) => TextureFormats.IsCrunch(format);

        // Each texture format's graphics format, as the original player reports them in the
        // game's (gamma) colour space, where isSRGB changes none of them.
        static readonly System.Collections.Generic.Dictionary<TextureFormat, GraphicsFormat> graphicsFormats = new()
        {
            [(TextureFormat)1] = (GraphicsFormat)54, [(TextureFormat)2] = (GraphicsFormat)67, [(TextureFormat)3] = (GraphicsFormat)7,
            [(TextureFormat)4] = (GraphicsFormat)8, [(TextureFormat)5] = (GraphicsFormat)88, [(TextureFormat)7] = (GraphicsFormat)69,
            [(TextureFormat)9] = (GraphicsFormat)21, [(TextureFormat)10] = (GraphicsFormat)97, [(TextureFormat)12] = (GraphicsFormat)101,
            [(TextureFormat)13] = (GraphicsFormat)66, [(TextureFormat)14] = (GraphicsFormat)59, [(TextureFormat)15] = (GraphicsFormat)45,
            [(TextureFormat)16] = (GraphicsFormat)46, [(TextureFormat)17] = (GraphicsFormat)48, [(TextureFormat)18] = (GraphicsFormat)49,
            [(TextureFormat)19] = (GraphicsFormat)50, [(TextureFormat)20] = (GraphicsFormat)52, [(TextureFormat)21] = (GraphicsFormat)141,
            [(TextureFormat)22] = (GraphicsFormat)73, [(TextureFormat)24] = (GraphicsFormat)106, [(TextureFormat)25] = (GraphicsFormat)109,
            [(TextureFormat)26] = (GraphicsFormat)102, [(TextureFormat)27] = (GraphicsFormat)104, [(TextureFormat)28] = (GraphicsFormat)97,
            [(TextureFormat)29] = (GraphicsFormat)101, [(TextureFormat)30] = (GraphicsFormat)111, [(TextureFormat)31] = (GraphicsFormat)115,
            [(TextureFormat)32] = (GraphicsFormat)113, [(TextureFormat)33] = (GraphicsFormat)117, [(TextureFormat)34] = (GraphicsFormat)118,
            [(TextureFormat)41] = (GraphicsFormat)125, [(TextureFormat)42] = (GraphicsFormat)126, [(TextureFormat)43] = (GraphicsFormat)127,
            [(TextureFormat)44] = (GraphicsFormat)128, [(TextureFormat)45] = (GraphicsFormat)120, [(TextureFormat)46] = (GraphicsFormat)122,
            [(TextureFormat)47] = (GraphicsFormat)124, [(TextureFormat)48] = (GraphicsFormat)130, [(TextureFormat)49] = (GraphicsFormat)132,
            [(TextureFormat)50] = (GraphicsFormat)134, [(TextureFormat)51] = (GraphicsFormat)136, [(TextureFormat)52] = (GraphicsFormat)138,
            [(TextureFormat)53] = (GraphicsFormat)140, [(TextureFormat)62] = (GraphicsFormat)6, [(TextureFormat)63] = (GraphicsFormat)5,
            [(TextureFormat)64] = (GraphicsFormat)118, [(TextureFormat)65] = (GraphicsFormat)124, [(TextureFormat)66] = (GraphicsFormat)145,
            [(TextureFormat)67] = (GraphicsFormat)146, [(TextureFormat)68] = (GraphicsFormat)147, [(TextureFormat)69] = (GraphicsFormat)148,
            [(TextureFormat)70] = (GraphicsFormat)149, [(TextureFormat)71] = (GraphicsFormat)150, [(TextureFormat)72] = (GraphicsFormat)22,
            [(TextureFormat)73] = (GraphicsFormat)23, [(TextureFormat)74] = (GraphicsFormat)24, [(TextureFormat)75] = (GraphicsFormat)9,
            [(TextureFormat)76] = (GraphicsFormat)10, [(TextureFormat)77] = (GraphicsFormat)11, [(TextureFormat)78] = (GraphicsFormat)12,
            [(TextureFormat)79] = (GraphicsFormat)25, [(TextureFormat)80] = (GraphicsFormat)26, [(TextureFormat)81] = (GraphicsFormat)27,
            [(TextureFormat)82] = (GraphicsFormat)28,
        };

        public static GraphicsFormat GetGraphicsFormat(TextureFormat format, bool isSRGB)
            => graphicsFormats.TryGetValue(format, out var graphics) ? graphics : GraphicsFormat.None;

        /// The texture format whose texels a graphics format's data has (the first with that
        /// graphics format; its sRGB twin for an sRGB one); 0 for none.
        internal static TextureFormat GetTextureFormat(GraphicsFormat format)
        {
            foreach (var (texture, graphics) in graphicsFormats)
                if (graphics == format) return texture;
            return format switch
            {
                GraphicsFormat.R8G8B8A8_SRGB => TextureFormat.RGBA32,
                GraphicsFormat.B8G8R8A8_SRGB => TextureFormat.BGRA32,
                GraphicsFormat.R8G8B8_SRGB => TextureFormat.RGB24,
                _ => 0,
            };
        }
    }
}

namespace OniBrutal.Engine
{
    /// Minimal PNG writer: one IHDR, one zlib-compressed IDAT, IEND.
    internal static class Png
    {
        public static byte[] Encode(int width, int height, int channels, byte[] filteredRows)
        {
            using var output = new MemoryStream();
            output.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
            var header = new byte[13];
            WriteBigEndian(header, 0, width);
            WriteBigEndian(header, 4, height);
            header[8] = 8;
            header[9] = channels switch { 1 => 0, 3 => 2, _ => 6 };
            Chunk(output, "IHDR", header);
            using (var compressed = new MemoryStream())
            {
                using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) z.Write(filteredRows);
                Chunk(output, "IDAT", compressed.ToArray());
            }
            Chunk(output, "IEND", Array.Empty<byte>());
            return output.ToArray();
        }

        static void Chunk(Stream s, string type, byte[] data)
        {
            var length = new byte[4];
            WriteBigEndian(length, 0, data.Length);
            s.Write(length);
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes);
            s.Write(data);
            var crc = new Crc32();
            crc.Append(typeBytes);
            crc.Append(data);
            var c = new byte[4];
            WriteBigEndian(c, 0, (int)crc.Current);
            s.Write(c);
        }

        static void WriteBigEndian(byte[] b, int offset, int value)
        {
            b[offset] = (byte)(value >> 24);
            b[offset + 1] = (byte)(value >> 16);
            b[offset + 2] = (byte)(value >> 8);
            b[offset + 3] = (byte)value;
        }
    }
}
