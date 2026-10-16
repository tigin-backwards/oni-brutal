using System;
using System.Globalization;

namespace UnityEngine
{
    public partial struct Bounds
    {
        private Vector3 m_Center;
        private Vector3 m_Extents;

        public Bounds(Vector3 center, Vector3 size) { m_Center = center; m_Extents = size * 0.5f; }

        public Vector3 center { get => m_Center; set => m_Center = value; }
        public Vector3 size { get => m_Extents * 2f; set => m_Extents = value * 0.5f; }
        public Vector3 extents { get => m_Extents; set => m_Extents = value; }
        public Vector3 min { get => center - extents; set => SetMinMax(value, max); }
        public Vector3 max { get => center + extents; set => SetMinMax(min, value); }

        public void SetMinMax(Vector3 min, Vector3 max)
        {
            extents = (max - min) * 0.5f;
            center = min + extents;
        }

        public void Encapsulate(Vector3 point) => SetMinMax(Vector3.Min(min, point), Vector3.Max(max, point));

        public void Encapsulate(Bounds bounds)
        {
            Encapsulate(bounds.center - bounds.extents);
            Encapsulate(bounds.center + bounds.extents);
        }

        public void Expand(float amount)
        {
            amount *= 0.5f;
            extents += new Vector3(amount, amount, amount);
        }

        public void Expand(Vector3 amount) => extents += amount * 0.5f;

        public bool Intersects(Bounds bounds)
            => min.x <= bounds.max.x && max.x >= bounds.min.x && min.y <= bounds.max.y && max.y >= bounds.min.y
            && min.z <= bounds.max.z && max.z >= bounds.min.z;

        /// Closed box: points on the boundary are contained.
        public bool Contains(Vector3 point)
        {
            Vector3 mn = min, mx = max;
            return point.x >= mn.x && point.x <= mx.x && point.y >= mn.y && point.y <= mx.y && point.z >= mn.z && point.z <= mx.z;
        }

        public float SqrDistance(Vector3 point)
        {
            Vector3 closest = ClosestPoint(point);
            return (point - closest).sqrMagnitude;
        }

        public Vector3 ClosestPoint(Vector3 point) => Vector3.Min(Vector3.Max(point, min), max);

        public bool IntersectRay(Ray ray) => IntersectRay(ray, out _);

