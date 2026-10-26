using System;
using System.IO;
using OniMoono.Assets.TypeTrees;
using UnityEngine;

namespace OniMoono.Engine.Particles
{
    /// Reading helpers for particle-system type-tree data (numeric fields vary in width).
    internal static class ParticleFields
    {
        public static float F(Fields f, string name) => Convert.ToSingle(f[name]);
        public static int I(Fields f, string name) => Convert.ToInt32(f[name]);
        public static bool B(Fields f, string name) => f[name] is bool b ? b : Convert.ToInt32(f[name]) != 0;

        public static Vector3 V3(Fields f, string name)
        {
            var v = f.Struct(name);
            return new Vector3(F(v, "x"), F(v, "y"), F(v, "z"));
        }

        public static Vector2 V2(Fields f, string name)
        {
            var v = f.Struct(name);
            return new Vector2(F(v, "x"), F(v, "y"));
        }

        public static Color Rgba(Fields c) => new(F(c, "r"), F(c, "g"), F(c, "b"), F(c, "a"));

        /// An AnimationCurve as stored natively (m_Curve keys, infinity modes 0 PingPong,
        /// 1 Loop, 2 ClampForever).
        public static AnimationCurve Curve(Fields f)
        {
            var raw = f.Array("m_Curve");
            var keys = new Keyframe[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                var k = (Fields)raw[i];
                keys[i] = Keyframe.FromSerialized(F(k, "time"), F(k, "value"), F(k, "inSlope"), F(k, "outSlope"),
                    I(k, "weightedMode"), F(k, "inWeight"), F(k, "outWeight"));
            }
            return new AnimationCurve(keys)
            {
                preWrapMode = Wrap(I(f, "m_PreInfinity")),
                postWrapMode = Wrap(I(f, "m_PostInfinity")),
            };
        }

        static WrapMode Wrap(int infinity) => infinity switch
        {
            0 => WrapMode.PingPong,
            1 => WrapMode.Loop,
            _ => WrapMode.ClampForever,
        };

        /// A Gradient as stored natively: eight packed keys, times in 1/65535 units.
        public static Gradient Gradient(Fields f)
        {
            var keys = new Color[8];
            var ctime = new ushort[8];
            var atime = new ushort[8];
            for (int i = 0; i < 8; i++)
            {
                keys[i] = Rgba(f.Struct("key" + i));
                ctime[i] = (ushort)I(f, "ctime" + i);
                atime[i] = (ushort)I(f, "atime" + i);
            }
            var g = new Gradient();
            g.SetFromSerialized(keys, ctime, atime, I(f, "m_Mode"), I(f, "m_NumColorKeys"), I(f, "m_NumAlphaKeys"));
            return g;
        }
    }

    /// Unity's MinMaxCurve: a value that is a constant, a curve, a random value between two
    /// curves, or a random value between two constants. Curves are scaled by the scalar (the
    /// "multiplier"); t is the curve's normalized time and random the caller's [0,1) value.
    internal sealed class MinMaxCurveData
    {
        public const int Constant = 0, Curve = 1, TwoCurves = 2, TwoConstants = 3;

        public int Mode;
        public float Scalar;
        public float MinScalar;
        public AnimationCurve MinCurve = new();
        public AnimationCurve MaxCurve = new();

        /// A constant: its value in the scalar, the minimum 0 (as a new component reports it).
        public static MinMaxCurveData Const(float value) => new() { Mode = Constant, Scalar = value };

        public static MinMaxCurveData Read(Fields f)
        {
            var c = new MinMaxCurveData
            {
                Mode = ParticleFields.I(f, "minMaxState"),
                Scalar = ParticleFields.F(f, "scalar"),
                MinScalar = ParticleFields.F(f, "minScalar"),
                MaxCurve = ParticleFields.Curve(f.Struct("maxCurve")),
                MinCurve = ParticleFields.Curve(f.Struct("minCurve")),
            };
            if (c.Mode is < Constant or > TwoConstants) throw new InvalidDataException($"MinMaxCurve mode {c.Mode} is not a Unity curve mode.");
            return c;
        }

