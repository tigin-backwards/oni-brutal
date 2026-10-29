using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OniMoono.Engine.Serialization
{
    /// JsonUtility.FromJson and FromJsonOverwrite: Unity's RapidJSON reader and its serializer's
    /// field rules, as measured in the original player.
    ///
    /// - Parse errors are RapidJSON's messages after "JSON parse error: "; a root that is not an
    ///   object gives "JSON must represent an object type.".
    /// - Fields in serialization order, each from the first member with its exact name; a field
    ///   without a member keeps its value. A member of the wrong kind still sets the field: numbers
    ///   from numeric strings ("12", " 6"), else 0 (true gives 0); strings from numbers; booleans
    ///   from true, nonzero numbers or "true"; enums from numbers only; null gives "" for a
    ///   string and an empty list or array.
    /// - Integers truncate and wrap (5.7 is 5, 2147483648 is int.MinValue); float overflow is
    ///   infinity; -0.0 stays negative.
    /// - Composite and collection fields whose type comes from an assembly the game was not built
    ///   with (a mod's) are skipped, as JsonUtility.ToJson leaves them out; the root may be any type.
    internal static class JsonReader
    {
        // ---- The document ----

        sealed class Node
        {
            public char Kind; // o object, a array, s string, n number, t true, f false, z null
            public string Text = "";
            public List<(string Name, Node Value)> Members;
            public List<Node> Items;

            public Node Member(string name)
            {
                foreach (var (n, v) in Members) if (n == name) return v;
                return null;
            }
        }

        sealed class Parser(string text)
        {
            int pos;

            static Exception Error(string message) => new ArgumentException("JSON parse error: " + message);

            public Node ParseDocument()
            {
                SkipWhitespace();
                if (pos >= text.Length) throw Error("The document is empty.");
                var root = ParseValue();
                SkipWhitespace();
                if (pos < text.Length) throw Error("The document root must not follow by other values.");
                return root;
            }

            void SkipWhitespace()
            {
                while (pos < text.Length && text[pos] is ' ' or '\t' or '\n' or '\r') pos++;
            }

            char Peek => pos < text.Length ? text[pos] : '\0';

            Node ParseValue()
            {
                switch (Peek)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return new Node { Kind = 's', Text = ParseString() };
                    case 't': return Literal("true", 't');
                    case 'f': return Literal("false", 'f');
                    case 'n': return Literal("null", 'z');
                    default: return ParseNumber();
                }
            }

            Node Literal(string word, char kind)
            {
                if (string.CompareOrdinal(text, pos, word, 0, word.Length) != 0) throw Error("Invalid value.");
                pos += word.Length;
                return new Node { Kind = kind };
            }

            Node ParseObject()
            {
                pos++;
                var node = new Node { Kind = 'o', Members = new() };
                SkipWhitespace();
                if (Peek == '}') { pos++; return node; }
                while (true)
                {
                    if (Peek != '"') throw Error("Missing a name for object member.");
                    var name = ParseString();
                    SkipWhitespace();
                    if (Peek != ':') throw Error("Missing a colon after a name of object member.");
                    pos++;
                    SkipWhitespace();
                    node.Members.Add((name, ParseValue()));
                    SkipWhitespace();
                    if (Peek == ',') { pos++; SkipWhitespace(); continue; }
                    if (Peek == '}') { pos++; return node; }
                    throw Error("Missing a comma or '}' after an object member.");
                }
            }

            Node ParseArray()
            {
                pos++;
                var node = new Node { Kind = 'a', Items = new() };
                SkipWhitespace();
                if (Peek == ']') { pos++; return node; }
                while (true)
                {
                    node.Items.Add(ParseValue());
                    SkipWhitespace();
                    if (Peek == ',') { pos++; SkipWhitespace(); continue; }
                    if (Peek == ']') { pos++; return node; }
                    throw Error("Missing a comma or ']' after an array element.");
                }
            }

            string ParseString()
            {
                pos++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (pos >= text.Length) throw Error("Missing a closing quotation mark in string.");
                    char c = text[pos++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) throw Error(c == '\0' ? "Missing a closing quotation mark in string." : "Invalid encoding in string.");
                    if (c != '\\') { sb.Append(c); continue; }
                    if (pos >= text.Length) throw Error("Invalid escape character in string.");
                    char e = text[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            int unit = Hex4();
                            if (unit >= 0xD800 && unit <= 0xDBFF)
                            {
                                if (pos + 1 >= text.Length || text[pos] != '\\' || text[pos + 1] != 'u') throw Error("The surrogate pair in string is invalid.");
                                pos += 2;
                                int low = Hex4();
                                if (low < 0xDC00 || low > 0xDFFF) throw Error("The surrogate pair in string is invalid.");
                                sb.Append((char)unit).Append((char)low);
                            }
                            else sb.Append((char)unit);
                            break;
                        default: throw Error("Invalid escape character in string.");
                    }
                }
            }

            int Hex4()
            {
                int v = 0;
                for (int i = 0; i < 4; i++)
                {
                    int d = pos < text.Length ? HexDigit(text[pos]) : -1;
                    if (d < 0) throw Error("Incorrect hex digit after \\u escape in string.");
                    v = v * 16 + d;
                    pos++;
                }
                return v;
            }

            static int HexDigit(char c) => c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;

            Node ParseNumber()
            {
                int start = pos;
                if (Peek == '-') pos++;
                if (Peek == '0') pos++;
                else if (Peek >= '1' && Peek <= '9') { while (char.IsAsciiDigit(Peek)) pos++; }
                else throw Error("Invalid value.");
                if (Peek == '.')
                {
                    pos++;
                    if (!char.IsAsciiDigit(Peek)) throw Error("Miss fraction part in number.");
                    while (char.IsAsciiDigit(Peek)) pos++;
                }
                if (Peek is 'e' or 'E')
                {
                    pos++;
                    if (Peek is '+' or '-') pos++;
                    if (!char.IsAsciiDigit(Peek)) throw Error("Miss exponent in number.");
                    while (char.IsAsciiDigit(Peek)) pos++;
                }
                return new Node { Kind = 'n', Text = text.Substring(start, pos - start) };
            }
        }

        // ---- Applying it ----

        public static object Read(string json, Type type)
        {
            var root = Parse(json);
            object target = Cloner.NewInstance(type);
            EmptyLists(target);
            Fill(target, root);
            return target;
        }

        /// Null list fields become empty, arrays stay null (measured: FromJson("{}") of a class
        /// with neither initialized gives list=[] and arr=null; FromJsonOverwrite of a member it
        /// does not name does the same).
        static void EmptyLists(object target)
        {
            if (target is Object) return;
            var shape = SerializedLayout.ShapeOf(target.GetType());
            if (shape != null && shape.Kind != ValueKind.Composite) return;
            var fields = SerializedLayout.CompositeFields(shape ?? new ValueShape { Type = target.GetType(), Kind = ValueKind.Composite, IsValueType = target.GetType().IsValueType });
            foreach (var f in fields)
                if (f.Shape.Kind == ValueKind.List && JsonLayout.Known(f.Shape) && f.Field.GetValue(target) == null)
                    f.Field.SetValue(target, Activator.CreateInstance(f.Shape.Type));
        }

        public static void Overwrite(string json, object target)
        {
            var root = Parse(json);
            EmptyLists(target);
            Fill(target, root);
        }

        static Node Parse(string json)
        {
            var root = new Parser(json).ParseDocument();
            if (root.Kind != 'o') throw new ArgumentException("JSON must represent an object type.");
            return root;
        }

        static void Fill(object target, Node node)
        {
            var type = target.GetType();
            FieldShape[] fields;
            if (target is Object)
                fields = SerializedLayout.ScriptFields(type);
            else
            {
                var shape = SerializedLayout.ShapeOf(type);
                if (shape?.Kind is ValueKind.BuiltinStruct or ValueKind.BuiltinClass)
                {
                    // A built-in value as the root: its members as the writer names them.
                    return;
                }
                fields = SerializedLayout.CompositeFields(new ValueShape { Type = type, Kind = ValueKind.Composite, IsValueType = type.IsValueType });
            }
            SetFields(target, fields, node);
            SerializedLayout.AfterDeserialize(target);
        }

        /// A boxed struct is written in place, so callers set it back.
        static void SetFields(object target, FieldShape[] fields, Node node)
        {
            foreach (var f in fields)
            {
                if (!JsonLayout.Known(f.Shape)) continue;
                var member = node.Member(f.Name);
                if (member == null) continue;
                f.Field.SetValue(target, Convert(member, f.Shape, f.Field.GetValue(target)));
            }
        }

        static object Convert(Node node, ValueShape shape, object current)
        {
            switch (shape.Kind)
            {
                case ValueKind.Bool: return node.Kind == 't' || (node.Kind == 'n' && Double(node) != 0) || (node.Kind == 's' && node.Text == "true");
                case ValueKind.SByte: return unchecked((sbyte)Integer(node));
                case ValueKind.Byte: return unchecked((byte)Integer(node));
                case ValueKind.Short: return unchecked((short)Integer(node));
                case ValueKind.UShort: return unchecked((ushort)Integer(node));
                case ValueKind.Char: return unchecked((char)Integer(node));
                case ValueKind.Int: return unchecked((int)Integer(node));
                case ValueKind.UInt: return unchecked((uint)Integer(node));
                case ValueKind.Long: return Integer(node);
                case ValueKind.ULong: return unchecked((ulong)Integer(node));
                case ValueKind.Float: return (float)Double(node);
                case ValueKind.Double: return Double(node);
                case ValueKind.Enum:
                    return Enum.ToObject(shape.Type, node.Kind == 'n' ? Integer(node) : 0);
                case ValueKind.String:
                    return node.Kind is 's' or 'n' ? node.Text : "";
                case ValueKind.ObjectReference:
                    return current;
                case ValueKind.BuiltinStruct:
                    return node.Kind == 'o' ? BuiltinStruct(node, current ?? Activator.CreateInstance(shape.Type)) : Activator.CreateInstance(shape.Type);
                case ValueKind.BuiltinClass:
                    return current;
                case ValueKind.Composite:
                {
                    var value = current ?? Cloner.NewInstance(shape.Type);
                    if (node.Kind == 'o')
                    {
                        SetFields(value, SerializedLayout.CompositeFields(shape), node);
                        SerializedLayout.AfterDeserialize(value);
                    }
                    return value;
                }
                case ValueKind.Array:
                case ValueKind.List:
                {
                    var items = node.Kind == 'a' ? node.Items : new List<Node>();
                    if (shape.Kind == ValueKind.Array)
                    {
                        var array = Array.CreateInstance(shape.Element.Type, items.Count);
                        for (int i = 0; i < items.Count; i++) array.SetValue(Convert(items[i], shape.Element, null), i);
                        return array;
                    }
                    var list = (IList)Activator.CreateInstance(shape.Type);
                    foreach (var item in items) list.Add(Convert(item, shape.Element, null));
                    return list;
                }
                default:
                    throw new InvalidOperationException("Unhandled value kind " + shape.Kind);
            }
        }

        /// A number's value, a numeric string's (leading whitespace allowed), else 0.
        static double Double(Node node)
        {
            string text = node.Kind == 'n' ? node.Text : node.Kind == 's' ? node.Text.TrimStart() : null;
            return text != null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
        }

        /// Integers keep every digit (a long's 9000000000); fractions truncate.
        static long Integer(Node node)
        {
            string text = node.Kind == 'n' ? node.Text : node.Kind == 's' ? node.Text.TrimStart() : null;
            if (text == null) return 0;
            if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l)) return l;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return 0;
            return d >= long.MinValue && d < 9.2233720368547758E18 ? (long)d : long.MinValue;
        }

        static float Float(Node obj, string name, float current)
        {
            var m = obj.Member(name);
            return m == null ? current : (float)Double(m);
        }

        static int Int(Node obj, string name, int current)
        {
            var m = obj.Member(name);
            return m == null ? current : unchecked((int)Integer(m));
        }

        /// The built-in structs by the member names JsonUtility.ToJson writes.
        static object BuiltinStruct(Node n, object current)
        {
            switch (current)
            {
                case Vector2 v: return new Vector2(Float(n, "x", v.x), Float(n, "y", v.y));
                case Vector3 v: return new Vector3(Float(n, "x", v.x), Float(n, "y", v.y), Float(n, "z", v.z));
                case Vector4 v: return new Vector4(Float(n, "x", v.x), Float(n, "y", v.y), Float(n, "z", v.z), Float(n, "w", v.w));
                case Quaternion q: return new Quaternion(Float(n, "x", q.x), Float(n, "y", q.y), Float(n, "z", q.z), Float(n, "w", q.w));
                case Color c: return new Color(Float(n, "r", c.r), Float(n, "g", c.g), Float(n, "b", c.b), Float(n, "a", c.a));
                case Color32 c:
                {
                    var m = n.Member("rgba");
                    if (m == null) return c;
                    uint rgba = unchecked((uint)Integer(m));
                    return new Color32((byte)rgba, (byte)(rgba >> 8), (byte)(rgba >> 16), (byte)(rgba >> 24));
                }
                case Rect r: return new Rect(Float(n, "x", r.x), Float(n, "y", r.y), Float(n, "width", r.width), Float(n, "height", r.height));
                case Vector2Int v: return new Vector2Int(Int(n, "x", v.x), Int(n, "y", v.y));
                case Vector3Int v: return new Vector3Int(Int(n, "x", v.x), Int(n, "y", v.y), Int(n, "z", v.z));
                case LayerMask m: return (LayerMask)Int(n, "m_Bits", m.value);
                default: return current;
            }
        }
    }

    /// Which values JsonUtility handles: a composite type (or a collection of one) must come from
    /// an assembly the game was built with. Measured in the original player: a mod's
    /// [Serializable] class as a field is neither written by ToJson nor read by FromJson, while
    /// the game's own (Vector2I) is; a mod's type as the root is read and written.
    internal static class JsonLayout
    {
        public static bool Known(ValueShape shape) => SerializedLayout.Known(shape);
    }
}
