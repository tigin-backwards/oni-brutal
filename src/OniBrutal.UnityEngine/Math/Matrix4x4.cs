using System;
using System.Globalization;

namespace UnityEngine
{
    /// Column-major 4x4 matrix; mRC is row R, column C. Fields are declared in memory order.
    public partial struct Matrix4x4
    {
        public float m00, m10, m20, m30;
        public float m01, m11, m21, m31;
        public float m02, m12, m22, m32;
        public float m03, m13, m23, m33;

        public Matrix4x4(Vector4 column0, Vector4 column1, Vector4 column2, Vector4 column3)
        {
            m00 = column0.x; m10 = column0.y; m20 = column0.z; m30 = column0.w;
            m01 = column1.x; m11 = column1.y; m21 = column1.z; m31 = column1.w;
            m02 = column2.x; m12 = column2.y; m22 = column2.z; m32 = column2.w;
            m03 = column3.x; m13 = column3.y; m23 = column3.z; m33 = column3.w;
        }

        public float this[int row, int column]
        {
            get => this[row + column * 4];
            set => this[row + column * 4] = value;
        }

        public float this[int index]
        {
            get => index switch
            {
                0 => m00, 1 => m10, 2 => m20, 3 => m30,
                4 => m01, 5 => m11, 6 => m21, 7 => m31,
                8 => m02, 9 => m12, 10 => m22, 11 => m32,
                12 => m03, 13 => m13, 14 => m23, 15 => m33,
                _ => throw new IndexOutOfRangeException("Invalid matrix index!"),
            };
            set
            {
                switch (index)
                {
                    case 0: m00 = value; break; case 1: m10 = value; break; case 2: m20 = value; break; case 3: m30 = value; break;
                    case 4: m01 = value; break; case 5: m11 = value; break; case 6: m21 = value; break; case 7: m31 = value; break;
                    case 8: m02 = value; break; case 9: m12 = value; break; case 10: m22 = value; break; case 11: m32 = value; break;
                    case 12: m03 = value; break; case 13: m13 = value; break; case 14: m23 = value; break; case 15: m33 = value; break;
                    default: throw new IndexOutOfRangeException("Invalid matrix index!");
                }
            }
        }

        public static Matrix4x4 zero => default;

        public static Matrix4x4 identity => new Matrix4x4(
            new Vector4(1f, 0f, 0f, 0f), new Vector4(0f, 1f, 0f, 0f), new Vector4(0f, 0f, 1f, 0f), new Vector4(0f, 0f, 0f, 1f));

        public Vector4 GetColumn(int index) => index switch
        {
            0 => new Vector4(m00, m10, m20, m30),
            1 => new Vector4(m01, m11, m21, m31),
            2 => new Vector4(m02, m12, m22, m32),
            3 => new Vector4(m03, m13, m23, m33),
            _ => throw new IndexOutOfRangeException("Invalid column index!"),
        };

        public Vector4 GetRow(int index) => index switch
        {
            0 => new Vector4(m00, m01, m02, m03),
            1 => new Vector4(m10, m11, m12, m13),
            2 => new Vector4(m20, m21, m22, m23),
            3 => new Vector4(m30, m31, m32, m33),
            _ => throw new IndexOutOfRangeException("Invalid row index!"),
        };

        public void SetColumn(int index, Vector4 column)
        {
            this[0, index] = column.x; this[1, index] = column.y; this[2, index] = column.z; this[3, index] = column.w;
        }

        public void SetRow(int index, Vector4 row)
        {
            this[index, 0] = row.x; this[index, 1] = row.y; this[index, 2] = row.z; this[index, 3] = row.w;
        }

        public Vector3 GetPosition() => new Vector3(m03, m13, m23);

