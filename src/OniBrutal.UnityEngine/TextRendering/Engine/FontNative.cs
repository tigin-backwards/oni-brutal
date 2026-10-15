using System;
using System.Collections.Generic;
using System.IO;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine.Assets;
using OniBrutal.Engine.Rendering;
using OniBrutal.Engine.Serialization;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace OniBrutal.Engine.Text
{
    /// A legacy (TextRendering) glyph at one pixel size and style: layout metrics in pixels
    /// (y up from the baseline) and, once requested, its place in the font texture.
    internal struct LegacyGlyph
    {
        public bool Exists;
        public float Advance;
        public int Left, Top, Width, Height;
        /// Texture placement (bottom-left texel) when InTexture.
        public bool InTexture;
        public int X, Y;
        /// UV rectangle for non-dynamic (bitmap) fonts, from the character rects.
        public Rect Uv;
        public bool Flipped;
        public int LastUsedFrame;
    }

    internal readonly record struct GlyphKey(int CodePoint, int Size, FontStyle Style);

    /// Engine side of UnityEngine.Font (native class 128): the serialized font (TTF/OTF data,
    /// names, metrics at m_FontSize, character rects of bitmap fonts) and, for dynamic fonts,
    /// the glyph cache rendered into the font texture.
    internal sealed class FontNative : NativeObject
    {
        // Unity's m_ConvertCase values.
        public const int DynamicFont = -2, UnicodeSet = -1, DontConvertCase = 0, UpperCase = 1, LowerCase = 2;

        public float LineSpacing;
        public Material Material;
        public float FontSize;
        public Texture2D Texture;
        public int AsciiStartOffset;
        public float Tracking = 1f;
        public int CharacterSpacing;
        public int CharacterPadding = 1;
        public int ConvertCase = DontConvertCase;
        public Dictionary<int, LegacyGlyph> CharacterRects = new();
        public Dictionary<(int, int), float> Kerning = new();
        public float PixelScale = 0.1f;
        public byte[] Data = Array.Empty<byte>();
        public float Ascent, Descent;
        public uint DefaultStyle;
        public string[] FontNames = Array.Empty<string>();
        public Font[] Fallbacks = Array.Empty<Font>();
        public int FontRenderingMode;
        public bool ShouldRoundAdvanceValue = true;

        public bool Dynamic => ConvertCase == DynamicFont;

        public override void Bind(Fields f, IReferenceResolver resolver)
        {
            Serialized = null;
            LineSpacing = f.Float("m_LineSpacing");
            Material = resolver.Resolve(f.PPtr("m_DefaultMaterial")) as Material;
            FontSize = f.Float("m_FontSize");
            Texture = resolver.Resolve(f.PPtr("m_Texture")) as Texture2D;
            AsciiStartOffset = f.Int("m_AsciiStartOffset");
            Tracking = f.Float("m_Tracking");
            CharacterSpacing = f.Int("m_CharacterSpacing");
            CharacterPadding = f.Int("m_CharacterPadding");
            ConvertCase = f.Int("m_ConvertCase");
            foreach (Fields c in f.Array("m_CharacterRects"))
            {
                var uv = c.Struct("uv");
                var vert = c.Struct("vert");
                // vert is relative to the pen position with y up; a negative height extends
                // downwards from vert.y.
                float vx = vert.Float("x"), vy = vert.Float("y"), vw = vert.Float("width"), vh = vert.Float("height");
                float top = Math.Max(vy, vy + vh), bottom = Math.Min(vy, vy + vh);
                CharacterRects[(int)c.Get<uint>("index")] = new LegacyGlyph
                {
                    Exists = true,
                    Advance = c.Float("advance"),
                    Left = (int)Math.Floor(Math.Min(vx, vx + vw)),
                    Top = (int)Math.Ceiling(top),
                    Width = (int)Math.Round(Math.Abs(vw)),
                    Height = (int)Math.Round(top - bottom),
                    Uv = new Rect(uv.Float("x"), uv.Float("y"), uv.Float("width"), uv.Float("height")),
                    Flipped = c.Bool("flipped"),
                };
            }
            foreach (Fields pair in f.Array("m_KerningValues"))
            {
                var chars = pair.Struct("first");
                Kerning[(chars.Get<ushort>("first"), chars.Get<ushort>("second"))] = pair.Float("second");
            }
            PixelScale = f.Float("m_PixelScale");
            Data = f.Get<byte[]>("m_FontData");
            Ascent = f.Float("m_Ascent");
            Descent = f.Float("m_Descent");
            DefaultStyle = f.Get<uint>("m_DefaultStyle");
            var names = f.Array("m_FontNames");
            FontNames = new string[names.Length];
            for (int i = 0; i < names.Length; i++) FontNames[i] = (string)names[i];
            var fallbacks = new List<Font>();
            foreach (PPtr p in f.Array("m_FallbackFonts"))
                if (resolver.Resolve(p) is Font fallback) fallbacks.Add(fallback);
            Fallbacks = fallbacks.ToArray();
            FontRenderingMode = f.Int("m_FontRenderingMode");
            ShouldRoundAdvanceValue = f.Bool("m_ShouldRoundAdvanceValue");
        }

        // ----- Font data -----

        byte[] faceData;
        bool faceDataResolved;

        /// The TrueType/OpenType data glyphs come from: the font's own data; for a font
        /// without data (built without "Include Font Data", or Unity's built-in LegacyRuntime),
        /// the installed OS font named in m_FontNames, else the built-in default face.
        /// The built-in LegacyRuntime font always uses the built-in default face, Liberation
        /// Sans (the face Unity's player embeds for it: its serialized ascent, descent and line
        /// spacing are Liberation Sans's), as do fonts whose OS fonts are not installed.
        public byte[] FaceData
        {
            get
            {
                if (faceDataResolved) return faceData;
                if (Data.Length > 0) faceData = Data;
                else
                {
                    if (Source.File != "Resources/unity default resources")
                        foreach (var name in FontNames)
                            if (SystemFonts.TryFindFamily(name, out var r))
                            {
                                faceData = File.ReadAllBytes(r.filePath);
                                faceIndex = r.faceIndex;
                                break;
                            }
                    faceData ??= BuiltinFont.Data;
                }
                faceDataResolved = true;
                return faceData;
            }
        }

        int faceIndex;

        // ----- Glyphs (dynamic fonts) -----

        readonly object gate = new();
        List<FontFace> faces;
        readonly Dictionary<GlyphKey, LegacyGlyph> glyphs = new();
        readonly List<GlyphRect> freeRects = new(), usedRects = new();

        /// This font's face, then its fallback fonts' faces. Separate FreeType faces from the
        /// TextCore font engine's (each keeps its own size).
        List<FontFace> Faces
        {
            get
            {
                if (faces != null) return faces;
                faces = new List<FontFace>();
                AddFace(FaceData, faceIndex);
                foreach (var fallback in Fallbacks)
                {
                    if (fallback == null) continue;
                    var fn = (FontNative)fallback.NativeOrThrow;
                    if (fn != this) AddFace(fn.FaceData, fn.faceIndex);
                }
                return faces;
            }
        }

        void AddFace(byte[] data, int index)
        {
            if (data == null || data.Length == 0) return;
            if (FontFace.TryOpen(data, index, out var face) == FontEngineError.Success) faces.Add(face);
        }

        /// Line metrics at a pixel size, from the serialized values at m_FontSize.
        public void LineMetrics(int size, out float ascent, out float descent, out float lineHeight)
        {
            float k = FontSize > 0 ? size / FontSize : 1f;
            ascent = Ascent * k;
            descent = Descent * k;
            lineHeight = LineSpacing * k;
        }

        public int MapCase(int c) => ConvertCase switch
        {
            UpperCase => char.ToUpperInvariant((char)c),
            LowerCase => char.ToLowerInvariant((char)c),
            _ => c,
        };

        public bool HasCharacter(int c)
        {
            if (!Dynamic) return CharacterRects.ContainsKey(MapCase(c));
            lock (gate)
            {
                foreach (var face in Faces)
                    if (face.GlyphIndex((uint)c) != 0) return true;
                return false;
            }
        }

        /// Layout metrics of a glyph (dynamic fonts: rendered by FreeType at `size` pixels with
        /// synthesized bold/italic; bitmap fonts: the character rect).
        public LegacyGlyph GetGlyph(int codePoint, int size, FontStyle style)
        {
            if (!Dynamic) return CharacterRects.GetValueOrDefault(MapCase(codePoint));
            lock (gate)
            {
                var key = new GlyphKey(codePoint, size, style);
                if (glyphs.TryGetValue(key, out var g)) return g;
                g = Measure(key);
                glyphs[key] = g;
                return g;
            }
        }

        int LoadFlags => FontRenderingMode switch
        {
            1 => FT.LOAD_DEFAULT | FT.LOAD_NO_BITMAP,
            2 => FT.LOAD_TARGET_MONO | FT.LOAD_NO_BITMAP,
            // Smooth and OS default: anti-aliased, unhinted.
            _ => FT.LOAD_NO_HINTING | FT.LOAD_NO_BITMAP,
        };

        /// Loads a glyph into its face's slot with the style applied; null when no face has it.
        unsafe FontFace LoadStyled(GlyphKey key)
        {
            foreach (var face in Faces)
            {
                uint index = face.GlyphIndex((uint)key.CodePoint);
                if (index == 0) continue;
                if (face.SetPixelSize(Math.Max(key.Size, 1)) != FontEngineError.Success) return null;
                if (!face.Load(index, LoadFlags)) return null;
                var slot = face.Slot;
                if (slot->format == FT.GLYPH_FORMAT_OUTLINE)
                {
                    if (key.Style is FontStyle.Italic or FontStyle.BoldAndItalic) FT.FT_GlyphSlot_Oblique(slot);
                    if (key.Style is FontStyle.Bold or FontStyle.BoldAndItalic) FT.FT_GlyphSlot_Embolden(slot);
                }
                return face;
            }
            return null;
        }

        unsafe LegacyGlyph Measure(GlyphKey key)
        {
            var face = LoadStyled(key);
            if (face == null) return default;
            var slot = face.Slot;
            var g = new LegacyGlyph { Exists = true, Advance = slot->advance.x / 64f };
            if (ShouldRoundAdvanceValue) g.Advance = (float)Math.Round(g.Advance, MidpointRounding.AwayFromZero);
            if (slot->format == FT.GLYPH_FORMAT_OUTLINE)
            {
                // The bitmap FreeType renders covers the outline's control box rounded out to
                // whole pixels.
                FT.FT_Outline_Get_CBox(&slot->outline, out var box);
                int xMin = (int)Math.Floor(box.xMin / 64.0), yMin = (int)Math.Floor(box.yMin / 64.0);
                int xMax = (int)Math.Ceiling(box.xMax / 64.0), yMax = (int)Math.Ceiling(box.yMax / 64.0);
                if (slot->outline.n_points > 0 && xMax > xMin && yMax > yMin)
                {
                    g.Left = xMin;
                    g.Top = yMax;
                    g.Width = xMax - xMin;
                    g.Height = yMax - yMin;
                }
            }
            return g;
        }

        // ----- Font texture (dynamic fonts) -----

        const int InitialTextureSize = 256, MaxTextureSize = 4096;

        Texture2DNative TextureData
        {
            get
            {
                if (Texture == null)
                {
                    Texture = new Texture2D(0, 0, TextureFormat.Alpha8, false) { name = "Font Texture" };
                    if (Material != null) Material.mainTexture = Texture;
                }
                return (Texture2DNative)Texture.NativeOrThrow;
            }
        }

        /// Makes sure every requested glyph is in the font texture. When they do not all fit,
        /// the texture is rebuilt with only the glyphs used this frame plus the request,
        /// growing (width, then height, alternately) while they do not fit; true when the
        /// texture was rebuilt (the caller then raises Font.textureRebuilt, after which every
        /// text using this font must regenerate).
        public bool RequestGlyphs(IEnumerable<GlyphKey> keys)
        {
            bool rebuilt = RequestGlyphsCore(keys);
            if (rebuilt) AtlasVersion++;
            return rebuilt;
        }

        /// Incremented whenever the font texture is rebuilt (glyph UVs move).
        public int AtlasVersion;

        bool RequestGlyphsCore(IEnumerable<GlyphKey> keys)
        {
            lock (gate)
            {
                var t = TextureData;
                int frame = Time.frameCount;
                var request = new List<GlyphKey>();
                var requested = new HashSet<GlyphKey>();
                foreach (var key in keys)
                {
                    var g = GetGlyph(key.CodePoint, key.Size, key.Style);
                    g.LastUsedFrame = frame;
                    glyphs[key] = g;
                    if (g.Exists && g.Width > 0 && g.Height > 0 && requested.Add(key)) request.Add(key);
                }
                bool rebuilt = false;
                if (t.Width <= 0 || t.Height <= 0 || t.Format != TextureFormat.Alpha8 || t.Memory == null)
                {
                    Reset(t, Math.Max(t.Width, InitialTextureSize), Math.Max(t.Height, InitialTextureSize));
                    rebuilt = true;
                }
                var missing = request.FindAll(k => !glyphs[k].InTexture);
                if (missing.Count == 0)
                {
                    if (rebuilt) t.ContentChanged();
                    return rebuilt;
                }
                if (!PackAll(t, missing))
                {
                    var keep = new List<GlyphKey>();
                    foreach (var (key, g) in glyphs)
                        if (g.Exists && g.Width > 0 && g.Height > 0 && (g.LastUsedFrame == frame || requested.Contains(key))) keep.Add(key);
                    int w = t.Width, h = t.Height;
                    while (true)
                    {
                        Reset(t, w, h);
                        if (PackAll(t, keep) || (w >= MaxTextureSize && h >= MaxTextureSize)) break;
                        if (w <= h) w = Math.Min(w * 2, MaxTextureSize); else h = Math.Min(h * 2, MaxTextureSize);
                    }
                    rebuilt = true;
                }
                t.ContentChanged();
                return rebuilt;
            }
        }

        void Reset(Texture2DNative t, int width, int height)
        {
            t.Width = width;
            t.Height = height;
            t.Format = TextureFormat.Alpha8;
            t.MipCount = 1;
            t.Readable = true;
            t.Allocate(t.SliceSize, 0);
            freeRects.Clear();
            usedRects.Clear();
            freeRects.Add(new GlyphRect(0, 0, width, height));
            foreach (var key in new List<GlyphKey>(glyphs.Keys))
            {
                var g = glyphs[key];
                g.InTexture = false;
                glyphs[key] = g;
            }
        }

        /// Packs and renders glyphs (with m_CharacterPadding texels around each); false when
        /// one does not fit (glyphs packed before it stay).
        bool PackAll(Texture2DNative t, List<GlyphKey> keys)
        {
            int pad = Math.Max(CharacterPadding, 0);
            foreach (var key in keys)
            {
                var g = glyphs[key];
                if (g.InTexture) continue;
                if (!GlyphPacker.TryPack(g.Width + 2 * pad, g.Height + 2 * pad, GlyphPackingMode.BestShortSideFit, t.Width, t.Height, freeRects, usedRects, out var placed))
                    return false;
                g.X = placed.x + pad;
                g.Y = placed.y + pad;
                g.InTexture = true;
                Render(t, key, ref g);
                glyphs[key] = g;
            }
            return true;
        }

        unsafe void Render(Texture2DNative t, GlyphKey key, ref LegacyGlyph g)
        {
            var face = LoadStyled(key);
            if (face == null) return;
            var slot = face.Slot;
            if (slot->format == FT.GLYPH_FORMAT_OUTLINE && FT.FT_Render_Glyph(slot, FontRenderingMode == 2 ? FT.RENDER_MODE_MONO : FT.RENDER_MODE_NORMAL) != 0) return;
            var bitmap = GlyphRasterizer.CopyBitmap(&slot->bitmap);
            // The rendered bitmap is normally exactly the measured box; anything beyond it is
            // clipped to the reserved rectangle.
            var clipped = new GlyphBitmap { Width = g.Width, Height = g.Height, Pixels = new byte[g.Width * g.Height] };
            int dx = slot->bitmap_left - g.Left, dy = g.Top - slot->bitmap_top;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int cx = x + dx, cy = y + dy;
                    if (cx < 0 || cy < 0 || cx >= g.Width || cy >= g.Height) continue;
                    clipped.Pixels[cy * g.Width + cx] = bitmap.Rgba ? bitmap.Pixels[4 * (y * bitmap.Width + x) + 3] : bitmap.Pixels[y * bitmap.Width + x];
                }
            FontEngine.Blit(t, clipped, g.X, g.Y);
        }

        /// UV rectangle (x, y, width, height) of a glyph placed in the texture.
        public Rect UvOf(in LegacyGlyph g)
        {
            if (!Dynamic) return g.Uv;
            var t = TextureData;
            return new Rect((float)g.X / t.Width, (float)g.Y / t.Height, (float)g.Width / t.Width, (float)g.Height / t.Height);
        }

        public LegacyGlyph PlacedGlyph(GlyphKey key)
        {
            lock (gate) return glyphs.GetValueOrDefault(key);
        }
    }

    /// The built-in default font face (what Unity's player embeds for LegacyRuntime):
    /// Liberation Sans Regular, taken from the game's own copy of it (a Font asset with that
    /// family in its m_FontNames and data included), else from the installed OS font.
    internal static class BuiltinFont
    {
        static byte[] data;
        static readonly object gate = new();

        public static byte[] Data
        {
            get
            {
                lock (gate)
                {
                    if (data != null) return data;
                    data = FromGameData() ?? FromSystem() ?? Array.Empty<byte>();
                    if (data.Length == 0) Debug.LogError("The built-in font (Liberation Sans) was found neither in the game data nor among the installed fonts; text in fonts without data will not render.");
                    return data;
                }
            }
        }

        static byte[] FromGameData()
        {
            if (string.IsNullOrEmpty(AssetSystem.DataDir)) return null;
            var names = new List<string> { "resources.assets" };
            for (int i = 0; File.Exists(Path.Combine(AssetSystem.DataDir, $"sharedassets{i}.assets")); i++) names.Add($"sharedassets{i}.assets");
            foreach (var name in names)
            {
                if (!File.Exists(Path.Combine(AssetSystem.DataDir, name))) continue;
                var file = AssetSystem.Get(name);
                foreach (var info in file.Objects)
                {
                    if (file.TypeOf(info).ClassId != 128) continue;
                    var f = TypeTreeReader.ReadNative(128, file.OpenObject(info));
                    var bytes = f.Get<byte[]>("m_FontData");
                    if (bytes.Length == 0 || f.Get<uint>("m_DefaultStyle") != 0) continue;
                    foreach (var n in f.Array("m_FontNames"))
                        if ((string)n == "Liberation Sans") return bytes;
                }
            }
            return null;
        }

        static byte[] FromSystem() => SystemFonts.TryFind("Liberation Sans", "Regular", out var r) ? File.ReadAllBytes(r.filePath) : null;
    }
}
