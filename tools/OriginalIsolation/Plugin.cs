using System;
using System.IO;
using BepInEx;
using BepInEx.Unity.Mono;
using HarmonyLib;

namespace OniMoono.OriginalIsolation
{
    /// TEST INFRASTRUCTURE for comparisons with the original Windows player, never shipped: run in
    /// a renamed copy of the game (scripts/original-windows.sh), it gives the original the same
    /// isolation our host gives itself (OniMoono.Host RunHome and AudioCapture). Documents and
    /// the local application data folder answer folders under ONI_RUN_HOME, so saves, settings,
    /// mods and the write test stay out of the user's own; FMOD writes the game's audio to
    /// fmodoutput.wav in the run home instead of the sound device. Without ONI_RUN_HOME the
    /// player is ended before the game reads any user data.
    [BepInPlugin("oni-moono.original-isolation", "OriginalIsolation", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        static string documents = "", localAppData = "";

        void Awake()
        {
            var home = Environment.GetEnvironmentVariable("ONI_RUN_HOME");
            if (string.IsNullOrEmpty(home) || !Path.IsPathRooted(home))
            {
                Logger.LogError("ONI_RUN_HOME (an absolute path) is not set: ending the player so it never reads the user's own data.");
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }
            documents = Path.Combine(home, "Documents");
            localAppData = Path.Combine(home, "AppData", "Local");
            Directory.CreateDirectory(documents);
            Directory.CreateDirectory(localAppData);
            Environment.CurrentDirectory = home;
            var harmony = new Harmony("oni-moono.original-isolation");
            var withOption = typeof(Environment).GetMethod(nameof(Environment.GetFolderPath), new[] { typeof(Environment.SpecialFolder), typeof(Environment.SpecialFolderOption) });
            var plain = typeof(Environment).GetMethod(nameof(Environment.GetFolderPath), new[] { typeof(Environment.SpecialFolder) });
            harmony.Patch(withOption, prefix: new HarmonyMethod(typeof(Plugin), nameof(FolderPrefix)));
            harmony.Patch(plain, prefix: new HarmonyMethod(typeof(Plugin), nameof(FolderPrefix)));
            var getOutputType = AccessTools.Method("FMODUnity.Platform:GetOutputType") ?? throw new MissingMethodException("FMODUnity.Platform", "GetOutputType");
            harmony.Patch(getOutputType, prefix: new HarmonyMethod(typeof(Plugin), nameof(OutputPrefix)));
            var check = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (check != documents)
            {
                Logger.LogError($"Documents still answers {check}: ending the player.");
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                return;
            }
            Logger.LogInfo($"run home {home}: Documents and local application data redirected, audio to {Path.Combine(home, "fmodoutput.wav")}");
        }

        static bool FolderPrefix(Environment.SpecialFolder folder, ref string __result)
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

        static bool OutputPrefix(ref FMOD.OUTPUTTYPE __result)
        {
            __result = FMOD.OUTPUTTYPE.WAVWRITER;
            return false;
        }
    }
}
