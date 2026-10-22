using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace E2eDriver
{
    /// The UI as the driver sees it: every element under a canvas, found by hierarchy-path
    /// pattern, text or component type, described with its screen rectangle (pixels, origin top
    /// left as in X11) and the top raycast hit at its centre, or dumped in the `--dump-ui` line
    /// format.
    static class Ui
    {
        // ---- paths ----

        public static string PathOf(Transform t)
        {
            var names = new List<string>();
            for (; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }

        /// The `--dump-ui` key: each level's name and sibling index ("/A#0/B#3").
        public static string KeyOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name + "#" + t.GetSiblingIndex());
            parts.Reverse();
            return "/" + string.Join("/", parts);
        }

        static List<Transform> Chain(Transform t)
        {
            var chain = new List<Transform>();
            for (; t != null; t = t.parent) chain.Add(t);
            chain.Reverse();
            return chain;
        }

        /// A hierarchy-path pattern: segments separated by '/', matched against the whole path
        /// from the scene root. A segment is a name with `*` and `?` wildcards and an optional
        /// `[n]` (the n-th sibling, from 0, among those whose name matches); `**` matches any
        /// number of segments. A pattern without '/' matches that name anywhere.
        public sealed class Pattern
        {
            readonly string[] globs;
            readonly int[] indices;
            readonly Regex[] regexes;
            public readonly string Text;
            /// Whether `[n]` counts inactive siblings too (queries that include inactive elements).
            readonly bool inactive;

            public Pattern(string text, bool inactive = false)
            {
                this.inactive = inactive;
                Text = text;
                text = text.Trim();
                if (text.StartsWith("/", StringComparison.Ordinal)) text = text.Substring(1);
                else if (text.IndexOf('/') < 0 && text != "**") text = "**/" + text;
                var segments = text.Split('/');
                globs = new string[segments.Length];
                indices = new int[segments.Length];
                regexes = new Regex[segments.Length];
                for (int i = 0; i < segments.Length; i++)
                {
                    var s = segments[i];
                    indices[i] = -1;
                    var m = Regex.Match(s, @"^(.*)\[(\d+)\]$");
                    if (m.Success && s != "**") { s = m.Groups[1].Value; indices[i] = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture); }
                    globs[i] = s;
                    if (s != "**")
                        regexes[i] = new Regex("^" + Regex.Escape(s).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.Singleline);
                }
            }

            public bool Matches(Transform t) => Matches(Chain(t));

            public bool Matches(List<Transform> chain)
            {
                var memo = new sbyte[globs.Length + 1, chain.Count + 1];
                return Match(0, 0, chain, memo);
            }

            bool Match(int si, int ci, List<Transform> chain, sbyte[,] memo)
            {
                if (memo[si, ci] != 0) return memo[si, ci] > 0;
                bool result;
                if (si == globs.Length) result = ci == chain.Count;
                else if (globs[si] == "**") result = Match(si + 1, ci, chain, memo) || (ci < chain.Count && Match(si, ci + 1, chain, memo));
                else result = ci < chain.Count && Segment(si, chain[ci]) && Match(si + 1, ci + 1, chain, memo);
                memo[si, ci] = (sbyte)(result ? 1 : -1);
                return result;
            }

            bool Segment(int si, Transform t)
            {
                var re = regexes[si];
                if (!re.IsMatch(t.name)) return false;
                if (indices[si] < 0) return true;
                // The ordinal among the siblings (or the scene's roots) that match the name.
                int ordinal = 0;
                if (t.parent != null)
                {
                    int index = t.GetSiblingIndex();
                    for (int i = 0; i < index; i++)
                    {
                        var sibling = t.parent.GetChild(i);
                        if ((inactive || sibling.gameObject.activeSelf) && re.IsMatch(sibling.name)) ordinal++;
                    }
                }
                else
                {
                    foreach (var root in t.gameObject.scene.GetRootGameObjects())
                    {
                        if (root.transform == t) break;
                        if ((inactive || root.activeSelf) && re.IsMatch(root.name)) ordinal++;
                    }
                }
                return ordinal == indices[si];
            }
        }

        // ---- the element walk ----

        static bool HasCanvasAbove(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent) if (p.GetComponent<Canvas>() != null) return true;
            return false;
        }

        /// The outermost canvases (no canvas above them), sorted by key.
        public static List<Transform> TopCanvases(bool inactive)
        {
            var tops = new List<Transform>();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(inactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!HasCanvasAbove(c.transform)) tops.Add(c.transform);
            var keys = new Dictionary<Transform, string>();
            foreach (var t in tops) keys[t] = KeyOf(t);
            tops.Sort((a, b) => string.CompareOrdinal(keys[a], keys[b]));
            return tops;
        }

        /// Every element (RectTransform) under the given starts, depth first in sibling order,
        /// with its chain from the scene root.
        public static void Walk(IEnumerable<Transform> starts, bool inactive, Action<RectTransform, List<Transform>> visit)
        {
            foreach (var start in starts)
            {
                if (!inactive && !start.gameObject.activeInHierarchy) continue;
                var chain = Chain(start);
                Visit(start, chain, inactive, visit);
            }
        }

        static void Visit(Transform t, List<Transform> chain, bool inactive, Action<RectTransform, List<Transform>> visit)
        {
            if (t is RectTransform rt) visit(rt, chain);
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (!inactive && !child.gameObject.activeSelf) continue;
                chain.Add(child);
                Visit(child, chain, inactive, visit);
                chain.RemoveAt(chain.Count - 1);
            }
        }

        // ---- finding ----

        /// The elements matching a query's path, text, type and under (all optional), in walk
        /// order.
        public static List<RectTransform> Find(JObject args)
        {
            bool inactive = (bool?)args["inactive"] ?? false;
            var path = (string)args["path"];
            var text = (string)args["text"];
            var type = (string)args["type"];
            var under = (string)args["under"];
            var pattern = path == null ? null : new Pattern(path, inactive);
            var underPattern = under == null ? null : new Pattern(under, inactive);
            var found = new List<RectTransform>();
            Walk(TopCanvases(inactive), inactive, (rt, chain) =>
            {
                if (pattern != null && !pattern.Matches(chain)) return;
                if (type != null && !HasType(rt, type)) return;
                if (text != null && !TextMatches(TextOf(rt), text)) return;
                if (underPattern != null && !Under(chain, underPattern)) return;
                found.Add(rt);
            });
            return found;
        }

        static bool Under(List<Transform> chain, Pattern pattern)
        {
            var prefix = new List<Transform>(chain.Count);
            foreach (var t in chain)
            {
                prefix.Add(t);
                if (pattern.Matches(prefix)) return true;
            }
            return false;
        }

        static bool HasType(Component c, string name)
        {
            foreach (var comp in c.GetComponents<Component>())
                for (var t = comp == null ? null : comp.GetType(); t != null; t = t.BaseType)
                    if (t.Name == name || t.FullName == name) return true;
            return false;
        }

        static readonly Regex tags = new Regex("<[^>]*>");

        /// Exact (after trimming, either with or without rich-text tags), or a case-insensitive
        /// substring when the wanted text starts with '~'.
        public static bool TextMatches(string actual, string wanted)
        {
            if (actual == null) return false;
            var plain = tags.Replace(actual, "");
            if (wanted.StartsWith("~", StringComparison.Ordinal))
            {
                var sub = wanted.Substring(1);
                return actual.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0 || plain.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return actual.Trim() == wanted.Trim() || plain.Trim() == wanted.Trim();
        }

        public static string TextOf(Component c)
        {
            var tmp = c.GetComponent<TMPro.TMP_Text>();
            if (tmp != null) return tmp.text;
            var text = c.GetComponent<Text>();
            return text != null ? text.text : null;
        }

        // ---- screen geometry ----

        static Canvas RootCanvasOf(Transform t)
        {
            Canvas root = null;
            for (; t != null; t = t.parent)
                if (t.GetComponent<Canvas>() is { } c) root = c;
            return root;
        }

        public static Camera CameraFor(Canvas root)
        {
            if (root == null || root.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            if (root.renderMode == RenderMode.ScreenSpaceCamera) return root.worldCamera;
            return root.worldCamera != null ? root.worldCamera : Camera.main;
        }

        /// The element's bounding box on screen: x, y from the top left, in pixels.
        public static Rect ScreenRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var cam = CameraFor(RootCanvasOf(rt));
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var corner in corners)
            {
                var p = RectTransformUtility.WorldToScreenPoint(cam, corner);
                minX = Math.Min(minX, p.x); maxX = Math.Max(maxX, p.x);
                minY = Math.Min(minY, p.y); maxY = Math.Max(maxY, p.y);
            }
            return new Rect(minX, Screen.height - maxY, maxX - minX, maxY - minY);
        }

        // ---- description ----

        static bool IsAncestor(Transform ancestor, Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent) if (p == ancestor) return true;
            return false;
        }

        /// The topmost raycast hit at a screen point (top-left pixels), and how it relates to the
        /// element (self, child: inside it; ancestor: a parent of it; other: covering it).
        public static JObject Hit(float x, float y, Transform element, int list = 0)
        {
            var system = UnityEngine.EventSystems.EventSystem.current;
            if (system == null) return new JObject { ["path"] = null, ["relation"] = "no-event-system", ["count"] = 0 };
            var pointer = new PointerEventData(system) { position = new Vector2(x, Screen.height - y) };
            var results = new List<RaycastResult>();
            system.RaycastAll(pointer, results);
            if (results.Count == 0 || results[0].gameObject == null) return new JObject { ["path"] = null, ["relation"] = "none", ["count"] = results.Count };
            var obj = HitEntry(results[0], element);
            obj["count"] = results.Count;
            // The next hits below the top one, for diagnosing an occlusion.
            if (list > 0)
            {
                var all = new JArray();
                for (int i = 0; i < results.Count && i < list; i++) all.Add(HitEntry(results[i], element));
                obj["all"] = all;
            }
            return obj;
        }

        static JObject HitEntry(RaycastResult r, Transform element)
        {
            if (r.gameObject == null) return new JObject { ["path"] = null };
            var hit = r.gameObject.transform;
            string relation = element == null ? null : hit == element ? "self" : IsAncestor(element, hit) ? "child" : IsAncestor(hit, element) ? "ancestor" : "other";
            return new JObject
            {
                ["path"] = PathOf(hit),
                ["relation"] = relation,
                ["raycaster"] = r.module == null ? null : r.module.GetType().Name,
                ["sortingLayer"] = r.sortingLayer,
                ["sortingOrder"] = r.sortingOrder,
                ["depth"] = r.depth,
                ["distance"] = Values.Num(r.distance),
            };
        }

        static bool? BoolMember(Component c, string name)
        {
            var type = c.GetType();
            var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(bool) && p.GetMethod != null && p.GetIndexParameters().Length == 0) return (bool)p.GetValue(c);
            var f = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(bool)) return (bool)f.GetValue(c);
            return null;
        }

        public static JObject Describe(RectTransform rt, bool withHit, int hits = 0)
        {
            var screen = ScreenRect(rt);
            float cx = screen.x + screen.width / 2, cy = screen.y + screen.height / 2;
            var go = rt.gameObject;
            var root = RootCanvasOf(rt);
            var obj = new JObject
            {
                ["path"] = PathOf(rt),
                ["key"] = KeyOf(rt),
                ["name"] = rt.name,
                ["active"] = go.activeInHierarchy,
                ["activeSelf"] = go.activeSelf,
                ["rect"] = Values.Rect(screen.x, screen.y, screen.width, screen.height),
                ["center"] = new JObject { ["x"] = Values.Px(cx), ["y"] = Values.Px(cy) },
                ["onscreen"] = cx >= 0 && cy >= 0 && cx < Screen.width && cy < Screen.height && screen.width > 0 && screen.height > 0,
                ["canvas"] = root == null ? null : PathOf(root.transform),
                ["renderMode"] = root == null ? null : root.renderMode.ToString(),
            };
            var components = new JArray();
            bool? interactable = null, isOn = null;
            int? state = null;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                components.Add(c.GetType().Name);
                switch (c)
                {
                    case Selectable s:
                        interactable = (interactable ?? true) && s.IsInteractable();
                        if (s is Toggle toggle) isOn = toggle.isOn;
                        break;
                    case MultiToggle multi:
                        state = multi.CurrentState;
                        break;
                    default:
                        if (c.GetType().Namespace?.StartsWith("UnityEngine", StringComparison.Ordinal) == true || c.GetType().Namespace == "TMPro") break;
                        if (BoolMember(c, "isInteractable") is bool k) interactable = (interactable ?? true) && k;
                        if (isOn == null && BoolMember(c, "isOn") is bool on) isOn = on;
                        break;
                }
            }
            obj["components"] = components;
            if (interactable != null) obj["interactable"] = interactable.Value;
            if (isOn != null) obj["isOn"] = isOn.Value;
            if (state != null) obj["state"] = state.Value;
            // The canvas groups above: what they allow.
            bool groupInteractable = true, blocks = true;
            float alpha = 1f;
            for (Transform t = rt; t != null; t = t.parent)
            {
                bool stop = false;
                foreach (var g in t.GetComponents<CanvasGroup>())
                {
                    if (!g.enabled) continue;
                    groupInteractable &= g.interactable;
                    blocks &= g.blocksRaycasts;
                    alpha *= g.alpha;
                    stop |= g.ignoreParentGroups;
                }
                if (stop) break;
            }
            obj["groupInteractable"] = groupInteractable;
            obj["blocksRaycasts"] = blocks;
            obj["alpha"] = Values.Num(alpha);
            var graphic = go.GetComponent<Graphic>();
            if (graphic != null) obj["raycastTarget"] = graphic.raycastTarget;
            var text = TextOf(rt);
            if (text != null) obj["text"] = text;
            if (withHit && go.activeInHierarchy && (bool)obj["onscreen"]) obj["hit"] = Hit(cx, cy, rt, hits);
            return obj;
        }

        // ---- dump (the --dump-ui line format) ----

        /// Public members recorded per component type (by full name, base types included):
        /// the host's --dump-ui list without what is not public (canvas renderer geometry) or
        /// not repeatable (a scroll rect's velocity).
        static readonly (string Type, string[] Members)[] recorded =
        {
            ("UnityEngine.Canvas", new[] { "renderMode", "scaleFactor", "sortingOrder", "overrideSorting", "pixelPerfect" }),
            ("UnityEngine.CanvasGroup", new[] { "alpha", "interactable", "blocksRaycasts" }),
            ("UnityEngine.UI.Graphic", new[] { "color", "raycastTarget", "depth" }),
            ("UnityEngine.UI.Image", new[] { "type", "fillAmount", "preserveAspect" }),
            ("UnityEngine.UI.Text", new[] { "text", "fontSize", "alignment" }),
            ("UnityEngine.UI.Selectable", new[] { "interactable" }),
            ("UnityEngine.UI.Toggle", new[] { "isOn" }),
            ("UnityEngine.UI.Slider", new[] { "value", "minValue", "maxValue" }),
            ("UnityEngine.UI.Scrollbar", new[] { "value", "size", "numberOfSteps" }),
            ("UnityEngine.UI.ScrollRect", new[] { "normalizedPosition" }),
            ("UnityEngine.UI.InputField", new[] { "text", "caretPosition" }),
            ("UnityEngine.UI.Dropdown", new[] { "value" }),
            ("UnityEngine.UI.LayoutElement", new[] { "minWidth", "minHeight", "preferredWidth", "preferredHeight", "flexibleWidth", "flexibleHeight", "ignoreLayout" }),
            ("UnityEngine.UI.LayoutGroup", new[] { "preferredWidth", "preferredHeight", "minWidth", "minHeight" }),
            ("UnityEngine.UI.CanvasScaler", new[] { "scaleFactor", "uiScaleMode" }),
            ("TMPro.TMP_Text", new[] { "text", "fontSize", "alignment", "isTextOverflowing", "firstOverflowCharacterIndex", "textInfo" }),
            ("TMPro.TMP_InputField", new[] { "text", "caretPosition", "isFocused" }),
            ("TMPro.TMP_Dropdown", new[] { "value" }),
        };

        static readonly Dictionary<Type, List<(string Label, Func<object, object> Get)>> readers = new Dictionary<Type, List<(string, Func<object, object>)>>();

        /// One line per element of each start's subtree: key, a tab, rect, world corners and
        /// screen rect, then " | Component" with its recorded members, for each component.
        public static List<string> Dump(IEnumerable<Transform> starts, bool inactive)
        {
            var lines = new List<string>();
            var corners = new Vector3[4];
            Walk(starts, inactive, (rt, chain) =>
            {
                var text = new StringBuilder();
                text.Append(KeyOf(rt)).Append('\t');
                var r = rt.rect;
                text.Append("rect ").Append(F(r.x)).Append(',').Append(F(r.y)).Append(',').Append(F(r.width)).Append(',').Append(F(r.height));
                rt.GetWorldCorners(corners);
                text.Append(" world ").Append(F(corners[0].x)).Append(',').Append(F(corners[0].y)).Append(',').Append(F(corners[2].x)).Append(',').Append(F(corners[2].y));
                var s = ScreenRect(rt);
                text.Append(" screen ").Append(F(s.x)).Append(',').Append(F(s.y)).Append(',').Append(F(s.width)).Append(',').Append(F(s.height));
                if (!rt.gameObject.activeInHierarchy) text.Append(" inactive");
                foreach (var c in rt.GetComponents<Component>())
                {
                    if (c == null || c is Transform) continue;
                    text.Append(" | ").Append(c.GetType().Name);
                    if (c is Behaviour b && !b.enabled) text.Append("(off)");
                    if (c is CanvasRenderer cr)
                    {
                        text.Append(" col=").Append(C(cr.GetColor()));
                        if (cr.cull) text.Append(" cull");
                    }
                    AppendMembers(text, c);
                }
                lines.Add(text.ToString());
            });
            return lines;
        }

        static void AppendMembers(StringBuilder text, Component c)
        {
            var type = c.GetType();
            if (!readers.TryGetValue(type, out var list))
            {
                list = new List<(string, Func<object, object>)>();
                for (var t = type; t != null; t = t.BaseType)
                    foreach (var (name, members) in recorded)
                        if (t.FullName == name)
                            foreach (var member in members)
                            {
                                var property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
                                if (property?.GetMethod != null && property.GetIndexParameters().Length == 0) list.Add((member, o => property.GetValue(o)));
                                else if (type.GetField(member, BindingFlags.Public | BindingFlags.Instance) is { } field) list.Add((member, o => field.GetValue(o)));
                            }
                readers[type] = list;
            }
            foreach (var (label, get) in list)
            {
                object value;
                try { value = get(c); }
                catch (TargetInvocationException e) { value = "!" + e.InnerException?.GetType().Name; }
                text.Append(' ').Append(label).Append('=').Append(Value(value));
            }
        }

        static readonly string[] textInfoCounts = { "characterCount", "spaceCount", "wordCount", "linkCount", "lineCount", "pageCount", "materialCount", "spriteCount" };

        static string TextInfoCounts(object info)
        {
            var type = info.GetType();
            var parts = new List<string>(textInfoCounts.Length);
            foreach (var name in textInfoCounts)
                if (type.GetField(name, BindingFlags.Public | BindingFlags.Instance) is { } f) parts.Add(System.Convert.ToString(f.GetValue(info), CultureInfo.InvariantCulture));
            return string.Join(",", parts);
        }

        static string Value(object v) => v switch
        {
            null => "null",
            _ when v.GetType().FullName == "TMPro.TMP_TextInfo" => TextInfoCounts(v),
            float f => F(f),
            Vector2 v2 => F(v2.x) + "," + F(v2.y),
            Color c => C(c),
            string s => "\"" + s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\"", "\\\"") + "\"",
            UnityEngine.Object o => o == null ? "null" : o.name,
            _ => System.Convert.ToString(v, CultureInfo.InvariantCulture),
        };

        static string F(float v)
        {
            var s = Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
            return s == "-0" ? "0" : s;
        }

        static string C(Color c) => $"{Math.Round(c.r * 255)},{Math.Round(c.g * 255)},{Math.Round(c.b * 255)},{Math.Round(c.a * 255)}";
    }
}
