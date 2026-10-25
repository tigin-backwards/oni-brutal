// Text heights in the translations' fonts at full precision ("PROBE ... TH ..."), at the main
// menu: TMP's preferred height and width of short labels in the scripts the Workshop's
// translations use (Japanese, Chinese, Korean, Thai, Cyrillic, Vietnamese and Latin), in the
// game's language fonts (NotoSansCJKjp/sc/kr, NotoSansThai, RobotoCondensed) and NotoSans, at the
// sizes the game's text styles use, set up as SetTextStyleSetting does (kerning, extra padding),
// with and without word wrapping. Each value is logged in "R" format with its float bits and the
// two-decimal form E2eDriver's ui_dump prints, beside the line's ascender, descender and
// baseline, each character's ascender, descender, scale and font, and the font's face values.
// With PROBE_SCENARIO=textheight only this probe runs.
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
    public class TextHeight : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static void L(string s) => Rec.Log("TH " + s);
        static string R(float v) => v.ToString("R", Inv);
        static string Bits(float v) => BitConverter.ToInt32(BitConverter.GetBytes(v), 0).ToString("X8");
        /// E2eDriver's ui_dump form (tools/E2eDriver/Ui.cs F).
        static string Dump(float v) { var s = Math.Round(v, 2).ToString("0.##", Inv); return s == "-0" ? "0" : s; }
        static string All(float v) => R(v) + " bits=" + Bits(v) + " dump=" + Dump(v);

        static readonly string[] Fonts =
        {
            "NotoSansCJKjp-Regular", "NotoSansCJKsc-Regular", "NotoSansCJKkr-Regular", "NotoSansThai-Regular",
            "RobotoCondensed-Regular", "NotoSans-Regular",
        };

        static readonly float[] Sizes = { 12f, 14f, 16f, 18f, 20f, 24f, 28f };

        static readonly string[] Texts =
        {
            // Japanese (the Game options screen's headers and title), Chinese, Korean, Thai.
            "全般", "温度単位", "セーブデータ", "ゲーム設定", "常规", "温度单位", "保存", "일반", "온도 단위", "저장",
            "ทั่วไป", "หน่วยอุณหภูมิ", "บันทึก",
            // Cyrillic, Vietnamese, Latin with accents, Latin.
            "Общие", "Единицы температуры", "Chung", "Đơn vị nhiệt độ", "Général", "Ägypten", "General", "Ag",
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
            var cgo = new GameObject("ThCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            yield return null;
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            foreach (var name in Fonts)
            {
                var font = all.FirstOrDefault(f => f.name == name);
                if (font == null)
                {
                    L("face " + name + " missing");
                    continue;
                }
                var fi = font.faceInfo;
                L("face " + font.name + " pointSize=" + R(fi.pointSize) + " scale=" + R(fi.scale) + " lineHeight=" + R(fi.lineHeight)
                  + " ascent=" + R(fi.ascentLine) + " descent=" + R(fi.descentLine) + " capLine=" + R(fi.capLine) + " meanLine=" + R(fi.meanLine)
                  + " baseline=" + R(fi.baseline) + " underline=" + R(fi.underlineOffset) + " padding=" + R(font.atlasPadding)
                  + " atlas=" + font.atlasWidth + "x" + font.atlasHeight + " mode=" + font.atlasPopulationMode
                  + " fallbacks=" + string.Join(",", (font.fallbackFontAssetTable ?? new System.Collections.Generic.List<TMP_FontAsset>()).Select(f => f == null ? "null" : f.name)));
                foreach (float size in Sizes)
                    yield return Measure(canvas.transform, font, size);
            }
            Destroy(cgo);
            var done = new GameObject("TextHeightProbeDone", typeof(RectTransform));
            DontDestroyOnLoad(done);
            done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            L("=== done");
        }

        IEnumerator Measure(Transform canvas, TMP_FontAsset font, float size)
        {
            // The 400 x 60 rect's top at y 30 (pivot 0.5), at 0 (pivot 1) and at 22.2 (pivot 0.63),
            // the last two labelled "@1" and "@0.63".
            foreach (var (wrap, py) in new[] { (false, 0.5f), (true, 0.5f), (false, 1f), (false, 0.63f) })
                foreach (var text in Texts)
                {
                    string label = font.name + " " + R(size) + (wrap ? " wrap" : "") + (py == 0.5f ? "" : "@" + R(py)) + " \"" + text + "\"";
                    var go = new GameObject("Th", typeof(RectTransform));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(canvas, false);
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, py);
                    rt.sizeDelta = new Vector2(400, 60);
                    var tmp = go.AddComponent<TextMeshProUGUI>();
                    tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                    tmp.enableKerning = true;
                    tmp.extraPadding = true;
                    tmp.alignment = TextAlignmentOptions.TopLeft;
                    tmp.fontSize = size;
                    tmp.font = font;
                    tmp.text = text;
                    L(label + " preferredHeight=" + All(tmp.preferredHeight) + " preferredWidth=" + All(tmp.preferredWidth));
                    var pv = tmp.GetPreferredValues(text, 400, 60);
                    L(label + " GetPreferredValues(400,60)=" + All(pv.x) + "," + All(pv.y));
                    tmp.ForceMeshUpdate();
                    var info = tmp.textInfo;
                    if (info.lineCount > 0)
                    {
                        var line = info.lineInfo[0];
                        L(label + " line0 ascender=" + All(line.ascender) + " descender=" + All(line.descender) + " baseline=" + R(line.baseline)
                          + " lineHeight=" + R(line.lineHeight)
                          + " lines=" + info.lineCount);
                    }
                    var sb = new StringBuilder();
                    for (int i = 0; i < info.characterCount; i++)
                    {
                        var ch = info.characterInfo[i];
                        sb.Append(' ').Append((int)ch.character).Append(':').Append(R(ch.ascender)).Append('/').Append(R(ch.descender))
                          .Append('/').Append(R(ch.scale)).Append('/').Append(R(ch.pointSize)).Append('/').Append(ch.fontAsset == null ? "null" : ch.fontAsset.name)
                          .Append('/').Append(ch.isVisible ? 'v' : '-');
                    }
                    L(label + " chars" + sb);
                    var bounds = tmp.textBounds;
                    L(label + " textBounds y=" + R(bounds.min.y) + ".." + R(bounds.max.y) + " rendered=" + All(tmp.renderedHeight));
                    Destroy(go);
                }
            yield return null;
        }
    }
}
