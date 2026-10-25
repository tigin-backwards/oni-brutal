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
    /// are on comes from the Brutal Tweaks screen's settings file or the command line
    /// (`--game-tweak`; EffectiveOptions): by default every tweak but those that change behaviour.
    internal static class GameTweaks
    {
        static bool backgroundSave;

        /// OniBrutal.GameTweaks.dll beside the host: the tweaks built against the game's own types,
        /// and the main menu's Brutal Tweaks screen (BrutalTweaksMenu).
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

    /// The tweaks' names, for option parsing: options are read before the game's assemblies
    /// (Harmony among them) can be resolved, so this class references none of them.
    internal static class GameTweakNames
    {
        /// The tweaks in src/OniBrutal.GameTweaks (named here: options are read before that assembly is loaded).
        public static readonly string[] Typed = ["off-screen-conveyors", "fast-save-type-checks", "fast-anim-cleanup", "fast-sublimation", "fast-anim-events", "fast-symbol-overrides", "fast-jobs", "fast-offset-tables", "slow-reachability", "off-world-fallers", "fast-partitioner-cleanup", "fast-anim-batches", "fast-overlays", "fast-conveyor-paths"];

        public static readonly string[] All = [FastSaveCompression.Name, BackgroundSave.Name, NoSaveGc.Name, FastSerialization.Name, FastParticleCollision.Name, .. Typed];

        /// The tweaks that change what the game does (each documented in its class); `exact` turns
        /// on every tweak but these.
        public static readonly string[] ChangingBehaviour = ["off-screen-conveyors", "slow-reachability", "off-world-fallers"];

        /// A `--game-tweak` value: a tweak's name, `-NAME` (left out of `all` or `exact`, for
        /// measuring one against the rest), `all`, `exact` (all but ChangingBehaviour) or `none`
        /// (no tweak unless one is named, whatever the settings file says: test runs).
        public static bool IsKnown(string name) => name is "all" or "exact" or "none" || Array.IndexOf(All, name.StartsWith('-') ? name.Substring(1) : name) >= 0;

        /// What the Brutal Tweaks screen shows for each tweak: a title and one plain line on what
        /// it does (a tweak missing here shows its name).
        public static readonly Dictionary<string, (string Title, string Description)> Descriptions = new(StringComparer.Ordinal)
        {
            [FastSaveCompression.Name] = ("Faster save compression", "Saves and loads with .NET's zlib: seconds shorter; the files stay compatible, about 14% larger."),
            [BackgroundSave.Name] = ("Background saving", "Compresses and writes the save file on another thread, so an autosave pauses the game for less time."),
            [NoSaveGc.Name] = ("No forced cleanup after saving", "Skips the full memory collection the game forces after each save, a pause of its own in big colonies."),
            [FastSerialization.Name] = ("Faster save serialization", "Writes the colony's numbers without wrapping each in an object: shorter autosaves, the same file."),
            [FastParticleCollision.Name] = ("Faster radbolt collisions", "A radbolt looks for other radbolts only where one can be, not through every stored item nearby."),
            ["off-screen-conveyors"] = ("Off-screen conveyor items", "Items riding conveyor rails off screen are placed only when they stop or come into view; until then their position lags."),
            ["fast-save-type-checks"] = ("Faster save type checks", "Remembers what saving asks about each kind of component instead of asking again for every object."),
            ["fast-anim-cleanup"] = ("Faster animation cleanup", "Sweeps the animation grid for removed animations only where one was actually removed."),
            ["fast-sublimation"] = ("Faster sublimation", "Skips sublimating items (polluted dirt, bleach stone...) that sit in full gas cells and cannot change."),
            ["fast-anim-events"] = ("Faster animation events", "Keeps a compact copy of what each playing animation's per-frame event check reads."),
            ["fast-symbol-overrides"] = ("Fewer animation uploads", "Does not upload a Duplicant's or building's symbol swaps again when nothing in them changed."),
            ["fast-jobs"] = ("Faster worker threads", "Wakes only as many worker threads as a job has work for, instead of all of them every time."),
            ["fast-offset-tables"] = ("Faster fetch checks", "Recomputes where an item can be picked up from only when something around it changed."),
            ["slow-reachability"] = ("Slower reachability checks", "Rechecks which errands Duplicants can reach every 4 s instead of every second: a door or new path is noticed up to 3 s later."),
            ["off-world-fallers"] = ("Land items falling out of a world", "Items falling out through the bottom of a world (a rocket's chute) are landed where they are instead of falling forever."),
            ["fast-partitioner-cleanup"] = ("Faster spatial index cleanup", "The once-a-second cleanup of the game's spatial index looks only at entries actually removed."),
            ["fast-anim-batches"] = ("Parallel animation uploads", "Prepares the animations' data for the graphics card on worker threads instead of one by one."),
            ["fast-overlays"] = ("Faster overlays", "The overlays (temperature most of all) skip rereading what cannot have changed since the last frame."),
            ["fast-conveyor-paths"] = ("Faster conveyor rebuilds", "Finds the conveyor rails' routes from an index instead of searching every route again: no hitch when rails change."),
        };
    }
}
