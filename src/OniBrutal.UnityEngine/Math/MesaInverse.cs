// Port of invert_matrix_general from Mesa's src/mesa/math/m_matrix.c (Mesa 20.3.5, the last
// release series with it in that file; code contributed to Mesa by Jacques Leroy):
//
// Mesa 3-D graphics library
//
// Copyright (C) 1999-2005  Brian Paul   All Rights Reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR
// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Runtime.CompilerServices;

namespace UnityEngine
{
    /// The original player's Matrix4x4.inverse gives the results of Mesa's general inversion
    /// bit for bit (measured: tools/UnityProbe PROBE_SCENARIO=matmul, mm-check.py; 400 of 400
    /// random matrices, half of them affine): Gauss–Jordan elimination with partial pivoting,
    /// every operation rounded to float. The C's operation order, pivot choice and zero tests
    /// are kept; CoreCLR rounds each float operation (ARCHITECTURE D25) and contracts none into
    /// a fused multiply-add, as the native code does.
    internal static unsafe class MesaInverse
    {
        /// Inverts m into result; false (result untouched) when a pivot is zero, where the C
        /// returns GL_FALSE.
        [SkipLocalsInit] // every element of wtmp is written before it is read
        public static bool Invert(in Matrix4x4 m, out Matrix4x4 result)
        {
            float* wtmp = stackalloc float[32];
            float m0, m1, m2, m3, s;
            float* r0 = wtmp, r1 = wtmp + 8, r2 = wtmp + 16, r3 = wtmp + 24;
            result = default;

            r0[0] = m.m00; r0[1] = m.m01; r0[2] = m.m02; r0[3] = m.m03;
            r0[4] = 1f; r0[5] = r0[6] = r0[7] = 0f;
            r1[0] = m.m10; r1[1] = m.m11; r1[2] = m.m12; r1[3] = m.m13;
            r1[5] = 1f; r1[4] = r1[6] = r1[7] = 0f;
            r2[0] = m.m20; r2[1] = m.m21; r2[2] = m.m22; r2[3] = m.m23;
            r2[6] = 1f; r2[4] = r2[5] = r2[7] = 0f;
            r3[0] = m.m30; r3[1] = m.m31; r3[2] = m.m32; r3[3] = m.m33;
            r3[7] = 1f; r3[4] = r3[5] = r3[6] = 0f;

            // choose pivot - or die
            if (MathF.Abs(r3[0]) > MathF.Abs(r2[0])) Swap(ref r3, ref r2);
            if (MathF.Abs(r2[0]) > MathF.Abs(r1[0])) Swap(ref r2, ref r1);
            if (MathF.Abs(r1[0]) > MathF.Abs(r0[0])) Swap(ref r1, ref r0);
            if (0f == r0[0]) return false;

            // eliminate first variable
            m1 = r1[0] / r0[0]; m2 = r2[0] / r0[0]; m3 = r3[0] / r0[0];
            s = r0[1]; r1[1] -= m1 * s; r2[1] -= m2 * s; r3[1] -= m3 * s;
            s = r0[2]; r1[2] -= m1 * s; r2[2] -= m2 * s; r3[2] -= m3 * s;
            s = r0[3]; r1[3] -= m1 * s; r2[3] -= m2 * s; r3[3] -= m3 * s;
            s = r0[4];
            if (s != 0f) { r1[4] -= m1 * s; r2[4] -= m2 * s; r3[4] -= m3 * s; }
            s = r0[5];
            if (s != 0f) { r1[5] -= m1 * s; r2[5] -= m2 * s; r3[5] -= m3 * s; }
            s = r0[6];
            if (s != 0f) { r1[6] -= m1 * s; r2[6] -= m2 * s; r3[6] -= m3 * s; }
            s = r0[7];
            if (s != 0f) { r1[7] -= m1 * s; r2[7] -= m2 * s; r3[7] -= m3 * s; }

            // choose pivot - or die
            if (MathF.Abs(r3[1]) > MathF.Abs(r2[1])) Swap(ref r3, ref r2);
            if (MathF.Abs(r2[1]) > MathF.Abs(r1[1])) Swap(ref r2, ref r1);
            if (0f == r1[1]) return false;

            // eliminate second variable
            m2 = r2[1] / r1[1]; m3 = r3[1] / r1[1];
            r2[2] -= m2 * r1[2]; r3[2] -= m3 * r1[2];
            r2[3] -= m2 * r1[3]; r3[3] -= m3 * r1[3];
            s = r1[4]; if (0f != s) { r2[4] -= m2 * s; r3[4] -= m3 * s; }
            s = r1[5]; if (0f != s) { r2[5] -= m2 * s; r3[5] -= m3 * s; }
            s = r1[6]; if (0f != s) { r2[6] -= m2 * s; r3[6] -= m3 * s; }
            s = r1[7]; if (0f != s) { r2[7] -= m2 * s; r3[7] -= m3 * s; }

            // choose pivot - or die
            if (MathF.Abs(r3[2]) > MathF.Abs(r2[2])) Swap(ref r3, ref r2);
            if (0f == r2[2]) return false;

            // eliminate third variable
            m3 = r3[2] / r2[2];
            r3[3] -= m3 * r2[3]; r3[4] -= m3 * r2[4];
            r3[5] -= m3 * r2[5]; r3[6] -= m3 * r2[6];
            r3[7] -= m3 * r2[7];

            // last check
            if (0f == r3[3]) return false;

            s = 1f / r3[3]; // now back substitute row 3
            r3[4] *= s; r3[5] *= s; r3[6] *= s; r3[7] *= s;

            m2 = r2[3]; // now back substitute row 2
            s = 1f / r2[2];
            r2[4] = s * (r2[4] - r3[4] * m2); r2[5] = s * (r2[5] - r3[5] * m2);
            r2[6] = s * (r2[6] - r3[6] * m2); r2[7] = s * (r2[7] - r3[7] * m2);
            m1 = r1[3];
            r1[4] -= r3[4] * m1; r1[5] -= r3[5] * m1;
            r1[6] -= r3[6] * m1; r1[7] -= r3[7] * m1;
            m0 = r0[3];
            r0[4] -= r3[4] * m0; r0[5] -= r3[5] * m0;
            r0[6] -= r3[6] * m0; r0[7] -= r3[7] * m0;

            m1 = r1[2]; // now back substitute row 1
            s = 1f / r1[1];
            r1[4] = s * (r1[4] - r2[4] * m1); r1[5] = s * (r1[5] - r2[5] * m1);
            r1[6] = s * (r1[6] - r2[6] * m1); r1[7] = s * (r1[7] - r2[7] * m1);
            m0 = r0[2];
            r0[4] -= r2[4] * m0; r0[5] -= r2[5] * m0;
            r0[6] -= r2[6] * m0; r0[7] -= r2[7] * m0;

            m0 = r0[1]; // now back substitute row 0
            s = 1f / r0[0];
            r0[4] = s * (r0[4] - r1[4] * m0); r0[5] = s * (r0[5] - r1[5] * m0);
            r0[6] = s * (r0[6] - r1[6] * m0); r0[7] = s * (r0[7] - r1[7] * m0);

            result.m00 = r0[4]; result.m01 = r0[5]; result.m02 = r0[6]; result.m03 = r0[7];
            result.m10 = r1[4]; result.m11 = r1[5]; result.m12 = r1[6]; result.m13 = r1[7];
            result.m20 = r2[4]; result.m21 = r2[5]; result.m22 = r2[6]; result.m23 = r2[7];
            result.m30 = r3[4]; result.m31 = r3[5]; result.m32 = r3[6]; result.m33 = r3[7];
            return true;
        }

        static void Swap(ref float* a, ref float* b)
        {
            float* t = a;
            a = b;
            b = t;
        }
    }
}
