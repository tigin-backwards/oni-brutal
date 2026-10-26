// Port of _math_matrix_frustum from Mesa's src/mesa/math/m_matrix.c (Mesa 20.3.5):
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

namespace UnityEngine
{
    /// The original player's Matrix4x4.Frustum gives the results of Mesa's frustum matrix bit
    /// for bit (measured: tools/UnityProbe PROBE_SCENARIO=projection, pj-check.py; 417 of 417
    /// boxes, degenerate ones included: equal planes, zero sizes, NaN, infinity). Every operation
    /// rounds to float, in the C's order; the C multiplies the result into the current matrix,
    /// here the identity.
    internal static class MesaFrustum
    {
        public static Matrix4x4 Frustum(float left, float right, float bottom, float top, float nearval, float farval)
        {
            float x = (2f * nearval) / (right - left);
            float y = (2f * nearval) / (top - bottom);
            float a = (right + left) / (right - left);
            float b = (top + bottom) / (top - bottom);
            float c = -(farval + nearval) / (farval - nearval);
            float d = -(2f * farval * nearval) / (farval - nearval);
            Matrix4x4 m = default;
            m.m00 = x; m.m02 = a;
            m.m11 = y; m.m12 = b;
            m.m22 = c; m.m23 = d;
            m.m32 = -1f;
            return m;
        }
    }
}
