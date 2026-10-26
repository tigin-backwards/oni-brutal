using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OniMoono.Engine;

namespace OniMoono.Host
{
    /// ARCHITECTURE D21. Harmony compiles a patched method's replacement fully optimized, and
    /// CoreCLR's JIT inlines methods it calls, and methods those call, and so on. A method patched
    /// later is marked not to be inlined from then on, but a replacement compiled before keeps the
    /// old body inlined, so the later patch never runs there. True Tiles patches
    /// BlockTileRenderer.GetConnectionBits, then adds a prefix to MatchesDef, which GetConnectionBits
    /// calls: the prefix never ran, and no tile connected to its neighbours. Forever Eruption Geyser
    /// patches Geyser.GetEmitRate, then GetIterationPercent, which GetEmitRate reaches through
    /// GetOnDuration: the emit rate kept the unpatched share. On Mono the later patch takes effect in
    /// that caller (both mods work in the original player).
    ///
    /// The host records the methods each replacement calls (from the instructions Harmony builds it
    /// from, CodeTranspiler.GetResult) and, through each one the JIT may inline, the methods its IL
    /// calls: a call graph of the candidates for inlining reachable from replacements, each body
    /// read once. The first time a method is patched, if the JIT may have inlined it (or it is a
    /// P/Invoke), the host walks that graph back from it, through candidates, to the JIT's inlining
    /// depth, and rebuilds the replacements already made that call a method on the way (the patched
    /// one included), as Harmony rebuilds one when a patch is added.
    internal static class PatchDependents
    {
        // The JIT's bounds (dotnet/runtime src/coreclr/jit): DEFAULT_MAX_INLINE_DEPTH, and the
        // largest inlinee of its extended default policy, JitExtDefaultPolicyMaxILProf: measured on
        // .NET 10, a replacement inlines a straight-line method of 1024 bytes of IL, not one of 1026
        // (--check-types checks both). An AggressiveInlining method has no size bound.
        const int MaxInlineDepth = 20;
        const int MaxInlineIL = 0x400;

        static bool installed;
        static readonly object gate = new();
        // Replacement's original → the methods its instructions call; and the reverse. Called and
        // patched methods are keyed as their declaring type reflects them (Key).
        static readonly Dictionary<MethodBase, MethodBase[]> calleesOf = new();
        static readonly Dictionary<MethodBase, HashSet<MethodBase>> replacementsCalling = new();
        // A method read → the methods its IL calls, if the JIT may inline it (else null); and, for
        // a method, the candidates read whose IL calls it.
        static readonly Dictionary<MethodBase, MethodBase[]?> inlineCallees = new();
        static readonly Dictionary<MethodBase, List<MethodBase>> inlineCallers = new();
        static readonly HashSet<MethodBase> patched = new();
        static MethodInfo updateWrapper = null!, getPatchInfo = null!, updatePatchInfo = null!;
        static Type patchInfoType = null!;
        // D34: the patched methods whose inlined copies have been looked for (PatchInliners).
        static readonly HashSet<MethodBase> inlinersHandled = new();
        [ThreadStatic] static bool rebuilding, copying;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            var harmony = new Harmony("OniMoono.Host.PatchDependents");
            var getResult = AccessTools.Method("HarmonyLib.CodeTranspiler:GetResult")
                ?? throw new MissingMethodException("HarmonyLib.CodeTranspiler", "GetResult");
            updateWrapper = AccessTools.Method("HarmonyLib.PatchFunctions:UpdateWrapper")
                ?? throw new MissingMethodException("HarmonyLib.PatchFunctions", "UpdateWrapper");
            getPatchInfo = AccessTools.Method("HarmonyLib.HarmonySharedState:GetPatchInfo")
                ?? throw new MissingMethodException("HarmonyLib.HarmonySharedState", "GetPatchInfo");
            updatePatchInfo = AccessTools.Method("HarmonyLib.HarmonySharedState:UpdatePatchInfo")
                ?? throw new MissingMethodException("HarmonyLib.HarmonySharedState", "UpdatePatchInfo");
            patchInfoType = AccessTools.TypeByName("HarmonyLib.PatchInfo")
                ?? throw new TypeLoadException("HarmonyLib.PatchInfo");
            harmony.Patch(getResult, postfix: new HarmonyMethod(typeof(PatchDependents), nameof(Recorded)));
            harmony.Patch(updateWrapper, postfix: new HarmonyMethod(typeof(PatchDependents), nameof(Updated)));
            PatchInliners.Install();
        }

