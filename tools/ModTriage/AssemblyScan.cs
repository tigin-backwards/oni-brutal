// What one mod assembly's own code is and uses: its own types (merged libraries removed), their
// base types, what their method bodies reference, and what they patch with Harmony.
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ModTriage;

/// A Harmony patch target. Helper is set when the patch is made by a shared helper library's
/// code (UtilLibs' shared tweaks), not by the mod's own.
public sealed record PatchTarget(string Type, string? Method, string Source, string By, bool Helper)
{
    public string Display => Method is null ? Type : $"{Type}.{Method}";
}

public sealed record OwnType(string Name, List<string> Bases, List<string> Interfaces, bool Helper);

/// What a body of code references: the mod's own code, or the shared helper library in it.
public sealed class CodeFacts
{
    /// External types referenced (method bodies, field types), by full name.
    public HashSet<string> RefTypes { get; } = new(StringComparer.Ordinal);
    /// External members referenced from method bodies, as "Type::member".
    public HashSet<string> RefMembers { get; } = new(StringComparer.Ordinal);
    /// PLib types and members used (merged or a separate PLib.dll), as "Area:Type::member".
    public SortedSet<string> PLibRefs { get; } = new(StringComparer.Ordinal);
    /// Attribute types on its types and members.
    public SortedSet<string> Attributes { get; } = new(StringComparer.Ordinal);
    public bool DefinesOnGUI { get; set; }
}

public sealed class AssemblyFacts
{
    public required string File { get; init; }
    public required string Name { get; init; }
    /// Set when the whole assembly is a library (0Harmony.dll, a separate PLib.dll).
    public Library WholeLibrary { get; set; }
    /// Type definitions, nested ones included.
    public int TypeCount { get; set; }
    /// Top-level types removed as merged libraries, per library.
    public SortedDictionary<string, int> Excluded { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> ExcludedNamespaces { get; } = new(StringComparer.Ordinal);
    /// Top-level types per namespace once libraries are removed (compiler-generated ones left out).
    public Dictionary<string, int> OwnTopLevel { get; } = new(StringComparer.Ordinal);
    /// Namespaces of the mod's entry classes (a UserMod2 nothing else derives from).
    public HashSet<string> EntryNamespaces { get; } = new(StringComparer.Ordinal);
    /// Own types, compiler-generated ones left out; shared-helper types are marked.
    public List<OwnType> Types { get; } = [];
    public CodeFacts Mod { get; } = new();
    public CodeFacts Helper { get; } = new();
    public List<PatchTarget> Patches { get; } = [];
    /// Harmony.Patch calls whose target type was not found next to the call.
    public int UnresolvedPatchCalls { get; set; }
}

public sealed class AssemblyScan
{
    readonly MetadataReader md;
    readonly PEReader pe;
    readonly Fingerprints fp;
    readonly GameIndex game;
    readonly AssemblyFacts facts;
    readonly Dictionary<TypeDefinitionHandle, Library> library = [];
    readonly Dictionary<MethodDefinitionHandle, List<(string type, string? method)>> lookups = [];
    /// Where the type being scanned records what it references.
    CodeFacts code;
    bool helper;

    AssemblyScan(PEReader pe, string file, Fingerprints fp, GameIndex game)
    {
        this.pe = pe;
        md = pe.GetMetadataReader();
        this.fp = fp;
        this.game = game;
        facts = new AssemblyFacts { File = file, Name = md.IsAssembly ? md.GetString(md.GetAssemblyDefinition().Name) : Path.GetFileNameWithoutExtension(file) };
        code = facts.Mod;
    }

    void Enter(string typeName)
    {
        var ns = Names.Namespace(typeName);
        helper = !facts.EntryNamespaces.Contains(ns) && fp.IsSharedHelper(ns, facts.OwnTopLevel.GetValueOrDefault(ns));
        code = helper ? facts.Helper : facts.Mod;
    }

    public static AssemblyFacts? Scan(string path, Fingerprints fp, GameIndex game)
    {
        using var stream = File.OpenRead(path);
        return Scan(stream, path, fp, game);
    }

    public static AssemblyFacts? Scan(Stream stream, string name, Fingerprints fp, GameIndex game)
    {
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata) return null;
        var scan = new AssemblyScan(pe, name, fp, game);
        scan.Run();
        return scan.facts;
    }

