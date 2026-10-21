using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host
{
    /// ARCHITECTURE D14. Mods that add elements give their SimHashes values names by patching
    /// enum formatting: Mono's private `Enum.InternalFormat(RuntimeType eT, Object value)` (Rocketry
    /// Expanded's shared ElementUtil, through AccessTools), or `Enum.ToString()`. In Mono's
    /// Enum (system/enum.cs) ToString() is InternalFormat(GetType(), GetValue()); ToString(format)
    /// with "G" (or none), the obsolete overloads and the IFormattable route that string.Format
    /// takes all call the virtual ToString(); Format(type, value, "G") calls InternalFormat. CoreCLR
    /// has no InternalFormat, and its ToString(format) and span formatting do not go through
    /// ToString(): the patches failed or reached only some paths, and element names came out as
    /// numbers. Once a mod looks InternalFormat up (it gets a stand-in with Mono's signature) or
    /// patches ToString(), the host routes the formatting as Mono's does; without such a mod
    /// nothing changes.
    internal static class MonoEnumFormat
    {
        static Harmony harmony = null!;
        static FieldInfo patchOriginal = null!;
        static MethodInfo enumToString = null!;
        static bool toStringRouted, internalFormatRouted;
        [ThreadStatic] static bool formatting;

        public static void Install()
        {
            if (harmony != null) return;
            harmony = new Harmony("OniBrutal.Host.MonoEnumFormat");
            enumToString = AccessTools.Method(typeof(Enum), nameof(Enum.ToString), Type.EmptyTypes);
            var lookup = new HarmonyMethod(typeof(MonoEnumFormat), nameof(LookupPostfix));
            foreach (var name in new[] { nameof(AccessTools.Method), nameof(AccessTools.DeclaredMethod) })
            {
                var method = AccessTools.Method(typeof(AccessTools), name, [typeof(Type), typeof(string), typeof(Type[]), typeof(Type[])])
                    ?? throw new MissingMethodException(nameof(AccessTools), name);
                harmony.Patch(method, postfix: lookup);
            }
            patchOriginal = AccessTools.Field(typeof(PatchProcessor), "original")
                ?? throw new MissingFieldException(nameof(PatchProcessor), "original");
            harmony.Patch(AccessTools.Method(typeof(PatchProcessor), nameof(PatchProcessor.Patch)),
                postfix: new HarmonyMethod(typeof(MonoEnumFormat), nameof(PatchPostfix)));
        }

        static void LookupPostfix(Type type, string name, ref MethodInfo __result)
        {
            if (__result != null || type != typeof(Enum) || name != "InternalFormat") return;
            __result = AccessTools.Method(typeof(MonoEnumFormat), nameof(InternalFormat));
            RouteInternalFormat();
        }

        /// A mod's patch of Enum.ToString() reaches every path Mono sends through it.
        static void PatchPostfix(PatchProcessor __instance)
        {
            if (!toStringRouted && Equals(patchOriginal.GetValue(__instance), enumToString)) RouteToString();
        }

        /// Mono's InternalFormat: an enum value's "G" text, given (as Mono gives it) the enum type
        /// and the value as its underlying integer. Mods patch this.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static string InternalFormat(Type eT, object value)
        {
            formatting = true;
            try
            {
                return Enum.ToObject(eT, value).ToString()!;
            }
            finally
            {
                formatting = false;
            }
        }

        static void Prefix(MethodBase? target, string prefix)
        {
            if (target == null) throw new MissingMethodException(nameof(Enum), prefix);
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(MonoEnumFormat), prefix));
        }

        /// ToString(format) with "G", the obsolete overloads and span formatting call ToString().
        static void RouteToString()
        {
            if (toStringRouted) return;
            toStringRouted = true;
            var enumType = typeof(Enum);
            Prefix(AccessTools.Method(enumType, nameof(Enum.ToString), [typeof(string)]), nameof(ToStringFormatPrefix));
            Prefix(AccessTools.Method(enumType, nameof(Enum.ToString), [typeof(IFormatProvider)]), nameof(ToStringProviderPrefix));
            Prefix(AccessTools.Method(enumType, nameof(Enum.ToString), [typeof(string), typeof(IFormatProvider)]), nameof(ToStringFormatPrefix));
            Prefix(AccessTools.Method(enumType, "System.ISpanFormattable.TryFormat"), nameof(TryFormatPrefix));
            EngineLog.Engine("mono enum format: enum formatting goes through Enum.ToString(), as in Mono");
        }

        /// ToString() and Format with "G" call InternalFormat (and everything above, ToString()).
        static void RouteInternalFormat()
        {
            if (internalFormatRouted) return;
            internalFormatRouted = true;
            RouteToString();
            Prefix(enumToString, nameof(ToStringPrefix));
            Prefix(AccessTools.Method(typeof(Enum), nameof(Enum.Format), [typeof(Type), typeof(object), typeof(string)]), nameof(FormatPrefix));
            EngineLog.Engine("mono enum format: a mod patches Enum.InternalFormat; enum names go through it");
        }

        /// Mono's "G" test: none, empty, "G" or "g".
        static bool IsGeneral(ReadOnlySpan<char> format) => format.IsEmpty || format is "G" or "g";

        /// As Mono calls it: the enum's type and its underlying value.
        static string Format(Enum value)
        {
            var type = value.GetType();
            return InternalFormat(type, Convert.ChangeType(value, Enum.GetUnderlyingType(type)));
        }

        static bool ToStringPrefix(Enum __instance, ref string __result)
        {
            if (formatting) return true;
            __result = Format(__instance);
            return false;
        }

        static bool ToStringProviderPrefix(Enum __instance, ref string __result)
        {
            if (formatting) return true;
            __result = __instance.ToString();
            return false;
        }

        static bool ToStringFormatPrefix(Enum __instance, string? format, ref string __result)
        {
            if (formatting || !IsGeneral(format)) return true;
            __result = __instance.ToString();
            return false;
        }

        static bool FormatPrefix(Type enumType, object value, string format, ref string __result)
        {
            // Mono's Format takes exactly one format character ("" throws, as the original does).
            if (formatting || format is not ("G" or "g") || enumType == null || !enumType.IsEnum || value == null) return true;
            // The enum or its underlying integer; anything else goes on to the original, which
            // rejects it.
            if (value is Enum e && e.GetType() == enumType) __result = Format(e);
            else if (value.GetType() == Enum.GetUnderlyingType(enumType)) __result = InternalFormat(enumType, value);
            else return true;
            return false;
        }

        static bool TryFormatPrefix(Enum __instance, Span<char> destination, ref int charsWritten, ReadOnlySpan<char> format, ref bool __result)
        {
            if (formatting || !IsGeneral(format)) return true;
            var text = __instance.ToString();
            __result = text.AsSpan().TryCopyTo(destination);
            charsWritten = __result ? text.Length : 0;
            return false;
        }
    }
}
