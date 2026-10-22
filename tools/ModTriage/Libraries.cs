// Merged libraries are not the mod's own code. PLib is merged (ILRepack) into many mods, sometimes
// renamed into the mod's namespace, so it is recognised by its type names, not its namespace.
// Harmony and Newtonsoft are recognised the same way. UtilLibs (Sgt_Imalas) is deliberately not a
// library here: it is those mods' UI. It and other authors' shared code are "helpers" instead
// (IsSharedHelper): scanned, but kept apart from the mod's own code.
namespace ModTriage;

public enum Library { None, PLib, Harmony, Newtonsoft }

public sealed class Fingerprints
{
    /// PLib's top-level type names, each with its area (the namespace after "PeterHan.PLib.":
    /// "UI", "Options", "Actions", "Core", …).
    public IReadOnlyDictionary<string, string> PLib { get; }

    static readonly HashSet<string> HarmonyNames = new(StringComparer.Ordinal)
    {
        "Harmony", "HarmonyInstance", "HarmonyPatch", "HarmonyMethod", "HarmonyPrefix", "HarmonyPostfix",
        "HarmonyTranspiler", "HarmonyPriority", "AccessTools", "Traverse", "CodeInstruction", "CodeMatcher",
        "CodeMatch", "PatchProcessor", "Transpilers", "MethodType", "ArgumentType", "HarmonyAttribute",
        "PatchClassProcessor", "Patches", "PatchInfo", "HarmonyException", "FileLog", "SymbolExtensions",
    };

    static readonly HashSet<string> NewtonsoftNames = new(StringComparer.Ordinal)
    {
        "JsonConvert", "JsonSerializer", "JsonSerializerSettings", "JsonReader", "JsonWriter", "JsonTextReader",
        "JsonTextWriter", "JsonPropertyAttribute", "JsonIgnoreAttribute", "JsonObjectAttribute", "JsonConverter",
        "JsonConverterAttribute", "JsonToken", "Formatting", "JToken", "JObject", "JArray", "JValue", "JProperty",
    };

    /// The PLib names that only PLib has; when this many sit in a namespace that is mostly the
    /// mod's own, PLib was merged into the mod's namespace and those types are removed one by one.
    const int FlattenedHits = 4;

    /// Namespaces that several mods define, each with the median number of top-level types the
    /// mods put in it: an author's shared code copied into each of their mods.
    public IReadOnlyDictionary<string, int> Shared { get; }

    public Fingerprints(IReadOnlyDictionary<string, string> plib, IReadOnlyDictionary<string, int>? shared = null)
    {
        PLib = plib;
        Shared = shared ?? new Dictionary<string, int>();
    }

    public static Fingerprints Default { get; } = Load();

    static Fingerprints Load()
    {
        var plib = Parse(Resource("plib-types.txt"));
        var shared = Parse(Resource("shared-namespaces.txt")).ToDictionary(kv => kv.Key, kv => int.Parse(kv.Value), StringComparer.Ordinal);
        return new Fingerprints(plib, shared);
    }

    static string Resource(string name)
    {
        using var stream = typeof(Fingerprints).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(name + " is not embedded");
        return new StreamReader(stream).ReadToEnd();
    }

