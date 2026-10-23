// TMP_InputField's layout element values ("PROBE ... IL ..."), at the main menu: minWidth/Height,
// preferredWidth/Height, flexibleWidth/Height and layoutPriority of fields built in code, beside
// the text component's (and placeholder's) preferred size, as functions of the text, the text
// viewport's offsets, the text rect's anchors and offsets, the field's own rect, the line type,
// the character limit and the placeholder. Then Geyser Control's slider row (a 64 x 24 field in a
// HorizontalLayoutGroup row with a LayoutElement, the text anchored to the viewport's top with
// size delta (-3, -1)), fields under a HorizontalLayoutGroup with childControlHeight on and off,
// and fields with a ContentSizeFitter, each through several layout passes, to see whether a pass
// changes the next. With PROBE_SCENARIO=inputlayout only this probe runs.
#pragma warning disable CS0618 // enableKerning, as SetTextStyleSetting.ApplyStyle sets it
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class InputLayout : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static TMP_FontAsset font;

        static void L(string s) => Rec.Log("IL " + s);
        static string R(float v) => v.ToString("R", Inv);
        static string V(Vector2 v) => R(v.x) + "," + R(v.y);

        void Update()
        {
            if (started || SceneManager.GetActiveScene().name != "frontend") return;
            if (sceneStart < 0f) sceneStart = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - sceneStart < 10f) return;
            started = true;
            StartCoroutine(Run());
        }

        class Cfg
        {
            public string Name;
            public Vector2 Size = new Vector2(64, 24);
            public Vector4 Viewport;                     // offsetMin.x, offsetMin.y, -offsetMax.x, -offsetMax.y
            public Vector2 TextAnchorMin = Vector2.zero, TextAnchorMax = Vector2.one;
            public Vector2 TextSizeDelta, TextPos;
            public Vector4 TextMargin;
            public string Text = "1915";
            public bool Placeholder;
            public string PlaceholderText = "Enter text...";
            public TMP_InputField.LineType Line = TMP_InputField.LineType.SingleLine;
            public int Limit;
            public bool Wrap;
            public bool NoTextComponent;
            public bool NoViewport;
            public Vector2 VpAnchorMin = Vector2.zero, VpAnchorMax = Vector2.one;
            public Vector2? VpSize;
            public Vector4 MaskPadding;
            public Cfg Copy(string name) { var c = (Cfg)MemberwiseClone(); c.Name = name; return c; }
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.enableKerning = true;
            t.extraPadding = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.alignment = TextAlignmentOptions.TopLeft;
            return t;
        }

        /// A field as Klei's prefabs build one: field (Image, TMP_InputField), child "Text Area"
        /// (RectMask2D, stretched with the viewport offsets), its child "Text" (and "Placeholder").
        static TMP_InputField Make(Transform parent, Cfg c)
        {
            var go = new GameObject("Field " + c.Name, typeof(RectTransform));
            go.SetActive(false);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = c.Size;
            go.AddComponent<Image>();
            var f = go.AddComponent<TMP_InputField>();
            var vp = new GameObject("Text Area", typeof(RectTransform));
            var vrt = (RectTransform)vp.transform;
            vrt.SetParent(rt, false);
            vrt.anchorMin = c.VpAnchorMin; vrt.anchorMax = c.VpAnchorMax;
            vrt.offsetMin = new Vector2(c.Viewport.x, c.Viewport.y);
            vrt.offsetMax = new Vector2(-c.Viewport.z, -c.Viewport.w);
            if (c.VpSize.HasValue) { vrt.sizeDelta = c.VpSize.Value; vrt.anchoredPosition = new Vector2(c.Viewport.x, c.Viewport.y); }
            vp.AddComponent<RectMask2D>().padding = c.MaskPadding;
            if (!c.NoViewport) f.textViewport = vrt;
            if (!c.NoTextComponent)
            {
                var t = MakeText(vrt, "Text", 14);
                var trt = t.rectTransform;
                trt.anchorMin = c.TextAnchorMin; trt.anchorMax = c.TextAnchorMax;
                trt.sizeDelta = c.TextSizeDelta; trt.anchoredPosition = c.TextPos;
                t.margin = c.TextMargin;
                if (c.Wrap) t.textWrappingMode = TextWrappingModes.Normal;
                f.textComponent = t;
            }
            if (c.Placeholder)
            {
                var p = MakeText(vrt, "Placeholder", 20);
                p.rectTransform.anchorMin = Vector2.zero; p.rectTransform.anchorMax = Vector2.one;
                p.rectTransform.sizeDelta = Vector2.zero;
                p.text = c.PlaceholderText;
                f.placeholder = p;
            }
            f.lineType = c.Line;
            f.characterLimit = c.Limit;
            f.text = c.Text;
            go.SetActive(true);
            return f;
        }

        static void Log(string tag, string phase, TMP_InputField f)
        {
            var rt = (RectTransform)f.transform;
            var t = f.textComponent;
            var p = f.placeholder as TMP_Text;
            string s = tag + " " + phase
                + " min=" + R(f.minWidth) + "," + R(f.minHeight)
                + " pref=" + R(f.preferredWidth) + "," + R(f.preferredHeight)
                + " flex=" + R(f.flexibleWidth) + "," + R(f.flexibleHeight)
                + " prio=" + f.layoutPriority
                + " LU=" + R(LayoutUtility.GetMinWidth(rt)) + "," + R(LayoutUtility.GetMinHeight(rt)) + "/" + R(LayoutUtility.GetPreferredWidth(rt)) + "," + R(LayoutUtility.GetPreferredHeight(rt))
                + " rect=" + R(rt.rect.width) + "x" + R(rt.rect.height)
                + (f.textViewport == null ? " vp=null" : " vp=" + R(f.textViewport.rect.width) + "x" + R(f.textViewport.rect.height)
                   + " vpoff=" + V(f.textViewport.offsetMin) + "/" + V(f.textViewport.offsetMax));
            if (t != null)
                s += " text=" + R(t.preferredWidth) + "," + R(t.preferredHeight) + " trect=" + R(t.rectTransform.rect.width) + "x" + R(t.rectTransform.rect.height)
                    + " label=\"" + Esc(t.text) + "\"";
            if (p != null)
                s += " ph=" + R(p.preferredWidth) + "," + R(p.preferredHeight) + " phOn=" + p.enabled;
            L(s);
        }

        static string Esc(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s) sb.Append(ch >= 0x20 && ch < 0x7F ? ch.ToString() : "\\u" + ((int)ch).ToString("X4"));
            return sb.ToString();
        }

        IEnumerator Run()
        {
            var cgo = new GameObject("IlCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            yield return null;
            font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(x => x.name == "NotoSans-Regular");
            if (font == null) { L("font missing"); yield break; }
            var root = canvas.transform;

            // 1. Standalone fields: the function of text, viewport, text rect, field rect, line
            //    type, limit and placeholder. Logged at once, after a forced rebuild and a frame on.
            var geyserText = new Cfg { Name = "x", TextAnchorMin = new Vector2(0, 1), TextAnchorMax = new Vector2(1, 1), TextSizeDelta = new Vector2(-3, -1), TextPos = new Vector2(1.5f, -0.5f) };
            var cases = new System.Collections.Generic.List<Cfg>();
            var bases = new[]
            {
                new Cfg { Name = "stretch" },
                new Cfg { Name = "textoff", TextSizeDelta = new Vector2(-20, -12) },
                new Cfg { Name = "textoffpos", TextSizeDelta = new Vector2(-20, -12), TextPos = new Vector2(3, -2) },
                geyserText.Copy("geysertext"),
                new Cfg { Name = "textfixed", TextAnchorMin = new Vector2(0.5f, 0.5f), TextAnchorMax = new Vector2(0.5f, 0.5f), TextSizeDelta = new Vector2(50, 10) },
                new Cfg { Name = "vp", Viewport = new Vector4(5, 3, 7, 4) },
                new Cfg { Name = "vp+textoff", Viewport = new Vector4(5, 3, 7, 4), TextSizeDelta = new Vector2(-20, -12) },
                new Cfg { Name = "margin", TextMargin = new Vector4(4, 2, 6, 3) },
            };
            foreach (var b in bases)
                foreach (var size in new[] { new Vector2(64, 24), new Vector2(200, 100), new Vector2(10, 5) })
                    foreach (var text in new[] { "1915", "", "abc def ghi jkl" })
                        foreach (bool ph in new[] { false, true })
                        {
                            var c = b.Copy(b.Name + " " + R(size.x) + "x" + R(size.y) + " \"" + text + "\"" + (ph ? " ph" : ""));
                            c.Size = size; c.Text = text; c.Placeholder = ph;
                            cases.Add(c);
                        }
            foreach (var line in new[] { TMP_InputField.LineType.SingleLine, TMP_InputField.LineType.MultiLineSubmit, TMP_InputField.LineType.MultiLineNewline })
                foreach (int limit in new[] { 0, 2 })
                    foreach (bool wrap in new[] { false, true })
                        foreach (var text in new[] { "1915", "ab\ncd", "abc def ghi jkl mno" })
                            cases.Add(new Cfg { Name = "line " + line + " limit=" + limit + (wrap ? " wrap" : "") + " \"" + Esc(text) + "\"", Line = line, Limit = limit, Wrap = wrap, Text = text });
            // The viewport's part: anchored to the field's centre at a fixed size (its offsets and
            // the difference of the rects then differ), anchored to part of the field, moved off
            // centre, larger than the field, with RectMask2D padding, and not assigned.
            foreach (var size in new[] { new Vector2(64, 24), new Vector2(200, 100) })
            {
                string z = " " + R(size.x) + "x" + R(size.y);
                var c5 = new Vector2(0.5f, 0.5f);
                cases.Add(new Cfg { Name = "vpfixed" + z, Size = size, VpAnchorMin = c5, VpAnchorMax = c5, VpSize = new Vector2(40, 10) });
                cases.Add(new Cfg { Name = "vpfixed-moved" + z, Size = size, VpAnchorMin = c5, VpAnchorMax = c5, VpSize = new Vector2(40, 10), Viewport = new Vector4(6, 2, 0, 0) });
                cases.Add(new Cfg { Name = "vppart" + z, Size = size, VpAnchorMin = new Vector2(0.25f, 0), VpAnchorMax = new Vector2(0.75f, 1) });
                cases.Add(new Cfg { Name = "vppart+off" + z, Size = size, VpAnchorMin = new Vector2(0.25f, 0.5f), VpAnchorMax = new Vector2(0.75f, 1), Viewport = new Vector4(2, 1, 3, 4) });
                cases.Add(new Cfg { Name = "vplarger" + z, Size = size, Viewport = new Vector4(-5, -3, -7, -4) });
                cases.Add(new Cfg { Name = "vpmask" + z, Size = size, MaskPadding = new Vector4(4, 3, 2, 1) });
                cases.Add(new Cfg { Name = "vpnull" + z, Size = size, NoViewport = true });
                cases.Add(new Cfg { Name = "vpnull+off" + z, Size = size, NoViewport = true, Viewport = new Vector4(5, 3, 7, 4) });
            }
            cases.Add(new Cfg { Name = "notext", NoTextComponent = true });
            cases.Add(new Cfg { Name = "notext ph", NoTextComponent = true, Placeholder = true });
            cases.Add(new Cfg { Name = "ph-long \"\"", Text = "", Placeholder = true, PlaceholderText = "a much longer placeholder text" });
            cases.Add(new Cfg { Name = "ph-long \"1915\"", Text = "1915", Placeholder = true, PlaceholderText = "a much longer placeholder text" });
            foreach (var c in cases)
            {
                var f = Make(root, c);
                Log("alone " + c.Name, "now", f);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)f.transform);
                Log("alone " + c.Name, "rebuilt", f);
                yield return null;
                Log("alone " + c.Name, "frame", f);
                Destroy(f.gameObject);
            }

            // 2. In layouts, through passes: Geyser Control's row; a HorizontalLayoutGroup with
            //    childControlHeight on and off (no LayoutElement); a ContentSizeFitter on the field.
            foreach (var text in new[] { "1915", "" })
                foreach (var tc in new[] { geyserText.Copy("geysertext"), new Cfg { Name = "stretch" }, new Cfg { Name = "textoff", TextSizeDelta = new Vector2(-20, -12) } })
                {
                    foreach (string layout in new[] { "geyserrow", "hlg-ctrl", "hlg-noctrl", "hlg-ctrl-expand", "csf", "csf-in-hlg" })
                    {
                        var c = tc.Copy(tc.Name + " \"" + text + "\"");
                        c.Text = text;
                        string tag = layout + " " + c.Name;
                        var row = new GameObject("Row " + tag, typeof(RectTransform));
                        var rrt = (RectTransform)row.transform;
                        rrt.SetParent(root, false);
                        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
                        rrt.sizeDelta = new Vector2(256, 24);
                        TMP_InputField f;
                        if (layout == "csf")
                        {
                            f = Make(rrt, c);
                            var csf = f.gameObject.AddComponent<ContentSizeFitter>();
                            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                        }
                        else
                        {
                            var hlg = row.AddComponent<HorizontalLayoutGroup>();
                            hlg.childControlWidth = true;
                            hlg.childControlHeight = layout != "hlg-noctrl";
                            hlg.childForceExpandWidth = false;
                            hlg.childForceExpandHeight = layout == "hlg-ctrl-expand";
                            hlg.childAlignment = TextAnchor.MiddleLeft;
                            if (layout == "geyserrow")
                            {
                                var lab = MakeText(rrt, "TargetLabel", 14);
                                lab.text = "Mass Per Cycle";
                                var lle = lab.gameObject.AddComponent<LayoutElement>();
                                lle.minWidth = 96; lle.minHeight = 15; lle.preferredWidth = 96; lle.flexibleWidth = 1;
                            }
                            f = Make(rrt, c);
                            if (layout == "geyserrow")
                            {
                                var le = f.gameObject.AddComponent<LayoutElement>();
                                le.minWidth = 64; le.minHeight = 24; le.preferredWidth = 64; le.flexibleWidth = 0;
                                f.characterLimit = 4;
                                f.contentType = TMP_InputField.ContentType.DecimalNumber;
                                var sp = new GameObject("Spacer", typeof(RectTransform));
                                sp.transform.SetParent(rrt, false);
                                sp.AddComponent<LayoutElement>().minWidth = 8;
                                var units = MakeText(rrt, "UnitsLabel", 14);
                                units.text = "kg";
                                var ule = units.gameObject.AddComponent<LayoutElement>();
                                ule.minWidth = 38; ule.minHeight = 16; ule.preferredWidth = 38; ule.flexibleWidth = 0;
                            }
                            if (layout == "csf-in-hlg")
                            {
                                var csf = rrt.gameObject.AddComponent<ContentSizeFitter>();
                                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                            }
                        }
                        Log(tag, "now", f);
                        for (int pass = 1; pass <= 4; pass++)
                        {
                            LayoutRebuilder.ForceRebuildLayoutImmediate(rrt);
                            var g = row.GetComponent<HorizontalLayoutGroup>();
                            Log(tag, "pass" + pass + (g != null ? " row=" + R(g.preferredHeight) + " rowrect=" + R(rrt.rect.height) : ""), f);
                        }
                        for (int fr = 1; fr <= 3; fr++)
                        {
                            yield return null;
                            Log(tag, "frame" + fr, f);
                        }
                        Destroy(row);
                    }
                }
            Destroy(cgo);
            L("=== done");
        }
    }
}
