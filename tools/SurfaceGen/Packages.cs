using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

/// Packages mode: the shape of the Unity packages this project replaces (uGUI, TextMeshPro,
/// Timeline), from their metadata only (no method bodies are read).
///
/// usage: SurfaceGen packages <unityManagedDir> <outDir> <generatedRoot> <dataScripts.tsv>
///
/// Required types = what the game's assemblies reference in the packages, the script classes
/// the game's data uses (`--check-assets --list-data-scripts`), and the types the game derives
/// from, closed over declaring and base types, implemented public interfaces, generic
/// constraints, serialized field types and the types in required signatures. Writes:
///   <outDir>/package-types.tsv     assembly, kind, full name, base, interfaces
///   <outDir>/package-members.tsv   assembly, type, member, how the game uses it (call, override), consumers
///   <generatedRoot>/<Assembly>/Generated/<Namespace>.cs   enums, interfaces, delegates, and class and
///       struct headers with their serialized fields and abstract members
static class Packages
{
    static readonly HashSet<string> providers = new(StringComparer.Ordinal) { "UnityEngine.UI", "Unity.TextMeshPro", "Unity.Timeline" };

    static readonly string[] consumers =
    [
        "Assembly-CSharp", "Assembly-CSharp-firstpass", "FMODUnity", "FMODUnityResonance", "FMODUnityHaptics",
        "ImGui.NET", "ImGui", "com.rlabrecque.steamworks.net",
    ];