        public float Evaluate(float t, float random) => Mode switch
        {
            Constant => Scalar,
            Curve => MaxCurve.Evaluate(t) * Scalar,
            TwoCurves => Mathf.LerpUnclamped(MinCurve.Evaluate(t), MaxCurve.Evaluate(t), random) * Scalar,
            _ => Mathf.LerpUnclamped(MinScalar, Scalar, random),
        };

        /// Whether every evaluation gives zero (lets modules be skipped).
        public bool IsZero => Mode switch
        {
            Constant => Scalar == 0f,
            TwoConstants => Scalar == 0f && MinScalar == 0f,
            _ => Scalar == 0f,
        };
    }

    /// Unity's MinMaxGradient: a color, a gradient, a random blend of two colors, a random blend
    /// of two gradients, or a random color picked from a gradient.
    internal sealed class MinMaxGradientData
    {
        public const int Color = 0, Gradient = 1, TwoColors = 2, TwoGradients = 3, RandomColor = 4;

        public int Mode;
        public UnityEngine.Color MinColor = UnityEngine.Color.white;
        public UnityEngine.Color MaxColor = UnityEngine.Color.white;
        public UnityEngine.Gradient MinGradient = new();
        public UnityEngine.Gradient MaxGradient = new();

        public static MinMaxGradientData White() => new() { Mode = Color };

        public static MinMaxGradientData Read(Fields f)
        {
            var g = new MinMaxGradientData
            {
                Mode = ParticleFields.I(f, "minMaxState"),
                MinColor = ParticleFields.Rgba(f.Struct("minColor")),
                MaxColor = ParticleFields.Rgba(f.Struct("maxColor")),
                MaxGradient = ParticleFields.Gradient(f.Struct("maxGradient")),
                MinGradient = ParticleFields.Gradient(f.Struct("minGradient")),
            };
            if (g.Mode is < Color or > RandomColor) throw new InvalidDataException($"MinMaxGradient mode {g.Mode} is not a Unity gradient mode.");
            return g;
        }

        public UnityEngine.Color Evaluate(float t, float random) => Mode switch
        {
            Color => MaxColor,
            Gradient => MaxGradient.Evaluate(t),
            TwoColors => UnityEngine.Color.LerpUnclamped(MinColor, MaxColor, random),
            TwoGradients => UnityEngine.Color.LerpUnclamped(MinGradient.Evaluate(t), MaxGradient.Evaluate(t), random),
            _ => MaxGradient.Evaluate(random),
        };
    }

    /// Deterministic random numbers for a particle system. The system stream is Unity's
    /// xorshift128 (seeded like UnityEngine.Random.InitState); each particle draws one seed from
    /// it, and per-property values are hashed from that seed with a property salt, so a
    /// property's random value is stable over the particle's life (Unity's per-particle
    /// random-seed scheme). Values are not bit-identical to Unity's.
    internal struct ParticleRandom
    {
        uint x, y, z, w;

        public ParticleRandom(uint seed)
        {
            x = seed;
            y = x * 1812433253u + 1u;
            z = y * 1812433253u + 1u;
            w = z * 1812433253u + 1u;
        }

        public uint NextUInt()
        {
            uint t = x ^ (x << 11);
            x = y; y = z; z = w;
            w = w ^ (w >> 19) ^ t ^ (t >> 8);
            return w;
        }

        /// In [0, 1).
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// A [0, 1) value for one property of a particle.
        public static float Hash01(uint seed, uint salt)
        {
            uint h = seed ^ (salt * 0x9E3779B9u);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f);
        }

        /// Seeds for systems with useAutoRandomSeed: a process-wide stream.
        static ParticleRandom auto = new((uint)Environment.TickCount ^ (uint)DateTime.UtcNow.Ticks);

        public static uint NextAutoSeed() => auto.NextUInt();
    }
}