        /// Slab test; distance is to the entry point (0 when the origin is inside).
        public bool IntersectRay(Ray ray, out float distance)
        {
            float tMin = float.NegativeInfinity, tMax = float.PositiveInfinity;
            Vector3 o = ray.origin, d = ray.direction, mn = min, mx = max;
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(d[i]) < 1e-12f)
                {
                    if (o[i] < mn[i] || o[i] > mx[i]) { distance = 0f; return false; }
                    continue;
                }
                float inv = 1f / d[i];
                float t1 = (mn[i] - o[i]) * inv, t2 = (mx[i] - o[i]) * inv;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = Math.Max(tMin, t1);
                tMax = Math.Min(tMax, t2);
                if (tMin > tMax) { distance = 0f; return false; }
            }
            if (tMax < 0f) { distance = 0f; return false; }
            distance = Math.Max(tMin, 0f);
            return true;
        }

        public static bool operator ==(Bounds lhs, Bounds rhs) => lhs.center == rhs.center && lhs.extents == rhs.extents;
        public static bool operator !=(Bounds lhs, Bounds rhs) => !(lhs == rhs);

        public override bool Equals(object other) => other is Bounds b && Equals(b);
        public bool Equals(Bounds other) => center.Equals(other.center) && extents.Equals(other.extents);
        public override int GetHashCode() => center.GetHashCode() ^ (extents.GetHashCode() << 2);

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) format = "F2";
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("Center: {0}, Extents: {1}", m_Center.ToString(format, formatProvider), m_Extents.ToString(format, formatProvider));
        }
    }

    public partial struct Ray
    {
        private Vector3 m_Origin;
        private Vector3 m_Direction;

        public Ray(Vector3 origin, Vector3 direction)
        {
            m_Origin = origin;
            m_Direction = direction.normalized;
        }

        public Vector3 origin { get => m_Origin; set => m_Origin = value; }
        public Vector3 direction { get => m_Direction; set => m_Direction = value.normalized; }
        public Vector3 GetPoint(float distance) => m_Origin + m_Direction * distance;

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) format = "F2";
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("Origin: {0}, Dir: {1}", m_Origin.ToString(format, formatProvider), m_Direction.ToString(format, formatProvider));
        }
    }

    public partial struct Plane
    {
        internal const int size = 16;
        private Vector3 m_Normal;
        private float m_Distance;

        public Plane(Vector3 inNormal, Vector3 inPoint)
        {
            m_Normal = Vector3.Normalize(inNormal);
            m_Distance = -Vector3.Dot(m_Normal, inPoint);
        }

        public Plane(Vector3 inNormal, float d)
        {
            m_Normal = Vector3.Normalize(inNormal);
            m_Distance = d;
        }

        public Plane(Vector3 a, Vector3 b, Vector3 c)
        {
            m_Normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            m_Distance = -Vector3.Dot(m_Normal, a);
        }

        public Vector3 normal { get => m_Normal; set => m_Normal = value; }
        public float distance { get => m_Distance; set => m_Distance = value; }

        public void SetNormalAndPosition(Vector3 inNormal, Vector3 inPoint)
        {
            m_Normal = Vector3.Normalize(inNormal);
            m_Distance = -Vector3.Dot(m_Normal, inPoint);
        }

        public float GetDistanceToPoint(Vector3 point) => Vector3.Dot(m_Normal, point) + m_Distance;
        public bool GetSide(Vector3 point) => Vector3.Dot(m_Normal, point) + m_Distance > 0f;
        public Vector3 ClosestPointOnPlane(Vector3 point) => point - m_Normal * (Vector3.Dot(m_Normal, point) + m_Distance);

        /// enter is the signed distance along the ray; true only for hits in front of the origin.
        public bool Raycast(Ray ray, out float enter)
        {
            float vdot = Vector3.Dot(ray.direction, m_Normal);
            float ndot = -Vector3.Dot(ray.origin, m_Normal) - m_Distance;
            if (Mathf.Approximately(vdot, 0f))
            {
                enter = 0f;
                return false;
            }
            enter = ndot / vdot;
            return enter > 0f;
        }

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) format = "F2";
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("(normal:{0}, distance:{1})", m_Normal.ToString(format, formatProvider), UnityFormat.Number(m_Distance, format, formatProvider));
        }

        public override bool Equals(object other) => other is Plane p && Equals(p);
        public bool Equals(Plane other) => m_Normal.Equals(other.m_Normal) && m_Distance.Equals(other.m_Distance);
        public override int GetHashCode() => m_Normal.GetHashCode() ^ (m_Distance.GetHashCode() << 2);
    }

    public partial struct LayerMask
    {
        private int m_Mask;

        public int value { get => m_Mask; set => m_Mask = value; }

        public static implicit operator int(LayerMask mask) => mask.m_Mask;
        public static implicit operator LayerMask(int intVal) => new LayerMask { m_Mask = intVal };

        public static string LayerToName(int layer) => OniBrutal.Engine.Layers.NameOf(layer);
        public static int NameToLayer(string layerName) => OniBrutal.Engine.Layers.IndexOf(layerName);

        public static int GetMask(params string[] layerNames)
        {
            if (layerNames == null) throw new ArgumentNullException(nameof(layerNames));
            int mask = 0;
            foreach (var name in layerNames)
            {
                int layer = NameToLayer(name);
                if (layer != -1) mask |= 1 << layer;
            }
            return mask;
        }
    }
}
