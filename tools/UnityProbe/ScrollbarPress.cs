// Scrollbar presses on the track ("PROBE ... SB ..."), at the main menu: what a press beside the
// handle does to `value`, frame by frame while the button is held and after it is released, and
// when `onValueChanged` is invoked. Found through the Consumables table (a press on its header's
// scrollbar track to the right of the handle): the original moved the value from 0 to 0.49994
// with size 0.4496, ours by one size. The pointer is scripted through the current input
// module's inputOverride, as in PointerMove.cs.
//
// Each case builds a fresh scrollbar on an overlay canvas of its own (scale 1): the track an
// Image of 400 x 20 (20 x 400 vertical) at the screen centre, a "Sliding Area" child stretched
// over it, and a "Handle" child Image in it as handleRect. A case sets direction, size, value
// and numberOfSteps, waits two frames, then presses at a fraction of the track's length (0 at
// the track's left or bottom end), holds for a number of frames and releases. Each frame logs the
// value and the handle's rect; each onValueChanged logs its argument.
// With PROBE_SCENARIO=scrollbar only this probe runs.
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class ScrollbarPress : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static void L(string s) => Rec.Log("SB " + s);
        static string F(float f) => f.ToString("R", Inv);
        static string V(Vector2 v) => F(v.x) + "," + F(v.y);

        class ScriptedInput : BaseInput
        {
            public Vector2 pos;
            public bool down, pressed, released;
            public override Vector2 mousePosition => pos;
            public override bool mousePresent => true;
            public override Vector2 mouseScrollDelta => Vector2.zero;
            public override bool GetMouseButton(int button) => button == 0 && down;
            public override bool GetMouseButtonDown(int button) => button == 0 && pressed;
            public override bool GetMouseButtonUp(int button) => button == 0 && released;
            public override bool touchSupported => false;
            public override int touchCount => 0;
            public override float GetAxisRaw(string axisName) => 0f;
            public override bool GetButtonDown(string buttonName) => false;
        }

        struct Case
        {
            public string name;
            public Scrollbar.Direction dir;
            public float size, value, at;
            public int steps, hold;
            /// Where the pointer goes on the frames after the press (null: stays).
            public float? moveTo;
        }

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        static Case C(string name, float size, float value, float at, int hold, Scrollbar.Direction dir = Scrollbar.Direction.LeftToRight, int steps = 0, float? moveTo = null)
            => new Case { name = name, size = size, value = value, at = at, hold = hold, dir = dir, steps = steps, moveTo = moveTo };

        IEnumerator Run()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            var module = es != null ? es.currentInputModule : null;
            L("eventsystem current=" + (module != null ? module.GetType().FullName : "null") + " dragThreshold=" + (es != null ? es.pixelDragThreshold : -1));
            if (module == null) yield break;
            var cgo = new GameObject("SbCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32002;
            cgo.AddComponent<GraphicRaycaster>();
            var input = module.gameObject.AddComponent<ScriptedInput>();
            var before = module.inputOverride;
            module.inputOverride = input;
            input.pos = new Vector2(-50f, -50f);
            var centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
            L("screen " + Screen.width + "x" + Screen.height + " scale=" + F(canvas.scaleFactor));

            var cases = new List<Case>
            {
                // The Consumables case: size 0.4496, value 0, a press right of the handle.
                C("consumables.at0.5.hold2", 0.4496488f, 0f, 0.5f, 2),
                C("s0.45.at0.5.hold1", 0.45f, 0f, 0.5f, 1),
                C("s0.45.at0.5.hold30", 0.45f, 0f, 0.5f, 30),
                C("s0.45.at0.6.hold2", 0.45f, 0f, 0.6f, 2),
                C("s0.45.at0.75.hold2", 0.45f, 0f, 0.75f, 2),
                C("s0.45.at0.9.hold2", 0.45f, 0f, 0.9f, 2),
                C("s0.45.at0.99.hold2", 0.45f, 0f, 0.99f, 2),
                C("s0.45.at0.99.hold60", 0.45f, 0f, 0.99f, 60),
                C("s0.45.at0.46.hold2", 0.45f, 0f, 0.46f, 2),
                C("s0.45.at0.2.onhandle", 0.45f, 0f, 0.2f, 2),
                C("s0.2.v0.at0.3.hold2", 0.2f, 0f, 0.3f, 2),
                C("s0.2.v0.at0.9.hold2", 0.2f, 0f, 0.9f, 2),
                C("s0.2.v0.at0.9.hold60", 0.2f, 0f, 0.9f, 60),
                C("s0.2.v0.5.at0.1.hold2", 0.2f, 0.5f, 0.1f, 2),
                C("s0.2.v0.5.at0.9.hold2", 0.2f, 0.5f, 0.9f, 2),
                C("s0.2.v1.at0.1.hold60", 0.2f, 1f, 0.1f, 60),
                C("s0.1.v0.at0.55.hold60", 0.1f, 0f, 0.55f, 60),
                C("s0.45.v0.at0.75.steps5", 0.45f, 0f, 0.75f, 2, steps: 5),
                C("s0.2.v0.at0.9.steps3.hold60", 0.2f, 0f, 0.9f, 60, steps: 3),
                C("rtl.s0.45.v0.at0.25.hold2", 0.45f, 0f, 0.25f, 2, Scrollbar.Direction.RightToLeft),
                C("btt.s0.3.v0.at0.8.hold2", 0.3f, 0f, 0.8f, 2, Scrollbar.Direction.BottomToTop),
                C("ttb.s0.3.v0.at0.8.hold2", 0.3f, 0f, 0.8f, 2, Scrollbar.Direction.TopToBottom),
                C("s0.2.v0.at0.9.movingto0.3.hold30", 0.2f, 0f, 0.9f, 30, moveTo: 0.3f),
                C("s0.2.v0.at0.9.movingoff.hold30", 0.2f, 0f, 0.9f, 30, moveTo: 2f),
                // Under and over the event system's drag threshold (20 px; 400 px track).
                C("s0.2.v0.at0.5.moving10px.hold10", 0.2f, 0f, 0.5f, 10, moveTo: 0.525f),
                C("s0.2.v0.at0.5.moving30px.hold10", 0.2f, 0f, 0.5f, 10, moveTo: 0.575f),
                // A press on the handle, off its centre, then a drag.
                C("s0.45.v0.at0.1.onhandle.dragto0.5", 0.45f, 0f, 0.1f, 10, moveTo: 0.5f),
            };
            foreach (var c in Part.StartsWith("clickrepeat") ? new List<Case>() : cases)
            {
                bool vertical = c.dir == Scrollbar.Direction.BottomToTop || c.dir == Scrollbar.Direction.TopToBottom;
                var track = new GameObject("Track", typeof(RectTransform));
                track.transform.SetParent(cgo.transform, false);
                var trt = (RectTransform)track.transform;
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.sizeDelta = vertical ? new Vector2(20f, 400f) : new Vector2(400f, 20f);
                trt.anchoredPosition = Vector2.zero;
                track.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.3f);
                var area = new GameObject("Sliding Area", typeof(RectTransform));
                area.transform.SetParent(track.transform, false);
                var art = (RectTransform)area.transform;
                art.anchorMin = Vector2.zero;
                art.anchorMax = Vector2.one;
                art.sizeDelta = Vector2.zero;
                var handle = new GameObject("Handle", typeof(RectTransform));
                handle.transform.SetParent(area.transform, false);
                var hrt = (RectTransform)handle.transform;
                hrt.sizeDelta = Vector2.zero;
                handle.AddComponent<Image>().color = Color.white;
                var bar = track.AddComponent<Scrollbar>();
                bar.handleRect = hrt;
                bar.direction = c.dir;
                bar.numberOfSteps = c.steps;
                bar.size = c.size;
                bar.value = c.value;
                string label = c.name;
                bar.onValueChanged.AddListener(v => L(label + " changed " + F(v) + " t=" + F(Time.unscaledTime)));
                yield return null;
                yield return null;
                L(c.name + " setup dir=" + c.dir + " size=" + F(bar.size) + " value=" + F(bar.value) + " steps=" + bar.numberOfSteps
                  + " handle=" + V(hrt.anchorMin) + ".." + V(hrt.anchorMax));
                Vector2 At(float f) => vertical ? centre + new Vector2(0f, (f - 0.5f) * 400f) : centre + new Vector2((f - 0.5f) * 400f, 0f);
                int total = 1 + c.hold + 1 + 3;
                for (int i = 0; i < total; i++)
                {
                    string phase;
                    if (i == 0) { input.pos = At(c.at); input.pressed = true; input.down = true; input.released = false; phase = "press"; }
                    else if (i <= c.hold)
                    {
                        input.pressed = false; input.down = true; input.released = false; phase = "held" + i;
                        if (c.moveTo.HasValue) input.pos = At(c.moveTo.Value);
                    }
                    else if (i == c.hold + 1) { input.pressed = false; input.down = false; input.released = true; phase = "release"; }
                    else { input.pressed = false; input.down = false; input.released = false; phase = "after" + (i - c.hold - 1); }
                    yield return null;
                    L(c.name + " " + phase + " at=" + V(input.pos) + " value=" + F(bar.value) + " handle=" + V(hrt.anchorMin) + ".." + V(hrt.anchorMax)
                      + " t=" + F(Time.unscaledTime) + " dt=" + F(Time.unscaledDeltaTime));
                }
                input.pos = new Vector2(-50f, -50f);
                input.released = false;
                yield return null;
                Destroy(track);
                yield return null;
            }
            if (Part != "press")
                yield return StartCoroutine(SubclassCases(cgo, input, centre));
            module.inputOverride = before;
            Destroy(input);
            Destroy(cgo);
            L("done");
        }

        /// PROBE_SB_PART: "press" runs only the press cases, "clickrepeat" only the subclass
        /// cases; unset runs both.
        static string Part => System.Environment.GetEnvironmentVariable("PROBE_SB_PART") ?? "";

        /// A subclass reaching the protected members a mod's subclass can: both ClickRepeat
        /// overloads, and Update (counted; base.Update skipped on request).
        public class ProbeScrollbar : Scrollbar
        {
            public bool skipBaseUpdate;
            public int updates;
            public IEnumerator RepeatFromEvent(PointerEventData eventData) => ClickRepeat(eventData);
            public IEnumerator RepeatFromPoint(Vector2 screenPosition, Camera camera) => ClickRepeat(screenPosition, camera);
            protected override void Update()
            {
                updates++;
                if (!skipBaseUpdate) base.Update();
            }
        }

        struct RepeatCase
        {
            public string name;
            public float size, value, at;
            /// "point": ClickRepeat(Vector2, Camera); "event-all", "event-position",
            /// "event-raycast", "event-press": ClickRepeat(PointerEventData) with every position
            /// field, or only position, pointerPressRaycast.screenPosition or pressPosition, set.
            public string overload;
            /// "idle": no button down; "heldOnHandle": the button pressed on the handle and held
            /// for the first 4 steps, then released; "heldOnTrack": the same, pressed on the track
            /// at 0.75.
            public string state;
            /// "hand": MoveNext once a frame; "coroutine": StartCoroutine on the scrollbar.
            public bool coroutine;
        }

        static RepeatCase R(string name, float value, float at, string overload, string state = "idle", bool coroutine = false)
            => new RepeatCase { name = name, size = 0.2f, value = value, at = at, overload = overload, state = state, coroutine = coroutine };

        static string Cur(object o) => o == null ? "null" : o.GetType().FullName;

        IEnumerator SubclassCases(GameObject cgo, ScriptedInput input, Vector2 centre)
        {
            Vector2 At(float f) => centre + new Vector2((f - 0.5f) * 400f, 0f);
            var es = UnityEngine.EventSystems.EventSystem.current;
            var repeats = new List<RepeatCase>
            {
                R("cr.point.off-right", 0f, 0.9f, "point"),
                R("cr.point.off-left", 1f, 0.1f, "point"),
                R("cr.point.on-handle", 0f, 0.1f, "point"),
                R("cr.point.off-right.coroutine", 0f, 0.9f, "point", coroutine: true),
                R("cr.point.on-handle.coroutine", 0f, 0.1f, "point", coroutine: true),
                R("cr.point.off-right.held", 0.5f, 0.9f, "point", "heldOnHandle"),
                R("cr.point.off-right.held.coroutine", 0.5f, 0.9f, "point", "heldOnHandle", true),
                R("cr.event-all.off-right", 0f, 0.9f, "event-all"),
                R("cr.event-all.off-left", 1f, 0.1f, "event-all"),
                R("cr.event-all.on-handle", 0f, 0.1f, "event-all"),
                R("cr.event-position.off-right", 0f, 0.9f, "event-position"),
                R("cr.event-raycast.off-right", 0f, 0.9f, "event-raycast"),
                R("cr.event-press.off-right", 0f, 0.9f, "event-press"),
                R("cr.event-all.off-right.coroutine", 0f, 0.9f, "event-all", coroutine: true),
                R("cr.event-all.off-right.held", 0.5f, 0.9f, "event-all", "heldOnHandle"),
                R("cr.event-all.off-right.held.coroutine", 0.5f, 0.9f, "event-all", "heldOnHandle", true),
                R("cr.point.off-left.heldtrack", 0f, 0.1f, "point", "heldOnTrack"),
                R("cr.event-all.off-left.heldtrack", 0f, 0.1f, "event-all", "heldOnTrack"),
                R("cr.point.off-left.heldtrack.coroutine", 0f, 0.1f, "point", "heldOnTrack", true),
            };
            if (Part == "clickrepeat2" || Part == "")
            {
                yield return StartCoroutine(HeldCases(cgo, input, centre));
                if (Part != "") yield break;
            }
            foreach (var c in repeats)
            {
                var bar = NewProbeBar(cgo, c.size, c.value, out var hrt, out var track);
                string label = c.name;
                bar.onValueChanged.AddListener(v => L(label + " changed " + F(v)));
                yield return null;
                yield return null;
                string State() => "value=" + F(bar.value) + " handle=" + V(hrt.anchorMin) + ".." + V(hrt.anchorMax);
                L(c.name + " setup overload=" + c.overload + " state=" + c.state + " at=" + F(c.at) + " " + State());
                if (c.state != "idle")
                {
                    // On the handle (0.4..0.6 at value 0.5), or on the track at 0.75 (value 0).
                    input.pos = At(c.state == "heldOnTrack" ? 0.75f : 0.5f); input.pressed = true; input.down = true; input.released = false;
                    yield return null;
                    input.pressed = false;
                    L(c.name + " pressed " + State());
                }
                var pt = At(c.at);
                IEnumerator it;
                if (c.overload == "point") it = bar.RepeatFromPoint(pt, null);
                else
                {
                    var e = new PointerEventData(es) { button = PointerEventData.InputButton.Left };
                    if (c.overload == "event-all" || c.overload == "event-position") e.position = pt;
                    if (c.overload == "event-all" || c.overload == "event-press") e.pressPosition = pt;
                    if (c.overload == "event-all" || c.overload == "event-raycast") e.pointerPressRaycast = new RaycastResult { screenPosition = pt, gameObject = track };
                    it = bar.RepeatFromEvent(e);
                }
                L(c.name + " created " + State());
                if (c.coroutine)
                {
                    int n = 0;
                    var co = bar.StartCoroutine(Counting(it, c.name, () => State(), () => ++n));
                    L(c.name + " started coroutine=" + (co == null ? "null" : "set") + " " + State());
                    for (int f = 1; f <= 12; f++)
                    {
                        if (c.state != "idle" && f == 5) { input.down = false; input.released = true; }
                        else input.released = false;
                        yield return null;
                        L(c.name + " frame" + f + " movenexts=" + n + " " + State());
                    }
                    if (co != null) bar.StopCoroutine(co);
                }
                else
                {
                    for (int k = 1; k <= 10; k++)
                    {
                        if (c.state != "idle" && k == 5) { input.down = false; input.released = true; yield return null; input.released = false; L(c.name + " released " + State()); }
                        bool more = Step(it, c.name + " step" + k, State);
                        L(c.name + " step" + k + " movenext=" + more + " current=" + (more ? Cur(it.Current) : "-") + " " + State());
                        if (!more) break;
                        yield return null;
                        L(c.name + " step" + k + " nextframe " + State());
                    }
                }
                input.down = false; input.released = false; input.pos = new Vector2(-50f, -50f);
                yield return null;
                Destroy(track);
                yield return null;
            }

            // Update: whether a subclass that does not call base.Update changes what a press on
            // the track, or a value set from code, does.
            foreach (bool skip in new[] { false, true })
            {
                string name = "update.skipbase=" + skip;
                var bar = NewProbeBar(cgo, 0.2f, 0f, out var hrt, out var track);
                bar.skipBaseUpdate = skip;
                bar.onValueChanged.AddListener(v => L(name + " changed " + F(v)));
                yield return null;
                yield return null;
                string State() => "value=" + F(bar.value) + " handle=" + V(hrt.anchorMin) + ".." + V(hrt.anchorMax) + " updates=" + bar.updates;
                L(name + " setup " + State());
                bar.value = 0.5f;
                L(name + " set0.5 " + State());
                yield return null;
                L(name + " set0.5 nextframe " + State());
                bar.size = 0.4f;
                L(name + " size0.4 " + State());
                yield return null;
                L(name + " size0.4 nextframe " + State());
                bar.size = 0.2f;
                bar.value = 0f;
                yield return null;
                L(name + " reset " + State());
                for (int i = 0; i < 7; i++)
                {
                    string phase;
                    if (i == 0) { input.pos = At(0.9f); input.pressed = true; input.down = true; phase = "press"; }
                    else if (i <= 3) { input.pressed = false; phase = "held" + i; }
                    else if (i == 4) { input.down = false; input.released = true; phase = "release"; }
                    else { input.released = false; phase = "after" + (i - 4); }
                    yield return null;
                    L(name + " " + phase + " " + State());
                }
                input.pos = new Vector2(-50f, -50f);
                yield return null;
                Destroy(track);
                yield return null;
            }
        }

        /// PROBE_SB_PART=clickrepeat2: a press held on the scrollbar (size 0.2), then one of: the
        /// value set from code while held; ClickRepeat with a PointerEventData carrying only one
        /// position field; ClickRepeat after the pointer has started a drag; ClickRepeat at a point
        /// on the handle; ClickRepeat after an earlier press was released.
        IEnumerator HeldCases(GameObject cgo, ScriptedInput input, Vector2 centre)
        {
            Vector2 At(float f) => centre + new Vector2((f - 0.5f) * 400f, 0f);
            var es = UnityEngine.EventSystems.EventSystem.current;
            // name, start value, press at, action
            var cases = new List<string[]>
            {
                new[] { "held.track.set0.2", "0", "0.75", "set0.2" },
                new[] { "held.handle.set0.45", "0.5", "0.5", "set0.45" },
                new[] { "held.handle.set0", "0.5", "0.5", "set0" },
                new[] { "held.track.event-position", "0", "0.75", "event-position" },
                new[] { "held.track.event-raycast", "0", "0.75", "event-raycast" },
                new[] { "held.track.event-press", "0", "0.75", "event-press" },
                // The same at 0.9 (an unset field is the screen's corner, left of the track).
                new[] { "held.track.event-position.right", "0", "0.5", "event-position" },
                new[] { "held.track.event-raycast.right", "0", "0.5", "event-raycast" },
                new[] { "held.track.event-press.right", "0", "0.5", "event-press" },
                new[] { "held.track.point-on-handle", "0", "0.75", "point-on-handle" },
                new[] { "held.track.dragging.point", "0", "0.75", "drag-point" },
                new[] { "released.point", "0", "0.75", "released-point" },
            };
            foreach (var c in cases)
            {
                string name = c[0], action = c[3];
                float start = float.Parse(c[1], Inv), at = float.Parse(c[2], Inv);
                var bar = NewProbeBar(cgo, 0.2f, start, out var hrt, out var track);
                bar.onValueChanged.AddListener(v => L(name + " changed " + F(v)));
                yield return null;
                yield return null;
                string State() => "value=" + F(bar.value) + " handle=" + V(hrt.anchorMin) + ".." + V(hrt.anchorMax);
                L(name + " setup " + State());
                input.pos = At(at); input.pressed = true; input.down = true; input.released = false;
                yield return null;
                input.pressed = false;
                L(name + " pressed " + State());
                yield return null;
                L(name + " held1 " + State());
                if (action == "released-point")
                {
                    input.down = false; input.released = true;
                    yield return null;
                    input.released = false;
                    L(name + " released " + State());
                    yield return null;
                }
                if (action == "drag-point")
                {
                    input.pos = At(0.3f);
                    yield return null;
                    L(name + " moved0.3 " + State());
                }
                IEnumerator it = null;
                if (action.StartsWith("set"))
                {
                    bar.value = action == "set0.2" ? 0.2f : action == "set0.45" ? 0.45f : 0f;
                    L(name + " " + action + " " + State());
                }
                else if (action.StartsWith("event-"))
                {
                    var pt = At(name.EndsWith(".right") ? 0.9f : 0.1f);
                    var e = new PointerEventData(es) { button = PointerEventData.InputButton.Left };
                    if (action == "event-position") e.position = pt;
                    if (action == "event-press") e.pressPosition = pt;
                    if (action == "event-raycast") e.pointerPressRaycast = new RaycastResult { screenPosition = pt, gameObject = track };
                    it = bar.RepeatFromEvent(e);
                }
                else if (action == "point-on-handle") it = bar.RepeatFromPoint(At(at), null);
                else it = bar.RepeatFromPoint(At(0.9f), null);
                for (int k = 1; k <= 4; k++)
                {
                    if (it != null)
                    {
                        bool more = Step(it, name + " step" + k, State);
                        L(name + " step" + k + " movenext=" + more + " current=" + (more ? Cur(it.Current) : "-") + " " + State());
                        if (!more) it = null;
                    }
                    yield return null;
                    L(name + " frame" + k + " " + State());
                }
                input.down = false; input.released = true;
                yield return null;
                input.released = false;
                L(name + " release " + State());
                if (it != null)
                {
                    bool more = Step(it, name + " after-release", State);
                    L(name + " after-release movenext=" + more + " " + State());
                }
                yield return null;
                L(name + " after1 " + State());
                input.pos = new Vector2(-50f, -50f);
                yield return null;
                Destroy(track);
                yield return null;
            }
        }

        /// MoveNext, logging an exception it throws (then false) with the state after it.
        static bool Step(IEnumerator it, string label, System.Func<string> state)
        {
            try
            {
                return it.MoveNext();
            }
            catch (System.Exception e)
            {
                L(label + " threw " + e.GetType().FullName + ": " + e.Message + " " + state());
                return false;
            }
        }

        static IEnumerator Counting(IEnumerator inner, string label, System.Func<string> state, System.Func<int> count)
        {
            while (true)
            {
                int n = count();
                bool more = Step(inner, label + " co movenext" + n, state);
                L(label + " co movenext" + n + "=" + more + " current=" + (more ? Cur(inner.Current) : "-") + " t=" + F(Time.unscaledTime) + " " + state());
                if (!more) yield break;
                yield return inner.Current;
            }
        }

        static ProbeScrollbar NewProbeBar(GameObject cgo, float size, float value, out RectTransform hrt, out GameObject track)
        {
            track = new GameObject("Track", typeof(RectTransform));
            track.transform.SetParent(cgo.transform, false);
            var trt = (RectTransform)track.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(400f, 20f);
            trt.anchoredPosition = Vector2.zero;
            track.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.3f);
            var area = new GameObject("Sliding Area", typeof(RectTransform));
            area.transform.SetParent(track.transform, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.sizeDelta = Vector2.zero;
            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(area.transform, false);
            hrt = (RectTransform)handle.transform;
            hrt.sizeDelta = Vector2.zero;
            handle.AddComponent<Image>().color = Color.white;
            var bar = track.AddComponent<ProbeScrollbar>();
            bar.handleRect = hrt;
            bar.size = size;
            bar.value = value;
            return bar;
        }
    }
}
