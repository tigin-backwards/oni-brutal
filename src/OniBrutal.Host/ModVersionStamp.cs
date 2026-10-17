using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host
{
    /// ARCHITECTURE D3. `KMod.Local.Subscribe` stores `directoryName.GetHashCode()` in mods.json
    /// as a local or dev mod's version stamp. Mono's string hash is stable; CoreCLR's is
    /// randomized per process, so the stamp changed at every launch: the game asked for a restart
    /// and disabled every local mod. The host replaces that one call with Mono's hash
    /// (OniBrutal.Engine.MonoStringHash), using the game's own Harmony, so the stamps are the ones
    /// the original player writes and mods.json is interchangeable between the two players. This
    /// is the host's only change to game code.
    internal static class ModVersionStamp
    {
        public static void Install()
        {
            var subscribe = AccessTools.Method("KMod.Local:Subscribe")
                ?? throw new MissingMethodException("KMod.Local", "Subscribe");
            new Harmony("OniBrutal.Host.ModVersionStamp").Patch(subscribe, transpiler: new HarmonyMethod(typeof(ModVersionStamp), nameof(Transpiler)));
        }

        /// `ldarg.1 (directoryName); callvirt object::GetHashCode` becomes a call to Mono's hash.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var getHashCode = typeof(object).GetMethod(nameof(GetHashCode), Type.EmptyTypes)!;
            var monoHash = typeof(MonoHash).GetMethod(nameof(MonoHash.OfString))!;
            var result = new List<CodeInstruction>(instructions);
            int replaced = 0;
            for (int i = 1; i < result.Count; i++)
            {
                if (!result[i].Calls(getHashCode) || !result[i - 1].IsLdarg(1)) continue;
                result[i].opcode = OpCodes.Call;
                result[i].operand = monoHash;
                replaced++;
            }
            if (replaced != 1)
                throw new InvalidOperationException($"KMod.Local.Subscribe: expected one directoryName.GetHashCode() call, found {replaced}.");
            return result;
        }
    }

    /// The call target the patched game method uses; public so the patched IL may call it.
    public static class MonoHash
    {
        public static int OfString(string text) => MonoStringHash.Compute(text);
    }
}
