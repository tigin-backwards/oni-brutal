using System;
using System.IO;
using System.IO.Compression;
using Brutal.StbApi;
using OniBrutal.Engine;
using OniBrutal.Engine.Rendering;

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
            int channels = format switch
            {
                TextureFormat.RGB24 or TextureFormat.RGB565 or TextureFormat.DXT1 or TextureFormat.RGB48 => 3,
                TextureFormat.R8 or TextureFormat.R16 or TextureFormat.RHalf or TextureFormat.RFloat or TextureFormat.BC4 => 1,
                _ => 4,
            };
            int w = native.Width, h = native.Height;
            var mip = native.Memory.Slice(0, native.MipSize(0));
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
