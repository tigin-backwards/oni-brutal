using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.TextCore.LowLevel;

namespace OniBrutal.Engine.Text
{
    /// The operating system's installed fonts: font files under the XDG font directories on Linux,
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
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrEmpty(dataHome)) dataHome = Path.Combine(home, ".local", "share");
            yield return Path.Combine(dataHome, "fonts");
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
                    foreach (var path in all)
                    {
                        var names = FontFace.ReadFaceNames(path);
                        for (int i = 0; i < names.Length; i++)
                            if (names[i].Family != null)
                                list.Add(new FontReference { familyName = names[i].Family, styleName = names[i].Style ?? "", faceIndex = i, filePath = path });
                    }
                    faces = list;
                    return faces;
                }
            }
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
