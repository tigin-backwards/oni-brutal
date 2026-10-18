// Computes the Unity API surface our engine must provide, and emits the declarations whose
// shape is fully determined by Unity's API (enums with their values, interfaces, delegates).
//
// usage: SurfaceGen <unityManagedDir> <outDir> <generatedCodeDir> [engineRequiredTypes.tsv]
//
// Required types = every type the consumer assemblies reference in a Unity engine assembly,
// closed over: declaring types, base types, implemented interfaces, generic constraints, and the
// types in the signatures of referenced members. Writes:
//   <outDir>/required-types.tsv   module, kind, full name, base, interfaces
//   <generatedCodeDir>/<Module>/<Namespace>.cs   enums, interfaces and delegates (shape only)
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

if (args.Length > 0 && args[0] == "packages") return Packages.Run(args[1..]);

var managed = args[0];
var outDir = args[1];
var genDir = args[2];

string[] consumers =
[
    "Assembly-CSharp", "Assembly-CSharp-firstpass", "UnityEngine.UI", "Unity.TextMeshPro", "Unity.Timeline",
    "FMODUnity", "FMODUnityResonance", "FMODUnityHaptics", "ImGui.NET", "ImGui", "com.rlabrecque.steamworks.net",
];
bool IsEngine(string asm) => asm.StartsWith("UnityEngine.", StringComparison.Ordinal) && asm.EndsWith("Module", StringComparison.Ordinal);

var resolver = new PathAssemblyResolver(Directory.GetFiles(managed, "*.dll"));
using var mlc = new MetadataLoadContext(resolver, "mscorlib");

var required = new Dictionary<string, Type>(StringComparer.Ordinal); // key: assembly-qualified-ish
var requiredSet = new HashSet<Type>();
var queue = new Queue<Type>();

void Require(Type? t)
{
    if (t == null) return;
    if (t.HasElementType) { Require(t.GetElementType()); return; }
    if (t.IsGenericParameter) return; // constraints are required when the declaring definition is processed
    if (t.IsConstructedGenericType)
    {
        Require(t.GetGenericTypeDefinition());
        foreach (var a in t.GetGenericArguments()) Require(a);
        return;
    }
    var asm = t.Assembly.GetName().Name!;
    if (!IsEngine(asm)) return;
    // Compiler-generated types (fixed buffers, closures) are not declarable; they belong to
    // the hand-written body of their declaring type.
    if (t.Name.Contains('<')) return;
    var key = asm + "|" + t.FullName;
    if (required.TryAdd(key, t)) { requiredSet.Add(t); queue.Enqueue(t); }
}

// Seeds: type references from the consumers, plus every type in the signatures of their member
// references into engine types, read with SRM and looked up by name in the MLC.
Type? Lookup(string asm, string fullName)
{
    if (!IsEngine(asm)) return null;
    return mlc.LoadFromAssemblyName(asm).GetType(fullName, throwOnError: true);
}

// Types no consumer names that the engine still provides: ones Unity's own behaviour creates
// (e.g. the colliders GameObject.CreatePrimitive adds) and ones mod libraries use, listed as
// "Module<TAB>FullName" with comments.
if (args.Length > 3)
    foreach (var line in File.ReadAllLines(args[3]))
    {
        var entry = line.Split('#')[0].Trim();
        if (entry.Length == 0) continue;
        var parts = entry.Split('\t', StringSplitOptions.RemoveEmptyEntries);
        Require(Lookup(parts[0], parts[1]));
    }

foreach (var consumer in consumers)
{
    using var pe = new PEReader(File.OpenRead(Path.Combine(managed, consumer + ".dll")));
    var md = pe.GetMetadataReader();
    var names = new RefNames(md);
    foreach (var h in md.TypeReferences)
    {
        var (asm, name) = names.Resolve(h);
        if (asm != null) Require(Lookup(asm, name));
    }
    var collector = new SigCollector(names);
    foreach (var h in md.MemberReferences)
    {
        var mr = md.GetMemberReference(h);
        (string? asm, string name) owner = mr.Parent.Kind switch
        {
            HandleKind.TypeReference => names.Resolve((TypeReferenceHandle)mr.Parent),
            HandleKind.TypeSpecification => names.ResolveSpecOwner((TypeSpecificationHandle)mr.Parent),
            _ => (null, ""),
        };
        if (owner.asm == null || !IsEngine(owner.asm)) continue;
        collector.Found.Clear();
        var blob = md.GetBlobReader(mr.Signature);
        var dec = new SignatureDecoder<object?, object?>(collector, md, null);
        if (blob.ReadSignatureHeader().Kind == SignatureKind.Field) { blob.Reset(); dec.DecodeFieldSignature(ref blob); }
        else { blob.Reset(); dec.DecodeMethodSignature(ref blob); }
        foreach (var (asm, name) in collector.Found) Require(Lookup(asm, name));
        // Constraints of a referenced generic method are part of what the consumer relies on
        // (e.g. Timeline's PlayableExtensions.SetTime<U>(this U) where U : struct, IPlayable).
        var header = md.GetBlobReader(mr.Signature).ReadSignatureHeader();
        if (header.IsGeneric && owner.asm != null)
        {
            var ownerType = Lookup(owner.asm, owner.name);
            var name = md.GetString(mr.Name);
            foreach (var m in ownerType?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance) ?? [])
                if (m.Name == name && m.IsGenericMethodDefinition)
                    foreach (var gp in m.GetGenericArguments()) foreach (var c in gp.GetGenericParameterConstraints()) Require(c);
        }
    }
}

