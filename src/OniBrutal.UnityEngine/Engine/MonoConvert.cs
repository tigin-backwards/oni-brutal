using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace OniBrutal.Engine
{
    /// Float-to-integer conversions as Unity's Mono gives them on x86-64 (ARCHITECTURE D29).
    /// Mono converts with the processor's truncating conversion (cvttsd2si), which gives the
    /// "integer indefinite" value, the target's minimum, for NaN, the infinities and anything
    /// whose truncation is out of range; CoreCLR (since .NET 9) saturates instead, and NaN gives
    /// 0. Measured in the original player (tools/UnityProbe, `PROBE_SCENARIO=convert`), every IL
    /// conversion of a float32 or float64 follows these rules, float32 values first widened to
    /// float64 exactly:
    /// - conv.i4: the 32-bit conversion; conv.i1, conv.i2, conv.u1 and conv.u2 take the low bits of
    ///   that result (300 gives 44 as a byte, NaN 0);
    /// - conv.i8: the 64-bit conversion; conv.u4 takes the low 32 bits of that (-1 gives
    ///   4294967295, NaN 0);
    /// - conv.u8 and conv.u: below 2^63 the 64-bit conversion, otherwise the 64-bit conversion of
    ///   the value less 2^63 with the top bit flipped (NaN, +∞ and 2^64 give 0, -∞ gives 2^63);
    /// - conv.i: the 32-bit conversion zero-extended (Mono's amd64 back end converts to 32 bits),
    ///   so -1 gives 4294967295 and NaN 2147483648.
    /// The checked conversions (conv.ovf.*) and Convert.ToXxx throw where .NET's do and agree
    /// otherwise.
    internal static class MonoConvert
    {
        const double Two31 = 2147483648.0, Two63 = 9223372036854775808.0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32(double x)
        {
            if (Sse2.IsSupported) return Sse2.ConvertToInt32WithTruncation(Vector128.CreateScalarUnsafe(x));
            return ToInt32Portable(x);
        }

        /// The same without x86 instructions (an arm64 process).
        internal static int ToInt32Portable(double x) => x > -Two31 - 1.0 && x < Two31 ? (int)x : int.MinValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ToInt64(double x)
        {
            if (Sse2.X64.IsSupported) return Sse2.X64.ConvertToInt64WithTruncation(Vector128.CreateScalarUnsafe(x));
            return ToInt64Portable(x);
        }

        /// The same without x86 instructions. The doubles next to -2^63 are 2048 apart: the one
        /// below it truncates out of range.
        internal static long ToInt64Portable(double x) => x >= -Two63 && x < Two63 ? (long)x : long.MinValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte ToSByte(double x) => (sbyte)ToInt32(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ToInt16(double x) => (short)ToInt32(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ToByte(double x) => (byte)ToInt32(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ToUInt16(double x) => (ushort)ToInt32(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToUInt32(double x) => (uint)ToInt64(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ToUInt64(double x) =>
            x < Two63 ? (ulong)ToInt64(x) : (ulong)ToInt64(x - Two63) ^ 0x8000000000000000UL;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint ToIntPtr(double x) => (nint)(uint)ToInt32(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint ToUIntPtr(double x) => (nuint)ToUInt64(x);
    }
}
