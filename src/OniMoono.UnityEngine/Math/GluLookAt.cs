// The basis of gluLookAt (and its normalize and cross helpers) from GLU's src/libutil/project.c
// (GLU 9.0.3, the release tarball at archive.mesa3d.org):
//
// SGI FREE SOFTWARE LICENSE B (Version 2.0, Sept. 18, 2008)
// Copyright (C) 1991-2000 Silicon Graphics, Inc. All Rights Reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice including the dates of first publication and
// either this permission notice or a reference to
// http://oss.sgi.com/projects/FreeB/
// shall be included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// SILICON GRAPHICS, INC. BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF
// OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
// Except as contained in this notice, the name of Silicon Graphics, Inc.
// shall not be used in advertising or otherwise to promote the sale, use or
// other dealings in this Software without prior written authorization from
// Silicon Graphics, Inc.

using System;

namespace UnityEngine
{
    /// The original player's Matrix4x4.LookAt builds its basis with gluLookAt's float arithmetic
    /// (measured: tools/UnityProbe PROBE_SCENARIO=projection, pj-check.py; 473 of 473 cases, edge
    /// cases and the fallback boundaries included): forward normalized as GLU's normalize does
    /// (r = √(x² + y² + z²) summed left to right, each component divided by r), the side vector
    /// as GLU's cross product and normalized, the up vector as their cross product. Where GLU
    /// takes forward × up and up = side × forward for a matrix looking down −z, Unity's matrix
    /// is the inverse, looking down +z from `from`: its columns are right = up × forward, up =
    /// forward × right and forward, its translation is `from`. (The operand order decides the
    /// sign of zero elements, so it is Unity's.) Unity falls back to an identity rotation where
    /// GLU would divide by a tiny length: when the forward vector's length or the right vector's
    /// length before normalizing is under 1e-5 (NaN passes through).
    internal static class GluLookAt
    {
        const float Epsilon = 1e-5f;

        public static Matrix4x4 LookAt(Vector3 from, Vector3 to, Vector3 up)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m03 = from.x; m.m13 = from.y; m.m23 = from.z;
            float fx = to.x - from.x, fy = to.y - from.y, fz = to.z - from.z;
            float r = Length(fx, fy, fz);
            if (r < Epsilon) return m;
            fx /= r; fy /= r; fz /= r;
            Cross(up.x, up.y, up.z, fx, fy, fz, out float sx, out float sy, out float sz);
            r = Length(sx, sy, sz);
            if (r < Epsilon) return m;
            sx /= r; sy /= r; sz /= r;
            Cross(fx, fy, fz, sx, sy, sz, out float ux, out float uy, out float uz);
            m.m00 = sx; m.m10 = sy; m.m20 = sz;
            m.m01 = ux; m.m11 = uy; m.m21 = uz;
            m.m02 = fx; m.m12 = fy; m.m22 = fz;
            return m;
        }

        static float Length(float x, float y, float z) => MathF.Sqrt(x * x + y * y + z * z);

        static void Cross(float ax, float ay, float az, float bx, float by, float bz, out float x, out float y, out float z)
        {
            x = ay * bz - az * by;
            y = az * bx - ax * bz;
            z = ax * by - ay * bx;
        }
    }
}
