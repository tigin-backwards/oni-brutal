// Value-level probes: results of engine functions game code consumes (formatting, maths,
// random sequences, serialization, hierarchy queries), printed one per line ("PROBE ... VAL").
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SemanticsProbe
{
    [Serializable]
    public class JsonSample
    {
        public int number = 7;
        public float ratio = 0.1f;
        public string text = "a \"quoted\" é";
        public Vector3 position = new Vector3(1.5f, -2f, 0.333f);
        public List<int> list = new List<int> { 1, 2 };
        public Color color = new Color(0.1f, 0.2f, 0.3f, 0.4f);
        public List<float> floats = new List<float> { 1f, 0.1f, -2.5f, 100000f, 1e21f, 1.5e-7f, 0.000001f, 123.456f, 3.4028235e38f, -0f, 1e-45f, 16777216f, 0.3f, 2.71828f, 0.030318595f, 3.9166574f, 1.8230687e-05f, -62.81875f, 0.00154345f, -2.1329275e-07f };
        public List<double> doubles = new List<double> { 0.1, 1e21, 1.5e-7, 123.456, 5e-324, 1.7976931348623157e308, 0.3, 1d / 3d, 1e22, 9007199254740993d, -7.3127151177519755e-06, 0.030318594544552595, 78663.40851152701, 3.9166573353688707, 1.8230687000260787e-05, -9553557779573.523, 0.30000000000000004, -0.0 };
    }

    public class Msg : MonoBehaviour
    {
        void Ping(string arg) => Rec.Log("VAL SendMessage Ping(" + arg + ") on " + name);
    }

    public class OrderRec : MonoBehaviour
    {
        public static string Order = "";
        void Awake() => Order += name + ".Awake ";
        void OnEnable() => Order += name + ".OnEnable ";
        void Start() => Order += name + ".Start ";
    }

    public static class Values
    {
        static void V(string what, object value) => Rec.Log("VAL " + what + " = " + Convert.ToString(value, CultureInfo.InvariantCulture));

        public static IEnumerator Run()
        {
            Rec.Log("=== values: screen");
            V("Screen", Screen.width + "x" + Screen.height + " current " + Screen.currentResolution.width + "x" + Screen.currentResolution.height + " fullScreenMode " + Screen.fullScreenMode);
            V("Display.main", Display.displays[0].systemWidth);

            Rec.Log("=== values: formatting");
            V("Vector3", new Vector3(1.23456f, -2f, 1e-5f));
            V("Vector2", new Vector2(0.5f, 100.25f));
            V("Vector4", new Vector4(1, 2, 3, 4));
            V("Quaternion", Quaternion.Euler(10, 20, 30));
            V("Color", new Color(0.1f, 0.25f, 1f, 0.5f));
            V("Color32", new Color32(1, 2, 3, 4));
            V("Rect", new Rect(1, 2, 3.5f, 4));
            V("Bounds", new Bounds(Vector3.one, new Vector3(2, 4, 6)));
            V("Matrix4x4 TRS", Matrix4x4.TRS(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0), Vector3.one * 2));
            V("Vector2Int", new Vector2Int(3, -4));
            V("Color.HSVToRGB", Color.HSVToRGB(0.3f, 0.5f, 0.7f));
            V("Color.Lerp", Color.Lerp(Color.red, Color.blue, 0.3f));
            V("Color32 implicit", (Color32)new Color(0.5f, 0.501f, 0.999f, 0.002f));

            Rec.Log("=== values: maths");
            V("Mathf.Repeat/PingPong", Mathf.Repeat(-1.25f, 1f) + " " + Mathf.PingPong(3.5f, 2f));
            V("Mathf.SmoothStep/SmoothDamp", Mathf.SmoothStep(0f, 10f, 0.3f) + " " + SmoothDamp());
            V("ColorUtility.ToHtmlStringRGB", ColorUtility.ToHtmlStringRGB(new Color(0.5f, 0.25f, 1f, 0.75f)));
            V("Vector2.SignedAngle", Vector2.SignedAngle(Vector2.right, new Vector2(1, 1)));
            V("Mathf.Round/RoundToInt", Mathf.Round(2.5f) + " " + Mathf.Round(3.5f) + " " + Mathf.RoundToInt(-2.5f) + " " + Mathf.RoundToInt(1.5f));
            V("Mathf.Approximately", Mathf.Approximately(1f, 1.0000001f) + " " + Mathf.Approximately(0f, 1e-40f));
            V("Vector3.Slerp", Vector3.Slerp(Vector3.right, Vector3.up, 0.3f));
            V("Vector3.Normalize tiny", new Vector3(1e-6f, 0, 0).normalized);
            V("Quaternion.LookRotation", Quaternion.LookRotation(new Vector3(1, 2, 3), Vector3.up));
            V("Quaternion.eulerAngles", new Quaternion(0.1f, 0.2f, 0.3f, 0.9273618f).eulerAngles);
            V("Quaternion.Slerp", Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(0, 170, 0), 0.4f));
            V("Quaternion.FromToRotation", Quaternion.FromToRotation(Vector3.up, Vector3.left));
            V("Matrix4x4.Ortho", Matrix4x4.Ortho(-1, 1, -2, 2, 0.1f, 10f));

            Rec.Log("=== values: json");
            string json = JsonUtility.ToJson(new JsonSample());
            V("JsonUtility.ToJson", json);

            Rec.Log("=== values: hierarchy");
            var root = new GameObject("H0");
            var c1 = new GameObject("H1"); c1.transform.SetParent(root.transform);
            var c2 = new GameObject("H2"); c2.transform.SetParent(root.transform);
            var c11 = new GameObject("H11"); c11.transform.SetParent(c1.transform);
            c11.SetActive(false);
            foreach (var go in new[] { root, c1, c2, c11 }) go.AddComponent<BoxCollider>();
            V("GetComponentsInChildren", string.Join(",", Array.ConvertAll(root.GetComponentsInChildren<BoxCollider>(), x => x.name)));
            V("GetComponentsInChildren(true)", string.Join(",", Array.ConvertAll(root.GetComponentsInChildren<BoxCollider>(true), x => x.name)));
            V("GetComponentsInParent", string.Join(",", Array.ConvertAll(c11.GetComponentsInParent<BoxCollider>(true), x => x.name)));
            V("Transform.Find", (root.transform.Find("H1/H11") != null) + " " + (GameObject.Find("H11") != null));
            root.transform.position = new Vector3(10, 0, 0);
            root.transform.localScale = new Vector3(2, 2, 2);
            c2.transform.localPosition = new Vector3(1, 1, 0);
            var loose = new GameObject("L");
            loose.transform.position = new Vector3(5, 5, 5);
            loose.transform.SetParent(c2.transform, true);
            V("SetParent(worldPositionStays)", loose.transform.localPosition.ToString("R") + " " + loose.transform.localScale.ToString("R"));
            loose.transform.SetParent(null, false);
            V("SetParent(null, false)", loose.transform.position.ToString("R"));
            V("sibling index", c2.transform.GetSiblingIndex() + " " + root.transform.childCount);
            c2.transform.SetAsFirstSibling();
            V("after SetAsFirstSibling", root.transform.GetChild(0).name);

            var ra = new GameObject("RA"); var rb = new GameObject("RB"); var rc = new GameObject("RC");
            V("root sibling indices", ra.transform.GetSiblingIndex() + " " + rb.transform.GetSiblingIndex() + " " + rc.transform.GetSiblingIndex());
            rc.transform.SetAsFirstSibling();
            V("root after SetAsFirstSibling", ra.transform.GetSiblingIndex() + " " + rb.transform.GetSiblingIndex() + " " + rc.transform.GetSiblingIndex());
            UnityEngine.Object.DontDestroyOnLoad(rb);
            V("root after DontDestroyOnLoad", ra.transform.GetSiblingIndex() + " " + rb.transform.GetSiblingIndex() + " " + rc.transform.GetSiblingIndex());

            Rec.Log("=== values: instantiate order");
            var proto = new GameObject("P0");
            proto.AddComponent<OrderRec>();
            var pc = new GameObject("P1"); pc.transform.SetParent(proto.transform); pc.AddComponent<OrderRec>();
            OrderRec.Order = "";
            var clone = UnityEngine.Object.Instantiate(proto);
            V("Instantiate callbacks", OrderRec.Order);
            V("clone names", clone.name + " " + clone.transform.GetChild(0).name);
            OrderRec.Order = "";
            proto.SetActive(false);
            var inactive = UnityEngine.Object.Instantiate(proto);
            V("Instantiate inactive", OrderRec.Order + "| activeSelf=" + inactive.activeSelf);
            OrderRec.Order = "";
            yield return null;
            V("next frame", OrderRec.Order);

            Rec.Log("=== values: messages");
            var m = new GameObject("M").AddComponent<Msg>();
            new GameObject("Mc").AddComponent<Msg>().transform.SetParent(m.transform);
            m.gameObject.SendMessage("Ping", "a", SendMessageOptions.RequireReceiver);
            m.gameObject.SendMessage("Missing", SendMessageOptions.DontRequireReceiver);
            for (int i = 0; i < 10; i++) yield return null;
            Rec.Log("=== values: done");
        }

        static string SmoothDamp()
        {
            var velocity = Vector3.zero;
            var x = Vector3.SmoothDamp(Vector3.zero, new Vector3(10f, 0f, 0f), ref velocity, 0.3f);
            return x.x.ToString("R", CultureInfo.InvariantCulture) + " " + velocity.x.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
