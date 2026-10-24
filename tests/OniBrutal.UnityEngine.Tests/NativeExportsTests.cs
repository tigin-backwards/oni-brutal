using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OniBrutal.Engine.Platform;
using Xunit;

namespace OniBrutal.Tests
{
    /// Exported symbols read from library files (the host's --check-natives), on hand-built
    /// Mach-O and ELF files.
    public class NativeExportsTests
    {
        sealed class Bytes
        {
            public readonly List<byte> Data = new();
            public int Length => Data.Count;
            public Bytes U32(uint v) { Data.AddRange(BitConverter.GetBytes(v)); return this; }
            public Bytes I32(int v) => U32((uint)v);
            public Bytes U16(ushort v) { Data.AddRange(BitConverter.GetBytes(v)); return this; }
            public Bytes U64(ulong v) { Data.AddRange(BitConverter.GetBytes(v)); return this; }
            public Bytes U8(byte v) { Data.Add(v); return this; }
            public Bytes Str(string s) { Data.AddRange(Encoding.ASCII.GetBytes(s)); Data.Add(0); return this; }
            public Bytes BigEndian(uint v) { Data.AddRange(BitConverter.GetBytes(v).Reverse()); return this; }
            public Bytes Raw(byte[] b) { Data.AddRange(b); return this; }
            public Bytes PadTo(int n) { while (Data.Count < n) Data.Add(0); return this; }
            public byte[] ToArray() => Data.ToArray();
        }

        /// A thin x86_64 dylib: a symbol table with two defined externals, an undefined one and a
        /// local, and an export trie naming one of them and a name the table lacks.
        static byte[] MachO(int cpu = 0x01000007)
        {
            var strings = new Bytes().U8(0).Str("_SimInit").Str("_SimStep").Str("_dlopen").Str("_local");
            int[] strx = [1, 10, 19, 27];
            // Trie: root -> "_Sim" -> {"Init" (terminal), "Free" (terminal)}.
            var trie = new Bytes();
            trie.U8(0).U8(1).Str("_Sim").U8(8);              // root at 0: not terminal, one child at 8
            trie.PadTo(8).U8(0).U8(2).Str("Init").U8(24).Str("Free").U8(28);
            trie.PadTo(24).U8(2).U8(0).U8(0x10).U8(0);      // terminal: flags 0, address 0x10; no children
            trie.PadTo(28).U8(2).U8(0).U8(0x20).U8(0);
            const int header = 32, symtabCmd = 24, trieCmd = 16;
            int symoff = header + symtabCmd + trieCmd, nsyms = 4;
            int stroff = symoff + 16 * nsyms, trieoff = stroff + strings.Length;
            var m = new Bytes().U32(0xFEEDFACF).I32(cpu).I32(3).U32(6).U32(2).U32(symtabCmd + trieCmd).U32(0).U32(0);
            m.U32(0x2).U32(symtabCmd).I32(symoff).I32(nsyms).I32(stroff).I32(strings.Length);
            m.U32(0x80000033).U32(trieCmd).I32(trieoff).I32(trie.Length);
            byte[] types = [0x0F, 0x0F, 0x01, 0x0E]; // ext+sect, ext+sect, ext undefined, local
            for (int i = 0; i < nsyms; i++) m.I32(strx[i]).U8(types[i]).U8(1).U16(0).U64(0x100);
            return m.Raw(strings.ToArray()).Raw(trie.ToArray()).ToArray();
        }

        [Fact]
        public void MachOExportsAreTheSymbolTablesAndTheTriesNames()
        {
            var file = MachO();
            Assert.Equal("Mach-O x86_64", NativeExports.Format(file));
            Assert.Equal(["SimFree", "SimInit", "SimStep"], NativeExports.Read(file).Order(StringComparer.Ordinal));
        }

        [Fact]
        public void TheUniversalFilesX8664SliceIsRead()
        {
            var thin = MachO();
            var fat = new Bytes().BigEndian(0xCAFEBABE).BigEndian(2)
                .BigEndian(0x0100000C).BigEndian(0).BigEndian(4096).BigEndian(16).BigEndian(12)
                .BigEndian(0x01000007).BigEndian(3).BigEndian(8192).BigEndian((uint)thin.Length).BigEndian(12)
                .PadTo(4096).Raw(new byte[16]).PadTo(8192).Raw(thin).ToArray();
            Assert.Equal("Mach-O universal", NativeExports.Format(fat));
            Assert.Equal([0x0100000C, 0x01000007], NativeExports.Architectures(fat));
            Assert.Equal(["SimFree", "SimInit", "SimStep"], NativeExports.Read(fat).Order(StringComparer.Ordinal));
            var armOnly = new Bytes().BigEndian(0xCAFEBABE).BigEndian(1).BigEndian(0x0100000C).BigEndian(0).BigEndian(64).BigEndian(16).BigEndian(12).PadTo(80).ToArray();
            Assert.Throws<InvalidDataException>(() => NativeExports.Read(armOnly));
        }

