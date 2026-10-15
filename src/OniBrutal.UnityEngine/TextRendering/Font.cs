using System;
using OniBrutal.Engine.Text;

namespace UnityEngine
{
    public partial class Font
    {
        /// An empty font: no data, no characters, not dynamic.
        public Font()
        {
            Bind(new FontNative());
        }

        FontNative Data => (FontNative)NativeOrThrow;

        internal override void EngineOnDestroy() => Data.ReleaseFaces();

        /// Raised after a dynamic font's texture was rebuilt (glyphs moved or were dropped);
        /// every text using the font must be regenerated.
        public static event Action<Font> textureRebuilt;

        internal static void InvokeTextureRebuilt_Internal(Font font) => textureRebuilt?.Invoke(font);

        public Material material
        {
            get => Data.Material;
            set => Data.Material = value;
        }

        public string[] fontNames
        {
            get => (string[])Data.FontNames.Clone();
            set => Data.FontNames = value == null ? Array.Empty<string>() : (string[])value.Clone();
        }

        /// Dynamic fonts render glyphs from font data on demand (m_ConvertCase == -2).
        public bool dynamic => Data.Dynamic;

        /// The size the font was imported at (its metrics and bitmap glyphs are at this size).
        public int fontSize => (int)Data.FontSize;

        public bool HasCharacter(char c) => Data.HasCharacter(c);

        /// Puts the characters, at `size` pixels (0: the font's size) and `style`, into the
        /// font texture; raises textureRebuilt when the texture had to be rebuilt. Bitmap fonts
        /// have all their characters in their texture already.
        public void RequestCharactersInTexture(string characters, int size, FontStyle style)
        {
            var data = Data;
            if (!data.Dynamic || string.IsNullOrEmpty(characters)) return;
            if (size == 0) size = (int)data.FontSize;
            var keys = new System.Collections.Generic.List<GlyphKey>(characters.Length);
            for (int i = 0; i < characters.Length; i++)
            {
                int c = characters[i];
                if (char.IsHighSurrogate(characters, i) && i + 1 < characters.Length && char.IsLowSurrogate(characters[i + 1]))
                    c = char.ConvertToUtf32(characters[i], characters[++i]);
                keys.Add(new GlyphKey(c, size, style));
            }
            if (data.RequestGlyphs(keys)) InvokeTextureRebuilt_Internal(this);
        }

        public void RequestCharactersInTexture(string characters, int size) => RequestCharactersInTexture(characters, size, FontStyle.Normal);

        public void RequestCharactersInTexture(string characters) => RequestCharactersInTexture(characters, 0, FontStyle.Normal);

        public static int GetMaxVertsForString(string str) => str.Length * 4 + 4;

        /// Paths of the font files installed on the system.
        public static string[] GetPathsToOSFonts() => (string[])SystemFonts.Paths.Clone();
    }
}
