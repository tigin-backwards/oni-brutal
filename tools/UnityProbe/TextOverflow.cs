// Overflow at the edges ("PROBE ... OV ..."), at the main menu: TMP's isTextOverflowing,
// firstOverflowCharacterIndex, isTextTruncated and each character's visibility for texts of
// white space alone, and for leading and trailing white space around a visible character, in
// rects around the text's preferred size (0, a hundredth, the preferred size and a little under
// and over it, one line of the font), with Overflow, Truncate, Ellipsis and Masking and with
// wrapping on and off. Set up as SetTextStyleSetting does (kerning, extra padding). With
// PROBE_SCENARIO=textoverflow only this probe runs.
#pragma warning disable CS0618 // enableKerning, as SetTextStyleSetting.ApplyStyle sets it
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SemanticsProbe
{
    public class TextOverflow : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static void L(string s) => Rec.Log("OV " + s);
        static string R(float v) => v.ToString("R", Inv);

        static readonly string[] Texts =
        {
            " ", "  ", "\t", " \n ", "\n", "", "\u00A0",
            "A", " A", "A ", " A ", "  A  ", "A\n", "\nA", "A\n ", " \nA", "A B", "AB",
            "A\nB", " \n\nA", "A \n ", "\u2026",
        };

        static readonly TextOverflowModes[] Modes =
            { TextOverflowModes.Overflow, TextOverflowModes.Truncate, TextOverflowModes.Ellipsis, TextOverflowModes.Masking };

        static string Esc(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                sb.Append(c == '\n' ? "\\n" : c == '\t' ? "\\t" : c == '\u00A0' ? "\\u00A0" : c.ToString());
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
            var cgo = new GameObject("OvCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            yield return null;
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var (name, size) in new[] { ("NotoSans-Regular", 14f), ("GRAYSTROKE REGULAR SDF", 12f) })
            {
                var font = fonts.FirstOrDefault(f => f.name == name);
                if (font == null) { L("font " + name + " missing"); continue; }
                var fi = font.faceInfo;
                float lh = (fi.ascentLine - fi.descentLine) * (size / fi.pointSize * fi.scale);
                L("face " + name + " size=" + R(size) + " ascent-descent=" + R(lh) + " lineHeight=" + R(fi.lineHeight * (size / fi.pointSize * fi.scale)));
                foreach (var text in Texts)
                {
                    Measure(canvas.transform, font, name, size, lh, text);
                    yield return null;
                }
            }
            Destroy(cgo);
            L("=== done");
        }

        static TextMeshProUGUI Make(Transform canvas, TMP_FontAsset font, float size, string text)
        {
            var go = new GameObject("Ov", typeof(RectTransform));
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
            tmp.text = text;
            return tmp;
        }

        static float[] Around(float v, params float[] fixedValues)
        {
            var list = new List<float>(fixedValues) { v - 0.01f, v - 0.001f, v, v + 0.001f, v + 0.01f };
            return list.Where(x => x >= 0f).Distinct().OrderBy(x => x).ToArray();
        }

        static void Measure(Transform canvas, TMP_FontAsset font, string name, float size, float lh, string text)
        {
            string tag = name + " " + R(size) + " \"" + Esc(text) + "\"";
            var tmp = Make(canvas, font, size, text);
            float pw = tmp.preferredWidth, ph = tmp.preferredHeight;
            L(tag + " preferred=" + R(pw) + "x" + R(ph));
            // The layout in the 400 x 100 rect, without wrapping: each character's origin, advance,
            // ascender, descender and baseline, and each line's.
            tmp.ForceMeshUpdate();
            var ri = tmp.textInfo;
            var mb = new StringBuilder();
            for (int i = 0; i < ri.characterCount; i++)
            {
                var ch = ri.characterInfo[i];
                mb.Append(' ').Append((int)ch.character).Append(':').Append(R(ch.origin)).Append('/').Append(R(ch.xAdvance))
                  .Append('/').Append(R(ch.ascender)).Append('/').Append(R(ch.descender)).Append('/').Append(R(ch.baseLine));
            }
            for (int k = 0; k < ri.lineCount; k++)
            {
                var li = ri.lineInfo[k];
                mb.Append(" line").Append(k).Append(':').Append(R(li.ascender)).Append('/').Append(R(li.descender)).Append('/').Append(R(li.baseline));
            }
            L(tag + " metrics" + mb);
            var widths = Around(pw, 0f, 0.005f, 0.01f, 400f);
            var heights = Around(ph, 0f, 0.005f, 0.01f, lh - 0.01f, lh, lh + 0.01f, 100f);
            var rt = (RectTransform)tmp.transform;
            foreach (var align in new[] { TextAlignmentOptions.Center, TextAlignmentOptions.TopLeft })
                foreach (var mode in Modes)
                    foreach (bool wrap in new[] { false, true })
                    {
                        if (align == TextAlignmentOptions.TopLeft && (mode != TextOverflowModes.Overflow || wrap)) continue;
                        tmp.alignment = align;
                        tmp.overflowMode = mode;
                        tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                        foreach (float w in widths)
                            foreach (float h in heights)
                            {
                                rt.sizeDelta = new Vector2(w, h);
                                tmp.ForceMeshUpdate();
                                var info = tmp.textInfo;
                                var sb = new StringBuilder();
                                for (int i = 0; i < info.characterCount; i++)
                                {
                                    var ch = info.characterInfo[i];
                                    sb.Append(' ').Append((int)ch.character).Append(ch.isVisible ? ":v" : ":-").Append(ch.lineNumber);
                                }
                                L(tag + " " + align + " " + mode + (wrap ? " wrap" : " nowrap") + " rect=" + R(w) + "x" + R(h)
                                  + " ovf=" + tmp.isTextOverflowing + " idx=" + tmp.firstOverflowCharacterIndex + " trunc=" + tmp.isTextTruncated
                                  + " cc=" + info.characterCount + " sc=" + info.spaceCount + " wc=" + info.wordCount
                                  + " lc=" + info.lineCount + " pc=" + info.pageCount + " chars" + sb);
                            }
                    }
            Destroy(tmp.gameObject);
        }
    }
}
