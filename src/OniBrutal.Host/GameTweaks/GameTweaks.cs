using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host.GameTweaks
{
    /// Optional changes to the game's own code, kept apart from the engine: the engine reproduces
    /// Unity, while these change how Klei's code does something, mostly without changing what it
    /// produces. Each is documented in its class with what it changes and what was measured. Which
    /// are on comes from the Moono Tweaks screen's settings file or the command line
    /// (`--game-tweak`; EffectiveOptions): by default every tweak but those that change behaviour.
    internal static class GameTweaks
    {
        static bool backgroundSave;

        /// OniBrutal.GameTweaks.dll beside the host: the tweaks built against the game's own types,
        /// and the main menu's Moono Tweaks screen (BrutalTweaksMenu).
        public static System.Reflection.Assembly TypedAssembly => typed ??= System.Reflection.Assembly.LoadFrom(System.IO.Path.Combine(AppContext.BaseDirectory, "OniBrutal.GameTweaks.dll"));
        static System.Reflection.Assembly? typed;

        public static void Install(IReadOnlySet<string> enabled)
        {
            bool On(string name) => enabled.Contains(name);
            if (enabled.Count == 0) return;
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
                var patches = TypedAssembly.GetType("OniBrutal.GameTweaks.Patches")!;
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
}
