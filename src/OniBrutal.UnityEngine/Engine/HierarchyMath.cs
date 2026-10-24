using System;
using UnityEngine;

namespace OniBrutal.Engine
{
    /// The transform hierarchy's world matrix, rotation and lossy scale, in the original player's
    /// float operations and order (black-box fit to tools/UnityProbe TransformMath.cs, `transform`
    /// scenario: every logged level of 1,200 chains of 1 to 6 transforms and 400 of
    /// RectTransforms reproduced bit for bit, signed zeros included; docs/cleanroom/ugui.md,
    /// "Transform hierarchy to the bit").
    /// Every operation rounds to float.
    internal static class HierarchyMath
    {
        /// A 3×4 affine matrix, rows of (rotation-scale, translation).
        public struct Affine
        {
            public float M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23;

            public readonly Matrix4x4 ToMatrix()
            {
                Matrix4x4 m;
                m.m00 = M00; m.m01 = M01; m.m02 = M02; m.m03 = M03;
                m.m10 = M10; m.m11 = M11; m.m12 = M12; m.m13 = M13;
                m.m20 = M20; m.m21 = M21; m.m22 = M22; m.m23 = M23;
                m.m30 = 0f; m.m31 = 0f; m.m32 = 0f; m.m33 = 1f;
                return m;
            }
        }

        /// The local matrix T·R·S: each rotation column is the quaternion turning that basis
        /// vector (t = 2 q×e; e_i + w t_i + (q×t)_i, summed as below), times that axis's scale.
        public static Affine Trs(Vector3 p, Quaternion q, Vector3 s)
        {
            RotateBasis(q, 1f, 0f, 0f, out float a0, out float a1, out float a2);
            RotateBasis(q, 0f, 1f, 0f, out float b0, out float b1, out float b2);
            RotateBasis(q, 0f, 0f, 1f, out float c0, out float c1, out float c2);
            Affine m;
            m.M00 = a0 * s.x; m.M01 = b0 * s.y; m.M02 = c0 * s.z; m.M03 = p.x;
            m.M10 = a1 * s.x; m.M11 = b1 * s.y; m.M12 = c1 * s.z; m.M13 = p.y;
            m.M20 = a2 * s.x; m.M21 = b2 * s.y; m.M22 = c2 * s.z; m.M23 = p.z;
            return m;
        }

        static void RotateBasis(Quaternion q, float vx, float vy, float vz, out float rx, out float ry, out float rz)
        {
            float x = q.x, y = q.y, z = q.z, w = q.w;
            float tx = 2f * (y * vz - z * vy);
            float ty = 2f * (z * vx - x * vz);
            float tz = 2f * (x * vy - y * vx);
            rx = ((-(z * ty) + y * tz) + w * tx) + vx;
            ry = ((-(x * tz) + z * tx) + w * ty) + vy;
            rz = ((-(y * tx) + x * ty) + w * tz) + vz;
        }

        /// What Transform.localRotation's setter stores: the quaternion over its length, the x and
        /// z lanes' length summed (xx + yy) + (zz + ww), the y and w lanes' (xx + ww) + (yy + zz)
        /// (`TQN`, `TNS`: every logged set, unit-length inputs included). A length that is not a
        /// positive finite number (not measured) falls back to Quaternion.Normalize.
        public static Quaternion Normalize(Quaternion q)
        {
            float xx = q.x * q.x, yy = q.y * q.y, zz = q.z * q.z, ww = q.w * q.w;
            float a = MathF.Sqrt((xx + yy) + (zz + ww)), b = MathF.Sqrt((xx + ww) + (yy + zz));
            if (!(a > 0f) || !(b > 0f) || float.IsInfinity(a) || float.IsInfinity(b)) return Quaternion.Normalize(q);
            Quaternion r;
            r.x = q.x / a; r.y = q.y / b; r.z = q.z / a; r.w = q.w / b;
            return r;
        }

