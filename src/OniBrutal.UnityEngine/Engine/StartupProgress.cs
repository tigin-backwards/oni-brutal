using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace OniBrutal.Engine
{
    /// How far start-up is to the main menu, for the loading splash's bar (any thread reads
    /// Value). Start-up passes checkpoints: the engine's own (window, first scene, first frame,
    /// each content bundle, the asset unload, the menu's scene and first frame) and the game's,
    /// recognized from its log lines (Steam ready, Global's start, the mod system, localization).
    /// Each launch's checkpoint times are kept in the player's cache folder; the next launch
    /// expects the same, so between two checkpoints the bar moves on at the last launch's pace
    /// (stopping short of the next one until it is reached), and where the engine knows how
    /// far it is (the first scene's objects loaded) it moves by that. The first launch uses times
    /// measured on a typical machine. It only ever moves forward.
    internal static class StartupProgress
    {
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static readonly object gate = new();
        static readonly List<(string Name, double Ms)> reached = new();
        static List<(string Name, double Ms)> expected;
        static float shown;
        static double fraction = -1;
        public static bool Done { get; private set; }

        /// The game's log lines that mark its start-up (prefixes), as checkpoint names.
        static readonly (string Prefix, string Name)[] gameLines =
        [
            ("DistributionPlatform initialized.", "platform"),
            ("Initializing at ", "global"),
            ("Using builtin mod system.", "mods"),
            ("Localization.Initialize!", "localization"),
        ];

        /// A start-up on a typical machine (Linux, every DLC; times in ms), for the first launch.
        static readonly (string, double)[] typical =
        [
            ("window", 1700), ("scene0", 4300), ("frame0", 5500), ("platform", 5600),
            ("bundle1", 13000), ("bundle2", 13100), ("bundle3", 13200), ("bundle4", 13300), ("bundle5", 13400), ("bundle6", 13500),
            ("global", 13600), ("mods", 14300), ("localization", 14400), ("unload", 21000), ("scene1", 21500), ("menu", 23000),
        ];

        static string FilePath => Path.Combine(EngineConfig.TemporaryCachePath, "OniBrutal", "startup-checkpoints.json");

        static int bundles;

        public static void Checkpoint(string name)
        {
            if (Done) return;
            lock (gate)
            {
                if (name == "bundle") name = "bundle" + ++bundles;
                reached.Add((name, clock.Elapsed.TotalMilliseconds));
                fraction = -1;
                if (name != "menu") return;
                Done = true;
            }
            StartupStats.Mark("start-up progress: at the menu");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var list = new List<object>();
                foreach (var (n, ms) in reached) list.Add(new { name = n, ms });
                File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }

        /// The end of a frame: the first frame, and the menu's (the frame that loaded the second
        /// scene).
        public static void FrameEnded(int frame)
        {
            if (Done) return;
            if (frame == 0) Checkpoint("frame0");
            bool menu;
            lock (gate) menu = reached.Exists(r => r.Name == "scene1");
            if (menu) Checkpoint("menu");
        }

        /// A main-thread log line: a checkpoint when it is one of the game's.
        public static void LogLine(string message)
        {
            if (Done || message == null) return;
            foreach (var (prefix, name) in gameLines)
                if (message.StartsWith(prefix, StringComparison.Ordinal))
                {
                    Checkpoint(name);
                    return;
                }
        }

        /// The part of the current phase done, when the engine knows it (0..1); -1 otherwise.
        public static void PhaseFraction(double value) => fraction = value;

        static List<(string, double)> Expected()
        {
            if (expected != null) return expected;
            var list = new List<(string, double)>();
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
                foreach (var e in doc.RootElement.EnumerateArray()) list.Add((e.GetProperty("name"u8).GetString()!, e.GetProperty("ms"u8).GetDouble()));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                list.Clear();
            }
            if (list.Count == 0 || list[^1].Item1 != "menu") list = [.. typical];
            return expected = list;
        }

        /// Start-up done so far, 0..1 (any thread).
        public static float Value
        {
            get
            {
                if (Done) return 1f;
                lock (gate)
                {
                    var exp = Expected();
                    double total = exp[^1].Item2;
                    double now = clock.Elapsed.TotalMilliseconds;
                    // The last checkpoint reached that the last launch also passed, and the next one.
                    int at = -1;
                    double reachedAt = 0;
                    for (int r = reached.Count - 1; r >= 0 && at < 0; r--)
                    {
                        int i = exp.FindIndex(e => e.Item1 == reached[r].Name);
                        if (i >= 0) { at = i; reachedAt = reached[r].Ms; }
                    }
                    double from = at >= 0 ? exp[at].Item2 : 0;
                    double to = at + 1 < exp.Count ? exp[at + 1].Item2 : total;
                    double span = Math.Max(1, to - from);
                    double part = fraction >= 0 ? fraction : Math.Min(0.95, (now - reachedAt) / span);
                    float value = (float)Math.Clamp((from + part * span) / total, 0, 0.99);
                    if (value > shown) shown = value;
                    return shown;
                }
            }
        }
    }
}
