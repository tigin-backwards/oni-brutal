using System;
using HarmonyLib;

namespace OniMoono.Host
{
    /// ARCHITECTURE D24. On Linux and macOS, Mono's Environment.GetFolderPath gives a special
    /// folder's path (its XDG user directory, or the home's "Desktop", "Documents"…) whether or not
    /// it exists (mcs/class/corlib/System/Environment.cs: UnixGetFolderPath, ReadXdgUserDir).
    /// CoreCLR checks that it exists unless asked not to and gives "" otherwise. Harmony's debug log
    /// creates the Desktop folder from that path, so a mod that turns the log on (Simple Cheat)
    /// failed to load on a machine without ~/Desktop: Directory.CreateDirectory("") throws. The
    /// default option is answered as Mono does, without the existence check; on Windows, where
    /// Mono asks the shell as CoreCLR does, nothing changes.
    internal static class MonoFolderPaths
    {
        static Harmony harmony = null!;

        public static void Install()
        {
            if (harmony != null || OperatingSystem.IsWindows()) return;
            harmony = new Harmony("OniMoono.Host.MonoFolderPaths");
            var prefix = new HarmonyMethod(typeof(MonoFolderPaths), nameof(Unverified));
            harmony.Patch(AccessTools.Method(typeof(Environment), nameof(Environment.GetFolderPath), [typeof(Environment.SpecialFolder), typeof(Environment.SpecialFolderOption)]), prefix: prefix);
            harmony.Patch(AccessTools.Method(typeof(Environment), nameof(Environment.GetFolderPath), [typeof(Environment.SpecialFolder)]),
                prefix: new HarmonyMethod(typeof(MonoFolderPaths), nameof(Default)));
        }

        static void Unverified(ref Environment.SpecialFolderOption option)
        {
            if (option == Environment.SpecialFolderOption.None) option = Environment.SpecialFolderOption.DoNotVerify;
        }

        static bool Default(Environment.SpecialFolder folder, ref string __result)
        {
            __result = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
            return false;
        }
    }
}
