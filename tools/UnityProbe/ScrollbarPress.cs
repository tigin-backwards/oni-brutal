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
            foreach (var c in cases)
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
            module.inputOverride = before;
            Destroy(input);
            Destroy(cgo);
            L("done");
        }
    }
}
