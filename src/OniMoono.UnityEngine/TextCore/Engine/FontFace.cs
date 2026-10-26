using System;
using System.Runtime.InteropServices;
using UnityEngine.TextCore.LowLevel;

namespace OniMoono.Engine.Text
{
    /// One FreeType face over font file bytes kept pinned for the face's lifetime, with its
    /// sfnt tables. Not thread-safe: callers lock on the face.
    internal sealed unsafe class FontFace : IDisposable
    {
        readonly GCHandle pin;
        FT.FaceRec* face;

        public readonly byte[] Data;
        public readonly int FaceIndex;
        public readonly SfntTables Tables;
        /// Current pixels-per-EM (FontEngine point size at 72 dpi).
        public int PixelSize { get; private set; }

        FontFace(byte[] data, int faceIndex, FT.FaceRec* face)
        {
            Data = data;
            FaceIndex = faceIndex;
            this.face = face;
            Tables = new SfntTables(data, faceIndex);
        }

        FontFace(byte[] data, int faceIndex, FT.FaceRec* face, GCHandle pin) : this(data, faceIndex, face) => this.pin = pin;

        /// Opens face `faceIndex` of a font file held in memory; FontEngineError on failure.
        public static FontEngineError TryOpen(byte[] data, int faceIndex, out FontFace result)
        {
            result = null;
            if (data == null || data.Length == 0) return FontEngineError.Invalid_File;
            var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            int error = FT.FT_New_Memory_Face(FT.Library, (byte*)pin.AddrOfPinnedObject(), new CLong(data.Length), new CLong(faceIndex), out var face);
            if (error != 0)
            {
                pin.Free();
                return ToEngineError(error);
            }
            result = new FontFace(data, faceIndex, face, pin);
            return FontEngineError.Success;
        }

        /// FreeType error codes map onto FontEngineError by value where Unity defines them.
        public static FontEngineError ToEngineError(int ftError) =>
            Enum.IsDefined(typeof(FontEngineError), ftError) ? (FontEngineError)ftError : FontEngineError.Invalid_Face;

        public FT.FaceRec* Handle => face;

        public int FaceCount => (int)face->num_faces.Value;
        public string FamilyName => FT.String(face->family_name);
        public string StyleName => FT.String(face->style_name);
        public int UnitsPerEm => face->units_per_EM;
        public short Ascender => face->ascender;
        public short Descender => face->descender;
        public short Height => face->height;
        public short UnderlinePosition => face->underline_position;
        public short UnderlineThickness => face->underline_thickness;
        public bool IsScalable => (face->face_flags.Value & 1) != 0;

        public FontEngineError SetPixelSize(int size)
        {
            if (size == PixelSize) return FontEngineError.Success;
            int error = FT.FT_Set_Pixel_Sizes(face, 0, (uint)Math.Max(size, 1));
            if (error != 0) return ToEngineError(error);
            PixelSize = size;
            return FontEngineError.Success;
        }

        public uint GlyphIndex(uint unicode) => FT.FT_Get_Char_Index(face, new CULong(unicode));

        public uint VariantGlyphIndex(uint unicode, uint selector) => FT.FT_Face_GetCharVariantIndex(face, new CULong(unicode), new CULong(selector));

        public FT.GlyphSlotRec* Slot => face->glyph;

        public bool Load(uint glyphIndex, int flags) => FT.FT_Load_Glyph(face, glyphIndex, flags) == 0;

        public void Dispose()
        {
            if (face == null) return;
            FT.FT_Done_Face(face);
            face = null;
            if (pin.IsAllocated) pin.Free();
        }

        /// Family and style names of every face in a font file, with FreeType's bold and italic
        /// style flags (from the OS/2 and head tables) and whether it is scalable (has outlines),
        /// without keeping it open.
        public static (string Family, string Style, bool Bold, bool Italic, bool Scalable)[] ReadFaceNames(string path)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(path + "\0");
            fixed (byte* p = bytes)
            {
                if (FT.FT_New_Face(FT.Library, p, new CLong(0), out var first) != 0) return Array.Empty<(string, string, bool, bool, bool)>();
                int count = (int)first->num_faces.Value;
                var names = new (string, string, bool, bool, bool)[count];
                names[0] = Describe(first);
                FT.FT_Done_Face(first);
                for (int i = 1; i < count; i++)
                {
                    if (FT.FT_New_Face(FT.Library, p, new CLong(i), out var f) != 0) continue;
                    names[i] = Describe(f);
                    FT.FT_Done_Face(f);
                }
                return names;
            }

