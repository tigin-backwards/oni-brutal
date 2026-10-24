// A scroll rect's first frames under an ancestor at scale 0, lines "PROBE ... ZS ...", at the
// main menu (10 s in). Found with Resize Dup UI (Workshop 2549295369): TableScreen.OnActivate
// sets the Vitals screen's scale to 0, and the next frame's ScreenUpdate sets it to 1. In ours the
// screen's KScrollRect, in its first LateUpdate under the zero scale, found its content 43 px
// above the view and sprang it down; the next frame its scrollbar was set to the overshoot and to
// value 1, which snapped the content back, and the size stayed (0.94 after a slow frame). The
// original's Vitals scrollbar ends at size 1 and value 0 in four runs of five.
//
// Each case builds, on an overlay canvas of its own (scale 1): S (1116 x 610, anchored and pivoted
// at the canvas's top right), holding SV (1114 x 500, its top 43 below S's, centre pivot) with a
// ZsScrollRect (vertical, Elastic, elasticity 0.2, inertia, deceleration 0.02, as KScrollRect's
// Awake sets), its Viewport (stretched, 4 in from each side, a RectMask2D) and Content (anchored
// along the viewport's top, pivot (0.5, 1), height 16), and a vertical Scrollbar (BottomToTop,
// 9 wide along SV's right side) with a Handle. SV starts inactive. Then, from a coroutine after
// Update:
// - zero: S's scale set to 0 and SV activated in one frame; the next frame S's scale is 1 again
//   and Content's height 208;
// - zero-slow: the same, after a frame made 250 ms long (Thread.Sleep), so the activation
//   frame's unscaledDeltaTime is about 0.25;
// - one: the same with S at scale 1 throughout;
// - zero-later: SV active for five frames first, then S at scale 0 for one frame;
// - zero-update, zero-update-slow: as zero and zero-slow, but the activation made from the
//   Update of a behaviour made at mod load (as OniMcp's bridge runs its requests).
// The scroll rect logs before and after its LateUpdate (content anchoredPosition, the bounds it
// holds, velocity, the scrollbar's value and size, normalizedPosition), and each end of frame
// logs the same; eight frames per case.
// With PROBE_SCENARIO=zeroscale only this probe runs.
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class ZsScrollRect : ScrollRect
    {
        public string Case = "";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string F(float f) => f.ToString("R", Inv);

        public string State()
        {
            var bar = verticalScrollbar;
            return "ap=" + F(content.anchoredPosition.y) + " cb=" + F(m_ContentBounds.min.y) + ".." + F(m_ContentBounds.max.y)
                + " vb=" + F(viewport.rect.yMin) + ".." + F(viewport.rect.yMax) + " vel=" + F(velocity.y)
                + " bar=" + (bar == null ? "none" : F(bar.value) + "/" + F(bar.size)) + " crect=" + F(content.rect.height)
                + " scale=" + F(transform.parent.localScale.y);
        }

        protected override void LateUpdate()
        {
            Rec.Log("ZS " + Case + " LU before dt=" + F(Time.unscaledDeltaTime) + " " + State());
            base.LateUpdate();
            Rec.Log("ZS " + Case + " LU after " + State() + " norm=" + F(verticalNormalizedPosition));
        }
    }

    /// Acts from its Update, as OniMcp's main-thread bridge runs a request.
    public class ZsActor : MonoBehaviour
    {
        public System.Action Pending;
        void Update()
        {
            var a = Pending;
            Pending = null;
            a?.Invoke();
        }
    }

    public class ZeroScale : MonoBehaviour
    {
        ZsActor actor;
        void Awake() => actor = gameObject.AddComponent<ZsActor>();

        bool started;
        float sceneStart = -1f;

        static void L(string s) => Rec.Log("ZS " + s);

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static ZsScrollRect Build(string name, out RectTransform s, out RectTransform content, out GameObject canvasGo)
        {
            canvasGo = new GameObject("ZsCanvas-" + name, typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            s = Rect("S", canvasGo.transform);
            s.anchorMin = s.anchorMax = s.pivot = Vector2.one;
            s.sizeDelta = new Vector2(1116f, 610f);
            s.anchoredPosition = Vector2.zero;
            var sv = Rect("SV", s);
            sv.gameObject.SetActive(false);
            sv.anchorMin = sv.anchorMax = new Vector2(0.5f, 1f);
            sv.pivot = new Vector2(0.5f, 0.5f);
            sv.sizeDelta = new Vector2(1114f, 500f);
            sv.anchoredPosition = new Vector2(0f, -43f - 250f);
            var viewport = Rect("Viewport", sv);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.sizeDelta = new Vector2(-8f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 16f);
            content.anchoredPosition = Vector2.zero;
            var barRect = Rect("Scrollbar", sv);
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = Vector2.one;
            barRect.pivot = Vector2.one;
            barRect.sizeDelta = new Vector2(9f, -10f);
            barRect.anchoredPosition = new Vector2(0f, -5f);
            barRect.gameObject.AddComponent<Image>();
            var area = Rect("Sliding Area", barRect);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.sizeDelta = Vector2.zero;
            var handle = Rect("Handle", area);
            handle.sizeDelta = new Vector2(-2f, -2f);
            handle.gameObject.AddComponent<Image>();
            var bar = barRect.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            var sr = sv.gameObject.AddComponent<ZsScrollRect>();
            sr.Case = name;
            sr.content = content;
            sr.viewport = viewport;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.elasticity = 0.2f;
            sr.inertia = true;
            sr.decelerationRate = 0.02f;
            sr.verticalScrollbar = bar;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return sr;
        }

        IEnumerator EndLogs(ZsScrollRect sr, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return new WaitForEndOfFrame();
                L(sr.Case + " end " + i + " " + sr.State());
            }
        }

        IEnumerator Case(string name, bool zero, bool slow, bool later, bool inUpdate = false)
        {
            L("case " + name + " begin");
            var sr = Build(name, out var s, out var content, out var canvasGo);
            yield return null;
            if (later)
            {
                sr.gameObject.SetActive(true);
                for (int i = 0; i < 5; i++) yield return null;
            }
            if (slow) System.Threading.Thread.Sleep(250);
            System.Action act = () =>
            {
                L(name + " act");
                if (zero) s.localScale = Vector3.zero;
                if (!later) sr.gameObject.SetActive(true);
                StartCoroutine(EndLogs(sr, 8));
            };
            if (inUpdate)
            {
                // Run by the actor's Update in the next frame; the coroutine resumes after it.
                actor.Pending = act;
                yield return null;
            }
            else
            {
                yield return null;
                act();
            }
            yield return null;
            L(name + " restore");
            s.localScale = Vector3.one;
            content.sizeDelta = new Vector2(0f, 208f);
            for (int i = 0; i < 8; i++) yield return null;
            Object.Destroy(canvasGo);
            yield return null;
            L("case " + name + " end");
        }

        IEnumerator Run()
        {
            yield return Case("zero", true, false, false);
            yield return Case("zero-slow", true, true, false);
            yield return Case("one", false, false, false);
            yield return Case("zero-later", true, false, true);
            yield return Case("zero-slow2", true, true, false);
            yield return Case("zero-update", true, false, false, true);
            yield return Case("zero-update-slow", true, true, false, true);
            L("done");
        }
    }
}
