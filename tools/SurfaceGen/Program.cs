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

// Types Unity's own behaviour creates although no consumer names them (e.g. the colliders
// GameObject.CreatePrimitive adds), listed as "Module<TAB>FullName" with comments.
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

static class Shapes
{
    public static HashSet<Type> Required = new();

    public static string Kind(Type t) =>
        t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.BaseType?.FullName == "System.MulticastDelegate" ? "delegate"
        : t.IsValueType ? "struct" : t.IsAbstract && t.IsSealed ? "static class" : "class";

    public static bool IsGenerated(Type t) => true;

    public static Type Outermost(Type t) { while (t.DeclaringType != null) t = t.DeclaringType; return t; }

    static readonly Dictionary<string, string> Keywords = new()
    {
        ["System.Void"] = "void", ["System.Boolean"] = "bool", ["System.Byte"] = "byte", ["System.SByte"] = "sbyte",
        ["System.Int16"] = "short", ["System.UInt16"] = "ushort", ["System.Int32"] = "int", ["System.UInt32"] = "uint",
        ["System.Int64"] = "long", ["System.UInt64"] = "ulong", ["System.Single"] = "float", ["System.Double"] = "double",
        ["System.Char"] = "char", ["System.String"] = "string", ["System.Object"] = "object", ["System.IntPtr"] = "System.IntPtr",
    };

    /// C# spelling of a type, fully qualified with global::, generic args included.
    public static string Cs(Type t)
    {
        if (t.IsByRef) return Cs(t.GetElementType()!);
        if (t.IsPointer) return Cs(t.GetElementType()!) + "*";
        if (t.IsArray) return Cs(t.GetElementType()!) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
        if (t.IsGenericParameter) return t.Name;
        if (t.FullName != null && Keywords.TryGetValue(t.FullName, out var kw)) return kw;
        var args = t.IsGenericType ? t.GetGenericArguments() : [];
        return "global::" + QualifiedName(t, args);
    }

