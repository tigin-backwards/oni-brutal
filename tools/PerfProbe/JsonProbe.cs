using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#pragma warning disable CS0649 // set by JsonUtility
namespace PerfProbe
{
    [Serializable]
    public class JsonTopLevelNested { public int x; }

    [Serializable]
    public class JsonFieldKinds
    {
        public JsonTopLevelNested top;
        public Vector2I cell;
        public List<string> names;
        public override string ToString() => $"top={(top == null ? "null" : top.x.ToString())} cell={cell} names={(names == null ? "null" : string.Join("|", names))}";
    }

    /// TEST INFRASTRUCTURE: JsonUtility.FromJson and FromJsonOverwrite measured (mods read their
    /// settings with them): types, missing and extra keys, mismatches, malformed input.
    static class JsonProbe
    {
        public enum Kind { A, B, C }

        [Serializable]
        public class Nested { public int x; public string name; }

        [Serializable]
        public struct Pair { public int a; public float b; }

        [Serializable]
        public class Data
        {
            public int i;
            public float f;
            public double d;
            public long l;
            public char ch;
            public string s = "init";
            public string untouched = "keep";
            public bool b;
            public Kind e;
            public List<int> list;
            public int[] arr;
            public Nested nested;
            public List<Nested> nestedList;
            public Vector3 v;
            public Color c;
            public Pair pair;
            [SerializeField] int hidden;
            [NonSerialized] public int skipped = 5;
            public int Prop { get; set; }
            public Dictionary<string, int> dict;

            public override string ToString() =>
                $"i={i} f={f:R} d={d:R} l={l} ch={(int)ch} s={Q(s)} untouched={Q(untouched)} b={b} e={e} list={L(list)} arr={L(arr)} nested={(nested == null ? "null" : $"({nested.x},{Q(nested.name)})")} nestedList={(nestedList == null ? "null" : string.Join("|", nestedList.Select(n => n == null ? "null" : $"({n.x},{Q(n.name)})")))} v={v} c={c} pair=({pair.a},{pair.b:R}) hidden={hidden} skipped={skipped} Prop={Prop} dict={(dict == null ? "null" : dict.Count.ToString())}";
        }

        public class Behaviour : MonoBehaviour { public int x; }

        static string Q(string s) => s == null ? "null" : "\"" + s.Replace("\n", "\\n") + "\"";
        static string L<T>(IEnumerable<T> e) => e == null ? "null" : "[" + string.Join(",", e) + "]";

        static void Try(string name, Func<object> f)
        {
            try
            {
                var r = f();
                Debug.Log($"PERF measure json {name}: {(r == null ? "null" : r.ToString())}");
            }
            catch (Exception e)
            {
                Debug.Log($"PERF measure json {name}: {e.GetType().Name}: {e.Message.Replace("\n", " ")}");
            }
        }

