using System;

namespace UnityEngine
{
    /// Xorshift128 generator, seeded the way Unity seeds it. Unity's Random is main-thread only.
    public static partial class Random
    {
        [Serializable]
        public struct State
        {
            [SerializeField] private int s0;
            [SerializeField] private int s1;
            [SerializeField] private int s2;
            [SerializeField] private int s3;

            internal State(uint a, uint b, uint c, uint d) { s0 = (int)a; s1 = (int)b; s2 = (int)c; s3 = (int)d; }
            internal uint S0 => (uint)s0;
            internal uint S1 => (uint)s1;
            internal uint S2 => (uint)s2;
            internal uint S3 => (uint)s3;
        }

        static uint x, y, z, w;

        static Random() => InitState(Environment.TickCount ^ (int)DateTime.UtcNow.Ticks);

        public static void InitState(int seed)
        {
            // Each word is derived from the previous with the MT19937 initialization multiplier.
            x = (uint)seed;
            y = x * 1812433253u + 1u;
            z = y * 1812433253u + 1u;
            w = z * 1812433253u + 1u;
        }

        public static State state
        {
            get => new State(x, y, z, w);
            set { x = value.S0; y = value.S1; z = value.S2; w = value.S3; }
        }

        static uint Next()
        {
            uint t = x ^ (x << 11);
            x = y; y = z; z = w;
            w = w ^ (w >> 19) ^ t ^ (t >> 8);
            return w;
        }

        /// Uniform in [0, 1], both ends inclusive.
        public static float value => (Next() & 0x007FFFFFu) / 8388607f;

        /// Inclusive range. Measured in the original (tools/UnityProbe, "values: random"): the draw
        /// weighs min, so Range(a, b) = a·t + b·(1 − t) for the next value t.
        public static float Range(float minInclusive, float maxInclusive)
        {
            float t = value;
            return minInclusive * t + maxInclusive * (1f - t);
        }

        /// Exclusive of max when min &lt; max; when min &gt; max the result lies in (max, min].
        public static int Range(int minInclusive, int maxExclusive)
        {
            if (minInclusive < maxExclusive) return (int)(minInclusive + Next() % (uint)((long)maxExclusive - minInclusive));
            if (minInclusive > maxExclusive) return (int)(minInclusive - Next() % (uint)((long)minInclusive - maxExclusive));
            return minInclusive;
        }

        public static Vector2 insideUnitCircle
        {
            get
            {
                float angle = Range(0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(Range(0f, 1f));
                return new Vector2(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
            }
        }

        public static Vector3 insideUnitSphere
        {
            get
            {
                Vector3 dir = onUnitSphere;
                float radius = Mathf.Pow(Range(0f, 1f), 1f / 3f);
                return dir * radius;
            }
        }

        public static Vector3 onUnitSphere
        {
            get
            {
                float zc = Range(-1f, 1f);
                float angle = Range(0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt(1f - zc * zc);
                return new Vector3(r * Mathf.Cos(angle), r * Mathf.Sin(angle), zc);
            }
        }

        public static Quaternion rotation => Quaternion.Normalize(new Quaternion(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)));

        // Every overload draws hue, saturation, value and alpha in that order, alpha from 1 to 1
        // when not given (measured in the original player: four draws whatever the overload).
        public static Color ColorHSV() => ColorHSV(0f, 1f, 0f, 1f, 0f, 1f, 1f, 1f);

        public static Color ColorHSV(float hueMin, float hueMax) => ColorHSV(hueMin, hueMax, 0f, 1f, 0f, 1f, 1f, 1f);

        public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax)
            => ColorHSV(hueMin, hueMax, saturationMin, saturationMax, 0f, 1f, 1f, 1f);

        public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax, float valueMin, float valueMax)
            => ColorHSV(hueMin, hueMax, saturationMin, saturationMax, valueMin, valueMax, 1f, 1f);

        public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax, float valueMin, float valueMax, float alphaMin, float alphaMax)
        {
            float h = Mathf.Lerp(hueMin, hueMax, value);
            float s = Mathf.Lerp(saturationMin, saturationMax, value);
            float v = Mathf.Lerp(valueMin, valueMax, value);
            Color c = Color.HSVToRGB(h, s, v, true);
            c.a = Mathf.Lerp(alphaMin, alphaMax, value);
            return c;
        }
    }
}
