using System;
using System.Reflection;
using HarmonyLib;

namespace OniMoono.Host
{
    /// ARCHITECTURE D44. Mono's lookup of a class by name takes '/' as the separator of nested
    /// types as well as '+' (mono/metadata/class.c: the name is split at its first '/', and
    /// return_nested_in follows the rest one '/' at a time), so `Assembly.GetType("Outer/Inner")`
    /// finds Outer's nested Inner, as ILDasm and Cecil spell it. CoreCLR's type name parser takes
    /// '+' only and finds nothing: Delivery Temperature Limit looks up
    /// "FetchAreaChore/StatesInstance/<>c__DisplayClass17_0" to patch a lambda of the game's, and its
    /// error then ended the game. A prefix of CoreLib's RuntimeAssembly.GetType(string, bool, bool),
    /// which every Assembly.GetType overload calls, spells such a name with '+'. No CLR type name
    /// can hold a '/' otherwise (C# and the game's assemblies never name one so).
    internal static class MonoNestedTypeNames
    {
        static Harmony harmony = null!;

        public static void Install()
        {
            if (harmony != null) return;
            harmony = new Harmony("OniMoono.Host.MonoNestedTypeNames");
            var runtimeAssembly = typeof(Assembly).Assembly.GetType("System.Reflection.RuntimeAssembly", throwOnError: true)!;
            var getType = runtimeAssembly.GetMethod(nameof(Assembly.GetType), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                [typeof(string), typeof(bool), typeof(bool)]) ?? throw new MissingMethodException(runtimeAssembly.FullName, nameof(Assembly.GetType));
            harmony.Patch(getType, prefix: new HarmonyMethod(typeof(MonoNestedTypeNames), nameof(Prefix)));
        }

        static void Prefix(ref string name)
        {
            if (name != null && name.Contains('/')) name = Spelled(name);
        }

        /// The name with '/' between nested types spelled '+', as CoreCLR parses it.
        internal static string Spelled(string name) => name.Replace('/', '+');
    }
}
