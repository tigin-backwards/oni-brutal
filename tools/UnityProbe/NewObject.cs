// What `new` gives for a ScriptableObject subclass (and, beside it, a MonoBehaviour), lines
// "PROBE ... NEW ...": whether the object is alive (== null, bool, instance id), its name and
// hideFlags, which of Awake/OnEnable run and when, what reaches the log, and Destroy afterwards.
// Ronivan's Legacy builds a TextStyleSetting with `new` and tests it against null (issue #7).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SemanticsProbe
{
    public class NewSo : ScriptableObject
    {
        public static string Order = "";
        public int Field = 3;
        public NewSo() { Order += "ctor "; }
        void Awake() => Order += "Awake ";
        void OnEnable() => Order += "OnEnable ";
        void OnDisable() => Order += "OnDisable ";
        void OnDestroy() => Order += "OnDestroy ";
    }

    public class NewSoPlain : ScriptableObject { }

    public class NewMb : MonoBehaviour
    {
        public static string Order = "";
        void Awake() => Order += "Awake ";
        void OnEnable() => Order += "OnEnable ";
    }

    public class NewObject : MonoBehaviour
    {
        static void L(string s) => Rec.Log("NEW " + s);

        static string Esc(string s) => s == null ? "null" : s.Replace("\r", "\\r").Replace("\n", "\\n");

        static T Capture<T>(string label, Func<T> make)
        {
            var main = new List<string>();
            Application.LogCallback on = (c, st, ty) => main.Add(ty + " \"" + Esc(c) + "\"");
            Application.logMessageReceived += on;
            T got;
            try { got = make(); }
            finally { Application.logMessageReceived -= on; }
            L(label + " log " + main.Count + (main.Count > 0 ? " " + string.Join(" | ", main) : ""));
            return got;
        }

        static void Describe(string label, UnityEngine.Object o)
        {
            L(label + " refnull=" + ((object)o == null) + " ==null=" + (o == null) + " bool=" + (bool)o
                + " id=" + (o is null ? 0 : o.GetInstanceID()) + " idNonZero=" + (o is null ? false : o.GetInstanceID() != 0));
            try { L(label + " name=\"" + o.name + "\""); } catch (Exception e) { L(label + " name throws " + e.GetType().Name); }
            try { L(label + " hideFlags=" + o.hideFlags); } catch (Exception e) { L(label + " hideFlags throws " + e.GetType().Name); }
            try { L(label + " ToString=\"" + o + "\""); } catch (Exception e) { L(label + " ToString throws " + e.GetType().Name); }
        }

        int frame;
        NewSo kept;

        void Start()
        {
            L("begin");
            NewSo.Order = "";
            var so = Capture("so", () => new NewSo());
            L("so order after new: " + NewSo.Order);
            Describe("so", so);
            L("so Field=" + so.Field);
            so.name = "Named";
            L("so after name set: name=\"" + so.name + "\" ==null=" + (so == null));
            L("so in FindObjectsOfTypeAll=" + Array.IndexOf(Resources.FindObjectsOfTypeAll<NewSo>(), so));
            var copy = Capture("so Instantiate", () => Instantiate(so));
            Describe("so copy", copy);
            NewSo.Order = "";
            DestroyImmediate(so);
            L("so after DestroyImmediate: order " + NewSo.Order + " ==null=" + (so == null));

            var plain = Capture("plain", () => new NewSoPlain());
            Describe("plain", plain);
            var bare = Capture("bare", () => new ScriptableObject());
            Describe("bare", bare);

            var made = Capture("CreateInstance", () => ScriptableObject.CreateInstance<NewSoPlain>());
            Describe("CreateInstance", made);

            NewMb.Order = "";
            var mb = Capture("mb", () => new NewMb());
            L("mb order after new: " + NewMb.Order);
            Describe("mb", mb);

            NewSo.Order = "";
            kept = new NewSo();
            L("kept order after new: " + NewSo.Order);
        }

        void Update()
        {
            if (frame++ == 2)
            {
                L("kept two frames later: order " + NewSo.Order + " ==null=" + (kept == null));
                NewSo.Order = "";
                Destroy(kept);
                L("kept after Destroy: order " + NewSo.Order + " ==null=" + (kept == null));
            }
            else if (frame == 4)
            {
                L("kept a frame after Destroy: order " + NewSo.Order + " ==null=" + (kept == null));
                L("end");
            }
        }
    }
}
