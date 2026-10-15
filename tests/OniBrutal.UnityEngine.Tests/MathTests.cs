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
    }
}
