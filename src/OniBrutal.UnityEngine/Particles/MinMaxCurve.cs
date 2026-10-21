using OniBrutal.Engine;
using OniBrutal.Engine.Particles;

namespace UnityEngine
{
    public partial class ParticleSystem
    {
        /// A value, a curve, or a random pick between two of either (the mode says which), as
        /// measured in the original player: the single-value constructors use the "max" slot
        /// (constant reads constantMax, curve reads curveMax), and Evaluate's lerpFactor (1 by
        /// default) picks between the two values or curves only in the two-value modes.
        public partial struct MinMaxCurve
        {
            ParticleSystemCurveMode m_Mode;
            float m_CurveMultiplier;
            AnimationCurve m_CurveMin;
            AnimationCurve m_CurveMax;
            float m_ConstantMin;
            float m_ConstantMax;

            public MinMaxCurve(float constant)
            {
                this = default;
                m_Mode = ParticleSystemCurveMode.Constant;
                m_ConstantMax = constant;
            }

            public MinMaxCurve(float multiplier, AnimationCurve curve)
            {
                this = default;
                m_Mode = ParticleSystemCurveMode.Curve;
                m_CurveMultiplier = multiplier;
                m_CurveMax = curve;
            }

            public MinMaxCurve(float multiplier, AnimationCurve min, AnimationCurve max)
            {
                this = default;
                m_Mode = ParticleSystemCurveMode.TwoCurves;
                m_CurveMultiplier = multiplier;
                m_CurveMin = min;
                m_CurveMax = max;
            }

            public MinMaxCurve(float min, float max)
            {
                this = default;
                m_Mode = ParticleSystemCurveMode.TwoConstants;
                m_ConstantMin = min;
                m_ConstantMax = max;
            }

            public ParticleSystemCurveMode mode { get => m_Mode; set => m_Mode = value; }
            public float curveMultiplier { get => m_CurveMultiplier; set => m_CurveMultiplier = value; }
            public AnimationCurve curveMax { get => m_CurveMax; set => m_CurveMax = value; }
            public AnimationCurve curveMin { get => m_CurveMin; set => m_CurveMin = value; }
            public float constantMax { get => m_ConstantMax; set => m_ConstantMax = value; }
            public float constantMin { get => m_ConstantMin; set => m_ConstantMin = value; }
            public float constant { get => m_ConstantMax; set => m_ConstantMax = value; }
            public AnimationCurve curve { get => m_CurveMax; set => m_CurveMax = value; }

            public static implicit operator MinMaxCurve(float constant) => new(constant);

            public float Evaluate(float time) => Evaluate(time, 1f);

            public float Evaluate(float time, float lerpFactor) => m_Mode switch
            {
                ParticleSystemCurveMode.Constant => m_ConstantMax,
                ParticleSystemCurveMode.TwoConstants => Mathf.Lerp(m_ConstantMin, m_ConstantMax, lerpFactor),
                ParticleSystemCurveMode.Curve => (m_CurveMax?.Evaluate(time) ?? 0f) * m_CurveMultiplier,
                _ => Mathf.Lerp(m_CurveMin?.Evaluate(time) ?? 0f, m_CurveMax?.Evaluate(time) ?? 0f, lerpFactor) * m_CurveMultiplier,
            };

            /// The engine keeps a constant in the scalar and a curve's multiplier in the same place.
            internal MinMaxCurveData ToData() => m_Mode switch
            {
                ParticleSystemCurveMode.Constant => new() { Mode = MinMaxCurveData.Constant, Scalar = m_ConstantMax, MinScalar = m_ConstantMin },
                ParticleSystemCurveMode.TwoConstants => new() { Mode = MinMaxCurveData.TwoConstants, Scalar = m_ConstantMax, MinScalar = m_ConstantMin },
                _ => new()
                {
                    Mode = (int)m_Mode,
                    Scalar = m_CurveMultiplier,
                    MinCurve = m_CurveMin ?? new AnimationCurve(),
                    MaxCurve = m_CurveMax ?? new AnimationCurve(),
                },
            };

            /// What a module's getter returns: the constants, or the curves with their multiplier;
            /// a constant reports a multiplier of 1 (measured).
            internal static MinMaxCurve FromData(MinMaxCurveData d) => d.Mode switch
            {
                MinMaxCurveData.Constant or MinMaxCurveData.TwoConstants => new()
                {
                    m_Mode = (ParticleSystemCurveMode)d.Mode,
                    m_CurveMultiplier = 1f,
                    m_ConstantMin = d.MinScalar,
                    m_ConstantMax = d.Scalar,
                },
                _ => new()
                {
                    m_Mode = (ParticleSystemCurveMode)d.Mode,
                    m_CurveMultiplier = d.Scalar,
                    m_CurveMin = d.Mode == MinMaxCurveData.TwoCurves ? d.MinCurve : null,
                    m_CurveMax = d.MaxCurve,
                },
            };
        }

        public partial struct MainModule
        {
            public MinMaxCurve startLifetime
            {
                get => MinMaxCurve.FromData(m_ParticleSystem.Data.Config.StartLifetime);
                set => m_ParticleSystem.Data.Config.StartLifetime = value.ToData();
            }

            public float simulationSpeed
            {
                get => m_ParticleSystem.Data.Config.SimulationSpeed;
                set => m_ParticleSystem.Data.Config.SimulationSpeed = value;
            }

            public bool prewarm
            {
                get => m_ParticleSystem.Data.Config.Prewarm;
                set => m_ParticleSystem.Data.Config.Prewarm = value;
            }

            /// Negative values are kept as 0 (measured).
            public int maxParticles
            {
                get => m_ParticleSystem.Data.Config.MaxParticles;
                set => m_ParticleSystem.Data.Config.MaxParticles = System.Math.Max(0, value);
            }
        }

        public partial struct EmissionModule
        {
            public MinMaxCurve rateOverTime
            {
                get => MinMaxCurve.FromData(m_ParticleSystem.Data.Config.RateOverTime);
                set => m_ParticleSystem.Data.Config.RateOverTime = value.ToData();
            }
        }

        public NoiseModule noise => new(this);

        /// Turbulence on the particles' movement. The engine does not simulate it (no game data
        /// enables it): its settings are kept, and enabling it is logged.
        public partial struct NoiseModule
        {
            internal ParticleSystem m_ParticleSystem;

            internal NoiseModule(ParticleSystem particleSystem) => m_ParticleSystem = particleSystem;

            public bool enabled
            {
                get => m_ParticleSystem.Data.Config.NoiseEnabled;
                set
                {
                    var c = m_ParticleSystem.Data.Config;
                    if (value && !c.NoiseEnabled) EngineLog.Engine($"particles: ParticleSystem on {m_ParticleSystem.name}: NoiseModule is not simulated");
                    c.NoiseEnabled = value;
                }
            }

            public MinMaxCurve strength
            {
                get => MinMaxCurve.FromData(m_ParticleSystem.Data.Config.NoiseStrength);
                set => m_ParticleSystem.Data.Config.NoiseStrength = value.ToData();
            }

            /// The strength's constant, or its curve's multiplier.
            public float strengthMultiplier
            {
                get => m_ParticleSystem.Data.Config.NoiseStrength.Scalar;
                set
                {
                    var c = m_ParticleSystem.Data.Config;
                    var s = c.NoiseStrength;
                    c.NoiseStrength = new MinMaxCurveData { Mode = s.Mode, Scalar = value, MinScalar = s.MinScalar, MinCurve = s.MinCurve, MaxCurve = s.MaxCurve };
                }
            }
        }
    }
}
