using System;
using System.Globalization;

namespace UnityEngine
{
    // KSerialization serializes Vector4 reflectively (public fields, settable properties), so the
    // only public instance state is x, y, z, w and there are no settable properties besides the
    // indexer (ARCHITECTURE §3.2).
    public partial struct Vector4
    {
        public const float kEpsilon = 1E-05f;

        public float x;
        public float y;
        public float z;
        public float w;

        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Vector4(float x, float y, float z) { this.x = x; this.y = y; this.z = z; w = 0f; }
        public Vector4(float x, float y) { this.x = x; this.y = y; z = 0f; w = 0f; }

        public float this[int index]
        {
            get => index switch { 0 => x, 1 => y, 2 => z, 3 => w, _ => throw new IndexOutOfRangeException("Invalid Vector4 index!") };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    case 3: w = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Vector4 index!");
                }
            }
        }

        public void Set(float newX, float newY, float newZ, float newW) { x = newX; y = newY; z = newZ; w = newW; }

        public float magnitude => (float)Math.Sqrt(Dot(this, this));
        public float sqrMagnitude => Dot(this, this);
        public Vector4 normalized => Normalize(this);
        public void Normalize() => this = Normalize(this);

        public static Vector4 Normalize(Vector4 a)
        {
            float mag = Magnitude(a);
            return mag > kEpsilon ? a / mag : zero;
        }

        public static Vector4 zero => new Vector4(0f, 0f, 0f, 0f);
        public static Vector4 one => new Vector4(1f, 1f, 1f, 1f);
        public static Vector4 positiveInfinity => new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        public static Vector4 negativeInfinity => new Vector4(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Vector4 LerpUnclamped(Vector4 a, Vector4 b, float t)
            => new Vector4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);

        public static Vector4 MoveTowards(Vector4 current, Vector4 target, float maxDistanceDelta)
        {
            Vector4 d = target - current;
            float sq = Dot(d, d);
            if (sq == 0f || (maxDistanceDelta >= 0f && sq <= maxDistanceDelta * maxDistanceDelta)) return target;
            return current + d / (float)Math.Sqrt(sq) * maxDistanceDelta;
        }

        public static Vector4 Scale(Vector4 a, Vector4 b) => new Vector4(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w);
        public void Scale(Vector4 scale) { x *= scale.x; y *= scale.y; z *= scale.z; w *= scale.w; }
        public static float Dot(Vector4 a, Vector4 b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public static Vector4 Project(Vector4 a, Vector4 b) => b * (Dot(a, b) / Dot(b, b));
        public static float Distance(Vector4 a, Vector4 b) => Magnitude(a - b);
        public static float Magnitude(Vector4 a) => (float)Math.Sqrt(Dot(a, a));
        public static float SqrMagnitude(Vector4 a) => Dot(a, a);
        public float SqrMagnitude() => Dot(this, this);
        public static Vector4 Min(Vector4 lhs, Vector4 rhs) => new Vector4(Mathf.Min(lhs.x, rhs.x), Mathf.Min(lhs.y, rhs.y), Mathf.Min(lhs.z, rhs.z), Mathf.Min(lhs.w, rhs.w));
        public static Vector4 Max(Vector4 lhs, Vector4 rhs) => new Vector4(Mathf.Max(lhs.x, rhs.x), Mathf.Max(lhs.y, rhs.y), Mathf.Max(lhs.z, rhs.z), Mathf.Max(lhs.w, rhs.w));

        public static Vector4 operator +(Vector4 a, Vector4 b) => new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        public static Vector4 operator -(Vector4 a, Vector4 b) => new Vector4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
        public static Vector4 operator -(Vector4 a) => new Vector4(-a.x, -a.y, -a.z, -a.w);
        public static Vector4 operator *(Vector4 a, float d) => new Vector4(a.x * d, a.y * d, a.z * d, a.w * d);
        public static Vector4 operator *(float d, Vector4 a) => new Vector4(a.x * d, a.y * d, a.z * d, a.w * d);
        public static Vector4 operator /(Vector4 a, float d) => new Vector4(a.x / d, a.y / d, a.z / d, a.w / d);

        public static bool operator ==(Vector4 lhs, Vector4 rhs)
        {
            float dx = lhs.x - rhs.x, dy = lhs.y - rhs.y, dz = lhs.z - rhs.z, dw = lhs.w - rhs.w;
            return dx * dx + dy * dy + dz * dz + dw * dw < kEpsilon * kEpsilon;
        }

        public static bool operator !=(Vector4 lhs, Vector4 rhs) => !(lhs == rhs);

        public static implicit operator Vector4(Vector3 v) => new Vector4(v.x, v.y, v.z, 0f);
        public static implicit operator Vector3(Vector4 v) => new Vector3(v.x, v.y, v.z);
        public static implicit operator Vector4(Vector2 v) => new Vector4(v.x, v.y, 0f, 0f);
        public static implicit operator Vector2(Vector4 v) => new Vector2(v.x, v.y);

        public override bool Equals(object other) => other is Vector4 v && Equals(v);
        public bool Equals(Vector4 other) => x == other.x && y == other.y && z == other.z && w == other.w;
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 1);

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) format = "F2";
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("({0}, {1}, {2}, {3})", UnityFormat.Number(x, format, formatProvider), UnityFormat.Number(y, format, formatProvider),
                UnityFormat.Number(z, format, formatProvider), UnityFormat.Number(w, format, formatProvider));
        }
    }
}
