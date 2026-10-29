// What a hierarchy reports while it is being deactivated or activated ("PROBE ... DA ..."), at
// mod load. Run alone with PROBE_SCENARIO=deactorder.
//
// Found through issue #23: DetailCollapsableLabel.OnDisable hides its content rows, siblings that
// come after it under the same parent, only where a row's activeInHierarchy differs from its
// "in use" flag. When the whole details screen is deactivated, the original hid the rows (they
// still read active then); ours had already cleared every flag of the subtree and left the rows
// active, so they came back, stale, with the next building selected.
//
// The hierarchy: P > A, B (> B1, B2), C, and a spy on each object that logs, in its OnEnable and
// OnDisable, every object's activeSelf/activeInHierarchy ("s"/"-" then "h"/"-") and every spy's
// isActiveAndEnabled ("e"/"-"), so the order of the callbacks and what each sees are both on
// record.
// DA <case> <step> <tag> <callback> P=.. A=.. B=.. B1=.. B2=.. C=..
// Cases:
//   off       P.SetActive(false)
//   on        P.SetActive(true) after off
//   rootoff   R.SetActive(false), R being P's parent (so P keeps activeSelf)
//   hideC     as off, and A's OnDisable sets C inactive; then P on again: is C still inactive?
//   hideB1    as off, and A's OnDisable sets B1 inactive (a later sibling's child)
//   hideP     as off, and B1's OnDisable sets A inactive (an earlier sibling, already done)
//   childoff  B.SetActive(false): B1 and B2 only
//   compoff   A's spy disabled (enabled = false) inside B1's OnDisable while P deactivates
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SemanticsProbe
{
    public static class DeactivateOrder
    {
        internal static string Case = "-";
        internal static string Step = "-";
        internal static Dictionary<string, GameObject> Objects = new Dictionary<string, GameObject>();
        internal static System.Action<string, string> Hook;

        public static void Run()
        {
            foreach (var name in new[] { "off", "on", "rootoff", "hideC", "hideB1", "hideP", "childoff", "compoff" })
            {
                try { RunCase(name); }
                catch (Exception e) { Rec.Log("DA " + name + " threw " + e.GetType().Name + ": " + e.Message); }
            }
            Rec.Log("DA done");
        }

        static void RunCase(string name)
        {
            Case = name;
            Hook = null;
            Objects.Clear();
            var r = new GameObject("R");
            var p = Make("P", r);
            var a = Make("A", p);
            var b = Make("B", p);
            Make("B1", b);
            Make("B2", b);
            var c = Make("C", p);
            Step = "built";
            Rec.Log("DA " + name + " built " + State());
            switch (name)
            {
                case "off":
                    Do("off", () => p.SetActive(false));
                    break;
                case "on":
                    p.SetActive(false);
                    Do("on", () => p.SetActive(true));
                    break;
                case "rootoff":
                    Do("rootoff", () => r.SetActive(false));
                    Do("rooton", () => r.SetActive(true));
                    break;
                case "hideC":
                    Hook = (tag, cb) => { if (tag == "A" && cb == "OnDisable") { Objects["C"].SetActive(false); Rec.Log("DA hideC set C inactive " + State()); } };
                    Do("off", () => p.SetActive(false));
                    Hook = null;
                    Do("on", () => p.SetActive(true));
                    break;
                case "hideB1":
                    Hook = (tag, cb) => { if (tag == "A" && cb == "OnDisable") { Objects["B1"].SetActive(false); Rec.Log("DA hideB1 set B1 inactive " + State()); } };
                    Do("off", () => p.SetActive(false));
                    Hook = null;
                    Do("on", () => p.SetActive(true));
                    break;
                case "hideP":
                    Hook = (tag, cb) => { if (tag == "B1" && cb == "OnDisable") { Objects["A"].SetActive(false); Rec.Log("DA hideP set A inactive " + State()); } };
                    Do("off", () => p.SetActive(false));
                    Hook = null;
                    Do("on", () => p.SetActive(true));
                    break;
                case "childoff":
                    Do("off", () => b.SetActive(false));
                    break;
                case "compoff":
                    Hook = (tag, cb) => { if (tag == "B1" && cb == "OnDisable") { Objects["A"].GetComponent<DaSpy>().enabled = false; Rec.Log("DA compoff disabled A's spy " + State()); } };
                    Do("off", () => p.SetActive(false));
                    Hook = null;
                    Do("on", () => p.SetActive(true));
                    break;
            }
            UnityEngine.Object.DestroyImmediate(r);
        }

        static void Do(string step, System.Action act)
        {
            Step = step;
            Rec.Log("DA " + Case + " " + step + " begin");
            act();
            Rec.Log("DA " + Case + " " + step + " end " + State());
        }

        static GameObject Make(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Objects[name] = go;
            go.AddComponent<DaSpy>().Tag = name;
            return go;
        }

        internal static string State() => string.Join(" ", new[] { "P", "A", "B", "B1", "B2", "C" }.Select(n =>
        {
            var go = Objects[n];
            var spy = go.GetComponent<DaSpy>();
            return n + "=" + (go.activeSelf ? "s" : "-") + (go.activeInHierarchy ? "h" : "-") + (spy != null && spy.isActiveAndEnabled ? "e" : "-");
        }));
    }

    public class DaSpy : MonoBehaviour
    {
        public string Tag;

        void OnEnable() => Report("OnEnable");
        void OnDisable() => Report("OnDisable");

        void Report(string cb)
        {
            if (Tag == null || !DeactivateOrder.Objects.ContainsKey("C")) return;
            Rec.Log("DA " + DeactivateOrder.Case + " " + DeactivateOrder.Step + " " + Tag + " " + cb + " " + DeactivateOrder.State());
            DeactivateOrder.Hook?.Invoke(Tag, cb);
        }
    }
}
