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
            bool all = Environment.GetEnvironmentVariable("PROBE_OV_PART") != "chars";
            foreach (var (name, size) in new[] { ("NotoSans-Regular", 14f), ("GRAYSTROKE REGULAR SDF", 12f) })
            {
                var font = fonts.FirstOrDefault(f => f.name == name);
                if (font == null) { L("font " + name + " missing"); continue; }
                Characters(canvas.transform, font, name, size);
                yield return null;
                Tabs(canvas.transform, font, name, size);
                yield return null;
                if (!all) continue;
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

        static string Hex(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                sb.Append(c >= 0x21 && c < 0x7F ? c.ToString() : "\\u" + ((int)c).ToString("X4"));
            return sb.ToString();
        }

        static string Chars(TMP_TextInfo info)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                sb.Append(' ').Append((int)ch.character).Append(ch.isVisible ? ":v" : ":-").Append(ch.lineNumber)
                  .Append('/').Append(R(ch.origin)).Append('/').Append(R(ch.xAdvance)).Append('/').Append(R(ch.ascender)).Append('/').Append(R(ch.descender));
            }
            return sb.ToString();
        }

        // Further white space and control characters: whether each is checked against the height,
        // counts towards the preferred height, and whether a text of only it overflows.
        static readonly int[] Codes =
        {
            0x20, 0xA0, 0x09, 0x0A, 0x0D, 0x0B, 0x0C, 0x03,
            0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200A,
            0x200B, 0x2028, 0x2029, 0x3000, 0xFEFF, 0x180E,
        };

        static void Characters(Transform canvas, TMP_FontAsset font, string name, float size)
        {
            var fi = font.faceInfo;
            float lh = (fi.ascentLine - fi.descentLine) * (size / fi.pointSize * fi.scale);
            foreach (int code in Codes)
            {
                string x = ((char)code).ToString();
                foreach (var text in new[] { x, x + x, x + "A", "A" + x, "A\n" + x, "A\n" + x + x, x + "\nA" })
                {
                    string tag = "CH " + name + " " + R(size) + " \"" + Hex(text) + "\"";
                    var tmp = Make(canvas, font, size, text);
                    var rt = (RectTransform)tmp.transform;
                    L(tag + " preferred=" + R(tmp.preferredWidth) + "x" + R(tmp.preferredHeight));
                    foreach (var mode in new[] { TextOverflowModes.Overflow, TextOverflowModes.Truncate })
                        foreach (var (w, h) in new[] { (0f, 0f), (0.01f, 0.01f), (400f, 0f), (400f, lh), (400f, lh - 0.01f), (400f, 100f) })
                        {
                            tmp.overflowMode = mode;
                            rt.sizeDelta = new Vector2(w, h);
                            tmp.ForceMeshUpdate();
                            var info = tmp.textInfo;
                            L(tag + " " + mode + " rect=" + R(w) + "x" + R(h) + " ovf=" + tmp.isTextOverflowing + " idx=" + tmp.firstOverflowCharacterIndex
                              + " trunc=" + tmp.isTextTruncated + " cc=" + info.characterCount + " lc=" + info.lineCount + " chars" + Chars(info));
                        }
                    Destroy(tmp.gameObject);
                }
            }
        }

        // The tab's advance: the font's tab settings, and tabs after other characters, at two sizes,
        // in rects of several widths, left-aligned and centred.
        static void Tabs(Transform canvas, TMP_FontAsset font, string name, float size)
        {
            var sb = new StringBuilder();
            foreach (var obj in new object[] { font, font.faceInfo })
                foreach (var m in obj.GetType().GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                {
                    if (m.Name.IndexOf("tab", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    object v = null;
                    try
                    {
                        if (m is System.Reflection.FieldInfo f) v = f.GetValue(obj);
                        else if (m is System.Reflection.PropertyInfo p && p.GetIndexParameters().Length == 0) v = p.GetValue(obj);
                        else continue;
                    }
                    catch (Exception e) { v = e.GetType().Name; }
                    sb.Append(' ').Append(obj.GetType().Name).Append('.').Append(m.Name).Append('=').Append(v);
                }
            foreach (uint c in new uint[] { 9, 32 })
            {
                if (font.characterLookupTable.TryGetValue(c, out var ch) && ch.glyph != null)
                    sb.Append(" glyph").Append(c).Append(": advance=").Append(R(ch.glyph.metrics.horizontalAdvance)).Append(" scale=").Append(R(ch.glyph.scale)).Append(" chscale=").Append(R(ch.scale));
                else
                    sb.Append(" glyph").Append(c).Append(": none");
            }
            var fi = font.faceInfo;
            sb.Append(" pointSize=").Append(R(fi.pointSize)).Append(" faceScale=").Append(R(fi.scale));
            L("TAB " + name + " settings" + sb);
            foreach (float s in new[] { size, size * 2 })
                foreach (var text in new[] { "\t", "\t\t", "A\t", "AAAAA\t", " \t", "\tA", "\t\tA", "A\tB" })
                    foreach (float w in new[] { 50f, 400f, 1000f })
                        foreach (var align in new[] { TextAlignmentOptions.TopLeft, TextAlignmentOptions.Top })
                        {
                            if (align == TextAlignmentOptions.Top && w != 400f) continue;
                            var tmp = Make(canvas, font, s, text);
                            tmp.alignment = align;
                            ((RectTransform)tmp.transform).sizeDelta = new Vector2(w, 100);
                            tmp.ForceMeshUpdate();
                            L("TAB " + name + " " + R(s) + " \"" + Hex(text) + "\" w=" + R(w) + " " + align + " preferred=" + R(tmp.preferredWidth) + "x" + R(tmp.preferredHeight)
                              + " chars" + Chars(tmp.textInfo));
                            Destroy(tmp.gameObject);
                        }
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
