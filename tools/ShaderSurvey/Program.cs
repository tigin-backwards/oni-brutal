// Surveys the compiled shader programs of a Unity player data folder, for every graphics API the
// build carries (docs/research/macos-feasibility.md §1):
//   summary DATADIR             each shader's platforms (ShaderCompilerPlatform: entries/bytes) and
//                               player subprograms by GPU program type, then totals
//   variants DATADIR            one line per player subprogram: shader, subshader/pass, stage,
//                               GPU program type, keywords, program hash and length, parameter blob
//   dump DATADIR NAME|* DIR PLATFORM
//                               every program and parameter blob of PLATFORM (14 Metal, 15 GLCore)
//                               as files, with map.txt naming each subprogram's blobs
//   check-metal DATADIR         Metal reflection against the MSL: every texture, sampler and
//                               constant buffer binding the parameter blob and the pass's common
//                               parameters give must be the slot the MSL declares, and every MSL
//                               slot must be accounted for
// It reads the shipped release type trees (OniBrutal.Assets), so it needs no engine or device.
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OniBrutal.Assets.Compression;
using OniBrutal.Assets.Files;
using OniBrutal.Assets.TypeTrees;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: ShaderSurvey summary|variants|check-metal DATADIR | ShaderSurvey dump DATADIR NAME|* DIR PLATFORM");
    return 2;
}
string mode = args[0], dataDir = args[1];
string[] files = ["level0", "level1", "level2", "resources.assets", "sharedassets0.assets", "sharedassets1.assets", "sharedassets2.assets",
    "Resources/unity default resources", "Resources/unity_builtin_extra"];
string[] stages = ["progVertex", "progFragment", "progGeometry", "progHull", "progDomain"];

switch (mode)
{
    case "summary": Summary(); return 0;
    case "variants": Variants(); return 0;
    case "dump": Dump(args[2], args[3], uint.Parse(args[4])); return 0;
    case "check-metal": return CheckMetal();
    default: Console.Error.WriteLine("unknown mode " + mode); return 2;
}

IEnumerable<(string File, Fields Shader)> Shaders()
{
    foreach (var name in files)
    {
        var path = Path.Combine(dataDir, name);
        if (!File.Exists(path)) continue;
        var file = SerializedFile.Open(path);
        foreach (var info in file.Objects)
        {
            var type = file.TypeOf(info);
            if (type.ClassId != 48) continue;
            var reader = file.OpenObject(info);
            yield return (name, type.Tree != null ? TypeTreeReader.ReadObject(type.Tree, reader, TypeTreeDatabase.TrailingBytes(48)) : TypeTreeReader.ReadNative(48, reader));
        }
    }
}

static string NameOf(Fields shader) => shader.Struct("m_ParsedForm").String("m_Name");

// One platform's programs: LZ4 segments whose first holds the (offset, length, segment) table.
static Blobs Platform(Fields shader, uint platform)
{
    int p = Array.IndexOf(shader.Get<uint[]>("platforms"), platform);
    if (p < 0) return new Blobs([], []);
    var blob = shader.Get<byte[]>("compressedBlob");
    var offsets = (uint[])shader.Array("offsets")[p]!;
    var compressed = (uint[])shader.Array("compressedLengths")[p]!;
    var decompressed = (uint[])shader.Array("decompressedLengths")[p]!;
    var segments = new byte[offsets.Length][];
    for (int s = 0; s < segments.Length; s++)
    {
        segments[s] = new byte[decompressed[s]];
        Lz4.Decode(blob.AsSpan((int)offsets[s], (int)compressed[s]), segments[s]);
    }
    var table = segments[0];
    var entries = new (int, int, int)[BitConverter.ToInt32(table, 0)];
    for (int i = 0; i < entries.Length; i++)
        entries[i] = (BitConverter.ToInt32(table, 4 + 12 * i), BitConverter.ToInt32(table, 8 + 12 * i), BitConverter.ToInt32(table, 12 + 12 * i));
    return new Blobs(segments, entries);
}

