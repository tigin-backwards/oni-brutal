using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace OniMoono.GameTweaks
{
    /// Game tweak `fast-save-type-checks` (opt-in). SaveLoadRoot.SaveWithoutTransform, run for every
    /// saved object, asks for each of its components twice whether its type carries
    /// [SkipSaveFileSerialization] (Type.IsDefined, which reads the attribute metadata every time)
    /// and writes the type's name (Type.ToString, a new string every time): in a large colony a
    /// tenth of a save's time is these runtime calls.
    ///
    /// Both answers depend only on the type, so they are kept per type: the same booleans and the
    /// same name are used, from the first time each type is asked. Only those two calls in that
    /// method are replaced.
    public static class FastSaveTypeChecks
    {
        public const string Name = "fast-save-type-checks";

        static readonly ConcurrentDictionary<(MemberInfo, Type, bool), bool> defined = new ConcurrentDictionary<(MemberInfo, Type, bool), bool>();
        static readonly ConcurrentDictionary<Type, string> names = new ConcurrentDictionary<Type, string>();

        public static void Install(Harmony harmony)
            => harmony.Patch(AccessTools.Method(typeof(SaveLoadRoot), nameof(SaveLoadRoot.SaveWithoutTransform)), transpiler: new HarmonyMethod(typeof(FastSaveTypeChecks), nameof(Transpiler)));

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var isDefined = AccessTools.Method(typeof(MemberInfo), nameof(MemberInfo.IsDefined), new[] { typeof(Type), typeof(bool) });
            var getType = AccessTools.Method(typeof(object), nameof(GetType));
            var toString = AccessTools.Method(typeof(object), nameof(ToString));
            var cachedIsDefined = AccessTools.Method(typeof(FastSaveTypeChecks), nameof(IsDefined));
            var cachedName = AccessTools.Method(typeof(FastSaveTypeChecks), nameof(TypeName));
            int checks = 0, typeNames = 0;
            CodeInstruction previous = null;
            foreach (var i in instructions)
            {
                // Replaced in place, so the instruction keeps its labels and exception blocks.
                if (i.Calls(isDefined))
                {
                    checks++;
                    i.opcode = OpCodes.Call;
                    i.operand = cachedIsDefined;
                }
                // Object.ToString called on the result of GetType(): the type's name.
                else if (i.Calls(toString) && previous != null && previous.Calls(getType))
                {
                    typeNames++;
                    i.opcode = OpCodes.Call;
                    i.operand = cachedName;
                }
                previous = i;
                yield return i;
            }
            if (checks != 2 || typeNames != 1) throw new InvalidOperationException($"{Name}: expected 2 IsDefined and 1 type name in SaveLoadRoot.SaveWithoutTransform, found {checks} and {typeNames}");
        }

        /// MemberInfo.IsDefined(attributeType, inherit), kept per member.
        static bool IsDefined(MemberInfo member, Type attribute, bool inherit)
            => defined.GetOrAdd((member, attribute, inherit), key => key.Item1.IsDefined(key.Item2, key.Item3));

        /// Type.ToString(), kept per type.
        static string TypeName(object type) => names.GetOrAdd((Type)type, t => t.ToString());
    }
}
