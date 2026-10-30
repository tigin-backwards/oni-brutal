using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OniMoono.Engine;

namespace OniMoono.Host
{
    /// ARCHITECTURE D55. A patch method's `___name` parameter gets the original's instance field
    /// `name`: Harmony emits `ldarg.0; ldfld name`. Patching a static method, it emits the same, so
    /// it reads the field's offset in whatever object the first argument is. Neither runtime checks
    /// the object's type there, and what is read depends on each runtime's field layout: Fast
    /// Track's prefix of the static IdleStates.MoveToNewCell asks for `___loop`, and reads the
    /// critter's IdleStates.Instance. In the original player that is null (measured for every
    /// critter kind on the prehistoric1 save); here it was the instance's transition stack, which
    /// the prefix then passed to GoTo as a state, and pushing it on the state machine's
    /// Stack<BaseState> threw ArrayTypeMismatchException (CoreCLR checks the array store).
    ///
    /// The host checks the object's type where Harmony injects an instance field into a patch of a
    /// static method: an instance of the field's type is read as before; anything else gives the
    /// field type's default (null), as in the original player for Fast Track. A field taken by
    /// reference (`ref`) is left as Harmony emits it.
    internal static class MonoInstanceFieldInjection
    {
        static FieldInfo config = null!, original = null!, il = null!;

        public static void Install()
        {
            var emit = AccessTools.Method("HarmonyLib.MethodCreatorTools:EmitCallParameter")
                ?? throw new MissingMethodException("HarmonyLib.MethodCreatorTools", "EmitCallParameter");
            config = AccessTools.Field("HarmonyLib.MethodCreator:config") ?? throw new MissingFieldException("HarmonyLib.MethodCreator", "config");
            original = AccessTools.Field("HarmonyLib.MethodCreatorConfig:original") ?? throw new MissingFieldException("HarmonyLib.MethodCreatorConfig", "original");
            il = AccessTools.Field("HarmonyLib.MethodCreatorConfig:il") ?? throw new MissingFieldException("HarmonyLib.MethodCreatorConfig", "il");
            new Harmony("OniMoono.Host.MonoInstanceFieldInjection").Patch(emit, postfix: new HarmonyMethod(typeof(MonoInstanceFieldInjection), nameof(Postfix)));
        }

        static void Postfix(object creator, MethodInfo patch, List<CodeInstruction> __result)
        {
            if (__result == null || creator == null) return;
            var settings = config.GetValue(creator);
            if (original.GetValue(settings) is not MethodBase { IsStatic: true } target) return;
            var generator = (ILGenerator)il.GetValue(settings)!;
            var first = target.GetParameters() is { Length: > 0 } parameters ? parameters[0].ParameterType : null;
            for (int i = 0; i + 1 < __result.Count; i++)
            {
                if (__result[i].opcode != OpCodes.Ldarg_0 || __result[i + 1].opcode != OpCodes.Ldfld
                    || __result[i + 1].operand is not FieldInfo { IsStatic: false, DeclaringType: { } declaring } field) continue;
                // An argument of the field's type, or a value type's (whose fields are read by address).
                if (first != null && (declaring.IsAssignableFrom(first) || first.IsByRef || declaring.IsValueType)) continue;
                var read = generator.DefineLabel();
                var done = generator.DefineLabel();
                var fallback = new List<CodeInstruction>
                {
                    new(OpCodes.Isinst, declaring),
                    new(OpCodes.Dup),
                    new(OpCodes.Brtrue, read),
                    new(OpCodes.Pop),
                };
                if (field.FieldType.IsValueType)
                    fallback.Add(new CodeInstruction(OpCodes.Ldloc, generator.DeclareLocal(field.FieldType)));
                else
                    fallback.Add(new CodeInstruction(OpCodes.Ldnull));
                fallback.Add(new CodeInstruction(OpCodes.Br, done));
                __result[i + 1].labels.Add(read);
                __result.InsertRange(i + 1, fallback);
                i += fallback.Count + 1;
                if (i + 1 < __result.Count) __result[i + 1].labels.Add(done);
                else __result.Add(new CodeInstruction(OpCodes.Nop) { labels = { done } });
                EngineLog.Engine($"mono field injection: {patch.DeclaringType?.FullName}.{patch.Name}'s ___{field.Name} of static {target.DeclaringType?.FullName}.{target.Name} reads {declaring.Name}'s field only from one, else the default");
            }
        }
    }
}
