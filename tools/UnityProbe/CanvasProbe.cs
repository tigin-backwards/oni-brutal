// Canvas semantics at the main menu ("PROBE ... CANVAS ..."): which disabled Behaviours the
// FindObjects family returns, and what nested canvases do when their root canvas's component is
// disabled or its GameObject made inactive: whether they draw (screenshots under
// PROBE_CAPTURE_DIR, read back by pixel), take raycasts (EventSystem.RaycastAll), where their
// rects end up and what isRootCanvas / rootCanvas report. With PROBE_SCENARIO=canvas only this
// probe runs.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class FindMarker : MonoBehaviour { }

    public class CanvasProbe : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;

        static void L(string s) => Rec.Log("CANVAS " + s);

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        static string Hosts<T>(IEnumerable<T> found) where T : Component
            => string.Join(",", found.Where(c => c != null && c.gameObject.name.StartsWith("FindHost")).Select(c => c.gameObject.name).OrderBy(n => n));

        static void Finds<T>(string what) where T : Component
        {
            L("find " + what + " ByType(Exclude) [" + Hosts(Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) + "]");
            L("find " + what + " ByType(Include) [" + Hosts(Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)) + "]");
            L("find " + what + " ByType(sort only) [" + Hosts(Object.FindObjectsByType<T>(FindObjectsSortMode.InstanceID)) + "]");
#pragma warning disable CS0618
            L("find " + what + " OfType() [" + Hosts(Object.FindObjectsOfType<T>()) + "]");
            L("find " + what + " OfType(true) [" + Hosts(Object.FindObjectsOfType<T>(true)) + "]");
            L("find " + what + " OfType(typeof) [" + Hosts(Object.FindObjectsOfType(typeof(T)).Cast<T>()) + "]");
