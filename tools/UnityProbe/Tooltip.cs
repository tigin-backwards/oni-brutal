// GUI.tooltip ("PROBE ... TT ..."), at the main menu. With PROBE_SCENARIO=tooltip only this
// probe runs.
//
// One behaviour draws IMGUI controls whose GUIContent carries a tooltip ("tA" for control A) or
// none: GUI.Button, Label, Box, Toggle, two overlapping buttons, a disabled button, GUILayout
// controls in an area, a button in a GUI.BeginGroup and two in a GUI.BeginScrollView (one
// scrolled out of view), and a button under a window. It opens GUI.Window 101 (buttons, a
// label without tooltip, a group, a window title with a tooltip, and a GUI.Window 103 called
// from inside its function), GUILayout.Window 102 (laid out as Customize Duplicant traits'
// window: buttons and labels, a GUILayout scroll view of labels with tooltips, then a read of
// GUI.tooltip) and GUI.Window 104 over part of 101. A second behaviour only reads.
//
// The mouse is moved over each control in turn through XTest (as InputCaret's part "x" types),
// at the centre of the control's screen rect as measured during Repaint. After it settles, two
// frames are recorded: every OnGUI call and window function logs one line per event, with
// GUI.tooltip read at its start, after each control and at its end; Update, LateUpdate and the
// end of the frame read it outside OnGUI. Then modes that set GUI.tooltip from code (at the
// start of OnGUI, at the end of its Repaint, inside window 101), that give a control keyboard
// focus, and presses of the mouse button over controls (held and dragged off).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SemanticsProbe
{
    public class Tooltip : MonoBehaviour
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static string Step = "-";
        internal static bool Recording;
        internal static string Mode = "plain";
        internal static bool Active;
        internal static readonly Dictionary<string, Rect> Rects = new Dictionary<string, Rect>();
        bool started;
        float sceneStart = -1f;

        static void L(string s) => Rec.Log("TT " + s);

        internal static string Q(string s) => s == null ? "null" : "'" + s.Replace("\n", "\\n") + "'";
        internal static string V(Vector2 v) => v.x.ToString("R", Inv) + "," + v.y.ToString("R", Inv);
        internal static string R(Rect r) => r.x.ToString("R", Inv) + "," + r.y.ToString("R", Inv) + "," + r.width.ToString("R", Inv) + "," + r.height.ToString("R", Inv);

        [MethodImpl(MethodImplOptions.NoInlining)] static string TipRaw() => GUI.tooltip;
        [MethodImpl(MethodImplOptions.NoInlining)] static void SetTipRaw(string s) => GUI.tooltip = s;
        [MethodImpl(MethodImplOptions.NoInlining)] static Rect TipRectRaw() => TipGui.Rect;

        internal static string Tip()
        {
            try { return Q(TipRaw()); }
            catch (Exception e) { return "<" + e.GetType().Name + ":" + e.Message + ">"; }
        }

        internal static string TipOrNull()
        {
            try { return TipRaw(); }
            catch (Exception) { return null; }
        }

        internal static string SetTip(string s)
        {
            try { SetTipRaw(s); return "ok"; }
            catch (Exception e) { return "<" + e.GetType().Name + ":" + e.Message + ">"; }
        }

        internal static string TipRect()
        {
            try { return R(TipRectRaw()); }
            catch (Exception e) { return "<" + e.GetType().Name + ">"; }
        }

        // GUI.tooltipRect is protected static; a subclass reads it.
        class TipGui : GUI
        {
            public static Rect Rect => tooltipRect;
        }

        // One OnGUI call or window function's line.
        internal class Line
        {
            readonly StringBuilder sb;
            int hot;
            string rect;
            public Line(string who)
            {
                var e = Event.current;
                sb = new StringBuilder();
                hot = GUIUtility.hotControl;
                rect = TipRect();
                sb.Append(Step).Append(' ').Append(Mode).Append(' ').Append(who).Append(' ').Append(e.type).Append(" raw=").Append(e.rawType)
                  .Append(" m=").Append(V(e.mousePosition)).Append(" kb=").Append(GUIUtility.keyboardControl).Append(" hot=").Append(hot)
                  .Append(" rect0=").Append(rect).Append(" start=").Append(Tip());
                // Mode "reset...": every call starts from "-", so a line shows what its own controls set.
                if (Mode.StartsWith("reset")) sb.Append(" set=").Append(SetTip("-")).Append(':').Append(Tip());
                if (Mode == "setnull") sb.Append(" set=").Append(SetTip(null)).Append(':').Append(Tip());
            }
            public void P(string name)
            {
                sb.Append(' ').Append(name).Append('=').Append(Tip());
                if (GUIUtility.hotControl != hot) { hot = GUIUtility.hotControl; sb.Append(" h=").Append(hot); }
                var r = TipRect();
                if (r != rect) { rect = r; sb.Append(" tr=").Append(r).Append("@").Append(V(Event.current.mousePosition)); }
            }
            public void Raw(string s) => sb.Append(' ').Append(s);
            public void End()
            {
                sb.Append(" end=").Append(Tip()).Append(" rect=").Append(TipRect());
                if (Recording) L(sb.ToString());
            }
        }

        internal static void Keep(string name, Rect r)
        {
            if (Event.current.type == EventType.Repaint) Rects[name] = GUIUtility.GUIToScreenRect(r);
        }

        void Awake()
        {
            gameObject.AddComponent<TooltipGui>();
            gameObject.AddComponent<TooltipGui2>();
            L("awake tip=" + Tip());
        }

        void Update()
        {
            if (Recording) L(Step + " " + Mode + " update tip=" + Tip());
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
            StartCoroutine(EndOfFrames());
        }

        void LateUpdate()
        {
            if (Recording) L(Step + " " + Mode + " late tip=" + Tip());
        }

        IEnumerator EndOfFrames()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                yield return wait;
                if (Recording) L(Step + " " + Mode + " eof tip=" + Tip());
            }
        }

        // ----- XTest -----

        [DllImport("libX11.so.6")] static extern IntPtr XOpenDisplay(IntPtr name);
        [DllImport("libX11.so.6")] static extern IntPtr XDefaultRootWindow(IntPtr dpy);
        [DllImport("libX11.so.6")] static extern int XQueryTree(IntPtr dpy, IntPtr w, out IntPtr root, out IntPtr parent, out IntPtr children, out uint n);
        [DllImport("libX11.so.6")] static extern int XGetWindowAttributes(IntPtr dpy, IntPtr w, int[] attributes);
        [DllImport("libX11.so.6")] static extern int XSetInputFocus(IntPtr dpy, IntPtr w, int revert, IntPtr time);
        [DllImport("libX11.so.6")] static extern int XFlush(IntPtr dpy);
        [DllImport("libX11.so.6")] static extern int XSync(IntPtr dpy, int discard);
        [DllImport("libX11.so.6")] static extern int XFree(IntPtr p);
        [DllImport("libXtst.so.6")] static extern int XTestFakeMotionEvent(IntPtr dpy, int screen, int x, int y, IntPtr delay);
        [DllImport("libXtst.so.6")] static extern int XTestFakeButtonEvent(IntPtr dpy, uint button, int down, IntPtr delay);

        static IntPtr dpy;
        static int offX, offY;

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
                    long a = (long)attrs[2] * attrs[3];
                    if (attrs[23] == 2 && a > area) { best = w; area = a; offX = attrs[0]; offY = attrs[1]; }
                }
                if (n > 0) XFree(children);
                if (best == IntPtr.Zero) { L("x no window"); return false; }
                XSetInputFocus(dpy, best, 1, IntPtr.Zero);
                XSync(dpy, 0);
                L("x window " + best.ToInt64() + " at " + offX + "," + offY + " area " + area);
                return true;
            }
            catch (Exception e) { L("x unavailable " + e.GetType().Name + " " + e.Message); return false; }
        }

        // A point in GUI coordinates of the screen (origin top left).
        static void Move(Vector2 gui)
        {
            XTestFakeMotionEvent(dpy, -1, offX + Mathf.RoundToInt(gui.x), offY + Mathf.RoundToInt(gui.y), IntPtr.Zero);
            XFlush(dpy);
        }

        static void Button(bool down)
        {
            XTestFakeButtonEvent(dpy, 1, down ? 1 : 0, IntPtr.Zero);
            XFlush(dpy);
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        Vector2 Centre(string name) => Rects.TryGetValue(name, out var r) ? r.center : new Vector2(1200f, 700f);

        IEnumerator At(string step, Vector2 p, int frames = 2)
        {
            Step = step;
            Move(p);
            yield return Frames(6);
            L(step + " " + Mode + " mouse gui=" + V(p) + " input=" + V(Input.mousePosition));
            Recording = true;
            yield return Frames(frames);
            Recording = false;
        }

        IEnumerator Run()
        {
            L("start tip=" + Tip());
            if (!XOpen()) yield break;
            Move(new Vector2(640f, 360f));
            yield return Frames(6);
            L("calibrate input=" + V(Input.mousePosition) + " screen=" + Screen.width + "x" + Screen.height);
            Active = true;
            yield return Frames(6);
            var sb = new StringBuilder("rects");
            foreach (var kv in Rects) sb.Append(' ').Append(kv.Key).Append('=').Append(R(kv.Value));
            L(sb.ToString());

            var none = new Vector2(1200f, 700f);
            var names = new[] { "A", "B", "L", "X", "T", "H", "C", "D", "Dgap", "E", "F", "G", "Z",
                                "W1a", "W1b", "W1c", "W1d", "W1g", "W2a", "W2b", "W2s0", "W2s2", "W2s5", "W2tail", "W4a" };
            // First without and then with the reset of every call to "-".
            foreach (var mode in new[] { "plain", "reset" })
            {
                Mode = mode;
                yield return At("none", none);
                foreach (var name in names) yield return At(name, Centre(name));
                var o1 = Rects["O1"]; var o2 = Rects["O2"];
                yield return At("O1only", new Vector2(o1.x + 10f, o1.center.y));
                yield return At("O1O2", new Vector2((o2.x + o1.xMax) / 2f, o1.center.y));
                yield return At("O2only", new Vector2(o2.xMax - 10f, o2.center.y));
                var d = Rects["W1d"]; var w4a = Rects["W4a"];
                yield return At("W1dW4a", new Vector2((d.x + d.xMax) / 2f, (Mathf.Max(d.y, w4a.y) + Mathf.Min(d.yMax, w4a.yMax)) / 2f));
                yield return At("W1title", new Vector2(Rects["w1"].center.x, Rects["w1"].y + 8f));
                yield return At("W1empty", new Vector2(Rects["w1"].x + 150f, Rects["w1"].yMax - 15f));
                yield return At("W2empty", new Vector2(Rects["w2"].xMax - 10f, Rects["w2"].yMax - 6f));
                yield return At("none2", none);
                yield return At("A", Centre("A"));
                yield return At("Aedge", new Vector2(Rects["A"].x + 1f, Rects["A"].y + 1f));
                yield return At("Aout", new Vector2(Rects["A"].x - 1f, Rects["A"].y - 1f));
                yield return At("L", Centre("L"));
                yield return At("none3", none);
            }
            Mode = "plain";

            // GUI.tooltip set from code.
            foreach (var mode in new[] { "setstart", "setend", "setwin", "setw2end", "setnull" })
            {
                foreach (var where in new[] { "none", "A", "W1a", "W2a" })
                {
                    Mode = mode;
                    yield return At(where, where == "none" ? none : Centre(where));
                    Mode = "plain";
                    Step = where + "-after-" + mode;
                    Recording = true;
                    yield return Frames(2);
                    Recording = false;
                }
            }

            // Keyboard focus on a control that has a tooltip (button A's id, and toggle T's).
            foreach (var mode in new[] { "focusA", "focusT", "resetfocusA", "resetfocusT" })
            {
                Mode = mode;
                yield return At("none", none);
                yield return At("B", Centre("B"));
                Mode = "plain";
            }
            yield return At("none-afterfocus", none);

            // Presses, with every call reset to "-": a click on A, a press held over A, dragged
            // off and released off; the same over L, W2a (focuses window 102) and W1a.
            Mode = "reset";
            foreach (var name in new[] { "A", "L", "W2a", "W1a", "W4a" })
            {
                Step = "click-" + name;
                Move(Centre(name));
                yield return Frames(6);
                Recording = true;
                Button(true);
                yield return Frames(3);
                Step = "click-up-" + name;
                Button(false);
                yield return Frames(3);
                Recording = false;
                yield return Frames(3);
                Step = "press-" + name;
                Recording = true;
                Button(true);
                yield return Frames(3);
                Step = "held-off-" + name;
                Move(none);
                yield return Frames(3);
                Step = "release-off-" + name;
                Button(false);
                yield return Frames(3);
                Recording = false;
                yield return Frames(3);
                yield return At("after-" + name, none);
            }
            sb = new StringBuilder("rects-after");
            foreach (var kv in Rects) sb.Append(' ').Append(kv.Key).Append('=').Append(R(kv.Value));
            L(sb.ToString());
            foreach (var name in new[] { "W1c", "W1d", "W4a", "A" })
                yield return At(name + "-after", Centre(name));
            Mode = "plain";
            L("done");
        }
    }

    // The behaviour with the controls and windows.
    public class TooltipGui : MonoBehaviour
    {
        Rect w1 = new Rect(400f, 20f, 300f, 250f);
        Rect w2 = new Rect(750f, 20f, 300f, 300f);
        Rect w3 = new Rect(50f, 600f, 200f, 80f);
        Rect w4 = new Rect(600f, 150f, 200f, 150f);
        Vector2 scroll, scroll2;
        bool tog;

        static GUIContent C(string text, string tip = null) => tip == null ? new GUIContent(text) : new GUIContent(text, tip);

        void OnGUI()
        {
            if (!Tooltip.Active) return;
            var line = new Tooltip.Line("gui");
            if (Tooltip.Mode == "setstart") line.Raw("set=" + Tooltip.SetTip("S") + ":" + Tooltip.Tip());
            Rect r;
            int pre = GUIUtility.GetControlID(FocusType.Passive);
            if (Tooltip.Mode == "focusA") GUIUtility.keyboardControl = pre + 1;
            GUI.Button(r = new Rect(20f, 20f, 100f, 30f), C("A", "tA")); line.P("A"); Tooltip.Keep("A", r);
            GUI.Button(r = new Rect(20f, 60f, 100f, 30f), C("B")); line.P("B"); Tooltip.Keep("B", r);
            GUI.Label(r = new Rect(20f, 100f, 100f, 30f), C("L", "tL")); line.P("L"); Tooltip.Keep("L", r);
            GUI.Box(r = new Rect(20f, 140f, 100f, 30f), C("X", "tX")); line.P("X"); Tooltip.Keep("X", r);
            int preT = GUIUtility.GetControlID(FocusType.Passive);
            if (Tooltip.Mode == "focusT") GUIUtility.keyboardControl = preT + 1;
            tog = GUI.Toggle(r = new Rect(140f, 20f, 100f, 30f), tog, C("T", "tT")); line.P("T"); Tooltip.Keep("T", r);
            GUI.Button(r = new Rect(140f, 100f, 100f, 30f), C("O1", "tO1")); line.P("O1"); Tooltip.Keep("O1", r);
            GUI.Button(r = new Rect(180f, 100f, 100f, 30f), C("O2", "tO2")); line.P("O2"); Tooltip.Keep("O2", r);
            GUI.enabled = false;
            GUI.Button(r = new Rect(140f, 140f, 100f, 30f), C("H", "tH")); line.P("H"); Tooltip.Keep("H", r);
            GUI.enabled = true;
            GUILayout.BeginArea(new Rect(20f, 180f, 200f, 100f));
            GUILayout.Button(C("C", "tC")); line.P("C"); Tooltip.Keep("C", GUILayoutUtility.GetLastRect());
            GUILayout.Space(20f);
            GUILayout.Label(C("D", "tD")); line.P("D"); Tooltip.Keep("D", GUILayoutUtility.GetLastRect());
            GUILayout.EndArea(); line.P("areaEnd");
            if (Event.current.type == EventType.Repaint)
            {
                var c = Tooltip.Rects["C"]; var d = Tooltip.Rects["D"];
                Tooltip.Rects["Dgap"] = new Rect(c.x, c.yMax + 2f, c.width, d.y - c.yMax - 4f);
            }
            GUI.BeginGroup(new Rect(20f, 300f, 200f, 100f));
            GUI.Button(r = new Rect(10f, 10f, 100f, 30f), C("E", "tE")); line.P("E"); Tooltip.Keep("E", r);
            GUI.EndGroup(); line.P("groupEnd");
            scroll = GUI.BeginScrollView(new Rect(20f, 420f, 200f, 80f), scroll, new Rect(0f, 0f, 180f, 300f));
            GUI.Button(r = new Rect(0f, 0f, 100f, 30f), C("F", "tF")); line.P("F"); Tooltip.Keep("F", r);
            GUI.Button(r = new Rect(0f, 100f, 100f, 30f), C("G", "tG")); line.P("G"); Tooltip.Keep("G", r);
            GUI.EndScrollView(); line.P("scrollEnd"); Tooltip.Keep("Fview", new Rect(20f, 420f, 200f, 80f));
            GUI.Button(r = new Rect(420f, 200f, 100f, 30f), C("Z", "tZ")); line.P("Z"); Tooltip.Keep("Z", r);
            w1 = GUI.Window(101, w1, Win1, C("Win1", "tWin1")); line.P("afterW1"); Tooltip.Keep("w1", w1);
            w2 = GUILayout.Window(102, w2, Win2, "Win2"); line.P("afterW2"); Tooltip.Keep("w2", w2);
            w4 = GUI.Window(104, w4, Win4, "Win4"); line.P("afterW4"); Tooltip.Keep("w4", w4);
            if (Tooltip.Mode == "setend" && Event.current.type == EventType.Repaint) line.Raw("set=" + Tooltip.SetTip("E") + ":" + Tooltip.Tip());
            line.End();
        }

        void Win1(int id)
        {
            var line = new Tooltip.Line("w1");
            if (Tooltip.Mode == "setwin") line.Raw("set=" + Tooltip.SetTip("W") + ":" + Tooltip.Tip());
            Rect r;
            GUI.Button(r = new Rect(10f, 25f, 100f, 30f), C("W1a", "tW1a")); line.P("W1a"); Tooltip.Keep("W1a", r);
            GUI.Label(r = new Rect(10f, 60f, 100f, 30f), C("W1b")); line.P("W1b"); Tooltip.Keep("W1b", r);
            GUI.Button(r = new Rect(210f, 140f, 80f, 30f), C("W1c", "tW1c")); line.P("W1c"); Tooltip.Keep("W1c", r);
            GUI.Button(r = new Rect(210f, 200f, 80f, 30f), C("W1d", "tW1d")); line.P("W1d"); Tooltip.Keep("W1d", r);
            GUI.BeginGroup(new Rect(10f, 100f, 150f, 60f));
            GUI.Button(r = new Rect(10f, 10f, 100f, 30f), C("W1g", "tW1g")); line.P("W1g"); Tooltip.Keep("W1g", r);
            GUI.EndGroup(); line.P("groupEnd");
            w3 = GUI.Window(103, w3, Win3, "Win3"); line.P("afterW3"); Tooltip.Keep("w3", w3);
            line.End();
        }

        void Win3(int id)
        {
            var line = new Tooltip.Line("w3");
            Rect r;
            GUI.Button(r = new Rect(10f, 25f, 100f, 30f), C("W3a", "tW3a")); line.P("W3a"); Tooltip.Keep("W3a", r);
            line.End();
        }

        void Win4(int id)
        {
            var line = new Tooltip.Line("w4");
            Rect r;
            GUI.Button(r = new Rect(10f, 60f, 100f, 30f), C("W4a", "tW4a")); line.P("W4a"); Tooltip.Keep("W4a", r);
            line.End();
        }

        // As Customize Duplicant traits' window: layout controls, a scroll view of labels with
        // tooltips, then GUI.tooltip read and shown.
        void Win2(int id)
        {
            var line = new Tooltip.Line("w2");
            GUILayout.Button(C("W2a", "tW2a")); line.P("W2a"); Tooltip.Keep("W2a", GUILayoutUtility.GetLastRect());
            GUILayout.Label(C("W2b")); line.P("W2b"); Tooltip.Keep("W2b", GUILayoutUtility.GetLastRect());
            scroll2 = GUILayout.BeginScrollView(scroll2, GUILayout.Height(100f));
            for (int i = 0; i < 6; i++)
            {
                GUILayout.Label(C("W2s" + i, "tW2s" + i)); line.P("W2s" + i); Tooltip.Keep("W2s" + i, GUILayoutUtility.GetLastRect());
            }
            GUILayout.EndScrollView(); line.P("scrollEnd"); Tooltip.Keep("W2view", GUILayoutUtility.GetLastRect());
            string read = Tooltip.TipOrNull();
            GUILayout.Label(string.IsNullOrEmpty(read) ? "(none)" : read); line.P("W2tail"); Tooltip.Keep("W2tail", GUILayoutUtility.GetLastRect());
            if (Tooltip.Mode == "setw2end" && Event.current.type == EventType.Repaint) line.Raw("set=" + Tooltip.SetTip("V") + ":" + Tooltip.Tip());
            line.End();
        }
    }

    // A second behaviour, enabled after the first: it only reads.
    public class TooltipGui2 : MonoBehaviour
    {
        void OnGUI()
        {
            if (!Tooltip.Active) return;
            new Tooltip.Line("gui2").End();
        }
    }
}
