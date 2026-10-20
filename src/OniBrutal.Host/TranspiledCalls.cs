using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host
{
    /// ARCHITECTURE D12. Mono compiles a `callvirt` to a static method as a call; CoreCLR's JIT
    /// refuses the method ("Method not found: '?'" when Harmony applies the patch). C# compilers
    /// never emit one, but Harmony transpilers that swap an instance call for a static helper and
    /// keep the opcode do (Mod Updater's KMod.Manager.Subscribe patch). The host turns such
    /// instructions into calls in the instructions Harmony's transpilers produce
    /// (CodeTranspiler.GetResult), before Harmony emits the patched method.
    internal static class TranspiledCalls
    {
        public static void Install()
        {
            var getResult = AccessTools.Method("HarmonyLib.CodeTranspiler:GetResult")
                ?? throw new MissingMethodException("HarmonyLib.CodeTranspiler", "GetResult");
            new Harmony("OniBrutal.Host.TranspiledCalls").Patch(getResult, postfix: new HarmonyMethod(typeof(TranspiledCalls), nameof(Postfix)));
        }

        static void Postfix(List<CodeInstruction> __result, MethodBase method)
        {
            foreach (var instruction in __result)
            {
                if (instruction.opcode != OpCodes.Callvirt || instruction.operand is not MethodInfo { IsStatic: true } target) continue;
                instruction.opcode = OpCodes.Call;
                EngineLog.Engine($"transpiled calls: callvirt to static {target.DeclaringType?.FullName}.{target.Name} in {method.DeclaringType?.FullName}.{method.Name} made a call");
            }
        }
    }
}
