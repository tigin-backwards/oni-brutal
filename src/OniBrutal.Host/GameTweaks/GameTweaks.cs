using System;
using System.Collections.Generic;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host.GameTweaks
{
    /// Optional changes to the game's own code (`--game-tweak NAME`, repeatable, or `all`), kept
    /// apart from the engine: the engine reproduces Unity, while these change how Klei's code does
    /// something, without changing what it produces. Each is off unless asked for, documented in
    /// its class with what it changes and what was measured.
    internal static class GameTweaks
    {
        static readonly Dictionary<string, Action<Harmony>> available = new(StringComparer.Ordinal)
        {
            [FastSaveCompression.Name] = FastSaveCompression.Install,
        };

        public static void Install(IReadOnlySet<string> requested)
        {
            if (requested.Count == 0) return;
            var harmony = new Harmony("OniBrutal.Host.GameTweaks");
            foreach (var (name, install) in available)
            {
                if (!requested.Contains(name) && !requested.Contains("all")) continue;
                install(harmony);
                EngineLog.Engine($"game tweak {name}");
            }
        }
    }

    /// The tweaks' names, for option parsing: options are read before the game's assemblies
    /// (Harmony among them) can be resolved, so this class references none of them.
    internal static class GameTweakNames
    {
        public static readonly string[] All = [FastSaveCompression.Name];

        public static bool IsKnown(string name) => name == "all" || Array.IndexOf(All, name) >= 0;
    }
}