while (queue.Count > 0)
{
    var t = queue.Dequeue();
    Require(t.DeclaringType);
    Require(t.BaseType);
    // Engine interfaces implemented by a required type are part of its required shape only if a
    // consumer references the interface itself; BCL interfaces carry no engine requirements.
    if (t.IsGenericTypeDefinition)
        foreach (var gp in t.GetGenericArguments()) foreach (var c in gp.GetGenericParameterConstraints()) Require(c);
    // Whatever the generated header mentions must exist: abstract member signatures of
    // externally derivable types, and types named by contract attributes.
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
    // Types of serialized fields: the asset loader deserializes them, so they must exist with
    // Unity's shape (public instance fields, or [SerializeField] ones).
    if (!t.IsEnum && !t.IsInterface)
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            if (f.IsPublic || f.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "UnityEngine.SerializeField"))
                Require(f.FieldType);
    foreach (var a in t.GetCustomAttributesData())
        if (Shapes.IsContractAttribute(a.AttributeType.FullName!))
            foreach (var arg in a.ConstructorArguments) if (arg.Value is Type at) Require(at);
    // A delegate's or interface's shape is its signatures; an enum needs nothing more.
    if (typeof(MulticastDelegate).FullName == t.BaseType?.FullName)
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
    .Select(t => (Module: t.Assembly.GetName().Name!, Type: t))
    .OrderBy(r => r.Module, StringComparer.Ordinal).ThenBy(r => r.Type.FullName, StringComparer.Ordinal)
    .ToList();
File.WriteAllLines(Path.Combine(outDir, "required-types.tsv"), rows.Select(r =>
    $"{r.Module}\t{Shapes.Kind(r.Type)}\t{r.Type.FullName}\t{(r.Type.BaseType == null ? "" : Shapes.Cs(r.Type.BaseType))}\t{string.Join(",", r.Type.GetInterfaces().Select(Shapes.Cs))}"));
Console.WriteLine($"{rows.Count} required types");
Shapes.Required = requiredSet;
foreach (var g in rows.GroupBy(r => Shapes.Kind(r.Type)).OrderBy(g => g.Key)) Console.WriteLine($"  {g.Key}: {g.Count()}");

// Generated shapes: enums, interfaces, delegates. Nested ones are emitted inside a partial
// declaration of their (hand-written) declaring type.
if (Directory.Exists(genDir)) Directory.Delete(genDir, recursive: true);
var byFile = rows.Where(r => Shapes.IsGenerated(r.Type))
    .GroupBy(r => (r.Module, Ns: Shapes.Outermost(r.Type).Namespace ?? ""));
foreach (var file in byFile)
{
    var sb = new StringBuilder();
    sb.AppendLine("// <auto-generated> by tools/SurfaceGen from Unity's public API shape. Do not edit. </auto-generated>");
    sb.AppendLine("#nullable disable");
    if (file.Key.Ns.Length > 0) sb.AppendLine($"namespace {file.Key.Ns}\n{{");
    foreach (var (_, t) in file.OrderBy(r => r.Type.FullName, StringComparer.Ordinal))
        sb.Append(Shapes.Declare(t));
    if (file.Key.Ns.Length > 0) sb.AppendLine("}");
    var dir = Path.Combine(genDir, file.Key.Module);
    Directory.CreateDirectory(dir);
    File.WriteAllText(Path.Combine(dir, (file.Key.Ns.Length > 0 ? file.Key.Ns : "_global") + ".cs"), sb.ToString());
}
return 0;

