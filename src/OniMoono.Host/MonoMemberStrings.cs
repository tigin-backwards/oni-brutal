using System;
using System.Reflection;
using HarmonyLib;

namespace OniMoono.Host
{
    /// ARCHITECTURE D35. Mono prints a field as its type's full name and its name
    /// (mcs/class/corlib/System.Reflection/RuntimeFieldInfo.cs: String.Format("{0} {1}",
    /// FieldType, name)), an event likewise (RuntimeEventInfo.cs: EventHandlerType + " " + Name).
    /// CoreCLR prints the type's short name when it is a primitive or nested type, or an array or
    /// pointer of one (Type.FormatTypeName): "Int32 NO_LIGHT" where Mono prints
    /// "System.Int32 NO_LIGHT". Harmony transpilers find instructions by comparing their operand's
    /// string with Mono's (Darkness Not Excluded Relit's patch of Workable.GetEfficiencyMultiplier
    /// found nothing and the mod's error ended the game). Methods, constructors, properties and
    /// parameters print alike on both runtimes and are left as they are.
    internal static class MonoMemberStrings
    {
        static Harmony harmony = null!;

        public static void Install()
        {
            if (harmony != null) return;
            harmony = new Harmony("OniMoono.Host.MonoMemberStrings");
            var core = typeof(FieldInfo).Assembly;
            harmony.Patch(DeclaredToString(core, "System.Reflection.RuntimeFieldInfo"), prefix: new HarmonyMethod(typeof(MonoMemberStrings), nameof(Field)));
            harmony.Patch(DeclaredToString(core, "System.Reflection.RuntimeEventInfo"), prefix: new HarmonyMethod(typeof(MonoMemberStrings), nameof(Event)));
        }

        static MethodInfo DeclaredToString(Assembly core, string type) =>
            core.GetType(type, throwOnError: true)!.GetMethod("ToString", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, Type.EmptyTypes)
            ?? throw new MissingMethodException(type, "ToString");

        static bool Field(FieldInfo __instance, ref string __result)
        {
            __result = __instance.FieldType + " " + __instance.Name;
            return false;
        }

        static bool Event(EventInfo __instance, ref string __result)
        {
            // An event without an add method throws as CoreCLR's does.
            if (__instance.GetAddMethod(true) == null) return true;
            __result = __instance.EventHandlerType + " " + __instance.Name;
            return false;
        }
    }
}
