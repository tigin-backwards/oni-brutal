using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace OniMoono.Host
{
    /// Mono lets reflection write `static readonly` fields at any time and reads them from memory
    /// afterwards; the game depends on it (Localization.OverloadStrings replaces the LocStrings in
    /// `STRINGS.UI.PERMIT_RARITY` with translations; LocString.CreateLocStringKeys writes every
    /// LocString field back). CoreCLR refuses such writes once the type is initialized and its JIT
    /// may embed the values of initialized static readonly fields in compiled code.
    ///
    /// Restoring Mono's behaviour means the runtime must not treat those fields as initonly, so
    /// the host clears `InitOnly` on the static fields of game assemblies as they are loaded. Only
    /// the 2-byte Flags column of Field table rows changes; IL, signatures and everything else in
    /// the image are byte-identical. C# compilers enforce readonly at compile time, so the game's
    /// IL never stores to those fields outside type initializers.
    internal static class MonoFieldSemantics
    {
        const ushort Static = (ushort)FieldAttributes.Static;
        const ushort InitOnly = (ushort)FieldAttributes.InitOnly;
        const ushort Literal = (ushort)FieldAttributes.Literal;

        /// The image with static fields made writable, or null when it has none to change.
        public static byte[]? Adjust(string path) => Adjust(File.ReadAllBytes(path), path);

        /// The same for an image in memory (changed in place), named by `path` in errors.
        public static byte[]? Adjust(byte[] image, string path)
        {
            int changed = 0;
            using (var pe = new PEReader(new MemoryStream(image, writable: false)))
            {
                var md = pe.GetMetadataReader();
                int tableOffset = md.GetTableMetadataOffset(TableIndex.Field);
                int rowSize = md.GetTableRowSize(TableIndex.Field);
                int rows = md.GetTableRowCount(TableIndex.Field);
                if (!pe.PEHeaders.TryGetDirectoryOffset(pe.PEHeaders.CorHeader!.MetadataDirectory, out int metadataStart))
                    throw new BadImageFormatException($"{path}: metadata is not mapped in the file.");
                for (int row = 0; row < rows; row++)
                {
                    int at = metadataStart + tableOffset + row * rowSize;
                    ushort flags = (ushort)(image[at] | image[at + 1] << 8);
                    if ((flags & Static) == 0 || (flags & InitOnly) == 0 || (flags & Literal) != 0) continue;
                    // Cross-check against the reader so a layout mistake can never go unnoticed.
                    var expected = md.GetFieldDefinition(MetadataTokens.FieldDefinitionHandle(row + 1)).Attributes;
                    if ((ushort)expected != flags) throw new BadImageFormatException($"{path}: field row {row + 1} flags mismatch.");
                    flags &= unchecked((ushort)~InitOnly);
                    image[at] = (byte)flags;
                    image[at + 1] = (byte)(flags >> 8);
                    changed++;
                }
            }
            return changed == 0 ? null : image;
        }
    }
}