        public static Matrix4x4 operator *(Matrix4x4 lhs, Matrix4x4 rhs)
        {
            Matrix4x4 r;
            r.m00 = Dot(lhs.m00, rhs.m00, lhs.m01, rhs.m10, lhs.m02, rhs.m20, lhs.m03, rhs.m30);
            r.m01 = Dot(lhs.m00, rhs.m01, lhs.m01, rhs.m11, lhs.m02, rhs.m21, lhs.m03, rhs.m31);
            r.m02 = Dot(lhs.m00, rhs.m02, lhs.m01, rhs.m12, lhs.m02, rhs.m22, lhs.m03, rhs.m32);
            r.m03 = Dot(lhs.m00, rhs.m03, lhs.m01, rhs.m13, lhs.m02, rhs.m23, lhs.m03, rhs.m33);
            r.m10 = Dot(lhs.m10, rhs.m00, lhs.m11, rhs.m10, lhs.m12, rhs.m20, lhs.m13, rhs.m30);
            r.m11 = Dot(lhs.m10, rhs.m01, lhs.m11, rhs.m11, lhs.m12, rhs.m21, lhs.m13, rhs.m31);
            r.m12 = Dot(lhs.m10, rhs.m02, lhs.m11, rhs.m12, lhs.m12, rhs.m22, lhs.m13, rhs.m32);
            r.m13 = Dot(lhs.m10, rhs.m03, lhs.m11, rhs.m13, lhs.m12, rhs.m23, lhs.m13, rhs.m33);
            r.m20 = Dot(lhs.m20, rhs.m00, lhs.m21, rhs.m10, lhs.m22, rhs.m20, lhs.m23, rhs.m30);
            r.m21 = Dot(lhs.m20, rhs.m01, lhs.m21, rhs.m11, lhs.m22, rhs.m21, lhs.m23, rhs.m31);
            r.m22 = Dot(lhs.m20, rhs.m02, lhs.m21, rhs.m12, lhs.m22, rhs.m22, lhs.m23, rhs.m32);
            r.m23 = Dot(lhs.m20, rhs.m03, lhs.m21, rhs.m13, lhs.m22, rhs.m23, lhs.m23, rhs.m33);
            r.m30 = Dot(lhs.m30, rhs.m00, lhs.m31, rhs.m10, lhs.m32, rhs.m20, lhs.m33, rhs.m30);
            r.m31 = Dot(lhs.m30, rhs.m01, lhs.m31, rhs.m11, lhs.m32, rhs.m21, lhs.m33, rhs.m31);
            r.m32 = Dot(lhs.m30, rhs.m02, lhs.m31, rhs.m12, lhs.m32, rhs.m22, lhs.m33, rhs.m32);
            r.m33 = Dot(lhs.m30, rhs.m03, lhs.m31, rhs.m13, lhs.m32, rhs.m23, lhs.m33, rhs.m33);
            return r;
        }

        public static Vector4 operator *(Matrix4x4 lhs, Vector4 v) => new Vector4(
            Dot(lhs.m00, v.x, lhs.m01, v.y, lhs.m02, v.z, lhs.m03, v.w),
            Dot(lhs.m10, v.x, lhs.m11, v.y, lhs.m12, v.z, lhs.m13, v.w),
            Dot(lhs.m20, v.x, lhs.m21, v.y, lhs.m22, v.z, lhs.m23, v.w),
            Dot(lhs.m30, v.x, lhs.m31, v.y, lhs.m32, v.z, lhs.m33, v.w));

        // The products are evaluated as Unity's own Matrix4x4 is under its Mono (ARCHITECTURE
        // D25: float32 off): each element's sum of products in double, left to right, rounded to
        // float once. Measured bit for bit in the original on 400 random cases of each product
        // (tools/UnityProbe MatrixMath.cs, mm-check.py); rounding every multiply and add, as
        // CoreCLR does with the float expression, matched 0 to 130 of them. ScrollRect's bounds
        // depend on it: whether a content's bounds in its viewport change by an ulp when the
        // content grows decides whether the scrollbar is updated again (docs/cleanroom/ugui.md,
        // "ScrollRect: bounds to the bit").
        static float Dot(float a0, float b0, float a1, float b1, float a2, float b2, float a3, float b3)
            => (float)((double)a0 * b0 + (double)a1 * b1 + (double)a2 * b2 + (double)a3 * b3);

        static float Dot(float a0, float b0, float a1, float b1, float a2, float b2)
            => (float)((double)a0 * b0 + (double)a1 * b1 + (double)a2 * b2);

        static float Affine(float a0, float b0, float a1, float b1, float a2, float b2, float a3)
            => (float)((double)a0 * b0 + (double)a1 * b1 + (double)a2 * b2 + a3);

        public static bool operator ==(Matrix4x4 lhs, Matrix4x4 rhs)
            => lhs.GetColumn(0) == rhs.GetColumn(0) && lhs.GetColumn(1) == rhs.GetColumn(1)
            && lhs.GetColumn(2) == rhs.GetColumn(2) && lhs.GetColumn(3) == rhs.GetColumn(3);

        public static bool operator !=(Matrix4x4 lhs, Matrix4x4 rhs) => !(lhs == rhs);