        [Fact]
        public void TheArm64SliceIsReadWhenAskedFor()
        {
            var arm = MachO(NativeExports.Arm64);
            Assert.Equal("Mach-O arm64", NativeExports.Format(arm));
            Assert.Equal(["SimFree", "SimInit", "SimStep"], NativeExports.Read(arm, NativeExports.Arm64).Order(StringComparer.Ordinal));
            Assert.Throws<InvalidDataException>(() => NativeExports.Read(arm));
            // A universal file whose arm64 slice exports what its x86_64 slice does not.
            var x64 = new Bytes().U32(0xFEEDFACF).I32(NativeExports.X8664).I32(3).U32(6).U32(0).U32(0).U32(0).U32(0).ToArray();
            var fat = new Bytes().BigEndian(0xCAFEBABE).BigEndian(2)
                .BigEndian(0x01000007).BigEndian(3).BigEndian(4096).BigEndian((uint)x64.Length).BigEndian(12)
                .BigEndian(0x0100000C).BigEndian(0).BigEndian(8192).BigEndian((uint)arm.Length).BigEndian(14)
                .PadTo(4096).Raw(x64).PadTo(8192).Raw(arm).ToArray();
            Assert.Empty(NativeExports.Read(fat));
            Assert.Equal(["SimFree", "SimInit", "SimStep"], NativeExports.Read(fat, NativeExports.Arm64).Order(StringComparer.Ordinal));
            Assert.Equal("arm64", NativeExports.ArchName(NativeExports.Arm64));
            Assert.Equal(NativeExports.X8664, NativeExports.Cpu("x86_64"));
            Assert.Null(NativeExports.Cpu("ppc"));
        }

        [Fact]
        public void MachODependenciesAreItsDylibLoadCommands()
        {
            // Two loads (plain and weak) and an LC_ID_DYLIB (0xD, the library's own name), which is not one.
            static void Dylib(Bytes b, uint cmd, string name)
            {
                int size = (24 + name.Length + 1 + 7) & ~7, start = b.Length;
                b.U32(cmd).I32(size).U32(24).U32(0).U32(0x10000).U32(0x10000).Str(name).PadTo(start + size);
            }
            var cmds = new Bytes();
            Dylib(cmds, 0xD, "@rpath/libfreetype.6.dylib");
            Dylib(cmds, 0xC, "@rpath/libpng18.18.dylib");
            Dylib(cmds, 0x80000018, "/usr/lib/libSystem.B.dylib");
            var m = new Bytes().U32(0xFEEDFACF).I32(NativeExports.Arm64).I32(0).U32(6).U32(3).U32((uint)cmds.Length).U32(0).U32(0).Raw(cmds.ToArray()).ToArray();
            Assert.Equal(["@rpath/libpng18.18.dylib", "/usr/lib/libSystem.B.dylib"], NativeExports.Dependencies(m, NativeExports.Arm64));
            Assert.Equal([NativeExports.Arm64], NativeExports.Architectures(m));
            Assert.Empty(NativeExports.Dependencies([0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0]));
        }

        [Fact]
        public void ElfExportsAreDynsymsDefinedGlobalsAndWeaks()
        {
            var strings = new Bytes().U8(0).Str("SimStep").Str("undefined").Str("local").Str("weak");
            int[] name = [0, 1, 9, 19, 25];
            byte[] info = [0, 0x12, 0x12, 0x02, 0x22];
            ushort[] section = [0, 5, 0, 5, 5];
            var symbols = new Bytes();
            for (int i = 0; i < 5; i++) symbols.I32(name[i]).U8(info[i]).U8(0).U16(section[i]).U64(0x1000).U64(8);
            int symoff = 64, stroff = symoff + symbols.Length, shoff = (stroff + strings.Length + 7) & ~7;
            var e = new Bytes().Raw([0x7F, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0]).PadTo(16)
                .U16(3).U16(0x3E).U32(1).U64(0).U64(0).U64((ulong)shoff).U32(0).U16(64).U16(0).U16(0).U16(64).U16(3).U16(2);
            e.PadTo(64).Raw(symbols.ToArray()).Raw(strings.ToArray()).PadTo(shoff);
            static void Section(Bytes b, uint type, int offset, int size, int link, int entsize) =>
                b.U32(0).U32(type).U64(0).U64(0).U64((ulong)offset).U64((ulong)size).U32((uint)link).U32(0).U64(8).U64((ulong)entsize);
            Section(e, 0, 0, 0, 0, 0);
            Section(e, 11, symoff, symbols.Length, 2, 24);
            Section(e, 3, stroff, strings.Length, 0, 0);
            var file = e.ToArray();
            Assert.Equal("ELF64", NativeExports.Format(file));
            Assert.Equal([NativeExports.X8664], NativeExports.Architectures(file)); // e_machine 0x3E
            Assert.Equal(["SimStep", "weak"], NativeExports.Read(file).Order(StringComparer.Ordinal));
            Assert.Null(NativeExports.Format([1, 2, 3, 4, 5, 6, 7, 8]));
        }
    }
}
