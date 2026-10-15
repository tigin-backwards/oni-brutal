using System;
using System.Globalization;

namespace UnityEngine
{
    public partial struct Vector2Int
    {
        // Serialized as m_X/m_Y, like Unity's own declaration.
        private int m_X;
        private int m_Y;

        public Vector2Int(int x, int y) { m_X = x; m_Y = y; }

        public int x { get => m_X; set => m_X = value; }
        public int y { get => m_Y; set => m_Y = value; }

        public int this[int index]
        {
            get => index switch { 0 => x, 1 => y, _ => throw new IndexOutOfRangeException($"Invalid Vector2Int index addressed: {index}!") };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    default: throw new IndexOutOfRangeException($"Invalid Vector2Int index addressed: {index}!");
                }
            }
        }

        public void Set(int x, int y) { m_X = x; m_Y = y; }
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public int sqrMagnitude => x * x + y * y;

        public static Vector2Int zero => new Vector2Int(0, 0);
        public static Vector2Int one => new Vector2Int(1, 1);
        public static Vector2Int up => new Vector2Int(0, 1);
        public static Vector2Int down => new Vector2Int(0, -1);
        public static Vector2Int left => new Vector2Int(-1, 0);
        public static Vector2Int right => new Vector2Int(1, 0);

        public static float Distance(Vector2Int a, Vector2Int b)
        {
            float dx = a.x - b.x, dy = a.y - b.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static Vector2Int Min(Vector2Int lhs, Vector2Int rhs) => new Vector2Int(Mathf.Min(lhs.x, rhs.x), Mathf.Min(lhs.y, rhs.y));
        public static Vector2Int Max(Vector2Int lhs, Vector2Int rhs) => new Vector2Int(Mathf.Max(lhs.x, rhs.x), Mathf.Max(lhs.y, rhs.y));
        public static Vector2Int Scale(Vector2Int a, Vector2Int b) => new Vector2Int(a.x * b.x, a.y * b.y);
        public void Scale(Vector2Int scale) { x *= scale.x; y *= scale.y; }
        public void Clamp(Vector2Int min, Vector2Int max) { x = Math.Max(min.x, x); x = Math.Min(max.x, x); y = Math.Max(min.y, y); y = Math.Min(max.y, y); }

        public static Vector2Int FloorToInt(Vector2 v) => new Vector2Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        public static Vector2Int CeilToInt(Vector2 v) => new Vector2Int(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));
        public static Vector2Int RoundToInt(Vector2 v) => new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));

        public static implicit operator Vector2(Vector2Int v) => new Vector2(v.x, v.y);
        public static explicit operator Vector3Int(Vector2Int v) => new Vector3Int(v.x, v.y, 0);

        public static Vector2Int operator -(Vector2Int v) => new Vector2Int(-v.x, -v.y);
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);
        public static Vector2Int operator *(Vector2Int a, Vector2Int b) => new Vector2Int(a.x * b.x, a.y * b.y);
        public static Vector2Int operator *(int a, Vector2Int b) => new Vector2Int(a * b.x, a * b.y);
        public static Vector2Int operator *(Vector2Int a, int b) => new Vector2Int(a.x * b, a.y * b);
        public static Vector2Int operator /(Vector2Int a, int b) => new Vector2Int(a.x / b, a.y / b);
        public static bool operator ==(Vector2Int lhs, Vector2Int rhs) => lhs.x == rhs.x && lhs.y == rhs.y;
        public static bool operator !=(Vector2Int lhs, Vector2Int rhs) => !(lhs == rhs);

        public override bool Equals(object other) => other is Vector2Int v && Equals(v);
        public bool Equals(Vector2Int other) => x == other.x && y == other.y;

        public override int GetHashCode()
        {
            const int p1 = 73856093, p2 = 83492791;
            return (x * p1) ^ (y * p2);
        }

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("({0}, {1})", x.ToString(format, formatProvider), y.ToString(format, formatProvider));
        }
    }

    public partial struct Vector3Int
    {
        private int m_X;
        private int m_Y;
        private int m_Z;

        public Vector3Int(int x, int y) { m_X = x; m_Y = y; m_Z = 0; }
        public Vector3Int(int x, int y, int z) { m_X = x; m_Y = y; m_Z = z; }

        public int x { get => m_X; set => m_X = value; }
        public int y { get => m_Y; set => m_Y = value; }
        public int z { get => m_Z; set => m_Z = value; }

        public int this[int index]
        {
            get => index switch { 0 => x, 1 => y, 2 => z, _ => throw new IndexOutOfRangeException($"Invalid Vector3Int index addressed: {index}!") };
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException($"Invalid Vector3Int index addressed: {index}!");
                }
            }
        }

        public void Set(int x, int y, int z) { m_X = x; m_Y = y; m_Z = z; }
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public int sqrMagnitude => x * x + y * y + z * z;

        public static Vector3Int zero => new Vector3Int(0, 0, 0);
        public static Vector3Int one => new Vector3Int(1, 1, 1);
        public static Vector3Int up => new Vector3Int(0, 1, 0);
        public static Vector3Int down => new Vector3Int(0, -1, 0);
        public static Vector3Int left => new Vector3Int(-1, 0, 0);
        public static Vector3Int right => new Vector3Int(1, 0, 0);
        public static Vector3Int forward => new Vector3Int(0, 0, 1);
        public static Vector3Int back => new Vector3Int(0, 0, -1);

        public static float Distance(Vector3Int a, Vector3Int b) => (a - b).magnitude;
        public static Vector3Int Min(Vector3Int lhs, Vector3Int rhs) => new Vector3Int(Mathf.Min(lhs.x, rhs.x), Mathf.Min(lhs.y, rhs.y), Mathf.Min(lhs.z, rhs.z));
        public static Vector3Int Max(Vector3Int lhs, Vector3Int rhs) => new Vector3Int(Mathf.Max(lhs.x, rhs.x), Mathf.Max(lhs.y, rhs.y), Mathf.Max(lhs.z, rhs.z));
        public static Vector3Int Scale(Vector3Int a, Vector3Int b) => new Vector3Int(a.x * b.x, a.y * b.y, a.z * b.z);
        public void Scale(Vector3Int scale) { x *= scale.x; y *= scale.y; z *= scale.z; }

        public static Vector3Int FloorToInt(Vector3 v) => new Vector3Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));
        public static Vector3Int CeilToInt(Vector3 v) => new Vector3Int(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y), Mathf.CeilToInt(v.z));
        public static Vector3Int RoundToInt(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));

        public static implicit operator Vector3(Vector3Int v) => new Vector3(v.x, v.y, v.z);
        public static explicit operator Vector2Int(Vector3Int v) => new Vector2Int(v.x, v.y);

        public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new Vector3Int(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3Int operator -(Vector3Int a, Vector3Int b) => new Vector3Int(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3Int operator *(Vector3Int a, Vector3Int b) => new Vector3Int(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3Int operator -(Vector3Int a) => new Vector3Int(-a.x, -a.y, -a.z);
        public static Vector3Int operator *(Vector3Int a, int b) => new Vector3Int(a.x * b, a.y * b, a.z * b);
        public static Vector3Int operator *(int a, Vector3Int b) => new Vector3Int(a * b.x, a * b.y, a * b.z);
        public static Vector3Int operator /(Vector3Int a, int b) => new Vector3Int(a.x / b, a.y / b, a.z / b);
        public static bool operator ==(Vector3Int lhs, Vector3Int rhs) => lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
        public static bool operator !=(Vector3Int lhs, Vector3Int rhs) => !(lhs == rhs);

        public override bool Equals(object other) => other is Vector3Int v && Equals(v);
        public bool Equals(Vector3Int other) => this == other;

        public override int GetHashCode()
        {
            int yHash = y.GetHashCode(), zHash = z.GetHashCode();
            return x.GetHashCode() ^ (yHash << 4) ^ (yHash >> 28) ^ (zHash >> 4) ^ (zHash << 28);
        }

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("({0}, {1}, {2})", x.ToString(format, formatProvider), y.ToString(format, formatProvider), z.ToString(format, formatProvider));
        }
    }
}
