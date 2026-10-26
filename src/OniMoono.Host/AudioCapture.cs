using System;
using System.IO;
using HarmonyLib;
using OniMoono.Engine;

namespace OniMoono.Host
{
    /// TEST INFRASTRUCTURE (`--audio-wav DIR`), not part of the product: the game's audio goes to
    /// a file instead of the sound device, so test runs never play on the user's speakers (on
    /// Linux scripts/audio-sink does this with a private ALSA device). FMODUnity takes the output
    /// type from its platform settings (`FMODUnity.Platform.GetOutputType`); this answers FMOD's
    /// real-time WAV writer, which mixes at the device rate into fmodoutput.wav in the working
    /// directory, here DIR.
    internal static class AudioCapture
    {
        public static void Install(string dir)
        {
            Directory.CreateDirectory(dir);
            Environment.CurrentDirectory = dir;
            var getOutputType = AccessTools.Method("FMODUnity.Platform:GetOutputType")
                ?? throw new MissingMethodException("FMODUnity.Platform", "GetOutputType");
            new Harmony("OniMoono.Host.AudioCapture").Patch(getOutputType, prefix: new HarmonyMethod(typeof(AudioCapture), nameof(Prefix)));
            EngineLog.Engine($"audio to {Path.Combine(dir, "fmodoutput.wav")}");
        }

        static bool Prefix(ref FMOD.OUTPUTTYPE __result)
        {
            __result = FMOD.OUTPUTTYPE.WAVWRITER;
            return false;
        }
    }
}
