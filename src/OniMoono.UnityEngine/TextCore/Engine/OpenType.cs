using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace OniMoono.Engine.Text
{
    /// Read-only view of one face's sfnt tables (TrueType/OpenType, including a face inside a
    /// TrueType collection). FreeType does not expose OpenType layout data, so the font
    /// engine's feature queries (GPOS kerning and mark attachment, GSUB ligatures, GDEF glyph
    /// classes, the legacy 'kern' table, OS/2 metrics) read the tables here.
    internal sealed class SfntTables
    {
        readonly byte[] data;
        readonly Dictionary<uint, (int Offset, int Length)> tables = new();

        public SfntTables(byte[] data, int faceIndex)
        {
            this.data = data;
            if (data.Length < 12) return;
            int dir = 0;
            if (U32(0) == Tag("ttcf"))
            {
                uint count = U32(8);
                if (faceIndex < 0 || faceIndex >= count) return;
                dir = (int)U32(12 + 4 * faceIndex);
            }
            int numTables = U16(dir + 4);
            for (int i = 0; i < numTables; i++)
            {
                int rec = dir + 12 + 16 * i;
                if (rec + 16 > data.Length) break;
                uint offset = U32(rec + 8), length = U32(rec + 12);
                if ((long)offset + length <= data.Length) tables[U32(rec)] = ((int)offset, (int)length);
            }
        }

        public static uint Tag(string s) => (uint)(s[0] << 24 | s[1] << 16 | s[2] << 8 | s[3]);

        public bool TryGet(string tag, out int offset)
        {
            if (tables.TryGetValue(Tag(tag), out var t)) { offset = t.Offset; return true; }
            offset = 0;
            return false;
        }

        public ushort U16(int o) => o + 2 <= data.Length ? BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(o)) : (ushort)0;
        public short S16(int o) => (short)U16(o);
        public uint U32(int o) => o + 4 <= data.Length ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(o)) : 0u;

        // ----- OS/2 -----

        /// OS/2 fields used for face metrics (design units); false when the table is absent.
        public bool TryGetOs2(out Os2 os2)
        {
            os2 = default;
            if (!TryGet("OS/2", out int o)) return false;
            ushort version = U16(o);
            os2.SubscriptYSize = S16(o + 12);
            os2.SubscriptYOffset = S16(o + 16);
            os2.SuperscriptYSize = S16(o + 20);
            os2.SuperscriptYOffset = S16(o + 24);
            os2.StrikeoutSize = S16(o + 26);
            os2.StrikeoutPosition = S16(o + 28);
            if (version >= 2)
            {
                os2.XHeight = S16(o + 86);
                os2.CapHeight = S16(o + 88);
                os2.HasHeights = true;
            }
            return true;
        }

        /// A face's line metrics in design units, as fonts made from OS fonts take them (measured
        /// 2026-10-24: FreeSans, Ubuntu, DejaVu Sans, Liberation Sans): OS/2's typographic values
        /// when its USE_TYPO_METRICS bit (fsSelection bit 7) is set, else hhea's; the line
        /// spacing is ascender − descender + line gap.
        public bool TryGetLineMetrics(out int ascender, out int descender, out int lineSpacing)
        {
            ascender = descender = lineSpacing = 0;
            int gap;
            if (TryGet("OS/2", out int o) && (U16(o + 62) & 0x80) != 0)
            {
                ascender = S16(o + 68);
                descender = S16(o + 70);
                gap = S16(o + 72);
            }
            else if (TryGet("hhea", out int h))
            {
                ascender = S16(h + 4);
                descender = S16(h + 6);
                gap = S16(h + 8);
            }
            else return false;
            lineSpacing = ascender - descender + gap;
            return true;
        }

        internal struct Os2
        {
            public short SubscriptYSize, SubscriptYOffset, SuperscriptYSize, SuperscriptYOffset, StrikeoutSize, StrikeoutPosition, XHeight, CapHeight;
            public bool HasHeights;
        }

        // ----- Common layout structures -----

        /// Coverage table: glyph → coverage index, or -1.
        public int CoverageIndex(int coverage, uint glyph)
        {
            int format = U16(coverage);
            if (format == 1)
            {
                int count = U16(coverage + 2);
                int lo = 0, hi = count - 1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    uint g = U16(coverage + 4 + 2 * mid);
                    if (g == glyph) return mid;
                    if (g < glyph) lo = mid + 1; else hi = mid - 1;
                }
            }
            else if (format == 2)
            {
                int count = U16(coverage + 2);
                for (int i = 0; i < count; i++)
                {
                    int r = coverage + 4 + 6 * i;
                    uint start = U16(r), end = U16(r + 2);
                    if (glyph >= start && glyph <= end) return U16(r + 4) + (int)(glyph - start);
                }
            }
            return -1;
        }

        /// The glyphs of a coverage table in coverage-index order.
        public List<uint> CoverageGlyphs(int coverage)
        {
            var list = new List<uint>();
            int format = U16(coverage);
            int count = U16(coverage + 2);
            if (format == 1)
            {
                for (int i = 0; i < count; i++) list.Add(U16(coverage + 4 + 2 * i));
            }
            else if (format == 2)
            {
                var ranges = new List<(int Index, uint Start, uint End)>();
                for (int i = 0; i < count; i++)
                {
                    int r = coverage + 4 + 6 * i;
                    ranges.Add((U16(r + 4), U16(r), U16(r + 2)));
                }
                ranges.Sort((a, b) => a.Index.CompareTo(b.Index));
                foreach (var (_, start, end) in ranges)
                    for (uint g = start; g <= end; g++) list.Add(g);
            }
            return list;
        }

        /// Class definition table: glyph → class (0 when not listed).
        public int ClassOf(int classDef, uint glyph)
        {
            int format = U16(classDef);
            if (format == 1)
            {
                uint start = U16(classDef + 2);
                int count = U16(classDef + 4);
                if (glyph >= start && glyph < start + count) return U16(classDef + 6 + 2 * (int)(glyph - start));
            }
            else if (format == 2)
            {
                int count = U16(classDef + 2);
                int lo = 0, hi = count - 1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    int r = classDef + 4 + 6 * mid;
                    uint start = U16(r), end = U16(r + 2);
                    if (glyph < start) hi = mid - 1;
                    else if (glyph > end) lo = mid + 1;
                    else return U16(r + 4);
                }
            }
            return 0;
        }

        /// Every glyph with a non-zero class in a class definition table, with its class.
        public List<(uint Glyph, int Class)> ClassMembers(int classDef)
        {
            var list = new List<(uint, int)>();
            int format = U16(classDef);
            if (format == 1)
            {
                uint start = U16(classDef + 2);
                int count = U16(classDef + 4);
                for (int i = 0; i < count; i++)
                {
                    int c = U16(classDef + 6 + 2 * i);
                    if (c != 0) list.Add((start + (uint)i, c));
                }
            }
            else if (format == 2)
            {
                int count = U16(classDef + 2);
                for (int i = 0; i < count; i++)
                {
                    int r = classDef + 4 + 6 * i;
                    int c = U16(r + 4);
                    if (c == 0) continue;
                    for (uint g = U16(r); g <= U16(r + 2); g++) list.Add((g, c));
                }
            }
            return list;
        }

        /// Absolute offsets of the subtables of the lookups that the given feature tags
        /// reference (any script/language), with their lookup type; extension subtables
        /// (GPOS 9, GSUB 7) are resolved to the subtable they wrap. Lookups keep LookupList
        /// order, which is OpenType's precedence order.
        public List<(int Type, int Offset)> FeatureSubtables(string table, string feature)
        {
            var result = new List<(int, int)>();
            if (!TryGet(table, out int t)) return result;
            int featureList = t + U16(t + 6), lookupList = t + U16(t + 8);
            var lookupIndices = new SortedSet<int>();
            int featureCount = U16(featureList);
            uint tag = Tag(feature);
            for (int i = 0; i < featureCount; i++)
            {
                int rec = featureList + 2 + 6 * i;
                if (U32(rec) != tag) continue;
                int f = featureList + U16(rec + 4);
                int n = U16(f + 2);
                for (int k = 0; k < n; k++) lookupIndices.Add(U16(f + 4 + 2 * k));
            }
            int extensionType = table == "GPOS" ? 9 : 7;
            int lookupCount = U16(lookupList);
            foreach (int index in lookupIndices)
            {
                if (index >= lookupCount) continue;
                int lookup = lookupList + U16(lookupList + 2 + 2 * index);
                int type = U16(lookup);
                int subCount = U16(lookup + 4);
                for (int s = 0; s < subCount; s++)
                {
                    int sub = lookup + U16(lookup + 6 + 2 * s);
                    if (type == extensionType)
                        result.Add((U16(sub + 2), sub + (int)U32(sub + 4)));
                    else
                        result.Add((type, sub));
                }
            }
            return result;
        }

        // ----- GDEF -----

        /// GDEF glyph class (1 base, 2 ligature, 3 mark, 4 component; 0 undefined).
        public int GlyphClass(uint glyph)
        {
            if (!TryGet("GDEF", out int g)) return 0;
            int classDef = U16(g + 4);
            return classDef == 0 ? 0 : ClassOf(g + classDef, glyph);
        }

        // ----- GPOS -----

        internal readonly record struct ValueRecord(short XPlacement, short YPlacement, short XAdvance, short YAdvance)
        {
            public bool IsZero => XPlacement == 0 && YPlacement == 0 && XAdvance == 0 && YAdvance == 0;
        }

        static int ValueSize(int format) => 2 * System.Numerics.BitOperations.PopCount((uint)format & 0xFF);

        ValueRecord ReadValue(int o, int format)
        {
            short xp = 0, yp = 0, xa = 0, ya = 0;
            if ((format & 1) != 0) { xp = S16(o); o += 2; }
            if ((format & 2) != 0) { yp = S16(o); o += 2; }
            if ((format & 4) != 0) { xa = S16(o); o += 2; }
            if ((format & 8) != 0) { ya = S16(o); }
            return new ValueRecord(xp, yp, xa, ya);
        }

        /// Pair adjustments of the 'kern' feature (PairPos formats 1 and 2), in design units.
        /// The first subtable that covers a pair decides it; class-based pairs are expanded
        /// over the glyphs listed in the second class definition (class 0 is not expanded) and
        /// records whose two value records are both zero are dropped. `accept` filters pairs.
        public List<(uint First, ValueRecord V1, uint Second, ValueRecord V2)> PairAdjustments(Func<uint, uint, bool> accept)
        {
            var result = new List<(uint, ValueRecord, uint, ValueRecord)>();
            var seen = new HashSet<ulong>();
            foreach (var (type, sub) in FeatureSubtables("GPOS", "kern"))
            {
                if (type != 2) continue;
                int format = U16(sub);
                int coverage = sub + U16(sub + 2);
                int vf1 = U16(sub + 4), vf2 = U16(sub + 6);
                int s1 = ValueSize(vf1), s2 = ValueSize(vf2);
                var firsts = CoverageGlyphs(coverage);
                if (format == 1)
                {
                    int setCount = U16(sub + 8);
                    for (int i = 0; i < firsts.Count && i < setCount; i++)
                    {
                        uint first = firsts[i];
                        int set = sub + U16(sub + 10 + 2 * i);
                        int pairs = U16(set);
                        int recSize = 2 + s1 + s2;
                        for (int p = 0; p < pairs; p++)
                        {
                            int r = set + 2 + recSize * p;
                            uint second = U16(r);
                            Add(first, second, ReadValue(r + 2, vf1), ReadValue(r + 2 + s1, vf2));
                        }
                    }
                }
                else if (format == 2)
                {
                    int classDef1 = sub + U16(sub + 8), classDef2 = sub + U16(sub + 10);
                    int class1Count = U16(sub + 12), class2Count = U16(sub + 14);
                    var seconds = ClassMembers(classDef2);
                    int recSize = s1 + s2;
                    foreach (uint first in firsts)
                    {
                        int c1 = ClassOf(classDef1, first);
                        if (c1 >= class1Count) continue;
                        foreach (var (second, c2) in seconds)
                        {
                            if (c2 >= class2Count) continue;
                            int r = sub + 16 + (c1 * class2Count + c2) * recSize;
                            Add(first, second, ReadValue(r, vf1), ReadValue(r + s1, vf2));
                        }
                    }
                }
            }
            return result;

            void Add(uint first, uint second, ValueRecord v1, ValueRecord v2)
            {
                if (!seen.Add((ulong)first << 32 | second)) return;
                if (v1.IsZero && v2.IsZero) return;
                if (accept(first, second)) result.Add((first, v1, second, v2));
            }
        }

        (short X, short Y) Anchor(int anchor) => (S16(anchor + 2), S16(anchor + 4));

        /// Mark attachments (MarkBasePos via 'mark', MarkMarkPos via 'mkmk'), in design units:
        /// (base glyph, base anchor, mark glyph, mark anchor). The first subtable covering a
        /// (base, mark) pair decides it.
        public List<(uint Base, (short X, short Y) BaseAnchor, uint Mark, (short X, short Y) MarkAnchor)> MarkAttachments(bool markToMark, Func<uint, uint, bool> accept)
        {
            var result = new List<(uint, (short, short), uint, (short, short))>();
            var seen = new HashSet<ulong>();
            foreach (var (type, sub) in FeatureSubtables("GPOS", markToMark ? "mkmk" : "mark"))
            {
                if (type != (markToMark ? 6 : 4) || U16(sub) != 1) continue;
                // Both formats share the layout: mark (combining) coverage, base coverage,
                // class count, mark array, base array.
                var marks = CoverageGlyphs(sub + U16(sub + 2));
                var bases = CoverageGlyphs(sub + U16(sub + 4));
                int classCount = U16(sub + 6);
                int markArray = sub + U16(sub + 8), baseArray = sub + U16(sub + 10);
                int baseCount = U16(baseArray);
                for (int b = 0; b < bases.Count && b < baseCount; b++)
                {
                    for (int m = 0; m < marks.Count && m < U16(markArray); m++)
                    {
                        int markRec = markArray + 2 + 4 * m;
                        int cls = U16(markRec);
                        if (cls >= classCount) continue;
                        int baseAnchorOffset = U16(baseArray + 2 + 2 * (b * classCount + cls));
                        if (baseAnchorOffset == 0) continue;
                        if (!seen.Add((ulong)bases[b] << 32 | marks[m])) continue;
                        if (!accept(bases[b], marks[m])) continue;
                        var baseAnchor = Anchor(baseArray + baseAnchorOffset);
                        var markAnchor = Anchor(markArray + U16(markRec + 2));
                        result.Add((bases[b], baseAnchor, marks[m], markAnchor));
                    }
                }
            }
            return result;
        }

        // ----- GSUB -----

        /// Ligature substitutions of the 'liga' feature: component glyphs (first included) and
        /// the ligature glyph.
        public List<(uint[] Components, uint Ligature)> Ligatures(Func<uint[], bool> accept)
        {
            var result = new List<(uint[], uint)>();
            foreach (var (type, sub) in FeatureSubtables("GSUB", "liga"))
            {
                if (type != 4 || U16(sub) != 1) continue;
                var firsts = CoverageGlyphs(sub + U16(sub + 2));
                int setCount = U16(sub + 4);
                for (int i = 0; i < firsts.Count && i < setCount; i++)
                {
                    int set = sub + U16(sub + 6 + 2 * i);
                    int count = U16(set);
                    for (int l = 0; l < count; l++)
                    {
                        int lig = set + U16(set + 2 + 2 * l);
                        uint ligGlyph = U16(lig);
                        int components = U16(lig + 2);
                        if (components < 1) continue;
                        var ids = new uint[components];
                        ids[0] = firsts[i];
                        for (int c = 1; c < components; c++) ids[c] = U16(lig + 4 + 2 * (c - 1));
                        if (accept(ids)) result.Add((ids, ligGlyph));
                    }
                }
            }
            return result;
        }

        // ----- kern (legacy) -----

        /// Horizontal kerning pairs of the legacy 'kern' table (format 0 subtables of the
        /// Microsoft version-0 header), in design units; later subtables add to earlier ones.
        public Dictionary<(uint Left, uint Right), int> KernPairs()
        {
            var pairs = new Dictionary<(uint, uint), int>();
            if (!TryGet("kern", out int k) || U16(k) != 0) return pairs;
            int count = U16(k + 2);
            int o = k + 4;
            for (int i = 0; i < count; i++)
            {
                int length = U16(o + 2);
                int coverage = U16(o + 4);
                bool horizontal = (coverage & 1) != 0, minimum = (coverage & 2) != 0, crossStream = (coverage & 4) != 0;
                if ((coverage >> 8) == 0 && horizontal && !minimum && !crossStream)
                {
                    int n = U16(o + 6);
                    for (int p = 0; p < n; p++)
                    {
                        int r = o + 14 + 6 * p;
                        var key = ((uint)U16(r), (uint)U16(r + 2));
                        pairs[key] = pairs.GetValueOrDefault(key) + S16(r + 4);
                    }
                }
                if (length == 0) break;
                o += length;
            }
            return pairs;
        }
    }
}
