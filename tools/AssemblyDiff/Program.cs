// Compares the observable shape of two builds of the same library: types (kind, visibility,
// layout kind, pack, size), fields (type, attributes, explicit offsets, marshalling), methods
// (signatures, attributes, P/Invoke entry points), properties and events. Prints differences.
//
// usage: AssemblyDiff <a.dll> <a-references-dir>[:<dir>...] <b.dll> <b-references-dir>[:<dir>...]
using System.Reflection;
using System.Runtime.InteropServices;

var a = Describe(args[0], args[1]);
var b = Describe(args[2], args[3]);
int diffs = 0;
foreach (var line in a.Except(b).Order(StringComparer.Ordinal)) { Console.WriteLine("- " + line); diffs++; }
foreach (var line in b.Except(a).Order(StringComparer.Ordinal)) { Console.WriteLine("+ " + line); diffs++; }
Console.WriteLine($"{diffs} differences ({a.Count} vs {b.Count} lines)");
return diffs == 0 ? 0 : 1;

static HashSet<string> Describe(string path, string refDirs)
{
    var files = refDirs.Split(':').SelectMany(d => Directory.GetFiles(d, "*.dll"))
        .Concat(Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
        .GroupBy(Path.GetFileName).Select(g => g.First()).Append(path).ToList();
    var core = files.Any(f => Path.GetFileName(f) == "mscorlib.dll" && !f.StartsWith(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory())) ? "mscorlib" : "System.Private.CoreLib";
    var mlc = new MetadataLoadContext(new PathAssemblyResolver(files), core);
    var asm = mlc.LoadFromAssemblyPath(path);
    var lines = new HashSet<string>(StringComparer.Ordinal);
    const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    foreach (var t in asm.GetTypes())
    {
        if (t.Name.Contains('<') || t.FullName?.StartsWith("<PrivateImplementationDetails>") == true) continue; // compiler-generated
        var tn = Name(t);
        var layout = t.StructLayoutAttribute;
        lines.Add($"type {tn} {t.Attributes & ~TypeAttributes.BeforeFieldInit} base={(t.BaseType == null ? "" : Name(t.BaseType))} layout={layout?.Value} pack={layout?.Pack} size={layout?.Size}");
        foreach (var f in t.GetFields(all))
        {
            if (f.Name.Contains('<')) continue;
            var offset = f.GetCustomAttributesData().FirstOrDefault(c => c.AttributeType.Name == "FieldOffsetAttribute")?.ConstructorArguments[0].Value;
            var marshal = f.GetCustomAttributesData().FirstOrDefault(c => c.AttributeType.Name == "MarshalAsAttribute");
            var m = marshal == null ? "" : $" marshal={marshal.ConstructorArguments[0].Value}{string.Concat(marshal.NamedArguments.Select(n => $",{n.MemberName}={n.TypedValue.Value}"))}";
            // A fixed buffer is the CoreCLR form of a by-value array; compare the native size only.
            var fixedBuf = f.GetCustomAttributesData().FirstOrDefault(c => c.AttributeType.Name == "FixedBufferAttribute");
            var ft = fixedBuf != null ? $"fixed {Name((Type)fixedBuf.ConstructorArguments[0].Value!)}[{fixedBuf.ConstructorArguments[1].Value}]" : Name(f.FieldType);
            lines.Add($"field {tn}.{f.Name} : {ft} {f.Attributes & ~FieldAttributes.HasFieldMarshal} offset={offset}{m}");
        }
        foreach (var mi in t.GetMethods(all).Cast<MethodBase>().Concat(t.GetConstructors(all)))
        {
            var sig = string.Join(",", mi.GetParameters().Select(p => Name(p.ParameterType)));
            var ret = mi is MethodInfo mm ? Name(mm.ReturnType) : "void";
            var pinv = "";
            if ((mi.Attributes & MethodAttributes.PinvokeImpl) != 0)
            {
                var di = mi.GetCustomAttributesData().FirstOrDefault(c => c.AttributeType.Name == "DllImportAttribute");
                pinv = di == null ? " pinvoke" : $" pinvoke {di.ConstructorArguments[0].Value} {string.Join(",", di.NamedArguments.Select(n => n.MemberName + "=" + n.TypedValue.Value))}";
            }
            lines.Add($"method {tn}.{mi.Name}({sig}) : {ret} {mi.Attributes & ~(MethodAttributes.HideBySig)}{pinv}");
        }
        foreach (var p in t.GetProperties(all)) lines.Add($"property {tn}.{p.Name} : {Name(p.PropertyType)}");
        foreach (var e in t.GetEvents(all)) lines.Add($"event {tn}.{e.Name} : {Name(e.EventHandlerType!)}");
    }
    return lines;
}

/// Type names without assembly qualification (the two builds target different core libraries).
static string Name(Type t)
{
    if (t.IsGenericParameter) return t.Name;
    if (t.IsByRef) return Name(t.GetElementType()!) + "&";
    if (t.IsPointer) return Name(t.GetElementType()!) + "*";
    if (t.IsArray) return Name(t.GetElementType()!) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
    if (t.IsConstructedGenericType)
        return Name(t.GetGenericTypeDefinition()) + "<" + string.Join(",", t.GetGenericArguments().Select(Name)) + ">";
    var decl = t.DeclaringType;
    if (decl != null) return Name(decl) + "+" + t.Name;
    return string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name;
}