        /// Full projective transform with the perspective divide.
        public Vector3 MultiplyPoint(Vector3 point)
        {
            float x = Affine(m00, point.x, m01, point.y, m02, point.z, m03);
            float y = Affine(m10, point.x, m11, point.y, m12, point.z, m13);
            float z = Affine(m20, point.x, m21, point.y, m22, point.z, m23);
            float w = 1f / Affine(m30, point.x, m31, point.y, m32, point.z, m33);
            return new Vector3(x * w, y * w, z * w);
        }

        public Vector3 MultiplyPoint3x4(Vector3 point) => new Vector3(
            Affine(m00, point.x, m01, point.y, m02, point.z, m03),
            Affine(m10, point.x, m11, point.y, m12, point.z, m13),
            Affine(m20, point.x, m21, point.y, m22, point.z, m23));

        public Vector3 MultiplyVector(Vector3 vector) => new Vector3(
            Dot(m00, vector.x, m01, vector.y, m02, vector.z),
            Dot(m10, vector.x, m11, vector.y, m12, vector.z),
            Dot(m20, vector.x, m21, vector.y, m22, vector.z));

        public static Matrix4x4 Scale(Vector3 vector)
        {
            var m = identity;
            m.m00 = vector.x; m.m11 = vector.y; m.m22 = vector.z;
            return m;
        }

        public static Matrix4x4 Translate(Vector3 vector)
        {
            var m = identity;
            m.m03 = vector.x; m.m13 = vector.y; m.m23 = vector.z;
            return m;
        }

        public static Matrix4x4 Rotate(Quaternion q)
        {
            float x = q.x * 2f, y = q.y * 2f, z = q.z * 2f;
            float xx = q.x * x, yy = q.y * y, zz = q.z * z;
            float xy = q.x * y, xz = q.x * z, yz = q.y * z;
            float wx = q.w * x, wy = q.w * y, wz = q.w * z;
            Matrix4x4 m;
            m.m00 = 1f - (yy + zz); m.m10 = xy + wz; m.m20 = xz - wy; m.m30 = 0f;
            m.m01 = xy - wz; m.m11 = 1f - (xx + zz); m.m21 = yz + wx; m.m31 = 0f;
            m.m02 = xz + wy; m.m12 = yz - wx; m.m22 = 1f - (xx + yy); m.m32 = 0f;
            m.m03 = 0f; m.m13 = 0f; m.m23 = 0f; m.m33 = 1f;
            return m;
        }

        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s)
        {
            var m = Rotate(q);
            m.m00 *= s.x; m.m10 *= s.x; m.m20 *= s.x;
            m.m01 *= s.y; m.m11 *= s.y; m.m21 *= s.y;
            m.m02 *= s.z; m.m12 *= s.z; m.m22 *= s.z;
            m.m03 = pos.x; m.m13 = pos.y; m.m23 = pos.z;
            return m;
        }

        public void SetTRS(Vector3 pos, Quaternion q, Vector3 s) => this = TRS(pos, q, s);

