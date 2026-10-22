using System.Globalization;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class RandomValuesTests
    {
        // The original's numbers as Mono printed them ("R"); compared as floats, since Mono's and
        // CoreCLR's shortest round-trip texts differ.
        static void Same(string expected, params float[] actual)
        {
            var parts = expected.Split(' ');
            Assert.Equal(parts.Length, actual.Length);
            for (int i = 0; i < parts.Length; i++) Assert.Equal(float.Parse(parts[i], CultureInfo.InvariantCulture), actual[i]);
        }

        /// Each value after InitState(42), as the original gives it (tools/UnityProbe, "values: random").
        [Fact]
        public void ValuesAfterASeedAreTheOriginals()
        {
            Random.InitState(42); Same("0.000857710955", Random.value);
            Random.InitState(42); Same("0.9991423", Random.Range(0f, 1f));
            Random.InitState(42); Same("9.993138", Random.Range(2f, 10f));
            Random.InitState(42); Same("2.00686169", Random.Range(10f, 2f));
            Random.InitState(42); Assert.Equal(683, Random.Range(0, 1000));
            Random.InitState(42); var c = Random.insideUnitCircle; Same("0.763039052 -0.004112087", c.x, c.y);
            Random.InitState(42); var q = Random.rotation; Same("0.922639549 0.152026743 -0.325961918 0.139186144", q.x, q.y, q.z, q.w);
            Random.InitState(42); var k = Random.ColorHSV(); Same("0.6763434 0.39525196 0.3937979 1", k.r, k.g, k.b, k.a);
        }
    }
}
