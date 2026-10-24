// Matrix4x4's products to the bit (lines "PROBE ... MM ..."), at mod load. Found with Resize Dup
// UI (Workshop 2549295369): ScrollRect maps its content's world corners into the viewport's space
// with worldToLocalMatrix.MultiplyPoint3x4, and whether the bounds of a 16 px and a 208 px content
// come out equal decides whether the scrollbar is updated again. From the original's logged
// inputs, its bounds are reproduced when each component is evaluated in double and rounded to
// float once (D25: Mono runs with float32 off), and never when every operation rounds.
//
// Each case draws a matrix and a vector from a seeded System.Random (a mix of magnitudes, signs
// and fractions, so that one rounding and many differ often) and logs, as float bits in hex:
// the inputs, MultiplyPoint3x4, MultiplyPoint, MultiplyVector, the matrix times a Vector4 and
// times a second matrix, and inverse. tools/UnityProbe/mm-check.py compares them with candidate
// formulas. Run alone with PROBE_SCENARIO=matmul.
using System;
using System.Text;
using UnityEngine;

namespace SemanticsProbe
{
    public static class MatrixMath
    {
        static string H(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0).ToString("x8");
        static string H(Vector3 v) => H(v.x) + "," + H(v.y) + "," + H(v.z);
        static string H(Vector4 v) => H(v.x) + "," + H(v.y) + "," + H(v.z) + "," + H(v.w);

        static string H(Matrix4x4 m)
        {
            var sb = new StringBuilder();
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    sb.Append(r == 0 && c == 0 ? "" : ",").Append(H(m[r, c]));
            return sb.ToString();
        }

        static float Draw(System.Random rng)
        {
            double mag = Math.Pow(10, rng.Next(-2, 4));
            double v = (rng.NextDouble() * 2 - 1) * mag;
            return (float)v;
        }

        static Matrix4x4 DrawMatrix(System.Random rng, bool affine)
        {
            var m = new Matrix4x4();
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    m[r, c] = Draw(rng);
            if (affine)
            {
                m[3, 0] = 0f; m[3, 1] = 0f; m[3, 2] = 0f; m[3, 3] = 1f;
            }
            return m;
        }

        static Matrix4x4 Rows(params float[] v)
        {
            var m = new Matrix4x4();
            for (int i = 0; i < 16; i++)
                m[i / 4, i % 4] = v[i];
            return m;
        }

        // Matrices the inversion finds singular or nearly so (lines "MMS"): a zero pivot at each
        // of the four elimination steps, dependent rows that cancel exactly or not, a zero scale
        // in a TRS, tiny, huge and subnormal scales, NaN and infinity, and negative zeros. Each
        // logs the matrix, inverse, Matrix4x4.Inverse and determinant.
        static void Singular()
        {
            float nan = float.NaN, inf = float.PositiveInfinity;
            var cases = new[]
            {
                Matrix4x4.zero,
                Rows(0, 1, 2, 3, 0, 5, 6, 7, 0, 9, 10, 11, 0, 13, 14, 15),
                Rows(1, 2, 3, 4, 2, 4, 6, 8, 5, 6, 7, 8, 9, 10, 11, 13),
                Rows(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16),
                Rows(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1),
                Rows(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1),
                Rows(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0),
                Rows(2, 0, 0, 5, 0, 3, 0, 6, 0, 0, 0, 7, 0, 0, 0, 1),
                Rows(1, 2, 3, 0, 4, 5, 6, 0, 7, 8, 9, 0, 0, 0, 0, 1),
                Rows(0.1f, 0.2f, 0.3f, 1, 0.4f, 0.5f, 0.6f, 2, 0.7f, 0.8f, 0.9f, 3, 0, 0, 0, 1),
                Rows(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1),
                Rows(0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 0),
                Rows(0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0),
                Matrix4x4.TRS(new Vector3(3, 4, 5), Quaternion.Euler(10, 20, 30), new Vector3(1, 0, 1)),
                Matrix4x4.TRS(new Vector3(3, 4, 5), Quaternion.identity, new Vector3(2, 3, 0)),
                Matrix4x4.TRS(new Vector3(3, 4, 5), Quaternion.Euler(0, 0, 45), new Vector3(0, 0, 0)),
                Matrix4x4.Scale(new Vector3(1e-20f, 1e-20f, 1e-20f)),
                Matrix4x4.Scale(new Vector3(1e-30f, 1e-30f, 1e-30f)),
                Matrix4x4.Scale(new Vector3(1e-40f, 1, 1)),
                Matrix4x4.Scale(new Vector3(1e-45f, 1e-45f, 1e-45f)),
                Matrix4x4.Scale(new Vector3(1e30f, 1e30f, 1e30f)),
                Matrix4x4.Scale(new Vector3(1e38f, 3e38f, 1)),
                Matrix4x4.Scale(new Vector3(-0f, 1, 1)),
                Rows(-0f, 0, 0, 0, 0, -0f, 0, 0, 0, 0, -0f, 0, 0, 0, 0, -0f),
                Rows(1, 2, 3, 4, 2, 4.000001f, 6, 8, 5, 6, 7, 8, 9, 10, 11, 13),
                Rows(1e-7f, 0, 0, 0, 0, 1e-7f, 0, 0, 0, 0, 1e-7f, 0, 0, 0, 0, 1e-7f),
                Rows(nan, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1),
                Rows(1, 0, 0, 0, 0, nan, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1),
                Rows(inf, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1),
                Rows(1, 0, 0, inf, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1),
                Rows(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, -0f),
                Rows(0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0),
                Matrix4x4.Perspective(60, 1.5f, 0.3f, 1000f),
                Matrix4x4.Ortho(-10, 10, -5, 5, 0.3f, 1000f),
                Matrix4x4.Perspective(60, 1.5f, 1f, 1f),
            };
            for (int i = 0; i < cases.Length; i++)
            {
                var m = cases[i];
                Rec.Log("MMS " + i + " m=" + H(m) + " inv=" + H(m.inverse) + " sinv=" + H(Matrix4x4.Inverse(m))
                    + " det=" + H(m.determinant));
            }
        }

        public static void Run()
        {
            var rng = new System.Random(20261024);
            for (int i = 0; i < 400; i++)
            {
                var m = DrawMatrix(rng, i % 2 == 0);
                var n = DrawMatrix(rng, i % 4 == 0);
                var p = new Vector3(Draw(rng), Draw(rng), Draw(rng));
                var q = new Vector4(Draw(rng), Draw(rng), Draw(rng), Draw(rng));
                Rec.Log("MM " + i + " m=" + H(m) + " n=" + H(n) + " p=" + H(p) + " q=" + H(q)
                    + " p3x4=" + H(m.MultiplyPoint3x4(p)) + " pt=" + H(m.MultiplyPoint(p)) + " vec=" + H(m.MultiplyVector(p))
                    + " mq=" + H(m * q) + " mn=" + H(m * n) + " inv=" + H(m.inverse));
            }
            Singular();
            Rec.Log("MM done");
        }
    }
}