// Every player subprogram of every pass, with the keywords it was compiled for.
IEnumerable<Sub> Subprograms(Fields shader)
{
    var parsed = shader.Struct("m_ParsedForm");
    var keywords = parsed.Contains("m_KeywordNames") ? parsed.Array("m_KeywordNames").Cast<string>().ToArray() : [];
    int s = 0;
    foreach (var sub in parsed.Array("m_SubShaders").Cast<Fields>())
    {
        int p = 0;
        foreach (var pass in sub.Array("m_Passes").Cast<Fields>())
        {
            foreach (var stage in stages)
            {
                if (!pass.Contains(stage) || !pass.Struct(stage).Contains("m_PlayerSubPrograms")) continue;
                var program = pass.Struct(stage);
                var lists = program.Array("m_PlayerSubPrograms");
                var parameterLists = program.Array("m_ParameterBlobIndices");
                for (int l = 0; l < lists.Length; l++)
                {
                    var list = (object?[])lists[l]!;
                    var parameterBlobs = l < parameterLists.Length ? (uint[])parameterLists[l]! : [];
                    for (int k = 0; k < list.Length; k++)
                    {
                        var sp = (Fields)list[k]!;
                        var indices = sp.Get<ushort[]>("m_KeywordIndices") ?? [];
                        yield return new Sub($"s{s}p{p}", stage, pass, program, sp.Get<sbyte>("m_GpuProgramType"), (int)sp.Get<uint>("m_BlobIndex"),
                            k < parameterBlobs.Length ? (int)parameterBlobs[k] : -1, string.Join("+", indices.Select(i => keywords[i]).Order(StringComparer.Ordinal)));
                    }
                }
            }
            p++;
        }
        s++;
    }
}

// ShaderCompilerPlatform of a GpuProgramType: GLCore32/41/43 are OpenGLCore, Metal VS/FS/CS are Metal.
static uint PlatformOf(int gpuProgramType) => gpuProgramType switch { 6 or 7 or 8 => 15u, 23 or 24 or 25 => 14u, _ => 0u };

void Summary()
{
    var totals = new SortedDictionary<string, int>(StringComparer.Ordinal);
    int count = 0;
    foreach (var (file, shader) in Shaders())
    {
        count++;
        var platforms = shader.Get<uint[]>("platforms");
        var info = platforms.Select(p => { var b = Platform(shader, p); return $"{p}:{b.Entries.Length}e/{b.Segments.Sum(x => (long)x.Length)}B"; });
        var types = Subprograms(shader).GroupBy(x => x.Type).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}").ToArray();
        Console.WriteLine($"{file}\t{NameOf(shader)}\tplatforms={string.Join(" ", info)}\tsubprograms={string.Join(" ", types)}");
        foreach (var p in platforms) totals[$"platform{p}"] = totals.GetValueOrDefault($"platform{p}") + 1;
        foreach (var x in Subprograms(shader)) totals[$"type{x.Type}"] = totals.GetValueOrDefault($"type{x.Type}") + 1;
    }
    Console.WriteLine($"total shaders={count} " + string.Join(" ", totals.Select(kv => kv.Key + "=" + kv.Value)));
}

void Variants()
{
    foreach (var (_, shader) in Shaders())
    {
        var platforms = new Dictionary<uint, Blobs>();
        foreach (var x in Subprograms(shader))
        {
            uint platform = PlatformOf(x.Type);
            if (!platforms.TryGetValue(platform, out var blobs)) platforms[platform] = blobs = Platform(shader, platform);
            string hash = "-";
            int length = 0;
            if (x.Blob < blobs.Entries.Length)
            {
                var bytes = blobs.Get(x.Blob);
                hash = Convert.ToHexString(SHA1.HashData(bytes))[..12];
                length = bytes.Length;
            }
            Console.WriteLine($"{NameOf(shader)}\t{x.Id}\t{x.Stage}\t{x.Type}\t{x.Keywords}\t{hash}\t{length}\t{x.ParameterBlob}");
        }
    }
}