        public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float zNear, float zFar)
        {
            var m = identity;
            m.m00 = 2f / (right - left);
            m.m11 = 2f / (top - bottom);
            m.m22 = -2f / (zFar - zNear);
            m.m03 = -(right + left) / (right - left);
            m.m13 = -(top + bottom) / (top - bottom);
            m.m23 = -(zFar + zNear) / (zFar - zNear);
            return m;
        }

        public static Matrix4x4 Perspective(float fov, float aspect, float zNear, float zFar)
        {
            float cotangent = 1f / Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
            Matrix4x4 m = default;
            m.m00 = cotangent / aspect;
            m.m11 = cotangent;
            m.m22 = -(zFar + zNear) / (zFar - zNear);
            m.m23 = -2f * zFar * zNear / (zFar - zNear);
            m.m32 = -1f;
            return m;
        }

        public Matrix4x4 transpose => Transpose(this);

        public static Matrix4x4 Transpose(Matrix4x4 m)
            => new Matrix4x4(m.GetRow(0), m.GetRow(1), m.GetRow(2), m.GetRow(3));

        public Matrix4x4 inverse => Inverse(this);

        /// General inverse by cofactors; a singular matrix yields Matrix4x4.zero, as in Unity.
        public static Matrix4x4 Inverse(Matrix4x4 m)
        {
            float a0 = m.m00 * m.m11 - m.m01 * m.m10, a1 = m.m00 * m.m12 - m.m02 * m.m10;
            float a2 = m.m00 * m.m13 - m.m03 * m.m10, a3 = m.m01 * m.m12 - m.m02 * m.m11;
            float a4 = m.m01 * m.m13 - m.m03 * m.m11, a5 = m.m02 * m.m13 - m.m03 * m.m12;
            float b0 = m.m20 * m.m31 - m.m21 * m.m30, b1 = m.m20 * m.m32 - m.m22 * m.m30;
            float b2 = m.m20 * m.m33 - m.m23 * m.m30, b3 = m.m21 * m.m32 - m.m22 * m.m31;
            float b4 = m.m21 * m.m33 - m.m23 * m.m31, b5 = m.m22 * m.m33 - m.m23 * m.m32;
            float det = a0 * b5 - a1 * b4 + a2 * b3 + a3 * b2 - a4 * b1 + a5 * b0;
            if (det == 0f) return zero;
            float inv = 1f / det;
            Matrix4x4 r;
            r.m00 = (+m.m11 * b5 - m.m12 * b4 + m.m13 * b3) * inv;
            r.m01 = (-m.m01 * b5 + m.m02 * b4 - m.m03 * b3) * inv;
            r.m02 = (+m.m31 * a5 - m.m32 * a4 + m.m33 * a3) * inv;
            r.m03 = (-m.m21 * a5 + m.m22 * a4 - m.m23 * a3) * inv;
            r.m10 = (-m.m10 * b5 + m.m12 * b2 - m.m13 * b1) * inv;
            r.m11 = (+m.m00 * b5 - m.m02 * b2 + m.m03 * b1) * inv;
            r.m12 = (-m.m30 * a5 + m.m32 * a2 - m.m33 * a1) * inv;
            r.m13 = (+m.m20 * a5 - m.m22 * a2 + m.m23 * a1) * inv;
            r.m20 = (+m.m10 * b4 - m.m11 * b2 + m.m13 * b0) * inv;
            r.m21 = (-m.m00 * b4 + m.m01 * b2 - m.m03 * b0) * inv;
            r.m22 = (+m.m30 * a4 - m.m31 * a2 + m.m33 * a0) * inv;
            r.m23 = (-m.m20 * a4 + m.m21 * a2 - m.m23 * a0) * inv;
            r.m30 = (-m.m10 * b3 + m.m11 * b1 - m.m12 * b0) * inv;
            r.m31 = (+m.m00 * b3 - m.m01 * b1 + m.m02 * b0) * inv;
            r.m32 = (-m.m30 * a3 + m.m31 * a1 - m.m32 * a0) * inv;
            r.m33 = (+m.m20 * a3 - m.m21 * a1 + m.m22 * a0) * inv;
            return r;
        }

        public float determinant
        {
            get
            {
                float a0 = m00 * m11 - m01 * m10, a1 = m00 * m12 - m02 * m10, a2 = m00 * m13 - m03 * m10;
                float a3 = m01 * m12 - m02 * m11, a4 = m01 * m13 - m03 * m11, a5 = m02 * m13 - m03 * m12;
                float b0 = m20 * m31 - m21 * m30, b1 = m20 * m32 - m22 * m30, b2 = m20 * m33 - m23 * m30;
                float b3 = m21 * m32 - m22 * m31, b4 = m21 * m33 - m23 * m31, b5 = m22 * m33 - m23 * m32;
                return a0 * b5 - a1 * b4 + a2 * b3 + a3 * b2 - a4 * b1 + a5 * b0;
            }
        }

        public bool isIdentity => this.Equals(identity);

        public override bool Equals(object other) => other is Matrix4x4 m && Equals(m);

        public bool Equals(Matrix4x4 other)
            => GetColumn(0).Equals(other.GetColumn(0)) && GetColumn(1).Equals(other.GetColumn(1))
            && GetColumn(2).Equals(other.GetColumn(2)) && GetColumn(3).Equals(other.GetColumn(3));

        public override int GetHashCode()
            => GetColumn(0).GetHashCode() ^ (GetColumn(1).GetHashCode() << 2) ^ (GetColumn(2).GetHashCode() >> 2) ^ (GetColumn(3).GetHashCode() >> 1);

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) format = "F5";
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            string F(float v) => UnityFormat.Number(v, format, formatProvider);
            return $"{F(m00)}\t{F(m01)}\t{F(m02)}\t{F(m03)}\n{F(m10)}\t{F(m11)}\t{F(m12)}\t{F(m13)}\n{F(m20)}\t{F(m21)}\t{F(m22)}\t{F(m23)}\n{F(m30)}\t{F(m31)}\t{F(m32)}\t{F(m33)}\n";
        }
    }
}