    void Run()
    {
        facts.WholeLibrary = Fingerprints.OfAssemblyName(facts.Name);
        ClassifyLibraries();
        facts.TypeCount = md.TypeDefinitions.Count - 1; // without <Module>
        if (facts.WholeLibrary != Library.None) return;
        // A separately shipped library under another file name: all of it fingerprints as one.
        if (facts.TypeCount > 0 && facts.Excluded.Count > 0 && md.TypeDefinitions.All(h =>
                LibraryOf(h) != Library.None || Names.FullName(md, h) is var n && (n == "<Module>" || n.StartsWith('<'))))
        {
            facts.WholeLibrary = Enum.Parse<Library>(facts.Excluded.MaxBy(kv => kv.Value).Key);
            return;
        }
        // The mod's entry (its UserMod2 that nothing else derives from) marks its own namespace,
        // which is never a shared helper's, even when another upload of the mod defines it too.
        var described = md.TypeDefinitions.Where(h => LibraryOf(h) == Library.None && !Names.FullName(md, h).Contains('<'))
            .ToDictionary(h => h, Describe);
        var baseNames = described.Values.SelectMany(o => o.Bases).ToHashSet(StringComparer.Ordinal);
        foreach (var o in described.Values)
            if (o.Bases.Contains("KMod.UserMod2") && !baseNames.Contains(o.Name)) facts.EntryNamespaces.Add(Names.Namespace(o.Name));
        foreach (var h in md.TypeDefinitions)
        {
            if (LibraryOf(h) != Library.None) continue;
            var td = md.GetTypeDefinition(h);
            var name = Names.FullName(md, h);
            if (name == "<Module>") continue;
            Enter(name);
            if (described.TryGetValue(h, out var own)) facts.Types.Add(own with { Helper = helper });
            Attributes(td.GetCustomAttributes());
            foreach (var fh in td.GetFields())
            {
                var f = md.GetFieldDefinition(fh);
                Attributes(f.GetCustomAttributes());
                try
                {
                    var sig = f.DecodeSignature(new SignatureNames(md), null);
                    foreach (var t in SignatureNames.Split(sig)) if (!IsOwnName(t)) code.RefTypes.Add(t);
                }
                catch (BadImageFormatException) { }
            }
            foreach (var ph in td.GetProperties()) Attributes(md.GetPropertyDefinition(ph).GetCustomAttributes());
            foreach (var mh in td.GetMethods())
            {
                var m = md.GetMethodDefinition(mh);
                Attributes(m.GetCustomAttributes());
                if (md.GetString(m.Name) == "OnGUI") code.DefinesOnGUI = true;
                ScanBody(mh, m, name);
            }
        }
        foreach (var h in md.TypeDefinitions)
        {
            if (LibraryOf(h) != Library.None) continue;
            Enter(Names.FullName(md, h));
            AttributePatches(h);
        }
        // Same target patched by a prefix and a postfix, or from several places: list it once.
        var unique = facts.Patches.OrderBy(p => p.Helper).DistinctBy(p => (p.Type, p.Method)).ToList();
        facts.Patches.Clear();
        facts.Patches.AddRange(unique);
    }

    // ---- libraries ---------------------------------------------------------------------------

    void ClassifyLibraries()
    {
        var byNamespace = new Dictionary<string, List<TypeDefinitionHandle>>(StringComparer.Ordinal);
        foreach (var h in md.TypeDefinitions)
        {
            var td = md.GetTypeDefinition(h);
            if (!td.GetDeclaringType().IsNil) continue;
            var name = md.GetString(td.Name);
            if (name.StartsWith('<')) continue;
            var ns = md.GetString(td.Namespace);
            if (!byNamespace.TryGetValue(ns, out var list)) byNamespace[ns] = list = [];
            list.Add(h);
        }
        foreach (var (ns, handles) in byNamespace)
        {
            var simple = handles.Select(h => md.GetString(md.GetTypeDefinition(h).Name)).ToList();
            var lib = fp.OfNamespace(ns, simple);
            if (lib != Library.None)
            {
                foreach (var h in handles) library[h] = lib;
                facts.ExcludedNamespaces.Add($"{lib}:{(ns.Length == 0 ? "(global)" : ns)}");
                Count(lib, handles.Count);
                continue;
            }
            var flattened = fp.FlattenedPLib(simple).ToHashSet(StringComparer.Ordinal);
            facts.OwnTopLevel[ns] = handles.Count - flattened.Count;
            if (flattened.Count == 0) continue;
            foreach (var h in handles)
                if (flattened.Contains(md.GetString(md.GetTypeDefinition(h).Name))) library[h] = Library.PLib;
            facts.ExcludedNamespaces.Add($"PLib:{(ns.Length == 0 ? "(global)" : ns)} ({flattened.Count} types)");
            Count(Library.PLib, flattened.Count);
        }
    }

