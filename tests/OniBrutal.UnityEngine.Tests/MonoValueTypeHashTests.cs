using OniBrutal.Engine;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class MonoValueTypeHashTests
    {
        enum Element { Oxygen = -1528777920, Methane = -1565447376 }

        // The shape of the game's BubbleManager.Archetype: no GetHashCode override.
        readonly struct Archetype
        {
            public readonly Vector2 velocity;
            public readonly Element element;
            public readonly float alphaFadeSpeed;

            public Archetype(Vector2 velocity, Element element)
            {
                this.velocity = velocity;
                this.element = element;
                alphaFadeSpeed = velocity.magnitude;
            }
        }

        struct Inner { public float a; public int b; }

        struct Mixed
        {
            public int count;
            public string name;
            public Inner inner;
            public bool flag;
            public object reference;
            public static int ignored = 77;
        }

        struct Empty { }

        [Fact]
        public void HashesEveryFieldWhereCoreClrHashesTheFirst()
        {
            var oxygen = new Archetype(Vector2.up, Element.Oxygen);
            var methane = new Archetype(Vector2.up, Element.Methane);
            // The bug (ARCHITECTURE D26): CoreCLR's ValueType.GetHashCode takes the velocity alone.
            Assert.Equal(oxygen.GetHashCode(), methane.GetHashCode());
            Assert.NotEqual(MonoValueTypeHash.Compute(oxygen), MonoValueTypeHash.Compute(methane));
        }

        [Fact]
        public void XorsTheFieldsHashes()
        {
            var value = new Archetype(new Vector2(0.5f, 2f), Element.Methane);
            int expected = new Vector2(0.5f, 2f).GetHashCode() ^ (int)Element.Methane ^ value.alphaFadeSpeed.GetHashCode();
            Assert.Equal(expected, MonoValueTypeHash.Compute(value));
        }

        [Fact]
        public void FollowsMonosRulePerFieldKind()
        {
            var reference = new object();
            var value = new Mixed { count = 12345, name = "Methane", inner = new Inner { a = 1.5f, b = 7 }, flag = true, reference = reference };
            // int: its value; string: mono_string_hash_internal; a struct without an override: its
            // own Mono hash; others: their GetHashCode. Static fields are left out.
            uint stringHash = 0;
            foreach (char c in "Methane") stringHash = stringHash * 31 + c;
            int inner = 1.5f.GetHashCode() ^ 7;
            int expected = 12345 ^ (int)stringHash ^ inner ^ true.GetHashCode() ^ reference.GetHashCode();
            Assert.Equal(expected, MonoValueTypeHash.Compute(value));
        }

        [Fact]
        public void SkipsNullFields() =>
            Assert.Equal(3, MonoValueTypeHash.Compute(new Mixed { count = 3 }));

        [Fact]
        public void StartsFromZero() => Assert.Equal(0, MonoValueTypeHash.Compute(new Empty()));

        [Theory]
        [InlineData("", 0u)]
        [InlineData("a", 97u)]
        [InlineData("ab", 97u * 31 + 98)]
        [InlineData("Ü漢", 0xDCu * 31 + 0x6F22)]
        public void StringFieldsUseMonosNativeStringHash(string text, uint expected) =>
            Assert.Equal(expected, MonoValueTypeHash.StringHash(text));
    }
}
