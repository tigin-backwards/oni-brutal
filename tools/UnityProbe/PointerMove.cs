// Pointer-move events ("PROBE ... PM ..."), at the main menu: when the event system's input
// module sends IPointerMoveHandler.OnPointerMove, to which objects, in what order beside enter
// and exit, and with what event data. The pointer is scripted frame by frame through the current
// module's inputOverride (a BaseInput whose mouse position and left button follow a list of
// steps). The hierarchy, on an overlay canvas of its own:
//   P (Image 400x300, enter/exit/move)
//     A (Image 100x100, left; enter/exit/move)
//       A1 (Image 40x40 inside A; no handler)
//     B (Image 100x100, right; move only)
//     C (Image 100x100, bottom middle; no handler)
//     D (Image 100x100, top middle; move, raycastTarget false)
// With PROBE_SCENARIO=pointermove only this probe runs.
using System;
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
    public class PointerMove : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static string Step = "-";

        static void L(string s) => Rec.Log("PM " + s);
        static string V(Vector2 v) => v.x.ToString("R", Inv) + "," + v.y.ToString("R", Inv);

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

        public class Handler : MonoBehaviour
        {
            public bool enter, exit, move;
            void Log(string what, PointerEventData d)
                => L(Step + " " + name + " " + what + " pos=" + V(d.position) + " delta=" + V(d.delta) + " moving=" + d.IsPointerMoving()
                     + " enter=" + (d.pointerEnter != null ? d.pointerEnter.name : "null")
                     + " cur=" + (d.pointerCurrentRaycast.gameObject != null ? d.pointerCurrentRaycast.gameObject.name : "null")
                     + " hovered=[" + string.Join(",", d.hovered.Select(h => h != null ? h.name : "null")) + "]"
                     + " press=" + (d.pointerPress != null ? d.pointerPress.name : "null") + " drag=" + (d.pointerDrag != null ? d.pointerDrag.name : "null")
                     + " dragging=" + d.dragging + " used=" + d.used + " id=" + d.pointerId + " button=" + d.button);
            public void OnEnterLog(PointerEventData d) { if (enter) Log("enter", d); }
            public void OnExitLog(PointerEventData d) { if (exit) Log("exit", d); }
            public void OnMoveLog(PointerEventData d) { if (move) Log("move", d); }
        }

        public class EnterExitMove : Handler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
        {
            public void OnPointerEnter(PointerEventData d) => OnEnterLog(d);
            public void OnPointerExit(PointerEventData d) => OnExitLog(d);
            public void OnPointerMove(PointerEventData d) => OnMoveLog(d);
        }

        public class MoveOnly : Handler, IPointerMoveHandler
        {
            public void OnPointerMove(PointerEventData d) => OnMoveLog(d);
        }

        // A second move handler on the same object, to see the order and whether both run.
        public class MoveSecond : MonoBehaviour, IPointerMoveHandler
        {
            public void OnPointerMove(PointerEventData d) => L(Step + " " + name + " move2");
        }

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        static GameObject Box(string name, Transform parent, Vector2 pos, Vector2 size, bool raycast = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.2f);
            img.raycastTarget = raycast;
            return go;
        }

        IEnumerator Run()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            L("eventsystem " + (es != null ? es.name : "null") + " modules=[" + (es != null ? string.Join(",", es.GetComponents<BaseInputModule>().Select(m => m.GetType().FullName + (m.enabled ? "" : "(disabled)"))) : "") + "]"
              + " current=" + (es != null && es.currentInputModule != null ? es.currentInputModule.GetType().FullName : "null"));
            L("functor pointerMoveHandler=" + (ExecuteEvents.pointerMoveHandler != null));

            var cgo = new GameObject("PmCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32002;
            cgo.AddComponent<GraphicRaycaster>();
            var p = Box("P", cgo.transform, Vector2.zero, new Vector2(400f, 300f));
            var ph = p.AddComponent<EnterExitMove>(); ph.enter = ph.exit = ph.move = true;
            var a = Box("A", p.transform, new Vector2(-130f, 0f), new Vector2(100f, 100f));
            var ah = a.AddComponent<EnterExitMove>(); ah.enter = ah.exit = ah.move = true;
            a.AddComponent<MoveSecond>();
            Box("A1", a.transform, Vector2.zero, new Vector2(40f, 40f));
            var b = Box("B", p.transform, new Vector2(130f, 0f), new Vector2(100f, 100f));
            b.AddComponent<MoveOnly>().move = true;
            Box("C", p.transform, new Vector2(0f, -90f), new Vector2(100f, 100f));
            var d = Box("D", p.transform, new Vector2(0f, 90f), new Vector2(100f, 100f), raycast: false);
            d.AddComponent<MoveOnly>().move = true;
            var disabled = Box("E", p.transform, new Vector2(0f, 0f), new Vector2(60f, 60f));
            var eh = disabled.AddComponent<MoveOnly>(); eh.move = true; eh.enabled = false;
            yield return null;
            yield return null;

            var module = es != null ? es.currentInputModule : null;
            if (module == null) { L("no current module"); yield break; }
            var input = module.gameObject.AddComponent<ScriptedInput>();
            var before = module.inputOverride;
            module.inputOverride = input;
            // The screen centre is the canvas centre (overlay, pivot at the middle).
            var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            L("screen " + Screen.width + "x" + Screen.height + " scale=" + canvas.scaleFactor.ToString("R", Inv));
            var steps = new List<(string label, Vector2 pos, int mode)>
            {
                // mode: 0 up, 1 press this frame, 2 held, 3 release this frame
                ("out0", c + new Vector2(-600f, 0f), 0), ("out1", c + new Vector2(-600f, 0f), 0),
                ("Aenter", c + new Vector2(-130f, 30f), 0), ("Astill", c + new Vector2(-130f, 30f), 0), ("Astill2", c + new Vector2(-130f, 30f), 0),
                ("Amove", c + new Vector2(-125f, 30f), 0), ("Amove2", c + new Vector2(-120f, 31f), 0), ("Astill3", c + new Vector2(-120f, 31f), 0),
                ("A1", c + new Vector2(-130f, 0f), 0), ("A1move", c + new Vector2(-131f, 0f), 0),
                ("Pbare", c + new Vector2(-40f, 0f), 0), ("Pbare2", c + new Vector2(-41f, 0f), 0),
                ("E", c + new Vector2(10f, 0f), 0), ("Emove", c + new Vector2(11f, 0f), 0),
                ("Center", c + new Vector2(0f, -90f), 0), ("Cmove", c + new Vector2(2f, -90f), 0),
                ("Dover", c + new Vector2(0f, 90f), 0), ("Dmove", c + new Vector2(2f, 90f), 0),
                ("Benter", c + new Vector2(130f, 0f), 0), ("Bmove", c + new Vector2(131f, 0f), 0), ("Bstill", c + new Vector2(131f, 0f), 0),
                ("out2", c + new Vector2(600f, 0f), 0), ("out3", c + new Vector2(601f, 0f), 0),
                ("Apress", c + new Vector2(-130f, 30f), 1), ("Aheld", c + new Vector2(-130f, 30f), 2),
                ("Adrag1", c + new Vector2(-128f, 30f), 2), ("Adrag2", c + new Vector2(-100f, 30f), 2), ("dragP", c + new Vector2(-40f, 0f), 2),
                ("dragB", c + new Vector2(130f, 0f), 2), ("release", c + new Vector2(130f, 0f), 3), ("after", c + new Vector2(131f, 0f), 0),
                ("Areturn", c + new Vector2(-130f, 30f), 0), ("Adisable", c + new Vector2(-129f, 30f), 0), ("Ainactive", c + new Vector2(-128f, 30f), 0),
                ("Aback", c + new Vector2(-130f, 30f), 0), ("Aback2", c + new Vector2(-130f, 30f), 0),
                ("off", new Vector2(-50f, -50f), 0), ("offstill", new Vector2(-50f, -50f), 0), ("offmove", new Vector2(-60f, -60f), 0),
                ("fromoff", c + new Vector2(-130f, 30f), 0), ("fromoff2", c + new Vector2(-130f, 30f), 0),
                ("shiftA", c + new Vector2(-130f, 30f), 0), ("shifted", c + new Vector2(-130f, 30f), 0), ("shifted2", c + new Vector2(-130f, 30f), 0),
                ("unshift", c + new Vector2(-130f, 30f), 0), ("unshifted", c + new Vector2(-130f, 30f), 0),
                ("end", c + new Vector2(-600f, 0f), 0), ("end2", c + new Vector2(-600f, 0f), 0),
            };
            foreach (var s in steps)
            {
                Step = s.label;
                input.pos = s.pos;
                input.pressed = s.mode == 1;
                input.down = s.mode == 1 || s.mode == 2;
                input.released = s.mode == 3;
                if (s.label == "Adisable") ah.enabled = false;
                if (s.label == "Ainactive") { ah.enabled = true; a.SetActive(false); }
                if (s.label == "Aback") a.SetActive(true);
                // The pointer stays still while A moves away from under it (and back).
                if (s.label == "shiftA") ((RectTransform)a.transform).anchoredPosition = new Vector2(-130f, 400f);
                if (s.label == "unshift") ((RectTransform)a.transform).anchoredPosition = new Vector2(-130f, 0f);
                L(s.label + " f" + Time.frameCount + " at " + V(s.pos) + " mode=" + s.mode);
                yield return null;
            }
            a.SetActive(true);
            module.inputOverride = before;
            Destroy(input);
            Destroy(cgo);
            L("done");
        }
    }
}
