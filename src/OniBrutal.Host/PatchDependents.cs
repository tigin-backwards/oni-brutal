using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host
{
    /// ARCHITECTURE D21. Harmony compiles a patched method's replacement fully optimized, and
    /// CoreCLR's JIT inlines small methods it calls. A method patched later is marked not to be
    /// inlined from then on, but a replacement compiled before keeps the old body inlined, so the
    /// later patch never runs there. True Tiles patches BlockTileRenderer.GetConnectionBits, then
    /// adds a prefix to MatchesDef, which GetConnectionBits calls: the prefix never ran, and no tile
    /// connected to its neighbours. On Mono the later patch takes effect in that caller (the mod
    /// works in the original player). The host records the methods each replacement calls (from the
    /// instructions Harmony builds it from, CodeTranspiler.GetResult) and, the first time a method is
    /// patched, rebuilds the replacements already made that call it, as Harmony rebuilds one when a
    /// patch is added.
    internal static class PatchDependents
    {
        static bool installed;
        static readonly object gate = new();
        // Replacement's original → methods its instructions call; and the reverse.
        static readonly Dictionary<MethodBase, HashSet<MethodBase>> calleesOf = new();
        static readonly Dictionary<MethodBase, HashSet<MethodBase>> callersOf = new();
        static readonly HashSet<MethodBase> patched = new();
        static MethodInfo updateWrapper = null!, getPatchInfo = null!, updatePatchInfo = null!;
        [ThreadStatic] static bool rebuilding;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            var harmony = new Harmony("OniBrutal.Host.PatchDependents");
            var getResult = AccessTools.Method("HarmonyLib.CodeTranspiler:GetResult")
                ?? throw new MissingMethodException("HarmonyLib.CodeTranspiler", "GetResult");
            updateWrapper = AccessTools.Method("HarmonyLib.PatchFunctions:UpdateWrapper")
                ?? throw new MissingMethodException("HarmonyLib.PatchFunctions", "UpdateWrapper");
            getPatchInfo = AccessTools.Method("HarmonyLib.HarmonySharedState:GetPatchInfo")
                ?? throw new MissingMethodException("HarmonyLib.HarmonySharedState", "GetPatchInfo");
            updatePatchInfo = AccessTools.Method("HarmonyLib.HarmonySharedState:UpdatePatchInfo")
                ?? throw new MissingMethodException("HarmonyLib.HarmonySharedState", "UpdatePatchInfo");
            harmony.Patch(getResult, postfix: new HarmonyMethod(typeof(PatchDependents), nameof(Recorded)));
            harmony.Patch(updateWrapper, postfix: new HarmonyMethod(typeof(PatchDependents), nameof(Updated)));
        }

        /// The instructions a replacement of `method` is built from: the methods they call.
        static void Recorded(List<CodeInstruction> __result, MethodBase method)
        {
            if (method == null) return;
            var callees = new HashSet<MethodBase>();
            foreach (var instruction in __result)
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Newobj)
                    && instruction.operand is MethodBase callee)
                    callees.Add(callee);
            lock (gate)
            {
                if (calleesOf.TryGetValue(method, out var old))
                    foreach (var callee in old)
                        if (callersOf.TryGetValue(callee, out var callers)) callers.Remove(method);
                calleesOf[method] = callees;
                foreach (var callee in callees)
                {
                    if (!callersOf.TryGetValue(callee, out var callers)) callersOf[callee] = callers = new HashSet<MethodBase>();
                    callers.Add(method);
                }
            }
        }

        /// After `original`'s replacement is made: the first time, the earlier replacements that call
        /// it are rebuilt, now that it is marked not to be inlined.
        static void Updated(MethodBase original)
        {
            if (rebuilding) return;
            List<MethodBase> dependents;
            lock (gate)
            {
                if (!patched.Add(original)) return;
                if (!callersOf.TryGetValue(original, out var callers)) return;
                dependents = new List<MethodBase>();
                foreach (var caller in callers)
                    if (!caller.Equals(original) && patched.Contains(caller)) dependents.Add(caller);
            }
            if (dependents.Count == 0) return;
            rebuilding = true;
            try
            {
                foreach (var dependent in dependents)
                {
                    var info = getPatchInfo.Invoke(null, [dependent]);
                    if (info == null) continue;
                    var replacement = (MethodInfo)updateWrapper.Invoke(null, [dependent, info])!;
                    updatePatchInfo.Invoke(null, [dependent, replacement, info]);
                    EngineLog.Engine($"patch dependents: {dependent.DeclaringType?.FullName}.{dependent.Name} rebuilt, as it calls {original.DeclaringType?.FullName}.{original.Name}, patched after it");
                }
            }
            finally
            {
                rebuilding = false;
            }
        }
    }
}