#pragma warning restore CS0618
        }

        static void Singles<T>(string what) where T : Component
        {
            string N(T c) => c == null ? "null" : c.gameObject.name;
            L("first " + what + " FirstByType(Exclude) " + N(Object.FindFirstObjectByType<T>(FindObjectsInactive.Exclude)));
            L("first " + what + " FirstByType(Include) " + N(Object.FindFirstObjectByType<T>(FindObjectsInactive.Include)));
            L("first " + what + " AnyByType() " + N(Object.FindAnyObjectByType<T>()));
#pragma warning disable CS0618
            L("first " + what + " OfType() " + N(Object.FindObjectOfType<T>()));
#pragma warning restore CS0618
        }

        static GameObject Host(string name, bool active, bool enabledComponents)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(active);
            var c = go.AddComponent<Canvas>();
            c.enabled = enabledComponents;
            var m = go.AddComponent<FindMarker>();
            m.enabled = enabledComponents;
            return go;
        }

        IEnumerator FindProbe()
        {
            L("=== find: FindHostOn (active, enabled), FindHostDisabled (active, components disabled), FindHostInactive (inactive GameObject, components enabled)");
            var on = Host("FindHostOn", true, true);
            var disabled = Host("FindHostDisabled", true, false);
            var inactive = Host("FindHostInactive", false, true);
            yield return null;
            Finds<Canvas>("Canvas");
            Finds<FindMarker>("FindMarker");
            Object.DestroyImmediate(on);
            L("=== find: FindHostOn destroyed");
            Singles<Canvas>("Canvas");
            Singles<FindMarker>("FindMarker");
            Object.Destroy(disabled);
            Object.Destroy(inactive);
            yield return null;
        }

        // ----- nested canvases under a disabled root -----

        static readonly (string name, Color color, bool canvas, bool overrideSorting, bool raycaster)[] Parts =
        {
            ("NestedOverride", Color.red, true, true, true),
            ("NestedPlain", Color.green, true, false, true),
            ("NestedOverrideNoRaycaster", Color.blue, true, true, false),
            ("DirectImage", Color.yellow, false, false, false),
        };

        Canvas root;
        readonly Dictionary<string, RectTransform> rects = new Dictionary<string, RectTransform>();
        string dir;

        void Build()
        {
            var go = new GameObject("ProbeRootCanvas", typeof(RectTransform));
            root = go.AddComponent<Canvas>();
            root.renderMode = RenderMode.ScreenSpaceOverlay;
            root.sortingOrder = 32000;
            root.scaleFactor = 1.5f;
            go.AddComponent<GraphicRaycaster>();
            for (int i = 0; i < Parts.Length; i++)
            {
                var p = Parts[i];
                var child = new GameObject(p.name, typeof(RectTransform));
                var rt = (RectTransform)child.transform;
                rt.SetParent(go.transform, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(20f + 100f * i, 20f);
                rt.sizeDelta = new Vector2(60f, 60f);
                if (p.canvas)
                {
                    var c = child.AddComponent<Canvas>();
                    if (p.overrideSorting) { c.overrideSorting = true; c.sortingOrder = 32001; }
                    if (p.raycaster) child.AddComponent<GraphicRaycaster>();
                }
                var img = new GameObject(p.name + "Image", typeof(RectTransform));
                var irt = (RectTransform)img.transform;
                irt.SetParent(rt, false);
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.sizeDelta = Vector2.zero;
                img.AddComponent<Image>().color = p.color;
                rects[p.name] = rt;
            }
            // An override-sorting canvas inside NestedPlain, drawn above it (canvas units 120,90).
            var inner = new GameObject("InnerOverride", typeof(RectTransform));
            var inrt = (RectTransform)inner.transform;
            inrt.SetParent(rects["NestedPlain"], false);
            inrt.anchorMin = inrt.anchorMax = inrt.pivot = Vector2.zero;
            inrt.anchoredPosition = new Vector2(0f, 70f);
            inrt.sizeDelta = new Vector2(60f, 60f);
            var ic = inner.AddComponent<Canvas>();
            ic.overrideSorting = true;
            ic.sortingOrder = 32001;
            inner.AddComponent<GraphicRaycaster>();
            var iimg = new GameObject("InnerOverrideImage", typeof(RectTransform));
            var iirt = (RectTransform)iimg.transform;
            iirt.SetParent(inrt, false);
            iirt.anchorMin = Vector2.zero;
            iirt.anchorMax = Vector2.one;
            iirt.sizeDelta = Vector2.zero;
            iimg.AddComponent<Image>().color = Color.magenta;
            rects["InnerOverride"] = inrt;
        }

        static readonly (string name, Vector2 centre)[] Probes =
        {
            ("NestedOverride", new Vector2(75f, 75f)), ("NestedPlain", new Vector2(225f, 75f)),
            ("NestedOverrideNoRaycaster", new Vector2(375f, 75f)), ("DirectImage", new Vector2(525f, 75f)),
            ("InnerOverride", new Vector2(225f, 180f)),
        };

        static string Corners(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return "(" + c[0].x.ToString("F1") + "," + c[0].y.ToString("F1") + ")-(" + c[2].x.ToString("F1") + "," + c[2].y.ToString("F1") + ")";
        }

        IEnumerator State(string label)
        {
            for (int i = 0; i < 4; i++) yield return null;
            L("--- state " + label + ": root enabled=" + root.enabled + " activeInHierarchy=" + root.gameObject.activeInHierarchy
              + " isActiveAndEnabled=" + root.isActiveAndEnabled + " rect=" + Corners((RectTransform)root.transform) + " scaleFactor=" + root.scaleFactor);
            var es = UnityEngine.EventSystems.EventSystem.current;
            foreach (var p in Probes)
            {
                var rt = rects[p.name];
                string line = p.name + ": rect=" + Corners(rt) + " activeInHierarchy=" + rt.gameObject.activeInHierarchy;
                var c = rt.GetComponent<Canvas>();
                if (c != null)
                    line += " isActiveAndEnabled=" + c.isActiveAndEnabled + " isRootCanvas=" + c.isRootCanvas + " rootCanvas=" + (c.rootCanvas == null ? "null" : c.rootCanvas.name)
                            + " scaleFactor=" + c.scaleFactor + " sortingOrder=" + c.sortingOrder + " renderMode=" + c.renderMode;
                var g = rt.GetComponentInChildren<Graphic>(true);
                line += " graphic.canvas=" + (g.canvas == null ? "null" : g.canvas.name);
                // The raycast at the part's centre as placed while the root was live.
                var centre = p.centre;
                if (es != null)
                {
                    var hits = new List<RaycastResult>();
                    es.RaycastAll(new PointerEventData(es) { position = centre }, hits);
                    line += " raycast@" + centre.x + "," + centre.y + "=[" + string.Join(",", hits.Select(h => h.gameObject.name)) + "]";
                }
                L(line);
            }
            if (!string.IsNullOrEmpty(dir))
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "canvas-" + label + ".png"));
                L("capture canvas-" + label + " screen " + Screen.width + "x" + Screen.height);
            }
            for (int i = 0; i < 4; i++) yield return null;
        }

        IEnumerator Run()
        {
            dir = System.Environment.GetEnvironmentVariable("PROBE_CAPTURE_DIR");
            yield return FindProbe();
            L("=== nested: root overlay canvas (scaleFactor 1.5, order 32000) with NestedOverride (overrideSorting, raycaster), NestedPlain (raycaster), NestedOverrideNoRaycaster, DirectImage, InnerOverride (override, in NestedPlain); 60x60 at x=20+100i,y=20 canvas units");
            Build();
            yield return State("live");
            root.enabled = false;
            yield return State("root-disabled");
            root.enabled = true;
            yield return State("root-reenabled");
            root.gameObject.SetActive(false);
            yield return State("root-inactive");
            root.gameObject.SetActive(true);
            yield return State("root-reactivated");
            // Nested canvases disabled under a live root: NestedOverride and NestedPlain (which
            // holds InnerOverride).
            rects["NestedOverride"].GetComponent<Canvas>().enabled = false;
            rects["NestedPlain"].GetComponent<Canvas>().enabled = false;
            yield return State("nested-disabled");
            // ... and the root disabled too.
            root.enabled = false;
            yield return State("nested-and-root-disabled");
            // A root that starts disabled: the nested canvases were never under a live root.
            Object.Destroy(root.gameObject);
            yield return null;
            Build();
            root.enabled = false;
            yield return State("built-disabled");
            Object.Destroy(root.gameObject);
            L("=== done");
        }
    }
}