        public static void Measure()
        {
            Try("full", () => JsonUtility.FromJson<Data>("{\"i\":5,\"f\":1.5,\"d\":2.25,\"l\":9000000000,\"ch\":65,\"s\":\"hi\\nthere \\u00e9\",\"b\":true,\"e\":2,\"list\":[1,2],\"arr\":[3],\"nested\":{\"x\":7,\"name\":\"n\"},\"nestedList\":[{\"x\":1},{\"name\":\"z\"}],\"v\":{\"x\":1,\"y\":2,\"z\":3},\"c\":{\"r\":1,\"g\":0.5,\"b\":0,\"a\":1},\"pair\":{\"a\":4,\"b\":0.25},\"hidden\":9,\"skipped\":11,\"Prop\":3,\"dict\":{\"a\":1},\"extra\":{\"deep\":[1,{\"k\":2}]}}"));
            Try("empty-object", () => JsonUtility.FromJson<Data>("{}"));
            Try("mismatch", () => JsonUtility.FromJson<Data>("{\"i\":\"5\",\"f\":\"x\",\"s\":5,\"b\":1,\"list\":5,\"nested\":3,\"e\":\"B\"}"));
            Try("numbers", () => JsonUtility.FromJson<Data>("{\"i\":5.7,\"l\":1e3,\"f\":1e40,\"d\":-0.0}"));
            Try("int-overflow", () => JsonUtility.FromJson<Data>("{\"i\":2147483648}"));
            Try("nulls", () => JsonUtility.FromJson<Data>("{\"s\":null,\"nested\":null,\"list\":null,\"arr\":null}"));
            Try("bool-strings", () => JsonUtility.FromJson<Data>("{\"b\":\"true\"}"));
            Try("whitespace-and-case", () => JsonUtility.FromJson<Data>(" { \"I\" : 3 , \"i\" : 4 } "));
            Try("duplicate-key", () => JsonUtility.FromJson<Data>("{\"i\":1,\"i\":2}"));
            Try("struct", () => { var p = JsonUtility.FromJson<Pair>("{\"a\":1,\"b\":2.5}"); return $"({p.a},{p.b:R})"; });
            Try("null-input", () => JsonUtility.FromJson<Data>(null));
            Try("empty-input", () => JsonUtility.FromJson<Data>(""));
            Try("blank-input", () => JsonUtility.FromJson<Data>("   "));
            Try("array-input", () => JsonUtility.FromJson<Data>("[1,2]"));
            Try("truncated", () => JsonUtility.FromJson<Data>("{\"i\":1"));
            Try("missing-value", () => JsonUtility.FromJson<Data>("{\"i\":}"));
            Try("trailing-comma", () => JsonUtility.FromJson<Data>("{\"i\":1,}"));
            Try("trailing-text", () => JsonUtility.FromJson<Data>("{\"i\":1} x"));
            Try("single-quotes", () => JsonUtility.FromJson<Data>("{'i':1}"));
            Try("comment", () => JsonUtility.FromJson<Data>("{/*c*/\"i\":1}"));
            Try("primitive-int", () => JsonUtility.FromJson<int>("5"));
            Try("primitive-string", () => JsonUtility.FromJson<string>("\"x\""));
            Try("type-overload", () => JsonUtility.FromJson("{\"i\":8}", typeof(Data)));
            Try("type-null", () => JsonUtility.FromJson("{\"i\":8}", null));
            Try("behaviour", () => JsonUtility.FromJson<Behaviour>("{\"x\":1}"));
            Try("overwrite", () => { var d = new Data { i = 1, s = "old" }; d.list = new List<int> { 9 }; JsonUtility.FromJsonOverwrite("{\"s\":\"new\"}", d); return d; });
            Try("overwrite-null-object", () => { JsonUtility.FromJsonOverwrite("{\"i\":1}", null); return "returned"; });
            Try("overwrite-behaviour", () => { var go = new GameObject("PerfJson"); var b = go.AddComponent<Behaviour>(); JsonUtility.FromJsonOverwrite("{\"x\":4}", b); var x = b.x; UnityEngine.Object.Destroy(go); return x; });
            Try("field-kinds", () => JsonUtility.FromJson<JsonFieldKinds>("{\"top\":{\"x\":3},\"cell\":{\"x\":4,\"y\":5},\"names\":[\"a\",null,\"b\"]}"));
            Try("field-kinds-empty", () => JsonUtility.FromJson<JsonFieldKinds>("{}"));
            Try("negative-zero", () => { var d = JsonUtility.FromJson<Data>("{\"d\":-0.0,\"f\":-0.0}"); return $"d={d.d.ToString("R")} f={d.f.ToString("R")} 1/d={1 / d.d}"; });
            Try("tojson-mod-types", () => JsonUtility.ToJson(new JsonFieldKinds { top = new JsonTopLevelNested { x = 2 }, cell = new Vector2I(1, 2), names = new List<string> { "a", null } }));
            Try("tojson-data", () => JsonUtility.ToJson(new Data { i = 1, nested = new Nested { x = 2 }, pair = new Pair { a = 3 } }));
            Try("overwrite-mismatch", () => { var d = new Data { i = 7, f = 3, s = "keep", b = true, e = Kind.C, l = 6 }; d.list = new List<int> { 9 }; JsonUtility.FromJsonOverwrite("{\"f\":\"x\",\"e\":\"B\",\"i\":true,\"s\":null,\"b\":\"no\",\"list\":5,\"l\":\"12\"}", d); return d; });
            Try("overwrite-floats", () => { var d = new Data { f = 3, d = 4, i = 5 }; JsonUtility.FromJsonOverwrite("{\"f\":\"2.5\",\"d\":true,\"i\":\" 6\"}", d); return d; });
            Try("struct-empty", () => { var p = JsonUtility.FromJson<Pair>(""); return $"({p.a},{p.b})"; });
            Try("roundtrip", () => JsonUtility.FromJson<Data>(JsonUtility.ToJson(new Data { i = 3, f = 0.1f, s = "r", list = new List<int> { 1 }, nested = new Nested { x = 2 } })));
        }
    }
}