        /// The instructions a replacement of `method` is built from: the methods they call; and the
        /// call graph of the candidates for inlining reachable from them.
        static void Recorded(List<CodeInstruction> __result, MethodBase method)
        {
            if (method == null) return;
            var direct = new HashSet<MethodBase>();
            foreach (var instruction in __result)
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Newobj)
                    && instruction.operand is MethodBase callee)
                    direct.Add(Key(callee));
            lock (gate)
            {
                if (calleesOf.TryGetValue(method, out var old))
                    foreach (var callee in old)
                        if (replacementsCalling.TryGetValue(callee, out var replacements)) replacements.Remove(method);
                var callees = new MethodBase[direct.Count];
                direct.CopyTo(callees);
                calleesOf[method] = callees;
                foreach (var callee in callees)
                {
                    if (!replacementsCalling.TryGetValue(callee, out var replacements)) replacementsCalling[callee] = replacements = new HashSet<MethodBase>();
                    replacements.Add(method);
                }
                Explore(callees);
            }
        }

        /// Reads the bodies of the candidates for inlining reachable from `from` through
        /// candidates, each once. A method already patched is not inlined (Harmony marked it), so
        /// nothing is reached through it.
        static void Explore(MethodBase[] from)
        {
            var pending = new Stack<MethodBase>(from);
            while (pending.Count > 0)
            {
                var method = pending.Pop();
                if (inlineCallees.ContainsKey(method) || patched.Contains(method)) continue;
                var callees = InlineCallees(method);
                inlineCallees[method] = callees;
                if (callees == null) continue;
                foreach (var callee in callees)
                {
                    if (!inlineCallers.TryGetValue(callee, out var callers)) inlineCallers[callee] = callers = new List<MethodBase>();
                    callers.Add(method);
                    if (!inlineCallees.ContainsKey(callee)) pending.Push(callee);
                }
            }
        }

        /// The methods `method`'s IL calls, if the JIT may inline it: it has IL, is not marked
        /// NoInlining or synchronized, and is small enough or marked AggressiveInlining. A virtual
        /// method counts, as the JIT inlines one it devirtualizes.
        static MethodBase[]? InlineCallees(MethodBase method)
        {
            try
            {
                var flags = method.MethodImplementationFlags;
                if ((flags & (MethodImplAttributes.NoInlining | MethodImplAttributes.Synchronized | MethodImplAttributes.InternalCall
                        | MethodImplAttributes.Native | MethodImplAttributes.Runtime)) == 0
                    && !method.IsAbstract && (method.Attributes & MethodAttributes.PinvokeImpl) == 0
                    && method is not MethodBuilder and not ConstructorBuilder and not DynamicMethod
                    && method.GetMethodBody()?.GetILAsByteArray() is { } il
                    && (il.Length <= MaxInlineIL || (flags & MethodImplAttributes.AggressiveInlining) != 0))
                    return CalledBy(method, il);
            }
            catch (Exception) // a body or token reflection cannot read: nothing reached through it
            {
            }
            return null;
        }

        static readonly OpCode[] oneByte = new OpCode[0x100], twoByte = new OpCode[0x100];

        static PatchDependents()
        {
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.GetValue(null) is OpCode code)
                {
                    var value = (ushort)code.Value;
                    if (code.Size == 1) oneByte[value] = code;
                    else if ((value >> 8) == 0xFE) twoByte[value & 0xFF] = code;
                }
        }

        /// The methods `il` (the body of `method`) calls or constructs with.
        static MethodBase[] CalledBy(MethodBase method, byte[] il)
        {
            var typeArguments = method.DeclaringType is { IsGenericType: true } type ? type.GetGenericArguments() : null;
            var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            var found = new HashSet<MethodBase>();
            int at = 0;
            while (at < il.Length)
            {
                var code = il[at] == 0xFE && at + 1 < il.Length ? twoByte[il[++at]] : oneByte[il[at]];
                at++;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: at += 1; break;
                    case OperandType.InlineVar: at += 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: at += 8; break;
                    case OperandType.InlineSwitch: at += 4 + 4 * BitConverter.ToInt32(il, at); break;
                    case OperandType.InlineMethod:
                        if (code == OpCodes.Call || code == OpCodes.Callvirt || code == OpCodes.Newobj)
                        {
                            var callee = method.Module.ResolveMethod(BitConverter.ToInt32(il, at), typeArguments, methodArguments);
                            if (callee != null) found.Add(Key(callee));
                        }
                        at += 4;
                        break;
                    default: at += 4; break;
                }
            }
            var callees = new MethodBase[found.Count];
            found.CopyTo(callees);
            return callees;
        }

        /// A method as its declaring type reflects it, as call instructions name it (one found
        /// through a derived type is a different MethodBase).
        static MethodBase Key(MethodBase method)
        {
            if (method.DeclaringType is not { } type || method.ReflectedType == type) return method;
            try
            {
                return MethodBase.GetMethodFromHandle(method.MethodHandle, type.TypeHandle) ?? method;
            }
            catch (Exception)
            {
                return method;
            }
        }

        /// After `original`'s replacement is made: the first time, the earlier replacements that may
        /// contain it are rebuilt, now that it is marked not to be inlined (D21), and so is the code
        /// the JIT compiled before with it inlined (D34, PatchInliners).
        static void Updated(MethodBase original, MethodInfo __result)
        {
            PatchInliners.Replaced(original, __result, copy: copying);
            if (rebuilding) return;
            var key = Key(original);
            var dependents = new List<MethodBase>();
            bool first;
            lock (gate)
            {
                if (patched.Add(key)) dependents.AddRange(ReplacementsInlining(key));
                first = inlinersHandled.Add(key);
            }
            if (first)
                foreach (var inliner in PatchInliners.Inlining(original))
                    if (!Key(inliner).Equals(key) && !dependents.Contains(inliner)) dependents.Add(inliner);
            Rebuild(dependents, $"it may inline {original.DeclaringType?.FullName}.{original.Name}, patched after it");
        }

        /// D21, under the gate: the replacements already made whose code may hold `key`'s, when the
        /// JIT may have inlined it: those calling it or a candidate reaching it.
        static List<MethodBase> ReplacementsInlining(MethodBase key)
        {
            var dependents = new List<MethodBase>();
            if (!inlineCallees.TryGetValue(key, out var callees)) callees = InlineCallees(key);
            // A P/Invoke's call is inlined too (its stub's work, then the native function): it
            // counts, though it has no IL.
            if (callees == null && (key.Attributes & MethodAttributes.PinvokeImpl) == 0) return dependents;
            // The candidates whose inlined body may hold `key`'s: it, and those reaching it
            // through candidates not yet patched, the chain at most MaxInlineDepth deep.
            var inlinedIn = new HashSet<MethodBase> { key };
            var level = new List<MethodBase> { key };
            for (int depth = 1; depth < MaxInlineDepth && level.Count > 0; depth++)
            {
                var next = new List<MethodBase>();
                foreach (var method in level)
                    if (inlineCallers.TryGetValue(method, out var callers))
                        foreach (var caller in callers)
                            if (!patched.Contains(caller) && inlinedIn.Add(caller)) next.Add(caller);
                level = next;
            }
            var seen = new HashSet<MethodBase>();
            foreach (var method in inlinedIn)
                if (replacementsCalling.TryGetValue(method, out var replacements))
                    foreach (var replacement in replacements)
                        if (!Key(replacement).Equals(key) && patched.Contains(Key(replacement)) && seen.Add(replacement))
                            dependents.Add(replacement);
            return dependents;
        }

        /// Rebuilds each method's replacement as Harmony rebuilds one when a patch is added: with its
        /// patches, or, for a method without (code of the game or a mod, D34), as a copy compiled
        /// from its own IL, which Harmony replaces when the method is patched itself.
        internal static void Rebuild(List<MethodBase> methods, string why)
        {
            if (methods.Count == 0) return;
            bool wasRebuilding = rebuilding;
            rebuilding = true;
            try
            {
                foreach (var method in methods)
                {
                    var info = getPatchInfo.Invoke(null, [method]);
                    bool copy = info == null;
                    if (copy && !PatchInliners.IsCopy(method) && !PatchInliners.Copyable(method, out var not))
                    {
                        EngineLog.Engine($"patch inliners: {method.DeclaringType?.FullName}.{method.Name} not rebuilt, though {why}: {not}");
                        continue;
                    }
                    try
                    {
                        copying = copy;
                        var replacement = (MethodInfo)updateWrapper.Invoke(null, [method, info ?? Activator.CreateInstance(patchInfoType)])!;
                        if (!copy) updatePatchInfo.Invoke(null, [method, replacement, info]);
                    }
                    catch (TargetInvocationException e)
                    {
                        EngineLog.Engine($"patch inliners: {method.DeclaringType?.FullName}.{method.Name} not rebuilt, though {why}: {e.InnerException?.Message}");
                        continue;
                    }
                    finally
                    {
                        copying = false;
                    }
                    EngineLog.Engine($"patch dependents: {method.DeclaringType?.FullName}.{method.Name} {(copy ? "copied" : "rebuilt")}, as {why}");
                }
            }
            finally
            {
                rebuilding = wasRebuilding;
            }
        }
    }
}
