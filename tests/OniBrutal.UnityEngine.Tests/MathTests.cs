using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class MathTests
    {
        static void Near(float expected, float actual, float tolerance = 1e-4f) => Assert.InRange(actual, expected - tolerance, expected + tolerance);

        [Fact]
        public void EulerRoundTripsInUnityOrder()
        {
            var q = Quaternion.Euler(10f, 20f, 30f);
            var e = q.eulerAngles;
            Near(10f, e.x); Near(20f, e.y); Near(30f, e.z);
            // ZXY: z applied first, then x, then y.
            var composed = Quaternion.AngleAxis(20f, Vector3.up) * Quaternion.AngleAxis(10f, Vector3.right) * Quaternion.AngleAxis(30f, Vector3.forward);
            Assert.True(Quaternion.Dot(q, composed) > 0.99999f);
        }

        [Fact]
        public void NegativeEulerAnglesComeBackPositive()
        {
            var e = Quaternion.Euler(0f, -90f, 0f).eulerAngles;
            Near(270f, e.y);
        }

        [Fact]
        public void LookRotationPointsForward()
        {
            var dir = new Vector3(1f, 2f, 3f).normalized;
            var q = Quaternion.LookRotation(dir);
            var f = q * Vector3.forward;
            Near(dir.x, f.x); Near(dir.y, f.y); Near(dir.z, f.z);
        }

        [Fact]
        public void MatrixInverseUndoesTrs()
        {
            var m = Matrix4x4.TRS(new Vector3(1, 2, 3), Quaternion.Euler(30, 40, 50), new Vector3(2, 3, 4));
            var p = new Vector3(5, -6, 7);
            var back = m.inverse.MultiplyPoint3x4(m.MultiplyPoint3x4(p));
            Near(p.x, back.x); Near(p.y, back.y); Near(p.z, back.z);
            Assert.True((m * m.inverse).isIdentity || ((m * m.inverse).GetColumn(0) - new Vector4(1, 0, 0, 0)).sqrMagnitude < 1e-8f);
        }

        [Fact]
        public void VectorEqualityIsApproximateAndEqualsIsExact()
        {
            var a = new Vector3(1f, 2f, 3f);
            var b = new Vector3(1f + 1e-7f, 2f, 3f);
            Assert.True(a == b);
            Assert.False(a.Equals(new Vector3(1.001f, 2f, 3f)));
        }

        [Fact]
        public void RoundToIntUsesBankersRounding()
        {
            Assert.Equal(2, Mathf.RoundToInt(2.5f));
            Assert.Equal(4, Mathf.RoundToInt(3.5f));
        }

        [Fact]
        public void RandomRangeIntExcludesMaxAndFloatIncludesBounds()
        {
            Random.InitState(1234);
            for (int i = 0; i < 1000; i++)
            {
                int v = Random.Range(0, 3);
                Assert.InRange(v, 0, 2);
                float f = Random.Range(1f, 2f);
                Assert.InRange(f, 1f, 2f);
            }
        }

        [Fact]
        public void HermiteCurveHitsKeysAndEaseInOutIsSmooth()
        {
            var c = AnimationCurve.EaseInOut(0f, 0f, 1f, 10f);
            Near(0f, c.Evaluate(0f));
            Near(5f, c.Evaluate(0.5f));
            Near(10f, c.Evaluate(1f));
            Near(10f, c.Evaluate(2f)); // clamped after the last key
        }

        [Fact]
        public void Color32RoundsAndClamps()
        {
            Color32 c = new Color(0.5f, 1.5f, -1f, 1f);
            Assert.Equal(128, c.r);
            Assert.Equal(255, c.g);
            Assert.Equal(0, c.b);
        }

        [Fact]
        public void RectContainsIsHalfOpen()
        {
            var r = new Rect(0, 0, 10, 10);
            Assert.True(r.Contains(new Vector2(0, 0)));
            Assert.False(r.Contains(new Vector2(10, 5)));
        }

        [Theory]
        [InlineData(1f, 0f, 0f)]
        [InlineData(0.2f, 0.8f, 0.4f)]
        [InlineData(0.1f, 0.3f, 0.9f)]
        [InlineData(0.9f, 0.1f, 0.6f)]
        [InlineData(0.5f, 0.5f, 0.2f)]
        public void HsvRoundTrips(float r, float g, float b)
        {
            Color.RGBToHSV(new Color(r, g, b), out float h, out float s, out float v);
            Assert.InRange(h, 0f, 1f);
            var back = Color.HSVToRGB(h, s, v);
            Near(r, back.r); Near(g, back.g); Near(b, back.b);
            Assert.Equal(1f, back.a);
        }

        [Fact]
        public void HsvEdgeCases()
        {
            // Grey: no saturation, the value is the grey level.
            Color.RGBToHSV(new Color(0.4f, 0.4f, 0.4f), out float h, out float s, out float v);
            Assert.Equal(0f, h); Assert.Equal(0f, s); Near(0.4f, v);
            Assert.Equal(new Color(0.4f, 0.4f, 0.4f, 1f), Color.HSVToRGB(0.7f, 0f, 0.4f));

            // Black: hue and saturation are 0.
            Color.RGBToHSV(Color.black, out h, out s, out v);
            Assert.Equal(0f, h); Assert.Equal(0f, s); Assert.Equal(0f, v);
            Assert.Equal(Color.black, Color.HSVToRGB(0.3f, 1f, 0f));

            // Red just below the blue end wraps to H near 1; H = 1 is red again.
            Color.RGBToHSV(new Color(1f, 0f, 0.5f), out h, out _, out _);
            Near(1f - 0.5f / 6f, h);
            var red = Color.HSVToRGB(1f, 1f, 1f);
            Near(1f, red.r); Near(0f, red.g); Near(0f, red.b);

            // Slightly negative H is the sector just below red.
            var magenta = Color.HSVToRGB(-0.1f, 1f, 1f);
            Near(1f, magenta.r); Near(0f, magenta.g); Near(0.6f, magenta.b);
            // Far outside the range: white.
            Assert.Equal(Color.white, Color.HSVToRGB(2f, 1f, 1f));

            // HDR values are kept unless clamped.
            Assert.Equal(2f, Color.HSVToRGB(0f, 0.5f, 2f).r);
            Assert.Equal(1f, Color.HSVToRGB(0f, 0.5f, 2f, false).r);
        }

        [Fact]
        public void SmoothDampConvergesWithoutOvershoot()
        {
            float x = 0f, velocity = 0f, previous = 0f;
            for (int i = 0; i < 200; i++)
            {
                x = Mathf.SmoothDamp(x, 10f, ref velocity, 0.3f, Mathf.Infinity, 1f / 60f);
                Assert.True(x <= 10f);
                Assert.True(x >= previous);
                previous = x;
            }
            Near(10f, x, 1e-3f);

            // A step long enough to pass the target lands on it and stops.
            velocity = 100f;
            Assert.Equal(10f, Mathf.SmoothDamp(9f, 10f, ref velocity, 0.3f, Mathf.Infinity, 0.5f));
            Assert.Equal(0f, velocity);

            // maxSpeed limits the distance the spring pulls over.
            velocity = 0f;
            float slow = Mathf.SmoothDamp(0f, 1000f, ref velocity, 1f, 1f, 0.1f);
            float fast = Mathf.SmoothDamp(0f, 1000f, ref velocity, 1f, Mathf.Infinity, 0.1f);
            Assert.True(slow < 0.1f && fast > slow);
        }

        [Fact]
        public void VectorSmoothDampConvergesWithoutOvershoot()
        {
            var target = new Vector3(10f, -5f, 2f);
            Vector3 p = Vector3.zero, velocity = Vector3.zero;
            float lastDistance = float.MaxValue;
            for (int i = 0; i < 200; i++)
            {
                p = Vector3.SmoothDamp(p, target, ref velocity, 0.25f, Mathf.Infinity, 1f / 60f);
                float d = Vector3.Distance(p, target);
                Assert.True(d <= lastDistance);
                lastDistance = d;
            }
            Assert.True(lastDistance < 1e-3f);

            var v2 = new Vector2(50f, 0f);
            Assert.Equal(new Vector2(1f, 1f), Vector2.SmoothDamp(new Vector2(0.9f, 0.9f), Vector2.one, ref v2, 0.3f, Mathf.Infinity, 0.5f));
            Assert.Equal(Vector2.zero, v2);
        }

        // An angle near 360 cannot come out of the decomposition (it yields -180..180), so the
        // wrap is checked through the private helper.
        static float WrapThroughEuler(float degrees) =>
            (float)typeof(Quaternion).GetMethod("WrapDegrees", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, [degrees]);

        [Fact]
        public void EulerAnglesKeepATolerance()
        {
            // Within 0.0001 rad below 0, an angle is not wrapped to just under 360.
            var e = Quaternion.Euler(0f, -0.001f, 0f).eulerAngles;
            Assert.InRange(e.y, -0.0011f, 0f);
            e = Quaternion.Euler(0f, -1f, 0f).eulerAngles;
            Near(359f, e.y, 1e-3f);
            // Within the tolerance below 360, an angle comes out slightly negative.
            Assert.Equal(-0.001f, WrapThroughEuler(359.999f), 3);
            Assert.Equal(359f, WrapThroughEuler(359f), 3);
        }

        [Fact]
        public void QuaternionEqualityUsesTheDotProduct()
        {
            var q = Quaternion.Euler(10f, 20f, 30f);
            Assert.True(q == Quaternion.Euler(10f, 20f, 30.01f));
            Assert.False(q == Quaternion.Euler(10f, 20f, 31f));
            Assert.True(q != Quaternion.identity);
        }

        // MiniBase's worldgen. Values sampled from Unity's Mathf.PerlinNoise in the original player
        // (to 4 decimals): the same noise, close enough for worldgen, not bit for bit.
        [Theory]
        [InlineData(0.13f, 0.29f, 0.4690f)]
        [InlineData(0.50f, 0.29f, 0.3934f)]
        [InlineData(0.87f, 0.29f, 0.3178f)]
        [InlineData(1.24f, 0.29f, 0.5787f)]
        [InlineData(1.3f, 2.7f, 0.5702f)]
        [InlineData(-1.3f, -2.7f, 0.5702f)]
        [InlineData(257.3f, 2.7f, 0.5702f)]
        [InlineData(3f, 7f, 0.4653f)]
        public void PerlinNoiseIsUnitysNoise(float x, float y, float unity)
        {
            Assert.InRange(Mathf.PerlinNoise(x, y), unity - 0.0002f, unity + 0.0002f);
        }
    }
}
