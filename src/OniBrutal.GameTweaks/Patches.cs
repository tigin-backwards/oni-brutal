using HarmonyLib;

namespace OniBrutal.GameTweaks
{
    /// The host's entry point (OniBrutal.Host.GameTweaks): installs a tweak of this assembly by name.
    public static class Patches
    {
        public static bool Install(Harmony harmony, string name)
        {
            switch (name)
            {
                case OffscreenConveyors.Name: OffscreenConveyors.Install(harmony); return true;
                case FastSaveTypeChecks.Name: FastSaveTypeChecks.Install(harmony); return true;
                case FastAnimCleanup.Name: FastAnimCleanup.Install(harmony); return true;
                case FastSublimation.Name: FastSublimation.Install(harmony); return true;
                case FastAnimEvents.Name: FastAnimEvents.Install(harmony); return true;
                case FastSymbolOverrides.Name: FastSymbolOverrides.Install(harmony); return true;
                case FastJobs.Name: FastJobs.Install(harmony); return true;
                case SlowReachability.Name: SlowReachability.Install(harmony); return true;
                default: return false;
            }
        }
    }
}
