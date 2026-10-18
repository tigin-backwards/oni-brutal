using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

static class Shapes
{
    public static HashSet<Type> Required = new();

    /// Packages mode (`SurfaceGen packages`): the types are a replaced package's own, so class and
    /// struct headers also declare the serialized fields (their names, types and order are fixed
    /// by the game's data) and keep every interface the package's type implements.
    public static bool PackageMode;
    public static Func<string, bool> IsProvider = _ => false;

    /// Unity serializes an instance field that is neither static, constant nor readonly, not
    /// [NonSerialized], and either public or [SerializeField] (as the engine's SerializedLayout).
    public static bool IsSerializedField(FieldInfo f)
    {
        if (f.IsStatic || f.IsLiteral || f.IsInitOnly) return false;
        var attrs = f.GetCustomAttributesData();
        const int fdNotSerialized = 0x80; // FieldAttributes.NotSerialized ([NonSerialized]), obsolete as a named member
        if (((int)f.Attributes & fdNotSerialized) != 0 || attrs.Any(a => a.AttributeType.FullName == "System.NonSerializedAttribute")) return false;
        return f.IsPublic || attrs.Any(a => a.AttributeType.FullName == "UnityEngine.SerializeField");
    }

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
            if (PackageMode)
            {
                if (!(gi.IsPublic || gi.IsNestedPublic) && !Required.Contains(gi)) continue;
            }
            else if (gi.Assembly.GetName().Name!.StartsWith("UnityEngine", StringComparison.Ordinal) && !Required.Contains(gi)) continue;
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
        if (PackageMode)
            foreach (var f in t.GetFields(declared).Where(IsSerializedField))
            {
                foreach (var a in f.GetCustomAttributesData())
                {
                    var an = a.AttributeType.FullName;
                    if (an == "UnityEngine.SerializeField") sb.AppendLine($"{indent}    [global::UnityEngine.SerializeField]");
                    else if (an == "UnityEngine.SerializeReference") sb.AppendLine($"{indent}    [global::UnityEngine.SerializeReference]");
                    else if (an == "UnityEngine.Serialization.FormerlySerializedAsAttribute")
                        sb.AppendLine($"{indent}    [global::UnityEngine.Serialization.FormerlySerializedAs({Arg(a.ConstructorArguments[0])})]");
                }
                sb.AppendLine($"{indent}    {FieldVisibility(f)} {Cs(f.FieldType)} {Escape(f.Name)};");
            }
        sb.AppendLine($"{indent}}}");
        return sb.ToString();
    }

    static string FieldVisibility(FieldInfo f) =>
        f.IsPublic ? "public" : f.IsFamily ? "protected" : f.IsFamilyOrAssembly ? "protected internal" : f.IsAssembly ? "internal" : f.IsFamilyAndAssembly ? "private protected" : "private";

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
