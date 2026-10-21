using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using OniBrutal.Engine;

namespace UnityEngine
{
    /// Values kept between runs in the player's prefs file, as Unity's Linux player keeps them
    /// (measured in the original player): `prefs` in the persistent data folder, an XML file of
    /// `<pref name="…" type="int|float|string">` entries, strings as base64 UTF-8, floats with six
    /// significant digits. The file also holds the player's own entries (screen settings and the
    /// like), which are kept as read. Setting a key with another type replaces it; reading a key as
    /// another type gives the default. On Windows, where Unity keeps them in the registry, the same
    /// file is used.
    public partial class PlayerPrefs
    {
        enum Kind { Int, Float, String }

        static readonly object gate = new();
        static Dictionary<string, (Kind Kind, string Text)> entries;
        // Entry order: as read from the file, new keys after.
        static List<string> order;
        static bool dirty;

        static string FilePath => Path.Combine(Application.persistentDataPath, "prefs");

        static Dictionary<string, (Kind Kind, string Text)> Entries()
        {
            if (entries != null) return entries;
            entries = new Dictionary<string, (Kind, string)>(StringComparer.Ordinal);
            order = new List<string>();
            var path = FilePath;
            if (!File.Exists(path)) return entries;
            try
            {
                var doc = new XmlDocument();
                doc.Load(path);
                if (doc.DocumentElement == null) return entries;
                foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                {
                    if (node is not XmlElement { Name: "pref" } pref) continue;
                    var name = pref.GetAttribute("name");
                    Kind? kind = pref.GetAttribute("type") switch { "int" => Kind.Int, "float" => Kind.Float, "string" => Kind.String, _ => null };
                    if (kind == null) continue;
                    if (!entries.ContainsKey(name)) order.Add(name);
                    entries[name] = (kind.Value, pref.InnerText);
                }
            }
            catch (XmlException e)
            {
                EngineLog.Engine($"PlayerPrefs: {path} could not be read ({e.Message}); starting empty");
            }
            return entries;
        }

        static void Set(string key, Kind kind, string text)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (gate)
            {
                var e = Entries();
                if (!e.ContainsKey(key)) order.Add(key);
                e[key] = (kind, text);
                dirty = true;
            }
        }

        static string Get(string key, Kind kind)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (gate)
                return Entries().TryGetValue(key, out var entry) && entry.Kind == kind ? entry.Text : null;
        }

        public static void SetInt(string key, int value) => Set(key, Kind.Int, value.ToString(CultureInfo.InvariantCulture));

        public static int GetInt(string key) => GetInt(key, 0);

        public static int GetInt(string key, int defaultValue)
            => Get(key, Kind.Int) is { } text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : defaultValue;

        /// Six significant digits, as C's %g writes them (1.5, 0.333333, 1e+07).
        public static void SetFloat(string key, float value) => Set(key, Kind.Float, value.ToString("G6", CultureInfo.InvariantCulture).Replace("E", "e"));

        public static float GetFloat(string key) => GetFloat(key, 0f);

        public static float GetFloat(string key, float defaultValue)
            => Get(key, Kind.Float) is { } text && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : defaultValue;

        public static void SetString(string key, string value) => Set(key, Kind.String, Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? "")));

        public static string GetString(string key) => GetString(key, "");

        public static string GetString(string key, string defaultValue)
        {
            if (Get(key, Kind.String) is not { } text) return defaultValue;
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(text));
            }
            catch (FormatException)
            {
                return defaultValue;
            }
        }

        public static bool HasKey(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (gate) return Entries().ContainsKey(key);
        }

        public static void DeleteKey(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (gate)
                if (Entries().Remove(key))
                {
                    order.Remove(key);
                    dirty = true;
                }
        }

        public static void DeleteAll()
        {
            lock (gate)
            {
                Entries().Clear();
                order.Clear();
                dirty = true;
            }
        }

        /// Writes the file (Unity also saves on quit).
        public static void Save()
        {
            lock (gate)
            {
                var e = Entries();
                var text = new StringBuilder("<unity_prefs version_major=\"1\" version_minor=\"1\">\n");
                foreach (var name in order)
                {
                    var (kind, value) = e[name];
                    var type = kind switch { Kind.Int => "int", Kind.Float => "float", _ => "string" };
                    text.Append("\t<pref name=\"").Append(SecurityElementEscape(name)).Append("\" type=\"").Append(type).Append("\">")
                        .Append(SecurityElementEscape(value)).Append("</pref>\n");
                }
                text.Append("</unity_prefs>");
                var path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
                dirty = false;
            }
        }

        static string SecurityElementEscape(string s) => System.Security.SecurityElement.Escape(s) ?? "";

        /// Forgets what was read; the file is read again at the next access (tests, a changed
        /// persistent data folder).
        internal static void Reload()
        {
            lock (gate)
            {
                entries = null;
                order = null;
                dirty = false;
            }
        }

        /// The player's quit: unsaved changes are written, as Unity writes them.
        internal static void SaveOnQuit()
        {
            lock (gate)
            {
                if (!dirty) return;
            }
            try
            {
                Save();
            }
            catch (IOException e)
            {
                EngineLog.Engine($"PlayerPrefs: not saved at quit ({e.Message})");
            }
        }
    }
}
