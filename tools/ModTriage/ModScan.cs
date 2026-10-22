// One mod folder: the version the game would load, its mod_info.yaml, its content folders, and
// the facts from each of its assemblies.
using System.Text.RegularExpressions;

namespace ModTriage;

public sealed record ModInfo(int? MinimumSupportedBuild, int? ApiVersion, string? Version, string? SupportedContent,
    List<string> RequiredDlcIds, List<string> ForbiddenDlcIds);

public sealed record Content(List<string> Dlls, int AnimFiles, List<string> Bundles, int Translations,
    bool Worldgen, bool Templates, bool Elements, bool Codex);

public sealed class ModFolder
{
    public required string Id { get; init; }
    public required string Root { get; init; }
    /// The folder the game loads: the root, or one of archived_versions/*.
    public required string Chosen { get; init; }
    public required string ChosenNote { get; init; }
    public string? Title { get; init; }
    public string? StaticId { get; init; }
    public required ModInfo Info { get; init; }
    public required Content Content { get; init; }
    public List<AssemblyFacts> Assemblies { get; } = [];

    public static ModFolder Scan(string id, string root, int? build, Fingerprints fp, GameIndex game)
    {
        var (chosen, note) = ChooseVersion(root, build);
        var modYaml = ReadText(Path.Combine(root, "mod.yaml")) ?? ReadText(Path.Combine(chosen, "mod.yaml")) ?? "";
        var folder = new ModFolder
        {
            Id = id,
            Root = root,
            Chosen = chosen,
            ChosenNote = note,
            Title = Yaml.Value(modYaml, "title"),
            StaticId = Yaml.Value(modYaml, "staticID"),
            Info = Yaml.Info(ReadText(Path.Combine(chosen, "mod_info.yaml")) ?? ""),
            Content = ScanContent(chosen),
        };
        foreach (var dll in folder.Content.Dlls)
        {
            try
            {
                if (AssemblyScan.Scan(Path.Combine(chosen, dll), fp, game) is { } facts) folder.Assemblies.Add(facts);
            }
            catch (BadImageFormatException) { }
        }
        return folder;
    }

    static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    /// As KMod picks: the root when its minimumSupportedBuild is at most the game's build, else
    /// the archived version with the highest minimumSupportedBuild that is.
    public static (string path, string note) ChooseVersion(string root, int? build)
    {
        if (build is not { } b) return (root, "root (game build unknown)");
        var rootInfo = Yaml.Info(ReadText(Path.Combine(root, "mod_info.yaml")) ?? "");
        if ((rootInfo.MinimumSupportedBuild ?? 0) <= b) return (root, "root");
        var archived = Path.Combine(root, "archived_versions");
        if (Directory.Exists(archived))
        {
            var best = Directory.GetDirectories(archived)
                .Select(d => (dir: d, info: Yaml.Info(ReadText(Path.Combine(d, "mod_info.yaml")) ?? "")))
                .Where(x => (x.info.MinimumSupportedBuild ?? 0) <= b)
                .OrderByDescending(x => x.info.MinimumSupportedBuild ?? 0)
                .FirstOrDefault();
            if (best.dir != null) return (best.dir, "archived_versions/" + Path.GetFileName(best.dir));
        }
        return (root, $"root (needs build {rootInfo.MinimumSupportedBuild}, no archived version fits)");
    }

    static readonly string[] ImageOrText = [".png", ".jpg", ".jpeg", ".txt", ".json", ".yaml", ".md", ".po", ".pot", ".dds", ".psd"];

    static Content ScanContent(string dir)
    {
        // The game loads every DLL at the top of the folder.
        var dlls = Directory.GetFiles(dir, "*.dll").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToList();
        int anim = Directory.Exists(Path.Combine(dir, "anim"))
            ? Directory.EnumerateFiles(Path.Combine(dir, "anim"), "*", SearchOption.AllDirectories).Count(f => f.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            : 0;
        var bundles = new List<string>();
        var assets = Path.Combine(dir, "assets");
        if (Directory.Exists(assets))
        {
            foreach (var f in Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories))
            {
                if (ImageOrText.Contains(Path.GetExtension(f).ToLowerInvariant())) continue;
                if (IsUnityBundle(f)) bundles.Add(Path.GetRelativePath(dir, f).Replace('\\', '/'));
            }
        }
        bundles.Sort(StringComparer.Ordinal);
        var translations = Directory.Exists(Path.Combine(dir, "translations"))
            ? Directory.GetFiles(Path.Combine(dir, "translations"), "*.po").Length
            : Directory.GetFiles(dir, "*.po").Length;
        return new Content(dlls, anim, bundles, translations,
            Directory.Exists(Path.Combine(dir, "worldgen")), Directory.Exists(Path.Combine(dir, "templates")),
            Directory.Exists(Path.Combine(dir, "elements")), Directory.Exists(Path.Combine(dir, "codex")));
    }

    static bool IsUnityBundle(string path)
    {
        try
        {
            using var f = File.OpenRead(path);
            var head = new byte[8];
            int n = f.Read(head, 0, 8);
            var s = System.Text.Encoding.ASCII.GetString(head, 0, n);
            return s.StartsWith("UnityFS", StringComparison.Ordinal) || s.StartsWith("UnityWeb", StringComparison.Ordinal) || s.StartsWith("UnityRaw", StringComparison.Ordinal);
        }
        catch (IOException) { return false; }
    }
}

/// The few keys triage needs, read with regular expressions like scripts/ws-mods.py does; the
/// files are flat, and a full YAML parser would add a dependency for nothing.
public static class Yaml
{
    public static string? Value(string text, string key)
    {
        var m = Regex.Match(text, $@"^{Regex.Escape(key)}:[ \t]*[""']?(.*?)[""']?[ \t]*\r?$", RegexOptions.Multiline);
        return m.Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : null;
    }

    /// A list given inline ("[A, B]") or as "- item" lines under the key.
    public static List<string> List(string text, string key)
    {
        var m = Regex.Match(text, $@"^{Regex.Escape(key)}:[ \t]*(.*?)\r?$((?:\r?\n[ \t]*-[ \t]*.*)*)", RegexOptions.Multiline);
        if (!m.Success) return [];
        var inline = m.Groups[1].Value.Trim();
        if (inline.Length > 0)
            return inline.Trim('[', ']').Split(',').Select(s => s.Trim().Trim('"', '\'')).Where(s => s.Length > 0).ToList();
        return Regex.Matches(m.Groups[2].Value, @"-[ \t]*(.*)").Select(x => x.Groups[1].Value.Trim().Trim('"', '\'')).Where(s => s.Length > 0).ToList();
    }

    static int? Int(string text, string key) => int.TryParse(Value(text, key), out var v) ? v : null;

    public static ModInfo Info(string text) => new(Int(text, "minimumSupportedBuild"), Int(text, "APIVersion"),
        Value(text, "version"), Value(text, "supportedContent"), List(text, "requiredDlcIds"), List(text, "forbiddenDlcIds"));
}
