// Lists every type and member that a set of consumer assemblies references in a set of
// provider assemblies (by assembly-name prefix). Output is JSON keyed by provider
// assembly, then type, then member signature, with the consumers that reference it.
//
// usage: Inventory <managedDir> <out.json> <providerPrefix,...> <consumer.dll> [consumer.dll...]
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

var managedDir = args[0];
var outPath = args[1];
var providerPrefixes = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries);
var consumers = args.Skip(3).ToArray();

bool IsProvider(string asm) => providerPrefixes.Any(p => asm == p || asm.StartsWith(p, StringComparison.Ordinal));

// provider asm -> type -> member -> consumers
var result = new SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, SortedSet<string>>>>(StringComparer.Ordinal);
var derived = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

void Add(string asm, string type, string member, string consumer)
{
    if (!result.TryGetValue(asm, out var types)) result[asm] = types = new(StringComparer.Ordinal);
    if (!types.TryGetValue(type, out var members)) types[type] = members = new(StringComparer.Ordinal);
    if (!members.TryGetValue(member, out var who)) members[member] = who = new(StringComparer.Ordinal);
    who.Add(consumer);
}

foreach (var path in consumers)
{
    using var fs = File.OpenRead(Path.Combine(managedDir, path));
    using var pe = new PEReader(fs);
    var md = pe.GetMetadataReader();
    var consumer = md.GetString(md.GetAssemblyDefinition().Name);
    var prov = new NameProvider(md);

    // Every type reference, so types used only in signatures/attributes still show up.
    foreach (var h in md.TypeReferences)
    {
        string? asm; string name;
        try { (asm, name) = prov.Resolve(h); }
        catch (BadImageFormatException e)
        {
            var tr = md.GetTypeReference(h);
            Console.Error.WriteLine($"{consumer}: typeref {MetadataTokens.GetToken(h):X8} scope {tr.ResolutionScope.Kind}: {e.Message}");
            continue;
        }
        if (asm != null && IsProvider(asm)) Add(asm, name, "<type>", consumer);
    }

    foreach (var h in md.MemberReferences)
    {
        var mr = md.GetMemberReference(h);
        string? asm; string type;
        switch (mr.Parent.Kind)
        {
            case HandleKind.TypeReference:
                (asm, type) = prov.Resolve((TypeReferenceHandle)mr.Parent);
                break;
            case HandleKind.TypeSpecification:
                (asm, type) = prov.ResolveSpecOwner((TypeSpecificationHandle)mr.Parent);
                break;
            default: continue;
        }
        if (asm == null || !IsProvider(asm)) continue;
        var name = md.GetString(mr.Name);
        string sig;
        var blob = md.GetBlobReader(mr.Signature);
        var header = blob.ReadSignatureHeader();
        blob.Reset();
        var dec = new SignatureDecoder<string, object?>(prov, md, null);
        if (header.Kind == SignatureKind.Field)
            sig = $"field {dec.DecodeFieldSignature(ref blob)} {name}";
        else
        {
            var ms = dec.DecodeMethodSignature(ref blob);
            var gen = ms.GenericParameterCount > 0 ? $"<{ms.GenericParameterCount}>" : "";
            sig = $"{(ms.Header.IsInstance ? "" : "static ")}{ms.ReturnType} {name}{gen}({string.Join(", ", ms.ParameterTypes)})";
        }
        Add(asm, type, sig, consumer);
    }

    foreach (var h in md.TypeDefinitions)
    {
        var td = md.GetTypeDefinition(h);
        if (td.BaseType.IsNil || td.BaseType.Kind != HandleKind.TypeReference) continue;
        var (asm, name) = prov.Resolve((TypeReferenceHandle)td.BaseType);
        if (asm == null || !IsProvider(asm)) continue;
        if (!derived.TryGetValue(name, out var set)) derived[name] = set = new(StringComparer.Ordinal);
        set.Add($"{consumer}:{prov.FullName(h)}");
    }
}

var json = new
{
    providers = result.ToDictionary(p => p.Key, p => p.Value.ToDictionary(t => t.Key, t => t.Value.ToDictionary(m => m.Key, m => m.Value.ToArray()))),
    derivedCounts = derived.ToDictionary(d => d.Key, d => d.Value.Count),
};
File.WriteAllText(outPath, JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
foreach (var (asm, types) in result)
    Console.WriteLine($"{asm}: {types.Count} types, {types.Sum(t => t.Value.Count(m => m.Key != "<type>"))} members");

sealed class NameProvider(MetadataReader md) : ISignatureTypeProvider<string, object?>
{
    public (string? asm, string name) Resolve(TypeReferenceHandle h)
    {
        var tr = md.GetTypeReference(h);
        var name = md.GetString(tr.Name);
        var ns = md.GetString(tr.Namespace);
        switch (tr.ResolutionScope.Kind)
        {
            case HandleKind.TypeReference:
                var (asm, outer) = Resolve((TypeReferenceHandle)tr.ResolutionScope);
                return (asm, outer + "+" + name);
            case HandleKind.AssemblyReference:
                var ar = md.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope);
                return (md.GetString(ar.Name), ns.Length > 0 ? ns + "." + name : name);
            default:
                return (null, ns.Length > 0 ? ns + "." + name : name);
        }
    }

    public (string? asm, string name) ResolveSpecOwner(TypeSpecificationHandle h)
    {
        var blob = md.GetBlobReader(md.GetTypeSpecification(h).Signature);
        var code = blob.ReadSignatureTypeCode();
        if (code != SignatureTypeCode.GenericTypeInstance) return (null, "");
        blob.ReadSignatureTypeCode();
        var handle = blob.ReadTypeHandle();
        return handle.Kind == HandleKind.TypeReference ? Resolve((TypeReferenceHandle)handle) : (null, "");
    }

    public string FullName(TypeDefinitionHandle h)
    {
        var td = md.GetTypeDefinition(h);
        var name = md.GetString(td.Name);
        var decl = td.GetDeclaringType();
        if (!decl.IsNil) return FullName(decl) + "+" + name;
        var ns = md.GetString(td.Namespace);
        return ns.Length > 0 ? ns + "." + name : name;
    }

    public string GetArrayType(string e, ArrayShape s) => $"{e}[{new string(',', s.Rank - 1)}]";
    public string GetByReferenceType(string e) => e + "&";
    public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) => $"{g}<{string.Join(",", a)}>";
    public string GetGenericMethodParameter(object? c, int i) => "!!" + i;
    public string GetGenericTypeParameter(object? c, int i) => "!" + i;
    public string GetModifiedType(string m, string u, bool r) => u;
    public string GetPinnedType(string e) => e;
    public string GetPointerType(string e) => e + "*";
    public string GetPrimitiveType(PrimitiveTypeCode t) => t.ToString();
    public string GetSZArrayType(string e) => e + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => FullName(h);
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => Resolve(h).name;
    public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) => "spec";
}
