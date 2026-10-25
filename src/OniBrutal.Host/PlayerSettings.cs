#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OniBrutal.Host
{
    /// Where an option's effective value came from.
    internal enum OptionSource { Default, SettingsFile, CommandLine }

    /// The player's own settings file, written by the main menu's Brutal Tweaks screen:
    /// `<persistent data path>/oni-brutal/settings.json`, beside the game's own data in the player's
    /// per-user folder (a test run's run home has its own, so it never reads the user's; updating
    /// the player never touches it). It records only the choices that differ from the built-in
    /// defaults, so a default that changes in an update (a new tweak among them) reaches every user
    /// who has not chosen otherwise:
    ///
    ///     { "gameTweaks": { "slow-reachability": true, "fast-jobs": false }, "splash": "moo", "frameStats": true }
    ///
    /// Tweaks no longer known (one removed in an update) are ignored and kept.
    internal sealed class PlayerSettings
    {
        /// The tweaks chosen on or off, by name; a tweak not named has its default.
        public readonly SortedDictionary<string, bool> GameTweaks = new(StringComparer.Ordinal);
        /// "moo" or "none"; null when not chosen.
        public string? Splash;
        public bool? FrameStats;

        public static string PathIn(string persistentDataPath) => Path.Combine(persistentDataPath, "oni-brutal", "settings.json");

        /// The file at `path`: empty when there is none. A file that cannot be read or parsed counts
        /// as empty too, with the reason in `problem` (it is not overwritten until the screen saves).
        public static PlayerSettings Read(string path, out string? problem)
        {
            problem = null;
            var settings = new PlayerSettings();
            if (!File.Exists(path)) return settings;
            try
            {
                return Parse(File.ReadAllText(path));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
            {
                problem = e.Message;
                return settings;
            }
        }

        public static PlayerSettings Parse(string json)
        {
            var settings = new PlayerSettings();
            var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
                       ?? throw new FormatException("the settings file is not a JSON object");
            if (root["gameTweaks"] is JsonObject tweaks)
                foreach (var (name, value) in tweaks)
                    settings.GameTweaks[name] = value?.GetValue<bool>() ?? throw new FormatException($"gameTweaks.{name} is null");
            if (root["splash"] is JsonNode splash)
            {
                var value = splash.GetValue<string>();
                settings.Splash = IsSplash(value) ? value : throw new FormatException($"splash \"{value}\" is not one of: moo, none");
            }
            if (root["frameStats"] is JsonNode frameStats) settings.FrameStats = frameStats.GetValue<bool>();
            return settings;
        }

        /// The file's text (indented); `indented: false` gives one line, for the log.
        public string ToJson(bool indented = true)
        {
            var root = new JsonObject();
            if (GameTweaks.Count > 0)
            {
                var tweaks = new JsonObject();
                foreach (var (name, on) in GameTweaks) tweaks[name] = on;
                root["gameTweaks"] = tweaks;
            }
            if (Splash != null) root["splash"] = Splash;
            if (FrameStats != null) root["frameStats"] = FrameStats;
            return indented ? root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n" : root.ToJsonString();
        }

        /// Writes the file whole: to a temporary file beside it, then moved over it, so a crash
        /// mid-write leaves the previous file.
        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, ToJson(), new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }

        public static bool IsSplash(string value) => value is "moo" or "none";
    }

    /// The options the Brutal Tweaks screen sets, as this run has them: built-in defaults, then the
    /// settings file, then the command line. A command line naming any game tweak
    /// (`--game-tweak NAME|-NAME|all|exact|none`, repeatable) sets the whole set of tweaks for the
    /// run, as before the settings file existed; `--splash` and `--frame-stats` set theirs.
    internal sealed class EffectiveOptions
    {
        public readonly IReadOnlyList<string> AllTweaks;
        public readonly Dictionary<string, (bool On, OptionSource Source)> Tweaks = new(StringComparer.Ordinal);
        /// The splash without a settings file or command line saying otherwise: the Gassy Moos.
        public const string DefaultSplash = "moo";
        public string Splash = DefaultSplash;
        public OptionSource SplashSource;
        public bool FrameStats;
        public OptionSource FrameStatsSource;
        /// The command line's `--game-tweak` values, in order (empty when it names none).
        public readonly IReadOnlyList<string> CommandLineTweaks;
        /// Names in the settings file that are not tweaks of this player.
        public readonly List<string> UnknownTweaks = new();

        EffectiveOptions(IReadOnlyList<string> all, IReadOnlyList<string> commandLineTweaks)
        {
            AllTweaks = all;
            CommandLineTweaks = commandLineTweaks;
        }

        /// A tweak's built-in default: on unless it changes what the game does (`exact`).
        public static bool DefaultOn(string name, IReadOnlyCollection<string> changing) => !changing.Contains(name);

        /// A tweak as the command line's `--game-tweak` values have it: on when named, or by `all`,
        /// or by `exact` unless it changes behaviour; `-NAME` leaves it out of `all` or `exact`;
        /// `none` turns on nothing (alone, it asks for the game without tweaks).
        public static bool OnByCommandLine(IReadOnlyCollection<string> values, string name, IReadOnlyCollection<string> changing)
            => !values.Contains("-" + name) && (values.Contains(name) || values.Contains("all") || values.Contains("exact") && !changing.Contains(name));

        public static EffectiveOptions Resolve(IReadOnlyList<string> allTweaks, IReadOnlyCollection<string> changing,
            IReadOnlyList<string> commandLineTweaks, string? commandLineSplash, bool commandLineFrameStats, PlayerSettings file)
        {
            var options = new EffectiveOptions(allTweaks, commandLineTweaks);
            foreach (var name in allTweaks)
            {
                if (commandLineTweaks.Count > 0) options.Tweaks[name] = (OnByCommandLine(commandLineTweaks, name, changing), OptionSource.CommandLine);
                else if (file.GameTweaks.TryGetValue(name, out bool on)) options.Tweaks[name] = (on, OptionSource.SettingsFile);
                else options.Tweaks[name] = (DefaultOn(name, changing), OptionSource.Default);
            }
            foreach (var name in file.GameTweaks.Keys)
                if (!allTweaks.Contains(name)) options.UnknownTweaks.Add(name);
            if (commandLineSplash != null) (options.Splash, options.SplashSource) = (commandLineSplash, OptionSource.CommandLine);
            else if (file.Splash != null) (options.Splash, options.SplashSource) = (file.Splash, OptionSource.SettingsFile);
            if (commandLineFrameStats) (options.FrameStats, options.FrameStatsSource) = (true, OptionSource.CommandLine);
            else if (file.FrameStats != null) (options.FrameStats, options.FrameStatsSource) = (file.FrameStats.Value, OptionSource.SettingsFile);
            return options;
        }

        public IEnumerable<string> EnabledTweaks => AllTweaks.Where(n => Tweaks[n].On);

        /// The file with the screen's choices applied: each choice that equals its default is
        /// dropped from the file, any other recorded; options not chosen (those the command line
        /// set, shown but not changeable) keep what the file had.
        public static PlayerSettings Apply(PlayerSettings file, IReadOnlyCollection<string> changing,
            IReadOnlyDictionary<string, bool> tweaks, bool? splashMoo, bool? frameStats)
        {
            var result = new PlayerSettings { Splash = file.Splash, FrameStats = file.FrameStats };
            foreach (var (name, on) in file.GameTweaks) result.GameTweaks[name] = on;
            foreach (var (name, on) in tweaks)
            {
                if (on == DefaultOn(name, changing)) result.GameTweaks.Remove(name);
                else result.GameTweaks[name] = on;
            }
            if (splashMoo != null) result.Splash = (splashMoo.Value ? "moo" : "none") is var splash && splash == DefaultSplash ? null : splash;
            if (frameStats != null) result.FrameStats = frameStats.Value ? true : null;
            return result;
        }

        static string From(OptionSource source) => source switch
        {
            OptionSource.CommandLine => "the command line",
            OptionSource.SettingsFile => "the settings file",
            _ => "default",
        };

        /// For the log: each option with where it came from.
        public IEnumerable<string> Describe()
        {
            if (CommandLineTweaks.Count > 0)
            {
                var on = EnabledTweaks.ToArray();
                yield return $"game tweaks from the command line (--game-tweak {string.Join(" --game-tweak ", CommandLineTweaks)}): "
                             + (on.Length == 0 ? "none" : string.Join(", ", on));
            }
            else
            {
                foreach (var group in AllTweaks.GroupBy(n => Tweaks[n]).OrderByDescending(g => g.Key.On).ThenBy(g => g.Key.Source))
                    yield return $"game tweaks {(group.Key.On ? "on" : "off")} ({From(group.Key.Source)}): {string.Join(", ", group)}";
            }
            yield return $"splash {Splash} ({From(SplashSource)})";
            yield return $"frame stats {(FrameStats ? "on" : "off")} ({From(FrameStatsSource)})";
        }
    }
}
