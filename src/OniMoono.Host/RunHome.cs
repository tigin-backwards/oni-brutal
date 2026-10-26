using System;
using System.IO;
using HarmonyLib;
using OniMoono.Engine;

namespace OniMoono.Host
{
    /// TEST INFRASTRUCTURE (`--run-home DIR`), not part of the product: keeps a test run's
    /// per-user data out of the user's own. Unity's persistent and cache paths are placed under
    /// DIR by Paths. On Windows the game also reads two known folders itself through
    /// `Environment.GetFolderPath`: Documents (`Documents\Klei\OxygenNotIncluded`: saves, mods,
    /// settings) and the local application data folder (a write test); those answer folders
    /// under DIR, as the XDG variables do for a Linux run (scripts/run.sh).
    internal static class RunHome
    {
        static string documents = "", localAppData = "";

        public static void Install(string dir)
        {
            if (!OperatingSystem.IsWindows()) return;
            documents = Path.Combine(dir, "Documents");
            localAppData = Path.Combine(dir, "AppData", "Local");
            Directory.CreateDirectory(documents);
            Directory.CreateDirectory(localAppData);
            var harmony = new Harmony("OniMoono.Host.RunHome");
            var withOption = typeof(Environment).GetMethod(nameof(Environment.GetFolderPath), [typeof(Environment.SpecialFolder), typeof(Environment.SpecialFolderOption)])!;
            var plain = typeof(Environment).GetMethod(nameof(Environment.GetFolderPath), [typeof(Environment.SpecialFolder)])!;
            harmony.Patch(withOption, prefix: new HarmonyMethod(typeof(RunHome), nameof(Prefix)));
            harmony.Patch(plain, prefix: new HarmonyMethod(typeof(RunHome), nameof(Prefix)));
            EngineLog.Engine($"run home {dir}: Documents and local application data redirected");
        }

        static bool Prefix(Environment.SpecialFolder folder, ref string __result)
        {
            switch (folder)
            {
                case Environment.SpecialFolder.MyDocuments:
                    __result = documents;
                    return false;
                case Environment.SpecialFolder.LocalApplicationData:
                    __result = localAppData;
                    return false;
                default:
                    return true;
            }
        }
    }
}
