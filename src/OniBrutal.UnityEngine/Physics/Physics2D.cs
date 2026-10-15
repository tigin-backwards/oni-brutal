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

    /// Engine-side state of a BoxCollider2D. Nothing simulates or queries 2D physics, so the
    /// shape is only stored.
    internal sealed class BoxCollider2DNative : BehaviourNative
    {
        public bool IsTrigger;
        public Vector2 Offset;
        public Vector2 Size = Vector2.one;
        public float EdgeRadius;

        public BoxCollider2DNative() => PhysicsWorld.Register(this);

        public override void CopyStateFrom(NativeObject source)
        {
            base.CopyStateFrom(source);
            var s = (BoxCollider2DNative)source;
            IsTrigger = s.IsTrigger;
            Offset = s.Offset;
            Size = s.Size;
            EdgeRadius = s.EdgeRadius;
        }

        public override void Bind(Fields data, IReferenceResolver resolver)
        {
            IsTrigger = data.Bool("m_IsTrigger");
            Offset = TransformBinder.ToVector2(data.Struct("m_Offset"));
            Size = TransformBinder.ToVector2(data.Struct("m_Size"));
            EdgeRadius = data.Float("m_EdgeRadius");
            Serialized = null;
        }
    }

    internal sealed class Rigidbody2DNative : ComponentNative
    {
        public Vector2 LinearVelocity;

        public override void Bind(Fields data, IReferenceResolver resolver) => Serialized = null;
    }
}
