// Texts as three Workshop mods build them ("PROBE ... MX ..."), at the main menu, about ten
// seconds. With PROBE_SCENARIO=modtext only this probe runs; PROBE_MX_PART=font, ellipsis or
// lines runs one part.
// - font (Smart Coolers, 2990501709, through PLib): a text whose font is set to null, on a
//   TextMeshProUGUI before and after it wakes, on a laid-out one, and on a LocText given a
//   TextStyleSetting made with `new` (no sdfFont) as PLib's AddLocText does. Logs the font and
//   material read back and the layout (character count, preferred size) after ForceMeshUpdate
//   and after a frame.
// - ellipsis (Rookie Quest Guide, 3738243630): short labels that fit their rect in the default
//   font, Bold, with each overflow mode: the material count and the sub-meshes made, with their
//   fonts. Then the default font's fallbacks and whether each holds U+2026.
// - lines (Social Life, 3775109476; Rookie Quest Guide's disclaimer): wrapped texts of several
//   lines with characters from fallback fonts: the preferred height to the bit, each line's
//   ascender, descender, line height and baseline, and each character from a fallback.
// A MxProbeDone object appears at the end, for a spec's ui_wait.
#pragma warning disable CS0618 // enableKerning
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class ModText : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string Part = System.Environment.GetEnvironmentVariable("PROBE_MX_PART");
        static bool P(string name) => string.IsNullOrEmpty(Part) || Part.Split(',').Contains(name);

        static void L(string s) => Rec.Log("MX " + s);
        static string R(float v) => v.ToString("R", Inv);
        static string Bits(float v) => BitConverter.ToInt32(BitConverter.GetBytes(v), 0).ToString("X8");
        static string N(UnityEngine.Object o) => o == null ? "null" : o.name;

        static string Hex(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                sb.Append(c >= 0x20 && c < 0x7F && c != '"' && c != '\\' ? c.ToString() : "\\u" + ((int)c).ToString("X4"));
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
            var cgo = new GameObject("MxCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            yield return null;
            L("default font=" + N(TMP_Settings.defaultFontAsset) + " fallbacks="
              + string.Join(",", (TMP_Settings.fallbackFontAssets ?? new System.Collections.Generic.List<TMP_FontAsset>()).Select(N)));
            foreach (var name in new[] { "LiberationSans SDF", "Roboto-Bold SDF", "GRAYSTROKE REGULAR SDF", "NotoSans-Regular", "NotoNaskhArabic-Regular", "NotoSansCJKjp-Regular", "NotoSansCJKsc-Regular" })
            {
                var f = Find(name);
                if (f == null) { L("face " + name + " missing"); continue; }
                var fi = f.faceInfo;
                L("face " + name + " pointSize=" + R(fi.pointSize) + " scale=" + R(fi.scale) + " lineHeight=" + R(fi.lineHeight)
                  + " ascent=" + R(fi.ascentLine) + " descent=" + R(fi.descentLine) + " fallbacks="
                  + string.Join(",", (f.fallbackFontAssetTable ?? new System.Collections.Generic.List<TMP_FontAsset>()).Select(N))
                  + " bold=" + (f.fontWeightTable != null && f.fontWeightTable.Length > 7 ? N(f.fontWeightTable[7].regularTypeface) : "none"));
            }
            if (P("font")) yield return StartCoroutine(Fonts(cgo.transform));
            if (P("ellipsis")) yield return StartCoroutine(Ellipsis(cgo.transform));
            if (P("lines")) yield return StartCoroutine(Lines(cgo.transform));
            if (P("pref")) yield return StartCoroutine(Pref(cgo.transform));
            if (P("bold")) yield return StartCoroutine(Bold(cgo.transform));
            Destroy(cgo);
            L("=== done");
            var done = new GameObject("MxProbeDone", typeof(RectTransform));
            DontDestroyOnLoad(done);
        }

        // ----- font -----

        static string State(TMP_Text t)
        {
            var info = t.textInfo;
            return "font=" + N(t.font) + " mat=" + N(t.fontSharedMaterial) + " cc=" + (info == null ? -1 : info.characterCount)
                + " lc=" + (info == null ? -1 : info.lineCount) + " mc=" + (info == null ? -1 : info.materialCount)
                + " pw=" + R(t.preferredWidth) + " ph=" + R(t.preferredHeight) + " size=" + R(t.fontSize) + " style=" + (int)t.fontStyle
                + " rect=" + R(t.rectTransform.rect.width) + "x" + R(t.rectTransform.rect.height);
        }

        static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static TMP_FontAsset Find(string name) => Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == name);

        static void SetField(object o, string name, object value)
        {
            var f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) { L("field " + name + " missing"); return; }
            f.SetValue(o, value);
        }

        IEnumerator Fonts(Transform root)
        {
            var noto = Find("NotoSans-Regular");
            // 1: an awake TextMeshProUGUI given font = null, then text.
            {
                var go = Child(root, "F1");
                var t = go.AddComponent<TextMeshProUGUI>();
                L("font f1 awake " + State(t));
                t.font = null;
                L("font f1 nulled " + State(t));
                t.text = "Cool Contents";
                t.ForceMeshUpdate();
                L("font f1 forced " + State(t));
                yield return null;
                L("font f1 frame " + State(t));
                t.font = noto;
                t.ForceMeshUpdate();
                L("font f1 noto " + State(t));
                Destroy(go);
            }
            // 2: font = null before the text wakes (the object inactive), then activation.
            {
                var go = Child(root, "F2");
                go.SetActive(false);
                var t = go.AddComponent<TextMeshProUGUI>();
                t.font = null;
                t.text = "Cool Contents";
                L("font f2 inactive " + "font=" + N(t.font));
                go.SetActive(true);
                L("font f2 active " + State(t));
                t.ForceMeshUpdate();
                L("font f2 forced " + State(t));
                yield return null;
                L("font f2 frame " + State(t));
                Destroy(go);
            }
            // 3: a laid-out text whose font is then set to null.
            {
                var go = Child(root, "F3");
                var t = go.AddComponent<TextMeshProUGUI>();
                t.font = noto;
                t.text = "Cool Contents";
                t.ForceMeshUpdate();
                L("font f3 laid " + State(t));
                t.font = null;
                L("font f3 nulled " + State(t));
                t.ForceMeshUpdate();
                L("font f3 forced " + State(t));
                yield return null;
                L("font f3 frame " + State(t));
                Destroy(go);
            }
            // 4 and 5: PLib's AddLocText with a TextStyleSetting made with new (Smart Coolers'
            // labelTextStyleSmall), then PTextComponent's TextChildHelper (alignment, text); in
            // 5 the style has NotoSans as its sdfFont.
            foreach (var withFont in new[] { false, true })
            {
                string tag = withFont ? "f5" : "f4";
                var style = new TextStyleSetting { fontSize = 14, textColor = Color.black, style = FontStyles.Bold, enableWordWrapping = false };
                if (withFont) style.sdfFont = noto;
                var go = Child(root, "Text");
                bool was = go.activeSelf;
                go.SetActive(false);
                var t = go.AddComponent<LocText>();
                SetField(t, "key", string.Empty);
                SetField(t, "textStyleSetting", style);
                L("font " + tag + " added font=" + N(t.font));
                go.SetActive(was);
                L("font " + tag + " active " + State(t));
                t.alignment = TextAlignmentOptions.Center;
                t.text = "Cool Contents";
                t.ForceMeshUpdate();
                L("font " + tag + " forced " + State(t));
                yield return null;
                L("font " + tag + " frame " + State(t));
                Destroy(go);
            }
            // 6: TMP_Text.font = null on a LocText with the game's default style, as a control.
            {
                var go = Child(root, "F6");
                go.SetActive(false);
                var t = go.AddComponent<LocText>();
                SetField(t, "key", string.Empty);
                go.SetActive(true);
                L("font f6 active " + State(t));
                t.font = null;
                t.text = "Cool Contents";
                t.ForceMeshUpdate();
                L("font f6 forced " + State(t));
                Destroy(go);
            }
        }

        // ----- ellipsis -----

        static string Subs(TMP_Text t)
        {
            var sb = new StringBuilder();
            foreach (var s in t.GetComponentsInChildren<TMP_SubMeshUI>(true))
            {
                if (sb.Length > 0) sb.Append(';');
                var m = s.mesh;
                sb.Append(N(s.fontAsset)).Append(s.gameObject.activeSelf ? "" : "/inactive").Append(s.enabled ? "" : "/disabled")
                  .Append(" v=").Append(m == null ? -1 : m.vertexCount).Append(" mat=").Append(N(s.sharedMaterial));
            }
            return "subs=[" + sb + "]";
        }

        static string Meshes(TMP_Text t)
        {
            var info = t.textInfo;
            var sb = new StringBuilder();
            for (int i = 0; i < info.materialCount && i < info.meshInfo.Length; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(N(info.meshInfo[i].material)).Append(" verts=").Append(info.meshInfo[i].vertexCount);
            }
            return "meshes=[" + sb + "]";
        }

        IEnumerator Ellipsis(Transform root)
        {
            var def = TMP_Settings.defaultFontAsset;
            foreach (var f in new[] { def }.Concat(def != null && def.fallbackFontAssetTable != null ? def.fallbackFontAssetTable : new System.Collections.Generic.List<TMP_FontAsset>())
                         .Concat(TMP_Settings.fallbackFontAssets ?? new System.Collections.Generic.List<TMP_FontAsset>()))
            {
                if (f == null) continue;
                L("ellipsis font " + f.name + " has2026=" + f.HasCharacter('…') + " has2026fb=" + f.HasCharacter('…', true)
                  + " has2E=" + f.HasCharacter('.') + " dynamic=" + f.atlasPopulationMode);
            }
            string[] texts = { "Pitcher Pump", "Outhouse", "AB", "▪ Pitcher Pump", "" };
            foreach (var mode in new[] { TextOverflowModes.Overflow, TextOverflowModes.Ellipsis, TextOverflowModes.Truncate })
                foreach (var bold in new[] { true, false })
                    foreach (var w in new[] { 238f, 40f })
                        foreach (var text in texts)
                        {
                            // As the mod's CreateText and MakeFoldoutHeader: a new label, text
                            // set before anything else, Bold, then the mode.
                            var head = Child(root, "MaterialHead");
                            var go = Child(head.transform, "Label");
                            var t = go.AddComponent<TextMeshProUGUI>();
                            t.text = text;
                            t.fontSize = 12;
                            t.alignment = TextAlignmentOptions.MidlineLeft;
                            t.raycastTarget = false;
                            if (bold) t.fontStyle = FontStyles.Bold;
                            t.overflowMode = mode;
                            ((RectTransform)go.transform).sizeDelta = new Vector2(w, 22f);
                            string tag = mode + " bold=" + (bold ? 1 : 0) + " w=" + R(w) + " \"" + Hex(text) + "\"";
                            t.ForceMeshUpdate();
                            L("ellipsis forced " + tag + " " + State(t) + " ov=" + (t.isTextOverflowing ? 1 : 0) + " " + Meshes(t) + " " + Subs(t));
                            yield return null;
                            L("ellipsis frame " + tag + " " + State(t) + " " + Meshes(t) + " " + Subs(t));
                            Destroy(head);
                        }
        }

        // ----- lines -----

        static string LineInfo(TMP_TextInfo info)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < info.lineCount; k++)
            {
                var li = info.lineInfo[k];
                if (k > 0) sb.Append(';');
                sb.Append(li.firstCharacterIndex).Append('-').Append(li.lastCharacterIndex).Append(':')
                  .Append(R(li.ascender)).Append(',').Append(R(li.descender)).Append(',').Append(R(li.lineHeight)).Append(',').Append(R(li.baseline))
                  .Append(",asc=").Append(R(li.lineExtents.max.y)).Append('/').Append(R(li.lineExtents.min.y));
            }
            return sb.ToString();
        }

        static string Fallbacks(TMP_Text t)
        {
            var info = t.textInfo;
            var sb = new StringBuilder();
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (ch.fontAsset == t.font && ch.character != ' ' && ch.character != '\n') continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(i).Append(':').Append(((int)ch.character).ToString("X")).Append(':').Append(N(ch.fontAsset)).Append(':')
                  .Append(R(ch.ascender)).Append(',').Append(R(ch.descender)).Append(',').Append(R(ch.baseLine)).Append(",s").Append(R(ch.scale))
                  .Append(",l").Append(ch.lineNumber);
            }
            return sb.ToString();
        }

        static void LogLines(string tag, TMP_Text t, float w)
        {
            ((RectTransform)t.transform).sizeDelta = new Vector2(w, 0f);
            t.ForceMeshUpdate();
            float ph = t.preferredHeight;
            var pv = t.GetPreferredValues(w, 0f);
            L("lines " + tag + " font=" + N(t.font) + " size=" + R(t.fontSize) + " w=" + R(w) + " ph=" + R(ph) + " bits=" + Bits(ph)
              + " pv=" + R(pv.x) + "," + R(pv.y) + " lc=" + t.textInfo.lineCount + " text=\"" + Hex(t.text) + "\"");
            ((RectTransform)t.transform).sizeDelta = new Vector2(w, 1000f);
            t.ForceMeshUpdate();
            L("lines " + tag + " metrics lines=" + LineInfo(t.textInfo) + " fb=" + Fallbacks(t));
        }

        // Two- and three-line texts whose first line takes a taller glyph from a fallback font
        // (U+00B7 from NotoNaskhArabic in GRAYSTROKE 13 pt; U+514D from NotoSansCJKjp in the
        // default font 9 pt), with the tall glyph first, last or between, with and without
        // white space, the last line in the primary font or not; wrapping on and off.
        IEnumerator Pref(Transform root)
        {
            var cases = new[] { ("GRAYSTROKE REGULAR SDF", 13f, "\u00B7"), ("LiberationSans SDF", 9f, "免") };
            string[] firsts = { "T", "AT", "TA", "ATA", "A T", "T A", "A T A", "AAT", "TAA", "A T 0", "T T", "AT T", "A  T", "AT ", " T" };
            string[] rest = { "\nB", "\nBB", "\nB T", "\nT", "\nB\nB", "\nT\nB", "\nB\nT" };
            // PROBE_MX_PREF=2: texts that tell apart where the preferred height's extra comes from.
            string[] second =
            {
                "T\nBB", "T\nB B", "T\nB ", "T\n B", "T\nBBB", "T\nBBT", "T\nBTB", "T\nTB", "T\nTT", "TB\nBB",
                "T\nB\nBB", "T\nBB\nB", "B\nT\nBB", "T\nBB\nBB", "BB\nT\nB", "BB\nT\nBB", "B\nBB", "BB\nBB",
                "T\n\nBB", "T\nB\nB\nBB", "<size=150%>B</size>\nBB", "<size=150%>B</size>\nB", "B<voffset=0.5em>B</voffset>\nBB",
                "T\nB<size=150%>B</size>", "T\nB<size=60%>B</size>",
            };
            if (System.Environment.GetEnvironmentVariable("PROBE_MX_PREF") == "2")
            {
                firsts = second;
                rest = new[] { "" };
            }
            foreach (var (fontName, size, tall) in cases)
            {
                var font = Find(fontName);
                if (font == null) { L("pref font " + fontName + " missing"); continue; }
                var go = new GameObject("Pref", typeof(RectTransform));
                go.transform.SetParent(root, false);
                var t = go.AddComponent<TextMeshProUGUI>();
                t.font = font;
                t.fontSize = size;
                t.alignment = TextAlignmentOptions.TopLeft;
                foreach (var wrap in new[] { true, false })
                {
                    t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                    foreach (var f in firsts)
                        foreach (var r in rest)
                        {
                            t.text = (f + r).Replace("T", tall);
                            ((RectTransform)t.transform).sizeDelta = new Vector2(740f, 0f);
                            t.ForceMeshUpdate();
                            float ph0 = t.preferredHeight;
                            ((RectTransform)t.transform).sizeDelta = new Vector2(740f, 1000f);
                            t.ForceMeshUpdate();
                            float ph1 = t.preferredHeight;
                            var info = t.textInfo;
                            float top = info.lineCount > 0 ? info.lineInfo[0].ascender : 0f;
                            float bottom = info.lineCount > 0 ? info.lineInfo[info.lineCount - 1].descender : 0f;
                            var cs = new StringBuilder();
                            for (int i = 0; i < info.characterCount; i++)
                            {
                                var ch = info.characterInfo[i];
                                cs.Append(i == 0 ? "" : ";").Append(((int)ch.character).ToString("X")).Append(':').Append(R(ch.ascender)).Append(',')
                                  .Append(R(ch.descender)).Append(',').Append(R(ch.baseLine)).Append(",l").Append(ch.lineNumber);
                            }
                            L("pref " + fontName + " " + R(size) + " wrap=" + (wrap ? 1 : 0) + " \"" + Hex(t.text) + "\" ph0=" + R(ph0) + " ph1=" + R(ph1)
                              + " ext=" + R(top - bottom) + " lines=" + LineInfo(info) + " chars=" + cs);
                        }
                    yield return null;
                }
                Destroy(go);
            }
        }

        // Bold and italic in the default font: which font asset each character, and the
        // ellipsis of an Ellipsis text, comes from; the weight tables of the fonts involved.
        IEnumerator Bold(Transform root)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>().OrderBy(f => f.name))
            {
                var wt = f.fontWeightTable;
                if (wt == null) continue;
                var sb = new StringBuilder();
                for (int i = 0; i < wt.Length; i++)
                    if (wt[i].regularTypeface != null || wt[i].italicTypeface != null)
                        sb.Append(' ').Append(i).Append('=').Append(N(wt[i].regularTypeface)).Append('/').Append(N(wt[i].italicTypeface));
                if (sb.Length > 0) L("bold weights " + f.name + sb);
            }
            L("bold globals " + string.Join(",", (TMP_Settings.fallbackFontAssets ?? new System.Collections.Generic.List<TMP_FontAsset>()).Select(N)));
            string[] texts = { "A…", "…", "AB", "A▪", "Pitcher Pump" };
            foreach (var style in new[] { FontStyles.Normal, FontStyles.Bold, FontStyles.Italic, FontStyles.Bold | FontStyles.Italic })
                foreach (var mode in new[] { TextOverflowModes.Overflow, TextOverflowModes.Ellipsis })
                    foreach (var w in new[] { 238f, 30f })
                        foreach (var text in texts)
                        {
                            var go = Child(root, "Bold");
                            var t = go.AddComponent<TextMeshProUGUI>();
                            t.text = text;
                            t.fontSize = 12;
                            t.fontStyle = style;
                            t.overflowMode = mode;
                            ((RectTransform)go.transform).sizeDelta = new Vector2(w, 22f);
                            t.ForceMeshUpdate();
                            var info = t.textInfo;
                            var sb = new StringBuilder();
                            for (int i = 0; i < info.characterCount; i++)
                            {
                                var ch = info.characterInfo[i];
                                sb.Append(i == 0 ? "" : ";").Append(((int)ch.character).ToString("X")).Append(':').Append(N(ch.fontAsset))
                                  .Append(":m").Append(ch.materialReferenceIndex).Append(ch.isVisible ? "" : ":hidden").Append(":x").Append(R(ch.xAdvance - ch.origin));
                            }
                            L("bold " + (int)style + " " + mode + " w=" + R(w) + " \"" + Hex(text) + "\" cc=" + info.characterCount + " mc=" + info.materialCount
                              + " pw=" + R(t.preferredWidth) + " chars=" + sb + " " + Subs(t));
                            Destroy(go);
                        }
            yield return null;
        }

        IEnumerator Lines(Transform root)
        {
            const string summary = "Couples: 0 · Cold wars: 0 · Best friends: 0\nColony: Neutral (0)\nLiving duplicants: 0\nRelationships: 0\nMatches: 0 · Blocks: 0";
            string[] social =
            {
                summary,
                summary.Replace('·', '-'),
                "Couples: 0 · Cold wars: 0",
                "Couples: 0 - Cold wars: 0",
                "Couples: 0 · Cold wars: 0\nColony: Neutral (0)",
                "Colony: Neutral (0)\nCouples: 0 · Cold wars: 0",
                "Colony: Neutral (0)\nLiving duplicants: 0\nMatches: 0 · Blocks: 0",
                "A\nB\nC\nD\nE",
                "A·\nB\nC\nD\nE",
                "A\nB\nC\nD\nE·",
                "A·\nB\nC\nD\nE·",
                "·",
                "A\n·",
                "A·\nB",
                "A\nB",
            };
            var fonts = new[] { "NotoSans-Regular", "GRAYSTROKE REGULAR SDF" }.Select(Find).Where(f => f != null).ToList();
            var confirm = ScreenPrefabs.Instance != null && ScreenPrefabs.Instance.ConfirmDialogScreen != null
                ? ScreenPrefabs.Instance.ConfirmDialogScreen.GetComponentInChildren<LocText>(true) : null;
            L("lines confirm-dialog font=" + N(confirm != null ? confirm.font : null));
            if (confirm != null && confirm.font != null && !fonts.Contains(confirm.font)) fonts.Add(confirm.font);
            foreach (var font in fonts)
                foreach (var text in social)
                {
                    // As Social Life's MakeText: an inactive object, a LocText given the font,
                    // size, colour, wrapping and alignment, then activated; the text after.
                    var go = new GameObject("Summary", typeof(RectTransform));
                    go.SetActive(false);
                    go.transform.SetParent(root, false);
                    var t = go.AddComponent<LocText>();
                    t.key = "";
                    t.allowOverride = true;
                    t.font = font;
                    t.fontSize = 13f;
                    t.color = new Color(0.17f, 0.19f, 0.235f);
                    t.textWrappingMode = TextWrappingModes.Normal;
                    t.alignment = TextAlignmentOptions.TopLeft;
                    t.raycastTarget = false;
                    t.text = "";
                    go.SetActive(true);
                    t.text = text;
                    LogLines("social", t, 740f);
                    Destroy(go);
                    yield return null;
                }
            // Rookie Quest Guide's disclaimer: TextMeshProUGUI in the default font, 9 pt, top
            // left, wrapping, 1212 wide.
            string[] disclaimer =
            {
                "免责声明：本模组为无偿、非商业的教学辅助工具；任务内容仅对B站UP主「大叔追云彩」《缺氧新手活过100天》系列视频作提纲式引用，不含画面/音频/完整文案转载，著作权归原作者；本模组与 Klei Entertainment 无隶属关系，不修改付费内容；若涉侵权，请通过工坊留言或 GitHub Issues 联系，本人将立即删除相应功能。",
                "Disclaimer: This is a free, non-commercial educational aid. Quest texts briefly reference (in outline form only) the video series by Bilibili creator \"Uncle Chasing Clouds\"; no footage, audio or full transcripts are reproduced and all rights belong to the creator. Not affiliated with Klei Entertainment; no paid-content modification. If any rights holder claims infringement, contact me via Workshop comments or GitHub Issues and the feature will be removed immediately.",
                "免责声明：本模组为无偿",
                "免责声明\n免责声明",
                "AB\n免责声明",
                "免责声明\nAB",
                "AB\nAB",
                "B站UP主「大叔」\nKlei",
                "免\nAB",
                "责\nAB",
                "AB\n责",
                "免\n免",
                "责\n责",
            };
            foreach (var text in disclaimer)
                foreach (var w in new[] { 1212f, 400f })
                {
                    var go = new GameObject("DisclaimerCn", typeof(RectTransform));
                    go.transform.SetParent(root, false);
                    var t = go.AddComponent<TextMeshProUGUI>();
                    t.text = text;
                    t.fontSize = 9;
                    t.alignment = TextAlignmentOptions.TopLeft;
                    t.raycastTarget = false;
                    LogLines("disclaimer", t, w);
                    Destroy(go);
                    yield return null;
                }
        }
    }
}
