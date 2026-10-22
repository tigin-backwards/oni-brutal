// The game's own types (Assembly-CSharp and -firstpass), read the same way, so that a mod type's
// base chain can be followed past the mod (MyScreen → KModalScreen → KScreen) and a patch target's
// declaring type can be checked for being a screen. Also the game build, for picking which of a
// mod's archived_versions the game would load.
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ModTriage;

public sealed class GameIndex : IDisposable
{
    readonly List<PEReader> readers = [];
    readonly Dictionary<string, (MetadataReader md, TypeDefinitionHandle h)> types = new(StringComparer.Ordinal);

    public int? Build { get; private set; }
    public bool IsEmpty => types.Count == 0;

    public static GameIndex Empty() => new();

    public static GameIndex Load(string managedDir)
    {
        var index = new GameIndex();
        foreach (var name in new[] { "Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll" })
        {
            var path = Path.Combine(managedDir, name);
            if (!File.Exists(path)) continue;
            var pe = new PEReader(File.OpenRead(path));
            index.readers.Add(pe);
            var md = pe.GetMetadataReader();
            foreach (var h in md.TypeDefinitions) index.types.TryAdd(Names.FullName(md, h), (md, h));
        }
        index.Build = index.ReadBuild();
        return index;
    }

    /// KleiVersion.ChangeList, a constant.
    int? ReadBuild()
    {
        if (!types.TryGetValue("KleiVersion", out var t)) return null;
        var td = t.md.GetTypeDefinition(t.h);
        foreach (var fh in td.GetFields())
        {
            var f = t.md.GetFieldDefinition(fh);
            if (t.md.GetString(f.Name) != "ChangeList") continue;
            var c = f.GetDefaultValue();
            if (c.IsNil) return null;
            var blob = t.md.GetBlobReader(t.md.GetConstant(c).Value);
            return t.md.GetConstant(c).TypeCode switch
            {
                ConstantTypeCode.UInt32 => (int)blob.ReadUInt32(),
                ConstantTypeCode.Int32 => blob.ReadInt32(),
                _ => null,
            };
        }
        return null;
    }

    public bool Contains(string fullName) => types.ContainsKey(fullName);

    /// The base types of a game type, nearest first, and the interfaces along the way.
    public (List<string> bases, List<string> interfaces) Chain(string fullName)
    {
        var bases = new List<string>();
        var interfaces = new List<string>();
        var name = fullName;
        for (int depth = 0; depth < 32 && types.TryGetValue(name, out var t); depth++)
        {
            var td = t.md.GetTypeDefinition(t.h);
            foreach (var ih in td.GetInterfaceImplementations())
                if (Names.Of(t.md, t.md.GetInterfaceImplementation(ih).Interface) is { } i) interfaces.Add(i.FullName);
            if (td.BaseType.IsNil || Names.Of(t.md, td.BaseType) is not { } b) break;
            bases.Add(b.FullName);
            name = b.FullName;
        }
        return (bases, interfaces);
    }

    public void Dispose()
    {
        foreach (var r in readers) r.Dispose();
    }
}
