// Layout groups and moved children, and RectMask2D culling ("PROBE ... LC ..."), 10 s into the
// main menu, on an overlay canvas of its own. With PROBE_SCENARIO=layoutcull only this probe runs.
//
// LC lay ...  a VerticalLayoutGroup (a subclass that counts its SetLayoutHorizontal/Vertical
//             calls) with three children A, B, C (Image + LayoutElement), with and without a
//             ContentSizeFitter, child size control on and off, B ignoring layout or not. One
//             change at a time (B's anchoredPosition, localPosition, sizeDelta, scale, pivot or
//             anchors, the group's position or size, a drag that sets B's position each frame),
//             then a re-layout from elsewhere (A's preferred height), each followed through the
//             frame's phases. Then nested groups: a grandchild's layout element or text changes.
// LC cull ... RectMask2D's cull of Images and TextMeshProUGUI texts at every offset across the
//             mask's bottom (and top) edge, by kind (alignment, pivot, text, rect height), at
//             canvas scale 1 and 1.032, with mask padding and softness; then when the cull is
//             re-evaluated (the graphic moves, its parent moves, the mask moves, the text or
//             its alignment changes, a text is created in place).
// Phases: "now" right after the step, then per frame "U" (an Update), "co" (a coroutine's yield
// return null), "L" (LateUpdate), "R" (Canvas.willRenderCanvases, after the canvas rebuild) and
// "E" (end of frame).
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
    public class LayoutCull : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static Action<string> snap;
        internal static string phase = "-";
        static void Phase(string p) { phase = p; snap?.Invoke(p); }
        Canvas canvas;
        TMP_FontAsset font;

        internal static void L(string s) => Rec.Log("LC " + s);
        static string F(float v) { var s = Math.Round(v, 3).ToString("0.###", Inv); return s == "-0" ? "0" : s; }
        static string F(Vector2 v) => F(v.x) + "," + F(v.y);
        static string F(Vector3 v) => F(v.x) + "," + F(v.y);
        static string F(Rect r) => F(r.xMin) + "," + F(r.yMin) + ".." + F(r.xMax) + "," + F(r.yMax);

        /// Counts the group's layout calls.
        public class SpyVLG : VerticalLayoutGroup
        {
            public int H, V, Dims, Children;
            public override void SetLayoutHorizontal() { H++; base.SetLayoutHorizontal(); }
            public override void SetLayoutVertical() { V++; base.SetLayoutVertical(); }
            protected override void OnRectTransformDimensionsChange() { Dims++; base.OnRectTransformDimensionsChange(); }
            protected override void OnTransformChildrenChanged() { Children++; base.OnTransformChildrenChanged(); }
        }

        /// Counts OnRectTransformDimensionsChange on its object; with Verbose, logs each with the
        /// phase it came after ("L" with rebuilding=True: during the canvas update's layout).
        public class Spy : UIBehaviour
        {
            public int Dims;
            public bool Verbose;
            protected override void OnRectTransformDimensionsChange()
            {
                Dims++;
                if (Verbose) L("dims " + name + " after " + phase + " rebuildingLayout=" + CanvasUpdateRegistry.IsRebuildingLayout() + " graphics=" + CanvasUpdateRegistry.IsRebuildingGraphics());
            }
        }

        void OnEnable() => Canvas.willRenderCanvases += WillRender;
        void OnDisable() => Canvas.willRenderCanvases -= WillRender;
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

        IEnumerator Run()
        {
            var cgo = new GameObject("LcCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32003;
            font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == "NotoSans-Regular");
            L("font " + (font == null ? "null" : font.name) + " screen " + Screen.width + "x" + Screen.height + " scale=" + F(canvas.scaleFactor));
            yield return null;
            yield return null;
            L("=== lay");
            yield return Layouts();
            L("=== cull");
            yield return Culls();
            Destroy(cgo);
            L("=== done");
        }

        // ---------------------------------------------------------------- layout

        class Lay
        {
            public string Name;
            public RectTransform G, A, B, C;
            public LayoutElement LeA, LeB;
            public SpyVLG Vlg;
            public Spy SpyB;
            public Image ImgB;
        }

        Lay BuildLay(string name, bool csf, bool cw, bool ch, bool ignoreB, bool bCsf = false)
        {
            var lay = new Lay { Name = name };
            lay.G = NewRect("G " + name, canvas.transform);
            lay.G.anchorMin = lay.G.anchorMax = new Vector2(0.5f, 0.5f);
            lay.G.pivot = new Vector2(0.5f, 0.5f);
            lay.G.sizeDelta = new Vector2(300, 400);
            lay.G.anchoredPosition = Vector2.zero;
            lay.Vlg = lay.G.gameObject.AddComponent<SpyVLG>();
            lay.Vlg.spacing = 10;
            lay.Vlg.childAlignment = TextAnchor.UpperLeft;
            lay.Vlg.childControlWidth = cw;
            lay.Vlg.childControlHeight = ch;
            lay.Vlg.childForceExpandWidth = false;
            lay.Vlg.childForceExpandHeight = false;
            if (csf)
            {
                var f = lay.G.gameObject.AddComponent<ContentSizeFitter>();
                f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
            RectTransform Child(string n, out LayoutElement le, bool withLe)
            {
                var rt = NewRect(n, lay.G);
                rt.sizeDelta = new Vector2(80, 40);
                var img = rt.gameObject.AddComponent<Image>();
                img.color = new Color(1, 1, 1, 0.1f);
                if (n == "B") lay.ImgB = img;
                le = null;
                if (withLe)
                {
                    le = rt.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = 120;
                    le.preferredHeight = 50;
                }
                return rt;
            }
            lay.A = Child("A", out lay.LeA, true);
            lay.B = Child("B", out lay.LeB, true);
            lay.C = Child("C", out _, true);
            if (ignoreB) lay.LeB.ignoreLayout = true;
            lay.SpyB = lay.B.gameObject.AddComponent<Spy>();
            lay.SpyB.Verbose = true;
            if (bCsf)
            {
                var bf = lay.B.gameObject.AddComponent<ContentSizeFitter>();
                bf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return lay;
        }

        static string Snap(Lay l)
            => "B ap=" + F(l.B.anchoredPosition) + " sd=" + F(l.B.sizeDelta) + " lp=" + F(l.B.localPosition) + " am=" + F(l.B.anchorMin)
               + " | A ap=" + F(l.A.anchoredPosition) + " C ap=" + F(l.C.anchoredPosition)
               + " | G ap=" + F(l.G.anchoredPosition) + " sd=" + F(l.G.sizeDelta)
               + " | H=" + l.Vlg.H + " V=" + l.Vlg.V + " Gdims=" + l.Vlg.Dims + " Gchildren=" + l.Vlg.Children + " Bdims=" + l.SpyB.Dims;

        IEnumerator Layouts()
        {
            var configs = new (string name, bool csf, bool cw, bool ch, bool ignoreB, bool bCsf)[]
            {
                ("csf.cwh", true, true, true, false, false),
                ("nocsf.cwh", false, true, true, false, false),
                ("csf.none", true, false, false, false, false),
                ("nocsf.none", false, false, false, false, false),
                ("csf.cw", true, true, false, false, false),
                ("csf.ch", true, false, true, false, false),
                ("csf.cwh.ignB", true, true, true, true, false),
                ("nocsf.none.ignB", false, false, false, true, false),
                ("csf.none.Bcsf", true, false, false, false, true),
                ("csf.cwh.Bcsf", true, true, true, false, true),
            };
            var actions = new (string name, Action<Lay> act)[]
            {
                ("apBx", l => l.B.anchoredPosition += new Vector2(-200, 0)),
                ("apBy", l => l.B.anchoredPosition += new Vector2(0, -30)),
                ("lpB", l => l.B.localPosition += new Vector3(-200, 0, 0)),
                ("posB", l => l.B.position += new Vector3(-200, 0, 0)),
                ("sdBw", l => l.B.sizeDelta += new Vector2(30, 0)),
                ("sdBh", l => l.B.sizeDelta += new Vector2(0, 30)),
                ("scaleB", l => l.B.localScale = new Vector3(1.5f, 1.5f, 1f)),
                ("pivotB", l => l.B.pivot = new Vector2(0f, 0f)),
                ("anchorB", l => { l.B.anchorMin = new Vector2(0.5f, 0.5f); l.B.anchorMax = new Vector2(0.5f, 0.5f); }),
                ("apGx", l => l.G.anchoredPosition += new Vector2(-200, 0)),
                ("sdGw", l => l.G.sizeDelta += new Vector2(30, 0)),
                ("sdGh", l => l.G.sizeDelta += new Vector2(0, 30)),
            };
            foreach (var cfg in configs)
            {
                foreach (var a in actions)
                {
                    var l = BuildLay(cfg.name, cfg.csf, cfg.cw, cfg.ch, cfg.ignoreB, cfg.bCsf);
                    yield return null;
                    yield return null;
                    string tag = "lay " + cfg.name + " " + a.name;
                    L(tag + " before " + Snap(l));
                    a.act(l);
                    yield return Watch(2, p => L(tag + " " + p + " " + Snap(l)));
                    // A re-layout from elsewhere: the sibling A's preferred height changes.
                    l.LeA.preferredHeight += 1;
                    yield return Watch(1, p => L(tag + " pokeA " + p + " " + Snap(l)));
                    Destroy(l.G.gameObject);
                    yield return null;
                }
                // A drag, as DraggableUI does it: B's anchoredPosition set every frame, then left.
                {
                    var l = BuildLay(cfg.name, cfg.csf, cfg.cw, cfg.ch, cfg.ignoreB, cfg.bCsf);
                    yield return null;
                    yield return null;
                    string tag = "lay " + cfg.name + " drag";
                    var x0 = l.B.anchoredPosition;
                    L(tag + " before " + Snap(l));
                    for (int k = 1; k <= 3; k++)
                    {
                        l.B.anchoredPosition = x0 + new Vector2(-100 * k, 0);
                        yield return Watch(1, p => L(tag + " " + k + " " + p + " " + Snap(l)));
                    }
                    yield return Watch(4, p => L(tag + " left " + p + " " + Snap(l)));
                    Destroy(l.G.gameObject);
                    yield return null;
                }
            }

            // Other ways to re-lay out after B moved (csf.cwh).
            var pokes = new (string name, Action<Lay> act)[]
            {
                ("markB", l => LayoutRebuilder.MarkLayoutForRebuild(l.B)),
                ("markG", l => LayoutRebuilder.MarkLayoutForRebuild(l.G)),
                ("imgLayoutDirtyB", l => l.ImgB.SetLayoutDirty()),
                ("imgVertsDirtyB", l => l.ImgB.SetVerticesDirty()),
                ("imgSpriteB", l => l.ImgB.color = Color.red),
                ("leBsame", l => l.LeB.preferredHeight = l.LeB.preferredHeight),
                ("leBchange", l => l.LeB.preferredHeight += 1),
                ("forceG", l => LayoutRebuilder.ForceRebuildLayoutImmediate(l.G)),
                ("forceCanvases", l => Canvas.ForceUpdateCanvases()),
                ("vlgSpacing", l => l.Vlg.spacing += 1),
                ("vlgSame", l => l.Vlg.spacing = l.Vlg.spacing),
                ("Bdisable", l => l.ImgB.enabled = false),
                ("CsetActive", l => l.C.gameObject.SetActive(false)),
                ("Dadd", l => NewRect("D", l.G)),
                ("BsetSibling", l => l.B.SetAsLastSibling()),
                ("Genable", l => { l.Vlg.enabled = false; l.Vlg.enabled = true; }),
                ("none", l => { }),
            };
            foreach (var pk in pokes)
            {
                var l = BuildLay("csf.cwh", true, true, true, false);
                yield return null;
                yield return null;
                string tag = "lay poke " + pk.name;
                L(tag + " before " + Snap(l));
                l.B.anchoredPosition += new Vector2(-200, 0);
                yield return Watch(1, p => L(tag + " moved " + p + " " + Snap(l)));
                pk.act(l);
                yield return Watch(2, p => L(tag + " " + p + " " + Snap(l)));
                Destroy(l.G.gameObject);
                yield return null;
            }

            yield return InUpdate();
            yield return Nested();
        }

        /// A rect (no layout around it) moved or resized from Canvas.willRenderCanvases, after
        /// the canvas update's own work, or from a coroutine: which OnRectTransformDimensionsChange
        /// it gets, and when.
        IEnumerator InUpdate()
        {
            var steps = new (string name, Action<RectTransform> act)[]
            {
                ("move", r => r.anchoredPosition += new Vector2(5, 0)),
                ("resize", r => r.sizeDelta += new Vector2(5, 0)),
                ("localPos", r => r.localPosition += new Vector3(5, 0, 0)),
                ("pivot", r => r.pivot = new Vector2(0.25f, 0.5f)),
                ("scale", r => r.localScale = new Vector3(2, 2, 1)),
            };
            foreach (var st in steps)
                foreach (bool inR in new[] { true, false })
                {
                    var r = NewRect("Solo", canvas.transform);
                    r.sizeDelta = new Vector2(50, 50);
                    var spy = r.gameObject.AddComponent<Spy>();
                    spy.Verbose = true;
                    yield return null;
                    yield return null;
                    string tag = "lay solo " + st.name + (inR ? " inR" : " inCo");
                    L(tag + " before ap=" + F(r.anchoredPosition) + " sd=" + F(r.sizeDelta));
                    if (inR)
                    {
                        bool done = false;
                        Canvas.WillRenderCanvases h = () => { if (done) return; done = true; L(tag + " acting in R"); st.act(r); };
                        Canvas.willRenderCanvases += h;
                        yield return Watch(2, p => L(tag + " " + p + " dims=" + spy.Dims));
                        Canvas.willRenderCanvases -= h;
                    }
                    else
                    {
                        st.act(r);
                        yield return Watch(2, p => L(tag + " " + p + " dims=" + spy.Dims));
                    }
                    Destroy(r.gameObject);
                    yield return null;
                }
        }

        /// G (VLG + CSF) > D (a VLG, or none) > T1 (Image + LayoutElement), T2 (TMP text).
        IEnumerator Nested()
        {
            foreach (bool dGroup in new[] { true, false })
            {
                var steps = new (string name, Action<LayoutElement, TextMeshProUGUI> act)[]
                {
                    ("leT1", (le, t) => le.preferredHeight += 1),
                    ("textT2", (le, t) => t.text = "Changed, longer text"),
                    ("sameTextT2", (le, t) => t.text = t.text),
                    ("colorT2", (le, t) => t.color = Color.red),
                    ("none", (le, t) => { }),
                };
                foreach (var st in steps)
                {
                    var g = NewRect("NG", canvas.transform);
                    g.sizeDelta = new Vector2(300, 400);
                    var vlg = g.gameObject.AddComponent<SpyVLG>();
                    vlg.childControlWidth = vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;
                    var csf = g.gameObject.AddComponent<ContentSizeFitter>();
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    var top = NewRect("Top", g);
                    top.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
                    var d = NewRect("D", g);
                    if (dGroup)
                    {
                        var dv = d.gameObject.AddComponent<VerticalLayoutGroup>();
                        dv.childControlWidth = dv.childControlHeight = true;
                        dv.childForceExpandWidth = dv.childForceExpandHeight = false;
                    }
                    else
                    {
                        var dle = d.gameObject.AddComponent<LayoutElement>();
                        dle.preferredWidth = 200; dle.preferredHeight = 100;
                    }
                    var t1 = NewRect("T1", d);
                    var le1 = t1.gameObject.AddComponent<LayoutElement>();
                    le1.preferredWidth = 150; le1.preferredHeight = 40;
                    var t2r = NewRect("T2", d);
                    var t2 = t2r.gameObject.AddComponent<TextMeshProUGUI>();
                    if (font != null) t2.font = font;
                    t2.fontSize = 14;
                    t2.text = "Some text";
                    yield return null;
                    yield return null;
                    string tag = "lay nested " + (dGroup ? "dvlg" : "dle") + " " + st.name;
                    Func<string> snapN = () => "D ap=" + F(d.anchoredPosition) + " sd=" + F(d.sizeDelta) + " | T1 ap=" + F(t1.anchoredPosition) + " | H=" + vlg.H + " V=" + vlg.V;
                    L(tag + " before " + snapN());
                    d.anchoredPosition += new Vector2(-200, 0);
                    yield return Watch(1, p => L(tag + " moved " + p + " " + snapN()));
                    st.act(le1, t2);
                    yield return Watch(2, p => L(tag + " " + p + " " + snapN()));
                    Destroy(g.gameObject);
                    yield return null;
                }
            }
        }

        // ---------------------------------------------------------------- cull

        class Kind
        {
            public string Name;
            public bool Text;
            public string Content = "Care Packages";
            public TextAlignmentOptions Align = TextAlignmentOptions.Left;
            public Vector2 Pivot = new Vector2(0f, 0.5f);
            public float Height = 30f;
        }

        static readonly Kind[] Kinds =
        {
            new Kind { Name = "img" },
            new Kind { Name = "img.p05", Pivot = new Vector2(0.5f, 0.5f) },
            new Kind { Name = "img.p1", Pivot = new Vector2(0.5f, 1f) },
            new Kind { Name = "txt", Text = true },
            new Kind { Name = "txt.top", Text = true, Align = TextAlignmentOptions.TopLeft },
            new Kind { Name = "txt.bot", Text = true, Align = TextAlignmentOptions.BottomLeft },
            new Kind { Name = "txt.c", Text = true, Align = TextAlignmentOptions.Center, Pivot = new Vector2(0.5f, 0.5f) },
            new Kind { Name = "txt.p1", Text = true, Pivot = new Vector2(0.5f, 1f) },
            new Kind { Name = "txt.p0", Text = true, Pivot = new Vector2(0.5f, 0f) },
            new Kind { Name = "txt.empty", Text = true, Content = "" },
            new Kind { Name = "txt.space", Text = true, Content = " " },
            new Kind { Name = "txt.AAA", Text = true, Content = "AAA" },
            new Kind { Name = "txt.h100", Text = true, Height = 100f },
            new Kind { Name = "txt.h100.top", Text = true, Height = 100f, Align = TextAlignmentOptions.TopLeft },
        };

        static List<float> Offsets()
        {
            var ds = new List<float>();
            for (int i = -140; i <= 140; i++) ds.Add(i * 0.5f);
            ds.AddRange(new[] { -0.25f, -0.1f, -0.01f, 0.01f, 0.1f, 0.25f });
            ds.Sort();
            return ds;
        }

        Graphic Make(Kind k, Transform parent, string name)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = k.Pivot;
            rt.sizeDelta = new Vector2(200, k.Height);
            if (!k.Text)
            {
                var img = rt.gameObject.AddComponent<Image>();
                img.color = new Color(1, 1, 1, 0.05f);
                return img;
            }
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableKerning = true;
            t.extraPadding = true;
            if (font != null) t.font = font;
            t.fontSize = 14;
            t.alignment = k.Align;
            t.text = k.Content;
            return t;
        }

        /// Puts the rect's centre at y = edge + d (x: the rect from -100 to 100), whatever its pivot.
        static void Place(RectTransform rt, float edge, float d)
        {
            var h = rt.sizeDelta.y;
            rt.anchoredPosition = new Vector2(-100 + rt.pivot.x * 200, edge + d + (rt.pivot.y - 0.5f) * h);
        }

        /// The graphic's rect, mesh bounds and text bounds in the root canvas's space.
        string Geometry(Graphic g)
        {
            var rt = g.rectTransform;
            var toRoot = canvas.transform.worldToLocalMatrix * rt.localToWorldMatrix;
            var c = new Vector3[4];
            rt.GetLocalCorners(c);
            Rect R(Vector3 min, Vector3 max)
            {
                var a = toRoot.MultiplyPoint(min); var b = toRoot.MultiplyPoint(max);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
            var sb = new StringBuilder("rect=" + F(R(c[0], c[2])));
            if (g is TMP_Text t)
            {
                var mb = t.mesh != null ? t.mesh.bounds : new Bounds();
                sb.Append(" meshLocal=" + F(new Rect(mb.min.x, mb.min.y, mb.size.x, mb.size.y)) + " mesh=" + F(R(mb.min, mb.max)) + " verts=" + (t.mesh != null ? t.mesh.vertexCount : -1));
                var tb = t.textBounds;
                sb.Append(" textBounds=" + F(R(tb.min, tb.max)));
                var b = t.bounds;
                sb.Append(" bounds=" + F(R(b.min, b.max)));
            }
            return sb.ToString();
        }

        /// Ranges of the offsets whose graphic is culled.
        static string Ranges(List<float> ds, List<bool> culled)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < ds.Count)
            {
                if (!culled[i]) { i++; continue; }
                int j = i;
                while (j + 1 < ds.Count && culled[j + 1]) j++;
                sb.Append(' ').Append(F(ds[i])).Append("..").Append(F(ds[j]));
                i = j + 1;
            }
            return sb.Length == 0 ? " none" : sb.ToString();
        }

        RectTransform NewMask(string name, Vector4 padding, Vector2Int softness)
        {
            var m = NewRect(name, canvas.transform);
            m.anchorMin = m.anchorMax = m.pivot = new Vector2(0.5f, 0.5f);
            m.sizeDelta = new Vector2(600, 200);
            var rm = m.gameObject.AddComponent<RectMask2D>();
            rm.padding = padding;
            rm.softness = softness;
            return m;
        }

        IEnumerator Grid(string tag, Vector4 padding, Vector2Int softness, Kind[] kinds, bool topEdge)
        {
            var ds = Offsets();
            var mask = NewMask("Mask " + tag, padding, softness);
            float edge = topEdge ? 100f : -100f;
            var made = new List<(Kind k, List<Graphic> gs)>();
            foreach (var k in kinds)
            {
                var gs = new List<Graphic>();
                foreach (var d in ds)
                {
                    var g = Make(k, mask, k.Name + " " + F(d));
                    Place(g.rectTransform, edge, d);
                    gs.Add(g);
                }
                made.Add((k, gs));
            }
            yield return null;
            yield return null;
            yield return null;
            var rm = mask.GetComponent<RectMask2D>();
            L(tag + " mask canvasRect=" + F(rm.canvasRect) + " padding=" + rm.padding + " softness=" + rm.softness + " edge=" + F(edge));
            foreach (var (k, gs) in made)
            {
                var culled = gs.Select(g => g.canvasRenderer.cull).ToList();
                L(tag + " " + k.Name + " culled at" + Ranges(ds, culled));
                int i0 = ds.IndexOf(0f);
                L(tag + " " + k.Name + " d=0 " + Geometry(gs[i0]));
            }
            Destroy(mask.gameObject);
            yield return null;
        }

        IEnumerator Culls()
        {
            yield return Grid("cull s1 bottom", Vector4.zero, Vector2Int.zero, Kinds, false);
            yield return Grid("cull s1 top", Vector4.zero, Vector2Int.zero, Kinds, true);
            canvas.scaleFactor = 1.032f;
            yield return null;
            L("cull scale=" + F(canvas.scaleFactor));
            yield return Grid("cull s1.032 bottom", Vector4.zero, Vector2Int.zero, Kinds, false);
            canvas.scaleFactor = 1f;
            yield return null;
            var few = Kinds.Where(k => k.Name == "img" || k.Name == "txt" || k.Name == "txt.h100").ToArray();
            yield return Grid("cull pad10 bottom", new Vector4(0, 10, 0, 0), Vector2Int.zero, few, false);
            yield return Grid("cull pad10top bottom", new Vector4(0, 0, 0, 10), Vector2Int.zero, few, false);
            yield return Grid("cull soft20 bottom", Vector4.zero, new Vector2Int(20, 20), few, false);
            yield return Timing();
        }

        // ---------------------------------------------------------------- cull timing

        IEnumerator Timing()
        {
            var img = new Kind { Name = "img" };
            var txt = new Kind { Name = "txt", Text = true };
            var txt100 = new Kind { Name = "txt.h100", Text = true, Height = 100f };

            // Each case: a mask, a container, the subject; the step; three frames watched.
            var cases = new (string name, Kind kind, float d, Action<RectTransform, RectTransform, Graphic> step)[]
            {
                ("img out", img, 0f, (m, c, g) => g.rectTransform.anchoredPosition += new Vector2(0, -300)),
                ("img in", img, -300f, (m, c, g) => g.rectTransform.anchoredPosition += new Vector2(0, 300)),
                ("img parent out", img, 0f, (m, c, g) => c.anchoredPosition += new Vector2(0, -300)),
                ("img parent in", img, -300f, (m, c, g) => c.anchoredPosition += new Vector2(0, 300)),
                ("img mask away", img, 0f, (m, c, g) => { var r = m.GetComponent<RectMask2D>(); m.anchoredPosition += new Vector2(0, 300); c.anchoredPosition -= new Vector2(0, 300); }),
                ("img size in", img, -40f, (m, c, g) => g.rectTransform.sizeDelta += new Vector2(0, 100)),
                ("img size out", img, -14f, (m, c, g) => g.rectTransform.sizeDelta -= new Vector2(0, 20)),
                ("img localPos out", img, 0f, (m, c, g) => g.rectTransform.localPosition += new Vector3(0, -300, 0)),
                ("img scale out", img, -10f, (m, c, g) => g.rectTransform.localScale = new Vector3(1, 0.1f, 1)),
                ("txt100 text grow", txt100, -30f, (m, c, g) => ((TMP_Text)g).text = "Care Packages\nsecond\nthird\nfourth"),
                ("txt100 text shrink", txt100, -45f, (m, c, g) => ((TMP_Text)g).text = ""),
                ("txt100 align top", txt100, -30f, (m, c, g) => ((TMP_Text)g).alignment = TextAlignmentOptions.TopLeft),
                ("txt100 align bottom", txt100, 30f, (m, c, g) => ((TMP_Text)g).alignment = TextAlignmentOptions.BottomLeft),
                ("txt100 fontsize", txt100, -30f, (m, c, g) => ((TMP_Text)g).fontSize = 60),
                ("txt out", txt, 0f, (m, c, g) => g.rectTransform.anchoredPosition += new Vector2(0, -300)),
                ("txt100 nothing", txt100, -30f, (m, c, g) => { }),
            };
            foreach (var cs in cases)
            {
                var mask = NewMask("TMask", Vector4.zero, Vector2Int.zero);
                var cont = NewRect("Cont", mask);
                cont.anchorMin = cont.anchorMax = new Vector2(0.5f, 0.5f);
                cont.sizeDelta = Vector2.zero;
                var g = Make(cs.kind, cont, "Subject");
                Place(g.rectTransform, -100f, cs.d);
                yield return null;
                yield return null;
                string tag = "cull time " + cs.name;
                L(tag + " before cull=" + g.canvasRenderer.cull + " " + Geometry(g));
                cs.step(mask, cont, g);
                yield return Watch(3, p => L(tag + " " + p + " cull=" + g.canvasRenderer.cull + (p == "E" || p == "now" ? " " + Geometry(g) : "")));
                Destroy(mask.gameObject);
                yield return null;
            }

            // Created in place: a text and an image whose rect straddles the bottom edge, the
            // pivot outside; then made under a mask through a parent change.
            foreach (var k in new[] { img, txt, txt100 })
                foreach (float d in new[] { -0.5f, -14f, -30f, 10f })
                {
                    var mask = NewMask("CMask", Vector4.zero, Vector2Int.zero);
                    var g = Make(k, mask, "Fresh");
                    Place(g.rectTransform, -100f, d);
                    string tag = "cull fresh " + k.Name + " d=" + F(d);
                    yield return Watch(3, p => L(tag + " " + p + " cull=" + g.canvasRenderer.cull + (p == "E" || p == "now" ? " " + Geometry(g) : "")));
                    Destroy(mask.gameObject);
                    yield return null;
                }

            // The Modify Difficulty Settings label: a toggle row (HorizontalLayoutGroup) whose
            // Label (LayoutElement, TMP) is placed by a VerticalLayoutGroup in a scroll content,
            // canvas scale 1.032; the row's position puts the label's centre 0.5 below the mask.
            canvas.scaleFactor = 1.032f;
            yield return null;
            foreach (float d in new[] { -0.5f, 0.5f, -5f, -10f })
            {
                var mask = NewMask("RMask", Vector4.zero, Vector2Int.zero);
                var content = NewRect("Content", mask);
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = new Vector2(368, 0);
                var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childControlWidth = vlg.childControlHeight = true;
                vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;
                var fit = content.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var row = NewRect("TogglePrefab", content);
                var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth = hlg.childControlHeight = false;
                hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.padding = new RectOffset(10, 0, 0, 0);
                var rle = row.gameObject.AddComponent<LayoutElement>();
                rle.preferredHeight = 36;
                var label = NewRect("Label", row);
                label.pivot = new Vector2(0f, 0.5f);
                label.sizeDelta = new Vector2(200, 30);
                var lle = label.gameObject.AddComponent<LayoutElement>();
                lle.minWidth = 120; lle.preferredWidth = 200;
                var t = label.gameObject.AddComponent<TextMeshProUGUI>();
                if (font != null) t.font = font;
                t.fontSize = 14;
                t.alignment = TextAlignmentOptions.Left;
                t.enableKerning = true;
                t.extraPadding = true;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.text = "Care Packages";
                // The row's centre (and the label's) at the mask's bottom + d: content top at
                // -100 + d + 18.
                content.anchoredPosition = new Vector2(0, -100 + d + 18);
                string tag = "cull row d=" + F(d);
                yield return Watch(3, p => L(tag + " " + p + " cull=" + t.canvasRenderer.cull + (p == "E" || p == "now" ? " " + Geometry(t) : "")));
                Destroy(mask.gameObject);
                yield return null;
            }
            canvas.scaleFactor = 1f;
            yield return null;
        }
    }
}
