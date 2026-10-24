// Where TMP_InputField leaves its caret when an edit ends, and whether its caret graphic is culled
// under a RectMask2D ("PROBE ... IC ..."), at the main menu. With PROBE_SCENARIO=inputcaret only
// this probe runs.
//
// Part "end": a KInputTextField built as Klei's NumberInputField is (as InputText.cs builds it,
// 120 wide) gets a text by typing (ProcessEvent), by typing after Ctrl+A and Backspace, by
// typing and then Shift+Home (a selection), or from code while focused or before focus. The edit
// is then ended by Return (ProcessEvent), by OnSubmit, by DeactivateInputField(),
// DeactivateInputField(true), deselection or disabling the GameObject; with resetOnDeActivation
// and onFocusSelectAll off and on; and with listeners that do what Klei's KInputField does on
// onEndEdit (set the text again, then DeactivateInputField at the end of the frame). Each step
// logs the text, caretPosition, the selection's anchor and focus, stringPosition and isFocused;
// each event logs the caret it was sent with. After the end the field is activated again and a
// "9" is typed, which shows where the caret really was.
//
// Part "x": the same with real key presses, sent to the player's window through XTest (as
// scripts/e2e.py types), so the keys reach the field through Event.PopEvent in
// OnUpdateSelected and the input module.
//
// Part "cull": the caret graphic (the viewport's child "Caret") of fields that were never
// focused, and of focused ones, under the field's own RectMask2D and under an outer one, with
// the caret left at its defaults (100 x 100 at the world origin: AssignPositioningIfNeeded
// skipped, as with PLib's patch when the field is hidden in the frame it was shown) or placed
// over the text. It logs the caret's CanvasRenderer.cull, hasRectClipping, its rect and mesh,
// beside an Image placed at the same rect as a control, through frames, a moved mask, enable
// cycles and direct calls of Cull.
#pragma warning disable CS0618 // enableKerning, as SetTextStyleSetting.ApplyStyle sets it
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class InputCaret : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static TMP_FontAsset font;
        static Canvas canvas;
        static readonly List<string> events = new List<string>();
        static bool skipAssign;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static void L(string s) => Rec.Log("IC " + s);
        static string F(float v) { var s = Math.Round(v, 2).ToString("0.##", Inv); return s == "-0" ? "0" : s; }

        static string Esc(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char ch in s) sb.Append(ch >= 0x20 && ch < 0x7F && ch != '"' ? ch.ToString() : "\\u" + ((int)ch).ToString("X4"));
            return sb.Append('"').ToString();
        }

        public static void Install(HarmonyLib.Harmony harmony)
        {
            var m = HarmonyLib.AccessTools.Method(typeof(TMP_InputField), "AssignPositioningIfNeeded");
            L("hook AssignPositioningIfNeeded " + (m == null ? "absent" : "found"));
            if (m != null) harmony.Patch(m, prefix: new HarmonyLib.HarmonyMethod(typeof(InputCaret), nameof(AssignPrefix)));
        }

        static bool AssignPrefix() => !skipAssign;

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        // ----- Fields -----

        static KInputTextField Make(Transform parent, string name, bool active, float width = 120)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(width, 24);
            go.AddComponent<Image>();
            var f = go.AddComponent<KInputTextField>();
            var vp = new GameObject("Text Area", typeof(RectTransform));
            var vrt = (RectTransform)vp.transform;
            vrt.SetParent(rt, false);
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.sizeDelta = Vector2.zero;
            vp.AddComponent<RectMask2D>();
            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(vrt, false);
            var t = tgo.AddComponent<TextMeshProUGUI>();
            t.font = font; t.fontSize = 14; t.enableKerning = true; t.extraPadding = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var trt = t.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.sizeDelta = new Vector2(-3, -1); trt.anchoredPosition = new Vector2(1.5f, -0.5f);
            f.textViewport = vrt;
            f.textComponent = t;
            f.onFocusSelectAll = false;
            f.onValueChanged.AddListener(v => events.Add("changed:" + Esc(v) + "@" + Pos(f)));
            f.onEndEdit.AddListener(v => events.Add("endEdit:" + Esc(v) + "@" + Pos(f)));
            f.onSubmit.AddListener(v => events.Add("submit:" + Esc(v) + "@" + Pos(f)));
            f.onDeselect.AddListener(v => events.Add("deselect:" + Esc(v) + "@" + Pos(f)));
            if (active) go.SetActive(true);
            return f;
        }

        static string Pos(TMP_InputField f) => f.caretPosition + "/" + f.selectionAnchorPosition + "/" + f.selectionFocusPosition + "/" + f.stringPosition + (f.isFocused ? "F" : "-");

        static void Log(string tag, string phase, TMP_InputField f)
        {
            var ev = events.Count == 0 ? "-" : string.Join(",", events.ToArray());
            events.Clear();
            var sel = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == f.gameObject;
            L("end " + tag + " " + phase + " text=" + Esc(f.text) + " caret=" + f.caretPosition + " anchor=" + f.selectionAnchorPosition
              + " focus=" + f.selectionFocusPosition + " string=" + f.stringPosition + "/" + f.selectionStringAnchorPosition
              + " focused=" + f.isFocused + " selected=" + sel + " ev=" + ev);
        }

        static Event Key(KeyCode code, char ch, EventModifiers mods = EventModifiers.None)
            => new Event { type = EventType.KeyDown, keyCode = code, character = ch, modifiers = mods };

        static void TypeText(TMP_InputField f, string s)
        {
            foreach (char ch in s) f.ProcessEvent(Key(KeyCode.None, ch));
            f.ForceLabelUpdate();
        }

        static IEnumerator Focus(TMP_InputField f)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(f.gameObject);
            f.ActivateInputField();
            yield return null;
            yield return null;
        }

        // What KInputField does: onEndEdit, while active, sets the text again (KNumberInputField
        // through float.Parse and ToString) and deactivates the field at the end of the frame.
        static void Klei(MonoBehaviour host, TMP_InputField f, Func<string, string> value)
        {
            f.onEndEdit.AddListener(v =>
            {
                if (!f.gameObject.activeInHierarchy) { f.DeactivateInputField(); return; }
                f.text = value(v);
                events.Add("klei-set:" + Esc(f.text) + "@" + Pos(f));
                host.StartCoroutine(KleiEnd(f));
            });
        }

        static IEnumerator KleiEnd(TMP_InputField f)
        {
            yield return new WaitForEndOfFrame();
            f.DeactivateInputField();
            events.Add("klei-deactivated@" + Pos(f));
        }

        static string Number(string v)
        {
            float n;
            return float.TryParse(v == "" ? "0" : v, NumberStyles.Float, Inv, out n) ? n.ToString(Inv) : v;
        }

        IEnumerator Run()
        {
            var cgo = new GameObject("IcCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            cgo.AddComponent<GraphicRaycaster>();
            yield return null;
            font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(x => x.name == "NotoSans-Regular");
            if (font == null) { L("font missing"); yield break; }
            L("eventsystem " + (UnityEngine.EventSystems.EventSystem.current == null ? "null" : UnityEngine.EventSystems.EventSystem.current.name) + " screen " + Screen.width + "x" + Screen.height);
            var part = Environment.GetEnvironmentVariable("PROBE_IC_PART") ?? "all";

            // The game's own fields, as loaded at the main menu: their settings.
            foreach (var g in Resources.FindObjectsOfTypeAll<TMP_InputField>().OrderBy(x => x.name).Take(60))
                L("settings " + g.GetType().Name + " " + Esc(g.name) + " selectAll=" + g.onFocusSelectAll + " reset=" + g.resetOnDeActivation
                  + " restore=" + g.restoreOriginalTextOnEscape + " line=" + g.lineType + " activateOnSelect=" + g.shouldActivateOnSelect);
            var probe = Make(cgo.transform, "defaults", true);
            L("defaults selectAll=" + probe.onFocusSelectAll + "(set off by Make) reset=" + probe.resetOnDeActivation + " restore=" + probe.restoreOriginalTextOnEscape
              + " line=" + probe.lineType + " activateOnSelect=" + probe.shouldActivateOnSelect + " blink=" + F(probe.caretBlinkRate));
            Destroy(probe.gameObject);
            yield return null;

            if (part == "all" || part == "end") yield return StartCoroutine(EndCases(cgo.transform));
            if (part == "all" || part == "x") yield return StartCoroutine(XCases(cgo.transform));
            if (part == "all" || part == "cull") yield return StartCoroutine(CullCases(cgo.transform));
            Destroy(cgo);
            L("=== done");
        }

        // ----- Part "end" -----

        IEnumerator EndCases(Transform root)
        {
            var inputs = new[] { "typed", "typed-clear", "typed-sel", "code-focused", "code-before" };
            var ends = new[] { "return", "submit", "deactivate", "deactivate-clear", "deselect", "disable" };
            foreach (var reset in new[] { false, true })
                foreach (var selectAll in new[] { false, true })
                    foreach (var input in inputs)
                        foreach (var end in ends)
                            foreach (var value in new[] { "500", "50000" })
                            {
                                if (value == "50000" && (reset || selectAll)) continue;
                                yield return StartCoroutine(EndCase(root, input, end, value, reset, selectAll, null));
                            }
            // Klei's KInputField on onEndEdit: the same text again, a shorter one, a longer one.
            foreach (var reset in new[] { false, true })
                foreach (var input in new[] { "typed", "typed-clear", "code-focused" })
                    foreach (var end in new[] { "return", "submit", "deactivate", "deselect" })
                        foreach (var klei in new[] { "same", "shorter", "longer", "number" })
                            yield return StartCoroutine(EndCase(root, input, end, "500", reset, false, klei));
        }

        IEnumerator EndCase(Transform root, string input, string end, string value, bool reset, bool selectAll, string klei)
        {
            string tag = input + " " + end + " " + Esc(value) + " reset=" + reset + " selectAll=" + selectAll + " klei=" + (klei ?? "none");
            var f = Make(root, "Field", true);
            f.resetOnDeActivation = reset;
            f.onFocusSelectAll = selectAll;
            if (klei != null)
                Klei(this, f, v => klei == "same" ? v : klei == "shorter" ? "5" : klei == "longer" ? v + "123" : Number(v + ".0"));
            yield return null;
            if (input == "typed-clear" || input == "code-before") f.text = input == "typed-clear" ? "1000" : value;
            events.Clear();
            yield return StartCoroutine(Focus(f));
            Log(tag, "focused", f);
            if (input == "typed" || input == "typed-sel") TypeText(f, value);
            else if (input == "typed-clear")
            {
                f.ProcessEvent(Key(KeyCode.A, 'a', EventModifiers.Control));
                Log(tag, "ctrl-a", f);
                f.ProcessEvent(Key(KeyCode.Backspace, '\0'));
                TypeText(f, value);
            }
            else if (input == "code-focused") f.text = value;
            if (input == "typed-sel") f.ProcessEvent(Key(KeyCode.Home, '\0', EventModifiers.Shift));
            Log(tag, "input", f);
            yield return null;
            Log(tag, "input+1", f);
            switch (end)
            {
                case "return": f.ProcessEvent(Key(KeyCode.Return, '\n')); break;
                case "submit": f.OnSubmit(new BaseEventData(UnityEngine.EventSystems.EventSystem.current)); break;
                case "deactivate": f.DeactivateInputField(); break;
                case "deactivate-clear": f.DeactivateInputField(true); break;
                case "deselect": UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null); break;
                case "disable": f.gameObject.SetActive(false); break;
            }
            Log(tag, "end", f);
            yield return null;
            Log(tag, "end+1", f);
            if (end == "disable") { f.gameObject.SetActive(true); Log(tag, "enabled", f); }
            yield return null;
            Log(tag, "end+2", f);
            // Where the caret was: activate again and type a 9.
            if (klei != null) f.onEndEdit.RemoveAllListeners();
            yield return StartCoroutine(Focus(f));
            Log(tag, "again", f);
            TypeText(f, "9");
            Log(tag, "nine", f);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            Destroy(f.gameObject);
            events.Clear();
            yield return null;
        }

        // ----- Part "x": real keys through XTest -----

        [DllImport("libX11.so.6")] static extern IntPtr XOpenDisplay(IntPtr name);
        [DllImport("libX11.so.6")] static extern IntPtr XDefaultRootWindow(IntPtr dpy);
        [DllImport("libX11.so.6")] static extern int XQueryTree(IntPtr dpy, IntPtr w, out IntPtr root, out IntPtr parent, out IntPtr children, out uint n);
        [DllImport("libX11.so.6")] static extern int XGetWindowAttributes(IntPtr dpy, IntPtr w, int[] attributes);
        [DllImport("libX11.so.6")] static extern int XSetInputFocus(IntPtr dpy, IntPtr w, int revert, IntPtr time);
        [DllImport("libX11.so.6")] static extern byte XKeysymToKeycode(IntPtr dpy, IntPtr keysym);
        [DllImport("libX11.so.6")] static extern int XFlush(IntPtr dpy);
        [DllImport("libX11.so.6")] static extern int XSync(IntPtr dpy, int discard);
        [DllImport("libX11.so.6")] static extern int XFree(IntPtr p);
        [DllImport("libXtst.so.6")] static extern int XTestFakeKeyEvent(IntPtr dpy, uint keycode, int down, IntPtr delay);
        [DllImport("libXtst.so.6")] static extern int XTestFakeMotionEvent(IntPtr dpy, int screen, int x, int y, IntPtr delay);

        static IntPtr dpy;

        static bool XOpen()
        {
            try
            {
                dpy = XOpenDisplay(IntPtr.Zero);
                if (dpy == IntPtr.Zero) { L("x no display"); return false; }
                IntPtr r, p, children; uint n;
                XQueryTree(dpy, XDefaultRootWindow(dpy), out r, out p, out children, out n);
                IntPtr best = IntPtr.Zero; long area = 0;
                var attrs = new int[64];
                for (int i = 0; i < n; i++)
                {
                    var w = Marshal.ReadIntPtr(children, i * IntPtr.Size);
                    if (XGetWindowAttributes(dpy, w, attrs) == 0) continue;
                    // map_state of XWindowAttributes on x86-64 is the int at index 23.
                    long a = (long)attrs[2] * attrs[3];
                    if (attrs[23] == 2 && a > area) { best = w; area = a; }
                }
                if (n > 0) XFree(children);
                if (best == IntPtr.Zero) { L("x no window"); return false; }
                XSetInputFocus(dpy, best, 1, IntPtr.Zero);
                XSync(dpy, 0);
                L("x focused window " + best.ToInt64() + " area " + area);
                return true;
            }
            catch (Exception e) { L("x unavailable " + e.GetType().Name + " " + e.Message); return false; }
        }

        static void XKey(long keysym, bool down)
        {
            XTestFakeKeyEvent(dpy, XKeysymToKeycode(dpy, new IntPtr(keysym)), down ? 1 : 0, IntPtr.Zero);
            XFlush(dpy);
        }

        static IEnumerator Press(long keysym, long modifier = 0)
        {
            if (modifier != 0) { XKey(modifier, true); yield return null; yield return null; }
            XKey(keysym, true);
            yield return null; yield return null;
            XKey(keysym, false);
            yield return null; yield return null;
            if (modifier != 0) { XKey(modifier, false); yield return null; yield return null; }
        }

        IEnumerator XCases(Transform root)
        {
            if (!XOpen()) yield break;
            for (int i = 0; i < 4; i++) yield return null;
            L("x isFocused=" + Application.isFocused);
            foreach (var reset in new[] { false, true })
                foreach (var input in new[] { "typed", "typed-clear", "typed-sel" })
                    foreach (var end in new[] { "return", "deselect", "escape" })
                        foreach (var klei in new[] { null, "number" })
                            foreach (var value in new[] { "500", "50000" })
                            {
                                if (value == "50000" && (reset || input == "typed-sel" || end == "escape")) continue;
                                yield return StartCoroutine(XCase(root, input, end, value, reset, klei));
                            }
        }

        IEnumerator XCase(Transform root, string input, string end, string value, bool reset, string klei)
        {
            string tag = "x " + input + " " + end + " " + Esc(value) + " reset=" + reset + " klei=" + (klei ?? "none");
            var f = Make(root, "Field", true);
            f.resetOnDeActivation = reset;
            if (klei != null) Klei(this, f, v => Number(v + ".0"));
            yield return null;
            if (input == "typed-clear") f.text = "1000";
            events.Clear();
            yield return StartCoroutine(Focus(f));
            Log(tag, "focused", f);
            if (input == "typed-clear")
            {
                yield return StartCoroutine(Press('a', 0xffe3));
                Log(tag, "ctrl-a", f);
                yield return StartCoroutine(Press(0xff08));
            }
            foreach (char ch in value) yield return StartCoroutine(Press(ch));
            if (input == "typed-sel") yield return StartCoroutine(Press(0xff50, 0xffe1));
            Log(tag, "input", f);
            if (end == "return") yield return StartCoroutine(Press(0xff0d));
            else if (end == "escape") yield return StartCoroutine(Press(0xff1b));
            else UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            Log(tag, "end", f);
            yield return null;
            Log(tag, "end+1", f);
            yield return null;
            Log(tag, "end+2", f);
            if (klei != null) f.onEndEdit.RemoveAllListeners();
            yield return StartCoroutine(Focus(f));
            Log(tag, "again", f);
            yield return StartCoroutine(Press('9'));
            Log(tag, "nine", f);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            Destroy(f.gameObject);
            events.Clear();
            yield return null;
        }

        // ----- Part "cull" -----

        static Graphic CaretOf(TMP_InputField f)
        {
            if (f.textViewport == null) return null;
            foreach (Transform c in f.textViewport)
            {
                var g = c.GetComponent<Graphic>();
                if (g != null && g.GetType().Name == "TMP_SelectionCaret") return g;
            }
            return null;
        }

        static string Box(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var a = canvas.transform.InverseTransformPoint(c[0]);
            var b = canvas.transform.InverseTransformPoint(c[2]);
            return F(a.x) + "," + F(a.y) + ".." + F(b.x) + "," + F(b.y);
        }

        static System.Reflection.MethodInfo getMesh = typeof(CanvasRenderer).GetMethod("GetMesh", Type.EmptyTypes);

        static string Mesh(CanvasRenderer cr)
        {
            if (getMesh == null) return "n/a";
            try
            {
                var m = getMesh.Invoke(cr, null) as Mesh;
                if (m == null) return "null";
                if (m.vertexCount == 0) return "0";
                var b = m.bounds;
                return m.vertexCount + "[" + F(b.min.x) + "," + F(b.min.y) + ".." + F(b.max.x) + "," + F(b.max.y) + "]";
            }
            catch (Exception e) { return "err:" + e.GetType().Name; }
        }

        static string G(Graphic g)
        {
            if (g == null) return "none";
            var cr = g.canvasRenderer;
            return "cull=" + cr.cull + " clip=" + cr.hasRectClipping + " rect=" + Box(g.rectTransform) + " mesh=" + Mesh(cr)
                   + " active=" + g.gameObject.activeInHierarchy + " enabled=" + g.enabled + " maskable=" + ((g as MaskableGraphic)?.maskable.ToString() ?? "-")
                   + " anchors=" + F(g.rectTransform.anchorMin.x) + "," + F(g.rectTransform.anchorMin.y) + "-" + F(g.rectTransform.anchorMax.x) + "," + F(g.rectTransform.anchorMax.y)
                   + " size=" + F(g.rectTransform.sizeDelta.x) + "," + F(g.rectTransform.sizeDelta.y);
        }

        static void CLog(string tag, string phase, TMP_InputField f, Image control)
        {
            L("cull " + tag + " " + phase + " caret: " + G(CaretOf(f)) + " | text: " + G(f.textComponent as Graphic)
              + " | control: " + G(control) + " | focused=" + f.isFocused + " caretPos=" + f.caretPosition);
        }

        // A control Image with the caret's rect, its sibling under the same viewport.
        static Image Control(TMP_InputField f)
        {
            var caret = CaretOf(f);
            var go = new GameObject("Control", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(f.textViewport, false);
            var img = go.AddComponent<Image>();
            if (caret != null)
            {
                var c = caret.rectTransform;
                rt.anchorMin = c.anchorMin; rt.anchorMax = c.anchorMax; rt.pivot = c.pivot; rt.sizeDelta = c.sizeDelta;
                rt.position = c.position;
            }
            return img;
        }

        IEnumerator CullCases(Transform root)
        {
            // where: "own" (only the field's viewport mask; the caret outside it when at its
            // defaults), "outer" (the field inside a 300 x 100 RectMask2D, moved out of it).
            foreach (var placed in new[] { false, true })
                foreach (var where in new[] { "own", "outer-in", "outer-out" })
                    foreach (var focus in new[] { "never", "focused", "focused-ended" })
                        foreach (var built in new[] { "active", "inactive" })
                            yield return StartCoroutine(CullCase(root, placed, where, focus, built));
        }

        IEnumerator CullCase(Transform root, bool placed, string where, string focus, string built)
        {
            string tag = (placed ? "placed" : "defaults") + " " + where + " " + focus + " " + built;
            Transform parent = root;
            RectTransform outer = null;
            if (where != "own")
            {
                var ogo = new GameObject("Outer", typeof(RectTransform));
                outer = (RectTransform)ogo.transform;
                outer.SetParent(root, false);
                outer.sizeDelta = new Vector2(300, 100);
                outer.anchoredPosition = new Vector2(100, 50);
                ogo.AddComponent<RectMask2D>();
                parent = outer;
            }
            skipAssign = !placed;
            var f = Make(parent, "Field", false);
            var frt = (RectTransform)f.transform;
            frt.anchoredPosition = where == "outer-out" ? new Vector2(0, 200) : new Vector2(0, 0);
            if (built == "active") f.gameObject.SetActive(true);
            yield return null;
            if (built == "inactive") f.gameObject.SetActive(true);
            skipAssign = false;
            CLog(tag, "made", f, null);
            yield return null;
            var control = Control(f);
            CLog(tag, "frame1", f, control);
            yield return null;
            CLog(tag, "frame2", f, control);
            if (focus != "never")
            {
                yield return StartCoroutine(Focus(f));
                TypeText(f, "12");
                yield return null;
                CLog(tag, "focused", f, control);
                if (focus == "focused-ended")
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                    yield return null;
                    yield return null;
                    CLog(tag, "ended", f, control);
                }
            }
            // The mask moved (the clip rect changes): every clippable under it is culled again.
            var mask = (where == "own" ? f.textViewport : outer);
            mask.anchoredPosition += new Vector2(0, 1);
            yield return null;
            CLog(tag, "mask-moved", f, control);
            mask.anchoredPosition -= new Vector2(0, 1);
            yield return null;
            CLog(tag, "mask-back", f, control);
            // The field moved by 1.
            frt.anchoredPosition += new Vector2(1, 0);
            yield return null;
            CLog(tag, "field-moved", f, control);
            // The caret's own GameObject and graphic cycled.
            var caret = CaretOf(f);
            if (caret != null)
            {
                caret.gameObject.SetActive(false);
                caret.gameObject.SetActive(true);
                yield return null;
                CLog(tag, "caret-cycled", f, control);
                caret.enabled = false;
                caret.enabled = true;
                yield return null;
                CLog(tag, "caret-reenabled", f, control);
                caret.rectTransform.anchoredPosition += new Vector2(0, 1);
                yield return null;
                CLog(tag, "caret-moved", f, control);
            }
            f.gameObject.SetActive(false);
            f.gameObject.SetActive(true);
            yield return null;
            CLog(tag, "field-cycled", f, control);
            // Direct calls of Cull: a rect far away, one covering everything, and validRect false.
            if (caret != null)
            {
                var far = new Rect(5000, 5000, 10, 10);
                var all = new Rect(-5000, -5000, 10000, 10000);
                ((IClippable)caret).Cull(far, true); ((IClippable)control).Cull(far, true);
                CLog(tag, "Cull(far,true)", f, control);
                ((IClippable)caret).Cull(all, true); ((IClippable)control).Cull(all, true);
                CLog(tag, "Cull(all,true)", f, control);
                ((IClippable)caret).Cull(far, false); ((IClippable)control).Cull(far, false);
                CLog(tag, "Cull(far,false)", f, control);
                ((IClippable)caret).Cull(all, true); ((IClippable)control).Cull(all, true);
                yield return null;
                CLog(tag, "after-calls+1", f, control);
            }
            Destroy(where == "own" ? f.gameObject : outer.gameObject);
            yield return null;
        }
    }
}
