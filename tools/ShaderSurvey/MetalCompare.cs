using System.Text;
using OniMoono.Assets.Shaders;
using OniMoono.Assets.TypeTrees;

namespace ShaderSurvey;

/// `compare-metal`: every Metal program's parameters against what the GLCore program of the same
/// pass and keywords exposes. GLCore is the oracle: the GL device runs it today.
///
/// For each GLCore variant of a pass, the Metal vertex and fragment programs are those Unity's
/// rule selects per stage for the variant's keywords (most matching keywords, then fewest extra,
/// then the first). Each Metal stage is compared with the GLSL stage section's declarations (what
/// GL reflection reports: names, types, array sizes, uniform blocks, samplers, storage blocks) and,
/// for the union of both stages, with the GLCore parameter blob. Every difference is put in a class;
/// classes this file explains are expected (docs/research/metal-backend-design.md §2 lists them),
/// any other fails the check.
static class MetalCompare
{
    sealed class Report
    {
        public readonly SortedDictionary<string, List<string>> Classes = new(StringComparer.Ordinal);
        /// Put before the classes of differences found while it is set ("fallback: " while
        /// comparing a GLCore variant with Metal programs selected for other keywords).
        public string Prefix = "";
        public void Add(string cls, string detail)
        {
            cls = Prefix + cls;
            if (!Classes.TryGetValue(cls, out var list)) Classes[cls] = list = new();
            list.Add(detail);
        }

        /// Comparisons that found no difference, by what was compared.
        public readonly SortedDictionary<string, int> Agreements = new(StringComparer.Ordinal);
        public void Agree(string what) => Agreements[Prefix + what] = Agreements.GetValueOrDefault(Prefix + what) + 1;
    }

    /// Classes of difference that follow from how the two platforms' programs are built.
    static readonly Dictionary<string, string> explained = new(StringComparer.Ordinal)
    {
        // GLCore variants whose keywords no pair of Metal programs has (Klei's DISABLE_K_LIGHTING,
        // UNITY_UI_ALPHACLIP and DISABLE_TEMPERATURE_PARTICLES, GL multiview): Unity's selection
        // falls back to the nearest Metal programs; their differences are reported as fallback.
        ["keywords: GLCore variant without an exact Metal counterpart"] = "",
        // HLSLcc's GLCore output turns Unity's built-in cbuffers (UnityPerDraw, UnityPerFrame,
        // UnityPerCamera…) into default-block uniforms; Metal keeps them as buffers.
        ["constant: Unity built-in cbuffer on Metal, loose uniform on GLCore"] = "",
        // Metal reads the texture with texture.read and no sampler; GLSL uses texelFetch on its
        // combined sampler.
        ["texture: no sampler on Metal (read with texture.read)"] = "",
        // A shadow map sampled plainly and with comparison: GLSL declares a second sampler,
        // hlslcc_zcmp_X, for the comparison; Metal's depth2d X takes both kinds of sampler.
        ["texture: GLCore comparison sampler hlslcc_zcmp_X of Metal's depth texture X"] = "",
        // The GLCore blob merges the two stages' $Globals layouts by offset: where both stages
        // have a member at the same offset only one is recorded. GLSL declares both (the GL
        // device binds by reflection and never reads the blob).
        ["blob: GLCore parameter blob drops members whose offsets coincide across stages"] = "",
        // The GLCore blob's ShaderParamType for int is 5, Metal's 1 (both declare int).
        ["blob: GLCore parameter blob records int as type 5"] = "",
    };

