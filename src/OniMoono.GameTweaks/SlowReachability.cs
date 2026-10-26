using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace OniMoono.GameTweaks
{
    /// Game tweak `slow-reachability` (opt-in, **changes behaviour**). ReachabilityMonitor
    /// re-checks every workable's reachability (its cell, its work offsets, the Duplicants'
    /// reachable cells) once a game second, spread across frames. A large colony has tens of
    /// thousands of workables, mostly items in storage (U57: about 27,000 pickupables), and each
    /// check reads several objects: about 2 ms a frame. Most of that is reaching each object at
    /// all, so it cannot be skipped per object without paying it.
    ///
    /// With the tweak the monitor's update runs every 4 game seconds (the scheduler's next rate)
    /// instead of every second, still spread across frames. What changes: a workable whose
    /// reachability changes (a door opened or closed, a path dug or blocked) is seen as reachable
    /// or unreachable up to 3 game seconds later than in the game, so errands become available,
    /// or are dropped, that much later.
    public static class SlowReachability
    {
        public const string Name = "slow-reachability";

        public static void Install(Harmony harmony)
            => harmony.Patch(AccessTools.Method(typeof(ReachabilityMonitor), nameof(ReachabilityMonitor.InitializeStates)),
                transpiler: new HarmonyMethod(typeof(SlowReachability), nameof(Transpiler)));

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            int replaced = 0;
            for (int i = 0; i + 2 < list.Count; i++)
            {
                // root.FastUpdate("UpdateReachability", updater, UpdateRate.SIM_1000ms, load_balance: true)
                if (list[i].opcode == OpCodes.Ldc_I4_6 && list[i + 1].opcode == OpCodes.Ldc_I4_1
                    && list[i + 2].operand is System.Reflection.MethodInfo m && m.Name == "FastUpdate")
                {
                    list[i].opcode = OpCodes.Ldc_I4_7; // UpdateRate.SIM_4000ms
                    replaced++;
                }
            }
            if (replaced != 1) throw new InvalidOperationException($"{Name}: expected one SIM_1000ms FastUpdate in ReachabilityMonitor.InitializeStates, found {replaced}");
            return list;
        }
    }
}