    void Count(Library lib, int n) => facts.Excluded[lib.ToString()] = facts.Excluded.GetValueOrDefault(lib.ToString()) + n;

    Library LibraryOf(TypeDefinitionHandle h)
    {
        while (true)
        {
            var decl = md.GetTypeDefinition(h).GetDeclaringType();
            if (decl.IsNil) return library.GetValueOrDefault(h);
            h = decl;
        }
    }

    HashSet<string>? definedNames;

    /// A name defined in this assembly (field types decode to names only).
    bool IsOwnName(string fullName) =>
        (definedNames ??= md.TypeDefinitions.Select(h => Names.FullName(md, h)).ToHashSet(StringComparer.Ordinal)).Contains(fullName);

    // ---- types -------------------------------------------------------------------------------

    OwnType Describe(TypeDefinitionHandle h)
    {
        var bases = new List<string>();
        var interfaces = new List<string>();
        var current = h;
        for (int depth = 0; depth < 32; depth++)
        {
            var td = md.GetTypeDefinition(current);
            foreach (var ih in td.GetInterfaceImplementations())
                if (Names.Of(md, md.GetInterfaceImplementation(ih).Interface) is { } i) interfaces.Add(i.FullName);
            if (td.BaseType.IsNil || Names.Of(md, td.BaseType) is not { } b) break;
            bases.Add(b.FullName);
            if (b.IsDefinition) { current = b.Definition; continue; }
            var (more, moreInterfaces) = game.Chain(b.FullName);
            bases.AddRange(more);
            interfaces.AddRange(moreInterfaces);
            break;
        }
        return new OwnType(Names.FullName(md, h), bases, interfaces.Distinct().ToList(), helper);
    }

    void Attributes(CustomAttributeHandleCollection attrs)
    {
        foreach (var ah in attrs)
        {
            var ca = md.GetCustomAttribute(ah);
            if (Names.AttributeType(md, ca) is not { } t) continue;
            if (t.IsDefinition && LibraryOf(t.Definition) == Library.PLib)
                code.PLibRefs.Add($"{fp.PLib.GetValueOrDefault(Names.OuterSimple(t.FullName), "?")}:{t.Simple}");
            else if (fp.PLibArea(t) is { } area)
                code.PLibRefs.Add($"{area}:{t.Simple}");
            if (t.Namespace.StartsWith("System.Runtime.CompilerServices", StringComparison.Ordinal) || t.Namespace == "System.Diagnostics") continue;
            code.Attributes.Add(t.FullName);
        }
    }

    // ---- method bodies -----------------------------------------------------------------------

    static readonly HashSet<string> LookupCalls = new(StringComparer.Ordinal)
    {
        "Method", "DeclaredMethod", "GetMethod", "GetMethodSafe", "PropertyGetter", "PropertySetter",
        "DeclaredPropertyGetter", "DeclaredPropertySetter", "GetProperty", "GetPropertySafe", "Constructor",
        "DeclaredConstructor", "GetConstructor", "FirstMethod", "GetDeclaredMethods", "GetMethods",
        "GetOverloadedMethodSafe", "Getter", "Setter",
    };

    static readonly HashSet<string> TypeByNameCalls = new(StringComparer.Ordinal) { "TypeByName", "GetType", "GetTypeSafe" };

