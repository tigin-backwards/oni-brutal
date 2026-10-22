using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace E2eDriver
{
    /// state.get's path language and setup.call's reflective calls (README.md, "state.get").
    static class State
    {
        /// A static context: members read from here are the type's static ones.
        sealed class Static
        {
            public readonly Type Type;
            public Static(Type type) { Type = type; }
        }

        public static object Eval(string expr)
        {
            expr = expr.Trim();
            object current;
            string rest;
            if (expr.StartsWith("selected:", StringComparison.Ordinal))
            {
                var selected = SelectTool.Instance == null ? null : SelectTool.Instance.selected;
                if (selected == null) throw new DriverError("nothing is selected");
                current = selected.gameObject;
                rest = expr.Substring("selected:".Length);
            }
            else if (Prefixed(expr, "ui:", out var pattern, out rest))
            {
                var found = Ui.Find(new JObject { ["path"] = pattern });
                if (found.Count == 0) throw new DriverError("no active element matches " + pattern);
                current = found[0].gameObject;
            }
            else if (Prefixed(expr, "type:", out var typeName, out rest))
            {
                var type = Values.FindType(typeName) ?? throw new DriverError("no type " + typeName);
                current = new Static(type);
            }
            else if (Prefixed(expr, "all:", out typeName, out rest))
            {
                var type = Values.FindType(typeName) ?? throw new DriverError("no type " + typeName);
                if (!typeof(UnityEngine.Object).IsAssignableFrom(type)) throw new DriverError(typeName + " is not a Unity object type");
                current = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.InstanceID);
            }
            else
            {
                // The longest leading run of plain segments that names a type.
                var segments = Split(expr);
                int i = segments.Count;
                Type type = null;
                for (; i > 0; i--)
                {
                    if (segments.Take(i).Any(s => s.IndexOfAny(new[] { '(', '[', '<' }) >= 0)) continue;
                    type = Values.FindType(string.Join(".", segments.Take(i)));
                    if (type != null) break;
                }
                if (type == null) throw new DriverError("no type at the start of " + expr);
                current = new Static(type);
                rest = string.Join(".", segments.Skip(i));
            }
            foreach (var segment in Split(rest)) current = Apply(current, segment);
            return current is Static s2 ? s2.Type : current;
        }

        /// "type:NAME::rest" (rest optional); the name may hold dots, so "::" ends it.
        static bool Prefixed(string expr, string prefix, out string name, out string rest)
        {
            name = rest = null;
            if (!expr.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var body = expr.Substring(prefix.Length);
            int split = body.IndexOf("::", StringComparison.Ordinal);
            name = split < 0 ? body : body.Substring(0, split);
            rest = split < 0 ? "" : body.Substring(split + 2);
            return true;
        }

        /// Dot-separated segments, dots inside <>, (), [] and quotes kept.
        static List<string> Split(string text)
        {
            var parts = new List<string>();
            int depth = 0, start = 0;
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"') quoted = !quoted;
                else if (quoted) continue;
                else if (c == '<' || c == '(' || c == '[') depth++;
                else if (c == '>' || c == ')' || c == ']') depth--;
                else if (c == '.' && depth == 0)
                {
                    parts.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }
            parts.Add(text.Substring(start));
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        }

        static object Apply(object current, string segment)
        {
            // The segment's name, then optional <T>, optional (args), then any [index]es.
            int i = 0;
            while (i < segment.Length && segment[i] != '<' && segment[i] != '(' && segment[i] != '[') i++;
            var name = segment.Substring(0, i);
            string generic = null, args = null;
            if (i < segment.Length && segment[i] == '<')
            {
                int end = segment.IndexOf('>', i);
                generic = segment.Substring(i + 1, end - i - 1).Trim();
                i = end + 1;
            }
            if (i < segment.Length && segment[i] == '(')
            {
                int end = Close(segment, i, '(', ')');
                args = segment.Substring(i + 1, end - i - 1);
                i = end + 1;
            }
            if (name.Length > 0)
            {
                if (current == null) throw new DriverError($"{segment}: the value before it is null");
                if (generic != null) current = ComponentQuery(current, name, generic);
                else if (args != null) current = CallMethod(current, name, ParseArgs(args));
                else current = Member(current, name);
            }
            while (i < segment.Length && segment[i] == '[')
            {
                int end = Close(segment, i, '[', ']');
                current = Index(current, segment.Substring(i + 1, end - i - 1).Trim());
                i = end + 1;
            }
            if (i < segment.Length) throw new DriverError("cannot read segment " + segment);
            return current;
        }

        static int Close(string text, int open, char a, char b)
        {
            int depth = 0;
            bool quoted = false;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '"') quoted = !quoted;
                if (quoted) continue;
                if (text[i] == a) depth++;
                else if (text[i] == b && --depth == 0) return i;
            }
            throw new DriverError("unbalanced " + a + " in " + text);
        }

        static JArray ParseArgs(string text)
        {
            var list = new JArray();
            if (text.Trim().Length == 0) return list;
            try { foreach (var t in JArray.Parse("[" + text + "]")) list.Add(t); }
            catch (Exception) { throw new DriverError("arguments are not JSON values: " + text); }
            return list;
        }

        static GameObject GameObjectOf(object o) => o switch
        {
            GameObject go => go,
            Component c => c.gameObject,
            _ => null,
        };

        static object ComponentQuery(object current, string name, string typeName)
        {
            var go = GameObjectOf(current) ?? throw new DriverError($"{name}<{typeName}> needs a GameObject or Component");
            var type = Values.FindType(typeName) ?? throw new DriverError("no type " + typeName);
            switch (name)
            {
                case "GetComponent": return go.GetComponent(type);
                case "GetComponents": return go.GetComponents(type);
                case "GetComponentInChildren": return go.GetComponentInChildren(type, true);
                case "GetComponentsInChildren": return go.GetComponentsInChildren(type, true);
                case "GetComponentInParent": return go.GetComponentInParent(type);
                default: throw new DriverError("unknown query " + name + "<>");
            }
        }

        static object Member(object current, string name)
        {
            var isStatic = current is Static;
            var type = isStatic ? ((Static)current).Type : current.GetType();
            var instance = isStatic ? null : current;
            var flags = (isStatic ? BindingFlags.Static : BindingFlags.Instance | BindingFlags.Static) | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, flags);
                if (f != null) return f.GetValue(f.IsStatic ? null : instance);
                var p = t.GetProperty(name, flags);
                if (p != null && p.GetMethod != null && p.GetIndexParameters().Length == 0) return p.GetValue(p.GetMethod.IsStatic ? null : instance);
            }
            // A nested type of a static context.
            if (isStatic && type.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic) is { } nested) return new Static(nested);
            // A component of a GameObject by its type name.
            if (GameObjectOf(current) is { } go && Values.FindType(name) is { } ct && typeof(Component).IsAssignableFrom(ct)) return go.GetComponent(ct);
            throw new DriverError($"no field or property {name} on {Values.TypeName(type)}");
        }

        public static object CallMethod(object current, string name, JArray args)
        {
            var isStatic = current is Static;
            var type = isStatic ? ((Static)current).Type : current.GetType();
            var instance = isStatic ? null : current;
            var flags = (isStatic ? BindingFlags.Static : BindingFlags.Instance | BindingFlags.Static) | BindingFlags.Public | BindingFlags.NonPublic;
            var candidates = new List<MethodInfo>();
            for (var t = type; t != null; t = t.BaseType)
                foreach (var m in t.GetMethods(flags | BindingFlags.DeclaredOnly))
                    if (m.Name == name && !m.IsGenericMethodDefinition) candidates.Add(m);
            if (candidates.Count == 0) throw new DriverError($"no method {name} on {Values.TypeName(type)}");
            // The overload whose parameters fit the arguments, preferring exact kinds (a JSON
            // integer for an integer parameter, a float for a floating one) and fewer defaults.
            MethodInfo best = null;
            object[] bestValues = null;
            int bestScore = int.MinValue;
            foreach (var m in candidates)
            {
                var ps = m.GetParameters();
                if (args.Count > ps.Length) continue;
                var values = new object[ps.Length];
                bool fits = true;
                int score = 0;
                for (int i = 0; i < ps.Length && fits; i++)
                {
                    if (i < args.Count)
                    {
                        fits = Values.Convert(args[i], ps[i].ParameterType, out values[i]);
                        if (fits && Exact(args[i], ps[i].ParameterType)) score += 2;
                    }
                    else if (ps[i].IsOptional) { values[i] = ps[i].DefaultValue is DBNull ? Type.Missing : ps[i].DefaultValue; score--; }
                    else fits = false;
                }
                if (fits && score > bestScore) { best = m; bestValues = values; bestScore = score; }
            }
            if (best != null) return best.Invoke(best.IsStatic ? null : instance, bestValues);
            throw new DriverError($"no overload of {Values.TypeName(type)}.{name} takes ({string.Join(", ", args.Select(a => a.Type.ToString()))})");
        }

        static bool Exact(JToken token, Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            switch (token.Type)
            {
                case JTokenType.Integer: return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);
                case JTokenType.Float: return type == typeof(float) || type == typeof(double);
                case JTokenType.String: return type == typeof(string);
                case JTokenType.Boolean: return type == typeof(bool);
                default: return false;
            }
        }

        static object Index(object current, string text)
        {
            if (current == null) throw new DriverError($"[{text}]: the value before it is null");
            JToken key;
            try { key = JToken.Parse(text); }
            catch (Exception) { key = text; }
            if (current is IDictionary dict)
            {
                var keyType = current.GetType().IsGenericType ? current.GetType().GetGenericArguments()[0] : typeof(object);
                if (!Values.Convert(key, keyType, out var k))
                {
                    var ctor = keyType.GetConstructor(new[] { typeof(string) });
                    if (ctor == null || key.Type != JTokenType.String) throw new DriverError($"[{text}] is not a key of {Values.TypeName(current.GetType())}");
                    k = ctor.Invoke(new object[] { (string)key });
                }
                if (!dict.Contains(k)) throw new DriverError($"no key {text}");
                return dict[k];
            }
            if (key.Type != JTokenType.Integer) throw new DriverError($"[{text}]: {Values.TypeName(current.GetType())} takes an integer index");
            int index = (int)key;
            if (current is IList list)
            {
                if (index < 0) index += list.Count;
                if (index < 0 || index >= list.Count) throw new DriverError($"index {text} out of range (count {list.Count})");
                return list[index];
            }
            if (current is IEnumerable items)
            {
                int n = 0;
                foreach (var item in items) if (n++ == index) return item;
                throw new DriverError($"index {text} out of range (count {n})");
            }
            throw new DriverError($"[{text}]: {Values.TypeName(current.GetType())} is not indexable");
        }

        /// setup.call: {type, method, args} for a static method, or {target, method, args} for one
        /// on the object a state.get expression names.
        public static object Call(JObject args)
        {
            var method = (string)args["method"] ?? throw new DriverError("setup.call needs a method");
            var list = args["args"] as JArray ?? new JArray();
            object on;
            if (args["target"] != null)
            {
                on = Eval((string)args["target"]);
                if (on == null) throw new DriverError("the target is null");
                if (on is Type t) on = new Static(t);
            }
            else
            {
                var typeName = (string)args["type"] ?? throw new DriverError("setup.call needs a type or a target");
                on = new Static(Values.FindType(typeName) ?? throw new DriverError("no type " + typeName));
            }
            return CallMethod(on, method, list);
        }

        public static JObject Result(object value, JObject args)
        {
            int depth = (int?)args["depth"] ?? 2, limit = (int?)args["limit"] ?? 200;
            return new JObject
            {
                ["value"] = Values.ToJson(value, depth, limit),
                ["type"] = Values.TypeName(value?.GetType()),
            };
        }
    }
}
