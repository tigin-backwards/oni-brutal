// Lines taller in the middle ("PROBE ... TL ..."), at the main menu: where a text overflows the
// rect's height when its line grows taller part-way along it, from a glyph of a taller fallback
// font (the details screen's title "Liquid Sulfur Geyser NJ24‑7", whose U+2011 GRAYSTROKE takes
// from a fallback), a <size> or a <voffset>. For each text, font, overflow mode and wrapping it
// sweeps the rect's height and logs each distinct result: isTextOverflowing,
// firstOverflowCharacterIndex, isTextTruncated, the textInfo counts, each character (code,
// visibility, line) and each line's ascender, descender and height. Each text's layout in a tall
// rect is logged first (`metrics`: each character's ascender, descender, origin and advance).
// Set up as SetTextStyleSetting does (kerning, extra padding). With PROBE_SCENARIO=tallline only
// this probe runs.
#pragma warning disable CS0618 // enableKerning, as SetTextStyleSetting.ApplyStyle sets it
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SemanticsProbe
{
    public class TallLine : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static void L(string s) => Rec.Log("TL " + s);
        static string R(float v) => v.ToString("R", Inv);

        // The tall part comes after, before or between ordinary characters, on the first line or
        // a later one, once or twice.
        static readonly string[] Texts =
        {
            "Liquid Sulfur Geyser NJ24‑7", "Chlorine Gas Vent AB12‑3", "NJ24‑7", "‑7", "AAAA‑", "‑AAAA",
            "AAAA<size=150%>B</size>AA", "<size=150%>B</size>AAAA", "AAAA<size=150%>B</size>", "AA<size=130%>B</size>AA<size=160%>C</size>AA",
            "AAAA<voffset=0.4em>B</voffset>AA", "AAAA<voffset=-0.4em>B</voffset>AA",
            "AAAA<size=60%>B</size>AA", " <size=150%> </size>AAAA", "AAAA <size=150%> </size>AA",
            "AA\nAAAA<size=150%>B</size>AA", "AA\nAAAA‑AA", "AA\n<size=150%>B</size>AAAA",
            "AAAA AAAA<size=150%>B</size>AA",
        };

        static readonly TextOverflowModes[] Modes =
            { TextOverflowModes.Overflow, TextOverflowModes.Truncate, TextOverflowModes.Ellipsis };

        static string Hex(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                sb.Append(c >= 0x21 && c < 0x7F && c != '"' && c != '\\' ? c.ToString() : "\\u" + ((int)c).ToString("X4"));
            return sb.ToString();
        }

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var cgo = new GameObject("TlCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            yield return null;
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var (name, size) in new[] { ("GRAYSTROKE REGULAR SDF", 14f), ("NotoSans-Regular", 14f), ("GRAYSTROKE REGULAR SDF", 12f) })
            {
                var font = fonts.FirstOrDefault(f => f.name == name);
                if (font == null) { L("font " + name + " missing"); continue; }
                var tmp = Make(canvas.transform, font, size);
                foreach (var text in Texts)
                {
                    Metrics(tmp, name, size, text);
                    foreach (bool wrap in new[] { false, true })
                        foreach (var mode in Modes)
                            Sweep(tmp, name, size, text, wrap, mode, wrap && text.StartsWith("AAAA AAAA") ? 60f : 400f);
                    yield return null;
                }
                Destroy(tmp.gameObject);
            }
            // The title itself: its own rect (175.13 × 22), as the details screen lays it out.
            {
                var font = fonts.FirstOrDefault(f => f.name == "GRAYSTROKE REGULAR SDF");
                var tmp = Make(canvas.transform, font, 14f);
                foreach (var mode in Modes)
                    foreach (float w in new[] { 175.13f, 400f })
                        One(tmp, "title", mode, false, "Liquid Sulfur Geyser NJ24‑7", w, 22f);
                Destroy(tmp.gameObject);
            }
            Destroy(cgo);
            L("=== done");
        }

        static void Metrics(TextMeshProUGUI tmp, string name, float size, string text)
        {
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)tmp.transform).sizeDelta = new Vector2(400, 1000);
            tmp.text = text;
            tmp.ForceMeshUpdate();
            var info = tmp.textInfo;
            var sb = new StringBuilder();
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (i > 0) sb.Append(';');
                sb.Append(((int)ch.character).ToString("X")).Append(':').Append(R(ch.ascender)).Append(',').Append(R(ch.descender))
                  .Append(',').Append(R(ch.origin)).Append(',').Append(R(ch.xAdvance)).Append(",l").Append(ch.lineNumber);
            }
            L("metrics " + name + " " + R(size) + " \"" + Hex(text) + "\" pw=" + R(tmp.preferredWidth) + " ph=" + R(tmp.preferredHeight)
              + " chars=" + sb + " lines=" + Lines(info));
        }

        static string Lines(TMP_TextInfo info)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < info.lineCount; k++)
            {
                var li = info.lineInfo[k];
                if (k > 0) sb.Append(';');
                sb.Append(li.firstCharacterIndex).Append('-').Append(li.lastCharacterIndex).Append(':')
                  .Append(R(li.ascender)).Append(',').Append(R(li.descender)).Append(',').Append(R(li.lineHeight)).Append(',').Append(R(li.baseline));
            }
            return sb.ToString();
        }

        static string Result(TextMeshProUGUI tmp)
        {
            var info = tmp.textInfo;
            var sb = new StringBuilder();
            sb.Append("ov=").Append(tmp.isTextOverflowing ? 1 : 0).Append(" first=").Append(tmp.firstOverflowCharacterIndex)
              .Append(" trunc=").Append(tmp.isTextTruncated ? 1 : 0).Append(" cc=").Append(info.characterCount).Append(" lc=").Append(info.lineCount)
              .Append(" chars=");
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (i > 0) sb.Append(',');
                sb.Append(((int)ch.character).ToString("X")).Append(ch.isVisible ? "" : "-").Append('@').Append(ch.lineNumber);
            }
            sb.Append(" lines=").Append(Lines(info));
            return sb.ToString();
        }

        // Heights from 0 to 70 in steps of 0.25: each distinct result, with the first height
        // that gave it.
        static void Sweep(TextMeshProUGUI tmp, string name, float size, string text, bool wrap, TextOverflowModes mode, float width)
        {
            string last = null;
            for (float h = 0f; h <= 70f; h += 0.25f)
            {
                string r = Lay(tmp, mode, wrap, text, width, h);
                if (r == last) continue;
                last = r;
                L("S " + name + " " + R(size) + " " + mode + " wrap=" + (wrap ? 1 : 0) + " w=" + R(width) + " h=" + R(h) + " \"" + Hex(text) + "\" " + r);
            }
        }

        static string Lay(TextMeshProUGUI tmp, TextOverflowModes mode, bool wrap, string text, float w, float h)
        {
            tmp.overflowMode = mode;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            ((RectTransform)tmp.transform).sizeDelta = new Vector2(w, h);
            tmp.text = text;
            tmp.ForceMeshUpdate();
            return Result(tmp);
        }

        static void One(TextMeshProUGUI tmp, string tag, TextOverflowModes mode, bool wrap, string text, float w, float h) =>
            L("one " + tag + " " + mode + " wrap=" + (wrap ? 1 : 0) + " w=" + R(w) + " h=" + R(h) + " \"" + Hex(text) + "\" " + Lay(tmp, mode, wrap, text, w, h));

        static TextMeshProUGUI Make(Transform canvas, TMP_FontAsset font, float size)
        {
            var go = new GameObject("Tl", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 100);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.enableKerning = true;
            tmp.extraPadding = true;
            tmp.fontSize = size;
            tmp.font = font;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }
    }
}