    void ScanBody(MethodDefinitionHandle mh, MethodDefinition m, string ownerName)
    {
        if (m.RelativeVirtualAddress == 0) return;
        MethodBodyBlock body;
        try { body = pe.GetMethodBody(m.RelativeVirtualAddress); }
        catch (BadImageFormatException) { return; }
        var by = $"{ownerName}.{md.GetString(m.Name)}";

        // The window since the last Patch call: types (ldtoken, TypeByName) and strings (ldstr) in
        // order, and the (type, method) pairs closed by lookups such as AccessTools.Method. The
        // original method is Patch's first argument, so the earliest pair or loose type wins.
        // Patch methods never do: pairs closed by `new HarmonyMethod(type, name)`, and pairs on
        // the mod's own types (a mod does not patch itself).
        var events = new List<(int seq, string? type, string? str, bool own)>();
        var pairs = new List<(int seq, string type, string? method)>();
        var allPairs = new List<(string type, string? method)>();
        int seq = 0, lastClose = -1;

        string? StringAfter(int index)
        {
            for (int i = index + 1; i < events.Count; i++) if (events[i].str != null) return events[i].str;
            return null;
        }

        void ClosePair(string member, bool patchMethod)
        {
            int lastType = events.FindLastIndex(e => e.type != null && (!patchMethod || e.seq > lastClose));
            int lastString = events.FindLastIndex(e => e.str != null);
            string type;
            string? method;
            bool own = false;
            // AccessTools.Method("Type:Method") names both in one string.
            if (lastString > lastType && member is "Method" or "DeclaredMethod" && events[lastString].str!.IndexOf(':') is var colon and > 0)
            {
                type = Names.FromSerialized(events[lastString].str![..colon]);
                method = events[lastString].str![(colon + 1)..];
                lastType = lastString;
            }
            else if (lastType >= 0)
            {
                type = events[lastType].type!;
                own = events[lastType].own;
                method = StringAfter(lastType);
            }
            else return;
            if (member is "PropertyGetter" or "DeclaredPropertyGetter" or "Getter" && method != null) method = "get_" + method;
            else if (member is "PropertySetter" or "DeclaredPropertySetter" or "Setter" && method != null) method = "set_" + method;
            else if (member is "Constructor" or "DeclaredConstructor" or "GetConstructor") method = ".ctor";
            if (!patchMethod)
            {
                allPairs.Add((type, method));
                if (!own) pairs.Add((events[lastType].seq, type, method));
            }
            events.RemoveRange(lastType, events.Count - lastType);
            lastClose = seq++;
        }

        bool lastWasLookup = false;
        foreach (var op in IL.Tokens(body.GetILReader()))
        {
            bool afterLookup = lastWasLookup;
            lastWasLookup = false;
            if (op.OpCode == IL.Ldstr)
            {
                events.Add((seq++, null, md.GetUserString(MetadataTokens.UserStringHandle(op.Token & 0xFFFFFF)), false));
                continue;
            }
            var handle = IL.Handle(op.Token);
            if (handle.Kind is HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification)
            {
                if (Names.Of(md, handle) is not { } t) continue;
                NoteType(t);
                if (op.OpCode == IL.Ldtoken) events.Add((seq++, t.FullName, null, t.IsDefinition && LibraryOf(t.Definition) == Library.None));
                continue;
            }
            var (owner, member) = Names.MemberOf(md, handle);
            if (owner is not { } o) continue;
            NoteMember(o, member);
            // A generic method's instantiation, for what it loads: AssetBundle.LoadAsset<GameObject>.
            if (handle.Kind == HandleKind.MethodSpecification && !o.IsDefinition)
            {
                try
                {
                    var args = md.GetMethodSpecification((MethodSpecificationHandle)handle).DecodeSignature(new SignatureNames(md), null);
                    code.RefMembers.Add($"{o.FullName}::{member}<{string.Join(",", args)}>");
                }
                catch (BadImageFormatException) { }
            }
            if (!IL.IsCall(op.OpCode)) continue;
            var ownerSimple = Names.Simple(o.FullName);
            if (TypeByNameCalls.Contains(member) && ownerSimple is "AccessTools" or "Type" or "PPatchTools" or "PGameUtils")
            {
                int last = events.FindLastIndex(e => e.str != null);
                if (last >= 0) events[last] = (events[last].seq, Names.FromSerialized(events[last].str!), null, false);
            }
            else if (member == ".ctor" && ownerSimple == "HarmonyMethod")
            {
                // new HarmonyMethod(AccessTools.Method(typeof(Patches), "Prefix")): the lookup just
                // made found a patch method, not the original.
                if (afterLookup && pairs.Count > 0) pairs.RemoveAt(pairs.Count - 1);
                else ClosePair(member, patchMethod: true);
            }
            else if (LookupCalls.Contains(member))
            {
                int before = pairs.Count;
                ClosePair(member, patchMethod: false);
                lastWasLookup = pairs.Count > before;
                continue;
            }
            else if (member == "Patch" && !(o.IsDefinition && LibraryOf(o.Definition) == Library.None && ownerSimple != "Harmony"))
            {
                int loose = events.FindIndex(e => e.type != null && !e.own);
                var candidates = pairs.Select(p => (p.seq, p.type, p.method)).ToList();
                if (loose >= 0) candidates.Add((events[loose].seq, events[loose].type!, StringAfter(loose)));
                if (candidates.Count > 0)
                {
                    var first = candidates.MinBy(c => c.seq);
                    facts.Patches.Add(new PatchTarget(first.type, first.method, "il", by, helper));
                }
                else facts.UnresolvedPatchCalls++;
                events.Clear();
                pairs.Clear();
            }
        }
        if (allPairs.Count > 0) lookups[mh] = allPairs;
    }

