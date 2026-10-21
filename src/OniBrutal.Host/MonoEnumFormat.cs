using System;
using System.Reflection;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host
{
    /// ARCHITECTURE D14. Mods that add elements give their SimHashes values names by patching
    /// Mono's private `Enum.InternalFormat(RuntimeType eT, Object value)`, which Enum.ToString
    /// and Enum.Format use for the "G" format (Rocketry Expanded's shared ElementUtil, through
    /// AccessTools.Method). CoreCLR has no such method: the patch failed, element names came out
    /// as numbers and lookups by name failed. When a mod looks the method up, the host hands it a
    /// stand-in with Mono's signature and from then on formats enum names through it, so the
    /// mod's patch of it applies; without such a mod nothing changes.
    internal static class MonoEnumFormat
    {
        static Harmony harmony = null!;
        static bool routed;
        [ThreadStatic] static bool formatting;

        public static void Install()
        {
            harmony = new Harmony("OniBrutal.Host.MonoEnumFormat");
            var method = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.Method), [typeof(Type), typeof(string), typeof(Type[]), typeof(Type[])])
                ?? throw new MissingMethodException(nameof(AccessTools), nameof(AccessTools.Method));
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(MonoEnumFormat), nameof(LookupPostfix)));
        }

        static void LookupPostfix(Type type, string name, ref MethodInfo __result)
        {
            if (__result != null || type != typeof(Enum) || name != "InternalFormat") return;
            __result = AccessTools.Method(typeof(MonoEnumFormat), nameof(InternalFormat));
            Route();
        }

        /// Mono's InternalFormat: an enum value's "G" text, given (as Mono gives it) the enum type
        /// and the value as its underlying integer. Mods patch this.
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

        /// Enum's "G" formatting (ToString with no format or "G", Format with "G", and the span
        /// formatting string interpolation uses) goes through InternalFormat.
        static void Route()
        {
            if (routed) return;
            routed = true;
            var enumType = typeof(Enum);
            void Prefix(MethodBase? target, string prefix)
            {
                if (target == null) throw new MissingMethodException(enumType.FullName, prefix);
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(MonoEnumFormat), prefix));
            }
            Prefix(AccessTools.Method(enumType, nameof(Enum.ToString), Type.EmptyTypes), nameof(ToStringPrefix));
            Prefix(AccessTools.Method(enumType, nameof(Enum.ToString), [typeof(string)]), nameof(ToStringFormatPrefix));
            // The obsolete overloads, which ignore the provider (Mono's forward to the two above).
            Prefix(AccessTools.Method(enumType, nameof(Enum.ToString), [typeof(IFormatProvider)]), nameof(ToStringPrefix));
            Prefix(AccessTools.Method(enumType, nameof(Enum.ToString), [typeof(string), typeof(IFormatProvider)]), nameof(ToStringFormatPrefix));
            Prefix(AccessTools.Method(enumType, nameof(Enum.Format), [typeof(Type), typeof(object), typeof(string)]), nameof(FormatPrefix));
            Prefix(AccessTools.Method(enumType, "System.ISpanFormattable.TryFormat"), nameof(TryFormatPrefix));
            EngineLog.Engine("mono enum format: a mod patches Enum.InternalFormat; enum names go through it");
        }

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

        static bool ToStringFormatPrefix(Enum __instance, string? format, ref string __result)
        {
            if (formatting || !IsGeneral(format)) return true;
            __result = Format(__instance);
            return false;
        }

        static bool FormatPrefix(Type enumType, object value, string format, ref string __result)
        {
            if (formatting || format == null || !IsGeneral(format) || enumType == null || !enumType.IsEnum || value == null) return true;
            // Mono takes the enum or its underlying integer; anything else goes on to the original,
            // which rejects it.
            if (value is Enum e && e.GetType() == enumType) __result = Format(e);
            else if (value.GetType() == Enum.GetUnderlyingType(enumType)) __result = InternalFormat(enumType, value);
            else return true;
            return false;
        }

        static bool TryFormatPrefix(Enum __instance, Span<char> destination, ref int charsWritten, ReadOnlySpan<char> format, ref bool __result)
        {
            if (formatting || !IsGeneral(format)) return true;
            var text = Format(__instance);
            __result = text.AsSpan().TryCopyTo(destination);
            charsWritten = __result ? text.Length : 0;
            return false;
        }
    }
}
