using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host
{
    /// ARCHITECTURE D26. A struct that does not override GetHashCode is hashed by
    /// `ValueType.GetHashCode`: Mono XORs all its fields, CoreCLR uses the first field alone when
    /// the struct holds floats or references. The game uses one such hash as an identity:
    /// `BubbleManager.Archetype.GetId` files bubbles under the hash of (velocity, element, fade
    /// speed), so under CoreCLR every bubble rising straight up had one id, and the first element
    /// registered under it (an Oxy Coral's oxygen) was what every later bubble released: a Thermal
    /// Gas Fissure gave oxygen instead of natural gas. The host makes that call Mono's hash
    /// (OniBrutal.Engine.MonoValueTypeHash). A scan of Assembly-CSharp's IL for GetHashCode called
    /// on a game struct without an override finds this one call (the others are on Tag and
    /// HashedString, which override it, and on enums).
    internal static class MonoValueTypeHashes
    {
        public static void Install()
        {
            var archetype = AccessTools.TypeByName("BubbleManager")?.GetNestedType("Archetype", BindingFlags.NonPublic)
                ?? throw new TypeLoadException("BubbleManager+Archetype");
            var getId = AccessTools.Method(archetype, "GetId") ?? throw new MissingMethodException("BubbleManager+Archetype", "GetId");
            new Harmony("OniBrutal.Host.MonoValueTypeHashes").Patch(getId, transpiler: new HarmonyMethod(typeof(MonoValueTypeHashes), nameof(Transpiler)));
        }

        /// `constrained. T; callvirt object::GetHashCode` on a struct T that inherits
        /// ValueType.GetHashCode becomes `ldobj T; box T; call MonoHash.OfValueType`.
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var getHashCode = typeof(object).GetMethod(nameof(GetHashCode), Type.EmptyTypes)!;
            var monoHash = typeof(MonoHash).GetMethod(nameof(MonoHash.OfValueType))!;
            var result = new List<CodeInstruction>(instructions);
            int replaced = 0;
            for (int i = 1; i < result.Count; i++)
            {
                if (!result[i].Calls(getHashCode) || result[i - 1].opcode != OpCodes.Constrained) continue;
                if (result[i - 1].operand is not Type type || !type.IsValueType || type.IsEnum || OverridesGetHashCode(type)) continue;
                result[i - 1].opcode = OpCodes.Ldobj;
                result[i].opcode = OpCodes.Call;
                result[i].operand = monoHash;
                result.Insert(i, new CodeInstruction(OpCodes.Box, type));
                replaced++;
            }
            if (replaced != 1)
                throw new InvalidOperationException($"{original.DeclaringType}.{original.Name}: expected one inherited ValueType.GetHashCode() call, found {replaced}.");
            return result;
        }

        static bool OverridesGetHashCode(Type type) =>
            type.GetMethod(nameof(GetHashCode), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)!.DeclaringType != typeof(ValueType);
    }
}
