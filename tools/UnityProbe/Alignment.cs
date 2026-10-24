// When TMP applies an alignment set from code (lines "PROBE ... AL ..."), 10 s into the main
// menu. Chain Tool sets `alignment` on labels it clones from a prefab under a hidden menu, before
// they first wake; the original then shows the prefab's alignment. Copies of loaded LocTexts (one
// per alignment value stored in their serialized m_textAlignment) and new TextMeshProUGUIs get an
// alignment set before and after their first activation, and each step logs the alignment, its
// two halves and the stored value.
//
// Run alone with PROBE_SCENARIO=alignment.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace SemanticsProbe
{
    public class AlWake : MonoBehaviour
    {
        public string Tag;
        void Awake() => Rec.Log("AL " + Tag + " probe component Awake");
        void OnEnable() => Rec.Log("AL " + Tag + " probe component OnEnable");
    }

    public class Alignment : MonoBehaviour
    {
        static void L(string s) => Rec.Log("AL " + s);

        static readonly FieldInfo StoredField = typeof(TMP_Text).GetField("m_textAlignment", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        static int Stored(TMP_Text t) => StoredField == null ? int.MinValue : Convert.ToInt32(StoredField.GetValue(t));

        static void State(string label, TMP_Text t) =>
            L(label + " alignment " + t.alignment + " (" + (int)t.alignment + ") h " + t.horizontalAlignment + " v " + t.verticalAlignment
              + " stored " + Stored(t) + " activeInHierarchy " + t.gameObject.activeInHierarchy);

        static GameObject Parent(string name, bool active)
        {
            var p = new GameObject(name, typeof(RectTransform));
            p.SetActive(active);
            return p;
        }

        static TMP_Text Copy(TMP_Text source, GameObject parent, string tag)
        {
            var go = Instantiate(source.gameObject, parent.transform, false);
            go.AddComponent<AlWake>().Tag = tag;
            go.SetActive(true);
            return go.GetComponent<TMP_Text>();
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(10f);
            L("begin field " + (StoredField == null ? "missing" : StoredField.FieldType.Name));
            var loaded = Resources.FindObjectsOfTypeAll<LocText>().Where(t => !t.gameObject.scene.IsValid()).ToList();
            var byStored = loaded.GroupBy(Stored).OrderBy(g => g.Key).ToList();
            foreach (var g in byStored) L("loaded stored " + g.Key + " count " + g.Count() + " alignment " + string.Join(",", g.Select(t => t.alignment.ToString()).Distinct().Take(6)));
            var sources = byStored.Select(g => g.First()).ToList();
            var made = new List<GameObject>();
            foreach (var source in sources)
            {
                string tag = "src" + Stored(source);
                State(tag + " source", source);

                // As Chain Tool: copied active under an inactive parent, set, then the parent shown.
                var hidden = Parent(tag + "-hidden", false);
                made.Add(hidden);
                var a = Copy(source, hidden, tag + " hidden");
                State(tag + " hidden copied", a);
                a.alignment = TextAlignmentOptions.MidlineLeft;
                State(tag + " hidden set MidlineLeft", a);
                a.fontStyle = FontStyles.Italic;
                hidden.SetActive(true);
                State(tag + " hidden shown", a);
                L(tag + " hidden shown fontStyle " + a.fontStyle);
                yield return null;
                State(tag + " hidden next frame", a);
                a.alignment = TextAlignmentOptions.MidlineLeft;
                State(tag + " hidden set again", a);
                hidden.SetActive(false);
                hidden.SetActive(true);
                State(tag + " hidden hidden and shown", a);

                // Only one half set before waking.
                var hidden2 = Parent(tag + "-hidden2", false);
                made.Add(hidden2);
                var b = Copy(source, hidden2, tag + " half");
                b.horizontalAlignment = HorizontalAlignmentOptions.Right;
                State(tag + " half set h Right", b);
                hidden2.SetActive(true);
                State(tag + " half shown", b);
                var hidden3 = Parent(tag + "-hidden3", false);
                made.Add(hidden3);
                var b2 = Copy(source, hidden3, tag + " vhalf");
                b2.verticalAlignment = VerticalAlignmentOptions.Bottom;
                State(tag + " vhalf set v Bottom", b2);
                hidden3.SetActive(true);
                State(tag + " vhalf shown", b2);

                // Copied awake, then set.
                var shown = Parent(tag + "-shown", true);
                made.Add(shown);
                var c = Copy(source, shown, tag + " awake");
                State(tag + " awake copied", c);
                c.alignment = TextAlignmentOptions.MidlineLeft;
                State(tag + " awake set MidlineLeft", c);
                shown.SetActive(false);
                shown.SetActive(true);
                State(tag + " awake hidden and shown", c);

                // A copy of a copy that was set before it woke.
                var hidden4 = Parent(tag + "-hidden4", false);
                made.Add(hidden4);
                var d = Copy(source, hidden4, tag + " unwoken");
                d.alignment = TextAlignmentOptions.MidlineLeft;
                var shown2 = Parent(tag + "-shown2", true);
                made.Add(shown2);
                var e = Instantiate(d.gameObject, shown2.transform, false).GetComponent<TMP_Text>();
                State(tag + " copy of unwoken set", e);

                // Woken under an inactive parent's sibling: SetActive(false) then true on itself.
                var hidden5 = Parent(tag + "-hidden5", false);
                made.Add(hidden5);
                var f = Copy(source, hidden5, tag + " self");
                f.alignment = TextAlignmentOptions.MidlineLeft;
                f.transform.SetParent(shown.transform, false);
                State(tag + " self moved to a shown parent", f);
            }

            // New components, set before and after waking.
            var fresh = Parent("fresh-hidden", false);
            made.Add(fresh);
            var g1 = new GameObject("fresh", typeof(RectTransform));
            g1.transform.SetParent(fresh.transform, false);
            var t1 = g1.AddComponent<TextMeshProUGUI>();
            State("fresh added", t1);
            t1.alignment = TextAlignmentOptions.MidlineLeft;
            State("fresh set MidlineLeft", t1);
            fresh.SetActive(true);
            State("fresh shown", t1);
            var g2 = new GameObject("fresh-awake", typeof(RectTransform));
            var t2 = g2.AddComponent<TextMeshProUGUI>();
            made.Add(g2);
            State("fresh awake added", t2);
            t2.alignment = TextAlignmentOptions.MidlineLeft;
            State("fresh awake set", t2);
            yield return null;
            foreach (var m in made) Destroy(m);
            L("end");
        }
    }
}
