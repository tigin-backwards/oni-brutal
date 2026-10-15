// Builds the UnityEngine.*Module facade assemblies. Each facade carries only type forwarders,
// so game IL compiled against e.g. [UnityEngine.CoreModule]UnityEngine.GameObject binds to the
// implementation in OniBrutal.UnityEngine (ARCHITECTURE §2).
//
//   FacadeGen map <unityManagedDir> <out.tsv>
//       Records which Unity module defines each public type (module<TAB>full type name).
//   FacadeGen emit <map.tsv> <outDir> <implementation.dll> [implementation.dll...]
//       Writes one forwarding facade per module that has at least one implemented type, plus
//       the all-forwarding UnityEngine.dll. Fails if an implementation assembly declares a public
//       type that no Unity module defines: our public surface is Unity's, nothing more.
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

return args.Length > 0 && args[0] == "map" && args.Length == 3 ? MakeMap(args[1], args[2])
     : args.Length > 3 && args[0] == "emit" ? Emit(args[1], args[2], args[3..])
     : Usage();

static int Usage()
{
    Console.Error.WriteLine("usage: FacadeGen map <unityManagedDir> <out.tsv> | emit <map.tsv> <outDir> <impl.dll>...");
    return 2;
}

static int MakeMap(string managedDir, string outPath)
{
    var lines = new SortedSet<string>(StringComparer.Ordinal);
    foreach (var file in Directory.EnumerateFiles(managedDir, "UnityEngine.*Module.dll"))
    {
        using var pe = new PEReader(File.OpenRead(file));
        var md = pe.GetMetadataReader();
        var module = md.GetString(md.GetAssemblyDefinition().Name);
        foreach (var h in md.TypeDefinitions)
        {
            var td = md.GetTypeDefinition(h);
            if (!td.GetDeclaringType().IsNil) continue;
            if ((td.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public) continue;
            lines.Add($"{module}\t{Names.Full(md, h)}");
        }
    }
    File.WriteAllLines(outPath, lines);
    Console.WriteLine($"{lines.Count} public types");
    return 0;
}

static int Emit(string mapPath, string outDir, string[] implementations)
{
    var moduleOf = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var line in File.ReadLines(mapPath))
    {
        var tab = line.IndexOf('\t');
        moduleOf[line[(tab + 1)..]] = line[..tab];
    }

    // module -> (implementing assembly, type) list
    var forwards = new SortedDictionary<string, List<Forward>>(StringComparer.Ordinal);
    var errors = new List<string>();
    foreach (var implPath in implementations)
    {
        using var pe = new PEReader(File.OpenRead(implPath));
        var md = pe.GetMetadataReader();
        var asm = md.GetAssemblyDefinition();
        var impl = new AssemblyRef(md.GetString(asm.Name), asm.Version);
        foreach (var h in md.TypeDefinitions)
        {
            var td = md.GetTypeDefinition(h);
            if (!td.GetDeclaringType().IsNil) continue;
            if ((td.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public) continue;
            var name = Names.Full(md, h);
            if (!moduleOf.TryGetValue(name, out var module))
            {
                errors.Add($"{impl.Name}: public type {name} is not part of Unity's API");
                continue;
            }
            if (!forwards.TryGetValue(module, out var list)) forwards[module] = list = new();
            list.Add(new Forward(impl, md.GetString(td.Namespace), md.GetString(td.Name), Names.PublicNested(md, h)));
        }
    }
    if (errors.Count > 0)
    {
        foreach (var e in errors) Console.Error.WriteLine("error: " + e);
        return 1;
    }

    Directory.CreateDirectory(outDir);
    foreach (var (module, list) in forwards)
        Facade.Write(Path.Combine(outDir, module + ".dll"), module, list);
    Facade.Write(Path.Combine(outDir, "UnityEngine.dll"), "UnityEngine", forwards.Values.SelectMany(l => l).ToList());
    Console.WriteLine($"{forwards.Count} module facades, {forwards.Values.Sum(l => l.Count)} forwarded types");
    return 0;
}

sealed record AssemblyRef(string Name, Version Version);

/// A forwarded top-level type and the names of its public nested types (as paths "Outer/Inner").
sealed record Forward(AssemblyRef Target, string Namespace, string Name, ImmutableArray<string> Nested);

static class Names
{
    public static string Full(MetadataReader md, TypeDefinitionHandle h)
    {
        var td = md.GetTypeDefinition(h);
        var ns = md.GetString(td.Namespace);
        var name = md.GetString(td.Name);
        return ns.Length > 0 ? ns + "." + name : name;
    }

    /// Public nested types, recursively, as '/'-separated paths relative to the top-level type.
    public static ImmutableArray<string> PublicNested(MetadataReader md, TypeDefinitionHandle h)
    {
        var result = ImmutableArray.CreateBuilder<string>();
        void Walk(TypeDefinitionHandle parent, string prefix)
        {
            foreach (var nh in md.GetTypeDefinition(parent).GetNestedTypes())
            {
                var nt = md.GetTypeDefinition(nh);
                var vis = nt.Attributes & TypeAttributes.VisibilityMask;
                if (vis != TypeAttributes.NestedPublic) continue;
                var path = prefix + md.GetString(nt.Name);
                result.Add(path);
                Walk(nh, path + "/");
            }
        }
        Walk(h, "");
        return result.ToImmutable();
    }
}

static class Facade
{
    // ECMA-335 II.23.1.15 tdForwarder; not named in System.Reflection.TypeAttributes.
    const TypeAttributes ForwarderFlag = (TypeAttributes)0x00200000;

    public static void Write(string path, string assemblyName, IReadOnlyList<Forward> types)
    {
        var mb = new MetadataBuilder();
        // Deterministic MVID: derived from the facade's name and contents.
        var identity = assemblyName + "|" + string.Join("|", types.Select(t => t.Target.Name + ":" + t.Namespace + "." + t.Name + "[" + string.Join(",", t.Nested) + "]").Order(StringComparer.Ordinal));
        var mvid = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16));
        mb.AddModule(0, mb.GetOrAddString(assemblyName + ".dll"), mb.GetOrAddGuid(mvid), default, default);
        mb.AddAssembly(mb.GetOrAddString(assemblyName), new Version(0, 0, 0, 0), default, default, default, AssemblyHashAlgorithm.Sha1);
        mb.AddTypeDefinition(default, default, mb.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));

        var refs = new Dictionary<string, AssemblyReferenceHandle>(StringComparer.Ordinal);
        foreach (var t in types.OrderBy(t => t.Namespace, StringComparer.Ordinal).ThenBy(t => t.Name, StringComparer.Ordinal))
        {
            if (!refs.TryGetValue(t.Target.Name, out var asmRef))
                refs[t.Target.Name] = asmRef = mb.AddAssemblyReference(mb.GetOrAddString(t.Target.Name), t.Target.Version, default, default, default, default);
            var top = mb.AddExportedType(ForwarderFlag, mb.GetOrAddString(t.Namespace), mb.GetOrAddString(t.Name), asmRef, 0);
            var byPath = new Dictionary<string, ExportedTypeHandle> { [""] = top };
            foreach (var nested in t.Nested)
            {
                var slash = nested.LastIndexOf('/');
                var parent = byPath[slash < 0 ? "" : nested[..slash]];
                byPath[nested] = mb.AddExportedType(TypeAttributes.NestedPublic, default, mb.GetOrAddString(nested[(slash + 1)..]), parent, 0);
            }
        }

        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(mb), new BlobBuilder(),
            deterministicIdProvider: content => BlobContentId.FromHash(System.Security.Cryptography.SHA256.HashData(
                content.SelectMany(b => b.GetBytes().ToArray()).ToArray())));
        var blob = new BlobBuilder();
        pe.Serialize(blob);
        using var fs = File.Create(path);
        blob.WriteContentTo(fs);
    }
}
