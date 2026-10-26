using System;
using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    public class JsonUtilityTests : EngineTest
    {
        // Shaped like the game's PerformanceCaptureMonitor.PerformanceCaptureData: the root
        // needs no [Serializable], nested element types do.
        public class Capture
        {
            [Serializable]
            public class Info
            {
                public string name;
                public int count;
            }

            public uint Revision;
            public string Patch;
            public bool IsBaseGame;
            public string[] Dlcs;
            public List<string> Tags;
            public List<Info> Brains;
            public float Time;
            public double Precise;
            public Info Single;
            private int hidden = 7;
            [SerializeField] private int shown = 3;
            [NonSerialized] public int skipped = 9;
            public Dictionary<string, int> notSerializable = new();
            public int Hidden => hidden + shown;
        }

        public enum Mode { A, B = 5 }

        [Serializable]
        public struct Mixed
        {
            public Vector3 v;
            public Color c;
            public Rect r;
            public LayerMask mask;
            public Mode mode;
            public Transform target;
        }

        public class Holder : MonoBehaviour
        {
            public int value = 4;
            public Transform self;
        }

        [Fact]
        public void WritesFieldsInOrderCompact()
        {
            var data = new Capture
            {
                Revision = 744825,
                Patch = "",
                IsBaseGame = true,
                Dlcs = ["EXPANSION1_ID"],
                Tags = new List<string> { "release" },
                Brains = new List<Capture.Info> { new() { name = "Dupe", count = 3 } },
                Time = 12.5f,
                Precise = 0.1,
            };
            Assert.Equal(
                "{\"Revision\":744825,\"Patch\":\"\",\"IsBaseGame\":true,\"Dlcs\":[\"EXPANSION1_ID\"],\"Tags\":[\"release\"]," +
                "\"Brains\":[{\"name\":\"Dupe\",\"count\":3}],\"Time\":12.5,\"Precise\":0.1,\"Single\":{\"name\":\"\",\"count\":0},\"shown\":3}",
                JsonUtility.ToJson(data));
        }

        [Fact]
        public void PrettyPrintIndentsWithFourSpaces()
        {
            var data = new Capture { Tags = new List<string> { "a", "b" } };
            Assert.Equal(
                "{\n    \"Revision\": 0,\n    \"Patch\": \"\",\n    \"IsBaseGame\": false,\n    \"Dlcs\": [],\n    \"Tags\": [\n        \"a\",\n        \"b\"\n    ],\n" +
                "    \"Brains\": [],\n    \"Time\": 0.0,\n    \"Precise\": 0.0,\n    \"Single\": {\n        \"name\": \"\",\n        \"count\": 0\n    },\n    \"shown\": 3\n}",
                JsonUtility.ToJson(data, true));
        }

        // Outputs of Unity's player (tools/UnityProbe), where its digit generation is well-defined.
        [Theory]
        [InlineData(1f, "1.0")]
        [InlineData(0.1f, "0.10000000149011612")]
        [InlineData(0.4f, "0.4000000059604645")]
        [InlineData(-2.5f, "-2.5")]
        [InlineData(100000f, "100000.0")]
        [InlineData(1.5e-7f, "1.500000053056283e-7")]
        [InlineData(123.456f, "123.45600128173828")]
        [InlineData(1e-45f, "1.401298464324817e-45")]
        [InlineData(16777216f, "16777216.0")]
        [InlineData(2.71828f, "2.718280076980591")]
        [InlineData(-0f, "0.0")]
        public void FloatsAreWrittenAsDoublesWithRapidJsonDigits(float value, string expected)
            => Assert.Equal(expected, OniMoono.Engine.Serialization.JsonWriter.FormatFloat(value));

        [Theory]
        [InlineData(0.1, "0.1")]
        [InlineData(1e21, "1e21")]
        [InlineData(1e22, "1e22")]
        [InlineData(5e-324, "5e-324")]
        [InlineData(1.7976931348623157e308, "1.7976931348623157e308")]
        [InlineData(0.3333333333333333, "0.3333333333333333")]
        [InlineData(9007199254740992.0, "9007199254740992.0")]
        [InlineData(-0.0, "0.0")]
        public void DoublesUseRapidJsonDigits(double value, string expected)
            => Assert.Equal(expected, OniMoono.Engine.Serialization.JsonWriter.FormatDouble(value));

        [Fact]
        public void BuiltinStructsEnumsAndReferences()
        {
            var go = new GameObject("t");
            var m = new Mixed { v = new Vector3(1f, 2f, 3f), c = Color.red, r = new Rect(0f, 1f, 2f, 3f), mask = 5, mode = Mode.B, target = go.transform };
            Assert.Equal(
                "{\"v\":{\"x\":1.0,\"y\":2.0,\"z\":3.0},\"c\":{\"r\":1.0,\"g\":0.0,\"b\":0.0,\"a\":1.0}," +
                "\"r\":{\"serializedVersion\":\"2\",\"x\":0.0,\"y\":1.0,\"width\":2.0,\"height\":3.0}," +
                "\"mask\":{\"serializedVersion\":\"2\",\"m_Bits\":5},\"mode\":5,\"target\":{\"instanceID\":" + go.transform.GetInstanceID() + "}}",
                JsonUtility.ToJson(m));
        }

        [Fact]
        public void MonoBehaviourWritesOnlyScriptFields()
        {
            var h = new GameObject("h").AddComponent<Holder>();
            Assert.Equal("{\"value\":4,\"self\":{\"instanceID\":0}}", JsonUtility.ToJson(h));
        }

        [Fact]
        public void EscapesLikeRapidJson()
        {
            var info = new Capture.Info { name = "a\"b\\c/d\n\u0001é" };
            Assert.Equal("{\"name\":\"a\\\"b\\\\c/d\\n\\u0001é\",\"count\":0}", JsonUtility.ToJson(info));
        }

        [Fact]
        public void NullPrimitivesAndEngineTypes()
        {
            Assert.Equal("", JsonUtility.ToJson(null));
            Assert.Equal("{}", JsonUtility.ToJson(5));
            Assert.Throws<ArgumentException>(() => JsonUtility.ToJson(new GameObject("g")));
        }
    }
}