    public static int Run(IEnumerable<(string File, Fields Shader)> shaders, TextWriter output, string? detail)
    {
        var report = new Report();
        int glVariants = 0, metalPrograms = 0, covered = 0, exact = 0, passes = 0;
        foreach (var (_, shader) in shaders)
        {
            var name = Data.NameOf(shader);
            var metal = Data.Table(shader, ShaderCompilerPlatform.Metal);
            var gl = Data.Table(shader, ShaderCompilerPlatform.OpenGLCore);
            if (metal.Count == 0 || gl.Count == 0) continue;
            var bySite = Data.Subprograms(shader).GroupBy(x => x.Id);
            foreach (var site in bySite)
            {
                var all = site.ToList();
                var glSubs = all.Where(x => x.Sub.Platform == ShaderCompilerPlatform.OpenGLCore && x.Sub.Stage == "progVertex").ToList();
                var vs = all.Where(x => x.Sub.Platform == ShaderCompilerPlatform.Metal && x.Sub.Stage == "progVertex").ToList();
                var fs = all.Where(x => x.Sub.Platform == ShaderCompilerPlatform.Metal && x.Sub.Stage == "progFragment").ToList();
                metalPrograms += vs.Count + fs.Count;
                if (vs.Count + fs.Count == 0) continue;
                passes++;
                var used = new HashSet<Subprogram>();
                foreach (var g in glSubs)
                {
                    glVariants++;
                    var enabled = new HashSet<string>(g.Sub.Keywords, StringComparer.Ordinal);
                    var v = Select(vs, enabled);
                    var f = Select(fs, enabled);
                    string where = $"{name} {site.Key} [{Data.KeywordKey(g.Sub.Keywords)}]";
                    if (v == null || f == null)
                    {
                        report.Add("keywords: no Metal program for a stage", where);
                        continue;
                    }
                    used.Add(v);
                    used.Add(f);
                    var union = new HashSet<string>(v.Sub.Keywords.Concat(f.Sub.Keywords), StringComparer.Ordinal);
                    bool exactKeywords = union.SetEquals(enabled);
                    if (exactKeywords) exact++;
                    else report.Add("keywords: GLCore variant without an exact Metal counterpart", $"{where}: Metal vertex [{Data.KeywordKey(v.Sub.Keywords)}], fragment [{Data.KeywordKey(f.Sub.Keywords)}]");
                    var glSource = GlSource(gl.Table!.Get(g.Sub.BlobIndex));
                    var glBlob = Data.Parameters(g, gl);
                    var vp = Data.Parameters(v, metal);
                    var fp = Data.Parameters(f, metal);
                    // Programs selected for other keywords differ as their keywords make them.
                    // GL_OVR_multiview variants (VR only; the GL device skips them without the
                    // extension) index unity_StereoMatrixVP by gl_ViewID; Metal has no such
                    // extension and its variant of the keyword reads unity_MatrixVP.
                    report.Prefix = !exactKeywords ? "fallback: " : enabled.Contains("STEREO_MULTIVIEW_ON") ? "multiview: " : "";
                    CompareStage(report, where + " vertex", vp, glSource["VERTEX"]);
                    CompareStage(report, where + " fragment", fp, glSource["FRAGMENT"]);
                    CompareBlob(report, where, vp, fp, glBlob, glSource);
                    report.Prefix = "";
                }
                covered += used.Count;
                foreach (var x in vs.Concat(fs))
                    if (!used.Contains(x)) report.Add("coverage: Metal program no GLCore variant selects", $"{name} {site.Key} {x.Sub.Stage} [{Data.KeywordKey(x.Sub.Keywords)}]");
            }
        }

        output.WriteLine($"{passes} passes, {glVariants} GLCore variants, {metalPrograms} Metal programs ({covered} selected by a GLCore variant), {exact} variants with exact Metal keywords");
        foreach (var (what, count) in report.Agreements) output.WriteLine($"{count,6}  agree        {what}");
        int unexplained = 0;
        foreach (var (cls, items) in report.Classes)
        {
            bool known = explained.ContainsKey(cls) || cls.StartsWith("fallback: ", StringComparison.Ordinal)
                || (cls.StartsWith("multiview: constant: ", StringComparison.Ordinal) && explained.ContainsKey(cls["multiview: ".Length..]))
                || cls is "multiview: constant: in the Metal blob only" or "multiview: constant: declared only by the GLCore stage";
            if (!known) unexplained += items.Count;
            output.WriteLine($"{items.Count,6}  {(known ? "explained  " : "UNEXPLAINED")}  {cls}");
            if (!known || detail == "all" || (detail != null && cls.Contains(detail, StringComparison.Ordinal)))
                foreach (var item in items) output.WriteLine("          " + item);
        }
        output.WriteLine(unexplained == 0 ? "every difference is explained" : $"{unexplained} unexplained differences");
        return unexplained == 0 ? 0 : 1;
    }

