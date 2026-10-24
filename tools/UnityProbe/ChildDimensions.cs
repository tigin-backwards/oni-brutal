// Dimension messages of the children of a changed RectTransform ("PROBE ... CD ..."), 10 s into
// the main menu, on an overlay canvas of its own. With PROBE_SCENARIO=childdims only this probe
// runs.
//
// Found in the main menu's news tiles (MOTD): each tile's ImageButton has a LayoutElement whose
// preferred width follows its height on OnRectTransformDimensionsChange. The row lays out with
// the tiles' widths at 0 first; in the original the row is laid out again over the next frames
// until the tiles sit side by side. The question is which message the original sends after the
// canvas update, to which rect, for which change.
//
// CD <case> <step> msg <name> <message> after <phase> rebuildingLayout=<bool>
//             one line per message a spy receives. The hierarchy: P (200 x 100, centred) holding
//             C (50 x 50, centre anchors) holding GC (20 x 20), and S (stretched over P). Each of
//             P, C, GC and S has a spy. One change to P per step: its anchoredPosition, sizeDelta,
//             localScale, localPosition, pivot or localRotation, made in a coroutine (outside the
//             canvas update), in Canvas.willRenderCanvases (after the layout and graphic
//             rebuilds) or during the layout rebuild (an ICanvasElement registered for it).
//             In the group cases P is the second child of a HorizontalLayoutGroup G, and either P
//             is resized (its preferred width) or moved (its sibling's preferred width).
// CD motd ... a copy of the news row: G (HorizontalLayoutGroup, right aligned) holding three
//             inactive tiles T (VerticalLayoutGroup) with a header (LayoutElement, height 18) and
//             a button (a LayoutElement whose preferred width is 1.61 x its height, set on each
//             OnRectTransformDimensionsChange). The tiles are activated, then each frame logs G's
//             preferred width and the tiles' positions.
// Each set runs on the probe's canvas at scale 1, at scale 1 pixel perfect and at 0.86 pixel
// perfect (case tags .s1, .s1pp, .s086pp). PROBE_CD_PART=extra runs only the pixel-perfect
// details (Extra): which objects are told, repeated and undone moves, values set to themselves,
// new, cloned and reparented objects, the end of the frame, nested canvases.
// Phases: "now" right after the step, then per frame "U" (an Update), "co" (a coroutine's yield
// return null), "L" (LateUpdate), "R" (Canvas.willRenderCanvases, after the canvas rebuild) and
// "E" (end of frame).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class ChildDimensions : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static string phase = "-";
        static string caseTag = "";
        static string cfgTag = "";
        static bool graphics;
        static bool logMessages;
        Canvas canvas;
        static System.Action inRender;

        internal static void L(string s) => Rec.Log("CD " + s);
        static string F(float v) { var s = Math.Round(v, 3).ToString("0.###", Inv); return s == "-0" ? "0" : s; }
        static string F(Vector2 v) => F(v.x) + "," + F(v.y);

        static void Msg(Component c, string m)
        {
            if (logMessages) L(caseTag + " msg " + c.name + " " + m + " after " + phase + " rebuildingLayout=" + CanvasUpdateRegistry.IsRebuildingLayout());
        }

        public class Spy : UIBehaviour
        {
            protected override void OnRectTransformDimensionsChange() => Msg(this, "OnRectTransformDimensionsChange");
            void OnChildRectTransformDimensionsChange() => Msg(this, "OnChildRectTransformDimensionsChange");
            protected override void OnTransformParentChanged() => Msg(this, "OnTransformParentChanged");
        }

        /// A LayoutElement whose preferred width follows its height, as the news tiles' button.
        public class RatioElement : LayoutElement
        {
            public float Ratio = 1.61f;
            protected override void OnRectTransformDimensionsChange()
            {
                flexibleHeight = 1f;
                preferredHeight = -1f;
                minHeight = -1f;
                flexibleWidth = 0f;
                preferredWidth = ((RectTransform)transform).sizeDelta.y * Ratio;
                minWidth = preferredWidth;
                Msg(this, "OnRectTransformDimensionsChange size=" + F(((RectTransform)transform).rect.size) + " pw=" + F(preferredWidth));
                base.OnRectTransformDimensionsChange();
            }
        }

        /// Runs an action once, during the next layout rebuild.
        class LayoutHook : ICanvasElement
        {
            public Transform Target;
            public System.Action Act;
            public Transform transform => Target;
            public void Rebuild(CanvasUpdate executing)
            {
                if (executing != CanvasUpdate.Layout || Act == null) return;
                var a = Act; Act = null;
                L(caseTag + " hook runs rebuildingLayout=" + CanvasUpdateRegistry.IsRebuildingLayout());
                a();
            }
            public void LayoutComplete() { }
            public void GraphicUpdateComplete() { }
            public bool IsDestroyed() => Target == null;
        }

        void OnEnable() => Canvas.willRenderCanvases += WillRender;
        void OnDisable() => Canvas.willRenderCanvases -= WillRender;
        void WillRender()
        {
            phase = "R";
            if (inRender != null)
            {
                var a = inRender; inRender = null;
                L(caseTag + " render hook runs");
                a();
            }
        }
        void LateUpdate() => phase = "L";

        void Update()
        {
            phase = "U";
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
            while (true) { yield return wait; phase = "E"; }
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            return rt;
        }

        IEnumerator Run()
        {
            var cgo = new GameObject("CdCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32005;
            L("screen " + Screen.width + "x" + Screen.height + " scale=" + F(canvas.scaleFactor));
            yield return null;
            yield return null;
            if (System.Environment.GetEnvironmentVariable("PROBE_CD_PART") == "extra")
            {
                canvas.pixelPerfect = true;
                yield return null;
                L("=== extra");
                yield return Extra();
                Destroy(cgo);
                L("=== done");
                yield break;
            }
            foreach (var cfg in new[] { (".s1", 1f, false), (".s1pp", 1f, true), (".s086pp", 0.86f, true) })
            {
                canvas.scaleFactor = cfg.Item2;
                canvas.pixelPerfect = cfg.Item3;
                cfgTag = cfg.Item1;
                yield return null;
                yield return null;
                L("=== cases" + cfgTag);
                yield return Cases();
                graphics = true;
                yield return Cases();
                graphics = false;
                L("=== motd" + cfgTag);
                yield return Motd(false);
                yield return Motd(true);
                yield return Motd(true, mask: true, arf: true);
            }
            Destroy(cgo);
            L("=== done");
        }

        class Setup
        {
            public RectTransform Top, P, C, GC, S, A;
            public LayoutElement LeP, LeA;
        }

        Setup Build(bool group)
        {
            var s = new Setup();
            Transform parent = canvas.transform;
            if (group)
            {
                var g = NewRect("G", canvas.transform, new Vector2(500, 100));
                var hlg = g.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.spacing = 4;
                g.gameObject.AddComponent<Spy>();
                s.A = NewRect("A", g, new Vector2(50, 50));
                s.LeA = s.A.gameObject.AddComponent<LayoutElement>();
                s.LeA.preferredWidth = 100;
                s.LeA.preferredHeight = 100;
                parent = g;
                s.Top = g;
            }
            s.P = NewRect("P", parent, new Vector2(200, 100));
            s.P.gameObject.AddComponent<Spy>();
            if (group)
            {
                s.LeP = s.P.gameObject.AddComponent<LayoutElement>();
                s.LeP.preferredWidth = 200;
                s.LeP.preferredHeight = 100;
            }
            else s.Top = s.P;
            s.C = NewRect("C", s.P, new Vector2(50, 50));
            s.C.gameObject.AddComponent<Spy>();
            s.GC = NewRect("GC", s.C, new Vector2(20, 20));
            s.GC.gameObject.AddComponent<Spy>();
            s.S = NewRect("S", s.P, Vector2.zero);
            s.S.anchorMin = Vector2.zero;
            s.S.anchorMax = Vector2.one;
            s.S.gameObject.AddComponent<Spy>();
            if (graphics)
            {
                // P an Image, C a RawImage, GC a text; S stays bare.
                s.P.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
                s.C.gameObject.AddComponent<RawImage>().color = new Color(1, 1, 1, 0.1f);
                var text = s.GC.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
                foreach (var f in Resources.FindObjectsOfTypeAll<TMPro.TMP_FontAsset>()) if (f.name == "NotoSans-Regular") text.font = f;
                text.text = "Ab";
            }
            return s;
        }

        string Snap(Setup s) => "P pos=" + F(s.P.anchoredPosition) + " size=" + F(s.P.rect.size) + " C world=" + F((Vector2)s.C.position);

        IEnumerator Cases()
        {
            var changes = new (string name, System.Action<Setup> act)[]
            {
                ("anchoredPosition", s => s.P.anchoredPosition += new Vector2(10, 0)),
                ("sizeDelta", s => s.P.sizeDelta += new Vector2(10, 0)),
                ("localScale", s => s.P.localScale = new Vector3(1.1f, 1.1f, 1f)),
                ("localPosition", s => s.P.localPosition += new Vector3(0, 10, 0)),
                ("pivot", s => s.P.pivot = new Vector2(0.4f, 0.5f)),
                ("localRotation", s => s.P.localRotation = Quaternion.Euler(0, 0, 10)),
                ("sameValue", s => s.P.anchoredPosition = s.P.anchoredPosition),
                ("subpixelMove", s => s.P.anchoredPosition += new Vector2(0.3f, 0)),
                ("nothing", s => { }),
            };
            foreach (var when in new[] { "co", "render", "layout" })
                foreach (var ch in changes)
                {
                    logMessages = false;
                    var s = Build(false);
                    yield return null;
                    yield return null;
                    caseTag = "solo" + cfgTag + (graphics ? ".gfx." : ".") + when + "." + ch.name;
                    L(caseTag + " before " + Snap(s));
                    logMessages = true;
                    phase = "now";
                    if (when == "co") ch.act(s);
                    else if (when == "render") inRender = () => ch.act(s);
                    else CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(new LayoutHook { Target = s.P, Act = () => ch.act(s) });
                    for (int i = 0; i < 3; i++) { yield return null; phase = "co"; L(caseTag + " co " + Snap(s)); }
                    logMessages = false;
                    Destroy(s.Top.gameObject);
                    yield return null;
                }
            var groupChanges = new (string name, System.Action<Setup> act)[]
            {
                ("resizeP", s => s.LeP.preferredWidth = 220),
                ("moveP", s => s.LeA.preferredWidth = 130),
                ("resizeHeightP", s => s.LeP.preferredHeight = 80),
            };
            foreach (var ch in groupChanges)
            {
                logMessages = false;
                var s = Build(true);
                yield return null;
                yield return null;
                caseTag = "group" + cfgTag + (graphics ? ".gfx." : ".") + ch.name;
                L(caseTag + " before " + Snap(s));
                logMessages = true;
                phase = "now";
                ch.act(s);
                for (int i = 0; i < 3; i++) { yield return null; phase = "co"; L(caseTag + " co " + Snap(s)); }
                logMessages = false;
                Destroy(s.Top.gameObject);
                yield return null;
            }
        }

        /// On the pixel-perfect canvas: which objects qualify (a CanvasRenderer alone, a disabled
        /// Image, an Image with alpha 0, an inactive child), several moves in one frame, a move
        /// undone in the same frame, a drag (a move every frame), a change made at the end of the
        /// frame, and nested canvases that override pixelPerfect.
        IEnumerator Extra()
        {
            var kinds = new (string name, System.Action<RectTransform> add)[]
            {
                ("image", r => r.gameObject.AddComponent<Image>()),
                ("rendererOnly", r => r.gameObject.AddComponent<CanvasRenderer>()),
                ("imageDisabled", r => r.gameObject.AddComponent<Image>().enabled = false),
                ("imageAlpha0", r => r.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0)),
                ("imageCulled", r => { r.gameObject.AddComponent<Image>(); r.GetComponent<CanvasRenderer>().cull = true; }),
                ("layoutElement", r => r.gameObject.AddComponent<LayoutElement>()),
            };
            var steps = new (string name, System.Action<RectTransform> act, int frames)[]
            {
                ("move", r => r.anchoredPosition += new Vector2(10, 0), 1),
                ("moveTwice", r => { r.anchoredPosition += new Vector2(10, 0); r.anchoredPosition += new Vector2(10, 0); }, 1),
                ("moveBack", r => { r.anchoredPosition += new Vector2(10, 0); r.anchoredPosition -= new Vector2(10, 0); }, 1),
                ("drag", r => r.anchoredPosition += new Vector2(1, 0), 3),
                ("sameLocalPosition", r => r.localPosition = r.localPosition, 1),
                ("sameLocalScale", r => r.localScale = r.localScale, 1),
                ("sameLocalRotation", r => r.localRotation = r.localRotation, 1),
                ("samePosition", r => r.position = r.position, 1),
                ("sameParent", r => r.SetParent(r.parent, false), 1),
                ("sizeDeltaOnly", r => r.sizeDelta += new Vector2(10, 0), 1),
            };
            foreach (var k in kinds)
                foreach (var st in steps)
                {
                    logMessages = false;
                    var p = NewRect("P", canvas.transform, new Vector2(200, 100));
                    p.gameObject.AddComponent<Spy>();
                    k.add(p);
                    var c = NewRect("C", p, new Vector2(50, 50));
                    c.gameObject.AddComponent<Spy>();
                    k.add(c);
                    var off = NewRect("Off", p, new Vector2(50, 50));
                    off.gameObject.AddComponent<Spy>();
                    off.gameObject.AddComponent<Image>();
                    off.gameObject.SetActive(false);
                    yield return null;
                    yield return null;
                    caseTag = "extra." + k.name + "." + st.name;
                    logMessages = true;
                    phase = "now";
                    for (int i = 0; i < st.frames; i++)
                    {
                        st.act(p);
                        yield return null;
                        phase = "co";
                    }
                    yield return null;
                    yield return null;
                    L(caseTag + " end");
                    logMessages = false;
                    Destroy(p.gameObject);
                    yield return null;
                }
            // Objects that appear: made, cloned, activated, reparented, each with an Image.
            foreach (var appear in new[] { "new", "instantiate", "activate", "reparent", "reparentStays", "siblingIndex" })
            {
                logMessages = false;
                var holder = NewRect("H", canvas.transform, new Vector2(300, 300));
                var other = NewRect("O", canvas.transform, new Vector2(100, 100));
                other.anchoredPosition = new Vector2(50, 0);
                var p = NewRect("P", holder, new Vector2(200, 100));
                p.gameObject.AddComponent<Spy>();
                p.gameObject.AddComponent<Image>();
                var q = NewRect("Q", holder, new Vector2(200, 100));
                q.gameObject.AddComponent<Spy>();
                q.gameObject.AddComponent<Image>();
                if (appear == "activate") p.gameObject.SetActive(false);
                yield return null;
                yield return null;
                caseTag = "extra.appear." + appear;
                logMessages = true;
                phase = "now";
                switch (appear)
                {
                    case "new":
                        var n = NewRect("New", holder, new Vector2(20, 20));
                        n.gameObject.AddComponent<Spy>();
                        n.gameObject.AddComponent<Image>();
                        break;
                    case "instantiate":
                        Instantiate(p.gameObject, holder).name = "Clone";
                        break;
                    case "activate":
                        p.gameObject.SetActive(true);
                        break;
                    case "reparent":
                        p.SetParent(other, false);
                        break;
                    case "reparentStays":
                        p.SetParent(other, true);
                        break;
                    case "siblingIndex":
                        p.SetAsLastSibling();
                        break;
                }
                yield return null;
                yield return null;
                L(caseTag + " end");
                logMessages = false;
                Destroy(holder.gameObject);
                Destroy(other.gameObject);
                yield return null;
            }
            // A move made after rendering (end of frame).
            {
                var p = NewRect("P", canvas.transform, new Vector2(200, 100));
                p.gameObject.AddComponent<Spy>();
                p.gameObject.AddComponent<Image>();
                yield return null;
                yield return null;
                caseTag = "extra.endOfFrame";
                logMessages = true;
                yield return new WaitForEndOfFrame();
                phase = "E";
                p.anchoredPosition += new Vector2(10, 0);
                L(caseTag + " moved at the end of the frame");
                yield return null;
                yield return null;
                L(caseTag + " end");
                logMessages = false;
                Destroy(p.gameObject);
                yield return null;
            }
            // Nested canvases under the pixel-perfect root: inheriting, and overriding it off.
            foreach (var nested in new[] { "inherit", "overrideOff", "overrideOn" })
            {
                var n = NewRect("N", canvas.transform, new Vector2(400, 300));
                var nc = n.gameObject.AddComponent<Canvas>();
                if (nested != "inherit")
                {
                    OverridePixelPerfect(nc);
                    nc.pixelPerfect = nested == "overrideOn";
                }
                var p = NewRect("P", n, new Vector2(200, 100));
                p.gameObject.AddComponent<Spy>();
                p.gameObject.AddComponent<Image>();
                yield return null;
                yield return null;
                caseTag = "extra.nested." + nested + " pixelPerfect=" + nc.pixelPerfect;
                logMessages = true;
                phase = "now";
                p.anchoredPosition += new Vector2(10, 0);
                yield return null;
                yield return null;
                L(caseTag + " end");
                logMessages = false;
                Destroy(n.gameObject);
                yield return null;
            }
            // The root itself turned off: a nested canvas overriding it on.
            {
                canvas.pixelPerfect = false;
                var n = NewRect("N", canvas.transform, new Vector2(400, 300));
                var nc = n.gameObject.AddComponent<Canvas>();
                OverridePixelPerfect(nc);
                nc.pixelPerfect = true;
                var p = NewRect("P", n, new Vector2(200, 100));
                p.gameObject.AddComponent<Spy>();
                p.gameObject.AddComponent<Image>();
                var q = NewRect("Q", canvas.transform, new Vector2(200, 100));
                q.gameObject.AddComponent<Spy>();
                q.gameObject.AddComponent<Image>();
                yield return null;
                yield return null;
                caseTag = "extra.rootOff.nestedOn pixelPerfect=" + nc.pixelPerfect;
                logMessages = true;
                phase = "now";
                p.anchoredPosition += new Vector2(10, 0);
                q.anchoredPosition += new Vector2(10, 0);
                yield return null;
                yield return null;
                L(caseTag + " end");
                logMessages = false;
                Destroy(n.gameObject);
                Destroy(q.gameObject);
                canvas.pixelPerfect = true;
                yield return null;
            }
            // Turning pixelPerfect on with nothing moved, and the canvas's scale changed.
            {
                canvas.pixelPerfect = false;
                var p = NewRect("P", canvas.transform, new Vector2(200, 100));
                p.gameObject.AddComponent<Spy>();
                p.gameObject.AddComponent<Image>();
                yield return null;
                yield return null;
                caseTag = "extra.turnOn";
                logMessages = true;
                phase = "now";
                canvas.pixelPerfect = true;
                yield return null;
                yield return null;
                L(caseTag + " end");
                caseTag = "extra.scaleFactor";
                phase = "now";
                canvas.scaleFactor = 0.9f;
                yield return null;
                yield return null;
                L(caseTag + " end");
                logMessages = false;
                canvas.scaleFactor = 1f;
                Destroy(p.gameObject);
                yield return null;
            }
        }

        /// Canvas.overridePixelPerfect, through reflection: a player without it (this project's,
        /// whose surface is what the game uses) still runs the other cases.
        static void OverridePixelPerfect(Canvas c)
        {
            var p = typeof(Canvas).GetProperty("overridePixelPerfect");
            if (p == null) L("overridePixelPerfect absent");
            else p.SetValue(c, true);
        }

        IEnumerator Motd(bool fitter, bool mask = false, bool arf = false, Canvas on = null, string tag = "")
        {
            logMessages = false;
            caseTag = "motd" + cfgTag + "." + (fitter ? "fitter" : "plain") + (mask ? ".mask" : "") + (arf ? ".arf" : "") + tag;
            var g = NewRect("MOTD", (on ?? canvas).transform, new Vector2(1000, 214.5f));
            g.pivot = new Vector2(1f, 0f);
            var hlg = g.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.gameObject.AddComponent<Spy>();
            hlg.childAlignment = TextAnchor.LowerRight;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 4;
            var tiles = new RectTransform[3];
            var buttons = new RatioElement[3];
            for (int i = 0; i < 3; i++)
            {
                var t = NewRect("T" + i, g, new Vector2(100, 100));
                var vlg = t.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 8, 8);
                vlg.spacing = 4;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                if (fitter) t.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                t.gameObject.AddComponent<Spy>();
                var header = NewRect("Header" + i, t, new Vector2(100, 18));
                header.gameObject.AddComponent<Spy>();
                var hle = header.gameObject.AddComponent<LayoutElement>();
                hle.minHeight = 18;
                hle.preferredHeight = 18;
                var button = NewRect("Button" + i, t, new Vector2(100, 100));
                button.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
                buttons[i] = button.gameObject.AddComponent<RatioElement>();
                if (mask) button.gameObject.AddComponent<Mask>();
                if (arf)
                {
                    // The tile's picture: a RawImage enveloping the button, ignored by the layout.
                    var pic = NewRect("Picture" + i, button, new Vector2(100, 100));
                    pic.gameObject.AddComponent<RawImage>();
                    pic.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    var f = pic.gameObject.AddComponent<AspectRatioFitter>();
                    f.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    f.aspectRatio = 1.6137f;
                    pic.gameObject.AddComponent<Spy>();
                }
                tiles[i] = t;
                t.gameObject.SetActive(false);
            }
            yield return null;
            yield return null;
            logMessages = true;
            L(caseTag + " activate");
            phase = "now";
            for (int i = 0; i < 3; i++) tiles[i].gameObject.SetActive(true);
            for (int f = 0; f < 8; f++)
            {
                yield return null;
                phase = "co";
                L(caseTag + " co pref=" + F(hlg.preferredWidth) + " tiles=" + F(tiles[0].anchoredPosition.x) + "/" + F(tiles[0].rect.width) + "," + F(tiles[1].anchoredPosition.x) + "," + F(tiles[2].anchoredPosition.x)
                  + " button=" + F(((RectTransform)buttons[0].transform).rect.size) + " pw=" + F(buttons[0].preferredWidth));
            }
            logMessages = false;
            Destroy(g.gameObject);
            yield return null;
        }
    }
}
