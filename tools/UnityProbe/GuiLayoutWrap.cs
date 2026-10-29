// GUILayout part "wrap" (PROBE_SCENARIO=guilayout PROBE_GL_PART=wrap, lines "GL wrap ..."): how a
// horizontal group shares too little width among its entries, how word-wrapping labels are sized
// and broken in a horizontal group and in a scroll view of a fixed width, and ModTools' Runtime
// Console window (Workshop 3241644477) laid out as its DoMyWindow does. Each entry's rect is
// logged (GetLastRect, in Repaint), with a label's line breaks read back through
// GetCursorPixelPosition ("brk=" lists the character indices that start a new line). A scroll
// view is asked for a scroll position of 100000 on both axes; the position it returns is how far
// its content overflows the view.
using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SemanticsProbe
{
    public class GlWrap : MonoBehaviour
    {
        internal static bool Log;
        static StringBuilder sb;
        static string F(float v) => GuiLayout.F(v);
        static string R(Rect r) => GuiLayout.R(r);

        static bool Rep => Event.current.type == EventType.Repaint;

        static void E(string name, System.Action a)
        {
            a();
            if (!Rep || sb == null) return;
            sb.Append(' ').Append(name).Append('=').Append(R(GUILayoutUtility.GetLastRect()));
        }

        // A label's entry, with where its text breaks into lines at the rect it got.
        static void Lab(string name, string text, GUIStyle style, params GUILayoutOption[] o)
        {
            GUILayout.Label(text, style, o);
            if (!Rep || sb == null) return;
            var r = GUILayoutUtility.GetLastRect();
            sb.Append(' ').Append(name).Append('=').Append(R(r)).Append(" brk=").Append(Breaks(style, r, text));
        }

        internal static string Breaks(GUIStyle style, Rect r, string text)
        {
            var c = new GUIContent(text);
            var parts = new StringBuilder();
            float y = float.NaN;
            for (int i = 0; i < text.Length; i++)
            {
                var p = style.GetCursorPixelPosition(r, c, i);
                if (!float.IsNaN(y) && Mathf.Abs(p.y - y) > 1f) parts.Append(parts.Length == 0 ? "" : ",").Append(i);
                y = p.y;
            }
            return parts.Length == 0 ? "-" : parts.ToString();
        }

        static void Seq(string name, float w, float h, System.Action body)
        {
            if (Rep && Log) sb = new StringBuilder("wrap " + name + " area=" + F(w) + "x" + F(h));
            else sb = null;
            GUILayout.BeginArea(new Rect(20f, 20f, w, h));
            try { body(); }
            catch (Exception e) { if (sb != null) sb.Append(" !").Append(e.GetType().Name).Append(':').Append(GuiLayout.Msg(e.Message)); }
            GUILayout.EndArea();
            if (sb != null) GuiLayout.L(sb.ToString());
            sb = null;
        }

        const string Long1 = "[17:12:36.557] [1] [WARNING] baby_snail_iron_kanim iron_ui placeSymbol [ iron_ui ] is missing";
        const string Long2 = "[17:12:37.970] [1] [WARNING] world Asteroid has invalid season VanillaSandstoneDefaultMeteorShowers";
        const string Long3 = "[17:12:40.206] [1] [INFO] Screen resolution updated, saving values to prefs: 1280x720 @ 0, fullscreen: Windowed";
        const string Short1 = "[OniMcp] Queued client request notifications/message for 1 session(s).";

        static Vector2 s1, s2, s3, s4;
        static string filter = "";

        void OnGUI()
        {
            if (GuiLayout.Phase != "wrap") return;
            var skin = GUI.skin;
            var lab = skin.label;

            if (Rep && Log)
            {
                foreach (var t in new[] { "Count: 59", "Count:", "X - Close Window", "Clear", "Show when Start", Long1, Long3 })
                {
                    lab.CalcMinMaxWidth(new GUIContent(t), out var mn, out var mx);
                    var sz = lab.CalcSize(new GUIContent(t));
                    var s = new StringBuilder("wrap measure '" + t + "' mm=" + F(mn) + "," + F(mx) + " calc=" + GuiLayout.V(sz));
                    foreach (var w in new[] { 10f, 20f, 25f, 30f, 35f, 40f, 45f, 50f, 60f, 80f, 120f, 200f, 300f, 400f, 600f })
                        s.Append(" h").Append(F(w)).Append('=').Append(F(lab.CalcHeight(new GUIContent(t), w)))
                         .Append('/').Append(Breaks(lab, new Rect(0f, 0f, w, 200f), t));
                    GuiLayout.L(s.ToString());
                }
            }

            // Too little width: buttons and labels side by side.
            foreach (var w in new[] { 400f, 200f, 120f })
            {
                Seq("sq.buttons." + w, w, 100f, () =>
                {
                    GUILayout.BeginHorizontal();
                    for (int i = 0; i < 6; i++) E("b" + i, () => GUILayout.Button("button " + i));
                    GUILayout.EndHorizontal();
                });
                Seq("sq.labels." + w, w, 100f, () =>
                {
                    GUILayout.BeginHorizontal();
                    for (int i = 0; i < 6; i++) Lab("l" + i, "label " + i, lab);
                    GUILayout.EndHorizontal();
                });
                Seq("sq.count." + w, w, 100f, () =>
                {
                    GUILayout.BeginHorizontal();
                    E("close", () => GUILayout.Button("X - Close Window"));
                    E("clear", () => GUILayout.Button("Clear"));
                    Lab("count", "Count: 59", lab);
                    E("tf", () => filter = GUILayout.TextField(filter, GUILayout.MinWidth(80f)));
                    GUILayout.EndHorizontal();
                });
            }
            foreach (var w in new[] { 1000f, 630f, 400f })
                Seq("sq.toolbar." + w, w, 100f, () => Toolbar(lab));
            // Mixed fixed and flexible widths.
            Seq("sq.fixed.200", 200f, 100f, () =>
            {
                GUILayout.BeginHorizontal();
                E("w150", () => GUILayout.Button("a", GUILayout.Width(150f)));
                E("b", () => GUILayout.Button("button b"));
                Lab("l", "Count: 59", lab);
                GUILayout.EndHorizontal();
            });
            Seq("sq.min.200", 200f, 100f, () =>
            {
                GUILayout.BeginHorizontal();
                E("min150", () => GUILayout.Button("a", GUILayout.MinWidth(150f)));
                E("b", () => GUILayout.Button("button b"));
                Lab("l", "Count: 59", lab);
                GUILayout.EndHorizontal();
            });

            // Word-wrapping labels beside a fixed button, in an area and in a scroll view.
            Seq("wr.row.400", 400f, 300f, () =>
            {
                GUILayout.BeginHorizontal();
                E("copy", () => GUILayout.Button("Copy", GUILayout.Width(44f), GUILayout.Height(24f)));
                Lab("l", Long1, lab, GUILayout.Height(24f));
                GUILayout.EndHorizontal();
                Lab("alone", Long1, lab);
                Lab("alone.h24", Long1, lab, GUILayout.Height(24f));
            });
            Seq("wr.scroll.w380", 400f, 300f, () =>
            {
                s1 = new Vector2(100000f, 100000f);
                E("sv", () => s1 = GUILayout.BeginScrollView(s1, GUILayout.Width(380f), GUILayout.Height(150f)));
                GUILayout.BeginHorizontal();
                E("copy", () => GUILayout.Button("Copy", GUILayout.Width(44f), GUILayout.Height(24f)));
                Lab("l", Long1, lab, GUILayout.Height(24f));
                GUILayout.EndHorizontal();
                Lab("alone", Long2, lab);
                GUILayout.EndScrollView();
                if (sb != null) sb.Append(" view=").Append(R(GUILayoutUtility.GetLastRect())).Append(" pos=").Append(GuiLayout.V(s1));
            });
            Seq("wr.scroll.noopt", 400f, 300f, () =>
            {
                s2 = new Vector2(100000f, 100000f);
                E("sv", () => s2 = GUILayout.BeginScrollView(s2));
                GUILayout.BeginHorizontal();
                E("copy", () => GUILayout.Button("Copy", GUILayout.Width(44f), GUILayout.Height(24f)));
                Lab("l", Long1, lab, GUILayout.Height(24f));
                GUILayout.EndHorizontal();
                GUILayout.EndScrollView();
                if (sb != null) sb.Append(" view=").Append(R(GUILayoutUtility.GetLastRect())).Append(" pos=").Append(GuiLayout.V(s2));
            });
            Seq("wr.scroll.tall", 400f, 300f, () =>
            {
                s3 = new Vector2(100000f, 100000f);
                E("sv", () => s3 = GUILayout.BeginScrollView(s3, GUILayout.Width(380f), GUILayout.Height(120f)));
                for (int i = 0; i < 8; i++)
                {
                    GUILayout.BeginHorizontal();
                    E("copy" + i, () => GUILayout.Button("Copy", GUILayout.Width(44f), GUILayout.Height(24f)));
                    Lab("l" + i, i % 2 == 0 ? Long1 : Short1, lab, GUILayout.Height(24f));
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                if (sb != null) sb.Append(" view=").Append(R(GUILayoutUtility.GetLastRect())).Append(" pos=").Append(GuiLayout.V(s3));
            });

            // The Runtime Console window, as the mod lays it out.
            GUI.Window(951, new Rect(620f, 120f, 650f, 500f), Console, "Runtime Console");
        }

        static void Toolbar(GUIStyle lab)
        {
            GUILayout.BeginHorizontal();
            E("close", () => GUILayout.Button("X - Close Window"));
            E("clear", () => GUILayout.Button("Clear"));
            Lab("count", "Count: 59", lab);
            E("tf", () => filter = GUILayout.TextField(filter, GUILayout.MinWidth(220f)));
            E("t1", () => GUILayout.Toggle(true, "Show when Start"));
            E("t2", () => GUILayout.Toggle(true, "Show Window when Error"));
            E("sp1", () => GUILayout.Space(10f));
            E("t3", () => GUILayout.Toggle(true, "Auto scroll"));
            E("sp2", () => GUILayout.Space(10f));
            E("t4", () => GUILayout.Toggle(true, "Error"));
            E("t5", () => GUILayout.Toggle(true, "Warning"));
            E("t6", () => GUILayout.Toggle(true, "Log"));
            GUILayout.EndHorizontal();
            if (sb != null) sb.Append(" row=").Append(R(GUILayoutUtility.GetLastRect()));
        }

        static readonly string[] Lines = { Long1, Long2, "[17:12:37.866] [1] [INFO] Checking which steam branch we're on. Got: []", Long3, Short1, Short1 };

        static void Console(int id)
        {
            if (Rep && Log) sb = new StringBuilder("wrap console");
            else sb = null;
            try
            {
                GUI.DragWindow(new Rect(0f, 0f, 2000f, 20f));
                E("bv", () => GUILayout.BeginVertical());
                Toolbar(GUI.skin.label);
                s4 = new Vector2(100000f, 100000f);
                E("sv", () => s4 = GUILayout.BeginScrollView(s4, GUILayout.Width(650f - 30f)));
                for (int i = 0; i < Lines.Length; i++)
                {
                    GUILayout.BeginHorizontal();
                    E("copy" + i, () => GUILayout.Button("Copy", GUILayout.Width(44f), GUILayout.Height(24f)));
                    Lab("l" + i, Lines[i], GUI.skin.label, GUILayout.Height(24f));
                    GUILayout.EndHorizontal();
                    if (sb != null) sb.Append(" row" + i + "=").Append(R(GUILayoutUtility.GetLastRect()));
                }
                GUILayout.EndScrollView();
                if (sb != null) sb.Append(" view=").Append(R(GUILayoutUtility.GetLastRect())).Append(" pos=").Append(GuiLayout.V(s4));
                GUILayout.EndVertical();
                if (sb != null) sb.Append(" vert=").Append(R(GUILayoutUtility.GetLastRect()));
            }
            catch (Exception e) { if (sb != null) sb.Append(" !").Append(e.GetType().Name).Append(':').Append(GuiLayout.Msg(e.Message)); }
            if (sb != null) GuiLayout.L(sb.ToString());
            sb = null;
        }
    }
}
