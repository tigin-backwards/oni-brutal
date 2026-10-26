using System;
using System.Reflection;
using HarmonyLib;

namespace OniMoono.Host
{
    /// ARCHITECTURE D39. Type.GetFields and Type.GetProperties list a type's members in Mono's
    /// order (OniMoono.Engine.MonoMemberOrder): the type's own, then each base class's, each class's
    /// in metadata order. CoreCLR lists a class's static fields after its instance fields and its
    /// constants last, and Harmony's MonoMod re-sorts the cached lists of the types a patched
    /// method names by metadata token, base classes' members first where their tokens are lower:
    /// a state machine's parameters were numbered in another order once a mod patched one of its
    /// methods (issue #16's Critter Condo Save Fix), and the components MyAttributeManager adds
    /// and spawns for MyCmpAdd/Get/Req fields came in another order. Postfixes on CoreLib's
    /// RuntimeType.GetFields(BindingFlags) and GetProperties(BindingFlags) reorder each result, for
    /// the whole process as on Mono; every other overload (GetFields(), TypeInfo.DeclaredFields,
    /// FindMembers) calls these. GetMembers and GetMember are left as they are.
    internal static class MonoReflectionOrder
    {
        static Harmony harmony = null!;

        public static void Install()
        {
            if (harmony != null) return;
            harmony = new Harmony("OniMoono.Host.MonoReflectionOrder");
            var runtimeType = typeof(Type).Assembly.GetType("System.RuntimeType", throwOnError: true)!;
            harmony.Patch(Declared(runtimeType, nameof(Type.GetFields)), postfix: new HarmonyMethod(typeof(MonoReflectionOrder), nameof(Fields)));
            harmony.Patch(Declared(runtimeType, nameof(Type.GetProperties)), postfix: new HarmonyMethod(typeof(MonoReflectionOrder), nameof(Properties)));
        }

        static MethodInfo Declared(Type runtimeType, string name) =>
            runtimeType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, [typeof(BindingFlags)])
            ?? throw new MissingMethodException(runtimeType.FullName, name);

        static void Fields(Type __instance, FieldInfo[] __result) => OniMoono.Engine.MonoMemberOrder.Sort(__instance, __result);

        static void Properties(Type __instance, PropertyInfo[] __result) => OniMoono.Engine.MonoMemberOrder.Sort(__instance, __result);
    }
}
