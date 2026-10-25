using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.TextCore.LowLevel;

namespace OniBrutal.Engine.Text
{
    /// The operating system's installed fonts: font files under the system font directories on Linux,
    /// the system and per-user font folders on Windows (what Unity's players scan), and their
    /// faces' family and style names.
    internal static class SystemFonts
    {
        static string[] paths;
        static List<FontReference> faces;
        static readonly object gate = new();

        static IEnumerable<string> Directories()
        {
            if (OperatingSystem.IsWindows())
            {
                yield return Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
                yield break;
            }
            // Not ~/.local/share/fonts: measured on Linux (2026-10-24), the original's
            // Font.GetPathsToOSFonts leaves out the font installed there. ~/.fonts and the
            // system folders are unmeasured (this machine has nothing in ~/.fonts).
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, ".fonts");
            var dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
            if (string.IsNullOrEmpty(dataDirs)) dataDirs = "/usr/local/share:/usr/share";
            foreach (var d in dataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries)) yield return Path.Combine(d, "fonts");
        }

        static bool IsFontFile(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase) || ext.Equals(".otf", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".ttc", StringComparison.OrdinalIgnoreCase) || ext.Equals(".otc", StringComparison.OrdinalIgnoreCase);
        }

        /// Full paths of the installed font files (sorted, each once).
        public static string[] Paths
        {
            get
            {
                lock (gate)
                {
                    if (paths != null) return paths;
                    var set = new SortedSet<string>(StringComparer.Ordinal);
                    foreach (var dir in Directories())
                    {
                        if (!Directory.Exists(dir)) continue;
                        try
                        {
                            foreach (var file in Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
                                if (IsFontFile(file)) set.Add(Path.GetFullPath(file));
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                    paths = new string[set.Count];
                    set.CopyTo(paths);
                    return paths;
                }
            }
        }

        /// Every face of every installed font file.
        public static List<FontReference> Faces
        {
            get
            {
                var all = Paths;
                lock (gate)
                {
                    if (faces != null) return faces;
                    var list = new List<FontReference>();
                    var bits = new List<int>();
                    foreach (var path in all)
                    {
                        var names = FontFace.ReadFaceNames(path);
                        for (int i = 0; i < names.Length; i++)
                            if (names[i].Family != null)
                            {
                                list.Add(new FontReference { familyName = names[i].Family, styleName = names[i].Style ?? "", faceIndex = i, filePath = path });
                                bits.Add((names[i].Bold ? Bold : 0) | (names[i].Italic ? Italic : 0) | (names[i].Scalable ? 0 : NotScalable));
                            }
                    }
                    styleBits = bits;
                    faces = list;
                    return faces;
                }
            }
        }

        /// Style bits of a face: FreeType's bold and italic flags. NotScalable marks a face
        /// without outlines (colour bitmap fonts such as Noto Color Emoji), which the OS font
        /// names leave out (measured).
        public const int Bold = 1, Italic = 2, NotScalable = 4;

        static List<int> styleBits;
        static string[] osFontNames;
        static Dictionary<string, (string Family, int Style)> osFontNameFaces;

        /// The installed fonts by the names Unity's player gives them (Font.GetOSInstalledFontNames,
        /// measured 2026-10-24 on Linux): the family, then " Bold" and " Italic" by the face's
        /// style flags (FreeSans Oblique is "FreeSans Italic"), each name once; by family
        /// (ordinal), then regular, bold, italic and bold italic.
        public static string[] OSFontNames
        {
            get
            {
                var all = Faces;
                lock (gate)
                {
                    if (osFontNames != null) return osFontNames;
                    var entries = new SortedSet<(string Family, int Order)>(Comparer<(string Family, int Order)>.Create((a, b) =>
                    {
                        int c = string.CompareOrdinal(a.Family, b.Family);
                        return c != 0 ? c : a.Order.CompareTo(b.Order);
                    }));
                    for (int i = 0; i < all.Count; i++)
                        if ((styleBits[i] & NotScalable) == 0) entries.Add((all[i].familyName, styleBits[i]));
                    var names = new List<string>(entries.Count);
                    var map = new Dictionary<string, (string, int)>(StringComparer.Ordinal);
                    foreach (var (family, style) in entries)
                    {
                        var name = family + ((style & Bold) != 0 ? " Bold" : "") + ((style & Italic) != 0 ? " Italic" : "");
                        if (map.TryAdd(name, (family, style))) names.Add(name);
                    }
                    osFontNameFaces = map;
                    osFontNames = names.ToArray();
                    return osFontNames;
                }
            }
        }

        /// The family and style an OS font name (one of OSFontNames, matched exactly) stands for.
        public static bool TryResolveOSFontName(string name, out string family, out int style)
        {
            family = null;
            style = 0;
            if (string.IsNullOrEmpty(name)) return false;
            _ = OSFontNames;
            if (!osFontNameFaces.TryGetValue(name, out var face)) return false;
            (family, style) = face;
            return true;
        }

        /// The installed face of a family (exact name) with these style bits.
        public static bool TryFindStyled(string family, int style, out FontReference result)
        {
            var all = Faces;
            for (int i = 0; i < all.Count; i++)
                if (styleBits[i] == style && string.Equals(all[i].familyName, family, StringComparison.Ordinal))
                {
                    result = all[i];
                    return true;
                }
            result = default;
            return false;
        }

        /// Case-insensitive family and style match; a null or empty style means "Regular".
        public static bool TryFind(string familyName, string styleName, out FontReference result)
        {
            result = default;
            if (string.IsNullOrEmpty(familyName)) return false;
            if (string.IsNullOrEmpty(styleName)) styleName = "Regular";
            foreach (var f in Faces)
            {
                if (string.Equals(f.familyName, familyName, StringComparison.OrdinalIgnoreCase) && string.Equals(f.styleName, styleName, StringComparison.OrdinalIgnoreCase))
                {
                    result = f;
                    return true;
                }
            }
            return false;
        }

        /// The installed face for a family name: its Regular style, else its first face.
        public static bool TryFindFamily(string familyName, out FontReference result)
        {
            if (TryFind(familyName, null, out result)) return true;
            foreach (var f in Faces)
            {
                if (string.Equals(f.familyName, familyName, StringComparison.OrdinalIgnoreCase))
                {
                    result = f;
                    return true;
                }
            }
            return false;
        }
    }
}
