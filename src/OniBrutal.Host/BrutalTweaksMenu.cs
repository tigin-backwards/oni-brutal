using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OniBrutal.Engine;
using OniBrutal.Host.GameTweaks;

namespace OniBrutal.Host
{
    /// The main menu's "Moono Tweaks" button and screen (OniBrutal.GameTweaks.BrutalTweaksMenu,
    /// built from the game's own UI): the options EffectiveOptions resolves, each with a toggle,
    /// saved to the settings file (PlayerSettings) and applied by restarting the game. This side
    /// gives the screen its rows and saves what it chose; options the command line set are shown
    /// but not changeable.
    internal static class BrutalTweaksMenu
    {
        public const string SplashRow = "splash", FrameStatsRow = "frame-stats";

        public static void Install(EffectiveOptions effective, PlayerSettings file, string settingsPath)
        {
            var rows = new List<(string Id, string Title, string Description, bool Changes, bool On, bool Locked)>
            {
                (SplashRow, "Loading splash: Gassy Moos", "Gassy Moos rain over the window while the game starts, with a bar showing how far it is.",
                    false, effective.Splash == "moo", effective.SplashSource == OptionSource.CommandLine),
                (FrameStatsRow, "Frame stats in the log", "Every 60 frames, writes where the frame time went to Player.log (for reporting slowness).",
                    false, effective.FrameStats, effective.FrameStatsSource == OptionSource.CommandLine),
            };
            // The tweaks that keep the game's results first, then those that change gameplay.
            foreach (var name in GameTweakNames.All.OrderBy(n => Array.IndexOf(GameTweakNames.ChangingBehaviour, n) >= 0))
            {
                var (title, description) = GameTweakNames.Descriptions.TryGetValue(name, out var d) ? d : (name, "");
                var (on, source) = effective.Tweaks[name];
                rows.Add((name, title, description, Array.IndexOf(GameTweakNames.ChangingBehaviour, name) >= 0, on, source == OptionSource.CommandLine));
            }
            var locked = new List<string>();
            foreach (var value in effective.CommandLineTweaks) locked.Add("--game-tweak " + value);
            if (effective.SplashSource == OptionSource.CommandLine) locked.Add("--splash " + effective.Splash);
            if (effective.FrameStatsSource == OptionSource.CommandLine) locked.Add("--frame-stats");

            var saved = file;
            Func<string[], bool[], string?> save = (ids, values) =>
            {
                var tweaks = new Dictionary<string, bool>(StringComparer.Ordinal);
                bool? splash = null, frameStats = null;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i] == SplashRow) splash = values[i];
                    else if (ids[i] == FrameStatsRow) frameStats = values[i];
                    else tweaks[ids[i]] = values[i];
                }
                var next = EffectiveOptions.Apply(saved, GameTweakNames.ChangingBehaviour, tweaks, splash, frameStats);
                try
                {
                    next.Write(settingsPath);
                }
                catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
                {
                    EngineLog.Engine($"settings: could not write {settingsPath}: {e.Message}");
                    return e.Message;
                }
                saved = next;
                EngineLog.Engine($"settings: saved {settingsPath}: {next.ToJson(indented: false)}");
                return null;
            };

            var install = GameTweaks.GameTweaks.TypedAssembly.GetType("OniBrutal.GameTweaks.BrutalTweaksMenu", throwOnError: true)!.GetMethod("Install")!;
            install.Invoke(null, [new Harmony("OniBrutal.Host.BrutalTweaksMenu"),
                rows.Select(r => r.Id).ToArray(), rows.Select(r => r.Title).ToArray(), rows.Select(r => r.Description).ToArray(),
                rows.Select(r => r.Changes).ToArray(), rows.Select(r => r.On).ToArray(), rows.Select(r => r.Locked).ToArray(),
                string.Join(" ", locked), settingsPath, save]);
        }
    }
}
