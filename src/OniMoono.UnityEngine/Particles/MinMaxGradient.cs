using System;
using System.Collections.Generic;
using OniMoono.Engine.Particles;

namespace UnityEngine
{
    public partial class ParticleSystem
    {
        /// A colour, a gradient, or a random pick between two of either (the mode says which).
        /// The single-value constructors and setters use the "max" slot, as Unity's do.
        public partial struct MinMaxGradient
        {
            ParticleSystemGradientMode m_Mode;
            Gradient m_GradientMin;
            Gradient m_GradientMax;
            Color m_ColorMin;
            Color m_ColorMax;

            public MinMaxGradient(Color color)
            {
                this = default;
                m_Mode = ParticleSystemGradientMode.Color;
                m_ColorMax = color;
            }

            public MinMaxGradient(Gradient gradient)
            {
                this = default;
                m_Mode = ParticleSystemGradientMode.Gradient;
                m_GradientMax = gradient;
            }

            public MinMaxGradient(Color min, Color max)
            {
                this = default;
                m_Mode = ParticleSystemGradientMode.TwoColors;
                m_ColorMin = min;
                m_ColorMax = max;
            }

            public MinMaxGradient(Gradient min, Gradient max)
            {
                this = default;
                m_Mode = ParticleSystemGradientMode.TwoGradients;
                m_GradientMin = min;
                m_GradientMax = max;
            }

            public ParticleSystemGradientMode mode { get => m_Mode; set => m_Mode = value; }
            public Gradient gradientMax { get => m_GradientMax; set => m_GradientMax = value; }
            public Gradient gradientMin { get => m_GradientMin; set => m_GradientMin = value; }
            public Color colorMax { get => m_ColorMax; set => m_ColorMax = value; }
            public Color colorMin { get => m_ColorMin; set => m_ColorMin = value; }
            public Color color { get => m_ColorMax; set => m_ColorMax = value; }
            public Gradient gradient { get => m_GradientMax; set => m_GradientMax = value; }

            public static implicit operator MinMaxGradient(Color color) => new(color);

            public static implicit operator MinMaxGradient(Gradient gradient) => new(gradient);

            public Color Evaluate(float time) => Evaluate(time, 1f);

            /// The colour at a normalized time; lerpFactor picks between the min and max values
            /// (and is the time on the gradient in RandomColor mode).
            public Color Evaluate(float time, float lerpFactor) => ToData().Evaluate(time, lerpFactor);

            internal MinMaxGradientData ToData() => new()
            {
                Mode = (int)m_Mode,
                MinColor = m_ColorMin,
                MaxColor = m_ColorMax,
                MinGradient = m_GradientMin ?? new Gradient(),
                MaxGradient = m_GradientMax ?? new Gradient(),
            };

            internal static MinMaxGradient FromData(MinMaxGradientData d) => new()
            {
                m_Mode = (ParticleSystemGradientMode)d.Mode,
                m_ColorMin = d.MinColor,
                m_ColorMax = d.MaxColor,
                m_GradientMin = d.MinGradient,
                m_GradientMax = d.MaxGradient,
            };
        }

        public ColorOverLifetimeModule colorOverLifetime => new(this);

        /// The colour each particle is multiplied by over its lifetime.
        public partial struct ColorOverLifetimeModule
        {
            internal ParticleSystem m_ParticleSystem;

            internal ColorOverLifetimeModule(ParticleSystem particleSystem) => m_ParticleSystem = particleSystem;

            public bool enabled
            {
                get => m_ParticleSystem.Data.Config.ColorEnabled;
                set => m_ParticleSystem.Data.Config.ColorEnabled = value;
            }

            public MinMaxGradient color
            {
                get => MinMaxGradient.FromData(m_ParticleSystem.Data.Config.ColorOverLifetime ?? MinMaxGradientData.White());
                set => m_ParticleSystem.Data.Config.ColorOverLifetime = value.ToData();
            }
        }

        public partial struct MainModule
        {
            public MinMaxGradient startColor
            {
                get => MinMaxGradient.FromData(m_ParticleSystem.Data.Config.StartColor);
                set => m_ParticleSystem.Data.Config.StartColor = value.ToData();
            }
        }

        /// The main module's simulation speed.
        public float playbackSpeed
        {
            get => Data.Config.SimulationSpeed;
            set => Data.Config.SimulationSpeed = value;
        }

        /// The main module's start colour as a single colour.
        public Color startColor
        {
            get => Data.Config.StartColor.MaxColor;
            set => Data.Config.StartColor = new MinMaxGradient(value).ToData();
        }
    }

    /// Where a particle hit a collider. The engine does not simulate particle collisions (no game
    /// data enables them; ARCHITECTURE §3.8), so no events are made.
    public partial struct ParticleCollisionEvent
    {
        Vector3 m_Intersection;
        Vector3 m_Normal;
        Vector3 m_Velocity;
        Component m_Collider;

        public Vector3 intersection => m_Intersection;
        public Vector3 normal => m_Normal;
        public Vector3 velocity => m_Velocity;
        public Component colliderComponent => m_Collider;
    }

    /// Collision events: particle collisions are not simulated (see ParticleCollisionEvent), so a
    /// system never has any.
    public static partial class ParticlePhysicsExtensions
    {
        public static int GetSafeCollisionEventSize(ParticleSystem ps)
        {
            if (ps == null) throw new ArgumentNullException(nameof(ps));
            return 0;
        }

        public static int GetCollisionEvents(ParticleSystem ps, GameObject go, List<ParticleCollisionEvent> collisionEvents)
        {
            if (ps == null) throw new ArgumentNullException(nameof(ps));
            if (go == null) throw new ArgumentNullException(nameof(go));
            if (collisionEvents == null) throw new ArgumentNullException(nameof(collisionEvents));
            collisionEvents.Clear();
            return 0;
        }

        public static int GetCollisionEvents(ParticleSystem ps, GameObject go, ParticleCollisionEvent[] collisionEvents)
        {
            if (ps == null) throw new ArgumentNullException(nameof(ps));
            if (go == null) throw new ArgumentNullException(nameof(go));
            if (collisionEvents == null) throw new ArgumentNullException(nameof(collisionEvents));
            return 0;
        }
    }
}
