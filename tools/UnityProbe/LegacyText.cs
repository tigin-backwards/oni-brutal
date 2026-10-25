// Legacy uGUI Text and TextGenerator glyph placement with a dynamic font ("PROBE ... LX ..."), at
// the main menu: the built-in font (Resources.GetBuiltinResource<Font>("Arial.ttf"), as Geyser
// Swapper's converter panel uses it). With PROBE_SCENARIO=legacytext only this probe runs.
//
//   LX font ...      the font's own values and its texture.
//   LX ci ...        Font.GetCharacterInfo after RequestCharactersInTexture, per pixel size and
//                    style: the int advance, the obsolete float width, the glyph box and its UVs.
//   LX gen ...       TextGenerator.Populate at a font size and scale factor (pixel sizes 5 to
//                    25, integer and not), per string: the preferred width, the size used, each
//                    character's cursor x and width (UICharInfo), the quads' x and y and UVs.
//   LX raster ...    which pixel sizes a glyph was rendered at after one generation of a
//                    character never requested before (GetCharacterInfo without a request).
//   LX gen ... oob=  the anchors with updateBounds and generateOutOfBounds on and off.
//   LX align ...     a line's start and the block's top in extents in eighths of a pixel.
//   LX os...         fonts made from OS font names (a glyph signature tells the face), and
//                    characters the default face lacks.
//   LX text ...      uGUI Text on canvases at scale 0.86, 1 and others, pixel perfect or not,
//                    alignment, wrap, best fit: preferredWidth, the generator's vertices and
//                    the mesh the Text hands the CanvasRenderer (a mesh modifier's view).
//
// tools/UnityProbe/lx-check.py reads the lines; docs/cleanroom/ugui.md ("Legacy Text glyph
// placement") records what it measured.
#pragma warning disable CS0618 // CharacterInfo.width, the float advance
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class LegacyText : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static void L(string s) => Rec.Log("LX " + s);
        static string R(float v) => v.ToString("R", Inv);
        static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

        /// The converter panel's labels (GeyserSwapperUI.CreateLabel: 13, 11 and 10 points), then
        /// strings with pairs that kern in Liberation Sans, narrow and wide letters.
        internal static readonly string[] Strings =
        {
            "Geyser Converter", "Choose a target type and start conversion.", "Target type", "Select type",
            "Start conversion", "Upgrade", "Steam Vent", "Cool Steam Vent", "Mode: Tuning",
            "AVAWAYTaToLTPAFaVa", "iiiiiiiiiijjjjllll", "WWWWWMMMMM", "The quick brown fox jumps over the lazy dog 0123456789",
            "a b  c", "1,234.5 kg/s (60%)",
        };

        static readonly float[] Scales = { 0.5f, 0.6f, 0.75f, 0.8f, 0.86f, 0.9f, 1f, 1.1f, 1.25f, 1.5f, 2f };

        Font font;

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
            string part = Environment.GetEnvironmentVariable("PROBE_LX_PART") ?? "";
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            Font.textureRebuilt += f => L("textureRebuilt " + (f == null ? "null" : f.name) + " frame=" + Time.frameCount);
            FontFacts();
            foreach (var c in FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).OrderBy(c => c.name, StringComparer.Ordinal))
                L("rootcanvas " + c.name + " scale=" + R(c.scaleFactor) + " pixelPerfect=" + c.pixelPerfect + " mode=" + c.renderMode);
            var parts = part == "" ? new[] { "raster", "ci", "gen", "anchors", "align", "fonts", "text" } : part.Split(',');
            // Each part runs alone, so a player without CharacterInfo (a TypeLoadException when
            // a part's method is compiled) still runs the others.
            foreach (var p in parts)
            {
                if (p == "text") continue;
                try
                {
                    if (p == "raster") Raster();
                    else if (p == "ci") CharInfos();
                    else if (p == "gen") Generations();
                    else if (p == "anchors") Anchors();
                    else if (p == "align") Align();
                    else if (p == "fonts") OsFonts();
                }
                catch (Exception e)
                {
                    L("part " + p + " threw " + e.GetType().Name + ": " + e.Message);
                }
            }
            if (parts.Contains("text")) yield return Texts();
            FontFacts();
            L("=== done");
        }

        void FontFacts()
        {
            var tex = font.material != null ? font.material.mainTexture : null;
            L("font name=" + font.name + " dynamic=" + font.dynamic + " fontSize=" + font.fontSize + " lineHeight=" + font.lineHeight + " ascent=" + font.ascent
              + " names=" + string.Join("|", font.fontNames ?? new string[0]) + " texture=" + (tex == null ? "null" : tex.width + "x" + tex.height)
              + " material=" + (font.material == null ? "null" : font.material.name));
        }

        static string Ci(CharacterInfo ci) =>
            "adv=" + ci.advance + " width=" + R(ci.width) + " minX=" + ci.minX + " maxX=" + ci.maxX + " minY=" + ci.minY + " maxY=" + ci.maxY
            + " gw=" + ci.glyphWidth + " gh=" + ci.glyphHeight + " bearing=" + ci.bearing + " size=" + ci.size + " style=" + ci.style
            + " uvBL=" + R(ci.uvBottomLeft.x) + "," + R(ci.uvBottomLeft.y) + " uvTR=" + R(ci.uvTopRight.x) + "," + R(ci.uvTopRight.y)
            + " uvTL=" + R(ci.uvTopLeft.x) + "," + R(ci.uvTopLeft.y);

        /// Unique characters (Cyrillic, then Greek) that nothing else here requests, one per case.
        static IEnumerable<char> Unused()
        {
            for (char c = 'А'; c <= 'я'; c++) yield return c;
            for (char c = 'Α'; c <= 'Ω'; c++) if (c != '΢') yield return c;
            for (char c = 'α'; c <= 'ω'; c++) yield return c;
        }

        /// One generation of a character never requested, then which pixel sizes (1 to 48) and
        /// styles hold it in the texture, and the size whose UVs its quad uses.
        void Raster()
        {
            L("=== raster");
            var unused = Unused().GetEnumerator();
            var cases = new List<(int size, float scale, FontStyle style, bool viaText)>();
            foreach (var (size, scale) in new[] { (11, 0.86f), (10, 0.86f), (13, 0.86f), (12, 0.86f), (14, 0.86f), (11, 1f), (9, 1f), (8, 1f), (11, 0.5f), (11, 0.75f),
                                                   (11, 1.5f), (11, 2f), (10, 0.95f), (10, 0.85f), (20, 0.43f), (19, 0.5f), (9, 0.99f), (9, 1.01f), (7, 1.3f), (16, 0.86f) })
                cases.Add((size, scale, FontStyle.Normal, false));
            cases.Add((11, 0.86f, FontStyle.Bold, false));
            cases.Add((11, 0.86f, FontStyle.Italic, false));
            foreach (var (size, scale, style, _) in cases)
            {
                unused.MoveNext();
                char c = unused.Current;
                var s = Settings(size, scale, style, TextAnchor.UpperLeft, HorizontalWrapMode.Overflow);
                var gen = new TextGenerator();
                gen.Populate(c.ToString(), s);
                var held = new List<string>();
                foreach (FontStyle st in new[] { FontStyle.Normal, FontStyle.Bold, FontStyle.Italic, FontStyle.BoldAndItalic })
                    for (int k = 1; k <= 48; k++)
                        if (font.GetCharacterInfo(c, out var ci, k, st)) held.Add(k + (st == FontStyle.Normal ? "" : ":" + st) + "(adv=" + ci.advance + ",w=" + R(ci.width) + ")");
                string quad = gen.vertexCount >= 4 ? " quadUV=" + R(gen.verts[0].uv0.x) + "," + R(gen.verts[0].uv0.y) + ".." + R(gen.verts[2].uv0.x) + "," + R(gen.verts[2].uv0.y)
                    + " quadX=" + R(gen.verts[0].position.x) + ".." + R(gen.verts[1].position.x) + " quadY=" + R(gen.verts[2].position.y) + ".." + R(gen.verts[0].position.y) : " noquad";
                L("raster fontSize=" + size + " scale=" + R(scale) + " style=" + style + " c=U+" + ((int)c).ToString("X4") + " used=" + gen.fontSizeUsedForBestFit
                  + " charWidth=" + (gen.characterCount > 0 ? R(gen.characters[0].charWidth) : "-") + quad + " held=" + string.Join(" ", held));
            }
        }

        void CharInfos()
        {
            L("=== ci");
            var ascii = new StringBuilder();
            for (char c = ' '; c <= '~'; c++) ascii.Append(c);
            string all = ascii.ToString();
            foreach (FontStyle style in new[] { FontStyle.Normal, FontStyle.Bold, FontStyle.Italic, FontStyle.BoldAndItalic })
                for (int size = 5; size <= 26; size++)
                {
                    string chars = style == FontStyle.Normal || style == FontStyle.Bold ? all : "AVWaeilmoT .,";
                    font.RequestCharactersInTexture(chars, size, style);
                    foreach (char c in chars)
                    {
                        bool ok = font.GetCharacterInfo(c, out var ci, size, style);
                        L("ci size=" + size + " style=" + style + " c=" + (int)c + " ok=" + ok + " " + Ci(ci));
                    }
                }
            // Size 0: the font's own size.
            font.RequestCharactersInTexture("AVa", 0, FontStyle.Normal);
            foreach (char c in "AVa")
                L("ci size=0 style=Normal c=" + (int)c + " ok=" + font.GetCharacterInfo(c, out var ci0, 0, FontStyle.Normal) + " " + Ci(ci0));
        }

        TextGenerationSettings Settings(int size, float scale, FontStyle style, TextAnchor anchor, HorizontalWrapMode wrap, float width = 4000f, float height = 400f)
        {
            return new TextGenerationSettings
            {
                font = font, color = Color.white, fontSize = size, lineSpacing = 1f, richText = true, scaleFactor = scale, fontStyle = style,
                textAnchor = anchor, alignByGeometry = false, resizeTextForBestFit = false, resizeTextMinSize = 1, resizeTextMaxSize = 40,
                updateBounds = true, verticalOverflow = VerticalWrapMode.Overflow, horizontalOverflow = wrap,
                generationExtents = new Vector2(width, height), pivot = new Vector2(0f, 1f), generateOutOfBounds = true,
            };
        }

        static string Floats(IEnumerable<float> values) => string.Join(",", values.Select(R));

        static void LogGen(string head, TextGenerator gen, string str, TextGenerationSettings s)
        {
            float pref = gen.GetPreferredWidth(str, s), prefH = gen.GetPreferredHeight(str, s);
            gen.Invalidate();
            gen.Populate(str, s);
            var ext = gen.rectExtents;
            L(head + " pref=" + R(pref) + " prefH=" + R(prefH) + " used=" + gen.fontSizeUsedForBestFit + " lines=" + gen.lineCount + " chars=" + gen.characterCount
              + " verts=" + gen.vertexCount + " ext=" + R(ext.x) + "," + R(ext.y) + "," + R(ext.width) + "," + R(ext.height));
            var chars = gen.characters;
            L(head + " cx=" + Floats(chars.Select(c => c.cursorPos.x)));
            L(head + " cw=" + Floats(chars.Select(c => c.charWidth)));
            var lines = gen.lines;
            L(head + " lines=" + string.Join(";", lines.Select(l => l.startCharIdx + ":" + l.height + ":" + R(l.topY) + ":" + R(l.leading))));
            var v = gen.verts;
            var qx = new List<string>();
            for (int i = 0; i + 3 < v.Count; i += 4)
                qx.Add(R(v[i].position.x) + "/" + R(v[i + 1].position.x) + "/" + R(v[i + 2].position.y) + "/" + R(v[i].position.y)
                       + "/" + R(v[i].uv0.x) + "/" + R(v[i].uv0.y) + "/" + R(v[i + 2].uv0.x) + "/" + R(v[i + 2].uv0.y));
            L(head + " quads=" + string.Join(" ", qx));
        }

        void Generations()
        {
            L("=== gen");
            var gen = new TextGenerator();
            var sizes = new List<(int, float)>();
            for (int size = 5; size <= 30; size++)
                foreach (float scale in Scales)
                {
                    float px = size * scale;
                    if (px >= 5f && px <= 25f) sizes.Add((size, scale));
                }
            foreach (var (size, scale) in sizes)
                foreach (var str in Strings)
                {
                    var s = Settings(size, scale, FontStyle.Normal, TextAnchor.UpperLeft, HorizontalWrapMode.Overflow);
                    LogGen("gen fontSize=" + size + " scale=" + R(scale) + " style=Normal anchor=UpperLeft wrap=Overflow str=" + Q(str), gen, str, s);
                }
            // Styles, anchors, wrapping and best fit at the panel's sizes.
            foreach (var (size, scale) in new[] { (11, 0.86f), (10, 0.86f), (13, 0.86f), (11, 1f), (9, 1f), (8, 1f), (12, 0.75f) })
            {
                foreach (FontStyle style in new[] { FontStyle.Bold, FontStyle.Italic, FontStyle.BoldAndItalic })
                    foreach (var str in Strings.Take(4))
                        LogGen("gen fontSize=" + size + " scale=" + R(scale) + " style=" + style + " anchor=UpperLeft wrap=Overflow str=" + Q(str), gen, str,
                            Settings(size, scale, style, TextAnchor.UpperLeft, HorizontalWrapMode.Overflow));
                foreach (TextAnchor anchor in new[] { TextAnchor.MiddleLeft, TextAnchor.MiddleCenter, TextAnchor.UpperRight, TextAnchor.LowerCenter })
                    foreach (float width in new[] { 200f, 201.3f, 190.5f })
                        foreach (var str in Strings.Take(4))
                            LogGen("gen fontSize=" + size + " scale=" + R(scale) + " style=Normal anchor=" + anchor + " wrap=Overflow width=" + R(width) + " str=" + Q(str), gen, str,
                                Settings(size, scale, FontStyle.Normal, anchor, HorizontalWrapMode.Overflow, width, 30f));
                foreach (float width in new[] { 120f, 160f, 222f })
                    foreach (var str in new[] { Strings[1], Strings[12] })
                        LogGen("gen fontSize=" + size + " scale=" + R(scale) + " style=Normal anchor=UpperLeft wrap=Wrap width=" + R(width) + " str=" + Q(str), gen, str,
                            Settings(size, scale, FontStyle.Normal, TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, width, 300f));
                foreach (float width in new[] { 60f, 100f, 400f })
                {
                    var s = Settings(size, scale, FontStyle.Normal, TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, width, 20f);
                    s.resizeTextForBestFit = true;
                    s.resizeTextMinSize = 4;
                    s.resizeTextMaxSize = size;
                    s.verticalOverflow = VerticalWrapMode.Truncate;
                    s.generateOutOfBounds = false;
                    LogGen("gen fontSize=" + size + " scale=" + R(scale) + " style=Normal anchor=UpperLeft wrap=Wrap bestFit=4.." + size + " width=" + R(width) + " str=" + Q(Strings[1]), gen, Strings[1], s);
                }
                // Rich text sizes.
                LogGen("gen fontSize=" + size + " scale=" + R(scale) + " style=Normal anchor=UpperLeft wrap=Overflow str=" + Q("ab<size=13>cd</size>ef<b>gh</b>"), gen, "ab<size=13>cd</size>ef<b>gh</b>",
                    Settings(size, scale, FontStyle.Normal, TextAnchor.UpperLeft, HorizontalWrapMode.Overflow));
            }
        }

        /// Alignment in a bare TextGenerator: every anchor, with generateOutOfBounds and
        /// updateBounds on and off, wrapping and overflow, pivots (0, 1) and (0.5, 0.5).
        void Anchors()
        {
            L("=== anchors");
            var gen = new TextGenerator();
            foreach (var (size, scale) in new[] { (11, 0.86f), (13, 1f) })
                foreach (TextAnchor anchor in Enum.GetValues(typeof(TextAnchor)))
                    foreach (bool oob in new[] { true, false })
                        foreach (bool bounds in new[] { true, false })
                            foreach (var wrap in new[] { HorizontalWrapMode.Overflow, HorizontalWrapMode.Wrap })
                                foreach (var pivot in new[] { new Vector2(0f, 1f), new Vector2(0.5f, 0.5f) })
                                    foreach (var str in new[] { "Target type", "Geyser Converter" })
                                    {
                                        var s = Settings(size, scale, FontStyle.Normal, anchor, wrap, 201.3f, 30f);
                                        s.generateOutOfBounds = oob;
                                        s.updateBounds = bounds;
                                        s.pivot = pivot;
                                        LogGen("gen fontSize=" + size + " scale=" + R(scale) + " style=Normal anchor=" + anchor + " wrap=" + wrap + " width=201.3 oob=" + oob
                                               + " bounds=" + bounds + " pivot=" + R(pivot.x) + "," + R(pivot.y) + " str=" + Q(str), gen, str, s);
                                    }
        }

        /// How an aligned line's start and a block's top are rounded: extents in eighths of a
        /// pixel, three pivots, updateBounds off (on, alignment is not applied).
        void Align()
        {
            L("=== align");
            var gen = new TextGenerator();
            foreach (var str in new[] { "Target type", "Steam Vent" })
                foreach (var pivot in new[] { new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0f), new Vector2(0.3f, 0.7f) })
                    foreach (TextAnchor anchor in new[] { TextAnchor.UpperLeft, TextAnchor.UpperCenter, TextAnchor.UpperRight, TextAnchor.MiddleLeft, TextAnchor.LowerLeft })
                        for (int k = 0; k <= 16; k++)
                        {
                            float w = 100f + k / 8f, h = 30f + k / 8f;
                            var s = Settings(13, 1f, FontStyle.Normal, anchor, HorizontalWrapMode.Overflow, w, h);
                            s.updateBounds = false;
                            s.generateOutOfBounds = false;
                            s.pivot = pivot;
                            gen.Invalidate();
                            gen.Populate(str, s);
                            var v = gen.verts;
                            L("align str=" + Q(str) + " pivot=" + R(pivot.x) + "," + R(pivot.y) + " anchor=" + anchor + " w=" + R(w) + " h=" + R(h)
                              + " cx0=" + (gen.characterCount > 0 ? R(gen.characters[0].cursorPos.x) : "-") + " cy0=" + (gen.characterCount > 0 ? R(gen.characters[0].cursorPos.y) : "-")
                              + " topY=" + (gen.lineCount > 0 ? R(gen.lines[0].topY) : "-") + " q0=" + (v.Count >= 4 ? R(v[0].position.x) + "," + R(v[0].position.y) : "-")
                              + " ext=" + R(gen.rectExtents.x) + "," + R(gen.rectExtents.y) + "," + R(gen.rectExtents.width) + "," + R(gen.rectExtents.height));
                        }
        }

        /// Which face the OS-font lookup gives: fonts made from OS font names, with a glyph
        /// signature (advances at 20 pixels of characters whose widths tell Liberation Sans,
        /// FreeSans, DejaVu Sans and others apart) and the line metrics.
        void OsFonts()
        {
            L("=== fonts");
            var names = Font.GetOSInstalledFontNames();
            L("osnames count=" + names.Length + " first=" + string.Join("|", names.Take(40)));
            var paths = Font.GetPathsToOSFonts();
            L("ospaths count=" + paths.Length + " first=" + string.Join("|", paths.Take(12)));
            const string sig = "!_~JNTjmW1";
            void Sig(string tag, Font f)
            {
                if (f == null) { L("osfont " + tag + " null"); return; }
                f.RequestCharactersInTexture(sig, 20, FontStyle.Normal);
                var advs = sig.Select(c => f.GetCharacterInfo(c, out var ci, 20, FontStyle.Normal) ? ci.advance.ToString() : "-");
                L("osfont " + tag + " name=" + f.name + " names=" + string.Join("|", f.fontNames ?? new string[0]) + " fontSize=" + f.fontSize + " lineHeight=" + f.lineHeight
                  + " ascent=" + f.ascent + " dynamic=" + f.dynamic + " sig20=" + string.Join(",", advs));
            }
            Sig("builtin Arial.ttf", font);
            // Characters the default face lacks: U+2125 and U+2140 (in DejaVu Sans, not in
            // FreeSans), U+0591 (Liberation Sans only), U+4E2D (WenQuanYi only).
            const string missing = "℥⅀֑中";
            font.RequestCharactersInTexture(missing, 20, FontStyle.Normal);
            foreach (char c in missing)
            {
                bool ok = font.GetCharacterInfo(c, out var ci, 20, FontStyle.Normal);
                L("osglyph builtin c=U+" + ((int)c).ToString("X4") + " ok=" + ok + " " + Ci(ci) + " hasCharacter=" + font.HasCharacter(c));
            }
            foreach (var n in new[] { "Arial", "Liberation Sans", "FreeSans", "DejaVu Sans", "Helvetica", "NoSuchFontAtAll", "sans-serif", "Sans", "Ubuntu", "" })
                Sig("os " + Q(n), Font.CreateDynamicFontFromOSFont(n, 13));
            Sig("os [NoSuchFontAtAll, Liberation Sans]", Font.CreateDynamicFontFromOSFont(new[] { "NoSuchFontAtAll", "Liberation Sans" }, 13));
            Sig("os [Arial, Liberation Sans]", Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Liberation Sans" }, 13));
            Sig("os [Liberation Sans, FreeSans]", Font.CreateDynamicFontFromOSFont(new[] { "Liberation Sans", "FreeSans" }, 13));
        }

        /// Logs the vertices the Text hands its CanvasRenderer.
        public class MeshSpy : BaseMeshEffect
        {
            public string Tag;
            public override void ModifyMesh(VertexHelper vh)
            {
                var v = new UIVertex();
                var qx = new List<string>();
                for (int i = 0; i + 3 < vh.currentVertCount; i += 4)
                {
                    vh.PopulateUIVertex(ref v, i);
                    float x0 = v.position.x, y1 = v.position.y;
                    vh.PopulateUIVertex(ref v, i + 1);
                    float x1 = v.position.x;
                    vh.PopulateUIVertex(ref v, i + 2);
                    qx.Add(R(x0) + "/" + R(x1) + "/" + R(v.position.y) + "/" + R(y1));
                }
                L("text " + Tag + " mesh verts=" + vh.currentVertCount + " quads=" + string.Join(" ", qx));
            }
        }

        IEnumerator Texts()
        {
            L("=== text");
            var cgo = new GameObject("LxCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            yield return null;
            var configs = new List<(float scale, bool pp, int size, TextAnchor anchor, HorizontalWrapMode wrap, bool bestFit, Vector2 pos)>();
            foreach (float scale in new[] { 0.86f, 1f, 0.5f, 1.5f })
                foreach (bool pp in new[] { false, true })
                    foreach (int size in new[] { 11, 10, 13 })
                    {
                        configs.Add((scale, pp, size, TextAnchor.MiddleLeft, HorizontalWrapMode.Wrap, false, new Vector2(10.3f, -20.7f)));
                        configs.Add((scale, pp, size, TextAnchor.MiddleCenter, HorizontalWrapMode.Wrap, false, new Vector2(10.3f, -20.7f)));
                        configs.Add((scale, pp, size, TextAnchor.UpperLeft, HorizontalWrapMode.Overflow, false, new Vector2(0f, 0f)));
                        configs.Add((scale, pp, size, TextAnchor.UpperRight, HorizontalWrapMode.Wrap, true, new Vector2(0.5f, 0.25f)));
                    }
            foreach (var cfg in configs)
            {
                canvas.scaleFactor = cfg.scale;
                canvas.pixelPerfect = cfg.pp;
                foreach (var str in Strings.Take(5))
                {
                    string tag = "scale=" + R(cfg.scale) + " pixelPerfect=" + cfg.pp + " fontSize=" + cfg.size + " anchor=" + cfg.anchor + " wrap=" + cfg.wrap + " bestFit=" + cfg.bestFit
                                 + " pos=" + R(cfg.pos.x) + "," + R(cfg.pos.y) + " str=" + Q(str);
                    var go = new GameObject("LxText", typeof(RectTransform));
                    go.transform.SetParent(cgo.transform, false);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(232f, 17f);
                    rt.anchoredPosition = cfg.pos;
                    var text = go.AddComponent<Text>();
                    text.font = font;
                    text.fontSize = cfg.size;
                    text.alignment = cfg.anchor;
                    text.horizontalOverflow = cfg.wrap;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    text.resizeTextForBestFit = cfg.bestFit;
                    text.resizeTextMinSize = 4;
                    text.resizeTextMaxSize = cfg.size;
                    go.AddComponent<MeshSpy>().Tag = tag;
                    text.text = str;
                    yield return null;
                    var gen = text.cachedTextGenerator;
                    var v = gen.verts;
                    var qx = new List<string>();
                    for (int i = 0; i + 3 < v.Count; i += 4) qx.Add(R(v[i].position.x) + "/" + R(v[i + 1].position.x) + "/" + R(v[i + 2].position.y) + "/" + R(v[i].position.y));
                    L("text " + tag + " preferredWidth=" + R(text.preferredWidth) + " preferredHeight=" + R(text.preferredHeight) + " pixelsPerUnit=" + R(text.pixelsPerUnit)
                      + " used=" + gen.fontSizeUsedForBestFit + " rect=" + R(rt.rect.width) + "x" + R(rt.rect.height) + " world=" + R(rt.position.x) + "," + R(rt.position.y));
                    L("text " + tag + " gen cx=" + Floats(gen.characters.Select(c => c.cursorPos.x)) + " cw=" + Floats(gen.characters.Select(c => c.charWidth)));
                    L("text " + tag + " gen quads=" + string.Join(" ", qx));
                    Destroy(go);
                }
            }
            Destroy(cgo);
        }
    }
}