    /// Unity's rule over one stage's programs.
    static Subprogram? Select(List<Subprogram> programs, HashSet<string> enabled)
    {
        Subprogram? best = null;
        int bestMatches = -1, bestExtras = int.MaxValue;
        foreach (var x in programs)
        {
            int matches = 0, extras = 0;
            foreach (var k in x.Sub.Keywords)
                if (enabled.Contains(k)) matches++;
                else extras++;
            if (matches > bestMatches || (matches == bestMatches && extras < bestExtras))
            {
                best = x;
                bestMatches = matches;
                bestExtras = extras;
            }
        }
        return best;
    }

    /// A GLCore entry's GLSL, split into stage sections (as ShaderPrograms.Split does).
    static Dictionary<string, string> GlSource(ReadOnlySpan<byte> entry)
    {
        int pos = 24;
        int count = BitConverter.ToInt32(entry[pos..]);
        pos += 4;
        for (int i = 0; i < count; i++) pos += 4 + ((BitConverter.ToInt32(entry[pos..]) + 3) & ~3);
        int length = BitConverter.ToInt32(entry[pos..]);
        var source = Encoding.UTF8.GetString(entry.Slice(pos + 4, length));
        var starts = new List<(int Index, string Stage)>();
        foreach (var stage in new[] { "VERTEX", "FRAGMENT", "GEOMETRY", "HULL", "DOMAIN" })
        {
            int at = source.IndexOf("#ifdef " + stage + "\n", StringComparison.Ordinal);
            if (at >= 0) starts.Add((at, stage));
        }
        starts.Sort((a, b) => a.Index.CompareTo(b.Index));
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int k = 0; k < starts.Count; k++)
        {
            int start = starts[k].Index + ("#ifdef " + starts[k].Stage + "\n").Length;
            int end = k + 1 < starts.Count ? starts[k + 1].Index : source.Length;
            int close = source.LastIndexOf("#endif", end - 1, end - start, StringComparison.Ordinal);
            result[starts[k].Stage] = source[start..close];
        }
        return result;
    }

    /// A constant as both platforms can describe it: scalar kind (float, int, uint, bool),
    /// columns, matrix, elements.
    readonly record struct Shape(string Kind, int Columns, bool Matrix, int Elements)
    {
        public override string ToString() => $"{Kind}{(Matrix ? "4x4" : Columns > 1 ? Columns.ToString() : "")}{(Elements > 1 ? $"[{Elements}]" : "")}";
    }

    const string MatrixPrefix = "hlslcc_mtx4x4";
    const string UnusedPrefix = "Xhlslcc_Unused";
    const string ZcmpPrefix = "hlslcc_zcmp";

    static string KindOf(int blobType) => blobType switch { 0 => "float", 1 => "int", 2 => "bool", 3 => "half", 4 => "short", 5 => "uint", _ => $"type{blobType}" };

    static Shape MetalShape(ShaderConstant c) => new(KindOf(c.Type), c.Columns, c.IsMatrix, c.Elements);

    /// A GLSL uniform's name without HLSLcc's matrix prefix, and its shape.
    static (string Name, Shape Shape)? GlslShape(GlslUniform u)
    {
        if (u.Name.StartsWith(UnusedPrefix, StringComparison.Ordinal)) return null;
        bool matrix = u.Name.StartsWith(MatrixPrefix, StringComparison.Ordinal);
        string name = matrix ? u.Name[MatrixPrefix.Length..] : u.Name;
        string t = u.Type;
        (string kind, int columns) = t switch
        {
            "float" => ("float", 1),
            "int" => ("int", 1),
            "uint" => ("uint", 1),
            "bool" => ("bool", 1),
            _ when t.StartsWith("vec") => ("float", t[3] - '0'),
            _ when t.StartsWith("ivec") => ("int", t[4] - '0'),
            _ when t.StartsWith("uvec") => ("uint", t[4] - '0'),
            _ when t.StartsWith("bvec") => ("bool", t[4] - '0'),
            "mat4" => ("float", 4),
            _ => ($"glsl:{t}", 1),
        };
        if (t == "mat4") return (name, new Shape("float", 4, true, Math.Max(1, u.ArraySize)));
        if (matrix) return (name, new Shape(kind, 4, true, Math.Max(1, u.ArraySize / 4)));
        return (name, new Shape(kind, columns, false, Math.Max(1, u.ArraySize)));
    }

    /// TextureDimension of a GLSL sampler type, and whether it is a shadow or multisample one.
    static (int Dimension, bool Shadow, bool MultiSampled) SamplerShape(string type) => type switch
    {
        "sampler2D" or "isampler2D" or "usampler2D" or "sampler2DRect" => (2, false, false),
        "sampler2DShadow" => (2, true, false),
        "sampler3D" => (3, false, false),
        "samplerCube" => (4, false, false),
        "samplerCubeShadow" => (4, true, false),
        "sampler2DArray" => (5, false, false),
        "sampler2DArrayShadow" => (5, true, false),
        "sampler2DMS" => (2, false, true),
        "sampler2DMSArray" => (5, false, true),
        _ => (-1, false, false),
    };

    static void CompareStage(Report report, string where, ShaderParameters metal, string glsl)
    {
        // Constants: Metal's buffer members against GLSL's declarations.
        var metalConstants = new Dictionary<string, (string Buffer, ShaderConstant C)>(StringComparer.Ordinal);
        foreach (var cb in metal.ConstantBuffers)
            foreach (var m in cb.Members) metalConstants[m.Name] = (cb.Name, m);
        var uniforms = GlslDeclarations.Uniforms(glsl);
        var glConstants = new Dictionary<string, (string Buffer, Shape Shape)>(StringComparer.Ordinal);
        foreach (var u in uniforms)
        {
            if (u.IsSampler) continue;
            var s = GlslShape(u);
            if (s is { } shape) glConstants[shape.Name] = (u.Block ?? "$Globals", shape.Shape);
        }
        foreach (var (name, (buffer, c)) in metalConstants)
        {
            if (!glConstants.TryGetValue(name, out var g))
            {
                report.Add("constant: in the Metal blob only", $"{where}: {buffer}.{name} {MetalShape(c)}");
                continue;
            }
            var ms = MetalShape(c);
            if (ms.Kind != g.Shape.Kind || ms.Columns != g.Shape.Columns || ms.Matrix != g.Shape.Matrix)
                report.Add("constant: type differs", $"{where}: {name} Metal {ms}, GLSL {g.Shape}");
            else if (ms.Elements != g.Shape.Elements)
                report.Add("constant: array size differs", $"{where}: {name} Metal {ms}, GLSL {g.Shape}");
            else report.Agree("constants with the same name, type and array size");
            bool metalLoose = buffer is "VGlobals" or "FGlobals";
            if (metalLoose && g.Buffer == "$Globals") { }
            else if (!metalLoose && g.Buffer == "$Globals") report.Add("constant: Unity built-in cbuffer on Metal, loose uniform on GLCore", $"{where}: {name} in {buffer} / $Globals");
            else if (buffer != g.Buffer) report.Add("constant: buffer differs", $"{where}: {name} in {buffer} on Metal, {g.Buffer} on GLCore");
        }
        foreach (var (name, g) in glConstants)
            if (!metalConstants.ContainsKey(name)) report.Add("constant: declared only by the GLCore stage", $"{where}: {g.Buffer}.{name} {g.Shape}");

        // Textures and samplers.
        var glSamplers = uniforms.Where(u => u.IsSampler).ToDictionary(u => u.Name, StringComparer.Ordinal);
        var metalTextures = metal.Textures.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var samplerUse = metal.Textures.Where(t => t.SamplerIndex >= 0).GroupBy(t => t.SamplerIndex).ToDictionary(gr => gr.Key, gr => gr.Count());
        foreach (var t in metal.Textures)
        {
            if (!glSamplers.TryGetValue(t.Name, out var u))
            {
                report.Add("texture: Metal only", $"{where}: {t.Name}");
                continue;
            }
            var (dim, _, ms) = SamplerShape(u.Type);
            if (dim != t.Dimension || ms != t.MultiSampled) report.Add("texture: dimension differs", $"{where}: {t.Name} Metal dim {t.Dimension}{(t.MultiSampled ? " ms" : "")}, GLSL {u.Type}");
            else report.Agree("textures with the same name and dimension");
            if (t.SamplerIndex < 0) report.Add("texture: no sampler on Metal (read with texture.read)", $"{where}: {t.Name} ({u.Type})");
            else if (samplerUse[t.SamplerIndex] > 1) report.Add("texture: shared or inline sampler on Metal", $"{where}: {t.Name} sampler {t.SamplerIndex}");
        }
        foreach (var name in glSamplers.Keys)
        {
            if (metalTextures.ContainsKey(name)) continue;
            // A shadow texture sampled both plainly and with comparison: GLSL declares a second,
            // comparison sampler `hlslcc_zcmp_X` for X; Metal's depth texture X serves both.
            if (name.StartsWith(ZcmpPrefix, StringComparison.Ordinal) && metalTextures.ContainsKey(name[ZcmpPrefix.Length..]))
                report.Add("texture: GLCore comparison sampler hlslcc_zcmp_X of Metal's depth texture X", $"{where}: {name} {glSamplers[name].Type}");
            else report.Add("texture: GLCore only", $"{where}: {name} {glSamplers[name].Type}");
        }

        // Constant buffer bindings name a buffer of the parameters; structured buffers, GL's
        // storage blocks.
        foreach (var b in metal.ConstantBufferBindings)
            if (metal.Buffer(b.Name) == null) report.Add("buffer: Metal binding without its constant buffer", $"{where}: {b.Name}");
        foreach (var cb in metal.ConstantBuffers)
            if (cb.Name.Length > 0 && cb.Members.Length > 0 && !metal.ConstantBufferBindings.Any(b => b.Name == cb.Name))
                report.Add("buffer: Metal constant buffer without a binding", $"{where}: {cb.Name}");
        var ssbos = GlslDeclarations.StorageBuffers(glsl).Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        var structured = metal.StructuredBuffers.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        if (!ssbos.SetEquals(structured))
            report.Add("buffer: structured buffers differ", $"{where}: Metal [{string.Join(" ", structured.Order())}], GLSL [{string.Join(" ", ssbos.Order())}]");
        else foreach (var _ in structured) report.Agree("structured buffers matching a GLSL storage block");
    }

    /// A stage's default-block uniforms at the offsets the GLCore blob gives them, in declaration
    /// order (inferred from the data): 16-byte registers, a value never straddling one; arrays and
    /// matrices start a register, their elements as long as the element (a vector of three as
    /// four), so an array of floats is packed tight, unlike HLSL's one register per element.
    static Dictionary<string, int> PackedOffsets(string glsl)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        int offset = 0;
        foreach (var u in GlslDeclarations.Uniforms(glsl))
        {
            if (u.IsSampler || u.Block != null || GlslShape(u) is not { } s) continue;
            int element = s.Shape.Matrix ? 64 : 4 * s.Shape.Columns;
            bool array = s.Shape.Matrix || s.Shape.Elements > 1;
            if (array || (offset % 16) + element > 16) offset = (offset + 15) & ~15;
            result.TryAdd(s.Name, offset);
            int stride = array && s.Shape.Columns == 3 ? 16 : element;
            offset += stride * (s.Shape.Elements - 1) + element;
        }
        return result;
    }

    /// Both Metal stages against the GLCore program's parameter blob (and its common parameters).
    static void CompareBlob(Report report, string where, ShaderParameters vertex, ShaderParameters fragment, ShaderParameters glBlob, Dictionary<string, string> glsl)
    {
        var metal = new Dictionary<string, ShaderConstant>(StringComparer.Ordinal);
        foreach (var p in new[] { vertex, fragment })
            foreach (var cb in p.ConstantBuffers)
                foreach (var m in cb.Members) metal.TryAdd(m.Name, m);
        var blob = new Dictionary<string, ShaderConstant>(StringComparer.Ordinal);
        foreach (var cb in glBlob.ConstantBuffers)
            foreach (var m in cb.Members) blob.TryAdd(m.Name, m);
        // Each stage's $Globals as HLSL packs it, in declaration order: the offsets the blob's
        // $Globals members carry.
        var stages = new[] { PackedOffsets(glsl["VERTEX"]), PackedOffsets(glsl["FRAGMENT"]) };
        var globals = glBlob.Buffer("$Globals");
        if (globals != null)
            foreach (var g in globals.Members)
                if (!stages.Any(s => s.TryGetValue(g.Name, out int o) && o == g.Offset))
                    report.Add("blob: GLCore $Globals offset is not its HLSL packing in either stage", $"{where}: {g.Name} @{g.Offset}");
        foreach (var (name, m) in metal)
        {
            if (!blob.TryGetValue(name, out var g))
            {
                // Declared by a stage at an offset where the blob records another stage's member.
                int stage = Array.FindIndex(stages, s => s.ContainsKey(name));
                if (stage < 0) continue;
                int offset = stages[stage][name];
                var other = stages[1 - stage];
                if (globals != null && globals.Members.Any(b => b.Offset == offset && other.TryGetValue(b.Name, out int o) && o == offset))
                    report.Add("blob: GLCore parameter blob drops members whose offsets coincide across stages", $"{where}: {name} @{offset}");
                else report.Add("blob: GLCore parameter blob lacks a declared member", $"{where}: {name}");
                continue;
            }
            if (g.Type != m.Type)
            {
                if (g.Type == 5 && m.Type == 1) report.Add("blob: GLCore parameter blob records int as type 5", $"{where}: {name}");
                else report.Add("blob: type differs from the GLCore parameter blob", $"{where}: {name} Metal {m.Type}, GLCore {g.Type}");
            }
            if (g.Columns != m.Columns || g.Rows != m.Rows || g.IsMatrix != m.IsMatrix) report.Add("blob: shape differs from the GLCore parameter blob", $"{where}: {name} Metal {m.Rows}x{m.Columns}, GLCore {g.Rows}x{g.Columns}");
            if (g.Elements != m.Elements) report.Add("blob: array size differs from the GLCore parameter blob", $"{where}: {name} Metal {m.Elements}, GLCore {g.Elements}");
        }
        var textures = vertex.Textures.Concat(fragment.Textures).Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var t in glBlob.Textures)
            if (!textures.Contains(t.Name)) report.Add("blob: texture of the GLCore parameter blob no Metal stage has", $"{where}: {t.Name}");
        foreach (var t in vertex.Textures.Concat(fragment.Textures))
            if (!glBlob.Textures.Any(g => g.Name == t.Name)) report.Add("blob: Metal texture the GLCore parameter blob lacks", $"{where}: {t.Name}");
    }
}
