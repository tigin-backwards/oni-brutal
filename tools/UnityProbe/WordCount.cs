// TMP's words ("PROBE ... WC ..."), at the main menu: textInfo.wordCount, each word's first and
// last character index and character count, and each line's word count, for texts around
// separators (hyphens and dashes, non-breaking spaces, apostrophes and quotes, digits, underscores,
// CJK, rich-text tags) and a sweep of single characters between, before and after letters
// ("ab?cd", "?ab", "ab?", "12?34", "?"). Then wrapped texts in rects of every width from 1 to past
// their preferred width, logging each distinct break (the characters of each line) with the words.
// In NotoSans-Regular 14 pt and GRAYSTROKE REGULAR SDF 12 pt, set up as SetTextStyleSetting does.
// With PROBE_SCENARIO=wordcount only this probe runs.
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
    public class WordCount : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static void L(string s) => Rec.Log("WC " + s);
        static string R(float v) => v.ToString("R", Inv);

        static readonly string[] Texts =
        {
            "Liquid Sulfur Geyser NJ24‑7", "Liquid Sulfur Geyser NJ24-7", "NJ24‑7", "NJ24-7", "NJ24", "24-7", "24‑7",
            "a-b", "a‐b", "a‑b", "a‒b", "a–b", "a—b", "a―b", "a­b", "a−b",
            "a--b", "a-", "-a", "-", "--", "- a", "a -", "a - b", "a‑", "‑a", "‑", "a ‑ b",
            "a b", "a b", "a b", "a ", " a", " ", "a  b",
            "don't", "'tis", "rock 'n' roll", "a''b", "a'", "'a", "a'1", "1'a", "a' b", "a 'b",
            "don’t", "’tis", "a‘b", "\"a\"", "a\"b", "“a”", "a`b",
            "a_b", "_a", "a_", "a1b2", "1a", "a1", "12 34", "1.5", "1,000", "3.14-2", "x2.5",
            "中文字", "ab中cd", "中 ab", "あいう", "가각 갂", "中。文",
            "<b>ab</b>cd", "ab<b>cd</b>", "ab <b>cd</b>", "<b>ab cd</b>", "<color=#FF0000>ab</color> cd", "ab<br>cd",
            "ab\ncd", "ab\n\ncd", "ab\tcd", "ab  cd", " ab", "ab ", "  ab  cd  ", ".ab", "ab.", "ab. cd", "a.b",
            "été", "éa", "αβ γ", "абв", "ab😀cd", "😀",
            "<noparse><b></noparse>ab", "a<i>-</i>b", "a<b>‑</b>b", "<sprite=0>ab", "ab<sprite=0>", "a​b",
            "", " ", "a", "1", "+", "%a", "a%", "$5", "5$", "(a)", "a/b", "a\\b", "a&b", "#1",
            // Texts without words, and a trailing apostrophe.
            "..", "...", "%%", "  ", "\t", "\n", ". ", " .", "+.", ".+", "(", "()", "+ a", ". a", "+ +", ". .",
            "§", "§a", "a§", "¶", "§ a",
            "'", "''", "' a", "a '", "ab'", "abc'", "abcd'", "ab''", "ab'c'", "ab' ", "ab'.", "ab'\n", "ab'\ncd", "ab'-", "ab-'",
            "1'", "12'", "ab'c", "ab’", "abc’", "ab'cd'", "ab cd'", "ab cde'", "abc de'", "x ab'", "ab'<b></b>", "ab' cd'",
            "-ab'", "a-'", "-'", "ab'x'", "ab‑'", "中文'",
            // Carriage returns, alone and before line feeds (One Hundred K Challenge's world
            // description ends its paragraphs with "\r\n").
            "\r", "ab\rcd", "ab\r", "\rab", "ab\r\ncd", "ab\r\n", "ab\r\n\r\ncd", "ab\n\rcd", "ab \r\ncd",
            "ab.\r\n\r\n<smallcaps>cd ef</smallcaps>\r\n", "ab\r\rcd", "ab\u000Bcd", "ab\x2028cd",
        };

        static IEnumerable<int> SweepCodes()
        {
            for (int c = 0x21; c < 0x7F; c++) yield return c;
            for (int c = 0xA0; c < 0x180; c++) yield return c;
            for (int c = 0x2000; c < 0x2070; c++) yield return c;
            foreach (int c in new[] { 0x0301, 0x02BC, 0x0387, 0x05BE, 0x05F3, 0x0640, 0x1806, 0x2212, 0x2E3A, 0x2E3B, 0x30A0, 0x30FB, 0x30FC, 0xFE58, 0xFE63, 0xFF0D, 0xFF3F, 0xFF07 })
                yield return c;
            for (int c = 0x3000; c < 0x3040; c++) yield return c;
            foreach (int c in new[] { 0x4E00, 0x3042, 0x30A2, 0xAC00, 0xFF21, 0xFF10 }) yield return c;
        }

        static readonly string[] Wrapped =
        {
            "aaaa-bbbb", "aaaa‑bbbb", "aaaa‐bbbb", "aaaa‒bbbb", "aaaa–bbbb", "aaaa−bbbb", "aaaa­bbbb",
            "aaaa bbbb", "aaaa bbbb", "aaaa bbbb", "aaaa bbbb", "aaaa'bbbb", "aaaa_bbbb", "aaaa/bbbb",
            "NJ24‑7 NJ24-7", "Liquid Sulfur Geyser NJ24‑7", "Liquid Sulfur Geyser NJ24-7", "aa-bb-cc-dd", "aa‑bb‑cc‑dd",
        };

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
            var cgo = new GameObject("WcCanvas", typeof(RectTransform));
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
                var tmp = Make(canvas.transform, font, size);
                int n = 0;
                foreach (var text in Texts)
                {
                    Words(tmp, name, "T", text);
                    if (++n % 50 == 0) yield return null;
                }
                foreach (int code in SweepCodes())
                {
                    string x = ((char)code).ToString();
                    foreach (var text in new[] { "ab" + x + "cd", x + "ab", "ab" + x, "12" + x + "34", x })
                        Words(tmp, name, "S", text);
                    if (++n % 20 == 0) yield return null;
                }
                tmp.richText = false;
                foreach (var text in new[] { "<b>ab</b>cd", "ab<b>cd</b>", "a<b", "a>b", "<ab>" })
                    Words(tmp, name, "N", text);
                tmp.richText = true;
                yield return null;
                foreach (var text in Wrapped)
                {
                    Wrap(tmp, name, text);
                    yield return null;
                }
                Destroy(tmp.gameObject);
            }
            Destroy(cgo);
            L("=== done");
        }

        static string Hex(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                sb.Append(c >= 0x21 && c < 0x7F && c != '"' && c != '\\' ? c.ToString() : "\\u" + ((int)c).ToString("X4"));
            return sb.ToString();
        }

        static string Info(TMP_TextInfo info)
        {
            var sb = new StringBuilder();
            sb.Append("cc=").Append(info.characterCount).Append(" sc=").Append(info.spaceCount).Append(" wc=").Append(info.wordCount)
              .Append(" lc=").Append(info.lineCount).Append(" words=");
            for (int w = 0; w < info.wordCount; w++)
            {
                var wi = info.wordInfo[w];
                if (w > 0) sb.Append(',');
                sb.Append(wi.firstCharacterIndex).Append('-').Append(wi.lastCharacterIndex).Append(':').Append(wi.characterCount);
            }
            sb.Append(" lw=");
            for (int k = 0; k < info.lineCount; k++)
            {
                if (k > 0) sb.Append(',');
                sb.Append(info.lineInfo[k].wordCount);
            }
            sb.Append(" chars=");
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (i > 0) sb.Append(',');
                sb.Append(((int)ch.character).ToString("X")).Append(ch.isVisible ? "" : "-");
            }
            return sb.ToString();
        }

        static void Words(TextMeshProUGUI tmp, string name, string kind, string text)
        {
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)tmp.transform).sizeDelta = new Vector2(400, 100);
            tmp.text = text;
            tmp.ForceMeshUpdate();
            L(kind + " " + name + " \"" + Hex(text) + "\" " + Info(tmp.textInfo));
        }

        // Each distinct break of a wrapped text across widths: the characters of each line, by
        // index, then the words.
        static void Wrap(TextMeshProUGUI tmp, string name, string text)
        {
            var rt = (RectTransform)tmp.transform;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            rt.sizeDelta = new Vector2(1000, 400);
            tmp.text = text;
            tmp.ForceMeshUpdate();
            float pw = tmp.preferredWidth;
            L("W " + name + " \"" + Hex(text) + "\" preferred=" + R(pw) + " " + Info(tmp.textInfo));
            tmp.textWrappingMode = TextWrappingModes.Normal;
            string last = null;
            for (float w = 1f; w <= pw + 2f; w += 0.25f)
            {
                rt.sizeDelta = new Vector2(w, 400);
                tmp.ForceMeshUpdate();
                var info = tmp.textInfo;
                var sb = new StringBuilder();
                for (int k = 0; k < info.lineCount; k++)
                {
                    var li = info.lineInfo[k];
                    if (k > 0) sb.Append('|');
                    for (int i = li.firstCharacterIndex; i <= li.lastCharacterIndex && i < info.characterCount; i++)
                        sb.Append(Hex(info.characterInfo[i].character.ToString()));
                }
                string lines = sb.ToString();
                string all = lines + " " + Info(info);
                if (all == last) continue;
                last = all;
                L("W " + name + " \"" + Hex(text) + "\" w=" + R(w) + " lines=" + lines + " " + Info(info));
            }
        }

        static TextMeshProUGUI Make(Transform canvas, TMP_FontAsset font, float size)
        {
            var go = new GameObject("Wc", typeof(RectTransform));
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
