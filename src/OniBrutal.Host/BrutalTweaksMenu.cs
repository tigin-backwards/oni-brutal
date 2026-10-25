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
        public static void Install(EffectiveOptions effective, PlayerSettings file, string settingsPath)
        {
            // The rows, their sections and copy from the one catalog (GameTweakNames.Rows).
            var rows = GameTweakNames.Rows;
            (bool On, bool Locked, bool Default) State(TweakInfo row) => row.Id switch
            {
                GameTweakNames.SplashRow => (effective.Splash == "moo", effective.SplashSource == OptionSource.CommandLine, EffectiveOptions.DefaultSplash == "moo"),
                GameTweakNames.FrameStatsRow => (effective.FrameStats, effective.FrameStatsSource == OptionSource.CommandLine, false),
                var name => (effective.Tweaks[name].On, effective.Tweaks[name].Source == OptionSource.CommandLine,
                    EffectiveOptions.DefaultOn(name, GameTweakNames.ChangingBehaviour)),
            };
            var states = rows.Select(State).ToArray();
            static string Measured(TweakGain g) => $"Measured on {g.Colony} ({g.Platform}, {g.Measured}).";
            string Tooltip(TweakInfo row)
            {
                var lines = new List<string>();
                if (row.Detail.Length > 0) lines.Add(row.Detail);
                if (row.Gain != null) lines.Add(Measured(row.Gain));
                else if (row.Group.Gain != null) lines.Add($"{row.Group.Title} together: {row.Group.Gain.Effect}. {Measured(row.Group.Gain)}");
                lines.Add("Launch option: " + row.LaunchOption);
                return string.Join("\n", lines);
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
                    if (ids[i] == GameTweakNames.SplashRow) splash = values[i];
                    else if (ids[i] == GameTweakNames.FrameStatsRow) frameStats = values[i];
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
                rows.Select(r => r.Id).ToArray(), rows.Select(r => r.Group.Title).ToArray(), rows.Select(r => r.Group.Gain?.Effect ?? "").ToArray(),
                rows.Select(r => r.Title).ToArray(), rows.Select(r => r.Effect).ToArray(), rows.Select(r => r.Gain?.Effect ?? "").ToArray(),
                rows.Select(Tooltip).ToArray(), rows.Select(r => r.Group == GameTweakNames.ChangesGameplay).ToArray(),
                states.Select(s => s.On).ToArray(), states.Select(s => s.Default).ToArray(), states.Select(s => s.Locked).ToArray(),
                string.Join(" ", locked), settingsPath, save]);
        }
    }
}
