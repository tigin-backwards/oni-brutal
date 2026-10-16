using System;
using System.Globalization;

namespace UnityEngine
{
    /// Number formatting for the engine's ToString methods as Unity's player produces it: Unity
    /// formats on Mono, which (like .NET Framework) drops the minus sign of a result that rounds
    /// to zero ("0.00", where .NET Core prints "-0.00"); observed with tools/UnityProbe.
    internal static class UnityFormat
    {
        public static string Number(float value, string format, IFormatProvider provider) => Unsigned(value.ToString(format, provider), provider);

        public static string Number(double value, string format, IFormatProvider provider) => Unsigned(value.ToString(format, provider), provider);

        public static string Number(int value, string format, IFormatProvider provider) => value.ToString(format, provider);

        static string Unsigned(string text, IFormatProvider provider)
        {
            var info = NumberFormatInfo.GetInstance(provider);
            string minus = info.NegativeSign;
            if (!text.StartsWith(minus, StringComparison.Ordinal)) return text;
            for (int i = minus.Length; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsDigit(c) && c != '0') return text;
                if (c is 'E' or 'e' or 'I' or 'N') return text; // exponent, infinity, NaN
            }
            return text[minus.Length..];
        }
    }
}
