// uGUI/TMP behaviour that documentation leaves open, measured on small hierarchies built in code
// on an overlay canvas of scale 1, 10 s into the front end (after the front-end UI dump):
//   UIS scroll ...  ScrollRect.normalizedPosition and its Scrollbars' value/size, content
//                   smaller, equal and larger than the viewport, by pivot, offset, movement
//                   type, and when the content's size changes at run time;
//   UIS input ...   when TMP_InputField fills its text component and creates and lays out its
//                   caret (enable, first frames, select, typing, deselect), phase by phase;
//   UIS depth ...   Graphic.depth / CanvasRenderer.absoluteDepth numbering on a canvas.
// Each line is "PROBE f<frame> UIS <section> <label> <phase> <values>". Phases: "now" right after
// the step, then per frame "U" (an Update), "co" (a coroutine's yield return null), "L" (a
// LateUpdate), "R" (Canvas.willRenderCanvases, after the canvas rebuild) and "E" (end of frame).
// The same mod runs in our player, so both outputs compare line by line.
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
    public class UiSemantics : MonoBehaviour
    {
        public static float StartSeconds = 10f;
        /// Called at each phase with its name while a scenario watches frames.
        static Action<string> snap;
        /// The last phase reached, for hooks that log from inside Unity's own calls.
        static string lastPhase = "-";
        static void Phase(string p) { lastPhase = p; snap?.Invoke(p); }
        bool started;
        string scene;
        float sceneStart;
        Canvas canvas;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string F(float v) { var s = Math.Round(v, 3).ToString("0.###", Inv); return s == "-0" ? "0" : s; }
        static string F(Vector2 v) => F(v.x) + "," + F(v.y);
        static string F(Vector3 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
        static string F(Rect r) => F(r.x) + "," + F(r.y) + "," + F(r.width) + "," + F(r.height);
        static void Log(string section, string s) => Rec.Log("UIS " + section + " " + s);

        static System.Reflection.FieldInfo caretField;
        /// While set, AssignPositioningIfNeeded is replaced as PLib's TextMeshProPatcher does it:
        /// when the caret's rect differs from the text's, a coroutine copies it at the end of
        /// the frame and the method itself is skipped.
        static bool emulatePLib;

        /// Hooks TMP_InputField.AssignPositioningIfNeeded (a private method that PLib, in some 36
        /// mods, patches by name, reading the private field caretRectTrans) to log when it runs.
        public static void Install(HarmonyLib.Harmony harmony)
        {
            var t = typeof(TMP_InputField);
            var m = HarmonyLib.AccessTools.Method(t, "AssignPositioningIfNeeded");
            caretField = HarmonyLib.AccessTools.Field(t, "caretRectTrans");
            Rec.Log("UIS hooks AssignPositioningIfNeeded " + (m == null ? "absent" : "found, " + m.GetParameters().Length + " parameters, returns " + m.ReturnType.Name)
                + "; caretRectTrans " + (caretField == null ? "absent" : caretField.FieldType.Name));
            if (m != null)
                harmony.Patch(m, prefix: new HarmonyLib.HarmonyMethod(typeof(UiSemantics), nameof(ApinPrefix)), postfix: new HarmonyLib.HarmonyMethod(typeof(UiSemantics), nameof(ApinPostfix)));
        }

        static string CaretRect(TMP_InputField f)
        {
            var rt = caretField?.GetValue(f) as RectTransform;
            return rt == null ? "null" : rt.name + " " + F(rt.anchorMin) + "-" + F(rt.anchorMax) + " size " + F(rt.sizeDelta);
        }

        static bool ApinPrefix(TMP_InputField __instance)
        {
            if (!__instance.name.StartsWith("Field ")) return true;
            Log("input", "APIN enter " + __instance.name + " after " + lastPhase + " rebuild=" + CanvasUpdateRegistry.IsRebuildingLayout() + "/" + CanvasUpdateRegistry.IsRebuildingGraphics()
                + " active=" + __instance.isActiveAndEnabled + " caretRectTrans=" + CaretRect(__instance));
            if (!emulatePLib) return true;
            var caret = caretField?.GetValue(__instance) as RectTransform;
            var text = __instance.textComponent == null ? null : __instance.textComponent.rectTransform;
            if (caret == null || text == null || !__instance.textComponent.isActiveAndEnabled) return true;
            if (caret.anchorMin == text.anchorMin && caret.anchorMax == text.anchorMax && caret.anchoredPosition == text.anchoredPosition && caret.sizeDelta == text.sizeDelta && caret.pivot == text.pivot && caret.localScale == text.localScale) return true;
            __instance.StartCoroutine(PLibResize(caret, text));
            Log("input", "APIN skipped (PLib emulation), resize at end of frame");
            return false;
        }

        static IEnumerator PLibResize(RectTransform caret, RectTransform text)
        {
            yield return new WaitForEndOfFrame();
            caret.localPosition = text.localPosition; caret.localRotation = text.localRotation; caret.localScale = text.localScale;
            caret.anchorMin = text.anchorMin; caret.anchorMax = text.anchorMax; caret.anchoredPosition = text.anchoredPosition;
            caret.sizeDelta = text.sizeDelta; caret.pivot = text.pivot;
            Log("input", "APIN PLib resize ran");
        }

        static void ApinPostfix(TMP_InputField __instance)
        {
            if (__instance.name.StartsWith("Field ")) Log("input", "APIN leave " + __instance.name + " caretRectTrans=" + CaretRect(__instance));
        }

        void OnEnable() => Canvas.willRenderCanvases += WillRender;
        void OnDisable() => Canvas.willRenderCanvases -= WillRender;
        void WillRender() => Phase("R");

        void Update()
        {
            Phase("U");
            var active = SceneManager.GetActiveScene().name;
            if (active != scene) { scene = active; sceneStart = Time.realtimeSinceStartup; }
            if (!started && scene == "frontend" && Time.realtimeSinceStartup - sceneStart >= StartSeconds)
            {
                started = true;
                StartCoroutine(Run());
                StartCoroutine(EndOfFrames());
            }
        }

        void LateUpdate() => Phase("L");

        IEnumerator EndOfFrames()
        {
            var wait = new WaitForEndOfFrame();
            while (true) { yield return wait; Phase("E"); }
        }

        /// Lets `frames` frames pass, calling `log` at every phase of each (and once now).
        IEnumerator Watch(int frames, Action<string> log, bool phases = true)
        {
            lastPhase = "now";
            log("now");
            snap = phases ? log : null;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                lastPhase = "co";
                if (phases) log("co");
            }
            snap = null;
            if (!phases) log("+" + frames);
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(string name, Transform parent)
            => NewRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        IEnumerator Run()
        {
            var cgo = new GameObject("UisCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            cgo.AddComponent<GraphicRaycaster>();
            yield return null;
            Rec.Log("=== UIS scroll");
            yield return Scroll();
            Rec.Log("=== UIS input");
            yield return Input();
            Rec.Log("=== UIS depth");
            yield return Depth();
            Destroy(cgo);
            Rec.Log("=== UIS done");
        }

        // ------------------------------------------------------------------ ScrollRect

        sealed class Sr
        {
            public string Label;
            public ScrollRect Rect;
            public RectTransform Content;
            public Scrollbar H, V;
            public int Changes;
        }

        Sr MakeScroll(string label, Vector2 at, Vector2 view, Vector2 content, Vector2 pivot, Vector2 offset, ScrollRect.MovementType movement, bool horizontal = true, bool vertical = true, float scale = 1f,
            Transform parent = null, bool bars = true, bool contentCanvas = false, bool kscroll = false)
        {
            var c = new Vector2(0.5f, 0.5f);
            var root = NewRect("SR " + label, parent ?? canvas.transform, c, c, c, at, view);
            root.gameObject.SetActive(false);
            root.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 1f);
            var viewport = Stretch("Viewport", root);
            viewport.gameObject.AddComponent<RectMask2D>();
            var ct = NewRect("Content", viewport, pivot, pivot, pivot, offset, content);
            ct.localScale = new Vector3(scale, scale, 1f);
            ct.gameObject.AddComponent<Image>().color = new Color(0.8f, 0.5f, 0.2f, 1f);
            Scrollbar Bar(string name, bool isH)
            {
                var b = isH ? NewRect(name, root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 8))
                            : NewRect(name, root, new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(8, 0));
                b.gameObject.AddComponent<Image>();
                var area = Stretch("Sliding Area", b);
                var handle = Stretch("Handle", area);
                handle.gameObject.AddComponent<Image>().color = Color.gray;
                var sb = b.gameObject.AddComponent<Scrollbar>();
                sb.handleRect = handle;
                sb.direction = isH ? Scrollbar.Direction.LeftToRight : Scrollbar.Direction.BottomToTop;
                return sb;
            }
            var sr = new Sr { Label = label, Content = ct, H = Bar("HBar", true), V = Bar("VBar", false) };
            ScrollRect s = kscroll ? root.gameObject.AddComponent<KScrollRect>() : root.gameObject.AddComponent<ScrollRect>();
            s.content = ct;
            s.viewport = viewport;
            s.horizontal = horizontal;
            s.vertical = vertical;
            s.movementType = movement;
            if (bars)
            {
                s.horizontalScrollbar = sr.H;
                s.verticalScrollbar = sr.V;
            }
            if (contentCanvas) ct.gameObject.AddComponent<Canvas>();
            s.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            s.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            s.onValueChanged.AddListener(_ => sr.Changes++);
            sr.Rect = s;
            root.gameObject.SetActive(true);
            return sr;
        }

        // Scrollbars first: reading normalizedPosition recomputes the bounds.
        static string State(Sr s) =>
            "hbar=" + F(s.H.value) + "/" + F(s.H.size) + " vbar=" + F(s.V.value) + "/" + F(s.V.size)
            + " np=" + F(s.Rect.normalizedPosition) + " pos=" + F(s.Content.anchoredPosition) + " vel=" + F(s.Rect.velocity) + " changes=" + s.Changes;

        void LogAll(List<Sr> all, string phase)
        {
            foreach (var s in all) Log("scroll", s.Label + " " + phase + " " + State(s));
        }

        IEnumerator Settle(List<Sr> all, params int[] frames)
        {
            LogAll(all, "now");
            int done = 0;
            float start = Time.unscaledTime;
            foreach (int f in frames)
            {
                while (done < f) { yield return null; done++; }
                Log("scroll", "time +" + f + " " + F(Time.unscaledTime - start) + "s dt " + F(Time.unscaledDeltaTime));
                LogAll(all, "+" + f);
            }
        }

        static void Clear(List<Sr> all)
        {
            foreach (var s in all) Destroy(s.Rect.gameObject);
            all.Clear();
        }

        IEnumerator Scroll()
        {
            var view = new Vector2(200, 100);
            var all = new List<Sr>();
            var unr = ScrollRect.MovementType.Unrestricted;
            float[] pivots = { 0f, 0.5f, 1f };

            // 1. Content smaller than the viewport (100x50), Unrestricted, by pivot (= anchor) and
            //    offset from the anchor.
            Rec.Log("UIS scroll --- smaller, unrestricted, pivot/offset grid");
            foreach (float p in pivots)
                foreach (float dx in new[] { -80f, -60f, -20f, 0f, 20f, 60f, 80f })
                    all.Add(MakeScroll("small p" + F(p) + " d" + F(dx), Vector2.zero, view, new Vector2(100, 50), new Vector2(p, p), new Vector2(dx, dx / 2), unr));
            yield return Settle(all, 1, 3);
            Clear(all);

            // 2. Content exactly the viewport's size, and 0.004 off it.
            Rec.Log("UIS scroll --- equal, unrestricted");
            foreach (float p in pivots)
                foreach (float dx in new[] { -20f, 0f, 20f })
                    all.Add(MakeScroll("equal p" + F(p) + " d" + F(dx), Vector2.zero, view, view, new Vector2(p, p), new Vector2(dx, dx / 2), unr));
            all.Add(MakeScroll("equal+0.004 p0", Vector2.zero, view, view + new Vector2(0.004f, 0.004f), Vector2.zero, Vector2.zero, unr));
            all.Add(MakeScroll("equal-0.004 p0", Vector2.zero, view, view - new Vector2(0.004f, 0.004f), Vector2.zero, Vector2.zero, unr));
            all.Add(MakeScroll("equal+0.02 p0", Vector2.zero, view, view + new Vector2(0.02f, 0.02f), Vector2.zero, Vector2.zero, unr));
            yield return Settle(all, 1, 3);
            Clear(all);

            // 3. Content larger (400x300): the scrollable range, and overshoot.
            Rec.Log("UIS scroll --- larger, unrestricted");
            foreach (float p in pivots)
                foreach (float dx in new[] { -250f, -100f, 0f, 100f, 250f })
                    all.Add(MakeScroll("large p" + F(p) + " d" + F(dx), Vector2.zero, view, new Vector2(400, 300), new Vector2(p, p), new Vector2(dx, dx), unr));
            yield return Settle(all, 1, 3);
            Clear(all);

            // 4. Movement types: where the content goes, over time.
            Rec.Log("UIS scroll --- movement types");
            foreach (ScrollRect.MovementType mt in new[] { ScrollRect.MovementType.Elastic, ScrollRect.MovementType.Clamped, unr })
                foreach (float p in new[] { 0f, 0.5f, 1f })
                {
                    var pv = new Vector2(p, 1 - p);
                    all.Add(MakeScroll(mt + " small p" + F(pv) + " d30,-20", Vector2.zero, view, new Vector2(100, 50), pv, new Vector2(30, -20), mt));
                    all.Add(MakeScroll(mt + " large p" + F(pv) + " d50,-50", Vector2.zero, view, new Vector2(400, 300), pv, new Vector2(50, -50), mt));
                    all.Add(MakeScroll(mt + " equalw p" + F(pv) + " d10,0", Vector2.zero, view, new Vector2(200, 300), pv, new Vector2(10, 0), mt));
                }
            yield return Settle(all, 1, 2, 5, 10, 20, 60);
            Clear(all);

            // 5. One axis disabled (Sweep By Type's list: vertical only, content as wide as the
            //    viewport and taller; and narrower).
            Rec.Log("UIS scroll --- vertical only");
            foreach (ScrollRect.MovementType mt in new[] { ScrollRect.MovementType.Elastic, ScrollRect.MovementType.Clamped, unr })
            {
                all.Add(MakeScroll(mt + " vonly w=view tall", Vector2.zero, view, new Vector2(200, 300), new Vector2(0, 1), Vector2.zero, mt, horizontal: false));
                all.Add(MakeScroll(mt + " vonly narrow tall d10", Vector2.zero, view, new Vector2(150, 300), new Vector2(0, 1), new Vector2(10, 0), mt, horizontal: false));
                all.Add(MakeScroll(mt + " honly small d10", Vector2.zero, view, new Vector2(100, 50), new Vector2(0, 1), new Vector2(10, -10), mt, vertical: false));
                all.Add(MakeScroll(mt + " none small d10", Vector2.zero, view, new Vector2(100, 50), new Vector2(0, 1), new Vector2(10, -10), mt, false, false));
            }
            yield return Settle(all, 1, 5, 60);
            Clear(all);

            // 6. Scaled content (Blueprints Expanded's preview: 400x400 at scale 0.25, centred).
            Rec.Log("UIS scroll --- scaled content");
            foreach (ScrollRect.MovementType mt in new[] { ScrollRect.MovementType.Elastic, ScrollRect.MovementType.Clamped, unr })
            {
                var c = new Vector2(0.5f, 0.5f);
                all.Add(MakeScroll(mt + " scaled0.25 c d20,10", Vector2.zero, view, new Vector2(400, 400), c, new Vector2(20, 10), mt, scale: 0.25f));
                all.Add(MakeScroll(mt + " scaled0.25 c d0", Vector2.zero, view, new Vector2(400, 400), c, Vector2.zero, mt, scale: 0.25f));
            }
            yield return Settle(all, 1, 5, 60);
            Clear(all);

            // 7. The content's size changing at run time, and normalizedPosition set while the
            //    content fits.
            Rec.Log("UIS scroll --- size changes");
            foreach (ScrollRect.MovementType mt in new[] { ScrollRect.MovementType.Elastic, ScrollRect.MovementType.Clamped, unr })
                foreach (float p in new[] { 0f, 0.5f })
                    all.Add(MakeScroll(mt + " resize p" + F(new Vector2(p, 1 - p)), Vector2.zero, view, new Vector2(400, 300), new Vector2(p, 1 - p), Vector2.zero, mt));
            yield return Settle(all, 2);
            Rec.Log("UIS scroll step normalizedPosition=(0.5,0.5)");
            foreach (var s in all) s.Rect.normalizedPosition = new Vector2(0.5f, 0.5f);
            yield return Settle(all, 1, 5);
            Rec.Log("UIS scroll step shrink to 100x50");
            foreach (var s in all) s.Content.sizeDelta = new Vector2(100, 50);
            yield return Settle(all, 1, 2, 5, 10, 60);
            Rec.Log("UIS scroll step normalizedPosition=(0.5,0.5) while it fits");
            foreach (var s in all) s.Rect.normalizedPosition = new Vector2(0.5f, 0.5f);
            yield return Settle(all, 1, 5, 60);
            Rec.Log("UIS scroll step horizontalNormalizedPosition=1, verticalNormalizedPosition=0 while it fits");
            foreach (var s in all) { s.Rect.horizontalNormalizedPosition = 1f; s.Rect.verticalNormalizedPosition = 0f; }
            yield return Settle(all, 1, 5, 60);
            Rec.Log("UIS scroll step grow to 400x300");
            foreach (var s in all) s.Content.sizeDelta = new Vector2(400, 300);
            yield return Settle(all, 1, 2, 5, 60);
            Rec.Log("UIS scroll step grow the viewport (root) to 500x400");
            foreach (var s in all) ((RectTransform)s.Rect.transform).sizeDelta = new Vector2(500, 400);
            yield return Settle(all, 1, 2, 5, 60);
            Clear(all);

            // 8. Content the viewport's size, a hair off it: no scrollbars (no feedback through
            //    them), under a parent at the game's canvas scale (0.86) at a fractional position,
            //    and as the game's mods build it (a Canvas on the content, KScrollRect).
            Rec.Log("UIS scroll --- hairline offsets");
            var scaled = NewRect("Scaled 0.86", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(37.37f, 11.11f), new Vector2(800, 600));
            scaled.localScale = new Vector3(0.86f, 0.86f, 1f);
            foreach (var (pname, parent) in new[] { ("s1", canvas.transform), ("s0.86", (Transform)scaled) })
                foreach (float dx in new[] { -0.1f, -0.01f, -0.001f, -0.0001f, -0.00001f, 0f, 0.00001f, 0.001f })
                {
                    all.Add(MakeScroll(pname + " eq p0,1 dx" + dx.ToString("R", Inv), new Vector2(0.3f, 0.7f), new Vector2(264f, 254.26f), new Vector2(264f, 332f), new Vector2(0, 1), new Vector2(dx, 0), unr, parent: parent, bars: false));
                    all.Add(MakeScroll(pname + " small c dx" + dx.ToString("R", Inv), new Vector2(0.3f, 0.7f), new Vector2(580f, 562.83f), new Vector2(400f, 400f), new Vector2(0.5f, 0.5f), new Vector2(dx, dx), unr, parent: parent, bars: false, scale: 0.25f));
                }
            foreach (ScrollRect.MovementType mt in new[] { ScrollRect.MovementType.Elastic, ScrollRect.MovementType.Clamped })
            {
                all.Add(MakeScroll("s0.86 " + mt + " vonly canvas-content", new Vector2(0.3f, 0.7f), new Vector2(264f, 254.26f), new Vector2(264f, 332f), new Vector2(0, 1), Vector2.zero, mt, horizontal: false, parent: scaled, bars: false, contentCanvas: true));
                all.Add(MakeScroll("s0.86 " + mt + " vonly canvas-content kscroll", new Vector2(0.3f, 0.7f), new Vector2(264f, 254.26f), new Vector2(264f, 332f), new Vector2(0, 1), Vector2.zero, mt, horizontal: false, parent: scaled, bars: false, contentCanvas: true, kscroll: true));
                all.Add(MakeScroll("s0.86 " + mt + " small c scaled", new Vector2(0.3f, 0.7f), new Vector2(580f, 562.83f), new Vector2(400f, 400f), new Vector2(0.5f, 0.5f), Vector2.zero, mt, parent: scaled, bars: false, scale: 0.25f));
            }
            yield return Settle(all, 1, 5, 60);
            foreach (var s in all) Log("scroll", "raw " + s.Label + " " + Raw(s));
            Clear(all);

            // 9. Random fractional layouts with the content exactly the viewport's size (or
            //    smaller, scaled): whether 0 or 1 is reported when it fits depends on rounding.
            Rec.Log("UIS scroll --- random fitting layouts");
            var rng = new System.Random(20261022);
            float R(double lo, double hi) => (float)(lo + rng.NextDouble() * (hi - lo));
            for (int i = 0; i < 48; i++)
            {
                var vsz = new Vector2(R(80, 600), R(80, 600));
                var at = new Vector2(R(-200, 200), R(-150, 150));
                var parent = i % 2 == 0 ? (Transform)scaled : canvas.transform;
                switch (i % 4 / 2)
                {
                    case 0:
                        all.Add(MakeScroll("rnd" + i + " eq p0,1", at, vsz, vsz, new Vector2(0, 1), Vector2.zero, unr, parent: parent, bars: false));
                        break;
                    default:
                        all.Add(MakeScroll("rnd" + i + " small c", at, vsz, vsz * 2f, new Vector2(0.5f, 0.5f), Vector2.zero, unr, parent: parent, bars: false, scale: 0.25f));
                        break;
                }
            }
            yield return Settle(all, 1);
            foreach (var s in all) Log("scroll", "raw " + s.Label + " " + Raw(s));
            Clear(all);

            // 10. Blueprints Expanded's preview: every frame its screen keeps the point under the
            //    pointer fixed while it sets the content's scale (here always the same scale):
            //    p0 = InverseTransformPoint(pointer); localScale = s; p1 = InverseTransformPoint
            //    (pointer); localPosition += (p1 - p0) * s. Any drift comes from the transforms.
            Rec.Log("UIS scroll --- zoom anchoring");
            foreach (ScrollRect.MovementType mt in new[] { ScrollRect.MovementType.Elastic, unr })
            {
                all.Add(MakeScroll(mt + " zoom fixed", new Vector2(0.3f, 0.7f), new Vector2(580f, 562.83f), new Vector2(400f, 400f), new Vector2(0.5f, 0.5f), Vector2.zero, mt, parent: scaled, bars: false, scale: 0.25f));
                all.Add(MakeScroll(mt + " zoom from1", new Vector2(0.3f, 0.7f), new Vector2(580f, 562.83f), new Vector2(400f, 400f), new Vector2(0.5f, 0.5f), Vector2.zero, mt, parent: scaled, bars: false, scale: 1f));
            }
            var zoom = new float[all.Count];
            for (int i = 0; i < all.Count; i++) zoom[i] = all[i].Content.localScale.x;
            var pointer = new Vector3(700f, 380f, 0f);
            for (int frame = 0; frame <= 120; frame++)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var ct = all[i].Content;
                    zoom[i] = Mathf.Lerp(zoom[i], 0.25f, Mathf.Min(4f * Time.unscaledDeltaTime, 1f));
                    var p0 = ct.InverseTransformPoint(pointer);
                    ct.localScale = new Vector3(zoom[i], zoom[i], 1f);
                    var p1 = ct.InverseTransformPoint(pointer);
                    ct.localPosition += (p1 - p0) * zoom[i];
                }
                if (frame % 30 == 0 || frame < 3)
                    foreach (var s in all) Log("scroll", s.Label + " f+" + frame + " scale " + F(s.Content.localScale.x) + " pos " + RF(s.Content.anchoredPosition) + " np " + F(s.Rect.normalizedPosition));
                yield return null;
            }
            Clear(all);
            Destroy(scaled.gameObject);
        }

        static string RF(float v) => v.ToString("R", Inv);
        static string RF(Vector3 v) => RF(v.x) + "," + RF(v.y);

        /// The inputs of the normalized position at full precision: the view's rect, the content's
        /// world corners and those corners in the view's space, and the result.
        static string Raw(Sr s)
        {
            var view = s.Rect.viewport != null ? s.Rect.viewport : (RectTransform)s.Rect.transform;
            var r = view.rect;
            var world = new Vector3[4];
            s.Content.GetWorldCorners(world);
            var m = view.worldToLocalMatrix;
            var sb = new StringBuilder();
            sb.Append("view=").Append(RF(r.xMin)).Append(',').Append(RF(r.yMin)).Append(',').Append(RF(r.width)).Append(',').Append(RF(r.height));
            sb.Append(" world=").Append(string.Join(";", world.Select(RF)));
            sb.Append(" local=").Append(string.Join(";", world.Select(w => RF(m.MultiplyPoint3x4(w)))));
            sb.Append(" np=").Append(RF(s.Rect.horizontalNormalizedPosition)).Append(',').Append(RF(s.Rect.verticalNormalizedPosition));
            return sb.ToString();
        }

        // ------------------------------------------------------------------ TMP_InputField

        static TMP_FontAsset Font()
        {
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            return fonts.FirstOrDefault(f => f.name.StartsWith("NotoSans-Regular")) ?? TMP_Settings.defaultFontAsset ?? fonts.FirstOrDefault();
        }

        static string Code(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char ch in s) sb.Append(ch >= 0x20 && ch < 0x7f ? ch.ToString() : "\\u" + ((int)ch).ToString("X4"));
            return sb.Append('"').ToString();
        }

        static TMP_InputField MakeField(string name, bool configureBeforeEnable, string presetText = null, bool noCanvas = false, bool textLater = false)
        {
            var parent = noCanvas ? null : UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.name == "UisCanvas").transform;
            var c = new Vector2(0.5f, 0.5f);
            var root = NewRect(name, parent, c, c, c, new Vector2(0, 100), new Vector2(160, 30));
            if (configureBeforeEnable) root.gameObject.SetActive(false);
            root.gameObject.AddComponent<Image>();
            var area = Stretch("Text Area", root);
            area.offsetMin = new Vector2(10, 6); area.offsetMax = new Vector2(-10, -7);
            area.gameObject.AddComponent<RectMask2D>();
            var font = Font();
            TextMeshProUGUI Label(string n, string t)
            {
                var rt = Stretch(n, area);
                var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.font = font;
                tmp.fontSize = 14;
                tmp.text = t;
                return tmp;
            }
            var placeholder = Label("Placeholder", "Enter text...");
            var text = Label("Text", "");
            var field = root.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            if (!textLater)
            {
                field.textComponent = text;
                field.placeholder = placeholder;
            }
            if (presetText != null) field.text = presetText;
            if (configureBeforeEnable) root.gameObject.SetActive(true);
            return field;
        }

        static string FieldState(TMP_InputField f)
        {
            if (f == null) return "destroyed";
            var t = f.textComponent;
            var sb = new StringBuilder();
            sb.Append("focused=").Append(f.isFocused).Append(" text=").Append(Code(f.text));
            if (t != null)
            {
                sb.Append(" tc=").Append(Code(t.text));
                var info = t.textInfo;
                sb.Append(" chars=").Append(info == null ? "null" : info.characterCount.ToString());
                sb.Append(" overflow=").Append(t.isTextOverflowing);
                sb.Append(" tcRect=").Append(F(t.rectTransform.rect));
            }
            if (f.placeholder != null) sb.Append(" ph=").Append(f.placeholder.enabled ? "on" : "off");
            // By name: the caret's type is public in the shipped package, internal in ours.
            var carets = f.GetComponentsInChildren<Graphic>(true).Where(g => g.GetType().Name == "TMP_SelectionCaret").ToArray();
            if (carets.Length == 0) sb.Append(" caret=none");
            foreach (var c in carets)
            {
                var rt = c.rectTransform;
                sb.Append(" caret=").Append(c.name).Append(" parent=").Append(rt.parent.name).Append('#').Append(rt.GetSiblingIndex())
                  .Append(" rect=").Append(F(rt.rect)).Append(" anchors=").Append(F(rt.anchorMin)).Append('-').Append(F(rt.anchorMax))
                  .Append(" pos=").Append(F(rt.anchoredPosition)).Append(" size=").Append(F(rt.sizeDelta)).Append(" pivot=").Append(F(rt.pivot))
                  .Append(" enabled=").Append(c.enabled).Append(" active=").Append(c.gameObject.activeSelf)
                  .Append(" hide=").Append(c.gameObject.hideFlags)
                  .Append(" comps=").Append(string.Join(",", c.GetComponents<Component>().Select(k => k.GetType().Name)));
                var le = c.GetComponent<LayoutElement>();
                if (le != null) sb.Append(" ignoreLayout=").Append(le.ignoreLayout);
                sb.Append(" mats=").Append(c.canvasRenderer.materialCount);
            }
            sb.Append(" children=").Append(string.Join(",", f.textViewport == null ? new string[0] : f.textViewport.Cast<Transform>().Select(k => k.name).ToArray()));
            return sb.ToString();
        }

        IEnumerator Input()
        {
            TMP_InputField field = null;
            Action<string> log = phase => Log("input", label + " " + phase + " " + FieldState(field));

            // 1. Configured while inactive (a prefab or a clone), then activated.
            label = "I1 inactive-built";
            field = MakeField("Field I1", true);
            yield return Watch(3, log);
            Log("input", label + " +30frames " + FieldState(field));
            for (int i = 0; i < 30; i++) yield return null;
            Log("input", label + " after30 " + FieldState(field));
            label = "I1 select";
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(field.gameObject);
            yield return Watch(3, log);
            label = "I1 settext-focused";
            field.text = "abc";
            yield return Watch(2, log);
            label = "I1 deselect";
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            yield return Watch(2, log);
            label = "I1 settext-empty-unfocused";
            field.text = "";
            yield return Watch(2, log);
            label = "I1 disable";
            field.gameObject.SetActive(false);
            log("now");
            label = "I1 reenable";
            field.gameObject.SetActive(true);
            yield return Watch(2, log);
            Destroy(field.gameObject);
            yield return null;

            // 2. Added to an active object, its references set after its OnEnable (how mods
            //    build fields in code).
            label = "I2 active-built";
            field = MakeField("Field I2", false);
            yield return Watch(3, log);
            label = "I2 activate-call";
            field.ActivateInputField();
            yield return Watch(3, log);
            label = "I2 deactivate";
            field.DeactivateInputField();
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            yield return Watch(2, log);
            Destroy(field.gameObject);
            yield return null;

            // 3. Text set before activation, and set while unfocused.
            label = "I3 preset-hello";
            field = MakeField("Field I3", true, "hello");
            yield return Watch(2, log);
            label = "I3 settext-unfocused";
            field.text = "bye";
            yield return Watch(2, log);
            Destroy(field.gameObject);
            yield return null;

            // 4. A clone of an inactive, never-enabled template, and one of an enabled field.
            label = "I4 template";
            var template = MakeField("Field I4 template", true);
            template.gameObject.SetActive(false);
            field = Instantiate(template.gameObject, template.transform.parent).GetComponent<TMP_InputField>();
            field.name = "Field I4 clone";
            log("clone-inactive");
            label = "I4 clone";
            field.gameObject.SetActive(true);
            yield return Watch(2, log);
            Destroy(field.gameObject);
            Destroy(template.gameObject);
            yield return null;

            // 6. Enabled before it has a text component, which is assigned later (a mod's asset
            //    bundle whose texts are replaced at run time, then given text).
            label = "I6 textcomponent-later";
            field = MakeField("Field I6", true, textLater: true);
            log("enabled");
            var viewport = field.textViewport;
            field.textComponent = viewport.Find("Text").GetComponent<TMP_Text>();
            field.placeholder = viewport.Find("Placeholder").GetComponent<Graphic>();
            yield return Watch(2, log);
            label = "I6 settext-empty";
            field.text = "";
            yield return Watch(1, log);
            label = "I6 settext-ab";
            field.text = "ab";
            yield return Watch(1, log);
            label = "I6 settext-empty-again";
            field.text = "";
            yield return Watch(1, log);
            label = "I6 forcelabelupdate";
            field.ForceLabelUpdate();
            yield return Watch(1, log);
            label = "I6 select";
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(field.gameObject);
            yield return Watch(2, log);
            label = "I6 deselect";
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            yield return Watch(1, log);
            label = "I6 disable-enable";
            field.gameObject.SetActive(false);
            field.gameObject.SetActive(true);
            yield return Watch(1, log);
            Destroy(field.gameObject);
            yield return null;

            // 7. Built and enabled outside any canvas (PLib's PTextField), then parented to one.
            label = "I7 no-canvas";
            field = MakeField("Field I7", true, noCanvas: true);
            log("enabled");
            yield return Watch(1, log);
            label = "I7 parented";
            field.transform.SetParent(canvas.transform, false);
            yield return Watch(2, log);
            label = "I7 select";
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(field.gameObject);
            yield return Watch(2, log);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            Destroy(field.gameObject);
            yield return null;

            // 8. Enabled under a canvas whose GameObject is inactive.
            label = "I8 inactive-canvas";
            var off = Stretch("Inactive parent", canvas.transform);
            off.gameObject.SetActive(false);
            field = MakeField("Field I8", true);
            field.transform.SetParent(off, false);
            log("built");
            off.gameObject.SetActive(true);
            yield return Watch(1, log);
            Destroy(off.gameObject);
            yield return null;

            // 9. PLib's replacement of AssignPositioningIfNeeded, with the field hidden again in the
            //    frame it was shown (its coroutine stops), then shown, selected, typed into.
            label = "I9 plib";
            emulatePLib = true;
            field = MakeField("Field I9", true);
            log("enabled");
            field.gameObject.SetActive(false);
            yield return Watch(2, log, phases: false);
            label = "I9 plib reshown";
            field.gameObject.SetActive(true);
            yield return Watch(2, log);
            label = "I9 plib select";
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(field.gameObject);
            yield return Watch(2, log);
            label = "I9 plib settext";
            field.text = "x";
            yield return Watch(1, log);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            emulatePLib = false;
            Destroy(field.gameObject);
            yield return null;

            // 5. Under an inactive parent that becomes active, and the canvas rebuild forced
            //    before the first frame.
            label = "I5 forced-update";
            field = MakeField("Field I5", true);
            log("before-force");
            Canvas.ForceUpdateCanvases();
            yield return Watch(1, log);
            Destroy(field.gameObject);
            yield return null;
        }

        string label;

        // ------------------------------------------------------------------ depth

        IEnumerator Depth()
        {
            var root = Stretch("Depth", canvas.transform);
            var c = new Vector2(0.5f, 0.5f);
            int n = 0;
            var watched = new List<(string, CanvasRenderer)>();
            Image Img(string name, Transform parent, Vector2 pos, Vector2 size)
            {
                var rt = NewRect(name, parent, c, c, c, pos, size);
                var img = rt.gameObject.AddComponent<Image>();
                watched.Add((name, img.canvasRenderer));
                return img;
            }
            RectTransform Group(string name, Transform parent)
            {
                var rt = NewRect(name, parent, c, c, c, Vector2.zero, new Vector2(50, 50));
                return rt;
            }
            var tex2 = new Texture2D(4, 4);
            Img("A image", root, new Vector2(-300, 0), new Vector2(20, 20));
            var b = Group("B renderer-only", root); watched.Add(("B renderer-only", b.gameObject.AddComponent<CanvasRenderer>()));
            Img("B2 group-without-renderer>child", Group("B2 group-without-renderer", root), Vector2.zero, new Vector2(10, 10));
            Img("C alpha0", root, new Vector2(-280, 0), new Vector2(20, 20)).color = new Color(1, 1, 1, 0);
            Img("D disabled", root, new Vector2(-260, 0), new Vector2(20, 20)).enabled = false;
            Img("E inactive", root, new Vector2(-240, 0), new Vector2(20, 20)).gameObject.SetActive(false);
            var g1 = Group("F group0-blocks", root); var cg1 = g1.gameObject.AddComponent<CanvasGroup>(); cg1.alpha = 0; cg1.blocksRaycasts = true;
            Img("F1 under group0-blocks", g1, Vector2.zero, new Vector2(10, 10));
            var g2 = Group("G group0-noblock", root); var cg2 = g2.gameObject.AddComponent<CanvasGroup>(); cg2.alpha = 0; cg2.blocksRaycasts = false;
            Img("G1 under group0-noblock", g2, Vector2.zero, new Vector2(10, 10));
            var mask = NewRect("H rectmask", root, c, c, c, new Vector2(-200, 0), new Vector2(20, 20)); mask.gameObject.AddComponent<RectMask2D>();
            Img("H1 culled", mask, new Vector2(200, 200), new Vector2(10, 10));
            Img("H2 inside", mask, Vector2.zero, new Vector2(10, 10));
            Img("I offscreen", root, new Vector2(5000, 5000), new Vector2(20, 20));
            var nested = Group("J nested-canvas", root); var nc = nested.gameObject.AddComponent<Canvas>();
            Img("J1 in nested", nested, Vector2.zero, new Vector2(10, 10));
            var ov = Group("K override-canvas", root); var oc = ov.gameObject.AddComponent<Canvas>(); oc.overrideSorting = true; oc.sortingOrder = 5;
            Img("K1 in override", ov, Vector2.zero, new Vector2(10, 10));
            Img("K2 in override", ov, Vector2.zero, new Vector2(10, 10));
            var tmpGo = NewRect("M tmp-empty", root, c, c, c, new Vector2(-100, 0), new Vector2(50, 20)).gameObject;
            var tmp = tmpGo.AddComponent<TextMeshProUGUI>(); tmp.font = Font(); tmp.text = "";
            watched.Add(("M tmp-empty", tmp.canvasRenderer));
            var tmp2Go = NewRect("M2 tmp-text", root, c, c, c, new Vector2(-60, 0), new Vector2(50, 20)).gameObject;
            var tmp2 = tmp2Go.AddComponent<TextMeshProUGUI>(); tmp2.font = Font(); tmp2.text = "Hi";
            watched.Add(("M2 tmp-text", tmp2.canvasRenderer));
            Img("P zero-size", root, new Vector2(0, 0), Vector2.zero);
            var q = Img("Q mask", root, new Vector2(40, 0), new Vector2(30, 30)); q.gameObject.AddComponent<Mask>();
            Img("Q1 masked", q.transform, Vector2.zero, new Vector2(10, 10));
            var r1 = Img("R1 overlap-tex2", root, new Vector2(80, 0), new Vector2(30, 30)); r1.sprite = Sprite.Create(tex2, new Rect(0, 0, 4, 4), c);
            Img("R2 overlap-default", root, new Vector2(80, 0), new Vector2(30, 30));
            var r3 = Img("R3 overlap-tex2", root, new Vector2(80, 0), new Vector2(30, 30)); r3.sprite = r1.sprite;
            Img("S1 apart", root, new Vector2(150, 0), new Vector2(10, 10));
            Img("S2 apart", root, new Vector2(200, 0), new Vector2(10, 10));
            var raw = NewRect("T rawimage", root, c, c, c, new Vector2(250, 0), new Vector2(10, 10)); watched.Add(("T rawimage", raw.gameObject.AddComponent<RawImage>().canvasRenderer));
            Img("Z last", root, new Vector2(300, 0), new Vector2(10, 10));
            void Dump(string phase)
            {
                var sb = new StringBuilder();
                foreach (var (name, cr) in watched)
                {
                    if (cr == null) { sb.Append(" [").Append(name).Append(" destroyed]"); continue; }
                    var g = cr.GetComponent<Graphic>();
                    sb.Append(" [").Append(name).Append(" abs=").Append(cr.absoluteDepth).Append(" rel=").Append(cr.relativeDepth);
                    if (g != null) sb.Append(" depth=").Append(g.depth);
                    sb.Append(" cull=").Append(cr.cull).Append(']');
                }
                Log("depth", "D " + phase + sb);
            }
            n++;
            yield return Watch(2, Dump);
            yield return null;
            Dump("+3");
            Rec.Log("UIS depth step: E active, D enabled, G alpha 1, H1 moved inside, an image inserted at sibling 0");
            watched[5].Item2.gameObject.SetActive(true);
            watched[4].Item2.GetComponent<Image>().enabled = true;
            cg2.alpha = 1;
            watched.First(w => w.Item1 == "H1 culled").Item2.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            Img("Y inserted first", root, new Vector2(-320, 0), new Vector2(10, 10)).transform.SetAsFirstSibling();
            yield return Watch(2, Dump);
            Rec.Log("UIS depth step: everything disabled again (D, E), G alpha 0");
            watched[5].Item2.gameObject.SetActive(false);
            watched[4].Item2.GetComponent<Image>().enabled = false;
            cg2.alpha = 0;
            yield return Watch(2, Dump);
            // One change at a time: which changes renumber the canvas.
            var a = watched[0].Item2.GetComponent<Image>();
            var e = watched[5].Item2.gameObject;
            var steps = new (string, System.Action)[]
            {
                ("nothing", () => { }),
                ("A colour", () => a.color = Color.red),
                ("A sprite", () => a.sprite = r1.sprite),
                ("A moved", () => a.rectTransform.anchoredPosition += new Vector2(3, 0)),
                ("E active", () => e.SetActive(true)),
                ("E inactive", () => e.SetActive(false)),
                ("E active again", () => e.SetActive(true)),
                ("C destroyed", () => Destroy(watched[3].Item2.gameObject)),
                ("image appended", () => Img("X appended", root, new Vector2(320, 0), new Vector2(10, 10))),
                ("A to sibling 6", () => a.transform.SetSiblingIndex(6)),
                ("Q showMaskGraphic off", () => q.GetComponent<Mask>().showMaskGraphic = false),
                ("Q Mask disabled", () => q.GetComponent<Mask>().enabled = false),
                ("G1 alpha group blocks", () => cg2.blocksRaycasts = true),
                ("H rectmask disabled", () => mask.GetComponent<RectMask2D>().enabled = false),
                ("W created inactive at sibling 3", () => { var w = Img("W late", root, new Vector2(-330, 0), new Vector2(10, 10)); w.gameObject.SetActive(false); w.transform.SetSiblingIndex(3); }),
                ("W activated", () => watched.Last(x => x.Item1 == "W late").Item2.gameObject.SetActive(true)),
                ("W deactivated", () => watched.Last(x => x.Item1 == "W late").Item2.gameObject.SetActive(false)),
                ("W activated again", () => watched.Last(x => x.Item1 == "W late").Item2.gameObject.SetActive(true)),
                ("W image disabled", () => watched.Last(x => x.Item1 == "W late").Item2.GetComponent<Image>().enabled = false),
                ("image added to B2 group", () => { var g = root.Find("B2 group-without-renderer").gameObject; watched.Add(("B2 group image", g.AddComponent<Image>().canvasRenderer)); }),
                ("S2 reparented under S1", () => root.Find("S2 apart").SetParent(root.Find("S1 apart"), true)),
                ("F group alpha 0 to 1", () => g1.GetComponent<CanvasGroup>().alpha = 1),
                ("W deactivated, then an image appended", () => { watched.Last(x => x.Item1 == "W late").Item2.gameObject.SetActive(false); Img("X2 appended", root, new Vector2(330, 0), new Vector2(10, 10)); }),
                ("nested canvas J disabled", () => nc.enabled = false),
                ("nested canvas J enabled", () => nc.enabled = true),
                ("canvas off-on", () => { canvas.enabled = false; canvas.enabled = true; }),
            };
            foreach (var (what, act) in steps)
            {
                Rec.Log("UIS depth step: " + what);
                act();
                yield return Watch(3, Dump, phases: false);
            }
            Destroy(root.gameObject);
            yield return null;
        }
    }
}
