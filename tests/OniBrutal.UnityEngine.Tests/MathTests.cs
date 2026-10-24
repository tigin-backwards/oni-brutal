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

        static float[] Bits(string hex) => System.Array.ConvertAll(hex.Split(','), h => System.BitConverter.Int32BitsToSingle(System.Convert.ToInt32(h, 16)));

        static Matrix4x4 RowMajor(string hex)
        {
            var v = Bits(hex);
            var m = new Matrix4x4();
            for (int i = 0; i < 16; i++) m[i / 4, i % 4] = v[i];
            return m;
        }

        static void SameBits(string expected, params float[] actual)
            => Assert.Equal(expected, string.Join(",", System.Array.ConvertAll(actual, f => System.BitConverter.SingleToInt32Bits(f).ToString("x8"))));

        /// Cases from the original player (tools/UnityProbe MatrixMath.cs, PROBE_SCENARIO=matmul,
        /// float bits in hex, matrices row by row): Unity's Matrix4x4 sums each element's products
        /// in double and rounds once (its Mono runs with float32 off, ARCHITECTURE D25). All 400
        /// probe cases of each product agree with that; rounding every operation matched none of
        /// the matrix products and a third of the others. These are cases where it differs.
        [Theory]
        [InlineData("c0ea1718,446b81b0,4205dd98,428560c8,bb8947df,bd998c1a,bef3801e,3d266c0f,3a3db3b1,c2821c7a,444860fb,c184a823,c2007e82,3c4c8028,406c149e,bb1d95b1",
            "c0de2600,3f6694d8,4092a2ef,3d802094,bdc923eb,41f8cd3a,3d38fe84,c3052ce3,c0db7732,3ef8e251,bb90d494,420b78e7,3dfcbd9e,bcec09e9,bf29aeac,bcdde273",
            "3da80b54,c0819333,c3f77526", "3b9f7568,3d9549c2,44647266,bc0a9915",
            "c69eaed3,436bb813,c8c19269", "4131c008,be04057d,4358d4d5", "c69f3434,436badac,c8c19057", "46ef727e,c3d94baa,4932cfa1,4552a95f",
            "c3838393,46e4f1f3,c20d4e11,c7f2c105,40536748,c0245789,bd4233c7,c0d32e93,c5aba58f,c4cc24b4,40906e70,470f038b,4345b4b9,c1d6054d,c313377b,42f9e55c")]
        [InlineData("c2067afb,c089b09b,bbaf28bc,c37372c2,40ab36d7,41683bc3,c0b7995d,3a2fdd3c,bf87102f,3b92fa91,be4a3421,b9d2bc9d,3b245ded,bccda395,bb8aa3d7,bf7fb611",
            "bc20bafc,bc1c68a1,3c1e1d3c,3e43a50d,3ab4c566,c34d9392,c0d48614,3f0222eb,c3b75129,3f39c83a,410d08f3,bec04352,bd91a366,39a6a338,41cff0ce,bd03d975",
            "3d29a1c1,3d0acc92,bdaea718", "3fddc07f,c3208463,40c9d3ef,3e928abc",
            "c374fc68,3f9a08c4,bcde14df", "43752afe,bf9a260e,3cde3f1a", "bfc4d330,3f99f2c8,bcdac9ed", "440caded,c5134c20,c072c9e2,406e2717",
            "419cc469,445d32ed,c5c4dd27,bf45adef,45037851,c53ac12a,c312e6d2,4128e1e8,4290d0b0,bf871507,bfe53fba,be00298f,3fcfa3ca,40a50695,c1ceaba6,3cafce7b")]
        [InlineData("bf17bbff,3da2c4a7,411df5fe,bf29f71b,bbfd95bd,3c8032f6,3c18bb72,429f77d9,bba9e266,3eeb6720,43026e66,c08e1bb6,00000000,00000000,00000000,3f800000",
            "be0d4a2f,bb3a2dda,bd2e179b,3f053297,bf2f65d2,3bc9fcce,420ed941,4265842d,c2bfc675,be008ae7,bea21b37,3dc8b673,00000000,00000000,00000000,3f800000",
            "44170cd3,42a73ab1,c3522351", "4262ef35,43a48346,3d846ba1,3f025f0c",
            "c517abc0,4294d0d7,c6d5e35e", "c517abc0,4294d0d7,c6d5e35e", "c517a121,c0aa7020,c6d5da7d", "c0e5a6ea,423543c3,431d277c,3f025f0c",
            "c46ca83a,bf9e5a2e,be864df7,4091c6ec,bf674d36,ba89daab,3f0e6641,42a14200,c6436c38,c182f61b,c1c7037b,420ae1c6,00000000,00000000,00000000,3f800000")]
        public void MatrixProductsRoundOnceAsUnitysDo(string m, string n, string p, string q, string point3x4, string point, string vector, string mq, string mn)
        {
            var a = RowMajor(m);
            var b = RowMajor(n);
            var pv = Bits(p);
            var qv = Bits(q);
            var P = new Vector3(pv[0], pv[1], pv[2]);
            var r1 = a.MultiplyPoint3x4(P);
            SameBits(point3x4, r1.x, r1.y, r1.z);
            var r2 = a.MultiplyPoint(P);
            SameBits(point, r2.x, r2.y, r2.z);
            var r3 = a.MultiplyVector(P);
            SameBits(vector, r3.x, r3.y, r3.z);
            var r4 = a * new Vector4(qv[0], qv[1], qv[2], qv[3]);
            SameBits(mq, r4.x, r4.y, r4.z, r4.w);
            var r5 = a * b;
            var e = new float[16];
            for (int i = 0; i < 16; i++) e[i] = r5[i / 4, i % 4];
            SameBits(mn, e);
        }
    }
}
