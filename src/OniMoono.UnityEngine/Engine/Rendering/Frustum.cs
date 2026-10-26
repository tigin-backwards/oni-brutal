using UnityEngine;

namespace OniMoono.Engine.Rendering
{
    /// The six clip planes of a view-projection matrix (Gribb-Hartmann; normals point inwards).
    internal readonly struct Frustum
    {
        readonly Vector4[] planes;

        public Frustum(Matrix4x4 m)
        {
            Vector4 Row(int i) => new(m[i, 0], m[i, 1], m[i, 2], m[i, 3]);
            var r0 = Row(0);
            var r1 = Row(1);
            var r2 = Row(2);
            var r3 = Row(3);
            planes = [r3 + r0, r3 - r0, r3 + r1, r3 - r1, r3 + r2, r3 - r2];
        }

        /// False only when the box lies entirely outside one plane.
        public bool Intersects(Bounds b)
        {
            var c = b.center;
            var e = b.extents;
            foreach (var p in planes)
            {
                float distance = p.x * c.x + p.y * c.y + p.z * c.z + p.w;
                float radius = Mathf.Abs(p.x) * e.x + Mathf.Abs(p.y) * e.y + Mathf.Abs(p.z) * e.z;
                if (distance + radius < 0f) return false;
            }
            return true;
        }
    }
}
