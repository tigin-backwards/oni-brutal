// Float-to-integer conversions (lines "CV ..."): what the player's runtime gives for NaN,
// infinities and values outside the target's range, for every IL conversion of a float32 and a
// float64 (conv.i1 to conv.u8, conv.i, conv.u, their checked forms conv.ovf.*), for Convert's
// ToXxx and for Unity's own conversions (Mathf.RoundToInt/FloorToInt/CeilToInt, Vector2Int and
// Vector3Int's, Color32 from Color).
//
// Each line is "CV <source> <operation> <input> = <result>", the input in "R" form with its
// bits, the result in decimal (an exception by its type name). The inputs are read from a static
// array, and every conversion sits in a method of its own that the JIT may not inline, so
// neither the compiler nor either JIT folds a constant. A second pass does the same conversions
// inline in a loop (source "f32i"/"f64i"), where the value is never stored to a float local.
//
// Run alone with PROBE_SCENARIO=convert (at mod load).
//
// Measured 2026-10-24 (U57 original on Unity's MonoBleedingEdge, amd64; ours on .NET 10
// CoreCLR), with the same DLL in both players: see docs/ARCHITECTURE.md D29 for the rules.
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SemanticsProbe
{
    public static class Conversions
    {
        public static double[] Inputs =
        {
            double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.0, 0.5, -0.5, -1, -1.5,
            127.5, 128, 200, 255.9, 256, 300, -128.5, -129, -200, -300,
            32767.5, 32768, 40000, 65535.5, 65536, 70000, -32768.5, -32769, -40000, -70000,
            2147483520, 2147483647.9, 2147483648, 3e9, 4294967295.5, 4294967296, 5e9, 1e10,
            -2147483648, -2147483648.9, -2147483649, -3e9, -4294967296, -5e9, -1e10,
            9.2e18, 9223372036854775807, 1e19, 18446744073709551615.0, 2e19, -9.2e18, -9.3e18, -2e19,
            1e30, -1e30, 3.4028234663852886e38, -3.4028234663852886e38, double.MaxValue, double.MinValue,
        };

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string In(double d) => d.ToString("R", Inv) + "(" + BitConverter.DoubleToInt64Bits(d).ToString("X16") + ")";
        static string In(float f) => f.ToString("R", Inv) + "(" + BitConverter.ToInt32(BitConverter.GetBytes(f), 0).ToString("X8") + ")";

        static void Line(string source, string op, string input, Func<string> result)
        {
            string r;
            try { r = result(); }
            catch (Exception e) { r = e.GetType().Name; }
            Rec.Log("CV " + source + " " + op + " " + input + " = " + r);
        }

        public static void Run()
        {
            foreach (double d in Inputs)
            {
                string i = In(d);
                Line("f64", "conv.i1", i, () => D_I1(d).ToString(Inv));
                Line("f64", "conv.i2", i, () => D_I2(d).ToString(Inv));
                Line("f64", "conv.i4", i, () => D_I4(d).ToString(Inv));
                Line("f64", "conv.i8", i, () => D_I8(d).ToString(Inv));
                Line("f64", "conv.u1", i, () => D_U1(d).ToString(Inv));
                Line("f64", "conv.u2", i, () => ((int)D_U2(d)).ToString(Inv));
                Line("f64", "conv.u4", i, () => D_U4(d).ToString(Inv));
                Line("f64", "conv.u8", i, () => D_U8(d).ToString(Inv));
                Line("f64", "conv.i", i, () => D_I(d).ToString(Inv));
                Line("f64", "conv.u", i, () => ((ulong)D_U(d)).ToString(Inv));
                Line("f64", "conv.ovf.i1", i, () => D_OI1(d).ToString(Inv));
                Line("f64", "conv.ovf.i2", i, () => D_OI2(d).ToString(Inv));
                Line("f64", "conv.ovf.i4", i, () => D_OI4(d).ToString(Inv));
                Line("f64", "conv.ovf.i8", i, () => D_OI8(d).ToString(Inv));
                Line("f64", "conv.ovf.u1", i, () => D_OU1(d).ToString(Inv));
                Line("f64", "conv.ovf.u2", i, () => D_OU2(d).ToString(Inv));
                Line("f64", "conv.ovf.u4", i, () => D_OU4(d).ToString(Inv));
                Line("f64", "conv.ovf.u8", i, () => D_OU8(d).ToString(Inv));
                Line("f64", "Convert.ToInt32", i, () => Convert.ToInt32(d).ToString(Inv));
                Line("f64", "Convert.ToInt64", i, () => Convert.ToInt64(d).ToString(Inv));
                Line("f64", "Convert.ToUInt32", i, () => Convert.ToUInt32(d).ToString(Inv));
                Line("f64", "Convert.ToInt16", i, () => Convert.ToInt16(d).ToString(Inv));
                Line("f64", "Convert.ToByte", i, () => Convert.ToByte(d).ToString(Inv));
                Line("f64", "Math.Round>conv.i4", i, () => ((int)Math.Round(d)).ToString(Inv));
                Line("f64", "Math.Floor>conv.i4", i, () => ((int)Math.Floor(d)).ToString(Inv));

                float f = (float)d;
                string j = In(f);
                Line("f32", "conv.i1", j, () => F_I1(f).ToString(Inv));
                Line("f32", "conv.i2", j, () => F_I2(f).ToString(Inv));
                Line("f32", "conv.i4", j, () => F_I4(f).ToString(Inv));
                Line("f32", "conv.i8", j, () => F_I8(f).ToString(Inv));
                Line("f32", "conv.u1", j, () => F_U1(f).ToString(Inv));
                Line("f32", "conv.u2", j, () => ((int)F_U2(f)).ToString(Inv));
                Line("f32", "conv.u4", j, () => F_U4(f).ToString(Inv));
                Line("f32", "conv.u8", j, () => F_U8(f).ToString(Inv));
                Line("f32", "conv.i", j, () => F_I(f).ToString(Inv));
                Line("f32", "conv.u", j, () => ((ulong)F_U(f)).ToString(Inv));
                Line("f32", "conv.ovf.i1", j, () => F_OI1(f).ToString(Inv));
                Line("f32", "conv.ovf.i2", j, () => F_OI2(f).ToString(Inv));
                Line("f32", "conv.ovf.i4", j, () => F_OI4(f).ToString(Inv));
                Line("f32", "conv.ovf.i8", j, () => F_OI8(f).ToString(Inv));
                Line("f32", "conv.ovf.u1", j, () => F_OU1(f).ToString(Inv));
                Line("f32", "conv.ovf.u2", j, () => F_OU2(f).ToString(Inv));
                Line("f32", "conv.ovf.u4", j, () => F_OU4(f).ToString(Inv));
                Line("f32", "conv.ovf.u8", j, () => F_OU8(f).ToString(Inv));
                Line("f32", "Convert.ToInt32", j, () => Convert.ToInt32(f).ToString(Inv));
                Line("f32", "Convert.ToInt64", j, () => Convert.ToInt64(f).ToString(Inv));
                Line("f32", "Convert.ToUInt32", j, () => Convert.ToUInt32(f).ToString(Inv));
                Line("f32", "Mathf.RoundToInt", j, () => Mathf.RoundToInt(f).ToString(Inv));
                Line("f32", "Mathf.FloorToInt", j, () => Mathf.FloorToInt(f).ToString(Inv));
                Line("f32", "Mathf.CeilToInt", j, () => Mathf.CeilToInt(f).ToString(Inv));
                Line("f32", "Mathf.Round", j, () => In(Mathf.Round(f)));
                Line("f32", "Mathf.Clamp01", j, () => In(Mathf.Clamp01(f)));
                Line("f32", "Vector2Int.RoundToInt", j, () => Vector2Int.RoundToInt(new Vector2(f, 1.5f)).ToString());
                Line("f32", "Vector2Int.FloorToInt", j, () => Vector2Int.FloorToInt(new Vector2(f, 1.5f)).ToString());
                Line("f32", "Vector2Int.CeilToInt", j, () => Vector2Int.CeilToInt(new Vector2(f, 1.5f)).ToString());
                Line("f32", "Vector3Int.RoundToInt", j, () => Vector3Int.RoundToInt(new Vector3(f, 1.5f, -1.5f)).ToString());
                Line("f32", "Vector3Int.FloorToInt", j, () => Vector3Int.FloorToInt(new Vector3(f, 1.5f, -1.5f)).ToString());
                Line("f32", "Vector3Int.CeilToInt", j, () => Vector3Int.CeilToInt(new Vector3(f, 1.5f, -1.5f)).ToString());
                Line("f32", "Color32", j, () => { Color32 c = new Color(f, 0.5f, 1f, f); return c.r + "," + c.g + "," + c.b + "," + c.a; });
            }
            // The same conversions inline, the value never stored to a float local first.
            float[] floats = new float[Inputs.Length];
            for (int k = 0; k < Inputs.Length; k++) floats[k] = (float)Inputs[k];
            for (int k = 0; k < Inputs.Length; k++)
            {
                Rec.Log("CV f64i conv " + In(Inputs[k]) + " = " + (int)Inputs[k] + " " + (long)Inputs[k] + " " + (uint)Inputs[k] + " " + (ulong)Inputs[k] + " " + (short)Inputs[k] + " " + (byte)Inputs[k]);
                Rec.Log("CV f32i conv " + In(floats[k]) + " = " + (int)floats[k] + " " + (long)floats[k] + " " + (uint)floats[k] + " " + (ulong)floats[k] + " " + (short)floats[k] + " " + (byte)floats[k]);
                Rec.Log("CV f32i product " + In(floats[k]) + " = " + (int)(floats[k] * One) + " " + (long)(floats[k] * One) + " " + (uint)(floats[k] * One));
            }
            Rec.Log("CV done");
        }

        public static float One = 1f;

        [MethodImpl(MethodImplOptions.NoInlining)] static sbyte D_I1(double x) => (sbyte)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static short D_I2(double x) => (short)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static int D_I4(double x) => (int)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static long D_I8(double x) => (long)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static byte D_U1(double x) => (byte)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static char D_U2(double x) => (char)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static uint D_U4(double x) => (uint)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static ulong D_U8(double x) => (ulong)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static long D_I(double x) => (long)(nint)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static nuint D_U(double x) => (nuint)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static sbyte D_OI1(double x) => checked((sbyte)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static short D_OI2(double x) => checked((short)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static int D_OI4(double x) => checked((int)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static long D_OI8(double x) => checked((long)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static byte D_OU1(double x) => checked((byte)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static ushort D_OU2(double x) => checked((ushort)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static uint D_OU4(double x) => checked((uint)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static ulong D_OU8(double x) => checked((ulong)x);

        [MethodImpl(MethodImplOptions.NoInlining)] static sbyte F_I1(float x) => (sbyte)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static short F_I2(float x) => (short)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static int F_I4(float x) => (int)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static long F_I8(float x) => (long)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static byte F_U1(float x) => (byte)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static char F_U2(float x) => (char)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static uint F_U4(float x) => (uint)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static ulong F_U8(float x) => (ulong)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static long F_I(float x) => (long)(nint)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static nuint F_U(float x) => (nuint)x;
        [MethodImpl(MethodImplOptions.NoInlining)] static sbyte F_OI1(float x) => checked((sbyte)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static short F_OI2(float x) => checked((short)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static int F_OI4(float x) => checked((int)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static long F_OI8(float x) => checked((long)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static byte F_OU1(float x) => checked((byte)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static ushort F_OU2(float x) => checked((ushort)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static uint F_OU4(float x) => checked((uint)x);
        [MethodImpl(MethodImplOptions.NoInlining)] static ulong F_OU8(float x) => checked((ulong)x);
    }
}
