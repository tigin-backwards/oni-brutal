using System;
using System.Collections.Generic;
using System.Linq;
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
        static bool backgroundSave;

        public static void Install(IReadOnlySet<string> requested)
        {
            bool On(string name) => requested.Contains(name) || requested.Contains("all");
            if (requested.Count == 0) return;
            var harmony = new Harmony("OniBrutal.Host.GameTweaks");
            backgroundSave = On(BackgroundSave.Name);
            // Both replace SaveLoader.CompressContents: with background-save on, it does the
            // compressing, with .NET's zlib when fast-save-compression is on too.
            if (On(FastSaveCompression.Name)) Report(FastSaveCompression.Name, () => FastSaveCompression.Install(harmony, compress: !backgroundSave));
            if (backgroundSave) Report(BackgroundSave.Name, () => BackgroundSave.Install(harmony, fastCompression: On(FastSaveCompression.Name)));
            if (On(NoSaveGc.Name)) Report(NoSaveGc.Name, () => NoSaveGc.Install(harmony));
            if (On(FastSerialization.Name)) Report(FastSerialization.Name, () => FastSerialization.Install(harmony));
            if (On(FastParticleCollision.Name)) Report(FastParticleCollision.Name, () => FastParticleCollision.Install(harmony));
            // The tweaks built against the game's own types (src/OniBrutal.GameTweaks).
            var typed = GameTweakNames.Typed.Where(On).ToArray();
            if (typed.Length > 0)
            {
                var patches = System.Reflection.Assembly.LoadFrom(System.IO.Path.Combine(AppContext.BaseDirectory, "OniBrutal.GameTweaks.dll")).GetType("OniBrutal.GameTweaks.Patches")!;
                var install = patches.GetMethod("Install")!;
                foreach (var name in typed)
                    Report(name, () =>
                    {
                        if (!(bool)install.Invoke(null, [harmony, name])!) throw new InvalidOperationException($"OniBrutal.GameTweaks has no tweak {name}");
                    });
            }
        }

        static void Report(string name, Action install)
        {
            install();
            EngineLog.Engine($"game tweak {name}");
        }

        /// The end of the run: saves still being written are finished first.
        public static void Shutdown()
        {
            if (backgroundSave) BackgroundSave.WaitAll();
        }
    }

    /// The tweaks' names, for option parsing: options are read before the game's assemblies
    /// (Harmony among them) can be resolved, so this class references none of them.
    internal static class GameTweakNames
    {
        /// The tweaks in src/OniBrutal.GameTweaks (named here: that assembly is loaded only when asked for).
        public static readonly string[] Typed = ["off-screen-conveyors", "fast-save-type-checks", "fast-anim-cleanup"];

        public static readonly string[] All = [FastSaveCompression.Name, BackgroundSave.Name, NoSaveGc.Name, FastSerialization.Name, FastParticleCollision.Name, .. Typed];

        public static bool IsKnown(string name) => name == "all" || Array.IndexOf(All, name) >= 0;
    }
}
