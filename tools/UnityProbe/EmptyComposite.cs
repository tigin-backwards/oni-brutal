// What Instantiate gives for a MonoBehaviour's field of a [Serializable] class that has no field
// Unity serializes, lines "PROBE ... EC ...". Rephysicalized's FueledDietController keeps its diet
// (a [Serializable] class whose state is in private fields, get-only auto properties and
// dictionaries, without a parameterless constructor) in a public field; its prefab has one, and
// each instance's OnSpawn takes the diet from a registry only when the field is null. Classes
// with and without serialized fields, with and without a parameterless constructor, a null field
// and one not null; for each: whether the copy's field is null, the source's object itself, and
// what the copy's object holds. A `EmptyCompositeProbeDone` canvas appears at the end.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SemanticsProbe
{
    [Serializable]
    public class EcEmpty
    {
        readonly Dictionary<string, float> table;
        public IReadOnlyList<int> List { get; private set; }
        public bool Flag { get; private set; }
        public EcEmpty(int n) { table = new Dictionary<string, float> { ["a"] = n }; List = new List<int> { n }; Flag = true; }
        public string Show() => "table=" + (table == null ? "null" : table.Count.ToString()) + " list=" + (List == null ? "null" : List.Count.ToString()) + " flag=" + Flag;
    }

    [Serializable]
    public class EcEmptyCtor
    {
        public static int Made;
        readonly Dictionary<string, float> table = new Dictionary<string, float>();
        public EcEmptyCtor() { Made++; }
        public string Show() => "table=" + (table == null ? "null" : table.Count.ToString());
    }

    [Serializable]
    public class EcOne
    {
        public int Value;
        readonly Dictionary<string, float> table;
        public EcOne(int n) { Value = n; table = new Dictionary<string, float>(); }
        public string Show() => "value=" + Value + " table=" + (table == null ? "null" : table.Count.ToString());
    }

    [Serializable]
    public struct EcStruct
    {
        public int Value;
    }

    [Serializable]
    public class EcDefault
    {
        public int Value = 3;
    }

    public class EcHolder : MonoBehaviour
    {
        public int number;
        public string text;
        public List<int> ints;
        public EcStruct structure;
        public EcDefault withDefault;
        public AttackEffect gameClass;
        public AttackEffect gameClassNull;
        public List<EcOne> listOfMod;
        public EcEmpty empty;
        public EcEmpty emptyNull;
        public EcEmptyCtor emptyCtor;
        public EcEmptyCtor emptyCtorNull;
        public EcOne one;
        public EcOne oneNull;
    }

    public class EmptyComposite : MonoBehaviour
    {
        static void L(string s) => Rec.Log("EC " + s);
        int frame;

        static void Show(string label, object src, object copy, Func<object, string> show)
        {
            L(label + " copyNull=" + (copy == null) + " same=" + ReferenceEquals(src, copy) + " srcNull=" + (src == null)
                + (copy != null ? " copy " + show(copy) : "") + (src != null ? " src " + show(src) : ""));
        }

        void Start()
        {
            L("begin");
            var go = new GameObject("EcSource");
            go.SetActive(false);
            var h = go.AddComponent<EcHolder>();
            L("after AddComponent: empty=" + (h.empty == null) + " emptyNull=" + (h.emptyNull == null) + " emptyCtor=" + (h.emptyCtor == null) + " one=" + (h.one == null));
            h.empty = new EcEmpty(4);
            h.emptyNull = null;
            h.emptyCtor = new EcEmptyCtor();
            h.emptyCtorNull = null;
            h.one = new EcOne(7);
            h.oneNull = null;
            h.number = 5;
            h.text = "t";
            h.ints = new List<int> { 1, 2 };
            h.structure = new EcStruct { Value = 9 };
            h.withDefault = new EcDefault { Value = 8 };
            h.gameClass = new AttackEffect("x", 0.5f);
            h.gameClassNull = null;
            h.listOfMod = new List<EcOne> { new EcOne(1), null };
            EcEmptyCtor.Made = 0;
            var c = Instantiate(go).GetComponent<EcHolder>();
            L("ctor calls during Instantiate=" + EcEmptyCtor.Made);
            Show("empty", h.empty, c.empty, o => ((EcEmpty)o).Show());
            Show("emptyNull", h.emptyNull, c.emptyNull, o => ((EcEmpty)o).Show());
            Show("emptyCtor", h.emptyCtor, c.emptyCtor, o => ((EcEmptyCtor)o).Show());
            Show("emptyCtorNull", h.emptyCtorNull, c.emptyCtorNull, o => ((EcEmptyCtor)o).Show());
            Show("one", h.one, c.one, o => ((EcOne)o).Show());
            Show("oneNull", h.oneNull, c.oneNull, o => ((EcOne)o).Show());
            L("number=" + c.number + " text=" + (c.text ?? "null") + " ints=" + (c.ints == null ? "null" : c.ints.Count + (ReferenceEquals(c.ints, h.ints) ? " same" : " new"))
                + " structure=" + c.structure.Value);
            Show("withDefault", h.withDefault, c.withDefault, o => "value=" + ((EcDefault)o).Value);
            Show("gameClass", h.gameClass, c.gameClass, o => "id=" + (((AttackEffect)o).effectID ?? "null") + " p=" + ((AttackEffect)o).effectProbability);
            Show("gameClassNull", h.gameClassNull, c.gameClassNull, o => "id=" + (((AttackEffect)o).effectID ?? "null"));
            L("listOfMod " + (c.listOfMod == null ? "null" : c.listOfMod.Count + (ReferenceEquals(c.listOfMod, h.listOfMod) ? " same" : " new")
                + " [0]=" + (c.listOfMod.Count > 0 ? (c.listOfMod[0] == null ? "null" : ReferenceEquals(c.listOfMod[0], h.listOfMod[0]) ? "same" : c.listOfMod[0].Show()) : "-")));
            L("source after: empty " + (h.empty == null ? "null" : h.empty.Show()) + " emptyNull=" + (h.emptyNull == null) + " oneNull=" + (h.oneNull == null) + " gameClassNull=" + (h.gameClassNull == null));
        }

        void Update()
        {
            if (++frame == 3)
            {
                L("=== done");
                var done = new GameObject("EmptyCompositeProbeDone", typeof(RectTransform));
                DontDestroyOnLoad(done);
                done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            }
        }
    }
}
