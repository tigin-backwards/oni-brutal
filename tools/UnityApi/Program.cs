// Prints the declared API of Unity types (from the game's original Unity assemblies): modifiers,
// base type, interfaces, fields in declaration order, properties, events and methods.
// A reference for writing engine code with exact signatures.
//
// usage: UnityApi <unityManagedDir> <full.type.Name> [more types...]
using System.Reflection;

var managed = args[0];
using var mlc = new MetadataLoadContext(new PathAssemblyResolver(Directory.GetFiles(managed, "*.dll")), "mscorlib");
var assemblies = Directory.GetFiles(managed, "UnityEngine*.dll").Concat(Directory.GetFiles(managed, "Unity.*.dll"))
    .Select(f => { try { return mlc.LoadFromAssemblyPath(f); } catch { return null; } }).Where(a => a != null).ToList();
const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

foreach (var name in args.Skip(1))
{
    var t = assemblies.Select(a => a!.GetType(name)).FirstOrDefault(x => x != null);
    if (t == null) { Console.WriteLine($"// {name}: not found"); continue; }
    Console.WriteLine($"// [{t.Assembly.GetName().Name}] {t.Attributes}");
    foreach (var a in t.GetCustomAttributesData()) Console.WriteLine($"[{a}]");
    Console.WriteLine($"{t.FullName} : {t.BaseType?.FullName} {string.Join(", ", t.GetInterfaces().Select(i => i.FullName))}");
    foreach (var f in t.GetFields(all))
        Console.WriteLine($"  field {Vis(f.IsPublic, f.IsFamily, f.IsAssembly, f.IsFamilyOrAssembly)}{(f.IsStatic ? " static" : "")}{(f.IsInitOnly ? " readonly" : "")}{(f.IsLiteral ? $" const={f.GetRawConstantValue()}" : "")} {f.FieldType} {f.Name} {string.Join(" ", f.GetCustomAttributesData().Select(a => "[" + a.AttributeType.Name + "]"))}");
    foreach (var p in t.GetProperties(all))
    {
        var acc = p.GetMethod ?? p.SetMethod!;
        Console.WriteLine($"  prop {Vis(acc.IsPublic, acc.IsFamily, acc.IsAssembly, acc.IsFamilyOrAssembly)}{(acc.IsStatic ? " static" : "")}{(acc.IsVirtual ? " virtual" : "")} {p.PropertyType} {p.Name} {{{(p.GetMethod != null ? " get;" : "")}{(p.SetMethod != null ? " set;" : "")} }}");
    }
    foreach (var e in t.GetEvents(all)) Console.WriteLine($"  event {e.EventHandlerType} {e.Name}");
    foreach (var c in t.GetConstructors(all))
        Console.WriteLine($"  ctor {Vis(c.IsPublic, c.IsFamily, c.IsAssembly, c.IsFamilyOrAssembly)}({string.Join(", ", c.GetParameters().Select(p => p.ParameterType + " " + p.Name))})");
    foreach (var m in t.GetMethods(all).Where(m => !m.IsSpecialName))
        Console.WriteLine($"  method {Vis(m.IsPublic, m.IsFamily, m.IsAssembly, m.IsFamilyOrAssembly)}{(m.IsStatic ? " static" : "")}{(m.IsAbstract ? " abstract" : m.IsVirtual ? (m.IsFinal ? " final" : "") + (m.Attributes.HasFlag(MethodAttributes.NewSlot) ? " virtual" : " override") : "")} {m.ReturnType} {m.Name}{(m.IsGenericMethodDefinition ? "<" + string.Join(",", m.GetGenericArguments().Select(g => g.Name)) + ">" : "")}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType + " " + p.Name + (p.HasDefaultValue ? " = " + p.RawDefaultValue : "")))})");
    Console.WriteLine();
}

static string Vis(bool pub, bool fam, bool asm, bool famOrAsm) => pub ? "public" : fam ? "protected" : famOrAsm ? "protected internal" : asm ? "internal" : "private";
