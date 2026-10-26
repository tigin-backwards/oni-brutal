using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OniMoono.Assets.Shaders
{
    /// A member of an MSL struct with the offset Metal gives it: its type and name as declared,
    /// its array length (0 for none), and its byte offset, size and alignment.
    public readonly record struct MslMember(string Type, string Name, int ArraySize, int Offset, int Size, int Alignment)
    {
        /// The distance between array elements (the element's size rounded up to its alignment).
        public int Stride => ArraySize > 0 ? Size / ArraySize : Size;
    }

    /// The structs an MSL program declares (`struct Name { … };`), laid out by the Metal Shading
    /// Language's rules for the types HLSLcc emits in constant buffers: scalars of 4 bytes (half 2,
    /// bool 1), vectors of two of 8 and of three or four of 16 bytes aligned to their size,
    /// `packed_` vectors of their components' size aligned to one component, matrices as their
    /// columns, arrays of elements whose stride is their size, and the struct's size rounded up to
    /// its largest alignment. Only structs of such types are laid out (others are skipped).
    public static class MslStructs
    {
        static readonly Regex structStart = new(@"^\s*struct\s+(\w+)\s*$", RegexOptions.Compiled);
        static readonly Regex member = new(@"^\s*(\w+)\s+(\w+)\s*(?:\[\s*(\d+)\s*\])?\s*;\s*$", RegexOptions.Compiled);

        /// Every struct of the program that could be laid out, by name, members in order.
        public static Dictionary<string, MslMember[]> Layouts(string source)
        {
            var result = new Dictionary<string, MslMember[]>(StringComparer.Ordinal);
            var lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var start = structStart.Match(lines[i]);
                if (!start.Success || i + 1 >= lines.Length || lines[i + 1].Trim() != "{") continue;
                var members = new List<MslMember>();
                int offset = 0;
                bool ok = true;
                int j = i + 2;
                for (; j < lines.Length && !lines[j].TrimStart().StartsWith("};", StringComparison.Ordinal); j++)
                {
                    var m = member.Match(lines[j]);
                    if (!m.Success || !TryType(m.Groups[1].Value, out int size, out int align)) { ok = false; continue; }
                    int count = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
                    int stride = (size + align - 1) / align * align;
                    offset = (offset + align - 1) / align * align;
                    int total = count > 0 ? stride * count : size;
                    members.Add(new MslMember(m.Groups[1].Value, m.Groups[2].Value, count, offset, total, align));
                    offset += total;
                }
                if (ok) result[start.Groups[1].Value] = members.ToArray();
                i = j;
            }
            return result;
        }

        /// The struct's size: its end rounded up to its largest alignment.
        public static int SizeOf(MslMember[] members)
        {
            int end = 0, align = 1;
            foreach (var m in members)
            {
                end = Math.Max(end, m.Offset + m.Size);
                align = Math.Max(align, m.Alignment);
            }
            return (end + align - 1) / align * align;
        }

        /// Size and alignment of a type HLSLcc puts in constant buffers.
        public static bool TryType(string type, out int size, out int alignment)
        {
            size = alignment = 0;
            bool packed = type.StartsWith("packed_", StringComparison.Ordinal);
            var t = packed ? type["packed_".Length..] : type;
            int component;
            string rest;
            if (t.StartsWith("float", StringComparison.Ordinal)) { component = 4; rest = t[5..]; }
            else if (t.StartsWith("half", StringComparison.Ordinal)) { component = 2; rest = t[4..]; }
            else if (t.StartsWith("uint", StringComparison.Ordinal)) { component = 4; rest = t[4..]; }
            else if (t.StartsWith("int", StringComparison.Ordinal)) { component = 4; rest = t[3..]; }
            else if (t.StartsWith("bool", StringComparison.Ordinal)) { component = 1; rest = t[4..]; }
            else return false;
            int columns = 1, rows;
            if (rest.Length == 0) rows = 1;
            else if (rest.Length == 1 && rest[0] is >= '2' and <= '4') rows = rest[0] - '0';
            else if (rest.Length == 3 && rest[1] == 'x' && rest[0] is >= '2' and <= '4' && rest[2] is >= '2' and <= '4' && !packed)
            {
                columns = rest[0] - '0';
                rows = rest[2] - '0';
            }
            else return false;
            if (packed)
            {
                if (rows == 1) return false;
                size = component * rows;
                alignment = component;
                return true;
            }
            // A vector of three is laid out as one of four.
            int vector = component * (rows == 3 ? 4 : rows);
            size = vector * columns;
            alignment = vector;
            return true;
        }
    }
}