    public static int Run(string[] args)
    {
        var managed = args[0];
        var outDir = args[1];
        var genRoot = args[2];
        var dataScripts = args[3];

        var resolver = new PathAssemblyResolver(Directory.GetFiles(managed, "*.dll"));
        using var mlc = new MetadataLoadContext(resolver, "mscorlib");
        bool IsProvider(string asm) => providers.Contains(asm);

        var required = new Dictionary<string, Type>(StringComparer.Ordinal);
        var queue = new Queue<Type>();
        void Require(Type? t)
        {
            if (t == null) return;
            if (t.HasElementType) { Require(t.GetElementType()); return; }
            if (t.IsGenericParameter) return;
            if (t.IsConstructedGenericType)
            {
                Require(t.GetGenericTypeDefinition());
                foreach (var a in t.GetGenericArguments()) Require(a);
                return;
            }
            var asm = t.Assembly.GetName().Name!;
            if (!IsProvider(asm) || t.Name.Contains('<')) return;
            if (required.TryAdd(asm + "|" + t.FullName, t)) queue.Enqueue(t);
        }
        Type? Lookup(string asm, string fullName) => IsProvider(asm) ? mlc.LoadFromAssemblyName(asm).GetType(fullName, throwOnError: true) : null;

        // Members the game uses: (assembly, type, member) -> (use, consumers).
        var members = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        void Use(string asm, string type, string member, string use, string consumer)
        {
            var key = $"{asm}\t{type}\t{member}\t{use}";
            if (!members.TryGetValue(key, out var set)) members[key] = set = new(StringComparer.Ordinal);
            set.Add(consumer);
        }

        foreach (var consumer in consumers)
        {
            var path = Path.Combine(managed, consumer + ".dll");
            if (!File.Exists(path)) continue;
            using var pe = new PEReader(File.OpenRead(path));
            var md = pe.GetMetadataReader();
            var names = new RefNames(md);
            foreach (var h in md.TypeReferences)
            {
                var (asm, name) = names.Resolve(h);
                if (asm != null) Require(Lookup(asm, name));
            }
            var sigNames = new SigNames(names);
            foreach (var h in md.MemberReferences)
            {
                var mr = md.GetMemberReference(h);
                (string? asm, string name) owner = mr.Parent.Kind switch
                {
                    HandleKind.TypeReference => names.Resolve((TypeReferenceHandle)mr.Parent),
                    HandleKind.TypeSpecification => names.ResolveSpecOwner((TypeSpecificationHandle)mr.Parent),
                    _ => (null, ""),
                };
                if (owner.asm == null || !IsProvider(owner.asm)) continue;
                Require(Lookup(owner.asm, owner.name));
                var collector = new SigCollector(names);
                var dec = new SignatureDecoder<object?, object?>(collector, md, null);
                var blob = md.GetBlobReader(mr.Signature);
                var header = blob.ReadSignatureHeader();
                blob.Reset();
                string member;
                if (header.Kind == SignatureKind.Field)
                {
                    blob.ReadSignatureHeader();
                    dec.DecodeType(ref blob);
                    blob.Reset();
                    blob.ReadSignatureHeader();
                    member = $"field {new SignatureDecoder<string, object?>(sigNames, md, null).DecodeType(ref blob)} {md.GetString(mr.Name)}";
                }
                else
                {
                    dec.DecodeMethodSignature(ref blob);
                    blob.Reset();
                    var sig = new SignatureDecoder<string, object?>(sigNames, md, null).DecodeMethodSignature(ref blob);
                    member = $"{(header.IsInstance ? "" : "static ")}{sig.ReturnType} {md.GetString(mr.Name)}({string.Join(", ", sig.ParameterTypes)})";
                }
                foreach (var (asm, name) in collector.Found) Require(Lookup(asm, name));
                Use(owner.asm, owner.name, member, "call", consumer);
            }
        }

        // The game's classes that derive from package types: the members they override.
        foreach (var consumer in consumers)
        {
            var path = Path.Combine(managed, consumer + ".dll");
            if (!File.Exists(path)) continue;
            Type[] types;
            try { types = mlc.LoadFromAssemblyPath(path).GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
            const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (var t in types)
            {
                bool derives = false;
                for (var b = t.BaseType; b != null; b = b.BaseType)
                    if (IsProvider(b.Assembly.GetName().Name!)) { derives = true; break; }
                if (!derives) continue;
                foreach (var m in t.GetMethods(declared))
                {
                    if (!m.IsVirtual || (m.Attributes & MethodAttributes.NewSlot) != 0) continue;
                    var baseDef = RootDefinition(m);
                    if (baseDef == null) continue;
                    var owner = baseDef.DeclaringType!;
                    var asm = owner.Assembly.GetName().Name!;
                    if (!IsProvider(asm)) continue;
                    var gdef = owner.IsConstructedGenericType ? owner.GetGenericTypeDefinition() : owner;
                    Require(gdef);
                    Require(baseDef.ReturnType);
                    foreach (var p in baseDef.GetParameters()) Require(p.ParameterType);
                    Use(asm, gdef.FullName!, $"{Shapes.Cs(baseDef.ReturnType)} {baseDef.Name}({string.Join(", ", baseDef.GetParameters().Select(p => Shapes.Cs(p.ParameterType)))})", "override", consumer);
                }
            }
        }

        // Script classes the game's data uses.
        foreach (var line in File.ReadAllLines(dataScripts))
        {
            var parts = line.Split('\t');
            if (parts.Length >= 2 && IsProvider(parts[0])) Require(Lookup(parts[0], parts[1]));
        }

        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            Require(t.DeclaringType);
            Require(t.BaseType);
            foreach (var i in t.GetInterfaces())
            {
                var gi = i.IsConstructedGenericType ? i.GetGenericTypeDefinition() : i;
                if (gi.IsPublic || gi.IsNestedPublic) Require(i);
            }
            if (t.IsGenericTypeDefinition)
                foreach (var gp in t.GetGenericArguments()) foreach (var c in gp.GetGenericParameterConstraints()) Require(c);
            if ((t.IsPublic || t.IsNestedPublic) && !t.IsSealed && !t.IsInterface)
            {
                const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                foreach (var m in t.GetMethods(declared))
                {
                    if (!m.IsAbstract || !(m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly)) continue;
                    Require(m.ReturnType);
                    foreach (var p in m.GetParameters()) Require(p.ParameterType);
                }
            }
            if (!t.IsEnum && !t.IsInterface)
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (Shapes.IsSerializedField(f)) Require(f.FieldType);
            foreach (var a in t.GetCustomAttributesData())
                if (Shapes.IsContractAttribute(a.AttributeType.FullName!))
                    foreach (var arg in a.ConstructorArguments) if (arg.Value is Type at) Require(at);
            if (t.BaseType?.FullName == "System.MulticastDelegate")
            {
                var invoke = t.GetMethod("Invoke")!;
                Require(invoke.ReturnType);
                foreach (var p in invoke.GetParameters()) Require(p.ParameterType);
            }
            if (t.IsInterface)
                foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    switch (m)
                    {
                        case MethodInfo mi: Require(mi.ReturnType); foreach (var p in mi.GetParameters()) Require(p.ParameterType); break;
                        case PropertyInfo pi: Require(pi.PropertyType); break;
                        case EventInfo ei: Require(ei.EventHandlerType); break;
                    }
        }

        Directory.CreateDirectory(outDir);
        var rows = required.Values
            .Select(t => (Assembly: t.Assembly.GetName().Name!, Type: t))
            .OrderBy(r => r.Assembly, StringComparer.Ordinal).ThenBy(r => r.Type.FullName, StringComparer.Ordinal)
            .ToList();
        File.WriteAllLines(Path.Combine(outDir, "package-types.tsv"), rows.Select(r =>
            $"{r.Assembly}\t{Shapes.Kind(r.Type)}\t{r.Type.FullName}\t{(r.Type.BaseType == null ? "" : Shapes.Cs(r.Type.BaseType))}\t{string.Join(",", r.Type.GetInterfaces().Select(Shapes.Cs))}"));
        File.WriteAllLines(Path.Combine(outDir, "package-members.tsv"), members.Select(m => $"{m.Key}\t{string.Join(",", m.Value)}"));
        File.WriteAllLines(Path.Combine(outDir, "package-versions.tsv"), providers.Order(StringComparer.Ordinal)
            .Select(p => $"{p}\t{mlc.LoadFromAssemblyName(p).GetName().Version}"));
        Console.WriteLine($"{rows.Count} required package types, {members.Count} used members");
        foreach (var g in rows.GroupBy(r => r.Assembly)) Console.WriteLine($"  {g.Key}: {g.Count()}");

        Shapes.PackageMode = true;
        Shapes.IsProvider = IsProvider;
        Shapes.Required = required.Values.ToHashSet();
        foreach (var assembly in rows.GroupBy(r => r.Assembly))
        {
            var dir = Path.Combine(genRoot, assembly.Key, "Generated");
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(dir);
            foreach (var file in assembly.GroupBy(r => Shapes.Outermost(r.Type).Namespace ?? ""))
            {
                var sb = new StringBuilder();
                sb.AppendLine("// <auto-generated> by tools/SurfaceGen (packages) from the package's metadata: type shapes and serialized fields. Do not edit. </auto-generated>");
                sb.AppendLine("#nullable disable");
                if (file.Key.Length > 0) sb.AppendLine($"namespace {file.Key}\n{{");
                foreach (var (_, t) in file.OrderBy(r => r.Type.FullName, StringComparer.Ordinal))
                    sb.Append(Shapes.Declare(t));
                if (file.Key.Length > 0) sb.AppendLine("}");
                File.WriteAllText(Path.Combine(dir, (file.Key.Length > 0 ? file.Key : "_global") + ".cs"), sb.ToString());
            }
        }
        return 0;
    }

