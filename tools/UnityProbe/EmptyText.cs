// Texts with nothing to draw ("PROBE ... ET ..."), at the main menu: isTextOverflowing,
// firstOverflowCharacterIndex, isTextTruncated, the textInfo counts and the preferred size of an
// empty text, a single space and texts of rich-text tags alone, in rects below and above one line
// of the font, in each overflow mode, wrapping on and off, with auto-size off and on. Found in a
// mod play-test: Adjustable Auto Sweeper's IntSliderSideScreen/Contents/UnitsLabel, a LocText
// whose text SliderSet.SetTarget sets to "" (NotoSans 16 pt, 95.7 x 15), reports overflowing at 0
// in the shipped package. So the texts are also laid out after another text (one that overflows
// and one that fits), set by ForceMeshUpdate and by the frame's own rebuild, and then resized.
// Set up as SetTextStyleSetting does (kerning, extra padding). With PROBE_SCENARIO=emptytext only
// this probe runs.
#pragma warning disable CS0618 // enableKerning, as SetTextStyleSetting.ApplyStyle sets it
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SemanticsProbe
{
    public class EmptyText : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static void L(string s) => Rec.Log("ET " + s);
        static string R(float v) => v.ToString("R", Inv);

        static readonly string[] Texts =
        {
            "", " ", "<b></b>", "<color=#FF0000></color>", "<size=150%></size>", "<b> </b>", "<i></i><u></u>", "<br>", "​",
        };

        static readonly TextOverflowModes[] Modes =
        {
            TextOverflowModes.Overflow, TextOverflowModes.Truncate, TextOverflowModes.Ellipsis, TextOverflowModes.Masking,
            TextOverflowModes.ScrollRect, TextOverflowModes.Page,
        };

        static string Hex(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
                sb.Append(c >= 0x21 && c < 0x7F && c != '"' && c != '\\' ? c.ToString() : "\\u" + ((int)c).ToString("X4"));
            return sb.Append('"').ToString();
        }

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        // The state as E2eDriver's dump reads it: the flags first, then the counts, then the
        // preferred size (whose reading may lay the text out again), then the flags once more.
        static string State(TextMeshProUGUI tmp)
        {
            var info = tmp.textInfo;
            var sb = new StringBuilder();
            sb.Append("ov=").Append(tmp.isTextOverflowing ? 1 : 0).Append(" first=").Append(tmp.firstOverflowCharacterIndex)
              .Append(" trunc=").Append(tmp.isTextTruncated ? 1 : 0)
              .Append(" cc=").Append(info == null ? -1 : info.characterCount).Append(" lc=").Append(info == null ? -1 : info.lineCount)
              .Append(" sc=").Append(info == null ? -1 : info.spaceCount).Append(" wc=").Append(info == null ? -1 : info.wordCount)
              .Append(" pc=").Append(info == null ? -1 : info.pageCount)
              .Append(" fs=").Append(R(tmp.fontSize));
            sb.Append(" pw=").Append(R(tmp.preferredWidth)).Append(" ph=").Append(R(tmp.preferredHeight));
            sb.Append(" after=").Append(tmp.isTextOverflowing ? 1 : 0).Append('/').Append(tmp.firstOverflowCharacterIndex);
            return sb.ToString();
        }

        IEnumerator Run()
        {
            var cgo = new GameObject("EtCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            yield return null;
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var (name, size) in new[] { ("NotoSans-Regular", 16f), ("GRAYSTROKE REGULAR SDF", 12f) })
            {
                var font = fonts.FirstOrDefault(f => f.name == name);
                if (font == null) { L("font " + name + " missing"); continue; }
                var fi = font.faceInfo;
                float lh = (fi.ascentLine - fi.descentLine) * (size / fi.pointSize * fi.scale);
                L("face " + name + " size=" + R(size) + " ascent-descent=" + R(lh));
                float[] heights = { 0f, 5f, 15f, lh - 0.01f, lh, lh + 0.01f, 100f };

                // Fresh texts: a new object per case, the text set before its first layout.
                foreach (var text in Texts)
                {
                    foreach (bool auto in new[] { false, true })
                        foreach (var mode in Modes)
                            foreach (bool wrap in new[] { false, true })
                                foreach (float h in heights)
                                {
                                    var tmp = Make(canvas.transform, font, size, auto);
                                    Set(tmp, mode, wrap, 95.7f, h);
                                    tmp.text = text;
                                    tmp.ForceMeshUpdate();
                                    L("fresh " + name + " " + R(size) + " " + mode + " wrap=" + (wrap ? 1 : 0) + " auto=" + (auto ? 1 : 0)
                                      + " rect=95.7x" + R(h) + " " + Hex(text) + " " + State(tmp));
                                    Destroy(tmp.gameObject);
                                }
                    yield return null;
                }

                // After another text: "kg" and "AB\nCD" (which overflows a one-line rect at 3),
                // laid out in a rect that it overflows (15 high, and one line for "AB\nCD") or that
                // fits it (100 high), then the empty text set and laid out by ForceMeshUpdate or by
                // the frame's rebuild; then the rect resized to 100 and to 0.
                foreach (var prior in new[] { "kg", "AB\nCD" })
                    foreach (float ph in new[] { 15f, lh + 0.01f, 100f })
                        foreach (var mode in new[] { TextOverflowModes.Overflow, TextOverflowModes.Truncate, TextOverflowModes.Ellipsis })
                            foreach (bool auto in new[] { false, true })
                                foreach (var text in new[] { "", " ", "<b></b>", null })
                                    foreach (bool frame in new[] { false, true })
                                    {
                                        string tag = "after " + name + " " + R(size) + " " + mode + " auto=" + (auto ? 1 : 0) + " prior=" + Hex(prior)
                                                     + " rect=95.7x" + R(ph) + " then=" + Hex(text) + (frame ? " frame" : " force");
                                        var tmp = Make(canvas.transform, font, size, auto);
                                        Set(tmp, mode, false, 95.7f, ph);
                                        tmp.text = prior;
                                        tmp.ForceMeshUpdate();
                                        L(tag + " prior " + State(tmp));
                                        tmp.text = text;
                                        if (frame) yield return null; else tmp.ForceMeshUpdate();
                                        L(tag + " set " + State(tmp));
                                        foreach (float h in new[] { 100f, 0f, 15f })
                                        {
                                            ((RectTransform)tmp.transform).sizeDelta = new Vector2(95.7f, h);
                                            if (frame) yield return null; else tmp.ForceMeshUpdate();
                                            L(tag + " resize=" + R(h) + " " + State(tmp));
                                        }
                                        Destroy(tmp.gameObject);
                                    }
                yield return null;

                // Which size auto-size leaves a text with nothing visible at: the size set
                // (fontSize) or the largest it may take (fontSizeMax), with the largest above and
                // below the size set, alone and after "kg".
                foreach (float max in new[] { size + 8f, size - 4f })
                    foreach (var prior in new[] { null, "kg" })
                        foreach (float h in new[] { 15f, 100f })
                            foreach (var text in new[] { "", " ", "<b></b>", "kg" })
                            {
                                string tag = "automax " + name + " " + R(size) + " max=" + R(max) + " prior=" + Hex(prior) + " rect=95.7x" + R(h) + " then=" + Hex(text);
                                var tmp = Make(canvas.transform, font, size, true);
                                tmp.fontSizeMax = max;
                                Set(tmp, TextOverflowModes.Overflow, false, 95.7f, h);
                                if (prior != null)
                                {
                                    tmp.text = prior;
                                    tmp.ForceMeshUpdate();
                                    L(tag + " prior " + State(tmp));
                                }
                                tmp.text = text;
                                tmp.ForceMeshUpdate();
                                L(tag + " set " + State(tmp));
                                Destroy(tmp.gameObject);
                            }
                yield return null;
            }

            // The side screen's own label, if the prefab is loaded at the main menu.
            foreach (var screen in Resources.FindObjectsOfTypeAll<IntSliderSideScreen>())
                foreach (var set in screen.sliderSets ?? new System.Collections.Generic.List<SliderSet>())
                {
                    var label = set.unitsLabel;
                    if (label == null) continue;
                    var rt = (RectTransform)label.transform;
                    L("prefab " + screen.name + " text=" + Hex(label.text) + " font=" + (label.font == null ? "null" : label.font.name) + " size=" + R(label.fontSize)
                      + " auto=" + label.enableAutoSizing + " mode=" + label.overflowMode + " wrap=" + label.textWrappingMode + " rect=" + R(rt.rect.width) + "x" + R(rt.rect.height)
                      + " stored=" + label.isTextOverflowing + "/" + label.firstOverflowCharacterIndex);
                }
            Destroy(cgo);
            L("=== done");
        }

        static void Set(TextMeshProUGUI tmp, TextOverflowModes mode, bool wrap, float w, float h)
        {
            tmp.overflowMode = mode;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            ((RectTransform)tmp.transform).sizeDelta = new Vector2(w, h);
        }

        static TextMeshProUGUI Make(Transform canvas, TMP_FontAsset font, float size, bool auto)
        {
            var go = new GameObject("Et", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(95.7f, 15f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.enableKerning = true;
            tmp.extraPadding = true;
            tmp.fontSize = size;
            tmp.font = font;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            if (auto)
            {
                tmp.fontSizeMin = 4f;
                tmp.fontSizeMax = size;
                tmp.enableAutoSizing = true;
            }
            return tmp;
        }
    }
}
