// What TMP_InputField does with text set from code ("PROBE ... IT ..."), at the main menu:
// `text =`, `SetTextWithoutNotify` and the text component's own `text`, against characterLimit,
// lineType and contentType, on fields that are inactive, active but not focused, and focused.
// Each field is a KInputTextField built as Klei's prefabs are (the NumberInputField of
// MultiSliderSideScreen's rows), its settings made before the text is set, as SliderSet.SetTarget
// does. After the set, the field is followed through a frame, being enabled (when it was
// inactive), activation and focus, an edit (End, then a typed character), and deselection, with
// the field's text, the label and every onValueChanged / onEndEdit / onSubmit report logged at
// each step. Then the order: characterLimit or contentType set after the text. With
// PROBE_SCENARIO=inputtext only this probe runs.
#pragma warning disable CS0618 // enableKerning, as SetTextStyleSetting.ApplyStyle sets it
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SemanticsProbe
{
    public class InputText : MonoBehaviour
    {
        bool started;
        float sceneStart = -1f;
        static TMP_FontAsset font;
        static readonly List<string> events = new List<string>();

        static void L(string s) => Rec.Log("IT " + s);

        static string Esc(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char ch in s) sb.Append(ch >= 0x20 && ch < 0x7F && ch != '"' ? ch.ToString() : "\\u" + ((int)ch).ToString("X4"));
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

        class Cfg
        {
            public string Name;
            public int Limit;
            public TMP_InputField.LineType Line = TMP_InputField.LineType.SingleLine;
            public TMP_InputField.ContentType Content = TMP_InputField.ContentType.Standard;
            public string Input;
            public string Order = "settings-first";   // or "limit-after", "content-after", "line-after"
        }

        static KInputTextField Make(Transform parent, Cfg c, bool active)
        {
            var go = new GameObject("NumberInputField", typeof(RectTransform));
            go.SetActive(false);
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(64, 24);
            go.AddComponent<Image>();
            var f = go.AddComponent<KInputTextField>();
            var vp = new GameObject("Text Area", typeof(RectTransform));
            var vrt = (RectTransform)vp.transform;
            vrt.SetParent(rt, false);
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.sizeDelta = Vector2.zero;
            vp.AddComponent<RectMask2D>();
            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(vrt, false);
            var t = tgo.AddComponent<TextMeshProUGUI>();
            t.font = font; t.fontSize = 14; t.enableKerning = true; t.extraPadding = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var trt = t.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.sizeDelta = new Vector2(-3, -1); trt.anchoredPosition = new Vector2(1.5f, -0.5f);
            f.textViewport = vrt;
            f.textComponent = t;
            f.onFocusSelectAll = false;
            f.onValueChanged.AddListener(v => events.Add("changed:" + Esc(v)));
            f.onEndEdit.AddListener(v => events.Add("endEdit:" + Esc(v)));
            f.onSubmit.AddListener(v => events.Add("submit:" + Esc(v)));
            if (active) go.SetActive(true);
            return f;
        }

        static void Settings(TMP_InputField f, Cfg c, bool limit, bool line, bool content)
        {
            if (line) f.lineType = c.Line;
            if (content) f.contentType = c.Content;
            if (limit) f.characterLimit = c.Limit;
        }

        static void Log(string tag, string phase, TMP_InputField f)
        {
            var ev = events.Count == 0 ? "-" : string.Join(",", events.ToArray());
            events.Clear();
            L(tag + " " + phase + " text=" + Esc(f.text) + " label=" + Esc(f.textComponent.text) + " ev=" + ev
              + " focused=" + f.isFocused + " active=" + f.gameObject.activeInHierarchy + " limit=" + f.characterLimit
              + " line=" + f.lineType + " content=" + f.contentType + " caret=" + f.caretPosition + "/" + f.selectionAnchorPosition);
        }

        static Event Key(KeyCode code, char ch)
        {
            var e = new Event { type = EventType.KeyDown, keyCode = code, character = ch };
            return e;
        }

        IEnumerator Run()
        {
            var cgo = new GameObject("ItCanvas", typeof(RectTransform));
            DontDestroyOnLoad(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            cgo.AddComponent<GraphicRaycaster>();
            yield return null;
            font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(x => x.name == "NotoSans-Regular");
            if (font == null) { L("font missing"); yield break; }
            L("eventsystem " + (UnityEngine.EventSystems.EventSystem.current == null ? "null" : UnityEngine.EventSystems.EventSystem.current.name));
            var root = canvas.transform;

            var cases = new List<Cfg>();
            // Geyser Control's four rows: limit 4, DecimalNumber.
            foreach (var s in new[] { "507.2", "63.6", "65208.3", "61.3", "1915", "12" })
                cases.Add(new Cfg { Name = "geyser", Limit = 4, Content = TMP_InputField.ContentType.DecimalNumber, Input = s });
            // The limit alone.
            foreach (int lim in new[] { 0, 1, 4 })
                cases.Add(new Cfg { Name = "limit" + lim, Limit = lim, Input = "abcdefg" });
            // Line types.
            foreach (var line in new[] { TMP_InputField.LineType.SingleLine, TMP_InputField.LineType.MultiLineSubmit, TMP_InputField.LineType.MultiLineNewline })
                foreach (var s in new[] { "ab\ncd", "a\tb", "ab\rcd" })
                    cases.Add(new Cfg { Name = "line-" + line, Line = line, Input = s });
            // Content types (each sets its own line type and validation).
            foreach (var ct in new[] { TMP_InputField.ContentType.Standard, TMP_InputField.ContentType.Autocorrected, TMP_InputField.ContentType.IntegerNumber,
                                       TMP_InputField.ContentType.DecimalNumber, TMP_InputField.ContentType.Alphanumeric, TMP_InputField.ContentType.Name,
                                       TMP_InputField.ContentType.EmailAddress, TMP_InputField.ContentType.Password, TMP_InputField.ContentType.Pin })
                foreach (var s in new[] { "-12.5e3", "ab c!1", "x\ny" })
                    cases.Add(new Cfg { Name = "content-" + ct, Content = ct, Input = s });
            // A limit with a multi-line type.
            cases.Add(new Cfg { Name = "limit3-multiline", Limit = 3, Line = TMP_InputField.LineType.MultiLineNewline, Input = "a\nbcd" });
            // Settings made after the text.
            cases.Add(new Cfg { Name = "limit-after", Limit = 4, Content = TMP_InputField.ContentType.DecimalNumber, Input = "507.2", Order = "limit-after" });
            cases.Add(new Cfg { Name = "content-after-int", Content = TMP_InputField.ContentType.IntegerNumber, Input = "507.2", Order = "content-after" });
            cases.Add(new Cfg { Name = "content-after-alnum", Content = TMP_InputField.ContentType.Alphanumeric, Input = "ab c!1", Order = "content-after" });
            cases.Add(new Cfg { Name = "line-after", Line = TMP_InputField.LineType.SingleLine, Input = "ab\ncd", Order = "line-after" });

            foreach (var c in cases)
                foreach (var setter in new[] { "text", "notify-less", "label" })
                    foreach (var state in new[] { "inactive", "active", "focused" })
                    {
                        if (c.Order != "settings-first" && state == "focused") continue;
                        string tag = c.Name + " " + Esc(c.Input) + " " + setter + " " + state + (c.Order == "settings-first" ? "" : " " + c.Order);
                        var f = Make(root, c, state != "inactive");
                        bool after = c.Order != "settings-first";
                        if (after)
                        {
                            // The other settings first, the named one after the text.
                            Settings(f, c, c.Order != "limit-after", c.Order != "line-after", c.Order != "content-after");
                            if (c.Order == "line-after") f.lineType = TMP_InputField.LineType.MultiLineNewline;
                        }
                        else Settings(f, c, true, true, true);
                        events.Clear();
                        if (state == "focused")
                        {
                            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(f.gameObject);
                            f.ActivateInputField();
                            yield return null;
                            yield return null;
                            Log(tag, "pre", f);
                        }
                        if (setter == "text") f.text = c.Input;
                        else if (setter == "notify-less") f.SetTextWithoutNotify(c.Input);
                        else f.textComponent.text = c.Input;
                        Log(tag, "set", f);
                        if (after)
                        {
                            if (c.Order == "limit-after") f.characterLimit = c.Limit;
                            else if (c.Order == "content-after") f.contentType = c.Content;
                            else f.lineType = c.Line;
                            Log(tag, "setting", f);
                        }
                        yield return null;
                        Log(tag, "frame", f);
                        if (state == "inactive")
                        {
                            f.gameObject.SetActive(true);
                            Log(tag, "enabled", f);
                            yield return null;
                            Log(tag, "enabled+1", f);
                        }
                        if (state != "focused")
                        {
                            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(f.gameObject);
                            f.ActivateInputField();
                            Log(tag, "activate", f);
                            yield return null;
                            Log(tag, "activate+1", f);
                            yield return null;
                            Log(tag, "activate+2", f);
                        }
                        // An edit: End, then a typed '9'.
                        f.ProcessEvent(Key(KeyCode.End, '\0'));
                        f.ProcessEvent(Key(KeyCode.None, '9'));
                        f.ForceLabelUpdate();
                        Log(tag, "typed", f);
                        yield return null;
                        Log(tag, "typed+1", f);
                        // A backspace.
                        f.ProcessEvent(Key(KeyCode.Backspace, '\0'));
                        f.ForceLabelUpdate();
                        Log(tag, "backspace", f);
                        yield return null;
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                        Log(tag, "deselect", f);
                        yield return null;
                        Log(tag, "deselect+1", f);
                        Destroy(f.gameObject);
                        yield return null;
                    }
            Destroy(cgo);
            L("=== done");
        }
    }
}