    /// Lines of "value<TAB>key" (area and type name; median and namespace); '#' starts a comment.
    public static Dictionary<string, string> Parse(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Trim().Length == 0 || line[0] == '#') continue;
            var parts = line.Split('\t');
            if (parts.Length == 2) map.TryAdd(parts[1], parts[0]);
        }
        return map;
    }

    /// The library a namespace belongs to, from its name or its types' names. A whole namespace
    /// is a library's when it is named like one, or when at least two of its types and at least
    /// half of them carry the library's names.
    public Library OfNamespace(string ns, IReadOnlyCollection<string> simpleNames)
    {
        if (ns == "PeterHan.PLib" || ns.StartsWith("PeterHan.PLib.", StringComparison.Ordinal)) return Library.PLib;
        if (ns is "HarmonyLib" or "Harmony" || ns.StartsWith("HarmonyLib.", StringComparison.Ordinal)
            || ns.StartsWith("Harmony.", StringComparison.Ordinal) || ns.StartsWith("MonoMod", StringComparison.Ordinal)
            || ns.StartsWith("Mono.Cecil", StringComparison.Ordinal)) return Library.Harmony;
        if (ns == "Newtonsoft.Json" || ns.StartsWith("Newtonsoft.Json.", StringComparison.Ordinal)) return Library.Newtonsoft;
        if (simpleNames.Count == 0) return Library.None;
        foreach (var (lib, hits) in new[] {
            (Library.PLib, simpleNames.Count(PLib.ContainsKey)),
            (Library.Harmony, simpleNames.Count(HarmonyNames.Contains)),
            (Library.Newtonsoft, simpleNames.Count(NewtonsoftNames.Contains)) })
        {
            if (hits >= 2 && hits * 2 >= simpleNames.Count) return lib;
        }
        return Library.None;
    }

    /// For a namespace that is not a library's as a whole: the types that are PLib's anyway,
    /// because PLib was merged into it (a renamed ILRepack). Empty unless enough PLib names are
    /// there to be sure.
    public IEnumerable<string> FlattenedPLib(IReadOnlyCollection<string> simpleNames)
    {
        var hits = simpleNames.Where(PLib.ContainsKey).ToList();
        // Only PLib's own "PName" types count towards being sure; names like ExtensionMethods
        // or PriorityQueue are then removed with them.
        int distinctive = hits.Count(n => n.Length > 1 && n[0] == 'P' && char.IsUpper(n[1]));
        return distinctive >= FlattenedHits ? hits : [];
    }

    /// The PLib area of a type that PLib defines, or null.
    public string? PLibArea(TypeName t)
    {
        var ns = t.Namespace;
        if (ns.StartsWith("PeterHan.PLib.", StringComparison.Ordinal)) return ns["PeterHan.PLib.".Length..].Split('.')[0];
        if (ns == "PeterHan.PLib") return "Core";
        return t.Assembly is { } a && a.StartsWith("PLib", StringComparison.Ordinal) && PLib.TryGetValue(Names.OuterSimple(t.FullName), out var area) ? area : null;
    }

    /// Shared helper code an author merges into each of their mods: Sgt_Imalas' UtilLibs, Ony's
    /// common code, and any namespace in Shared, unless this mod holds well over the usual number
    /// of types there (it is then the library's home mod, as ONI Together is for its networking
    /// namespaces). Helpers stay in the scan: UtilLibs' FScreen, FButton and the like are those
    /// mods' UI, and their screens derive from them. But the helpers' own references and shared
    /// tweaks (patches on ModsScreen, MainMenu, WattsonMessage, … that every one of those mods
    /// applies) say nothing about the mod, so they are kept apart.
    public bool IsSharedHelper(string ns, int typesHere)
    {
        if (Under(ns, "UtilLibs") || ns == "ElementUtilNamespace" || ns is "Ony.OxygenNotIncluded" or "Ony.OxygenNotIncluded.Extensions"
            || Under(ns, "Ony.UnityUI") || Under(ns, "Ony.Unity.UI")) return true;
        return Shared.TryGetValue(ns, out var median) && typesHere <= Math.Max(2 * median, median + 3);
    }

    /// Namespaces a scan of many mods should not count as shared: the runtime's, the compiler's,
    /// and the global one (where small mods put everything).
    public static bool NeverShared(string ns) => ns.Length == 0 || ns == "Properties"
        || ns.StartsWith("System", StringComparison.Ordinal) || ns.StartsWith("Microsoft", StringComparison.Ordinal);

    static bool Under(string ns, string root) => ns == root || ns.StartsWith(root + ".", StringComparison.Ordinal);

    public static Library OfAssemblyName(string name) => name switch
    {
        "0Harmony" or "Harmony" or "HarmonyLib" => Library.Harmony,
        "Newtonsoft.Json" => Library.Newtonsoft,
        _ when name.StartsWith("PLib", StringComparison.Ordinal) => Library.PLib,
        _ when name.StartsWith("MonoMod", StringComparison.Ordinal) || name.StartsWith("Mono.Cecil", StringComparison.Ordinal) => Library.Harmony,
        _ => Library.None,
    };
}