        /// parent × child: each element a0·b0 + (a1·b1 + a2·b2), the translation's parent column
        /// added last.
        public static Affine Mul(in Affine a, in Affine b)
        {
            Affine c;
            c.M00 = a.M00 * b.M00 + (a.M01 * b.M10 + a.M02 * b.M20);
            c.M01 = a.M00 * b.M01 + (a.M01 * b.M11 + a.M02 * b.M21);
            c.M02 = a.M00 * b.M02 + (a.M01 * b.M12 + a.M02 * b.M22);
            c.M03 = (a.M00 * b.M03 + (a.M01 * b.M13 + a.M02 * b.M23)) + a.M03;
            c.M10 = a.M10 * b.M00 + (a.M11 * b.M10 + a.M12 * b.M20);
            c.M11 = a.M10 * b.M01 + (a.M11 * b.M11 + a.M12 * b.M21);
            c.M12 = a.M10 * b.M02 + (a.M11 * b.M12 + a.M12 * b.M22);
            c.M13 = (a.M10 * b.M03 + (a.M11 * b.M13 + a.M12 * b.M23)) + a.M13;
            c.M20 = a.M20 * b.M00 + (a.M21 * b.M10 + a.M22 * b.M20);
            c.M21 = a.M20 * b.M01 + (a.M21 * b.M11 + a.M22 * b.M21);
            c.M22 = a.M20 * b.M02 + (a.M21 * b.M12 + a.M22 * b.M22);
            c.M23 = (a.M20 * b.M03 + (a.M21 * b.M13 + a.M22 * b.M23)) + a.M23;
            return c;
        }

        /// a·b as the hierarchy multiplies rotations (each lane's terms in its own order; the
        /// vector lanes negated, which decides the sign of a zero).
        public static Quaternion Mul(Quaternion a, Quaternion b)
        {
            float ax = a.x, ay = a.y, az = a.z, aw = a.w, bx = b.x, by = b.y, bz = b.z, bw = b.w;
            Quaternion r;
            r.x = -(((az * by - ay * bz) - aw * bx) - bw * ax);
            r.y = -(((ax * bz - az * bx) - aw * by) - bw * ay);
            r.z = -(((ay * bx - aw * bz) - az * bw) - ax * by);
            r.w = ((aw * bw - ax * bx) - az * bz) - ay * by;
            return r;
        }

        /// A child's rotation seen through its parent's scale signs: each vector component times
        /// the signs of the other two axes (a mirrored axis reverses the rotations about the
        /// others).
        public static Quaternion Mirror(Quaternion q, Vector3 parentScale)
        {
            float sx = Sign(parentScale.x), sy = Sign(parentScale.y), sz = Sign(parentScale.z);
            Quaternion r;
            r.x = q.x * (sy * sz);
            r.y = q.y * (sx * sz);
            r.z = q.z * (sx * sy);
            r.w = q.w;
            return r;
        }

        static float Sign(float v) => float.IsNegative(v) ? -1f : 1f;

        /// The world matrix with the world rotation taken out: the diagonal of R(rotation⁻¹)·M.
        public static Vector3 LossyScale(in Affine m, Quaternion rotation)
        {
            float x = -rotation.x, y = -rotation.y, z = -rotation.z, w = rotation.w;
            float x2 = x + x, y2 = y + y, z2 = z + z;
            float xx = x * x2, yy = y * y2, zz = z * z2, xy = x * y2, xz = x * z2, yz = y * z2;
            float wx = w * x2, wy = w * y2, wz = w * z2;
            float r00 = 1f - (yy + zz), r01 = xy - wz, r02 = xz + wy;
            float r10 = xy + wz, r11 = 1f - (xx + zz), r12 = yz - wx;
            float r20 = xz - wy, r21 = yz + wx, r22 = 1f - (xx + yy);
            return new Vector3(
                r00 * m.M00 + (r01 * m.M10 + r02 * m.M20),
                r10 * m.M01 + (r11 * m.M11 + r12 * m.M21),
                r20 * m.M02 + (r21 * m.M12 + r22 * m.M22));
        }
    }
}
