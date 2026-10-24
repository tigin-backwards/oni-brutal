// The draw order of root overlay canvases with the same sorting order ("PROBE ... CO ..."), in a
// colony (it starts 120 frames after Game.Instance exists). With PROBE_SCENARIO=canvasorder only
// this probe runs; PROBE_CAPTURE_DIR receives a screenshot per step, which co-check.py reads.
//
// Each probe canvas is a root ScreenSpaceOverlay canvas at sorting order 31000 that draws a
// horizontal stripe (its row) and a vertical stripe (its column) in its own colour. At the
// crossing of canvas i's row and canvas j's column only those two canvases draw, so the pixel
// there shows which of the two is on top. co-check.py turns the pairs into an order.
//
// Steps change one thing at a time and capture: creation, a canvas component disabled and
// enabled, a GameObject deactivated and activated, nested canvases (with and without
// overrideSorting) enabled under another probe canvas or under an unrelated root, a new root
// canvas at another sorting order, sortingOrder changed and restored, sibling order and
// parenting, and objects created inactive and activated in another order.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class CanvasOrder : MonoBehaviour
    {
        static void L(string s) => Rec.Log("CO " + s);

        const int Order = 31000;
        const float Origin = 100f, Pitch = 30f, Width = 16f, Span = 200f;
        static readonly Color[] Colours =
        {
            new Color(1f, 0f, 0f), new Color(0f, 1f, 0f), new Color(0f, 0f, 1f),
            new Color(1f, 1f, 0f), new Color(1f, 0f, 1f), new Color(0f, 1f, 1f),
        };

        readonly List<Canvas> canvases = new List<Canvas>();
        readonly List<GameObject> extra = new List<GameObject>();
        string dir;
        int wait = -1;
        bool started;
        int step;

        void Update()
        {
            if (started) return;
            if (Game.Instance == null) return;
            if (wait < 0) wait = Time.frameCount + 120;
            if (Time.frameCount < wait) return;
            started = true;
            StartCoroutine(Run());
        }

        /// A probe canvas: index i draws row i and column i. `parent` null puts it at the root.
        Canvas Make(int i, Transform parent = null, bool active = true, GameObject existing = null)
        {
            var go = existing != null ? existing : new GameObject("ProbeCanvas" + (char)('A' + i), typeof(RectTransform));
            if (!active) go.SetActive(false);
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = Order;
            Stripe(go.transform, "Row", new Rect(Origin - 20f, Origin + Pitch * i, Span, Width), Colours[i]);
            Stripe(go.transform, "Col", new Rect(Origin + Pitch * i, Origin - 20f, Width, Span), Colours[i]);
            while (canvases.Count <= i) canvases.Add(null);
            canvases[i] = c;
            return c;
        }

        /// A rect in screen pixels from the bottom left (canvas scale 1).
        static GameObject Stripe(Transform parent, string name, Rect r, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = r.position;
            rt.sizeDelta = r.size;
            go.AddComponent<Image>().color = colour;
            return go;
        }

        /// A nested canvas under `parent`, drawing a small square away from the stripes.
        GameObject Nested(Transform parent, string name, bool overrideSorting, bool active)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            if (overrideSorting) { c.overrideSorting = true; c.sortingOrder = 100; }
            Stripe(go.transform, "Square", new Rect(400f, 400f, 10f, 10f), Color.white);
            go.SetActive(active);
            extra.Add(go);
            return go;
        }

        /// From the sorted order (C,B,A bottom to top, A made first) to B,A,C: C's GameObject
        /// deactivated and activated.
        IEnumerator Scramble(string label)
        {
            canvases[2].gameObject.SetActive(false);
            canvases[2].gameObject.SetActive(true);
            yield return Capture(label + "-scrambled");
        }

        GameObject OtherRoot(string name, int order, bool active = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (!active) go.SetActive(false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            extra.Add(go);
            return go;
        }

        /// What re-sorts the list: from a scrambled order, one change at a time.
        IEnumerator Triggers()
        {
            L("=== triggers: each from B,A,C (C's GameObject toggled); C,B,A means a re-sort by creation");
            Make(0); Make(1); Make(2);
            var other = OtherRoot("ProbeOther", 50);
            var otherNested = Nested(other.transform, "OtherNestedOverride", true, false);
            var aNested = Nested(canvases[0].transform, "ANestedOverride", true, false);
            yield return Capture("t-base");
            yield return Scramble("t-wait");
            for (int i = 0; i < 30; i++) yield return null;
            yield return Capture("t-wait-30-frames");

            yield return Scramble("t-new-root");
            OtherRoot("ProbeNewRoot", 60);
            yield return Capture("t-new-root-order-60");

            yield return Scramble("t-new-root-default");
            var plain = new GameObject("ProbeNewRootDefault", typeof(RectTransform));
            plain.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            extra.Add(plain);
            yield return Capture("t-new-root-default-order");

            yield return Scramble("t-new-root-inactive");
            var later = OtherRoot("ProbeNewRootInactive", 70, active: false);
            yield return Capture("t-new-root-inactive-made");
            later.SetActive(true);
            yield return Capture("t-new-root-inactive-activated");

            yield return Scramble("t-same-order-A");
            canvases[0].sortingOrder = Order;
            yield return Capture("t-A-order-set-same");

            yield return Scramble("t-same-order-other");
            other.GetComponent<Canvas>().sortingOrder = 50;
            yield return Capture("t-other-order-set-same");

            yield return Scramble("t-other-order");
            other.GetComponent<Canvas>().sortingOrder = 51;
            yield return Capture("t-other-order-51");

            yield return Scramble("t-other-toggle");
            other.SetActive(false);
            other.SetActive(true);
            yield return Capture("t-other-toggled");

            yield return Scramble("t-other-disable");
            other.GetComponent<Canvas>().enabled = false;
            yield return Capture("t-other-disabled");
            other.GetComponent<Canvas>().enabled = true;
            yield return Capture("t-other-enabled");

            yield return Scramble("t-other-nested");
            otherNested.SetActive(true);
            yield return Capture("t-other-nested-override-active");
            otherNested.SetActive(false);
            yield return Capture("t-other-nested-override-inactive");

            yield return Scramble("t-a-nested");
            aNested.SetActive(true);
            yield return Capture("t-A-nested-override-active");
            aNested.SetActive(false);
            yield return Capture("t-A-nested-override-inactive");

            yield return Scramble("t-nested-order");
            otherNested.SetActive(true);
            yield return Capture("t-nested-order-active");
            otherNested.GetComponent<Canvas>().sortingOrder = 101;
            yield return Capture("t-nested-order-101");

            yield return Scramble("t-nested-override-flag");
            otherNested.GetComponent<Canvas>().overrideSorting = false;
            yield return Capture("t-nested-override-off");
            otherNested.GetComponent<Canvas>().overrideSorting = true;
            yield return Capture("t-nested-override-on");

            yield return Scramble("t-nested-new");
            var fresh = new GameObject("FreshNested", typeof(RectTransform));
            fresh.transform.SetParent(other.transform, false);
            fresh.AddComponent<Canvas>();
            extra.Add(fresh);
            yield return Capture("t-nested-new-plain");

            yield return Scramble("t-nested-new-override");
            var fresh2 = new GameObject("FreshNestedOverride", typeof(RectTransform));
            fresh2.transform.SetParent(other.transform, false);
            var fc = fresh2.AddComponent<Canvas>();
            fc.overrideSorting = true;
            fc.sortingOrder = 5;
            extra.Add(fresh2);
            yield return Capture("t-nested-new-override");

            yield return Scramble("t-destroy-root");
            Destroy(plain);
            yield return Capture("t-root-destroyed");

            yield return Scramble("t-instantiate");
            var copy = Instantiate(other);
            extra.Add(copy);
            yield return Capture("t-root-instantiated");

            yield return Scramble("t-ui-root-graphic");
            var g = new GameObject("RootLabel", typeof(RectTransform));
            g.AddComponent<Image>();
            extra.Add(g);
            yield return Capture("t-graphic-at-root");

            yield return Scramble("t-force-update");
            Canvas.ForceUpdateCanvases();
            yield return Capture("t-force-update-canvases");

            yield return Scramble("t-render-mode");
            other.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            yield return Capture("t-other-render-mode-set-same");

            // Reparenting within a root, as ToolTipScreen moves its widget (a nested override
            // canvas) under its anchor each time it shows a tooltip.
            var anchor = new GameObject("OtherAnchor", typeof(RectTransform));
            anchor.transform.SetParent(other.transform, false);
            extra.Add(anchor);
            otherNested.SetActive(true);
            otherNested.GetComponent<Canvas>().overrideSorting = true;
            for (int i = 0; i < 5; i++) yield return null;
            yield return Scramble("t-nested-reparent");
            otherNested.transform.SetParent(anchor.transform);
            yield return Capture("t-nested-override-reparented");
            yield return Scramble("t-nested-reparent-same");
            otherNested.transform.SetParent(anchor.transform);
            yield return Capture("t-nested-override-reparented-same-parent");
            yield return Scramble("t-nested-reparent-back");
            otherNested.transform.SetParent(other.transform, false);
            yield return Capture("t-nested-override-reparented-back");
            fresh.SetActive(true);
            yield return Scramble("t-nested-plain-reparent");
            fresh.transform.SetParent(anchor.transform);
            yield return Capture("t-nested-plain-reparented");
            otherNested.SetActive(false);
            yield return Scramble("t-nested-inactive-reparent");
            otherNested.transform.SetParent(other.transform, false);
            yield return Capture("t-nested-inactive-reparented");
            otherNested.SetActive(true);
            var img = new GameObject("PlainImage", typeof(RectTransform));
            img.transform.SetParent(other.transform, false);
            img.AddComponent<Image>();
            extra.Add(img);
            for (int i = 0; i < 3; i++) yield return null;
            yield return Scramble("t-image-reparent");
            img.transform.SetParent(anchor.transform, false);
            yield return Capture("t-image-reparented");
            yield return Scramble("t-nested-order-later");
            fc.sortingOrder = 6;
            yield return Capture("t-nested-override-order-6");
            yield return Scramble("t-nested-plain-new-order");
            var fresh3 = new GameObject("FreshNestedPlainOrder", typeof(RectTransform));
            fresh3.transform.SetParent(other.transform, false);
            fresh3.AddComponent<Canvas>().sortingOrder = 7;
            extra.Add(fresh3);
            yield return Capture("t-nested-plain-new-order-set");
            yield return Scramble("t-nested-override-new-no-order");
            var fresh4 = new GameObject("FreshNestedOverrideNoOrder", typeof(RectTransform));
            fresh4.transform.SetParent(other.transform, false);
            fresh4.AddComponent<Canvas>().overrideSorting = true;
            extra.Add(fresh4);
            yield return Capture("t-nested-override-new-no-order-set");
            yield return Scramble("t-root-sibling");
            canvases[1].transform.SetAsLastSibling();
            yield return Capture("t-B-last-sibling");
            yield return Scramble("t-root-setparent-null");
            canvases[0].transform.SetParent(null);
            yield return Capture("t-A-setparent-null");
            yield return Scramble("t-layer");
            canvases[0].sortingLayerID = canvases[0].sortingLayerID;
            yield return Capture("t-A-layer-set-same");
            yield return Scramble("t-nest-root");
            canvases[0].transform.SetParent(other.transform, false);
            yield return Capture("t-A-nested-in-other");
            canvases[0].transform.SetParent(null, false);
            yield return Capture("t-A-back-at-root");

            yield return Scramble("t-reparent");
            var holder = new GameObject("ProbeHolder");
            extra.Add(holder);
            canvases[1].transform.SetParent(holder.transform, false);
            yield return Capture("t-B-reparented");
            yield return Scramble("t-holder-moved");
            var holder2 = new GameObject("ProbeHolder2");
            extra.Add(holder2);
            holder.transform.SetParent(holder2.transform, false);
            yield return Capture("t-holder-of-B-reparented");
            yield return Scramble("t-nested-in-moved-object");
            var mover = new GameObject("Mover", typeof(RectTransform));
            mover.transform.SetParent(other.transform, false);
            extra.Add(mover);
            otherNested.transform.SetParent(mover.transform, false);
            yield return Capture("t-nested-moved-under-mover");
            yield return Scramble("t-mover-moved");
            mover.transform.SetParent(anchor.transform, false);
            yield return Capture("t-mover-holding-nested-reparented");
            // Nested override canvases set up inactive (as Nested makes them) or active, then a
            // sortingOrder set after some frames.
            var x1 = Nested(other.transform, "X1MadeInactive", true, false);
            x1.SetActive(true);
            var x2 = new GameObject("X2MadeActive", typeof(RectTransform));
            x2.transform.SetParent(other.transform, false);
            var x2c = x2.AddComponent<Canvas>();
            x2c.overrideSorting = true;
            extra.Add(x2);
            for (int i = 0; i < 5; i++) yield return null;
            yield return Scramble("t-x1");
            x1.GetComponent<Canvas>().sortingOrder = 102;
            yield return Capture("t-x1-made-inactive-order-set");
            yield return Scramble("t-x2");
            x2c.sortingOrder = 103;
            yield return Capture("t-x2-made-active-order-set");
            yield return Scramble("t-x1-again");
            x1.GetComponent<Canvas>().sortingOrder = 104;
            yield return Capture("t-x1-order-set-again");
            yield return Scramble("t-x1-same");
            x1.GetComponent<Canvas>().sortingOrder = 104;
            yield return Capture("t-x1-order-set-same");
            Clear();
            yield return null;

            // Canvas components added in the reverse order of their GameObjects' creation.
            L("=== component order: GameObjects A, B, C made, Canvases added C, B, A");
            var ga = new GameObject("ProbeCanvasA", typeof(RectTransform));
            var gb = new GameObject("ProbeCanvasB", typeof(RectTransform));
            var gc = new GameObject("ProbeCanvasC", typeof(RectTransform));
            Make(2, existing: gc); Make(1, existing: gb); Make(0, existing: ga);
            yield return Capture("r-components-CBA");
            canvases[2].gameObject.SetActive(false);
            canvases[2].gameObject.SetActive(true);
            canvases[0].gameObject.SetActive(false);
            canvases[0].gameObject.SetActive(true);
            yield return Capture("r-scrambled");
            OtherRoot("ProbeNewRoot2", 60);
            yield return Capture("r-new-root");
            Clear();
            yield return null;
        }

        void Clear()
        {
            foreach (var c in canvases) if (c != null) Destroy(c.gameObject);
            foreach (var g in extra) if (g != null) Destroy(g);
            canvases.Clear();
            extra.Clear();
        }

        IEnumerator Capture(string label)
        {
            for (int i = 0; i < 3; i++) yield return null;
            step++;
            string name = "co-" + step.ToString("D3") + "-" + label;
            string desc = "";
            for (int i = 0; i < canvases.Count; i++)
                if (canvases[i] != null)
                    desc += " " + (char)('A' + i) + "(enabled=" + canvases[i].enabled + " active=" + canvases[i].gameObject.activeInHierarchy
                            + " root=" + canvases[i].isRootCanvas + " order=" + canvases[i].sortingOrder + " id=" + canvases[i].GetInstanceID()
                            + " goid=" + canvases[i].gameObject.GetInstanceID() + " sibling=" + canvases[i].transform.GetSiblingIndex() + ")";
            L("step " + name + " n=" + canvases.Count + desc);
            if (!string.IsNullOrEmpty(dir)) ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            for (int i = 0; i < 3; i++) yield return null;
        }

        IEnumerator Run()
        {
            dir = System.Environment.GetEnvironmentVariable("PROBE_CAPTURE_DIR");
            L("=== start screen " + Screen.width + "x" + Screen.height + " origin " + Origin + " pitch " + Pitch + " width " + Width
              + " (pixel of pair (row i, col j): x=" + (Origin + Pitch + Width / 2) + "+" + Pitch + "(j-1), y from bottom=" + (Origin + Width / 2) + "+" + Pitch + "i)");

            // 1. Created in order A, B, C at the root.
            Make(0); Make(1); Make(2);
            yield return Capture("created-ABC");
            yield return Capture("created-ABC-again");
            // 2. A's component disabled, then enabled.
            canvases[0].enabled = false;
            yield return Capture("A-disabled");
            canvases[0].enabled = true;
            yield return Capture("A-reenabled");
            // 3. B's GameObject deactivated, then activated.
            canvases[1].gameObject.SetActive(false);
            yield return Capture("B-inactive");
            canvases[1].gameObject.SetActive(true);
            yield return Capture("B-reactivated");
            // 4. A nested canvas with overrideSorting under C, activated (as a tooltip is).
            var n1 = Nested(canvases[2].transform, "NestedOverrideInC", true, false);
            yield return Capture("nested-override-in-C-created-inactive");
            n1.SetActive(true);
            yield return Capture("nested-override-in-C-active");
            n1.SetActive(false);
            yield return Capture("nested-override-in-C-inactive");
            // 5. A plain nested canvas under A activated.
            var n2 = Nested(canvases[0].transform, "NestedPlainInA", false, false);
            n2.SetActive(true);
            yield return Capture("nested-plain-in-A-active");
            // 6. An unrelated root canvas (another order) with a nested override canvas.
            var other = new GameObject("ProbeOtherRoot", typeof(RectTransform));
            var oc = other.AddComponent<Canvas>();
            oc.renderMode = RenderMode.ScreenSpaceOverlay;
            oc.sortingOrder = 50;
            extra.Add(other);
            yield return Capture("other-root-created");
            var n3 = Nested(other.transform, "NestedOverrideInOther", true, false);
            n3.SetActive(true);
            yield return Capture("nested-override-in-other-active");
            n3.SetActive(false);
            yield return Capture("nested-override-in-other-inactive");
            n3.SetActive(true);
            yield return Capture("nested-override-in-other-active-again");
            // 7. sortingOrder of A changed and restored.
            canvases[0].sortingOrder = Order + 1;
            yield return Capture("A-order-raised");
            canvases[0].sortingOrder = Order;
            yield return Capture("A-order-restored");
            canvases[2].sortingOrder = Order - 1;
            yield return Capture("C-order-lowered");
            canvases[2].sortingOrder = Order;
            yield return Capture("C-order-restored");
            // 8. Sibling order at the root.
            canvases[2].transform.SetAsFirstSibling();
            yield return Capture("C-first-sibling");
            canvases[0].transform.SetAsLastSibling();
            yield return Capture("A-last-sibling");
            Clear();
            yield return null;

            // 9. Created inactive as C, B, A; activated A, B, C.
            Make(2, null, false); Make(1, null, false); Make(0, null, false);
            canvases[0].gameObject.SetActive(true);
            canvases[1].gameObject.SetActive(true);
            canvases[2].gameObject.SetActive(true);
            yield return Capture("made-CBA-activated-ABC");
            canvases[1].enabled = false;
            canvases[1].enabled = true;
            yield return Capture("B-toggled-same-frame");
            Clear();
            yield return null;

            // 10. Under non-canvas parents P0 (holding C) and P1 (holding A, B), created in that order.
            var p0 = new GameObject("ProbeParent0"); extra.Add(p0);
            var p1 = new GameObject("ProbeParent1"); extra.Add(p1);
            Make(2, p0.transform); Make(0, p1.transform); Make(1, p1.transform);
            yield return Capture("parents-C-in-P0-AB-in-P1");
            p0.transform.SetAsLastSibling();
            yield return Capture("P0-last-sibling");
            p1.SetActive(false);
            p1.SetActive(true);
            yield return Capture("P1-toggled");
            var n4 = Nested(canvases[2].transform, "NestedOverrideInC2", true, true);
            yield return Capture("nested-override-in-C-active-2");
            Clear();
            yield return null;

            // 11. Four canvases: D created after the others enabled, then a nested override
            // activated under B.
            Make(0); Make(1); Make(2);
            yield return Capture("ABC");
            canvases[0].enabled = false; canvases[0].enabled = true;
            yield return Capture("ABC-A-toggled");
            Make(3);
            yield return Capture("ABC-A-toggled-D-made");
            var n5 = Nested(canvases[1].transform, "NestedOverrideInB", true, true);
            yield return Capture("ABCD-nested-override-in-B");
            var n6 = Nested(canvases[3].transform, "NestedPlainInD", false, true);
            yield return Capture("ABCD-nested-plain-in-D");
            Clear();
            yield return null;

            yield return Triggers();
            var done = new GameObject("CanvasOrderProbeDone", typeof(RectTransform));
            done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            L("=== done");
        }
    }
}
