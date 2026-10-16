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
        bool done;

        void Update()
        {
            var active = SceneManager.GetActiveScene().name;
            if (active != scene)
            {
                scene = active;
                sceneStart = Time.realtimeSinceStartup;
            }
            if (done || scene != "frontend" || Time.realtimeSinceStartup - sceneStart < DelaySeconds) return;
            done = true;
            Dump();
        }

        static string F(float v) { var s = v.ToString("F1", CultureInfo.InvariantCulture); return s == "-0.0" ? "0.0" : s; }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        public static void Dump()
        {
            Rec.Log("=== ui: screen " + Screen.width + "x" + Screen.height);
            var corners = new Vector3[4];
            var lines = new System.Collections.Generic.List<string>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (canvas.isRootCanvas)
                {
                    var cam = canvas.worldCamera;
                    lines.Add("UI canvas " + Path(canvas.transform) + " mode=" + canvas.renderMode + " scale=" + canvas.scaleFactor.ToString("F3", CultureInfo.InvariantCulture)
                              + (cam == null ? "" : " camera " + cam.name + " " + cam.pixelWidth + "x" + cam.pixelHeight + " target " + (cam.targetTexture == null ? "none" : cam.targetTexture.width + "x" + cam.targetTexture.height)));
                }
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (rt.GetComponentInParent<Canvas>() == null) continue;
                rt.GetWorldCorners(corners);
                var sb = new StringBuilder("UI ");
                sb.Append(Path(rt));
                if (rt.parent != null) sb.Append(" #").Append(rt.GetSiblingIndex());
                var r = rt.rect;
                sb.Append(" rect ").Append(F(r.x)).Append(',').Append(F(r.y)).Append(' ').Append(F(r.width)).Append('x').Append(F(r.height));
                sb.Append(" world ").Append(F(corners[0].x)).Append(',').Append(F(corners[0].y)).Append(" to ").Append(F(corners[2].x)).Append(',').Append(F(corners[2].y));
                var text = rt.GetComponent<TMP_Text>();
                if (text != null && text.enabled)
                {
                    sb.Append(" text \"").Append(text.text.Replace("\n", "\\n")).Append('"');
                    sb.Append(" size ").Append(F(text.fontSize));
                    sb.Append(" pref ").Append(F(text.preferredWidth)).Append('x').Append(F(text.preferredHeight));
                    sb.Append(" lines ").Append(text.textInfo != null ? text.textInfo.lineCount : -1);
                }
                lines.Add(sb.ToString());
            }
            lines.Sort(System.StringComparer.Ordinal);
            foreach (var l in lines) Rec.Log(l);
            Rec.Log("=== ui: done");
        }
    }
}
