using System;
using System.Collections.Generic;
using System.Linq;
using OniBrutal.Engine;

namespace UnityEngine
{
    public partial struct RaycastHit2D
    {
        private Vector2 m_Centroid;
        private Vector2 m_Point;
        private Vector2 m_Normal;
        private float m_Distance;
        private float m_Fraction;
        private EntityId m_Collider;

        public Vector2 centroid { get => m_Centroid; set => m_Centroid = value; }
        public Vector2 point { get => m_Point; set => m_Point = value; }
        public Vector2 normal { get => m_Normal; set => m_Normal = value; }
        public float distance { get => m_Distance; set => m_Distance = value; }
        public float fraction { get => m_Fraction; set => m_Fraction = value; }

        public Collider2D collider => PhysicsWorld.FindCollider(m_Collider) as Collider2D;

        /// A hit converts to true when it hit a collider.
        public static implicit operator bool(RaycastHit2D hit) => hit.collider != null;

        /// Hits without a collider sort last; otherwise by fraction along the cast.
        public int CompareTo(RaycastHit2D other)
        {
            if (collider == null) return 1;
            if (other.collider == null) return -1;
            return fraction.CompareTo(other.fraction);
        }
    }

    public partial class Collider2D
    {
        /// The shape's offset from the transform in local space (stored; nothing simulates 2D physics).
        public Vector2 offset
        {
            get => ((Collider2DNative)NativeOrThrow).Offset;
            set => ((Collider2DNative)NativeOrThrow).Offset = value;
        }
    }

    public sealed partial class PolygonCollider2D
    {
        PolygonCollider2DNative Data => (PolygonCollider2DNative)NativeOrThrow;

        /// Setting fewer paths drops the last ones; more adds empty ones.
        public int pathCount
        {
            get => Data.Paths.Count;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Path count must be zero or positive.");
                var paths = Data.Paths;
                if (value < paths.Count) paths.RemoveRange(value, paths.Count - value);
                while (paths.Count < value) paths.Add(Array.Empty<Vector2>());
            }
        }

        public Vector2[] GetPath(int index)
        {
            if (index < 0 || index >= Data.Paths.Count) throw new ArgumentOutOfRangeException(nameof(index), $"Path {index} does not exist.");
            return (Vector2[])Data.Paths[index].Clone();
        }

        /// Sets a path, adding paths up to index as needed.
        public void SetPath(int index, Vector2[] points)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), "Negative path index is invalid.");
            if (points == null) throw new ArgumentNullException(nameof(points));
            var paths = Data.Paths;
            while (paths.Count <= index) paths.Add(Array.Empty<Vector2>());
            paths[index] = (Vector2[])points.Clone();
        }
    }

    public sealed partial class BoxCollider2D
    {
        public Vector2 size
        {
            set => ((BoxCollider2DNative)NativeOrThrow).Size = value;
        }
    }

    public sealed partial class Rigidbody2D
    {
        /// No simulation steps bodies (the game creates none): the velocity changes only when a
        /// script sets it. Velocity is not serialized, so a loaded body starts at rest.
        public Vector2 linearVelocity
        {
            get => ((Rigidbody2DNative)NativeOrThrow).LinearVelocity;
            set => ((Rigidbody2DNative)NativeOrThrow).LinearVelocity = value;
        }
    }
}

namespace OniBrutal.Engine
{
    using OniBrutal.Assets.TypeTrees;
    using OniBrutal.Engine.Assets;
    using OniBrutal.Engine.Serialization;
    using UnityEngine;

    /// Engine-side state shared by 2D colliders. Nothing simulates or queries 2D physics, so the
    /// shapes are only stored.
    internal abstract class Collider2DNative : BehaviourNative
    {
        public bool IsTrigger;
        public Vector2 Offset;

        public override void CopyStateFrom(NativeObject source)
        {
            base.CopyStateFrom(source);
            var s = (Collider2DNative)source;
            IsTrigger = s.IsTrigger;
            Offset = s.Offset;
        }

        public override void Bind(Fields data, IReferenceResolver resolver)
        {
            IsTrigger = data.Bool("m_IsTrigger");
            Offset = TransformBinder.ToVector2(data.Struct("m_Offset"));
            Serialized = null;
        }
    }

    internal sealed class BoxCollider2DNative : Collider2DNative
    {
        public Vector2 Size = Vector2.one;
        public float EdgeRadius;

        public BoxCollider2DNative() => PhysicsWorld.Register(this);

        public override void CopyStateFrom(NativeObject source)
        {
            base.CopyStateFrom(source);
            var s = (BoxCollider2DNative)source;
            Size = s.Size;
            EdgeRadius = s.EdgeRadius;
        }

        public override void Bind(Fields data, IReferenceResolver resolver)
        {
            base.Bind(data, resolver);
            Size = TransformBinder.ToVector2(data.Struct("m_Size"));
            EdgeRadius = data.Float("m_EdgeRadius");
        }
    }

    /// A PolygonCollider2D's paths (closed outlines in local space).
    internal sealed class PolygonCollider2DNative : Collider2DNative
    {
        public List<Vector2[]> Paths = new();

        public override void CopyStateFrom(NativeObject source)
        {
            base.CopyStateFrom(source);
            Paths = ((PolygonCollider2DNative)source).Paths.Select(p => (Vector2[])p.Clone()).ToList();
        }

        public override void Bind(Fields data, IReferenceResolver resolver)
        {
            base.Bind(data, resolver);
            Paths = new List<Vector2[]>();
            foreach (var path in data.Struct("m_Points").Array("m_Paths"))
                Paths.Add(((object[])path).Cast<Fields>().Select(TransformBinder.ToVector2).ToArray());
        }
    }

    internal sealed class Rigidbody2DNative : ComponentNative
    {
        public Vector2 LinearVelocity;

        public override void Bind(Fields data, IReferenceResolver resolver) => Serialized = null;
    }
}