    static string QualifiedName(Type t, Type[] allArgs)
    {
        // Generic arguments are distributed over the nesting chain by declared arity.
        var chain = new List<Type>();
        for (var c = t; c != null; c = c.DeclaringType) chain.Insert(0, c);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(chain[0].Namespace)) sb.Append(chain[0].Namespace).Append('.');
        int used = 0;
        for (int i = 0; i < chain.Count; i++)
        {
            if (i > 0) sb.Append('.');
            var name = chain[i].Name;
            var tick = name.IndexOf('`');
            if (tick >= 0)
            {
                int arity = int.Parse(name[(tick + 1)..]);
                sb.Append(name[..tick]).Append('<').Append(string.Join(", ", allArgs.Skip(used).Take(arity).Select(Cs))).Append('>');
                used += arity;
            }
            else sb.Append(name);
        }
        return sb.ToString();
    }

    static string SimpleName(Type t)
    {
        var name = t.Name;
        var tick = name.IndexOf('`');
        if (tick < 0) return name;
        // Only this type's own generic parameters (those not declared by an enclosing type).
        int outer = t.DeclaringType?.GetGenericArguments().Length ?? 0;
        var own = t.GetGenericArguments().Skip(outer).Select(a => a.Name);
        return name[..tick] + "<" + string.Join(", ", own) + ">";
    }

    static string Variance(Type gp) =>
        (gp.GenericParameterAttributes & GenericParameterAttributes.VarianceMask) switch
        {
            GenericParameterAttributes.Covariant => "out ",
            GenericParameterAttributes.Contravariant => "in ",
            _ => "",
        };

    static string Parameters(ParameterInfo[] ps) => string.Join(", ", ps.Select(p =>
    {
        var mod = p.ParameterType.IsByRef ? (p.IsOut ? "out " : p.IsIn ? "in " : "ref ") : "";
        var parms = p.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "System.ParamArrayAttribute") ? "params " : "";
        return $"{parms}{mod}{Cs(p.ParameterType)} {Escape(p.Name!)}";
    }));

    static string Escape(string name) => name is "object" or "string" or "base" or "params" or "event" or "ref" or "out" or "in" or "operator" or "fixed" or "lock" ? "@" + name : name;

    static string Constraints(Type[] gps)
    {
        var sb = new StringBuilder();
        foreach (var gp in gps)
        {
            var parts = new List<string>();
            var a = gp.GenericParameterAttributes;
            if ((a & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) parts.Add("struct");
            else if ((a & GenericParameterAttributes.ReferenceTypeConstraint) != 0) parts.Add("class");
            parts.AddRange(gp.GetGenericParameterConstraints().Where(c => c.FullName != "System.ValueType").Select(Cs));
            if ((a & GenericParameterAttributes.DefaultConstructorConstraint) != 0 && (a & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0) parts.Add("new()");
            if (parts.Count > 0) sb.Append($" where {gp.Name} : {string.Join(", ", parts)}");
        }
        return sb.ToString();
    }

    /// Declaration of a generated type, wrapped in partial declarations of its enclosing types.
    public static string Declare(Type t)
    {
        var chain = new List<Type>();
        for (var c = t.DeclaringType; c != null; c = c.DeclaringType) chain.Insert(0, c);
        var sb = new StringBuilder();
        var indent = "    ";
        foreach (var outer in chain)
        {
            var kind = outer.IsInterface ? "interface" : outer.IsValueType ? "struct" : "class";
            var mods = outer.IsAbstract && outer.IsSealed && !outer.IsInterface ? "static " : "";
            sb.AppendLine($"{indent}{Visibility(outer)} {mods}partial {kind} {SimpleName(outer)}\n{indent}{{");
            indent += "    ";
        }
        sb.Append(Body(t, indent));
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            indent = indent[..^4];
            sb.AppendLine($"{indent}}}");
        }
        return sb.ToString();
    }

    static string Visibility(Type t) => t.IsPublic || t.IsNestedPublic ? "public" : t.IsNestedFamily ? "protected" : t.IsNestedFamORAssem ? "protected internal" : "internal";

    static string Body(Type t, string indent)
    {
        var sb = new StringBuilder();
        foreach (var a in t.GetCustomAttributesData())
            if (a.AttributeType.FullName == "System.FlagsAttribute") sb.AppendLine($"{indent}[global::System.Flags]");
            else if (a.AttributeType.FullName == "System.ObsoleteAttribute") { /* omitted: obsoletion would only add warnings in our build */ }
        if (t.IsEnum)
        {
            var underlying = t.GetEnumUnderlyingType();
            var ut = underlying.FullName == "System.Int32" ? "" : " : " + Cs(underlying);
            sb.AppendLine($"{indent}{Visibility(t)} enum {t.Name}{ut}\n{indent}{{");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                sb.AppendLine($"{indent}    {f.Name} = {Literal(f.GetRawConstantValue()!)},");
            sb.AppendLine($"{indent}}}");
        }
        else if (t.IsInterface)
        {
            var bases = t.GetInterfaces().Where(i => !t.GetInterfaces().Any(o => o != i && o.GetInterfaces().Contains(i))).Select(Cs).ToList();
            var gps = t.IsGenericTypeDefinition ? t.GetGenericArguments() : [];
            var name = gps.Length > 0 ? t.Name[..t.Name.IndexOf('`')] + "<" + string.Join(", ", gps.Select(g => Variance(g) + g.Name)) + ">" : t.Name;
            sb.AppendLine($"{indent}{Visibility(t)} interface {name}{(bases.Count > 0 ? " : " + string.Join(", ", bases) : "")}{Constraints(gps)}\n{indent}{{");
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var idx = p.GetIndexParameters();
                var head = idx.Length > 0 ? $"this[{Parameters(idx)}]" : p.Name;
                sb.AppendLine($"{indent}    {Cs(p.PropertyType)} {head} {{ {(p.GetMethod != null ? "get; " : "")}{(p.SetMethod != null ? "set; " : "")}}}");
            }
            foreach (var e in t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                sb.AppendLine($"{indent}    event {Cs(e.EventHandlerType!)} {e.Name};");
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
            {
                var mgps = m.IsGenericMethodDefinition ? m.GetGenericArguments() : [];
                var mname = mgps.Length > 0 ? $"{m.Name}<{string.Join(", ", mgps.Select(g => g.Name))}>" : m.Name;
                sb.AppendLine($"{indent}    {Cs(m.ReturnType)} {mname}({Parameters(m.GetParameters())}){Constraints(mgps)};");
            }
            sb.AppendLine($"{indent}}}");
        }
        else if (t.BaseType?.FullName != "System.MulticastDelegate")
        {
            sb.Append(Header(t, indent));
        }
        else
        {
            var invoke = t.GetMethod("Invoke")!;
            var gps = t.IsGenericTypeDefinition ? t.GetGenericArguments() : [];
            var name = gps.Length > 0 ? t.Name[..t.Name.IndexOf('`')] + "<" + string.Join(", ", gps.Select(g => Variance(g) + g.Name)) + ">" : t.Name;
            sb.AppendLine($"{indent}{Visibility(t)} delegate {Cs(invoke.ReturnType)} {name}({Parameters(invoke.GetParameters())}){Constraints(gps)};");
        }
        return sb.ToString();
    }

    /// Attribute types whose presence on a Unity type is part of its contract (engine behaviour reads them).
    static readonly HashSet<string> ContractAttributes =
    [
        "UnityEngine.RequireComponent", "UnityEngine.DisallowMultipleComponent", "UnityEngine.ExecuteAlways",
        "UnityEngine.ExecuteInEditMode", "UnityEngine.AddComponentMenu", "UnityEngine.SelectionBaseAttribute",
    ];

    /// Class/struct declaration: modifiers, base, directly-added interfaces, constraints, contract
    /// attributes and abstract members. Bodies of everything else are written by hand in partial parts.
    public static bool IsContractAttribute(string fullName) => ContractAttributes.Contains(fullName);

    static string Header(Type t, string indent)
    {
        var sb = new StringBuilder();
        const int tdSerializable = 0x2000; // TypeAttributes.Serializable, obsolete as a named member
        if (((int)t.Attributes & tdSerializable) != 0) sb.AppendLine($"{indent}[global::System.Serializable]");
        foreach (var a in t.GetCustomAttributesData())
        {
            var an = a.AttributeType.FullName!;
            if (an == "System.AttributeUsageAttribute")
            {
                var targets = (AttributeTargets)(int)a.ConstructorArguments[0].Value!;
                var named = string.Concat(a.NamedArguments.Select(n => $", {n.MemberName} = {(n.TypedValue.Value is bool b ? (b ? "true" : "false") : n.TypedValue.Value)}"));
                sb.AppendLine($"{indent}[global::System.AttributeUsage({EnumFlags("global::System.AttributeTargets", targets)}{named})]");
            }
            else if (ContractAttributes.Contains(an))
            {
                var ctorArgs = string.Join(", ", a.ConstructorArguments.Select(Arg));
                var named = string.Join(", ", a.NamedArguments.Select(n => $"{n.MemberName} = {Arg(n.TypedValue)}"));
                var all = string.Join(", ", new[] { ctorArgs, named }.Where(x => x.Length > 0));
                sb.AppendLine($"{indent}[global::{an}({all})]");
            }
        }
        var gps = t.IsGenericTypeDefinition ? t.GetGenericArguments().Skip(t.DeclaringType?.GetGenericArguments().Length ?? 0).ToArray() : [];
        var kind = t.IsValueType ? "struct" : "class";
        var mods = t.IsValueType ? "" : t.IsAbstract && t.IsSealed ? "static " : t.IsAbstract ? "abstract " : t.IsSealed ? "sealed " : "";
        var bases = new List<string>();
        if (!t.IsValueType && t.BaseType != null && t.BaseType.FullName != "System.Object") bases.Add(Cs(t.BaseType));
        var inherited = t.BaseType?.GetInterfaces() ?? [];
        foreach (var i in t.GetInterfaces())
        {
            if (inherited.Contains(i)) continue;
            if (i.Namespace == "System.Runtime.InteropServices" && i.Name.StartsWith('_')) continue; // Mono-only COM shims
            var gi = i.IsConstructedGenericType ? i.GetGenericTypeDefinition() : i;
            if (gi.Assembly.GetName().Name!.StartsWith("UnityEngine", StringComparison.Ordinal) && !Required.Contains(gi)) continue;
            bases.Add(Cs(i));
        }
        sb.AppendLine($"{indent}{Visibility(t)} {mods}partial {kind} {SimpleName(t)}{(bases.Count > 0 ? " : " + string.Join(", ", bases) : "")}{Constraints(gps)}\n{indent}{{");
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        // Abstract members are part of the contract only where consumers can derive from the
        // type; Unity's internal types are reachable only through InternalsVisibleTo friends,
        // which use members, not derivation.
        bool externallyDerivable = (t.IsPublic || t.IsNestedPublic) && !t.IsSealed;
        foreach (var p in externallyDerivable ? t.GetProperties(declared) : [])
        {
            var acc = p.GetMethod ?? p.SetMethod!;
            if (!acc.IsAbstract || !(acc.IsPublic || acc.IsFamily || acc.IsFamilyOrAssembly)) continue;
            var vis = acc.IsPublic ? "public" : "protected";
            sb.AppendLine($"{indent}    {vis} abstract {Cs(p.PropertyType)} {p.Name} {{ {(p.GetMethod != null ? "get; " : "")}{(p.SetMethod != null ? "set; " : "")}}}");
        }
        foreach (var m in (externallyDerivable ? t.GetMethods(declared) : []).Where(m => m.IsAbstract && !m.IsSpecialName && (m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly)))
        {
            var vis = m.IsPublic ? "public" : "protected";
            var mgps = m.IsGenericMethodDefinition ? m.GetGenericArguments() : [];
            var mname = mgps.Length > 0 ? $"{m.Name}<{string.Join(", ", mgps.Select(g => g.Name))}>" : m.Name;
            sb.AppendLine($"{indent}    {vis} abstract {Cs(m.ReturnType)} {mname}({Parameters(m.GetParameters())}){Constraints(mgps)};");
        }
        sb.AppendLine($"{indent}}}");
        return sb.ToString();
    }

    static string Arg(CustomAttributeTypedArgument a) => a.Value switch
    {
        Type ty => $"typeof({Cs(ty)})",
        string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
        bool b => b ? "true" : "false",
        null => "null",
        _ when a.ArgumentType.IsEnum => $"({Cs(a.ArgumentType)}){a.Value}",
        _ => Convert.ToString(a.Value, System.Globalization.CultureInfo.InvariantCulture)!,
    };

    static string EnumFlags(string typeName, AttributeTargets v) =>
        v == AttributeTargets.All ? $"{typeName}.All"
        : string.Join(" | ", Enum.GetValues<AttributeTargets>().Where(f => f != AttributeTargets.All && (v & f) == f).Select(f => $"{typeName}.{f}"));

    static string Literal(object v) => v switch
    {
        long l when l < 0 => $"unchecked((long){l})",
        int i when i < 0 => $"unchecked((int){i})",
        short s when s < 0 => $"unchecked((short){s})",
        sbyte s when s < 0 => $"unchecked((sbyte){s})",
        _ => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)!,
    };
}