void Dump(string name, string outDir, uint platform)
{
    foreach (var (_, shader) in Shaders())
    {
        if (name != "*" && NameOf(shader) != name) continue;
        var blobs = Platform(shader, platform);
        if (blobs.Entries.Length == 0) continue;
        var dir = Path.Combine(outDir, string.Join("_", NameOf(shader).Split(Path.GetInvalidFileNameChars().Append('/').ToArray())));
        Directory.CreateDirectory(dir);
        for (int i = 0; i < blobs.Entries.Length; i++) File.WriteAllBytes(Path.Combine(dir, $"blob{i}.bin"), blobs.Get(i).ToArray());
        File.WriteAllLines(Path.Combine(dir, "map.txt"), Subprograms(shader).Where(x => PlatformOf(x.Type) == platform)
            .Select(x => $"{x.Id} {x.Stage} type={x.Type} blob={x.Blob} parameters={x.ParameterBlob} keywords={x.Keywords}"));
        Console.WriteLine($"{NameOf(shader)}: {blobs.Entries.Length} blobs -> {dir}");
    }
}

int CheckMetal()
{
    int programs = 0, failures = 0;
    var slot = new Regex(@"(?:texture\w*<[^>]*>|depth\w*<[^>]*>|sampler|constant \w+&|(?:const )?device \w+\s*\*)\s*(\w+)\s*\[\[\s*(texture|sampler|buffer)\s*\((\d+)\)\s*\]\]");
    foreach (var (_, shader) in Shaders())
    {
        var blobs = Platform(shader, 14);
        if (blobs.Entries.Length == 0) continue;
        foreach (var x in Subprograms(shader).Where(x => PlatformOf(x.Type) == 14))
        {
            programs++;
            if (x.Blob >= blobs.Entries.Length || x.ParameterBlob >= blobs.Entries.Length)
            {
                failures++;
                Console.WriteLine($"{NameOf(shader)} {x.Id} {x.Stage} [{x.Keywords}]: blob {x.Blob} or {x.ParameterBlob} not among the platform's {blobs.Entries.Length}");
                continue;
            }
            var source = Encoding.ASCII.GetString(blobs.Get(x.Blob));
            var declared = new Dictionary<(string Kind, int Slot), string>();
            foreach (Match m in slot.Matches(source)) declared[(m.Groups[2].Value, int.Parse(m.Groups[3].Value))] = m.Groups[1].Value;
            var expected = new Dictionary<(string Kind, int Slot), string>();
            var names = new Dictionary<int, string>();
            foreach (var pair in x.Pass.Array("m_NameIndices").Cast<Fields>()) names[Convert.ToInt32(pair["second"])] = pair.String("first");
            // The pass stage's common parameters (shared by its variants), then the variant's own.
            var common = x.Program.Struct("m_CommonParameters");
            foreach (var t in common.Array("m_TextureParams").Cast<Fields>()) AddTexture(expected, names[t.Int("m_NameIndex")], t.Int("m_Index"), t.Int("m_SamplerIndex"));
            foreach (var b in common.Array("m_ConstantBufferBindings").Cast<Fields>()) expected[("buffer", b.Int("m_Index"))] = names[b.Int("m_NameIndex")];
            string? error = null;
            if (x.ParameterBlob >= 0)
                try { ParameterBlob.Read(blobs.Get(x.ParameterBlob), expected); }
                catch (InvalidDataException e) { error = e.Message; }
            foreach (var (key, value) in expected)
                if (key.Kind == "sampler") { if (!declared.TryGetValue(key, out var d) || !d.StartsWith("sampler", StringComparison.Ordinal)) error ??= $"no sampler({key.Slot}) for {value}"; }
                else if (!declared.TryGetValue(key, out var d) || d != value) error ??= $"{key.Kind}({key.Slot}) is {(d ?? "undeclared")} in the MSL, {value} in the parameters";
            foreach (var (key, value) in declared)
                if (!expected.ContainsKey(key)) error ??= $"MSL {key.Kind}({key.Slot}) {value} has no parameter";
            if (error == null) continue;
            failures++;
            Console.WriteLine($"{NameOf(shader)} {x.Id} {x.Stage} [{x.Keywords}]: {error}");
        }
    }
    Console.WriteLine($"{programs} Metal subprograms checked, {failures} inconsistent");
    return failures == 0 ? 0 : 1;
}

