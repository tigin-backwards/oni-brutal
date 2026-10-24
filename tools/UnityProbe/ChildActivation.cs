// Layout groups whose children are activated or deactivated ("PROBE ... CA ..."), 10 s into the
// main menu, on an overlay canvas of its own. With PROBE_SCENARIO=childactive only this probe runs.
//
// Found in the build menu's search: PlanScreen keeps every building button under one
// GridLayoutGroup and filters by SetActive on the buttons. A button's root has no Graphic and
// no layout element (only its children have Images and texts). The question is what re-lays out
// the grid when such children appear or disappear, and in which frame.
//
// CA <case> <step> <phase> ...  a GridLayoutGroup G (a subclass counting its layout calls and
//             messages; three columns, cells 80 x 100, spacing 4) holding nine children T0..T8.
//             Each child is a bare RectTransform (40 x 30) with a stretched Image child "BG",
//             unless the case says otherwise. G sits either at the canvas (a root group) or in
//             R (VerticalLayoutGroup + ContentSizeFitter) > S (VerticalLayoutGroup) > G, as the
//             build menu nests it. Children's messages are logged too (a UIBehaviour spy on T and
//             on G, and on S/R). After each step the phases follow for two frames.
// Phases: "now" right after the step, then per frame "U" (an Update), "co" (a coroutine's yield
// return null), "L" (LateUpdate), "R" (Canvas.willRenderCanvases, after the canvas rebuild) and
// "E" (end of frame).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class ChildActivation : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static Action<string> snap;
        internal static string phase = "-";
        static string caseTag = "";
        static void Phase(string p) { phase = p; snap?.Invoke(p); }
        Canvas canvas;

        internal static void L(string s) => Rec.Log("CA " + s);
        static string F(float v) { var s = Math.Round(v, 3).ToString("0.###", Inv); return s == "-0" ? "0" : s; }
        static string F(Vector2 v) => F(v.x) + "," + F(v.y);

        /// Counts the grid's layout calls and messages.
        public class SpyGrid : GridLayoutGroup
        {
            public int CH, CV, H, V;
            public override void CalculateLayoutInputHorizontal() { CH++; base.CalculateLayoutInputHorizontal(); }
            public override void CalculateLayoutInputVertical() { CV++; base.CalculateLayoutInputVertical(); }
            public override void SetLayoutHorizontal() { H++; base.SetLayoutHorizontal(); }
            public override void SetLayoutVertical() { V++; base.SetLayoutVertical(); }
            protected override void OnRectTransformDimensionsChange() { Msg(this, "OnRectTransformDimensionsChange"); base.OnRectTransformDimensionsChange(); }
            protected override void OnTransformChildrenChanged() { Msg(this, "OnTransformChildrenChanged"); base.OnTransformChildrenChanged(); }
            protected override void OnCanvasHierarchyChanged() { Msg(this, "OnCanvasHierarchyChanged"); base.OnCanvasHierarchyChanged(); }
            protected override void OnTransformParentChanged() { Msg(this, "OnTransformParentChanged"); base.OnTransformParentChanged(); }
            protected override void OnDidApplyAnimationProperties() { Msg(this, "OnDidApplyAnimationProperties"); base.OnDidApplyAnimationProperties(); }
            protected override void OnChildRectTransformDimensionsChange() { Msg(this, "OnChildRectTransformDimensionsChange"); base.OnChildRectTransformDimensionsChange(); }
        }

        /// Counts the vertical group's layout calls.
        public class SpyVLG : VerticalLayoutGroup
        {
            public int H, V;
            public override void SetLayoutHorizontal() { H++; base.SetLayoutHorizontal(); }
            public override void SetLayoutVertical() { V++; base.SetLayoutVertical(); }
            protected override void OnRectTransformDimensionsChange() { Msg(this, "OnRectTransformDimensionsChange"); base.OnRectTransformDimensionsChange(); }
            protected override void OnTransformChildrenChanged() { Msg(this, "OnTransformChildrenChanged"); base.OnTransformChildrenChanged(); }
            protected override void OnChildRectTransformDimensionsChange() { Msg(this, "OnChildRectTransformDimensionsChange"); base.OnChildRectTransformDimensionsChange(); }
        }

        /// Logs the messages a child (or any object) receives.
        public class Spy : UIBehaviour
        {
            protected override void OnEnable() { Msg(this, "OnEnable"); base.OnEnable(); }
            protected override void OnDisable() { Msg(this, "OnDisable"); base.OnDisable(); }
            protected override void OnRectTransformDimensionsChange() => Msg(this, "OnRectTransformDimensionsChange");
            protected override void OnTransformParentChanged() => Msg(this, "OnTransformParentChanged");
            protected override void OnCanvasHierarchyChanged() => Msg(this, "OnCanvasHierarchyChanged");
            void OnTransformChildrenChanged() => Msg(this, "OnTransformChildrenChanged");
            void OnChildRectTransformDimensionsChange() => Msg(this, "OnChildRectTransformDimensionsChange");
        }

        static bool logMessages;
        static void Msg(Component c, string m)
        {
            if (!logMessages) return;
            L(caseTag + " msg " + c.name + " " + m + " after " + phase + " rebuildingLayout=" + CanvasUpdateRegistry.IsRebuildingLayout());
        }

        void OnEnable()
        {
            Canvas.willRenderCanvases += WillRender;
            RectTransform.reapplyDrivenProperties += Reapply;
        }

        void OnDisable()
        {
            Canvas.willRenderCanvases -= WillRender;
            RectTransform.reapplyDrivenProperties -= Reapply;
        }

        static void Reapply(RectTransform rt)
        {
            if (logMessages) L(caseTag + " msg " + rt.name + " reapplyDrivenProperties after " + phase);
        }

        void WillRender() => Phase("R");
        void LateUpdate() => Phase("L");

        void Update()
        {
            Phase("U");
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
            StartCoroutine(EndOfFrames());
        }

        IEnumerator EndOfFrames()
        {
            var wait = new WaitForEndOfFrame();
            while (true) { yield return wait; Phase("E"); }
        }

        IEnumerator Watch(int frames, Action<string> log)
        {
            phase = "now";
            log("now");
            snap = log;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                phase = "co";
                log("co");
            }
            snap = null;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// The layout rebuild queue's roots, read from the registry's queues by reflection (each
        /// queue's elements' transforms), to see which root a step registered.
        static string Queues()
        {
            var reg = CanvasUpdateRegistry.instance;
            var parts = new List<string>();
            int n = 0;
            foreach (var f in reg.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (!(f.GetValue(reg) is IList<ICanvasElement> list)) continue;
                var names = new List<string>();
                for (int i = 0; i < list.Count; i++)
                {
                    var ce = list[i];
                    names.Add(ce == null || ce.transform == null ? "null" : ce.transform.name);
                }
                parts.Add("q" + n++ + "=[" + string.Join(";", names.Where(x => x.StartsWith("CA")).ToArray()) + "]");
            }
            return string.Join(" ", parts.ToArray());
        }

        IEnumerator Run()
        {
            var cgo = new GameObject("CaCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32004;
            L("screen " + Screen.width + "x" + Screen.height + " scale=" + F(canvas.scaleFactor));
            yield return null;
            yield return null;
            L("=== cases");
            yield return Cases();
            Destroy(cgo);
            L("=== done");
        }

        class Setup
        {
            public string Name;
            public RectTransform Top, G, S, R;
            public SpyGrid Grid;
            public SpyVLG Svlg, Rvlg;
            public RectTransform[] T = new RectTransform[9];
        }

        enum ChildKind { Bare, Image, LayoutElement, BareNoBG }

        Setup Build(string name, bool nested, ChildKind kind, Func<int, bool> activeAtStart)
        {
            var s = new Setup { Name = name };
            Transform parent = canvas.transform;
            if (nested)
            {
                s.R = NewRect("CA R " + name, canvas.transform);
                s.R.anchorMin = s.R.anchorMax = new Vector2(0f, 1f);
                s.R.pivot = new Vector2(0f, 1f);
                s.R.sizeDelta = new Vector2(260, 100);
                s.R.anchoredPosition = new Vector2(20, -20);
                s.Rvlg = s.R.gameObject.AddComponent<SpyVLG>();
                s.Rvlg.childControlWidth = true;
                s.Rvlg.childControlHeight = true;
                s.Rvlg.childForceExpandWidth = false;
                s.Rvlg.childForceExpandHeight = false;
                s.Rvlg.padding = new RectOffset(8, 8, 8, 8);
                var f = s.R.gameObject.AddComponent<ContentSizeFitter>();
                f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                s.S = NewRect("CA S", s.R);
                s.Svlg = s.S.gameObject.AddComponent<SpyVLG>();
                s.Svlg.childControlWidth = true;
                s.Svlg.childControlHeight = true;
                s.Svlg.childForceExpandWidth = false;
                s.Svlg.childForceExpandHeight = false;
                s.S.gameObject.AddComponent<Spy>();
                parent = s.S;
                s.Top = s.R;
            }
            s.G = NewRect("CA G", parent);
            if (!nested)
            {
                s.G.anchorMin = s.G.anchorMax = new Vector2(0f, 1f);
                s.G.pivot = new Vector2(0f, 1f);
                s.G.sizeDelta = new Vector2(248, 100);
                s.G.anchoredPosition = new Vector2(20, -20);
                s.Top = s.G;
            }
            s.Grid = s.G.gameObject.AddComponent<SpyGrid>();
            s.Grid.cellSize = new Vector2(80, 100);
            s.Grid.spacing = new Vector2(4, 4);
            s.Grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            s.Grid.constraintCount = 3;
            for (int i = 0; i < 9; i++)
            {
                var t = NewRect("CA T" + i, s.G);
                t.sizeDelta = new Vector2(40, 30);
                t.anchoredPosition = new Vector2(7, -7);
                if (kind == ChildKind.Image) t.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
                if (kind == ChildKind.LayoutElement) t.gameObject.AddComponent<LayoutElement>().preferredWidth = 50;
                if (kind != ChildKind.BareNoBG)
                {
                    var bg = NewRect("CA BG" + i, t);
                    bg.anchorMin = Vector2.zero;
                    bg.anchorMax = Vector2.one;
                    bg.sizeDelta = Vector2.zero;
                    bg.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
                }
                t.gameObject.AddComponent<Spy>();
                if (!activeAtStart(i)) t.gameObject.SetActive(false);
                s.T[i] = t;
            }
            return s;
        }

        static string Snap(Setup s)
        {
            var parts = new List<string>();
            for (int i = 0; i < 9; i++)
            {
                var t = s.T[i];
                parts.Add(i + (t.gameObject.activeSelf ? "+" : "-") + F(t.anchoredPosition) + "/" + F(t.sizeDelta));
            }
            return "G cH=" + s.Grid.CH + " cV=" + s.Grid.CV + " H=" + s.Grid.H + " V=" + s.Grid.V + " pref=" + F(s.Grid.preferredHeight) + " sd=" + F(s.G.sizeDelta)
                   + (s.Svlg != null ? " | S H=" + s.Svlg.H + " sd=" + F(s.S.sizeDelta) + " | R H=" + s.Rvlg.H + " sd=" + F(s.R.sizeDelta) : "")
                   + " | " + string.Join(" ", parts.ToArray()) + " | " + Queues();
        }

        IEnumerator Cases()
        {
            // name, nested, child kind, active at the start, steps (each followed through two frames)
            var cases = new List<(string name, bool nested, ChildKind kind, Func<int, bool> start, (string name, Action<Setup> act)[] steps)>();
            foreach (var nested in new[] { false, true })
                foreach (var kind in new[] { ChildKind.Bare, ChildKind.BareNoBG, ChildKind.Image, ChildKind.LayoutElement })
                {
                    string k = (nested ? "nested." : "root.") + kind;
                    // A never laid-out child appears.
                    cases.Add((k + ".activate", nested, kind, i => i < 5, new (string, Action<Setup>)[]
                    {
                        ("act5", s => s.T[5].gameObject.SetActive(true)),
                    }));
                    // A laid-out child disappears.
                    cases.Add((k + ".deactivate", nested, kind, i => i < 5, new (string, Action<Setup>)[]
                    {
                        ("deact0", s => s.T[0].gameObject.SetActive(false)),
                    }));
                    // The build menu's search: the shown children hide and the hidden ones show,
                    // in one frame, after the order was changed in the frame before.
                    cases.Add((k + ".swap", nested, kind, i => i < 5, new (string, Action<Setup>)[]
                    {
                        ("sort", s => { for (int i = 8; i >= 0; i--) s.T[i].SetAsFirstSibling(); }),
                        ("swap", s => { for (int i = 0; i < 9; i++) s.T[i].gameObject.SetActive(i >= 5); }),
                    }));
                    // The same with children that were laid out before they were hidden.
                    cases.Add((k + ".swapBack", nested, kind, i => true, new (string, Action<Setup>)[]
                    {
                        ("hide", s => { for (int i = 5; i < 9; i++) s.T[i].gameObject.SetActive(false); }),
                        ("swap", s => { for (int i = 0; i < 9; i++) s.T[i].gameObject.SetActive(i >= 5); }),
                    }));
                }
            // Bare children: the child's own components disabled and enabled (no activation).
            cases.Add(("root.Bare.bgOff", false, ChildKind.Bare, i => true, new (string, Action<Setup>)[]
            {
                ("bgInactive", s => s.T[0].GetChild(0).gameObject.SetActive(false)),
                ("bgActive", s => s.T[0].GetChild(0).gameObject.SetActive(true)),
            }));
            // A bare child moved and a bare child resized by hand, then a sibling appears.
            cases.Add(("root.Bare.movedThenActivate", false, ChildKind.Bare, i => i < 5, new (string, Action<Setup>)[]
            {
                ("move0", s => s.T[0].anchoredPosition += new Vector2(0, -300)),
                ("act5", s => s.T[5].gameObject.SetActive(true)),
            }));
            // A bare child appears while the group is disabled (the component), then the group
            // is enabled.
            cases.Add(("root.Bare.groupDisabled", false, ChildKind.Bare, i => i < 5, new (string, Action<Setup>)[]
            {
                ("gridOff", s => s.Grid.enabled = false),
                ("act5", s => s.T[5].gameObject.SetActive(true)),
                ("gridOn", s => s.Grid.enabled = true),
            }));

            foreach (var c in cases)
            {
                logMessages = false;
                var s = Build(c.name, c.nested, c.kind, c.start);
                yield return null;
                yield return null;
                yield return null;
                caseTag = c.name;
                L(caseTag + " before " + Snap(s));
                logMessages = true;
                foreach (var st in c.steps)
                {
                    string stepTag = caseTag + " " + st.name;
                    L(stepTag + " step");
                    st.act(s);
                    yield return Watch(2, p => L(stepTag + " " + p + " " + Snap(s)));
                }
                logMessages = false;
                Destroy(s.Top.gameObject);
                yield return null;
            }
        }
    }
}
