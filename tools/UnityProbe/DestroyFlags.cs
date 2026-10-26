// The active flags a component sees in its own OnDestroy, lines "PROBE ... DF ...": a GameObject
// destroyed with Destroy (active, inactive, under an inactive parent; its child and grandchild),
// with DestroyImmediate, and a component destroyed alone. KMonoBehaviour.OnDestroy releases the
// object's KObject (its event system) only when `!gameObject.activeSelf`; a KObject left alive
// keeps a destroyed component's handlers reachable (the rocket launch NRE in
// RocketControlStation.OnTagsChanged). The last case destroys a KMonoBehaviour that has an event
// system and reports, two frames later, whether KObjectManager released it.
using System.Collections;
using UnityEngine;

namespace SemanticsProbe
{
    public class DfRec : MonoBehaviour
    {
        public string Tag;
        static string Flags(Behaviour b) => "activeSelf=" + b.gameObject.activeSelf + " activeInHierarchy=" + b.gameObject.activeInHierarchy + " enabled=" + b.enabled + " isActiveAndEnabled=" + b.isActiveAndEnabled;
        void OnDisable() => Rec.Log("DF " + Tag + " OnDisable " + Flags(this));
        void OnDestroy() => Rec.Log("DF " + Tag + " OnDestroy " + Flags(this));
    }

    public class DfKmb : KMonoBehaviour
    {
        public string Tag;
        protected override void OnCleanUp() => Rec.Log("DF " + Tag + " OnCleanUp activeSelf=" + gameObject.activeSelf + " activeInHierarchy=" + gameObject.activeInHierarchy);
    }

    public class DestroyFlags : MonoBehaviour
    {
        static DfRec Make(string tag, Transform parent)
        {
            var go = new GameObject(tag);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<DfRec>();
            r.Tag = tag;
            return r;
        }

        static void L(string s) => Rec.Log("DF " + s);

        IEnumerator Start()
        {
            yield return null;
            L("=== 1: Destroy(go) of an active root, with a child and a grandchild");
            var a = Make("A", null); var ac = Make("AC", a.transform); Make("ACC", ac.transform);
            yield return null;
            Destroy(a.gameObject);
            L("1 after Destroy: A activeSelf=" + a.gameObject.activeSelf);
            yield return null;

            L("=== 2: Destroy(component) on an active object");
            var b = Make("B", null); var bgo = b.gameObject;
            yield return null;
            Destroy(b);
            yield return null;
            L("2 next frame: B's object activeSelf=" + bgo.activeSelf);
            Destroy(bgo);
            yield return null;

            L("=== 3: DestroyImmediate(go) of an active root with a child");
            var c = Make("C", null); Make("CC", c.transform);
            yield return null;
            DestroyImmediate(c.gameObject);
            L("3 after DestroyImmediate");
            yield return null;

            L("=== 4: Destroy(go) of an inactive root");
            var d = Make("D", null);
            yield return null;
            d.gameObject.SetActive(false);
            Destroy(d.gameObject);
            yield return null;

            L("=== 5: Destroy(go) of an active child under an inactive parent");
            var p = Make("P", null); var e = Make("E", p.transform);
            yield return null;
            p.gameObject.SetActive(false);
            Destroy(e.gameObject);
            yield return null;
            Destroy(p.gameObject);
            yield return null;

            L("=== 6: Destroy(go) of a child, its parent staying");
            var q = Make("Q", null); Make("QC", q.transform);
            yield return null;
            Destroy(q.transform.GetChild(0).gameObject);
            yield return null;
            Destroy(q.gameObject);
            yield return null;

            L("=== 7: a KMonoBehaviour with an event system, destroyed active");
            var kgo = new GameObject("K");
            var k = kgo.AddComponent<DfKmb>(); k.Tag = "K";
            k.Subscribe(12345, _ => { });
            var kobj = HarmonyLib.Traverse.Create(k).Field("obj").GetValue<KObject>();
            L("7 before: kobj=" + (kobj != null) + " hasEventSystem=" + (kobj != null && kobj.hasEventSystem) + " manager=" + (KObjectManager.Instance != null));
            yield return null;
            Destroy(kgo);
            yield return null;
            yield return null;
            L("7 two frames later: hasEventSystem=" + (kobj != null && kobj.hasEventSystem));

            L("=== 8: the same, deactivated before Destroy");
            var kgo2 = new GameObject("K2");
            var k2 = kgo2.AddComponent<DfKmb>(); k2.Tag = "K2";
            k2.Subscribe(12345, _ => { });
            var kobj2 = HarmonyLib.Traverse.Create(k2).Field("obj").GetValue<KObject>();
            yield return null;
            kgo2.SetActive(false);
            Destroy(kgo2);
            yield return null;
            yield return null;
            L("8 two frames later: hasEventSystem=" + (kobj2 != null && kobj2.hasEventSystem));

            var done = new GameObject("DestroyFlagsProbeDone", typeof(RectTransform));
            DontDestroyOnLoad(done);
            done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            L("=== done");
        }
    }
}
