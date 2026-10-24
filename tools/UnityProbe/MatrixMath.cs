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
            Rec.Log("MM done");
        }
    }
}
