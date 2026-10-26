using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OniMoono.Assets.Shaders
{
    /// A uniform a GLCore stage declares: its GLSL type and name as written (HLSLcc's names:
    /// `hlslcc_mtx4x4X` for matrix X as four column vectors), its array length (0 for none), the
    /// uniform block holding it (null for the default block: Unity's `$Globals`) and its
    /// `UNITY_LOCATION` (-1 for none).
    public readonly record struct GlslUniform(string Type, string Name, int ArraySize, string? Block, int Location)
    {
        public bool IsSampler => Type.Contains("sampler", StringComparison.Ordinal);
    }

    /// A shader storage block (`layout(std430, binding = n) readonly buffer X`).
    public readonly record struct GlslBuffer(string Name, int Binding);

    /// The uniform declarations of a GLCore stage's source, as Unity's HLSLcc writes them: one per
    /// line, `uniform` declarations in the default block, `UNITY_LOCATION(n) uniform` samplers,
    /// `UNITY_BINDING(n) uniform Block { … };` uniform blocks, and storage blocks. This is what GL
    /// reflection of the program would report before the compiler drops unused uniforms (HLSLcc
    /// declares only what the stage uses, apart from block members named `Xhlslcc_Unused…`, kept
    /// for the block's layout).
    public static class GlslDeclarations
    {
        static readonly Regex uniform = new(@"^\s*(?:UNITY_LOCATION\((\d+)\)\s*)?uniform\s+(?:(?:lowp|mediump|highp)\s+)?(\w+)\s+(\w+)\s*(?:\[\s*(\d+)\s*\])?\s*;", RegexOptions.Compiled);
        static readonly Regex blockStart = new(@"^\s*(?:UNITY_BINDING\(\d+\)\s*)?(?:layout\([^)]*\)\s*)?uniform\s+(\w+)\s*\{", RegexOptions.Compiled);
        static readonly Regex member = new(@"^\s*(?:(?:lowp|mediump|highp)\s+)?(\w+)\s+(\w+)\s*(?:\[\s*(\d+)\s*\])?\s*;", RegexOptions.Compiled);
        static readonly Regex storage = new(@"^\s*layout\([^)]*binding\s*=\s*(\d+)[^)]*\)\s*(?:readonly\s+|writeonly\s+|coherent\s+|restrict\s+)*buffer\s+(\w+)", RegexOptions.Compiled);

        public static List<GlslUniform> Uniforms(string source) => Read(source).Uniforms;

        public static List<GlslBuffer> StorageBuffers(string source) => Read(source).Buffers;

        static (List<GlslUniform> Uniforms, List<GlslBuffer> Buffers) Read(string source)
        {
            var uniforms = new List<GlslUniform>();
            var buffers = new List<GlslBuffer>();
            string? block = null;
            foreach (var raw in source.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (block != null)
                {
                    if (line.TrimStart().StartsWith('}')) { block = null; continue; }
                    var m = member.Match(line);
                    if (m.Success) uniforms.Add(new GlslUniform(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0, block, -1));
                    continue;
                }
                var s = storage.Match(line);
                if (s.Success)
                {
                    buffers.Add(new GlslBuffer(s.Groups[2].Value, int.Parse(s.Groups[1].Value)));
                    continue;
                }
                var b = blockStart.Match(line);
                if (b.Success)
                {
                    block = b.Groups[1].Value;
                    continue;
                }
                var u = uniform.Match(line);
                if (u.Success)
                    uniforms.Add(new GlslUniform(u.Groups[2].Value, u.Groups[3].Value, u.Groups[4].Success ? int.Parse(u.Groups[4].Value) : 0, null,
                        u.Groups[1].Success ? int.Parse(u.Groups[1].Value) : -1));
            }
            return (uniforms, buffers);
        }
    }
}
