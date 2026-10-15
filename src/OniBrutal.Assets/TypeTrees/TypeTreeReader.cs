using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OniBrutal.Assets.IO;

namespace OniBrutal.Assets.TypeTrees
{
    /// A PPtr: a reference to an object in this file (FileId 0) or an external file.
    public readonly record struct PPtr(int FileId, long PathId)
    {
        public bool IsNull => PathId == 0;
    }

    /// The fields of a deserialized structure, in serialization order.
    public sealed class Fields
    {
        readonly List<KeyValuePair<string, object?>> items = new();

        public void Add(string name, object? value) => items.Add(new(name, value));
        public IReadOnlyList<KeyValuePair<string, object?>> Items => items;

        public bool Contains(string name)
        {
            foreach (var kv in items) if (kv.Key == name) return true;
            return false;
        }

        public object? this[string name]
        {
            get
            {
                foreach (var kv in items) if (kv.Key == name) return kv.Value;
                throw new KeyNotFoundException(name);
            }
        }

        public T Get<T>(string name) => (T)this[name]!;
        public Fields Struct(string name) => (Fields)this[name]!;
        public PPtr PPtr(string name) => (PPtr)this[name]!;
        public int Int(string name) => Convert.ToInt32(this[name]);
        public float Float(string name) => (float)this[name]!;
        public bool Bool(string name) => (bool)this[name]!;
        public string String(string name) => (string)this[name]!;
        public object?[] Array(string name) => (object?[])this[name]!;
    }

    /// Reads data described by a type tree into Fields / arrays / primitives.
    public static class TypeTreeReader
    {
        /// Reads an object whose root node describes the whole object; the object must be
        /// consumed exactly (that is how type trees are validated against the data).
        public static Fields ReadObject(TypeTreeNode root, DataReader r, int expectedTrailingBytes = 0)
        {
            var fields = ReadFields(root, r);
            if (r.Remaining != expectedTrailingBytes)
                throw new InvalidDataException($"{root.TypeName}: {r.Remaining} bytes left after reading {r.Length} (expected {expectedTrailingBytes}).");
            return fields;
        }

        /// A native object of the given class, read with the release tree and checked for size.
        public static Fields ReadNative(int classId, DataReader r)
            => ReadObject(TypeTreeDatabase.Get(classId).Tree!, r, TypeTreeDatabase.TrailingBytes(classId));

        public static Fields ReadFields(TypeTreeNode node, DataReader r)
        {
            var fields = new Fields();
            foreach (var child in node.Children) fields.Add(child.Name, ReadValue(child, r));
            return fields;
        }

        public static object? ReadValue(TypeTreeNode node, DataReader r)
        {
            object? value = ReadValueCore(node, r);
            if (node.AlignAfter) r.Align(4);
            return value;
        }

        static object? ReadValueCore(TypeTreeNode node, DataReader r)
        {
            if (node.IsArray || node.TypeName == "Array") return ReadArray(node, r);
            switch (node.TypeName)
            {
                case "bool": return r.ReadBool();
                case "SInt8": return r.ReadSByte();
                case "UInt8": case "char": return r.ReadByte();
                case "SInt16": case "short": return r.ReadInt16();
                case "UInt16": case "unsigned short": return r.ReadUInt16();
                case "SInt32": case "int": return r.ReadInt32();
                case "UInt32": case "unsigned int": case "Type*": return r.ReadUInt32();
                case "SInt64": case "long long": return r.ReadInt64();
                case "UInt64": case "unsigned long long": case "FileSize": return r.ReadUInt64();
                case "float": return r.ReadSingle();
                case "double": return r.ReadDouble();
                case "string":
                {
                    int length = r.ReadInt32();
                    var s = Encoding.UTF8.GetString(r.ReadSpan(length));
                    // The alignment flag sits on the string's inner Array node.
                    if (node.Children.Count > 0 && node.Children[0].AlignAfter) r.Align(4);
                    return s;
                }
                case "TypelessData":
                {
                    int size = r.ReadInt32();
                    return r.ReadBytes(size);
                }
            }
            if (node.TypeName.StartsWith("PPtr<", StringComparison.Ordinal))
                return new PPtr(r.ReadInt32(), r.ReadInt64());
            // Containers (vector, map, set, staticvector) are a wrapper around one Array node.
            if (node.Children.Count == 1 && node.Children[0].IsArray) return ReadValue(node.Children[0], r);
            return ReadFields(node, r);
        }

        static object ReadArray(TypeTreeNode node, DataReader r)
        {
            // An "Array" node has children "size" and "data"; containers (vector, map, set,
            // staticvector) wrap one.
            var data = node.Children[1];
            int count = r.ReadInt32();
            if (count < 0 || count > r.Remaining) throw new InvalidDataException($"Array count {count} exceeds the data.");
            switch (data.TypeName)
            {
                case "UInt8" or "char" when data.Children.Count == 0: return r.ReadBytes(count);
                case "SInt32" or "int" when !data.AlignAfter: { var a = new int[count]; for (int i = 0; i < count; i++) a[i] = r.ReadInt32(); return a; }
                case "UInt32" or "unsigned int" when !data.AlignAfter: { var a = new uint[count]; for (int i = 0; i < count; i++) a[i] = r.ReadUInt32(); return a; }
                case "float" when !data.AlignAfter: { var a = new float[count]; for (int i = 0; i < count; i++) a[i] = r.ReadSingle(); return a; }
                case "UInt16" or "unsigned short" when !data.AlignAfter: { var a = new ushort[count]; for (int i = 0; i < count; i++) a[i] = r.ReadUInt16(); return a; }
            }
            var items = new object?[count];
            for (int i = 0; i < count; i++) items[i] = ReadValue(data, r);
            return items;
        }
    }
}