static void AddTexture(Dictionary<(string, int), string> expected, string name, int index, int sampler)
{
    expected[("texture", index)] = name;
    // A negative sampler index: the texture is only read (texture.read), no sampler.
    if (sampler >= 0) expected[("sampler", sampler)] = name;
}

readonly record struct Blobs(byte[][] Segments, (int Offset, int Length, int Segment)[] Entries)
{
    public ReadOnlySpan<byte> Get(int i) => Segments[Entries[i].Segment].AsSpan(Entries[i].Offset, Entries[i].Length);
}

sealed record Sub(string Id, string Stage, Fields Pass, Fields Program, int Type, int Blob, int ParameterBlob, string Keywords);

// A variant's parameter blob, as read here (its layout inferred from the data, all GLCore and
// Metal blobs of the game parse to their end): magic 0x0C0A75BA; a constant-buffer count, the
// first an unnamed root; each buffer its name, size, members (name and six ints: type, rows,
// columns, matrix flag, array size, offset) and a struct count (always 0 here); then a count of
// bindings, each a name and a kind: 0 texture (index, sampler index, dimension), 1 constant buffer
// (index, array size), 2 structured buffer (index, and an int, 1 in every blob here).
static class ParameterBlob
{
    public static void Read(ReadOnlySpan<byte> blob, Dictionary<(string, int), string> bindings)
    {
        int pos = 0;
        int Int(ReadOnlySpan<byte> b) { if (pos + 4 > b.Length) throw new InvalidDataException("parameter blob ends early"); int v = BitConverter.ToInt32(b[pos..]); pos += 4; return v; }
        string Str(ReadOnlySpan<byte> b) { int n = Int(b); if (n < 0 || pos + n > b.Length) throw new InvalidDataException("bad string"); var s = Encoding.ASCII.GetString(b.Slice(pos, n)); pos += (n + 3) & ~3; return s; }
        if ((uint)Int(blob) != 0x0C0A75BA) throw new InvalidDataException("not a parameter blob");
        int buffers = Int(blob);
        for (int c = 0; c < buffers; c++)
        {
            Str(blob); Int(blob);
            int members = Int(blob);
            for (int m = 0; m < members; m++) { Str(blob); for (int k = 0; k < 6; k++) Int(blob); }
            if (Int(blob) != 0) throw new InvalidDataException("struct members");
        }
        int count = Int(blob);
        for (int i = 0; i < count; i++)
        {
            string name = Str(blob);
            switch (Int(blob))
            {
                case 0: { int index = Int(blob), sampler = Int(blob); Int(blob); AddTexture(bindings, name, index, sampler); break; }
                case 1 or 2: { bindings[("buffer", Int(blob))] = name; Int(blob); break; }
                case var kind: throw new InvalidDataException($"binding kind {kind} for {name}");
            }
        }
        if (pos != blob.Length) throw new InvalidDataException($"{blob.Length - pos} trailing bytes");
    }

    static void AddTexture(Dictionary<(string, int), string> expected, string name, int index, int sampler)
    {
        expected[("texture", index)] = name;
        if (sampler >= 0) expected[("sampler", sampler)] = name;
    }
}