sealed class RefNames(MetadataReader md)
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
        if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance) return (null, "");
        blob.ReadSignatureTypeCode();
        var handle = blob.ReadTypeHandle();
        return handle.Kind == HandleKind.TypeReference ? Resolve((TypeReferenceHandle)handle) : (null, "");
    }
}

/// Collects every referenced (assembly, type) appearing in a decoded signature.
sealed class SigCollector(RefNames names) : ISignatureTypeProvider<object?, object?>
{
    public readonly List<(string asm, string name)> Found = new();
    public object? GetArrayType(object? e, ArrayShape s) => null;
    public object? GetByReferenceType(object? e) => null;
    public object? GetFunctionPointerType(MethodSignature<object?> s) => null;
    public object? GetGenericInstantiation(object? g, System.Collections.Immutable.ImmutableArray<object?> a) => null;
    public object? GetGenericMethodParameter(object? c, int i) => null;
    public object? GetGenericTypeParameter(object? c, int i) => null;
    public object? GetModifiedType(object? m, object? u, bool r) => null;
    public object? GetPinnedType(object? e) => null;
    public object? GetPointerType(object? e) => null;
    public object? GetPrimitiveType(PrimitiveTypeCode t) => null;
    public object? GetSZArrayType(object? e) => null;
    public object? GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => null;
    public object? GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k)
    {
        var (asm, name) = names.Resolve(h);
        if (asm != null) Found.Add((asm, name));
        return null;
    }
    public object? GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) => null;
}
