using OniMoono.Assets.Files;
using OniMoono.Assets.Shaders;
using OniMoono.Assets.TypeTrees;

namespace ShaderSurvey;

/// `bundles WSDIR`: every UnityFS file under each mod folder (WSDIR/&lt;id&gt;/…) that holds
/// Shader objects, one tab-separated line per shader: mod id, bundle path, shader name, the
/// shader platforms it carries programs for, whether its Vulkan programs read as U60's
/// (VulkanProgram.Read on each pass's first Vulkan vertex variant, as the Vulkan device's route
/// choice does) and the bundle's Unity version. Lines starting with `#` report files that could
/// not be read.
static class BundleSurvey
{
    public static int Run(string wsDir, TextWriter o)
    {
        o.WriteLine("mod\tbundle\tshader\tplatforms\tvulkan\tunity");
        foreach (var modDir in Directory.EnumerateDirectories(wsDir).Order(StringComparer.Ordinal))
        {
            string mod = Path.GetFileName(modDir);
            foreach (var path in Directory.EnumerateFiles(modDir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                if (!IsUnityFs(path)) continue;
                string bundle = Path.GetRelativePath(modDir, path);
                try
                {
                    var archive = UnityFsArchive.Open(path);
                    foreach (var node in archive.Nodes)
                    {
                        if (node.Path.EndsWith(".resS", StringComparison.Ordinal) || node.Path.EndsWith(".resource", StringComparison.Ordinal)) continue;
                        SerializedFile file;
                        try
                        {
                            file = SerializedFile.Parse(node.Path, archive.Read(node));
                        }
                        catch (Exception e) when (e is InvalidDataException or NotSupportedException or ArgumentException)
                        {
                            continue; // not a serialized file
                        }
                        foreach (var info in file.Objects)
                        {
                            var type = file.TypeOf(info);
                            if (type.ClassId != 48) continue;
                            var (f, i, t) = (file, info, type);
                            o.WriteLine(Describe(mod, bundle, () => t.Tree != null ? TypeTreeReader.ReadObject(t.Tree, f.OpenObject(i), TypeTreeDatabase.TrailingBytes(48)) : TypeTreeReader.ReadNative(48, f.OpenObject(i)),
                                archive.UnityRevision.Length > 0 ? archive.UnityRevision : file.UnityVersion));
                        }
                    }
                }
                catch (Exception e)
                {
                    o.WriteLine($"# {mod}\t{bundle}\tnot read: {e.GetType().Name}: {e.Message}");
                }
            }
        }
        return 0;
    }

    static bool IsUnityFs(string path)
    {
        try
        {
            using var f = File.OpenRead(path);
            Span<byte> head = stackalloc byte[8];
            return f.Read(head) == 8 && head.SequenceEqual("UnityFS\0"u8);
        }
        catch (IOException)
        {
            return false;
        }
    }

    static string Describe(string mod, string bundle, Func<Fields> read, string unity)
    {
        string name = "?", platforms = "?", vulkan = "-";
        try
        {
            var shader = read();
            var parsed = shader.Contains("m_ParsedForm") ? shader.Struct("m_ParsedForm") : null;
            name = parsed?.String("m_Name") ?? (shader.Contains("m_Name") ? shader.String("m_Name") : "?");
            var list = shader.Contains("platforms") ? shader.Get<uint[]>("platforms") : [];
            platforms = list.Length == 0 ? "none" : string.Join(",", list);
            if (list.Contains(ShaderCompilerPlatform.Vulkan)) vulkan = VulkanReads(shader, parsed);
        }
        catch (Exception e)
        {
            platforms = $"not read ({e.GetType().Name}: {e.Message})";
        }
        return $"{mod}\t{bundle}\t{name}\t{platforms}\t{vulkan}\t{unity}";
    }

    static string VulkanReads(Fields shader, Fields? parsed)
    {
        try
        {
            if (parsed == null) return "no parsed form";
            var table = ShaderProgramTable.Load(shader, ShaderCompilerPlatform.Vulkan);
            if (table == null) return "no table";
            var keywords = parsed.Contains("m_KeywordNames") ? parsed.Array("m_KeywordNames").Cast<string>().ToArray() : [];
            int read = 0;
            foreach (var sub in parsed.Array("m_SubShaders").Cast<Fields>())
                foreach (var pass in sub.Array("m_Passes").Cast<Fields>())
                {
                    var first = PlayerSubprograms.Of(pass, "progVertex", keywords).Where(x => x.Platform == ShaderCompilerPlatform.Vulkan).Select(x => (int?)x.BlobIndex).FirstOrDefault();
                    if (first == null) continue;
                    VulkanProgram.Read(table.Get(first.Value));
                    read++;
                }
            return read > 0 ? "reads" : "no Vulkan variants";
        }
        catch (Exception e)
        {
            return $"not read ({e.GetType().Name}: {e.Message.Split('\n')[0]})";
        }
    }
}
