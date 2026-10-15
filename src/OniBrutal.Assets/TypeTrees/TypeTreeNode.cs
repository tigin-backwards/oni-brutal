using System.Collections.Generic;

namespace OniBrutal.Assets.TypeTrees
{
    /// One node of a Unity type tree: a field (Name) of a type (TypeName) with its children.
    public sealed class TypeTreeNode
    {
        public string TypeName = "";
        public string Name = "";
        public int ByteSize;
        public int Version;
        public int TypeFlags;
        public uint MetaFlag;
        public List<TypeTreeNode> Children = new();

        /// kAlignBytes: align the stream to 4 after this node's data.
        public const uint AlignBytesFlag = 0x4000;
        public bool AlignAfter => (MetaFlag & AlignBytesFlag) != 0;

        /// TypeFlags bit 0: the node is an array (its children are "size" and "data").
        public bool IsArray => (TypeFlags & 1) != 0;

        public TypeTreeNode Child(string name)
        {
            foreach (var c in Children) if (c.Name == name) return c;
            return null!;
        }

        public override string ToString() => $"{TypeName} {Name} ({ByteSize}) flags={MetaFlag:X}";
    }
}
