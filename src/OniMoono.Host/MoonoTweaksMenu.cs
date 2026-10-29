using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OniMoono.Engine;
using OniMoono.Host.GameTweaks;

namespace OniMoono.Host
{
    /// The main menu's "Moono Tweaks" button and screen (OniMoono.GameTweaks.MoonoTweaksMenu,
    /// built from the game's own UI): the options EffectiveOptions resolves, each with a toggle,
    /// saved to the settings file (PlayerSettings) and applied by restarting the game. This side
    /// gives the screen its rows and saves what it chose; options the command line set are shown
    /// but not changeable. Each row's value is a number: 0 or 1 for a toggle (off, on), else the
    /// index in its Choices (the GPU's).
    internal static class MoonoTweaksMenu
    {
        public static void Install(EffectiveOptions effective, PlayerSettings file, string settingsPath)
        {
            // The rows, their sections and copy from the one catalog (GameTweakNames.Rows).
            var rows = GameTweakNames.Rows;
            static int Flag(bool on) => on ? 1 : 0;
            (int Value, bool Locked, int Default) State(TweakInfo row) => row.Id switch
            {
                GameTweakNames.SplashRow => (Flag(effective.Splash == "moo"), effective.SplashSource == OptionSource.CommandLine, Flag(EffectiveOptions.DefaultSplash == "moo")),
                GameTweakNames.FrameStatsRow => (Flag(effective.FrameStats), effective.FrameStatsSource == OptionSource.CommandLine, 0),
                GameTweakNames.LogPathsRow => (Flag(effective.HideLogPaths), effective.LogPathsSource == OptionSource.CommandLine, Flag(EffectiveOptions.DefaultLogPaths == "hidden")),
                GameTweakNames.GpuRow => (Array.IndexOf(row.Choices!, effective.Gpu), effective.GpuSource == OptionSource.CommandLine,
                    Array.IndexOf(row.Choices!, EffectiveOptions.DefaultGpu)),
                var name => (Flag(effective.Tweaks[name].On), effective.Tweaks[name].Source == OptionSource.CommandLine,
                    Flag(EffectiveOptions.DefaultOn(name, GameTweakNames.ChangingBehaviour))),
            };
            var states = rows.Select(State).ToArray();
            // The tooltip's parts (the screen styles the gain): the row's gain, or its group's
            // ("Saving together: ..."); then the launch option.
            static string Capitalized(string s) => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;
            string GainText(TweakInfo row) => row.Gain != null ? Capitalized(row.Gain.Effect)
                : row.Group.Gain != null ? $"{row.Group.Title} together: {row.Group.Gain.Effect}" : "";
            string Notes(TweakInfo row) => "Launch option: " + row.LaunchOption;
            var locked = new List<string>();
            foreach (var value in effective.CommandLineTweaks) locked.Add("--game-tweak " + value);
            if (effective.SplashSource == OptionSource.CommandLine) locked.Add("--splash " + effective.Splash);
            if (effective.FrameStatsSource == OptionSource.CommandLine) locked.Add("--frame-stats");
            if (effective.LogPathsSource == OptionSource.CommandLine) locked.Add("--log-paths " + effective.LogPaths);
            if (effective.GpuSource == OptionSource.CommandLine) locked.Add("--gpu " + effective.Gpu);

            var saved = file;
            Func<string[], int[], string?> save = (ids, values) =>
            {
                var tweaks = new Dictionary<string, bool>(StringComparer.Ordinal);
                bool? splash = null, frameStats = null, hideLogPaths = null;
                string? gpu = null;
                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i] == GameTweakNames.SplashRow) splash = values[i] != 0;
                    else if (ids[i] == GameTweakNames.FrameStatsRow) frameStats = values[i] != 0;
                    else if (ids[i] == GameTweakNames.LogPathsRow) hideLogPaths = values[i] != 0;
                    else if (ids[i] == GameTweakNames.GpuRow) gpu = rows.First(r => r.Id == GameTweakNames.GpuRow).Choices![values[i]];
                    else tweaks[ids[i]] = values[i] != 0;
                }
                var next = EffectiveOptions.Apply(saved, GameTweakNames.ChangingBehaviour, tweaks, splash, frameStats, hideLogPaths, gpu);
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

            var install = GameTweaks.GameTweaks.TypedAssembly.GetType("OniMoono.GameTweaks.MoonoTweaksMenu", throwOnError: true)!.GetMethod("Install")!;
            install.Invoke(null, [new Harmony("OniMoono.Host.MoonoTweaksMenu"),
                rows.Select(r => r.Id).ToArray(), rows.Select(r => r.Group.Title).ToArray(),
                rows.Select(r => r.Title).ToArray(), rows.Select(r => r.Effect).ToArray(),
                rows.Select(r => r.Detail).ToArray(), rows.Select(GainText).ToArray(), rows.Select(Notes).ToArray(),
                rows.Select(r => r.Group == GameTweakNames.ChangesGameplay).ToArray(), rows.Select(r => r.Choices).ToArray(),
                states.Select(s => s.Value).ToArray(), states.Select(s => s.Default).ToArray(), states.Select(s => s.Locked).ToArray(),
                string.Join(" ", locked), settingsPath, save]);
        }
    }
}
