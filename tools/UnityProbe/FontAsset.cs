// Font assets made at run time from OS fonts ("PROBE ... FA ..."), at the main menu:
// TMP_FontAsset.CreateFontAsset(familyName, styleName[, pointSize]) and
// CreateFontAsset(fontFilePath, faceIndex, samplingPointSize, atlasPadding, renderMode,
// atlasWidth, atlasHeight), as Geyser Tab calls the first. For installed and missing families,
// style names in other cases, empty and null names and files that are not fonts, it logs
// whether an asset comes back (or what is thrown, and what the log received), and the asset's
// name, face, atlas, material, creation settings and tables; then what a TextMeshProUGUI does
// with it: a character the asset has to add, one the face lacks, and the asset's tables
// afterwards. With PROBE_SCENARIO=fontasset only this probe runs.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace SemanticsProbe
{
    public class FontAsset : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly List<string> logged = new List<string>();

        static void L(string s) => Rec.Log("FA " + s);
        static string R(float v) => v.ToString("R", Inv);
        static string Q(string s) => s == null ? "null" : "\"" + s + "\"";

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (!message.StartsWith("PROBE")) logged.Add(type + ": " + message.Replace("\n", "\\n"));
        }

        static void Flush(string label)
        {
            foreach (var m in logged) L(label + " log " + m);
            logged.Clear();
        }

        IEnumerator Run()
        {
            Application.logMessageReceived += OnLog;
            var cgo = new GameObject("FaCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            yield return null;

            L("os fonts " + Font.GetOSInstalledFontNames().Length + " paths " + Font.GetPathsToOSFonts().Length);
            foreach (var n in new[] { "DejaVu Sans", "Segoe UI Symbol", "Arial", "Tahoma", "Malgun Gothic", "FreeSans", "Liberation Sans" })
                L("os name " + Q(n) + " listed=" + Font.GetOSInstalledFontNames().Contains(n));

            // The engine's lookup behind it (internal in the original; by reflection).
            var tryRef = typeof(FontEngine).GetMethod("TryGetSystemFontReference", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            L("TryGetSystemFontReference " + (tryRef != null ? tryRef.ToString() : "missing"));
            foreach (var (family, style) in new[] { ("DejaVu Sans", "Book"), ("DejaVu Sans", ""), ("DejaVu Sans", null), ("DejaVu Sans", "Regular"), ("dejavu sans", "BOLD"),
                         ("FreeSans", ""), ("FreeSans", "Oblique"), ("Loma", ""), ("DejaVu Sans Mono", ""), ("Latin Modern Math", ""), ("IPAGothic", ""), ("IPAゴシック", ""),
                         ("Unifont", ""), ("Courier 10 Pitch", ""), ("Arial", "") })
            {
                if (tryRef == null) break;
                var args = new object[] { family, style, null };
                bool ok;
                try { ok = (bool)tryRef.Invoke(null, args); }
                catch (Exception e) { L("ref " + Q(family) + " " + Q(style) + " threw " + (e.InnerException ?? e).GetType().Name); continue; }
                Flush("ref");
                // FontReference is internal too: its fields by reflection.
                var r = args[2];
                string F(string n) => r?.GetType().GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(r)?.ToString();
                L("ref " + Q(family) + " " + Q(style) + " -> " + ok + " family=" + Q(F("familyName")) + " style=" + Q(F("styleName")) + " faceIndex=" + F("faceIndex") + " path=" + Q(F("filePath")));
            }
            foreach (var family in new[] { "FreeSans", "Loma", "DejaVu Sans Mono", "IPAGothic", "IPAゴシック" })
            {
                var e1 = TMP_FontAsset.CreateFontAsset(family, "", 60);
                Flush("empty");
                L("empty-style " + Q(family) + " -> " + (e1 == null ? "null" : Q(e1.faceInfo.familyName) + " " + Q(e1.faceInfo.styleName)));
            }

            var byName = new (string family, string style, int size)[]
            {
                ("Segoe UI Symbol", "Regular", 60), ("Arial", "Regular", 60), ("Malgun Gothic", "Regular", 60), ("Tahoma", "Regular", 60),
                ("DejaVu Sans", "Book", 60), ("DejaVu Sans", "Regular", 60), ("dejavu sans", "book", 60), ("DEJAVU SANS", "BOOK", 60),
                ("DejaVu Sans", "Bold", 90), ("DejaVu Sans", "", 60), ("DejaVu Sans", null, 60), ("DejaVuSans", "Book", 60),
                ("FreeSans", "Regular", 60), ("FreeSans", "Medium", 60), ("Unifont", "Regular", 16),
                ("", "Regular", 60), (null, "Regular", 60), ("DejaVu Sans", "Book", 0), ("DejaVu Sans", "Book", -5),
            };
            var made = new List<(string, TMP_FontAsset)>();
            foreach (var (family, style, size) in byName)
            {
                string label = "name " + Q(family) + " " + Q(style) + " " + size;
                TMP_FontAsset a = null;
                try { a = TMP_FontAsset.CreateFontAsset(family, style, size); }
                catch (Exception e) { L(label + " threw " + e.GetType().Name + ": " + e.Message); }
                Flush(label);
                Describe(label, a);
                if (a != null) made.Add((label, a));
                yield return null;
            }
            {
                // The default point size (90).
                string label = "name2 \"DejaVu Sans\" \"Book\"";
                TMP_FontAsset a = null;
                try { a = TMP_FontAsset.CreateFontAsset("DejaVu Sans", "Book"); }
                catch (Exception e) { L(label + " threw " + e.GetType().Name + ": " + e.Message); }
                Flush(label);
                Describe(label, a);
                if (a != null) made.Add((label, a));
                // A second call for the same face: a new asset or the same one?
                TMP_FontAsset b = TMP_FontAsset.CreateFontAsset("DejaVu Sans", "Book");
                Flush(label + " again");
                L(label + " again same=" + ReferenceEquals(a, b) + " sameAtlas=" + (a != null && b != null && a.atlasTexture == b.atlasTexture));
            }

            var byPath = new (string path, int face, int size, int pad, GlyphRenderMode mode, int w, int h)[]
            {
                ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 0, 60, 6, GlyphRenderMode.SDFAA, 512, 512),
                ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 0, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024),
                ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 0, 32, 4, GlyphRenderMode.RASTER_HINTED, 256, 256),
                ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 1, 60, 6, GlyphRenderMode.SDFAA, 512, 512),
                ("/usr/share/fonts/truetype/dejavu/NoSuchFont.ttf", 0, 60, 6, GlyphRenderMode.SDFAA, 512, 512),
                ("/etc/hostname", 0, 60, 6, GlyphRenderMode.SDFAA, 512, 512),
                ("", 0, 60, 6, GlyphRenderMode.SDFAA, 512, 512),
            };
            foreach (var p in byPath)
            {
                string label = "path " + Q(p.path) + " " + p.face + " " + p.size + " " + p.pad + " " + p.mode + " " + p.w + "x" + p.h;
                TMP_FontAsset a = null;
                try { a = TMP_FontAsset.CreateFontAsset(p.path, p.face, p.size, p.pad, p.mode, p.w, p.h); }
                catch (Exception e) { L(label + " threw " + e.GetType().Name + ": " + e.Message); }
                Flush(label);
                Describe(label, a);
                if (a != null) made.Add((label, a));
                yield return null;
            }

            // What a text does with them: Geyser Tab's "↻" (U+21BB), "A", and a character DejaVu
            // Sans lacks (U+AC00, a Hangul syllable).
            foreach (var (label, a) in made.Take(3))
            {
                foreach (var text in new[] { "↻", "A", "가", "A↻B" })
                {
                    var go = new GameObject("FaText", typeof(RectTransform));
                    go.transform.SetParent(canvas.transform, false);
                    ((RectTransform)go.transform).sizeDelta = new Vector2(200f, 50f);
                    var t = go.AddComponent<TextMeshProUGUI>();
                    t.font = a;
                    t.fontSize = 16f;
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                    t.text = text;
                    t.ForceMeshUpdate(false, false);
                    Flush(label + " text U+" + ((int)text[0]).ToString("X4"));
                    var info = t.textInfo;
                    var chars = string.Join(" ", Enumerable.Range(0, info.characterCount).Select(i =>
                    {
                        var c = info.characterInfo[i];
                        return "U+" + ((int)c.character).ToString("X4") + ":vis=" + c.isVisible + ":own=" + (c.fontAsset == a) + ":font=" + (c.fontAsset != null ? c.fontAsset.name : "null") + ":adv=" + R(c.xAdvance - c.origin);
                    }));
                    L(label + " text " + string.Join(",", text.Select(c => "U+" + ((int)c).ToString("X4"))) + " pw=" + R(t.preferredWidth) + " ph=" + R(t.preferredHeight) + " count=" + info.characterCount + " " + chars);
                    L(label + " table [" + string.Join(" ", a.characterTable.Select(ch => "U+" + ch.unicode.ToString("X4"))) + "]");
                    L(label + " after chars=" + a.characterTable.Count + " glyphs=" + a.glyphTable.Count + " atlases=" + a.atlasTextures.Length
                      + " atlas0=" + a.atlasTextures[0].width + "x" + a.atlasTextures[0].height);
                    Destroy(go);
                    yield return null;
                }
                bool has = a.HasCharacter('↻');
                bool added = a.TryAddCharacters("↻Z", out string missing);
                Flush(label + " add");
                L(label + " HasCharacter(U+21BB)=" + has + " TryAddCharacters=" + added + " missing=" + Q(missing) + " chars=" + a.characterTable.Count);
                bool addedMissing = a.TryAddCharacters("가", out missing);
                Flush(label + " add missing");
                L(label + " TryAddCharacters(U+AC00)=" + addedMissing + " missing=" + Q(missing) + " chars=" + a.characterTable.Count);
            }
            Application.logMessageReceived -= OnLog;
            L("done");
        }

        static void Describe(string label, TMP_FontAsset a)
        {
            if (a == null) { L(label + " -> null"); return; }
            var fi = a.faceInfo;
            L(label + " -> asset name=" + Q(a.name) + " hideFlags=" + a.hideFlags + " version=" + Q(a.version)
              + " source=" + (a.sourceFontFile != null ? Q(a.sourceFontFile.name) : "null"));
            L(label + " face family=" + Q(fi.familyName) + " style=" + Q(fi.styleName) + " pointSize=" + R(fi.pointSize) + " scale=" + R(fi.scale)
              + " lineHeight=" + R(fi.lineHeight) + " ascent=" + R(fi.ascentLine) + " descent=" + R(fi.descentLine) + " cap=" + R(fi.capLine)
              + " mean=" + R(fi.meanLine) + " baseline=" + R(fi.baseline) + " tab=" + R(fi.tabWidth));
            L(label + " atlas " + a.atlasWidth + "x" + a.atlasHeight + " padding=" + a.atlasPadding + " mode=" + a.atlasRenderMode
              + " population=" + a.atlasPopulationMode + " multi=" + a.isMultiAtlasTexturesEnabled + " textures=" + a.atlasTextures.Length
              + " tex0=" + (a.atlasTextures.Length > 0 && a.atlasTextures[0] != null ? a.atlasTextures[0].width + "x" + a.atlasTextures[0].height + " " + a.atlasTextures[0].format + " name=" + Q(a.atlasTextures[0].name) : "none")
              + " atlasTexture==tex0=" + (a.atlasTextures.Length > 0 && a.atlasTexture == a.atlasTextures[0]));
            var m = a.material;
            L(label + " material " + (m == null ? "null" : "name=" + Q(m.name) + " shader=" + Q(m.shader != null ? m.shader.name : null)
              + " mainTex==atlas=" + (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") == a.atlasTexture)
              + " TextureWidth=" + (m.HasProperty("_TextureWidth") ? R(m.GetFloat("_TextureWidth")) : "-")
              + " TextureHeight=" + (m.HasProperty("_TextureHeight") ? R(m.GetFloat("_TextureHeight")) : "-")
              + " GradientScale=" + (m.HasProperty("_GradientScale") ? R(m.GetFloat("_GradientScale")) : "-")
              + " WeightNormal=" + (m.HasProperty("_WeightNormal") ? R(m.GetFloat("_WeightNormal")) : "-")
              + " WeightBold=" + (m.HasProperty("_WeightBold") ? R(m.GetFloat("_WeightBold")) : "-")));
            var cs = a.creationSettings;
            L(label + " creation guid=" + Q(cs.sourceFontFileGUID) + " faceIndex=" + cs.faceIndex + " pointSizeMode=" + cs.pointSizeSamplingMode
              + " pointSize=" + cs.pointSize + " padding=" + cs.padding + " packing=" + cs.packingMode + " atlas=" + cs.atlasWidth + "x" + cs.atlasHeight
              + " charSetMode=" + cs.characterSetSelectionMode + " renderMode=" + cs.renderMode + " includeKerning=" + cs.includeFontFeatures);
            L(label + " tables chars=" + a.characterTable.Count + " glyphs=" + a.glyphTable.Count + " fallbacks=" + (a.fallbackFontAssetTable == null ? "null" : a.fallbackFontAssetTable.Count.ToString())
              + " normalStyle=" + R(a.normalStyle) + " normalSpacing=" + R(a.normalSpacingOffset) + " boldStyle=" + R(a.boldStyle) + " boldSpacing=" + R(a.boldSpacing)
              + " italicStyle=" + a.italicStyle + " tabSize=" + a.tabSize + " weights=" + (a.fontWeightTable == null ? "null" : a.fontWeightTable.Length.ToString()));
        }
    }
}
