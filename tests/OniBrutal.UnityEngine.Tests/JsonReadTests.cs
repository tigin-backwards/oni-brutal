using System;
using System.Collections.Generic;
using UnityEngine;
using Xunit;

#pragma warning disable CS0649 // set by JsonUtility
namespace OniBrutal.Tests
{
    /// JsonUtility.FromJson and FromJsonOverwrite against the original player's results
    /// (PerfProbe's measure mode, "measure json").
    public class JsonReadTests : EngineTest
    {
        public enum Kind { A, B, C }

        [Serializable]
        public class Data
        {
            public int i;
            public float f;
            public double d;
            public long l;
            public string s = "init";
            public bool b;
            public Kind e;
            public List<int> list;
            public int[] arr;
            public Vector3 v;
            public Color c;
            [SerializeField] int hidden;
            [NonSerialized] public int skipped = 5;
            public int Hidden => hidden;
        }

        [Fact]
        public void ReadsEveryKindOfField()
        {
            var d = JsonUtility.FromJson<Data>("{\"i\":5,\"f\":1.5,\"d\":2.25,\"l\":9000000000,\"s\":\"hi\\nthere \\u00e9\",\"b\":true,\"e\":2,\"list\":[1,2],\"arr\":[3],\"v\":{\"x\":1,\"y\":2,\"z\":3},\"c\":{\"r\":1,\"g\":0.5,\"b\":0,\"a\":1},\"hidden\":9,\"skipped\":11,\"extra\":{\"deep\":[1,{\"k\":2}]}}");
            Assert.Equal((5, 1.5f, 2.25, 9000000000L, "hi\nthere é", true, Kind.C), (d.i, d.f, d.d, d.l, d.s, d.b, d.e));
            Assert.Equal(new[] { 1, 2 }, d.list);
            Assert.Equal(new[] { 3 }, d.arr);
            Assert.Equal(new Vector3(1, 2, 3), d.v);
            Assert.Equal(new Color(1, 0.5f, 0, 1), d.c);
            Assert.Equal((9, 5), (d.Hidden, d.skipped));
        }

        [Fact]
        public void MissingMembersKeepValuesAndListsStartEmpty()
        {
            var d = JsonUtility.FromJson<Data>("{}");
            Assert.Equal("init", d.s);
            Assert.Empty(d.list);
            Assert.Null(d.arr);
        }

        [Fact]
        public void MismatchedMembersStillSetTheField()
        {
            var d = new Data { i = 7, f = 3, s = "keep", b = true, e = Kind.C, l = 6, list = new List<int> { 9 } };
            JsonUtility.FromJsonOverwrite("{\"f\":\"x\",\"e\":\"B\",\"i\":true,\"s\":null,\"b\":\"no\",\"list\":5,\"l\":\"12\"}", d);
            Assert.Equal((0, 0f, "", false, Kind.A, 12L), (d.i, d.f, d.s, d.b, d.e, d.l));
            Assert.Empty(d.list);
            var n = JsonUtility.FromJson<Data>("{\"i\":5.7,\"l\":1e3,\"f\":1e40,\"s\":5,\"b\":1}");
            Assert.Equal((5, 1000L, float.PositiveInfinity, "5", true), (n.i, n.l, n.f, n.s, n.b));
            Assert.Equal(int.MinValue, JsonUtility.FromJson<Data>("{\"i\":2147483648}").i);
            Assert.Equal(1, JsonUtility.FromJson<Data>("{\"i\":1,\"i\":2}").i);
        }

        [Theory]
        [InlineData("   ", "JSON parse error: The document is empty.")]
        [InlineData("[1,2]", "JSON must represent an object type.")]
        [InlineData("{\"i\":1", "JSON parse error: Missing a comma or '}' after an object member.")]
        [InlineData("{\"i\":}", "JSON parse error: Invalid value.")]
        [InlineData("{\"i\":1,}", "JSON parse error: Missing a name for object member.")]
        [InlineData("{\"i\":1} x", "JSON parse error: The document root must not follow by other values.")]
        [InlineData("5", "JSON must represent an object type.")]
        public void MalformedInputThrowsUnitysMessages(string json, string message)
        {
            Assert.Equal(message, Assert.Throws<ArgumentException>(() => JsonUtility.FromJson<Data>(json)).Message);
        }

        [Fact]
        public void EmptyInputAndEngineTypes()
        {
            Assert.Null(JsonUtility.FromJson<Data>(""));
            Assert.Null(JsonUtility.FromJson<Data>(null));
            Assert.Equal("Cannot deserialize JSON to new instances of type 'Behaviour.'",
                Assert.Throws<ArgumentException>(() => JsonUtility.FromJson<Behaviour>("{\"x\":1}")).Message);
        }

        public class Behaviour : MonoBehaviour { public int x; }
    }
}
