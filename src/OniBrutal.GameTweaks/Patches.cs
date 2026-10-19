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
                default: return false;
            }
        }
    }
}
