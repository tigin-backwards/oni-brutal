using System;
using System.Reflection;
using HarmonyLib;

namespace OniMoono.Host
{
    /// ARCHITECTURE D19. Harmony calls a mod's prefixes, postfixes and finalizers from the
    /// replacement method it builds for the patched method, a dynamic method owned by 0Harmony.
    /// CoreCLR's JIT inlines a small patch method into it, so the patch method's frame is gone, and
    /// what it calls sees 0Harmony as its caller: Assembly.GetCallingAssembly() names 0Harmony,
    /// whose folder is not the mod's (Traffic Visualizer's SquareLib finds its icons from it, and the
    /// game stopped loading). On Mono the patch method keeps its frame, as measured with the mod in
    /// the original player. The host marks every patch method not to be inlined (MonoMod's own
    /// TryDisableInlining, as Harmony does for the patched method) before Harmony builds the
    /// replacement; this project's own patches (the game tweaks, the host's) keep being inlined.
    internal static class PatchFrames
    {
        static bool installed;
        static Func<MethodBase, bool> disableInlining = null!;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            var harmony = typeof(Harmony).Assembly;
            var triple = harmony.GetType("MonoMod.Core.Platforms.PlatformTriple", true)!;
            var current = triple.GetProperty("Current")!.GetValue(null)!;
            var tryDisable = triple.GetMethod("TryDisableInlining", [typeof(MethodBase)])
                ?? throw new MissingMethodException(triple.FullName, "TryDisableInlining");
            disableInlining = (Func<MethodBase, bool>)Delegate.CreateDelegate(typeof(Func<MethodBase, bool>), current, tryDisable);
            var updateWrapper = AccessTools.Method("HarmonyLib.PatchFunctions:UpdateWrapper")
                ?? throw new MissingMethodException("HarmonyLib.PatchFunctions", "UpdateWrapper");
            new Harmony("OniMoono.Host.PatchFrames").Patch(updateWrapper, prefix: new HarmonyMethod(typeof(PatchFrames), nameof(Prefix)));
        }

        static void Prefix(PatchInfo patchInfo)
        {
            foreach (var patches in new[] { patchInfo.prefixes, patchInfo.postfixes, patchInfo.finalizers, patchInfo.innerprefixes, patchInfo.innerpostfixes })
                foreach (var patch in patches)
                    if (patch.PatchMethod is { } method && !Ours(method.Module.Assembly.GetName().Name))
                        disableInlining(method);
        }

        /// The host (OniMoono) and the engine's assemblies (OniMoono.*).
        static bool Ours(string? name) => name?.StartsWith("OniMoono", StringComparison.Ordinal) == true;
    }
}
