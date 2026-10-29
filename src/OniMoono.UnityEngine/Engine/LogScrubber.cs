using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OniMoono.Engine
{
    /// What Player.log and the console get in place of personal paths, so a log attached to an
    /// issue does not name the player (`--log-paths hidden`, the default). Each rule is anchored
    /// to a value known at start-up, never guessed from free text:
    /// - the profile folder: `%USERPROFILE%` on Windows (either slash direction, any case; also
    ///   its short 8.3 form, as the temp folder names it), `~` elsewhere;
    /// - the Documents folder when it is not under the profile: `%DOCUMENTS%`;
    /// - a SteamID64 (17 digits, 7656119…) as a path segment (cloud_save_files\ID\…): `<steam id>`;
    /// - the user name as a path segment (a profile on another drive, /mnt/c/Users/NAME): `<user>`.
    ///   The bare word is left alone.
    /// A path matches with any run of slashes between its folders (Windows: either kind, as in
    /// "C:/Users\Name" or an escaped "C:\\Users\\Name") and not inside a longer name.
    ///
    /// Every rule needs a slash and one of a few words (the folders' last names, the user name,
    /// 7656119), so most lines cost a few vectorized searches; only a line with both is scanned,
    /// and only at slashes, after them and at drive letters. Hand-written rather than a Regex:
    /// one built from the same values cost ~10 ms of the player's start-up (the first use of the
    /// Regex and SearchValues code), this ~2 ms.
    internal sealed class LogScrubber
    {
        public const string ProfileText = "%USERPROFILE%", HomeText = "~", DocumentsText = "%DOCUMENTS%",
            SteamIdText = "<steam id>", UserText = "<user>";
        const string SteamIdStart = "7656119";
        const int SteamIdLength = 17;

        /// A folder's path by segments; `Rooted` when it starts with a slash.
        sealed record Rule(string[] Segments, bool Rooted, string Replacement);

        readonly bool windows;
        readonly StringComparison comparison;
        readonly Rule[] paths;
        readonly string userName;
        /// A line without any of these can match no rule.
        readonly string[] anchors;
        /// The first characters of the paths that start with no slash (a drive: C:, c:).
        readonly string starts;

        LogScrubber(bool windows, Rule[] paths, string userName, string[] anchors)
        {
            var first = new StringBuilder();
            foreach (var rule in paths)
                if (!rule.Rooted)
                {
                    char c = rule.Segments[0][0];
                    first.Append(c);
                    if (windows) first.Append(char.ToLowerInvariant(c)).Append(char.ToUpperInvariant(c));
                }
            starts = first.ToString();
            this.windows = windows;
            comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            this.paths = paths;
            this.userName = userName;
            this.anchors = anchors;
        }

        /// This machine's values: the profile and Documents folders .NET reports, the user name.
        public static LogScrubber ForThisMachine()
        {
            bool windows = OperatingSystem.IsWindows();
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var aliases = new List<string>();
            if (windows)
            {
                // The temp folder can name the profile by its short form (C:\Users\ADMINI~1).
                const string tempTail = @"\AppData\Local\Temp\";
                string temp = Path.GetTempPath();
                if (temp.EndsWith(tempTail, StringComparison.OrdinalIgnoreCase)) aliases.Add(temp[..^tempTail.Length]);
            }
            return Create(windows, profile, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Environment.UserName, aliases);
        }

        /// The rules for these values; `windows` makes them case-insensitive, with either slash.
        public static LogScrubber Create(bool windows, string profile, string documents, string userName, IEnumerable<string> profileAliases = null)
        {
            var comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var rules = new List<Rule>();
            var anchors = new List<string> { SteamIdStart };
            void Add(string path, string replacement)
            {
                var segments = Segments(path, windows);
                if (segments.Length == 0) return;
                foreach (var rule in rules)
                    if (SameSegments(rule.Segments, segments, comparison)) return;
                rules.Add(new Rule(segments, path[0] is '/' or '\\', replacement));
                anchors.Add(segments[^1]);
            }
            Add(profile, windows ? ProfileText : HomeText);
            foreach (var alias in profileAliases ?? []) Add(alias, windows ? ProfileText : HomeText);
            // Documents on its own only when no profile rule covers it; a drive root is no anchor.
            var documentsSegments = Segments(documents, windows);
            bool covered = false;
            foreach (var rule in rules) covered |= IsUnder(documentsSegments, rule.Segments, comparison);
            if (documentsSegments.Length >= 2 && !covered) Add(documents, DocumentsText);
            // Longest first: at one position, the more specific path wins.
            rules.Sort((a, b) => b.Segments.Length.CompareTo(a.Segments.Length));
            // Short names (root, a, me) would hit unrelated segments.
            if (string.IsNullOrEmpty(userName) || userName.Length < 3 || userName.IndexOfAny(['/', '\\']) >= 0) userName = null;
            else anchors.Add(userName);
            return new LogScrubber(windows, rules.ToArray(), userName, anchors.ToArray());
        }

        static string[] Segments(string path, bool windows)
            => string.IsNullOrEmpty(path) ? [] : path.Split(windows ? ['\\', '/'] : ['/'], StringSplitOptions.RemoveEmptyEntries);

        static bool SameSegments(string[] a, string[] b, StringComparison comparison)
            => a.Length == b.Length && IsUnder(a, b, comparison);

        static bool IsUnder(string[] path, string[] folder, StringComparison comparison)
        {
            if (path.Length < folder.Length) return false;
            for (int i = 0; i < folder.Length; i++)
                if (!string.Equals(path[i], folder[i], comparison)) return false;
            return true;
        }

        bool IsSeparator(char c) => c == '/' || windows && c == '\\';

        static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        /// A character a file or folder name continues with.
        static bool IsNameChar(char c) => IsWordChar(c) || c is '.' or '-' or '~' or '$';

        /// A name ends at `at`: the end, a slash, or a character no name continues with.
        bool EndsName(string text, int at) => at == text.Length || IsSeparator(text[at]) || !IsNameChar(text[at]);

        bool Matches(string text, int at, string word)
        {
            if (at + word.Length > text.Length) return false;
            // The first character before the whole word: most candidates fail there.
            char a = text[at], b = word[0];
            if (a != b && !(windows && char.ToUpperInvariant(a) == char.ToUpperInvariant(b))) return false;
            return text.AsSpan(at, word.Length).Equals(word, comparison);
        }

        int SkipSeparators(string text, int at)
        {
            while (at < text.Length && IsSeparator(text[at])) at++;
            return at;
        }

        /// Where the rule's path ends if it starts at `at`, else -1.
        int MatchPath(string text, int at, Rule rule)
        {
            // Not the end of a longer name: "x/home/me", "CD:\" (Windows' drive or share names
            // stand after anything but a word's character).
            if (at > 0 && (windows ? IsWordChar(text[at - 1]) : IsNameChar(text[at - 1]))) return -1;
            int i = at;
            if (rule.Rooted)
            {
                if (!IsSeparator(text[i])) return -1;
                i = SkipSeparators(text, i);
            }
            for (int s = 0; s < rule.Segments.Length; s++)
            {
                if (s > 0)
                {
                    int next = SkipSeparators(text, i);
                    if (next == i) return -1;
                    i = next;
                }
                if (!Matches(text, i, rule.Segments[s])) return -1;
                i += rule.Segments[s].Length;
            }
            return EndsName(text, i) ? i : -1;
        }

        /// Where a one-segment rule (a Steam ID, the user name) ends if it starts at `at`, else -1.
        int MatchSegment(string text, int at, out string replacement)
        {
            replacement = null;
            if (at == 0 || !IsSeparator(text[at - 1])) return -1;
            if (Matches(text, at, SteamIdStart) && at + SteamIdLength <= text.Length)
            {
                int end = at + SteamIdStart.Length;
                while (end < at + SteamIdLength && char.IsAsciiDigit(text[end])) end++;
                if (end == at + SteamIdLength && EndsName(text, end))
                {
                    replacement = SteamIdText;
                    return end;
                }
            }
            if (userName != null && Matches(text, at, userName) && EndsName(text, at + userName.Length))
            {
                replacement = UserText;
                return at + userName.Length;
            }
            return -1;
        }

        /// The text with the rules applied; the same instance when nothing matched.
        public string Scrub(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var span = text.AsSpan();
            if (span.IndexOfAny('/', '\\') < 0) return text;
            bool anchored = false;
            foreach (var anchor in anchors)
                if (span.IndexOf(anchor, comparison) >= 0) { anchored = true; break; }
            if (!anchored) return text;

            StringBuilder result = null;
            int copied = 0;
            for (int at = 0; at < text.Length;)
            {
                // Each rule starts at a slash, after one, or at a drive's letter.
                char c = text[at];
                if (!IsSeparator(c) && starts.IndexOf(c) < 0 && (at == 0 || !IsSeparator(text[at - 1])))
                {
                    at++;
                    continue;
                }
                int end = -1;
                string replacement = null;
                foreach (var rule in paths)
                {
                    end = MatchPath(text, at, rule);
                    if (end >= 0) { replacement = rule.Replacement; break; }
                }
                if (end < 0) end = MatchSegment(text, at, out replacement);
                if (end < 0)
                {
                    at++;
                    continue;
                }
                result ??= new StringBuilder(text.Length);
                result.Append(text, copied, at - copied).Append(replacement);
                at = copied = end;
            }
            if (result == null) return text;
            return result.Append(text, copied, text.Length - copied).ToString();
        }
    }
}
