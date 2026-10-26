using OniMoono.Engine;
using Xunit;

namespace OniMoono.Tests
{
    public class MonoStringHashTests
    {
        // string.GetHashCode in the original player (tools/UnityProbe, VAL strhash).
        [Theory]
        [InlineData("", 371857150)]
        [InlineData("a", 372029373)]
        [InlineData("ab", 1093630535)]
        [InlineData("abc", 1099313834)]
        [InlineData("abcd", -1664220650)]
        [InlineData("SemanticsProbe", 692750779)]
        [InlineData("onimaxxing-sim", 470071606)]
        [InlineData("Ünïcødé 漢字", -1476450740)]
        [InlineData("nul\0after", -1985799593)]
        [InlineData("a\0", 372029373)]
        [InlineData("\0", 371857150)]
        public void MatchesUnitysMono(string text, int expected) => Assert.Equal(expected, MonoStringHash.Compute(text));
    }
}
