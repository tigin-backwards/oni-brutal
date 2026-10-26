using System;
using System.Reflection;
using HarmonyLib;

namespace OniMoono.Host
{
    /// ARCHITECTURE D20. Before detouring a method, Harmony's MonoMod prepares it. For a P/Invoke,
    /// CoreCLR then binds the native export, and a missing one fails the patch
    /// (EntryPointNotFoundException). Mono binds a P/Invoke at its first call, so a mod can patch one
    /// whose export the platform lacks and keep it from being called: Debug Buttons and ONI Together
    /// skip `KImGuiUtil.SetKAssertCB`, absent from the game's Linux cimgui, and load in the original
    /// player. The host lets the preparation of a P/Invoke go without its export; the detour is
    /// placed on the method's own entry, and calling the original still fails as on Mono.
    internal static class PInvokePatches
    {
        static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            var compile = AccessTools.Method("MonoMod.Core.Platforms.PlatformTriple:Compile")
                ?? throw new MissingMethodException("MonoMod.Core.Platforms.PlatformTriple", "Compile");
            new Harmony("OniMoono.Host.PInvokePatches").Patch(compile, finalizer: new HarmonyMethod(typeof(PInvokePatches), nameof(Finalizer)));
        }

        static Exception? Finalizer(Exception? __exception, MethodBase method)
            => __exception is EntryPointNotFoundException or DllNotFoundException && (method.Attributes & MethodAttributes.PinvokeImpl) != 0 ? null : __exception;
    }
}
