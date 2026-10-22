using TMPro;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    // TMPro_ExtensionMethods' colour helpers (Paint Your Pipes multiplies pipe colours).
    public class TmpExtensionMethodsTests
    {
        [Fact]
        public void MultiplyDividesEveryChannelByInteger()
        {
            var m = new Color32(1, 1, 128, 255).Multiply(new Color32(255, 128, 128, 255));
            Assert.Equal(new Color32(1, 0, 64, 255), m);
            // Alpha is multiplied too.
            Assert.Equal(new Color32(255, 255, 255, 1), new Color32(255, 255, 255, 1).Multiply(new Color32(255, 255, 255, 255)));
            Assert.Equal(new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 1).Multiply(new Color32(255, 255, 255, 128)));
            Assert.Equal(new Color32(255, 255, 255, 64), new Color32(255, 255, 255, 128).Multiply(new Color32(255, 255, 255, 128)));
        }

        [Fact]
        public void TintWithColourIsMultiply()
        {
            var a = new Color32(1, 128, 200, 128);
            var b = new Color32(255, 128, 77, 128);
            Assert.Equal(a.Multiply(b), a.Tint(b));
            Assert.Equal(new Color32(1, 64, 60, 64), a.Tint(b));
        }

        [Fact]
        public void MinAlphaKeepsFirstRgb()
        {
            var c1 = new Color(0.1f, 0.2f, 0.3f, 0.9f);
            var c2 = new Color(0.7f, 0.8f, 0.6f, 0.4f);
            var m = c1.MinAlpha(c2);
            Assert.Equal((0.1f, 0.2f, 0.3f, 0.4f), (m.r, m.g, m.b, m.a));
            m = c2.MinAlpha(c1);
            Assert.Equal((0.7f, 0.8f, 0.6f, 0.4f), (m.r, m.g, m.b, m.a));
        }

        [Fact]
        public void CompareIsExactAndRgbIgnoresAlpha()
        {
            var k1 = new Color32(10, 20, 30, 40);
            var k2 = new Color32(10, 20, 30, 41);
            var k3 = new Color32(10, 21, 30, 40);
            Assert.True(k1.Compare(k1));
            Assert.False(k1.Compare(k2));
            Assert.False(k1.Compare(k3));
            Assert.True(k1.CompareRGB(k2));
            Assert.False(k1.CompareRGB(k3));

            var f1 = new Color(0.5f, 0.25f, 0.75f, 1f);
            var f2 = new Color(0.5f, 0.25f, 0.75f, 0.5f);
            var f3 = new Color(0.5f, 0.25f + 1e-7f, 0.75f, 1f);
            Assert.NotEqual(f1.g, f3.g);
            Assert.True(f1.Compare(f1));
            Assert.False(f1.Compare(f2));
            Assert.False(f1.Compare(f3));
            Assert.True(f1.CompareRGB(f2));
            Assert.False(f1.CompareRGB(f3));
        }
    }
}
