// TMP_SubMeshUI's Graphic state ("PROBE ... SM ..."), at the main menu: the sub-meshes a
// TextMeshProUGUI makes for characters from a fallback font. For each step it logs every
// sub-mesh's raycastTarget, maskable, activity, components, rect (anchors, pivot, size, rect),
// mesh vertex count, depth and layout values, and what EventSystem.RaycastAll hits at the text's
// centre, at the fallback glyph and at a corner of the rect away from the glyphs. The steps:
// the parent's raycastTarget left on, set off before the text, set off and on again after the
// sub-mesh exists; the sub-mesh's own raycastTarget set on; the text changed so no fallback
// glyph remains and back; the parent's rect resized and re-pivoted; the parent disabled and
// deactivated; maskable; a clone; a TMP_SubMeshUI added by hand. Then every TMP_SubMeshUI the
// player has loaded (scene objects and prefab assets) with its serialized values, and an
// instantiated copy of a prefab text that holds one. With PROBE_SCENARIO=submesh only this
// probe runs.
#pragma warning disable CS0618 // enableKerning, as SetTextStyleSetting.ApplyStyle sets it
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class SubMesh : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static Canvas canvas;

        static void L(string s) => Rec.Log("SM " + s);
        static string R(float v) => v.ToString("0.###", Inv);
        static string V(Vector2 v) => R(v.x) + "," + R(v.y);
        static string RectS(Rect r) => R(r.x) + "," + R(r.y) + "," + R(r.width) + "," + R(r.height);

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        static string Hex(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                sb.Append(c >= 0x21 && c < 0x7F ? c.ToString() : "\\u" + ((int)c).ToString("X4"));
            return sb.ToString();
        }

        static string Sub(TMP_SubMeshUI s)
        {
            var rt = (RectTransform)s.transform;
            var comps = string.Join(",", s.GetComponents<Component>().Select(c => c == null ? "null" : c.GetType().Name));
            var le = s.GetComponent<LayoutElement>();
            int vc = -1;
            string bounds = "-";
            try { var m = s.mesh; vc = m == null ? -2 : m.vertexCount; if (m != null) bounds = R(m.bounds.size.x) + "x" + R(m.bounds.size.y); } catch (Exception e) { vc = -3; L("mesh exception " + e.GetType().Name); }
            string mat = s.sharedMaterial == null ? "null" : s.sharedMaterial.name;
            return "'" + s.name + "' hide=" + s.gameObject.hideFlags + " actSelf=" + s.gameObject.activeSelf + " actH=" + s.gameObject.activeInHierarchy
                + " en=" + s.enabled + " rt=" + s.raycastTarget + " mask=" + s.maskable + " layer=" + s.gameObject.layer
                + " comps=" + comps + " ile=" + (s is ILayoutElement) + (le == null ? "" : " le.ignore=" + le.ignoreLayout)
                + " amin=" + V(rt.anchorMin) + " amax=" + V(rt.anchorMax) + " pivot=" + V(rt.pivot) + " sd=" + V(rt.sizeDelta)
                + " ap=" + V(rt.anchoredPosition) + " lp=" + R(rt.localPosition.x) + "," + R(rt.localPosition.y) + "," + R(rt.localPosition.z)
                + " rect=" + RectS(rt.rect) + " vc=" + vc + " bounds=" + bounds + " depth=" + s.depth + " cull=" + s.canvasRenderer.cull
                + " pw=" + R(LayoutUtility.GetPreferredWidth(rt)) + " ph=" + R(LayoutUtility.GetPreferredHeight(rt))
                + " mat=" + mat + " color=" + s.color + " sortIdx=" + s.transform.GetSiblingIndex();
        }


        // Destroys an object after taking its sub-meshes out of the raycast list: in the original a
        // sub-mesh made a raycast target through the setter stays listed after it is destroyed,
        // and every later raycast on the canvas throws (CanvasRenderer.cull on a dead object).
        static void Kill(GameObject go)
        {
            foreach (var s in go.GetComponentsInChildren<TMP_SubMeshUI>(true)) { s.raycastTarget = true; s.raycastTarget = false; }
            Destroy(go);
        }

        static IEnumerator Nothing() { yield break; }
        static IEnumerator Do(System.Action a) { a(); yield break; }
        static IEnumerator Seq(System.Action a, System.Action b) { a(); yield return null; b(); }

        // Whether a raycast at the graphic's rect centre reaches that graphic.
        static bool IsHit(Graphic g)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            var pos = ScreenAt(g.rectTransform, new Vector2(0.5f, 0.5f));
            var ped = new PointerEventData(es) { position = pos };
            var list = new List<RaycastResult>();
            es.RaycastAll(ped, list);
            return list.Any(r => r.gameObject == g.gameObject);
        }

        static string RegG(Graphic g) => "rt=" + g.raycastTarget + " en=" + g.enabled + " act=" + g.gameObject.activeInHierarchy + " depth=" + g.depth + " hit=" + IsHit(g);

        static string Reg(TextMeshProUGUI t)
        {
            var sb = new StringBuilder("text " + RegG(t) + " mc=" + t.textInfo.materialCount);
            var subs = Subs(t);
            for (int i = 0; i < subs.Count; i++)
            {
                int vc = -1; try { vc = subs[i].mesh.vertexCount; } catch { }
                sb.Append(" | sub" + i + " " + RegG(subs[i]) + " vc=" + vc + " pivot=" + V(subs[i].rectTransform.pivot));
            }
            return sb.ToString();
        }

        static List<TMP_SubMeshUI> Subs(TextMeshProUGUI t) =>
            t.GetComponentsInChildren<TMP_SubMeshUI>(true).Where(s => s.transform.parent == t.transform).ToList();

        static string Hits(Vector2 pos)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return "noEventSystem";
            var ped = new PointerEventData(es) { position = pos };
            var list = new List<RaycastResult>();
            es.RaycastAll(ped, list);
            var mine = list.Where(r => r.gameObject != null && r.gameObject.transform.IsChildOf(canvas.transform))
                .Select(r => r.gameObject.GetComponent<TMP_SubMeshUI>() != null
                    ? "sub" + r.gameObject.transform.GetSiblingIndex() + "(" + r.gameObject.transform.parent.name + ")@" + r.depth
                    : r.gameObject.name + "@" + r.depth);
            string top = list.Count == 0 || list[0].gameObject == null ? "-" : list[0].gameObject.name;
            return "[" + string.Join(" ", mine) + "] top=" + top + " n=" + list.Count;
        }

        // A point of the screen inside the first character from material index > 0.
        static Vector2? FallbackPoint(TextMeshProUGUI t)
        {
            var info = t.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible || ch.materialReferenceIndex == 0) continue;
                var c = (ch.bottomLeft + ch.topRight) * 0.5f;
                var w = t.transform.TransformPoint(c);
                return RectTransformUtility.WorldToScreenPoint(null, w);
            }
            return null;
        }

        static Vector2 ScreenAt(RectTransform rt, Vector2 normalized)
        {
            var r = rt.rect;
            var local = new Vector2(r.x + r.width * normalized.x, r.y + r.height * normalized.y);
            return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(local));
        }

        static void Log(string tag, string phase, TextMeshProUGUI t)
        {
            var rt = t.rectTransform;
            var info = t.textInfo;
            L(tag + " " + phase + " text rt=" + t.raycastTarget + " mask=" + t.maskable + " en=" + t.enabled + " act=" + t.gameObject.activeInHierarchy
              + " mc=" + (info == null ? -1 : info.materialCount) + " cc=" + (info == null ? -1 : info.characterCount)
              + " pivot=" + V(rt.pivot) + " rect=" + RectS(rt.rect) + " depth=" + t.depth);
            var subs = Subs(t);
            L(tag + " " + phase + " subs=" + subs.Count);
            for (int i = 0; i < subs.Count; i++)
                L(tag + " " + phase + " sub" + i + " " + Sub(subs[i]));
            var fp = FallbackPoint(t);
            L(tag + " " + phase + " hit centre " + Hits(ScreenAt(rt, new Vector2(0.5f, 0.5f))));
            L(tag + " " + phase + " hit corner " + Hits(ScreenAt(rt, new Vector2(0.97f, 0.06f))));
            L(tag + " " + phase + " hit glyph " + (fp.HasValue ? V(fp.Value) + " " + Hits(fp.Value) : "none"));
        }

        static TextMeshProUGUI Make(TMP_FontAsset font, float size, string name, bool? raycast = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 100);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (raycast.HasValue) tmp.raycastTarget = raycast.Value;
            tmp.enableKerning = true;
            tmp.extraPadding = true;
            tmp.fontSize = size;
            tmp.font = font;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            return tmp;
        }

        IEnumerator Frame(string tag, TextMeshProUGUI t)
        {
            yield return null;
            Log(tag, "frame", t);
            yield return new WaitForEndOfFrame();
            Log(tag, "eof", t);
        }

        IEnumerator Run()
        {
            var cgo = new GameObject("SmCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            cgo.AddComponent<GraphicRaycaster>();
            yield return null;
            L("eventsystem=" + (UnityEngine.EventSystems.EventSystem.current == null ? "null" : UnityEngine.EventSystems.EventSystem.current.name) + " screen=" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height);
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var f in fonts.OrderBy(f => f.name, StringComparer.Ordinal))
                L("font " + f.name + " fallbacks=" + string.Join(",", (f.fallbackFontAssetTable ?? new List<TMP_FontAsset>()).Select(x => x == null ? "null" : x.name)));
            L("global fallbacks=" + string.Join(",", (TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>()).Select(x => x == null ? "null" : x.name)));

            var cases = new[] { ("GRAYSTROKE REGULAR SDF", 12f), ("NotoSans-Regular", 14f) };
            var texts = new[] { "AB中文", "中文", "AB▼" };
            foreach (var (name, size) in cases)
            {
                var font = fonts.FirstOrDefault(f => f.name == name);
                if (font == null) { L("font " + name + " missing"); continue; }
                foreach (var text in texts)
                {
                    string tag = name.Replace(' ', '_') + " " + R(size) + " \"" + Hex(text) + "\"";

                    // Parent left on (the default), the text set, the sub-mesh made by the layout.
                    var t = Make(font, size, "SmText");
                    Log(tag + " default", "made", t);
                    t.text = text;
                    Log(tag + " default", "set", t);
                    t.ForceMeshUpdate();
                    Log(tag + " default", "forced", t);
                    yield return Frame(tag + " default", t);

                    // Parent off after the sub-mesh exists, then on again.
                    t.raycastTarget = false;
                    Log(tag + " offAfter", "now", t);
                    yield return Frame(tag + " offAfter", t);
                    t.raycastTarget = true;
                    Log(tag + " onAgain", "now", t);
                    yield return Frame(tag + " onAgain", t);

                    // The sub-mesh's own raycastTarget set on; then a new layout.
                    var subs = Subs(t);
                    foreach (var s in subs) s.raycastTarget = true;
                    Log(tag + " subOn", "now", t);
                    yield return Frame(tag + " subOn", t);
                    t.text = text + "x";
                    t.ForceMeshUpdate();
                    Log(tag + " subOnRelayout", "forced", t);
                    yield return Frame(tag + " subOnRelayout", t);
                    t.raycastTarget = false;
                    Log(tag + " subOnParentOff", "now", t);
                    yield return Frame(tag + " subOnParentOff", t);
                    t.raycastTarget = true;
                    foreach (var s in Subs(t)) s.raycastTarget = false;

                    // No fallback glyph left, then back.
                    var ids = string.Join(",", Subs(t).Select(s => s.GetInstanceID()));
                    t.text = "AB";
                    Log(tag + " noFallback", "set", t);
                    t.ForceMeshUpdate();
                    Log(tag + " noFallback", "forced", t);
                    yield return Frame(tag + " noFallback", t);
                    foreach (var s in Subs(t)) s.raycastTarget = true;
                    Log(tag + " noFallbackSubOn", "now", t);
                    yield return Frame(tag + " noFallbackSubOn", t);
                    foreach (var s in Subs(t)) s.raycastTarget = false;
                    t.text = "";
                    t.ForceMeshUpdate();
                    Log(tag + " empty", "forced", t);
                    yield return Frame(tag + " empty", t);
                    t.text = text;
                    t.ForceMeshUpdate();
                    L(tag + " back ids before=" + ids + " after=" + string.Join(",", Subs(t).Select(s => s.GetInstanceID())));
                    Log(tag + " back", "forced", t);
                    yield return Frame(tag + " back", t);

                    // The parent's rect resized and re-pivoted, its anchors stretched.
                    var rt = t.rectTransform;
                    rt.sizeDelta = new Vector2(300, 60);
                    Log(tag + " resize", "now", t);
                    yield return Frame(tag + " resize", t);
                    rt.pivot = new Vector2(0f, 1f);
                    Log(tag + " pivot", "now", t);
                    yield return Frame(tag + " pivot", t);
                    rt.anchorMin = new Vector2(0.3f, 0.3f);
                    rt.anchorMax = new Vector2(0.6f, 0.5f);
                    rt.sizeDelta = new Vector2(-10, -20);
                    Log(tag + " anchors", "now", t);
                    yield return Frame(tag + " anchors", t);
                    t.margin = new Vector4(5, 6, 7, 8);
                    Log(tag + " margin", "now", t);
                    yield return Frame(tag + " margin", t);

                    // Maskable.
                    t.maskable = false;
                    Log(tag + " unmaskable", "now", t);
                    yield return Frame(tag + " unmaskable", t);
                    t.maskable = true;

                    // A clone.
                    var clone = Instantiate(t.gameObject, canvas.transform, false).GetComponent<TextMeshProUGUI>();
                    Log(tag + " clone", "made", clone);
                    yield return Frame(tag + " clone", clone);
                    Kill(clone.gameObject);

                    // Disabled, then deactivated.
                    t.enabled = false;
                    Log(tag + " disabled", "now", t);
                    yield return Frame(tag + " disabled", t);
                    t.enabled = true;
                    Log(tag + " reenabled", "now", t);
                    yield return Frame(tag + " reenabled", t);
                    t.gameObject.SetActive(false);
                    Log(tag + " inactive", "now", t);
                    t.gameObject.SetActive(true);
                    Log(tag + " reactivated", "now", t);
                    yield return Frame(tag + " reactivated", t);
                    Kill(t.gameObject);
                    yield return null;

                    // Parent off before the text, so before the sub-mesh exists.
                    t = Make(font, size, "SmText", false);
                    t.text = text;
                    t.ForceMeshUpdate();
                    Log(tag + " offBefore", "forced", t);
                    yield return Frame(tag + " offBefore", t);
                    t.raycastTarget = true;
                    Log(tag + " offBeforeOn", "now", t);
                    yield return Frame(tag + " offBeforeOn", t);
                    Kill(t.gameObject);

                    // Parent unmaskable before the text.
                    t = Make(font, size, "SmText");
                    t.maskable = false;
                    t.text = text;
                    t.ForceMeshUpdate();
                    Log(tag + " unmaskableBefore", "forced", t);
                    Kill(t.gameObject);

                    // Text set without a forced update: the sub-mesh made by the canvas rebuild.
                    t = Make(font, size, "SmText");
                    t.text = text;
                    yield return Frame(tag + " rebuild", t);
                    Kill(t.gameObject);
                    yield return null;
                }
            }

            // Raycast registration of fresh sub-meshes: each step on a new text, then whether the
            // sub-mesh is hit at its rect's centre.
            {
                var font = fonts.FirstOrDefault(f => f.name == "GRAYSTROKE REGULAR SDF");
                var steps = new (string, Func<TextMeshProUGUI, TMP_SubMeshUI, IEnumerator>)[]
                {
                    ("none", (t, s) => Nothing()),
                    ("subEnabledCycle", (t, s) => Do(() => { s.enabled = false; s.enabled = true; })),
                    ("subActiveCycle", (t, s) => Do(() => { s.gameObject.SetActive(false); s.gameObject.SetActive(true); })),
                    ("parentEnabledCycle", (t, s) => Do(() => { t.enabled = false; t.enabled = true; })),
                    ("parentActiveCycle", (t, s) => Do(() => { t.gameObject.SetActive(false); t.gameObject.SetActive(true); })),
                    ("parentEnabledCycleFrame", (t, s) => Seq(() => t.enabled = false, () => t.enabled = true)),
                    ("parentActiveCycleFrame", (t, s) => Seq(() => t.gameObject.SetActive(false), () => t.gameObject.SetActive(true))),
                    ("subRtCycle", (t, s) => Do(() => { s.raycastTarget = false; s.raycastTarget = true; })),
                    ("subRtCycleThenSubEnabledCycle", (t, s) => Do(() => { s.raycastTarget = false; s.raycastTarget = true; s.enabled = false; s.enabled = true; })),
                    ("subRtCycleThenParentActiveCycle", (t, s) => Do(() => { s.raycastTarget = false; s.raycastTarget = true; t.gameObject.SetActive(false); t.gameObject.SetActive(true); })),
                    ("subRtCycleThenParentEnabledCycle", (t, s) => Do(() => { s.raycastTarget = false; s.raycastTarget = true; t.enabled = false; t.enabled = true; })),
                    ("subOffRelayout", (t, s) => Do(() => { s.raycastTarget = false; t.text = t.text + "y"; t.ForceMeshUpdate(); })),
                    ("parentOffRelayout", (t, s) => Do(() => { t.raycastTarget = false; t.text = t.text + "y"; t.ForceMeshUpdate(); })),
                    ("parentOffIdle", (t, s) => Do(() => { t.raycastTarget = false; t.text = "AB"; t.ForceMeshUpdate(); t.text = "AB中"; t.ForceMeshUpdate(); })),
                    ("subOffIdle", (t, s) => Do(() => { s.raycastTarget = false; t.text = "AB"; t.ForceMeshUpdate(); t.text = "AB中"; t.ForceMeshUpdate(); })),
                    ("idle", (t, s) => Do(() => { t.text = "AB"; t.ForceMeshUpdate(); t.text = "AB中"; t.ForceMeshUpdate(); })),
                    ("idleFrame", (t, s) => Seq(() => { t.text = "AB"; t.ForceMeshUpdate(); }, () => { t.text = "AB中"; t.ForceMeshUpdate(); })),
                    ("emptyIdleFrame", (t, s) => Seq(() => { t.text = ""; t.ForceMeshUpdate(); }, () => { t.text = "AB中"; t.ForceMeshUpdate(); })),
                    ("parentOffIdleFrame", (t, s) => Seq(() => { t.raycastTarget = false; t.text = "AB"; t.ForceMeshUpdate(); }, () => { t.text = "AB中"; t.ForceMeshUpdate(); })),
                    ("otherFallback", (t, s) => Do(() => { t.text = "AB▼"; t.ForceMeshUpdate(); })),
                    ("reparent", (t, s) => Do(() => { t.transform.SetParent(canvas.transform.GetChild(0), false); })),
                };
                var holder = new GameObject("SmHolder", typeof(RectTransform));
                holder.transform.SetParent(canvas.transform, false);
                ((RectTransform)holder.transform).sizeDelta = new Vector2(400, 100);
                holder.transform.SetAsFirstSibling();
                foreach (var (label, step) in steps)
                {
                    foreach (bool parentOn in new[] { true, false })
                    {
                        var t = Make(font, 12f, "SmReg", parentOn);
                        t.text = "AB中";
                        t.ForceMeshUpdate();
                        yield return null;
                        var s0 = Subs(t).FirstOrDefault();
                        if (s0 == null) { L("reg " + label + " nosub"); Kill(t.gameObject); continue; }
                        string tag = "reg " + label + " parent=" + parentOn;
                        L(tag + " before " + Reg(t));
                        yield return step(t, s0);
                        L(tag + " now " + Reg(t));
                        yield return null;
                        L(tag + " frame " + Reg(t));
                        yield return new WaitForEndOfFrame();
                        L(tag + " eof " + Reg(t));
                        Kill(t.gameObject);
                        yield return null;
                    }
                }
                Destroy(holder);
                yield return null;
            }

            // Controls: an Image made the same ways.
            foreach (var how in new[] { "parentedThenAdded", "addedThenParented", "addedThenParentedThenEnabledCycle", "addedThenParentedThenActiveCycle", "addedThenParentedThenRtCycle", "addedThenParentedThenParentActiveCycle" })
            {
                var host = new GameObject("SmImgHost", typeof(RectTransform));
                host.transform.SetParent(canvas.transform, false);
                ((RectTransform)host.transform).sizeDelta = new Vector2(200, 50);
                var go = new GameObject("SmImg", typeof(RectTransform));
                Image img;
                if (how == "parentedThenAdded") { go.transform.SetParent(host.transform, false); img = go.AddComponent<Image>(); }
                else { img = go.AddComponent<Image>(); go.transform.SetParent(host.transform, false); }
                ((RectTransform)go.transform).sizeDelta = new Vector2(100, 40);
                L("img " + how + " made " + RegG(img));
                if (how.EndsWith("ThenEnabledCycle")) { img.enabled = false; img.enabled = true; }
                if (how.EndsWith("ThenActiveCycle") && !how.EndsWith("ParentActiveCycle")) { go.SetActive(false); go.SetActive(true); }
                if (how.EndsWith("ThenRtCycle")) { img.raycastTarget = false; img.raycastTarget = true; }
                if (how.EndsWith("ThenParentActiveCycle")) { host.SetActive(false); host.SetActive(true); }
                L("img " + how + " now " + RegG(img));
                yield return null;
                L("img " + how + " frame " + RegG(img));
                Kill(host);
                yield return null;
            }

            // A TMP_SubMeshUI added by hand under a text, as a component's own default.
            foreach (var how in new[] { "parentedThenAdded", "addedThenParented" })
            {
                var font = fonts.FirstOrDefault(f => f.name == "NotoSans-Regular");
                var t = Make(font, 14f, "SmHand");
                t.text = "AB";
                var go = new GameObject("Hand", typeof(RectTransform));
                TMP_SubMeshUI s;
                if (how == "parentedThenAdded") { go.transform.SetParent(t.transform, false); s = go.AddComponent<TMP_SubMeshUI>(); }
                else { s = go.AddComponent<TMP_SubMeshUI>(); go.transform.SetParent(t.transform, false); }
                L("hand " + how + " added " + Sub(s) + " hit=" + IsHit(s));
                yield return null;
                L("hand " + how + " frame " + Sub(s) + " hit=" + IsHit(s));
                Kill(t.gameObject);
                yield return null;
            }
            {
                var img = new GameObject("Img", typeof(RectTransform)).AddComponent<Image>();
                L("hand image rt=" + img.raycastTarget);
                Destroy(img.gameObject);
            }

            // Every loaded TMP_SubMeshUI: scene objects and prefab assets, with their serialized values.
            var all = Resources.FindObjectsOfTypeAll<TMP_SubMeshUI>();
            L("loaded count=" + all.Length);
            var byKey = new Dictionary<string, int>();
            var saved = new List<TMP_SubMeshUI>();
            foreach (var s in all)
            {
                bool asset = !s.gameObject.scene.IsValid();
                var parent = s.transform.parent == null ? null : s.transform.parent.GetComponent<TMP_Text>();
                string key = (asset ? "asset" : "scene") + " rt=" + s.raycastTarget + " parentRt=" + (parent == null ? "none" : parent.raycastTarget.ToString())
                    + " named=" + (s.name.StartsWith("TMP SubMeshUI") ? "TMP SubMeshUI[..]" : s.name);
                byKey[key] = byKey.TryGetValue(key, out int n) ? n + 1 : 1;
                L("loaded " + (asset ? "asset " : "scene ") + Path(s.transform) + " parent=" + (parent == null ? "none" : parent.GetType().Name + " rt=" + parent.raycastTarget) + " " + Sub(s));
                if (asset && parent is TextMeshProUGUI && s.transform.parent.parent != null) saved.Add(s);
            }
            foreach (var kv in byKey.OrderBy(k => k.Key, StringComparer.Ordinal))
                L("loaded summary " + kv.Key + " count=" + kv.Value);

            // Instantiated copies of prefab texts holding a saved sub-mesh, in a 400 x 100 box:
            // the reticle's label (parent off), a language menu label (parent on), the sprite one.
            var picks = new List<TMP_SubMeshUI>();
            foreach (var key in new[] { "CalculatingImpactSection/label/", "UninstallButton/Label/", "GameplayInfoOptionText/" })
            {
                var p = saved.Where(s => Path(s.transform).Contains(key)).OrderBy(s => Path(s.transform), StringComparer.Ordinal).FirstOrDefault();
                if (p != null) picks.Add(p); else L("saved none for " + key);
            }
            foreach (var s in picks)
            {
                string label = "saved " + s.transform.parent.name;
                var src = s.transform.parent.gameObject;
                L(label + " source " + Path(s.transform) + " srcActive=" + src.activeSelf);
                var box = new GameObject("SmBox", typeof(RectTransform));
                box.transform.SetParent(canvas.transform, false);
                ((RectTransform)box.transform).sizeDelta = new Vector2(400, 100);
                GameObject copy = null;
                try { copy = Instantiate(src, box.transform, false); }
                catch (Exception e) { L(label + " instantiate exception " + e.GetType().Name + " " + e.Message); Kill(box); continue; }
                var t = copy.GetComponent<TextMeshProUGUI>();
                var crt = t.rectTransform;
                L(label + " copy active=" + copy.activeSelf + " text=\"" + Hex(t.text ?? "") + "\" amin=" + V(crt.anchorMin) + " amax=" + V(crt.anchorMax) + " sd=" + V(crt.sizeDelta) + " rect=" + RectS(crt.rect));
                if (crt.rect.width < 20 || crt.rect.height < 10)
                {
                    crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.sizeDelta = Vector2.zero; crt.anchoredPosition = Vector2.zero;
                    L(label + " stretched rect=" + RectS(crt.rect));
                }
                copy.SetActive(true);
                Log(label, "made", t);
                L(label + " made reg " + Reg(t));
                yield return null;
                L(label + " frame reg " + Reg(t));
                t.ForceMeshUpdate();
                L(label + " forced reg " + Reg(t));
                yield return new WaitForEndOfFrame();
                Log(label, "eof", t);
                foreach (var x in Subs(t)) { x.enabled = false; x.enabled = true; }
                L(label + " subEnabledCycle reg " + Reg(t));
                foreach (var x in Subs(t)) { x.raycastTarget = false; x.raycastTarget = true; }
                L(label + " subRtCycle reg " + Reg(t));
                Kill(box);
                yield return null;
            }

            // The whole reticle prefab instantiated, its labels' sub-meshes checked.
            {
                var s = picks.FirstOrDefault(x => Path(x.transform).Contains("CalculatingImpactSection"));
                if (s != null)
                {
                    var root = s.transform.root.gameObject;
                    GameObject copy = null;
                    try { copy = Instantiate(root, canvas.transform, false); copy.SetActive(true); }
                    catch (Exception e) { L("reticle instantiate exception " + e.GetType().Name + " " + e.Message); }
                    yield return null;
                    if (copy != null)
                    {
                        foreach (var sub in copy.GetComponentsInChildren<TMP_SubMeshUI>(true))
                        {
                            var t = sub.transform.parent.GetComponent<TextMeshProUGUI>();
                            L("reticle " + Path(sub.transform) + " textRt=" + (t == null ? "-" : t.raycastTarget.ToString()) + " " + Sub(sub) + " hit=" + IsHit(sub));
                        }
                        Kill(copy);
                        yield return null;
                    }
                }
            }
            Destroy(cgo);
            L("=== done");
        }
    }
}
