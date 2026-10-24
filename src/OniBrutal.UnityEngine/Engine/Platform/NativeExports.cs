using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OniBrutal.Engine.Platform
{
    /// The symbols a native library exports, read from its file without loading it: Mach-O
    /// (thin or universal; the x86_64 slice) and ELF64. Names are as dlsym takes them (Mach-O's
    /// leading underscore removed). For checking a platform's P/Invokes against its libraries on
    /// another platform (the host's --check-natives).
    internal static class NativeExports
    {
        const uint MhMagic64 = 0xFEEDFACF, FatMagic = 0xCAFEBABE;
        const int CpuTypeX8664 = 0x01000007;
        const uint LcSymtab = 0x2, LcDyldInfo = 0x22, LcDyldInfoOnly = 0x80000022, LcDyldExportsTrie = 0x80000033;

        /// The file's format ("Mach-O x86_64", "ELF64"), or null when it is neither.
        public static string Format(ReadOnlySpan<byte> file)
        {
            if (file.Length >= 4 && BinaryPrimitives.ReadUInt32BigEndian(file) == FatMagic) return "Mach-O universal";
            if (file.Length >= 8 && BinaryPrimitives.ReadUInt32LittleEndian(file) == MhMagic64) return BinaryPrimitives.ReadInt32LittleEndian(file[4..]) == CpuTypeX8664 ? "Mach-O x86_64" : "Mach-O";
            if (file.Length >= 5 && file[0] == 0x7F && file[1] == (byte)'E' && file[2] == (byte)'L' && file[3] == (byte)'F' && file[4] == 2) return "ELF64";
            return null;
        }

        /// The architectures of a universal Mach-O (CPU types), or of a thin one.
        public static List<int> Architectures(ReadOnlySpan<byte> file)
        {
            var result = new List<int>();
            if (BinaryPrimitives.ReadUInt32BigEndian(file) == FatMagic)
            {
                int n = BinaryPrimitives.ReadInt32BigEndian(file[4..]);
                for (int i = 0; i < n; i++) result.Add(BinaryPrimitives.ReadInt32BigEndian(file[(8 + 20 * i)..]));
            }
            else if (BinaryPrimitives.ReadUInt32LittleEndian(file) == MhMagic64) result.Add(BinaryPrimitives.ReadInt32LittleEndian(file[4..]));
            return result;
        }

        public static HashSet<string> Read(string path) => Read(File.ReadAllBytes(path));

        public static HashSet<string> Read(byte[] file)
        {
            var span = file.AsSpan();
            if (span.Length >= 4 && BinaryPrimitives.ReadUInt32BigEndian(span) == FatMagic) return MachO(X8664Slice(span));
            if (span.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(span) == MhMagic64) return MachO(span);
            if (Format(span) == "ELF64") return Elf(span);
            throw new InvalidDataException("Not a Mach-O 64-bit or ELF64 file.");
        }

        static ReadOnlySpan<byte> X8664Slice(ReadOnlySpan<byte> file)
        {
            int n = BinaryPrimitives.ReadInt32BigEndian(file[4..]);
            for (int i = 0; i < n; i++)
            {
                var arch = file[(8 + 20 * i)..];
                if (BinaryPrimitives.ReadInt32BigEndian(arch) != CpuTypeX8664) continue;
                int offset = BinaryPrimitives.ReadInt32BigEndian(arch[8..]), size = BinaryPrimitives.ReadInt32BigEndian(arch[12..]);
                return file.Slice(offset, size);
            }
            throw new InvalidDataException("Universal Mach-O without an x86_64 slice.");
        }

        /// Defined external symbols of the symbol table, and the export trie's names, together
        /// (a library's exports are the trie's; the table lists the same for the game's plugins).
        static HashSet<string> MachO(ReadOnlySpan<byte> m)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(m[4..]) != CpuTypeX8664) throw new InvalidDataException("The Mach-O file is not x86_64.");
            int commands = BinaryPrimitives.ReadInt32LittleEndian(m[16..]);
            int at = 32;
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int c = 0; c < commands; c++)
            {
                uint cmd = BinaryPrimitives.ReadUInt32LittleEndian(m[at..]);
                int size = BinaryPrimitives.ReadInt32LittleEndian(m[(at + 4)..]);
                var lc = m[at..];
                if (cmd == LcSymtab)
                {
                    int symoff = BinaryPrimitives.ReadInt32LittleEndian(lc[8..]), nsyms = BinaryPrimitives.ReadInt32LittleEndian(lc[12..]);
                    int stroff = BinaryPrimitives.ReadInt32LittleEndian(lc[16..]);
                    for (int s = 0; s < nsyms; s++)
                    {
                        var sym = m[(symoff + 16 * s)..];
                        byte type = sym[4];
                        // N_EXT, N_SECT, not a debugging entry (N_STAB) or a private extern (N_PEXT).
                        if ((type & 0xF0) != 0 || (type & 0x01) == 0 || (type & 0x0E) != 0x0E) continue;
                        names.Add(Unmangle(CString(m, stroff + BinaryPrimitives.ReadInt32LittleEndian(sym))));
                    }
                }
                else if (cmd is LcDyldInfo or LcDyldInfoOnly or LcDyldExportsTrie)
                {
                    int off = BinaryPrimitives.ReadInt32LittleEndian(lc[(cmd == LcDyldExportsTrie ? 8 : 40)..]);
                    int len = BinaryPrimitives.ReadInt32LittleEndian(lc[(cmd == LcDyldExportsTrie ? 12 : 44)..]);
                    if (len > 0) Trie(m.Slice(off, len), 0, "", names, 0);
                }
                at += size;
            }
            return names;
        }

        /// dyld's export trie: a node's terminal information (its size, then flags and address),
        /// then its children (edge label, NUL-terminated, and the child's offset).
        static void Trie(ReadOnlySpan<byte> trie, int node, string prefix, HashSet<string> names, int depth)
        {
            if (depth > 512) throw new InvalidDataException("Export trie too deep.");
            int at = node;
            ulong terminal = Uleb(trie, ref at);
            if (terminal > 0) names.Add(Unmangle(prefix));
            at += (int)terminal;
            int children = trie[at++];
            for (int i = 0; i < children; i++)
            {
                int end = trie[at..].IndexOf((byte)0);
                string edge = Encoding.UTF8.GetString(trie.Slice(at, end));
                at += end + 1;
                int child = (int)Uleb(trie, ref at);
                Trie(trie, child, prefix + edge, names, depth + 1);
            }
        }

        static ulong Uleb(ReadOnlySpan<byte> data, ref int at)
        {
            ulong v = 0;
            int shift = 0;
            byte b;
            do
            {
                b = data[at++];
                v |= (ulong)(b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return v;
        }

        static string Unmangle(string name) => name.StartsWith('_') ? name[1..] : name;

        static string CString(ReadOnlySpan<byte> data, int at)
        {
            int end = data[at..].IndexOf((byte)0);
            return Encoding.UTF8.GetString(data.Slice(at, end < 0 ? data.Length - at : end));
        }

        /// ELF64: the defined global and weak symbols of .dynsym.
        static HashSet<string> Elf(ReadOnlySpan<byte> e)
        {
            long shoff = BinaryPrimitives.ReadInt64LittleEndian(e[0x28..]);
            int shentsize = BinaryPrimitives.ReadUInt16LittleEndian(e[0x3A..]), shnum = BinaryPrimitives.ReadUInt16LittleEndian(e[0x3C..]);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < shnum; i++)
            {
                var sh = e[(int)(shoff + i * shentsize)..];
                if (BinaryPrimitives.ReadUInt32LittleEndian(sh[4..]) != 11) continue; // SHT_DYNSYM
                long offset = BinaryPrimitives.ReadInt64LittleEndian(sh[0x18..]), size = BinaryPrimitives.ReadInt64LittleEndian(sh[0x20..]);
                int link = BinaryPrimitives.ReadInt32LittleEndian(sh[0x28..]);
                long entsize = BinaryPrimitives.ReadInt64LittleEndian(sh[0x38..]);
                var strings = e[(int)(shoff + link * shentsize)..];
                long stroff = BinaryPrimitives.ReadInt64LittleEndian(strings[0x18..]);
                for (long s = 0; s < size / entsize; s++)
                {
                    var sym = e[(int)(offset + s * entsize)..];
                    int bind = sym[4] >> 4;
                    ushort shndx = BinaryPrimitives.ReadUInt16LittleEndian(sym[6..]);
                    if (shndx == 0 || (bind != 1 && bind != 2)) continue;
                    names.Add(CString(e, (int)(stroff + BinaryPrimitives.ReadUInt32LittleEndian(sym))));
                }
            }
            return names;
        }
    }
}
