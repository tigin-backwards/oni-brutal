using System;
using System.Globalization;

namespace UnityEngine
{
    public partial struct Quaternion
    {
        public const float kEpsilon = 1E-06f;

        public float x;
        public float y;
        public float z;
        public float w;

        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }

        public float this[int index]
        {
            get => index switch { 0 => x, 1 => y, 2 => z, 3 => w, _ => throw new IndexOutOfRangeException("Invalid Quaternion index!") };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    case 3: w = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Quaternion index!");
                }
            }
        }

        public void Set(float newX, float newY, float newZ, float newW) { x = newX; y = newY; z = newZ; w = newW; }

        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        public static Quaternion operator *(Quaternion lhs, Quaternion rhs) => new Quaternion(
            lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
            lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
            lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x,
            lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);

        public static Vector3 operator *(Quaternion rotation, Vector3 point)
        {
            float x2 = rotation.x * 2f, y2 = rotation.y * 2f, z2 = rotation.z * 2f;
            float xx = rotation.x * x2, yy = rotation.y * y2, zz = rotation.z * z2;
            float xy = rotation.x * y2, xz = rotation.x * z2, yz = rotation.y * z2;
            float wx = rotation.w * x2, wy = rotation.w * y2, wz = rotation.w * z2;
            return new Vector3(
                (1f - (yy + zz)) * point.x + (xy - wz) * point.y + (xz + wy) * point.z,
                (xy + wz) * point.x + (1f - (xx + zz)) * point.y + (yz - wx) * point.z,
                (xz - wy) * point.x + (yz + wx) * point.y + (1f - (xx + yy)) * point.z);
        }

        /// Unity's rotation equality: unit quaternions whose dot product exceeds 1 - kEpsilon
        /// describe (nearly) the same rotation.
        static bool DotMeansSameRotation(float dot) => dot > 1f - kEpsilon;

        public static bool operator ==(Quaternion lhs, Quaternion rhs) => DotMeansSameRotation(Dot(lhs, rhs));
        public static bool operator !=(Quaternion lhs, Quaternion rhs) => !(lhs == rhs);

        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public static float Angle(Quaternion a, Quaternion b)
        {
            float dot = Mathf.Min(Mathf.Abs(Dot(a, b)), 1f);
            return DotMeansSameRotation(dot) ? 0f : Mathf.Acos(dot) * 2f * Mathf.Rad2Deg;
        }

        public static Quaternion Inverse(Quaternion rotation)
        {
            float sq = Dot(rotation, rotation);
            if (sq == 0f) return rotation;
            float inv = 1f / sq;
            return new Quaternion(-rotation.x * inv, -rotation.y * inv, -rotation.z * inv, rotation.w * inv);
        }

        public static Quaternion Normalize(Quaternion q)
        {
            float mag = Mathf.Sqrt(Dot(q, q));
            return mag < Mathf.Epsilon ? identity : new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
        }

        public void Normalize() => this = Normalize(this);
        public Quaternion normalized => Normalize(this);

        public static Quaternion AngleAxis(float angle, Vector3 axis)
        {
            float mag = axis.magnitude;
            if (mag < Vector3.kEpsilon) return identity;
            float half = angle * Mathf.Deg2Rad * 0.5f;
            float s = Mathf.Sin(half) / mag;
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(half));
        }

        public void ToAngleAxis(out float angle, out Vector3 axis)
        {
            var q = Normalize(this);
            angle = 2f * Mathf.Acos(Mathf.Clamp(q.w, -1f, 1f)) * Mathf.Rad2Deg;
            float s = Mathf.Sqrt(1f - q.w * q.w);
            axis = s > 0.001f ? new Vector3(q.x / s, q.y / s, q.z / s) : new Vector3(1f, 0f, 0f);
        }

        /// Rotation of z degrees about Z, then x about X, then y about Y (Unity's ZXY order).
        public static Quaternion Euler(float x, float y, float z) => FromEulerRad(new Vector3(x, y, z) * Mathf.Deg2Rad);
        public static Quaternion Euler(Vector3 euler) => FromEulerRad(euler * Mathf.Deg2Rad);

        static Quaternion FromEulerRad(Vector3 e)
        {
            float hx = e.x * 0.5f, hy = e.y * 0.5f, hz = e.z * 0.5f;
            var qx = new Quaternion(Mathf.Sin(hx), 0f, 0f, Mathf.Cos(hx));
            var qy = new Quaternion(0f, Mathf.Sin(hy), 0f, Mathf.Cos(hy));
            var qz = new Quaternion(0f, 0f, Mathf.Sin(hz), Mathf.Cos(hz));
            return qy * qx * qz;
        }

        public Vector3 eulerAngles
        {
            get
            {
                var degrees = ToEulerRad(this) * Mathf.Rad2Deg;
                return new Vector3(WrapDegrees(degrees.x), WrapDegrees(degrees.y), WrapDegrees(degrees.z));
            }
            set => this = FromEulerRad(value * Mathf.Deg2Rad);
        }

        /// Wraps an angle with Unity's tolerance t = 0.0001 rad (in degrees): below -t it gains
        /// 360, above 360 - t it loses 360 (so an angle just below 360 comes out slightly
        /// negative), and anything else is returned unchanged.
        static float WrapDegrees(float degrees)
        {
            const float tolerance = 0.0001f * Mathf.Rad2Deg;
            if (degrees < -tolerance) return degrees + 360f;
            if (degrees > 360f - tolerance) return degrees - 360f;
            return degrees;
        }

        /// Inverse of FromEulerRad: decomposes R = Ry * Rx * Rz.
        static Vector3 ToEulerRad(Quaternion q)
        {
            q = Normalize(q);
            float m02 = 2f * (q.x * q.z + q.w * q.y);
            float m12 = 2f * (q.y * q.z - q.w * q.x);
            float m22 = 1f - 2f * (q.x * q.x + q.y * q.y);
            float m10 = 2f * (q.x * q.y + q.w * q.z);
            float m11 = 1f - 2f * (q.x * q.x + q.z * q.z);
            float m00 = 1f - 2f * (q.y * q.y + q.z * q.z);
            float m20 = 2f * (q.x * q.z - q.w * q.y);
            float sinX = Mathf.Clamp(-m12, -1f, 1f);
            if (Mathf.Abs(sinX) < 0.99999f)
                return new Vector3(Mathf.Asin(sinX), Mathf.Atan2(m02, m22), Mathf.Atan2(m10, m11));
            // Gimbal lock: X at ±90°, fold Z into Y.
            return new Vector3(sinX > 0f ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f, Mathf.Atan2(-m20, m00), 0f);
        }


        public static Quaternion FromToRotation(Vector3 fromDirection, Vector3 toDirection)
        {
            Vector3 from = fromDirection.normalized, to = toDirection.normalized;
            float dot = Vector3.Dot(from, to);
            if (dot < -1f + kEpsilon)
            {
                Vector3 axis = Vector3.Cross(Vector3.right, from);
                if (axis.sqrMagnitude < kEpsilon) axis = Vector3.Cross(Vector3.up, from);
                return AngleAxis(180f, axis.normalized);
            }
            Vector3 c = Vector3.Cross(from, to);
            return Normalize(new Quaternion(c.x, c.y, c.z, 1f + dot));
        }

        public void SetFromToRotation(Vector3 fromDirection, Vector3 toDirection) => this = FromToRotation(fromDirection, toDirection);

        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);

        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards)
        {
            if (forward.sqrMagnitude < Vector3.kEpsilon * Vector3.kEpsilon)
            {
                Debug.Log("Look rotation viewing vector is zero");
                return identity;
            }
            Vector3 z = forward.normalized;
            Vector3 x = Vector3.Cross(upwards, z);
            if (x.sqrMagnitude < Vector3.kEpsilon * Vector3.kEpsilon)
                return FromToRotation(Vector3.forward, z); // up parallel to forward: shortest arc
            x = x.normalized;
            Vector3 y = Vector3.Cross(z, x);
            return FromBasis(x, y, z);
        }

        public void SetLookRotation(Vector3 view) => this = LookRotation(view);
        public void SetLookRotation(Vector3 view, Vector3 up) => this = LookRotation(view, up);

        /// Quaternion of the rotation matrix whose columns are x, y, z.
        internal static Quaternion FromBasis(Vector3 x, Vector3 y, Vector3 z)
        {
            float m00 = x.x, m10 = x.y, m20 = x.z;
            float m01 = y.x, m11 = y.y, m21 = y.z;
            float m02 = z.x, m12 = z.y, m22 = z.z;
            float trace = m00 + m11 + m22;
            Quaternion q;
            if (trace > 0f)
            {
                float s = Mathf.Sqrt(trace + 1f) * 2f;
                q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
                q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            else if (m11 > m22)
            {
                float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
                q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            else
            {
                float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
                q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s);
            }
            return Normalize(q);
        }

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => SlerpUnclamped(a, b, Mathf.Clamp01(t));

        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            float dot = Dot(a, b);
            if (dot < 0f)
            {
                b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
                dot = -dot;
            }
            if (dot > 0.9995f) return LerpUnclamped(a, b, t);
            float theta = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
            float sinTheta = Mathf.Sin(theta);
            float wa = Mathf.Sin((1f - t) * theta) / sinTheta, wb = Mathf.Sin(t * theta) / sinTheta;
            return new Quaternion(a.x * wa + b.x * wb, a.y * wa + b.y * wb, a.z * wa + b.z * wb, a.w * wa + b.w * wb);
        }

        /// Normalized linear interpolation along the shorter arc.
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));

        public static Quaternion LerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            if (Dot(a, b) < 0f) b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
            return Normalize(new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t));
        }

        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta)
        {
            float angle = Angle(from, to);
            return angle == 0f ? to : SlerpUnclamped(from, to, Mathf.Min(1f, maxDegreesDelta / angle));
        }

        public override bool Equals(object other) => other is Quaternion q && Equals(q);
        public bool Equals(Quaternion other) => x.Equals(other.x) && y.Equals(other.y) && z.Equals(other.z) && w.Equals(other.w);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 1);

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) format = "F5";
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("({0}, {1}, {2}, {3})", UnityFormat.Number(x, format, formatProvider), UnityFormat.Number(y, format, formatProvider),
                UnityFormat.Number(z, format, formatProvider), UnityFormat.Number(w, format, formatProvider));
        }
    }
}