            static (string, string, bool, bool, bool) Describe(FT.FaceRec* f)
            {
                long flags = (long)f->style_flags.Value;
                return (FT.String(f->family_name), FT.String(f->style_name), (flags & FT.STYLE_FLAG_BOLD) != 0, (flags & FT.STYLE_FLAG_ITALIC) != 0,
                    ((long)f->face_flags.Value & 1) != 0);
            }
        }
    }

    /// A glyph rasterized for an atlas: 8-bit coverage/field rows top-down (or RGBA for colour
    /// glyphs), its size in texels and the placement metrics in pixels.
    internal sealed class GlyphBitmap
    {
        public int Width, Height;
        /// Width×Height bytes (Alpha) or Width×Height×4 (RGBA, straight alpha), rows top-down.
        public byte[] Pixels;
        public bool Rgba;
        public float BearingX, BearingY, Advance;
    }

    /// Rasterization of glyphs in Unity's GlyphRenderMode variants.
    internal static unsafe class GlyphRasterizer
    {
        // GlyphRasterModes bits.
        const int ModeMono = 0x2, ModeNoHinting = 0x4, ModeSdf = 0x20, ModeSdfaa = 0x40, Mode8x = 0x2000, Mode16x = 0x4000, Mode32x = 0x8000, ModeColor = 0x10000;

        public static bool IsSdf(GlyphRenderMode mode) => ((int)mode & (ModeSdf | ModeSdfaa)) != 0;

        /// FreeType load flags for a render mode: hinting per the mode, embedded bitmaps only
        /// for the colour modes.
        public static int LoadFlags(GlyphRenderMode mode)
        {
            int m = (int)mode;
            int flags = (m & ModeNoHinting) != 0 ? FT.LOAD_NO_HINTING : FT.LOAD_DEFAULT;
            if ((m & ModeColor) != 0) flags |= FT.LOAD_COLOR;
            else flags |= FT.LOAD_NO_BITMAP;
            if ((m & ModeMono) != 0 && (m & (ModeSdf | ModeSdfaa | ModeNoHinting)) == 0) flags |= FT.LOAD_TARGET_MONO;
            return flags;
        }

        /// Renders the glyph at the face's current size. SDF modes produce the field over the
        /// glyph plus `padding` texels on each side (the returned size includes the padding);
        /// bitmap modes produce the glyph bitmap only.
        public static GlyphBitmap Render(FontFace face, uint glyphIndex, GlyphRenderMode mode, int padding)
        {
            if (!face.Load(glyphIndex, LoadFlags(mode))) return null;
            var slot = face.Slot;
            int m = (int)mode;
            if (IsSdf(mode)) return RenderSdf(face, slot, m, padding);
            if (slot->format == FT.GLYPH_FORMAT_OUTLINE && FT.FT_Render_Glyph(slot, (m & ModeMono) != 0 ? FT.RENDER_MODE_MONO : FT.RENDER_MODE_NORMAL) != 0) return null;
            var g = CopyBitmap(&slot->bitmap);
            g.BearingX = slot->bitmap_left;
            g.BearingY = slot->bitmap_top;
            g.Advance = slot->metrics.horiAdvance.Value / 64f;
            return g;
        }

        static GlyphBitmap RenderSdf(FontFace face, FT.GlyphSlotRec* slot, int m, int padding)
        {
            int scale = (m & Mode32x) != 0 ? 32 : (m & Mode16x) != 0 ? 16 : (m & Mode8x) != 0 ? 8 : 1;
            bool antialiased = (m & ModeSdfaa) != 0;
            var g = new GlyphBitmap { Advance = slot->metrics.horiAdvance.Value / 64f };
            if (slot->format != FT.GLYPH_FORMAT_OUTLINE) return null;
            FT.FT_Outline_Get_CBox(&slot->outline, out var box);
            int xMin = FloorPx(box.xMin.Value), yMin = FloorPx(box.yMin.Value), xMax = CeilPx(box.xMax.Value), yMax = CeilPx(box.yMax.Value);
            int w = xMax - xMin, h = yMax - yMin;
            g.BearingX = xMin;
            g.BearingY = yMax;
            if (w <= 0 || h <= 0 || slot->outline.n_points == 0)
            {
                g.Width = g.Height = 0;
                g.Pixels = Array.Empty<byte>();
                return g;
            }
            int pw = w + 2 * padding, ph = h + 2 * padding;
            int bw = pw * scale, bh = ph * scale;
            FT.FT_Outline_Translate(&slot->outline, new CLong((nint)(padding - xMin) * 64), new CLong((nint)(padding - yMin) * 64));
            if (scale != 1)
            {
                var matrix = new FT.Matrix { xx = new CLong(scale << 16), yy = new CLong(scale << 16) };
                FT.FT_Outline_Transform(&slot->outline, &matrix);
            }
            var coverage = Rasterize(&slot->outline, bw, bh, antialiased);
            g.Width = pw;
            g.Height = ph;
            g.Pixels = DistanceField.Generate(coverage, bw, bh, scale, padding + 1);
            return g;
        }

        static int FloorPx(nint v) => (int)Math.Floor(v / 64.0);
        static int CeilPx(nint v) => (int)Math.Ceiling(v / 64.0);

        /// Coverage of an outline (already placed in bitmap space) over a w×h grid, top row
        /// first: anti-aliased (0..1) or binary.
        static double[] Rasterize(FT.Outline* outline, int w, int h, bool antialiased)
        {
            int pitch = antialiased ? w : (w + 7) / 8;
            var buffer = new byte[pitch * h];
            var cov = new double[w * h];
            fixed (byte* p = buffer)
            {
                var bitmap = new FT.Bitmap
                {
                    rows = (uint)h,
                    width = (uint)w,
                    pitch = pitch,
                    buffer = p,
                    num_grays = (ushort)(antialiased ? 256 : 2),
                    pixel_mode = antialiased ? FT.PIXEL_MODE_GRAY : FT.PIXEL_MODE_MONO,
                };
                FT.FT_Outline_Get_Bitmap(FT.Library, outline, &bitmap);
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    cov[y * w + x] = antialiased
                        ? buffer[y * pitch + x] / 255.0
                        : (buffer[y * pitch + (x >> 3)] & (0x80 >> (x & 7))) != 0 ? 1.0 : 0.0;
            return cov;
        }

        /// Copies a rendered FreeType bitmap: gray as is, mono expanded to 0/255, BGRA
        /// (premultiplied) to straight-alpha RGBA.
        public static GlyphBitmap CopyBitmap(FT.Bitmap* b)
        {
            int w = (int)b->width, h = (int)b->rows;
            var g = new GlyphBitmap { Width = w, Height = h };
            bool rgba = b->pixel_mode == FT.PIXEL_MODE_BGRA;
            g.Rgba = rgba;
            g.Pixels = new byte[w * h * (rgba ? 4 : 1)];
            for (int y = 0; y < h; y++)
            {
                byte* row = b->pitch >= 0 ? b->buffer + y * b->pitch : b->buffer + (h - 1 - y) * -b->pitch;
                for (int x = 0; x < w; x++)
                {
                    switch (b->pixel_mode)
                    {
                        case FT.PIXEL_MODE_MONO:
                            g.Pixels[y * w + x] = (row[x >> 3] & (0x80 >> (x & 7))) != 0 ? (byte)255 : (byte)0;
                            break;
                        case FT.PIXEL_MODE_BGRA:
                        {
                            byte bl = row[4 * x], gr = row[4 * x + 1], r = row[4 * x + 2], a = row[4 * x + 3];
                            int o = 4 * (y * w + x);
                            g.Pixels[o] = Unpremultiply(r, a);
                            g.Pixels[o + 1] = Unpremultiply(gr, a);
                            g.Pixels[o + 2] = Unpremultiply(bl, a);
                            g.Pixels[o + 3] = a;
                            break;
                        }
                        default:
                            g.Pixels[y * w + x] = b->num_grays == 256 || b->num_grays == 0 ? row[x] : (byte)(row[x] * 255 / Math.Max(b->num_grays - 1, 1));
                            break;
                    }
                }
            }
            return g;
        }

        static byte Unpremultiply(byte c, byte a) => a == 0 ? (byte)0 : (byte)Math.Min(255, (c * 255 + a / 2) / a);
    }
}