    /// The method an override ultimately overrides: the highest method up the base chain with the
    /// same name and parameter types (MethodInfo.GetBaseDefinition is unavailable on a
    /// MetadataLoadContext).
    static MethodInfo? RootDefinition(MethodInfo m)
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var parameters = m.GetParameters().Select(p => Shapes.Cs(p.ParameterType)).ToArray();
        MethodInfo? root = null;
        for (var b = m.DeclaringType!.BaseType; b != null; b = b.BaseType)
            foreach (var candidate in b.GetMethods(declared))
                if (candidate.Name == m.Name && candidate.IsVirtual
                    && candidate.GetParameters().Select(p => Shapes.Cs(p.ParameterType)).SequenceEqual(parameters))
                    root = candidate;
        return root;
    }
}

/// C#-like names of the types in a member reference's signature, for the members list.
sealed class SigNames(RefNames names) : ISignatureTypeProvider<string, object?>
{
    public string GetArrayType(string e, ArrayShape s) => e + "[" + new string(',', s.Rank - 1) + "]";
    public string GetByReferenceType(string e) => "ref " + e;
    public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) => g[..(g.IndexOf('`') is var i and >= 0 ? i : g.Length)] + "<" + string.Join(", ", a) + ">";
    public string GetGenericMethodParameter(object? c, int i) => "!!" + i;
    public string GetGenericTypeParameter(object? c, int i) => "!" + i;
    public string GetModifiedType(string m, string u, bool r) => u;
    public string GetPinnedType(string e) => e;
    public string GetPointerType(string e) => e + "*";
    public string GetPrimitiveType(PrimitiveTypeCode t) => t.ToString();
    public string GetSZArrayType(string e) => e + "[]";
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k)
    {
        var td = r.GetTypeDefinition(h);
        return r.GetString(td.Namespace) is { Length: > 0 } ns ? ns + "." + r.GetString(td.Name) : r.GetString(td.Name);
    }
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => names.Resolve(h).name;
    public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) => "spec";
}
