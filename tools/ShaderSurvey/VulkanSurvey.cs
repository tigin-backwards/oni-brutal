using System.Text;
using OniMoono.Assets.Shaders;

namespace ShaderSurvey;

/// `vulkan DATADIR NAME|*`: each Vulkan (platform 18) program's entry header, its stages decoded
/// from SMOL-V, and what each stage's SPIR-V declares (execution model, interface variables with
/// their names, storage classes, locations, descriptor sets and bindings).
static class VulkanSurvey
{
    public static int Run(string dataDir, string name, TextWriter o)
    {
        int programs = 0, failures = 0;
        foreach (var (_, shader) in Data.Shaders(dataDir))
        {
            if (name != "*" && Data.NameOf(shader) != name) continue;
            var blobs = Data.Table(shader, ShaderCompilerPlatform.Vulkan);
            if (blobs.Count == 0) continue;
            var seen = new HashSet<int>();
            foreach (var x in Data.Subprograms(shader).Where(x => x.Sub.Platform == ShaderCompilerPlatform.Vulkan))
            {
                if (!seen.Add(x.Sub.BlobIndex)) continue;
                programs++;
                var entry = blobs.Table!.Get(x.Sub.BlobIndex);
                o.WriteLine($"{Data.NameOf(shader)} {x.Id} blob {x.Sub.BlobIndex} [{Data.KeywordKey(x.Sub.Keywords)}] {entry.Length} bytes");
                try
                {
                    Describe(entry, o);
                }
                catch (Exception e)
                {
                    failures++;
                    o.WriteLine($"  FAILED: {e.Message}");
                }
            }
        }
        o.WriteLine($"{programs} programs, {failures} failures");
        return failures == 0 ? 0 : 1;
    }

    static void Describe(ReadOnlySpan<byte> entry, TextWriter o)
    {
        var words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(entry[..(entry.Length / 4 * 4)]);
        o.WriteLine("  header " + string.Join(" ", words[..28].ToArray().Select(w => w.ToString("x"))));
        var program = VulkanProgram.Read(entry);
        o.WriteLine($"  flags {program.Flags:x}, channels {program.ChannelMask:x}, inputs {string.Join(" ", program.Inputs.Select(i => $"{i.Channel}->{i.Component}"))}");
        foreach (var stage in program.Stages)
        {
            var spirv = stage.Spirv;
            o.WriteLine($"  stage {stage.Index} at {stage.Offset:x} size {stage.Size:x}: {spirv.Length} bytes SPIR-V, validator: {SpirvValidator.Validate(spirv) ?? "valid"}");
            foreach (var line in SpirvSummary(spirv)) o.WriteLine("    " + line);
            if (Environment.GetEnvironmentVariable("SURVEY_DISASSEMBLE") == "1") o.WriteLine(SpirvValidator.Disassemble(spirv));
        }
    }

    /// Entry points, and interface variables with their decorations.
    public static IEnumerable<string> SpirvSummary(byte[] spirv)
    {
        var w = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(spirv);
        var names = new Dictionary<uint, string>();
        var decorations = new Dictionary<uint, List<string>>();
        var lines = new List<string>();
        var variables = new List<(uint Id, uint Storage)>();
        for (int i = 5; i < w.Length;)
        {
            int count = (int)(w[i] >> 16), op = (int)(w[i] & 0xFFFF);
            var a = w.Slice(i + 1, count - 1);
            switch (op)
            {
                case 15: lines.Add($"entry point model {a[0]} '{Str(a[2..])}'"); break;
                case 16: lines.Add($"execution mode {string.Join(" ", a.ToArray())}"); break;
                case 5: names[a[0]] = Str(a[1..]); break;
                case 71:
                {
                    string d = a[1] switch { 30 => $"Location {a[2]}", 33 => $"Binding {a[2]}", 34 => $"Set {a[2]}", 11 => $"BuiltIn {a[2]}", 2 => "Block", 3 => "BufferBlock", var k => $"dec{k}" + (a.Length > 2 ? " " + a[2] : "") };
                    (decorations.TryGetValue(a[0], out var l) ? l : decorations[a[0]] = new()).Add(d);
                    break;
                }
                case 59: variables.Add((a[1], a[2])); break;
            }
            i += count;
        }
        foreach (var (id, storage) in variables)
        {
            if (storage == 7) continue; // Function
            string storageName = storage switch { 0 => "UniformConstant", 1 => "Input", 2 => "Uniform", 3 => "Output", 6 => "Private", 9 => "PushConstant", 12 => "StorageBuffer", var s => "storage" + s };
            lines.Add($"var {id} {storageName} '{names.GetValueOrDefault(id, "")}' {string.Join(", ", decorations.GetValueOrDefault(id) ?? [])}");
        }
        return lines;
    }

    static string Str(ReadOnlySpan<uint> words)
    {
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(words);
        int end = bytes.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);
    }
}
