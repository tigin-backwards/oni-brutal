using System;
using System.Runtime.InteropServices;

namespace OniBrutal.Engine.Text
{
    /// P/Invokes into FreeType 2 (BRUTAL's build, Ahwoo.BRUTAL.FreeType: freetype.dll /
    /// libfreetype.so; Unity links FreeType statically, the game ships none). Struct layouts are
    /// FreeType's public records; FT_Long/FT_Pos/FT_Fixed are C `long` (CLong: 64-bit on Linux,
    /// 32-bit on Windows) and FT_ULong is CULong. Only the leading fields we read are declared
    /// where FreeType documents the rest as private.
    internal static unsafe class FT
    {
        const string Lib = "freetype";

        // FT_LOAD_* (GlyphLoadFlags has the same values).
        public const int LOAD_DEFAULT = 0, LOAD_NO_SCALE = 1, LOAD_NO_HINTING = 2, LOAD_RENDER = 4, LOAD_NO_BITMAP = 8,
            LOAD_FORCE_AUTOHINT = 32, LOAD_MONOCHROME = 4096, LOAD_NO_AUTOHINT = 32768, LOAD_COLOR = 1 << 20,
            LOAD_TARGET_MONO = 2 << 16;

        // FT_Render_Mode.
        public const int RENDER_MODE_NORMAL = 0, RENDER_MODE_MONO = 2;

        // FT_Pixel_Mode.
        public const byte PIXEL_MODE_MONO = 1, PIXEL_MODE_GRAY = 2, PIXEL_MODE_BGRA = 7;

        // FT_Glyph_Format 'outl'.
        public const uint GLYPH_FORMAT_OUTLINE = 0x6F75746C;

        public const long FACE_FLAG_KERNING = 1 << 6;

        // FT_STYLE_FLAG_*.
        public const long STYLE_FLAG_ITALIC = 1, STYLE_FLAG_BOLD = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct Vector
        {
            public CLong x, y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BBox
        {
            public CLong xMin, yMin, xMax, yMax;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Matrix
        {
            public CLong xx, xy, yx, yy;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Generic
        {
            public IntPtr data, finalizer;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Bitmap
        {
            public uint rows;
            public uint width;
            public int pitch;
            public byte* buffer;
            public ushort num_grays;
            public byte pixel_mode;
            public byte palette_mode;
            public IntPtr palette;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Outline
        {
            public short n_contours;
            public short n_points;
            public Vector* points;
            public byte* tags;
            public short* contours;
            public int flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GlyphMetrics
        {
            public CLong width, height, horiBearingX, horiBearingY, horiAdvance, vertBearingX, vertBearingY, vertAdvance;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GlyphSlotRec
        {
            public IntPtr library;
            public FaceRec* face;
            public IntPtr next;
            public uint glyph_index;
            public Generic generic;
            public GlyphMetrics metrics;
            public CLong linearHoriAdvance;
            public CLong linearVertAdvance;
            public Vector advance;
            public uint format;
            public Bitmap bitmap;
            public int bitmap_left;
            public int bitmap_top;
            public Outline outline;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SizeMetrics
        {
            public ushort x_ppem, y_ppem;
            public CLong x_scale, y_scale, ascender, descender, height, max_advance;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SizeRec
        {
            public FaceRec* face;
            public Generic generic;
            public SizeMetrics metrics;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FaceRec
        {
            public CLong num_faces;
            public CLong face_index;
            public CLong face_flags;
            public CLong style_flags;
            public CLong num_glyphs;
            public byte* family_name;
            public byte* style_name;
            public int num_fixed_sizes;
            public IntPtr available_sizes;
            public int num_charmaps;
            public IntPtr charmaps;
            public Generic generic;
            public BBox bbox;
            public ushort units_per_EM;
            public short ascender;
            public short descender;
            public short height;
            public short max_advance_width;
            public short max_advance_height;
            public short underline_position;
            public short underline_thickness;
            public GlyphSlotRec* glyph;
            public SizeRec* size;
            public IntPtr charmap;
        }

        [DllImport(Lib)] public static extern int FT_Init_FreeType(out IntPtr library);
        [DllImport(Lib)] public static extern int FT_New_Memory_Face(IntPtr library, byte* fileBase, CLong fileSize, CLong faceIndex, out FaceRec* face);
        [DllImport(Lib)] public static extern int FT_New_Face(IntPtr library, byte* path, CLong faceIndex, out FaceRec* face);
        [DllImport(Lib)] public static extern int FT_Done_Face(FaceRec* face);
        [DllImport(Lib)] public static extern int FT_Set_Pixel_Sizes(FaceRec* face, uint pixelWidth, uint pixelHeight);
        [DllImport(Lib)] public static extern uint FT_Get_Char_Index(FaceRec* face, CULong charcode);
        [DllImport(Lib)] public static extern uint FT_Face_GetCharVariantIndex(FaceRec* face, CULong charcode, CULong variantSelector);
        [DllImport(Lib)] public static extern int FT_Load_Glyph(FaceRec* face, uint glyphIndex, int loadFlags);
        [DllImport(Lib)] public static extern int FT_Render_Glyph(GlyphSlotRec* slot, int renderMode);
        [DllImport(Lib)] public static extern void FT_GlyphSlot_Embolden(GlyphSlotRec* slot);
        [DllImport(Lib)] public static extern void FT_GlyphSlot_Oblique(GlyphSlotRec* slot);
        [DllImport(Lib)] public static extern void FT_Outline_Get_CBox(Outline* outline, out BBox cbox);
        [DllImport(Lib)] public static extern void FT_Outline_Translate(Outline* outline, CLong xOffset, CLong yOffset);
        [DllImport(Lib)] public static extern void FT_Outline_Transform(Outline* outline, Matrix* matrix);
        [DllImport(Lib)] public static extern int FT_Outline_Get_Bitmap(IntPtr library, Outline* outline, Bitmap* bitmap);

        static IntPtr library;
        static readonly object initLock = new();

        /// The process-wide FreeType library handle (created on first use).
        public static IntPtr Library
        {
            get
            {
                if (library != IntPtr.Zero) return library;
                lock (initLock)
                {
                    if (library == IntPtr.Zero)
                    {
                        int error = FT_Init_FreeType(out var lib);
                        if (error != 0) throw new InvalidOperationException($"FT_Init_FreeType failed ({error}).");
                        library = lib;
                    }
                }
                return library;
            }
        }

        public static string String(byte* s) => s == null ? null : Marshal.PtrToStringUTF8((IntPtr)s);
    }
}
