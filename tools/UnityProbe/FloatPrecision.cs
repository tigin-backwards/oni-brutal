// Float precision (lines "VAL fp ..."): where the player's JIT rounds float (R4) arithmetic to
// float32 and where it keeps the double it computed in. Each case's inputs are chosen so that
// float32 rounding and double evaluation give different results; the line prints the result,
// both references and which one it matched ("f32", "f64", "other"; "same" when they agree).
// The inputs are read from static fields set at run time, so neither the compiler nor the JIT
// can fold them.
//
// Run alone with PROBE_SCENARIO=floatprec (at mod load); the Values run includes it too.
//
// Measured 2026-10-23 (U59 on Unity 6000.3.5f2, whose MonoBleedingEdge is 6.13.0
// explicit/ea2b0940; ours on .NET 10 CoreCLR), with the same DLL in both players:
//
// - The original keeps float arithmetic at double precision and rounds to float32 only where
//   a value is stored to a float32 place: a float local (address taken or not), a static,
//   instance or struct field, an array element, a float parameter (also of an inlined
//   method), a float return value (also inlined), a box, and an explicit (float) cast. A
//   float expression consumed directly keeps its double value: widened to double (implicit
//   or explicit), passed to a double parameter (Math.Round, Convert.ToString), truncated to
//   int, compared, or used as an operand of further arithmetic. Constants the JIT folds
//   (a literal local) fold at double precision too. Ours rounds after every operation.
// - So these differ (original / ours): Math.Round(0.9f*255f) 229 / 230;
//   (double)(0.9f*255f) 229.49999392032623 / 229.5; (int)(0.01f*100f) 0 / 1;
//   (int)(0.7f*10f) 6 / 7; (int)(0.5f/0.1f) 4 / 5; 0.7f*10f == 7f False / True;
//   0.7f*10f < 7f True / False; 0.01f*100f >= 1f False / True; (double)(0.7f*10f - 7f) and
//   a float local of it -1.1920929E-07 / 0; (double)(a*b+c) 1.0000001788139343 / 1.0000002;
//   Math.Round(Mathf.Clamp01(0.9f)*255f) 229 / 230 (the product after the call);
//   Mathf.Sqrt(2)*Mathf.Sqrt(2) 1.9999999315429164 / 1.9999998807907104; Unity's
//   Color32.Lerp(0, 100, 0.01f).r 0 / 1; E2eDriver's dump Math.Round(c.r*255) 229 / 230.
// - These agree (float32 in both): every store listed above; Mathf.RoundToInt, Mathf.Round
//   and Mathf.FloorToInt of a float expression (their parameter rounds it); Mathf.Lerp;
//   (a*255f).ToString("R") and "" + (a*255f); ten += 0.1f into a local or field
//   (1.0000001); the Color -> Color32 conversion (0.9f -> 230, 0.3f -> 76).
// - Math.Round(c.r*255d) is 229 in both: double arithmetic is the same on both runtimes.
// - A fused multiply-add appears in neither (a*b+c stored to a float is 1.0000002, not the
//   fused 1.0000001).
// - "R" of a double prints 17 digits in the original ("1.0000002384185791") and the shortest
//   form in ours ("1.000000238418579"): the same value (ARCHITECTURE D2).
//
// Why: UnityPlayer.so passes "-O=-float32" to mono_jit_parse_options (the string sits beside
// "Unity Root Domain"), turning off the float32 optimization that Mono's DEFAULT_OPTIMIZATIONS
// enables (mono/mini/driver.c). Without it cfg->r4fp is 0 (mini.c), R4 values are STACK_R8
// and R4 locals move with OP_FMOVE (method-to-ir.c); on amd64 a float32 load converts to
// double (OP_LOADR4_MEMBASE: cvtss2sd), arithmetic runs on doubles, and only stores convert
// back (OP_STORER4_MEMBASE_REG, OP_FCONV_TO_R4: cvtsd2ss) (mini-amd64.c). R4 variables are
// kept out of registers ("Putting R4 vars into registers doesn't work currently"), so every
// float local is a memory slot and rounds on store.
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SemanticsProbe
{
    public static class FloatPrecision
    {
        public static float A09, A07, A001, A03, K255, K100, K10, K7, K1, K01, K05, T01, CA, CB, CC, Two;
        static float sField;
        struct Pair { public float X; }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string R(double d) => d.ToString("R", Inv);

        static void P(string label, double got, double f32, double f64)
        {
            string v = f32.Equals(f64) ? "same" : got.Equals(f32) ? "f32" : got.Equals(f64) ? "f64" : "other";
            Rec.Log("VAL fp " + label + " = " + R(got) + " f32=" + R(f32) + " f64=" + R(f64) + " -> " + v);
        }

        static void B(string label, bool got, bool f32, bool f64)
        {
            string v = f32 == f64 ? "same" : got == f32 ? "f32" : "f64";
            Rec.Log("VAL fp " + label + " = " + got + " f32=" + f32 + " f64=" + f64 + " -> " + v);
        }

        static void S(string label, string got, string f32, string f64)
        {
            string v = f32 == f64 ? "same" : got == f32 ? "f32" : got == f64 ? "f64" : "other";
            Rec.Log("VAL fp " + label + " = " + got + " f32=" + f32 + " f64=" + f64 + " -> " + v);
        }

        [MethodImpl(MethodImplOptions.NoInlining)] static double DArg(double x) => x;
        [MethodImpl(MethodImplOptions.NoInlining)] static double FArg(float x) => x;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] static double FArgInline(float x) => x;
        [MethodImpl(MethodImplOptions.NoInlining)] static float FRet(float a, float b) => a * b;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] static float FRetInline(float a, float b) => a * b;
        [MethodImpl(MethodImplOptions.NoInlining)] static double Widen(float x) => x;
        [MethodImpl(MethodImplOptions.NoInlining)] static void Keep(ref float x) { }

        static void Init()
        {
            float F(string s) => float.Parse(s, Inv);
            A09 = F("0.9"); A07 = F("0.7"); A001 = F("0.01"); A03 = F("0.3");
            K255 = F("255"); K100 = F("100"); K10 = F("10"); K7 = F("7"); K1 = F("1"); K01 = F("0.1"); K05 = F("0.5");
            T01 = F("0.1"); Two = F("2");
            // a*b+c: the product rounded to float puts the sum on a tie (even: up), the exact sum is
            // just under it (fused: down).
            CA = F("1.00000381469726562"); CB = F("5.96044174017151818E-08"); CC = F("1.00000011920928955");
        }

        public static void Run()
        {
            Init();
            float a = A09, k = K255;
            double f32 = (float)(a * k), f64 = (double)a * k;

            // Rounding of the float product 0.9f * 255 (float32 229.5, double 229.49999392).
            P("Math.Round(a*255f)", Math.Round(a * k), Math.Round(f32), Math.Round(f64));
            P("(double)(a*255f)", (double)(a * k), f32, f64);
            P("(double)(float)(a*255f)", (double)(float)(a * k), f32, f64);
            P("double d = a*255f", Store(a, k), f32, f64);
            float local = a * k;
            P("float local; (double)local", local, f32, f64);
            float kept = a * k; Keep(ref kept);
            P("float local, address taken", kept, f32, f64);
            P("double param DArg(a*255f)", DArg(a * k), f32, f64);
            P("float param FArg(a*255f)", FArg(a * k), f32, f64);
            P("inlined float param FArgInline(a*255f)", FArgInline(a * k), f32, f64);
            P("float return FRet(a,255f)", FRet(a, k), f32, f64);
            P("inlined float return FRetInline(a,255f)", FRetInline(a, k), f32, f64);
            sField = a * k; P("static field", Widen(sField), f32, f64);
            sField = a * k; P("static field, read in place", sField, f32, f64);
            var o = new FloatPrecisionHolder(); o.Field = a * k; P("instance field", o.Field, f32, f64);
            var arr = new float[1]; arr[0] = a * k; P("array element", arr[0], f32, f64);
            var pair = new Pair(); pair.X = a * k; P("struct local field", pair.X, f32, f64);
            P("new Vector3(a*255f,0,0).x", new Vector3(a * k, 0f, 0f).x, f32, f64);
            object boxed = a * k; P("boxed float", (float)boxed, f32, f64);
            S("(a*255f).ToString(\"R\")", (a * k).ToString("R", Inv), ((float)f32).ToString("R", Inv), "229.49999");
            S("\"\" + (a*255f)", "" + (a * k), ((float)f32).ToString(Inv), "229.49999");
            S("Convert.ToString((double)(a*255f))", Convert.ToString((double)(a * k), Inv), Convert.ToString(f32, Inv), Convert.ToString(f64, Inv));

            // Truncation to int: 0.01f * 100 (float32 1, double 0.99999998).
            float p = A001, h = K100;
            P("(int)(0.01f*100f)", (int)(p * h), (int)(float)(p * h), (int)((double)p * h));
            P("Mathf.FloorToInt(0.01f*100f)", Mathf.FloorToInt(p * h), Mathf.FloorToInt((float)(p * h)), Math.Floor((double)p * h));
            P("Mathf.RoundToInt(0.9f*255f)", Mathf.RoundToInt(a * k), Math.Round(f32), Math.Round(f64));
            P("Mathf.Round(0.9f*255f)", Mathf.Round(a * k), Math.Round(f32), Math.Round(f64));
            P("(int)(0.7f*10f)", (int)(A07 * K10), (int)(float)(A07 * K10), (int)((double)A07 * K10));
            P("(int)(0.5f/0.1f)", (int)(K05 / K01), (int)(float)(K05 / K01), (int)((double)K05 / K01));

            // Comparisons at a threshold.
            B("0.7f*10f == 7f", A07 * K10 == K7, (float)(A07 * K10) == K7, (double)A07 * K10 == K7);
            B("0.7f*10f < 7f", A07 * K10 < K7, (float)(A07 * K10) < K7, (double)A07 * K10 < K7);
            B("0.01f*100f >= 1f", p * h >= K1, (float)(p * h) >= K1, (double)p * h >= K1);
            float cmp = A07 * K10;
            B("float local == 7f", cmp == K7, (float)(A07 * K10) == K7, (double)A07 * K10 == K7);

            // Compound expressions.
            double c32 = (float)((float)(A07 * K10) - K7), c64 = (double)A07 * K10 - K7;
            P("(double)(0.7f*10f - 7f)", (double)(A07 * K10 - K7), c32, c64);
            float cl = A07 * K10 - K7;
            P("float local = 0.7f*10f - 7f", cl, c32, (float)c64);
            double m32 = (float)((float)(CA * CB) + CC), m64 = (double)CA * CB + CC;
            P("(double)(a*b+c)", (double)(CA * CB + CC), m32, m64);
            float ml = CA * CB + CC;
            // The fused result (one rounding of the exact a*b+c) is CC, 1.00000011920928955.
            P("float local = a*b+c (fused " + R(CC) + ")", ml, m32, (float)m64);

            // Accumulation: ten additions of 0.1f into a float local and into a field.
            float sum = 0f;
            for (int i = 0; i < 10; i++) sum += T01;
            double s32 = 0f, s64 = 0;
            { float t = 0f; for (int i = 0; i < 10; i++) t = (float)(t + T01); s32 = t; }
            for (int i = 0; i < 10; i++) s64 += T01;
            P("float local += 0.1f x10", sum, s32, (float)s64);
            sField = 0f;
            for (int i = 0; i < 10; i++) sField += T01;
            P("static field += 0.1f x10", Widen(sField), s32, (float)s64);
            float wide = 0f;
            for (int i = 0; i < 10; i++) wide = wide + T01 * K1;
            P("(double)(sum*1f) after x10", (double)(wide * K1), s32, (float)s64);

            // Mathf and engine conversions game code goes through.
            P("Mathf.Lerp(0,255,0.9f)", Mathf.Lerp(0f, k, a), f32, f64);
            P("Math.Round(Mathf.Lerp(0,255,0.9f))", Math.Round(Mathf.Lerp(0f, k, a)), Math.Round(f32), Math.Round(f64));
            P("Math.Round(Mathf.Clamp01(0.9f)*255f)", Math.Round(Mathf.Clamp01(a) * k), Math.Round(f32), Math.Round(f64));
            P("Math.Round(Mathf.Max(0.9f,0f)*255f)", Math.Round(Mathf.Max(a, 0f) * k), Math.Round(f32), Math.Round(f64));
            double q32 = (float)((float)Math.Sqrt(Two) * (float)Math.Sqrt(Two)), q64 = (double)(float)Math.Sqrt(Two) * (float)Math.Sqrt(Two);
            P("(double)(Mathf.Sqrt(2)*Mathf.Sqrt(2))", (double)(Mathf.Sqrt(Two) * Mathf.Sqrt(Two)), q32, q64);
            var c32b = (Color32)new Color(a, A03, A07, K1);
            P("(Color32)Color(0.9f,..).r", c32b.r, Math.Round(f32), Math.Round(f64));
            P("(Color32)Color(..,0.3f,..).g", c32b.g, Math.Round((float)(A03 * k)), Math.Round((double)A03 * k));
            var lerp = Color32.Lerp(new Color32(0, 0, 0, 0), new Color32(100, 100, 100, 100), p);
            P("Color32.Lerp(0,100,0.01f).r", lerp.r, (byte)(float)(100 * p), (byte)(100 * (double)p));
            Color c = new Color(a, a, a, a);
            P("dump Math.Round(c.r*255)", Math.Round(c.r * 255), Math.Round(f32), Math.Round(f64));
            P("dump Math.Round(c.r*255d)", Math.Round(c.r * 255d), Math.Round(f64), Math.Round(f64));

            // Constants the JIT can see (a literal local): folding happens at the JIT's precision.
            float lit = 0.9f;
            P("literal local Math.Round(0.9f*255)", Math.Round(lit * 255), Math.Round(f32), Math.Round(f64));
            Rec.Log("=== values: float precision done");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static double Store(float a, float k)
        {
            double d = a * k;
            return d;
        }
    }

    public class FloatPrecisionHolder
    {
        public float Field;
    }
}
