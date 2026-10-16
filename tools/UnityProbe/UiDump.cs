// UI layout probe: once the front end has been up for a while, every canvas's RectTransforms
// (active state, rect, world corners) and texts (content, font size, preferred size, lines),
// one line each ("PROBE ... UI ...").
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SemanticsProbe
{
    public class UiDump : MonoBehaviour
    {
        public static float DelaySeconds = 8f;
        float sceneStart = -1f;
        string scene;
        bool frontEndDone, gameDone;
        float lastClock = -1f;
        int runningFrames;

        void Update()
        {
            var active = SceneManager.GetActiveScene().name;
            if (active != scene)
            {
                scene = active;
                sceneStart = Time.realtimeSinceStartup;
            }
            float elapsed = Time.realtimeSinceStartup - sceneStart;
            if (!frontEndDone && scene == "frontend" && elapsed >= DelaySeconds)
            {
                frontEndDone = true;
                Dump("frontend", false);
            }
            // In game: once the clock has been running for 120 frames (embarked, unpaused).
            if (scene == "backend" && GameClock.Instance != null)
            {
                float t = GameClock.Instance.GetTime();
                runningFrames = t > lastClock ? runningFrames + 1 : runningFrames;
                lastClock = t;
            }
            if (!gameDone && scene == "backend" && runningFrames >= 120)
            {
                gameDone = true;
                Dump("game", true);
            }
        }

        static string F(float v) { var s = v.ToString("F1", CultureInfo.InvariantCulture); return s == "-0.0" ? "0.0" : s; }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        /// overlayOnly: only screen-space overlay canvases (world-space name tags follow the
        /// Duplicants, which move differently in each run).
        public static void Dump(string label, bool overlayOnly)
        {
            Rec.Log("=== ui " + label + ": screen " + Screen.width + "x" + Screen.height);
            var corners = new Vector3[4];
            var lines = new System.Collections.Generic.List<string>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (canvas.isRootCanvas)
                {
                    var cam = canvas.worldCamera;
                    lines.Add("UI canvas " + Path(canvas.transform) + " mode=" + canvas.renderMode + " scale=" + canvas.scaleFactor.ToString("F3", CultureInfo.InvariantCulture)
                              + (cam == null ? "" : " camera " + cam.name + " " + cam.pixelWidth + "x" + cam.pixelHeight + " target " + (cam.targetTexture == null ? "none" : cam.targetTexture.width + "x" + cam.targetTexture.height)));
                }
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c.isRootCanvas || (overlayOnly && c.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && c.renderMode != RenderMode.ScreenSpaceOverlay)) continue;
                var own = c.GetComponentInParent<Canvas>();
                lines.Add("UI nested " + Path(c.transform) + " enabled=" + c.enabled + " root=" + c.rootCanvas.name + " mode=" + c.renderMode
                          + " override=" + c.overrideSorting + " order=" + c.sortingOrder + " inParent=" + (own == null ? "null" : own == c ? "self" : own.name));
            }
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var parentCanvas = rt.GetComponentInParent<Canvas>();
                if (parentCanvas == null) continue;
                if (overlayOnly && parentCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                var sb = new StringBuilder("UI ");
                try
                {
                rt.GetWorldCorners(corners);
                sb.Append(Path(rt));
                if (rt.parent != null) sb.Append(" #").Append(rt.GetSiblingIndex());
                var r = rt.rect;
                sb.Append(" rect ").Append(F(r.x)).Append(',').Append(F(r.y)).Append(' ').Append(F(r.width)).Append('x').Append(F(r.height));
                sb.Append(" world ").Append(F(corners[0].x)).Append(',').Append(F(corners[0].y)).Append(" to ").Append(F(corners[2].x)).Append(',').Append(F(corners[2].y));
                var text = rt.GetComponent<TMP_Text>();
                if (text != null && text.enabled)
                {
                    sb.Append(" text ").Append(text.text == null ? "null" : "\"" + text.text.Replace("\n", "\\n") + "\"");
                    sb.Append(" size ").Append(F(text.fontSize));
                    sb.Append(" pref ").Append(F(text.preferredWidth)).Append('x').Append(F(text.preferredHeight));
                    sb.Append(" lines ").Append(text.textInfo != null ? text.textInfo.lineCount : -1);
                }
                }
                catch (System.Exception e)
                {
                    sb.Append(" ERROR ").Append(e.GetType().Name);
                }
                lines.Add(sb.ToString());
            }
            lines.Sort(System.StringComparer.Ordinal);
            foreach (var l in lines) Rec.Log(l);
            Rec.Log("=== ui " + label + ": done");
        }
    }
}