    void NoteType(TypeName t)
    {
        if (t.IsDefinition)
        {
            if (LibraryOf(t.Definition) == Library.PLib)
                code.PLibRefs.Add($"{fp.PLib.GetValueOrDefault(Names.OuterSimple(t.FullName), "?")}:{t.Simple}");
            return;
        }
        if (fp.PLibArea(t) is { } area) code.PLibRefs.Add($"{area}:{t.Simple}");
        code.RefTypes.Add(t.FullName);
    }

    void NoteMember(TypeName owner, string member)
    {
        if (owner.IsDefinition)
        {
            var lib = LibraryOf(owner.Definition);
            if (lib == Library.PLib)
                code.PLibRefs.Add($"{fp.PLib.GetValueOrDefault(Names.OuterSimple(owner.FullName), "?")}:{owner.Simple}::{member}");
            else if (lib != Library.None)
                code.RefMembers.Add($"{owner.FullName}::{member}");
            return;
        }
        if (fp.PLibArea(owner) is { } area) code.PLibRefs.Add($"{area}:{owner.Simple}::{member}");
        code.RefTypes.Add(owner.FullName);
        code.RefMembers.Add($"{owner.FullName}::{member}");
    }

    // ---- [HarmonyPatch] ----------------------------------------------------------------------

    sealed record PatchAttr(string? Type, string? Method, int MethodType);

    List<PatchAttr> HarmonyAttrs(CustomAttributeHandleCollection attrs)
    {
        var list = new List<PatchAttr>();
        foreach (var ah in attrs)
        {
            var ca = md.GetCustomAttribute(ah);
            if (Names.AttributeType(md, ca) is not { } t || t.Simple != "HarmonyPatch") continue;
            string? type = null, method = null;
            int methodType = 0;
            try
            {
                var value = ca.DecodeValue(new SignatureNames(md));
                var strings = new List<string>();
                foreach (var arg in value.FixedArguments)
                {
                    if (arg.Type == SignatureNames.SystemType) type ??= arg.Value as string;
                    else if (arg.Type == "System.String" && arg.Value is string s) strings.Add(s);
                    else if (arg.Value is int e && !arg.Type.EndsWith("[]", StringComparison.Ordinal)) methodType = e;
                }
                // (string typeName, string methodName) in Harmony 2.3; otherwise the method's name.
                if (type == null && strings.Count >= 2) { type = strings[0]; method = strings[1]; }
                else if (strings.Count >= 1) method = strings[0];
            }
            catch (Exception e) when (e is BadImageFormatException or ArgumentException or InvalidOperationException) { }
            list.Add(new PatchAttr(type, method, methodType));
        }
        return list;
    }

    static PatchAttr Merge(IEnumerable<PatchAttr> attrs)
    {
        string? type = null, method = null;
        int methodType = 0;
        foreach (var a in attrs)
        {
            type = a.Type ?? type;
            method = a.Method ?? method;
            if (a.MethodType != 0) methodType = a.MethodType;
        }
        method = methodType switch
        {
            1 when method != null => "get_" + method,
            2 when method != null => "set_" + method,
            3 => ".ctor",
            4 => ".cctor",
            _ => method,
        };
        return new PatchAttr(type, method, methodType);
    }

    void AttributePatches(TypeDefinitionHandle h)
    {
        var td = md.GetTypeDefinition(h);
        var name = Names.FullName(md, h);
        var onClass = HarmonyAttrs(td.GetCustomAttributes());
        var onMethods = new List<(MethodDefinitionHandle mh, List<PatchAttr> attrs)>();
        foreach (var mh in td.GetMethods())
        {
            var attrs = HarmonyAttrs(md.GetMethodDefinition(mh).GetCustomAttributes());
            if (attrs.Count > 0) onMethods.Add((mh, attrs));
        }
        if (onClass.Count == 0 && onMethods.Count == 0) return;
        var targets = onMethods.Count > 0
            ? onMethods.Select(x => Merge(onClass.Concat(x.attrs))).ToList()
            : [Merge(onClass)];
        foreach (var t in targets)
        {
            if (t.Type != null) { facts.Patches.Add(new PatchTarget(t.Type, t.Method, "attribute", name, helper)); continue; }
            // [HarmonyPatch] with a TargetMethod(s) that finds the target itself.
            var found = false;
            foreach (var mh in td.GetMethods())
            {
                var mname = md.GetString(md.GetMethodDefinition(mh).Name);
                if (mname is not ("TargetMethod" or "TargetMethods") || !lookups.TryGetValue(mh, out var pairs)) continue;
                foreach (var (type, method) in pairs) facts.Patches.Add(new PatchTarget(type, method, "target-method", name, helper));
                found = true;
            }
            if (!found) facts.UnresolvedPatchCalls++;
        }
    }
}
