using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using OniMoono.Engine;

namespace OniMoono.Host
{
    /// ARCHITECTURE D18. A method of a generic class instantiated over reference types runs code
    /// that every such instantiation shares (`Pool<__Canon>.Draw` for `Pool<Image>.Draw` and
    /// `Pool<MonoBehaviour>.Draw`), as on Mono, so a Harmony patch of one instantiation's method
    /// applies to all (Better Info Cards patches `HoverTextDrawer.Pool<MonoBehaviour>.Draw`, which
    /// the game never instantiates, to see every hover-card widget drawn). When tiered compilation
    /// recompiles that shared code, Harmony's MonoMod puts its detour on the new code, but only
    /// for a recompiled method equal to the patched one; the shared code's is the canonical
    /// method (`Pool<__Canon>.Draw`), so the detour stays on the old code and the patch stops
    /// running once the method tiers up (Better Info Cards: its cards lost their shadow bar,
    /// NullReferenceException every frame). The host gives MonoMod's handler each patched method
    /// whose code was recompiled under the canonical method: the same method handle, as all the
    /// instantiations sharing the code have.
    internal static class SharedGenerics
    {
        static bool installed;
        static IDictionary detours = null!;
        static MethodInfo methodCompiled = null!;
        // The patched methods of constructed generic types by method handle, from MonoMod's table
        // (rebuilt when it changes size).
        static Dictionary<IntPtr, List<MethodBase>> byHandle = new();
        static int indexedCount = -1;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            var harmony = typeof(Harmony).Assembly;
            var detour = harmony.GetType("MonoMod.Core.Platforms.PlatformTripleDetourFactory+Detour", true)!;
            detours = (IDictionary)(detour.GetField("relatedDetours", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingFieldException(detour.FullName, "relatedDetours")).GetValue(null)!;
            methodCompiled = detour.GetMethod("OnMethodCompiled", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(detour.FullName, "OnMethodCompiled");
            var triple = harmony.GetType("MonoMod.Core.Platforms.PlatformTriple", true)!;
            var runtime = triple.GetProperty("Runtime")!.GetValue(triple.GetProperty("Current")!.GetValue(null))!;
            var compiledEvent = harmony.GetType("MonoMod.Core.Platforms.IRuntime", true)!.GetEvent("OnMethodCompiled")
                ?? throw new MissingMemberException("MonoMod.Core.Platforms.IRuntime", "OnMethodCompiled");
            compiledEvent.AddEventHandler(runtime, Delegate.CreateDelegate(compiledEvent.EventHandlerType!, typeof(SharedGenerics).GetMethod(nameof(Compiled), BindingFlags.NonPublic | BindingFlags.Static)!));
        }

        /// MonoMod's OnMethodCompiledCallback, raised for every method the JIT compiles.
        static void Compiled(RuntimeMethodHandle handle, MethodBase? method, IntPtr codeStart, IntPtr codeRw, ulong codeSize)
        {
            if (method?.DeclaringType is not { IsConstructedGenericType: true }) return;
            var index = Index();
            if (!index.TryGetValue(handle.Value, out var patched)) return;
            foreach (var target in patched)
            {
                if (target.Equals(method)) continue; // MonoMod's own handler has it.
                methodCompiled.Invoke(null, [handle, target, codeStart, codeRw, codeSize]);
            }
        }

        static Dictionary<IntPtr, List<MethodBase>> Index()
        {
            lock (detours)
            {
                int count = detours.Count;
                if (count == indexedCount) return byHandle;
                var index = new Dictionary<IntPtr, List<MethodBase>>();
                foreach (var key in detours.Keys)
                {
                    if (key is not MethodBase { DeclaringType.IsConstructedGenericType: true } patched) continue;
                    var value = patched.MethodHandle.Value;
                    if (!index.TryGetValue(value, out var list)) index[value] = list = new List<MethodBase>();
                    list.Add(patched);
                }
                bool changed = index.Count != byHandle.Count;
                byHandle = index;
                indexedCount = count;
                if (changed) EngineLog.Engine($"shared generics: {index.Count} patched method(s) of generic classes follow their shared code");
                return index;
            }
        }
    }
}
