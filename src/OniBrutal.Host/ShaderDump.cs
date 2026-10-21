using System;
using System.IO;
using System.Linq;
using OniBrutal.Engine;
using OniBrutal.Engine.Assets;
using OniBrutal.Engine.Rendering;
using UnityEngine;

namespace OniBrutal.Host
{
    /// `--dump-shader NAME:DIR` (diagnostics): writes the GLSL of every OpenGLCore variant of a
    /// shader, one file per pass and blob, named by its keywords, with each pass's render state and
    /// the shader's properties.
    internal static class ShaderDump
    {
        static readonly string[] files =
        [
            "level0", "level1", "level2", "resources.assets", "sharedassets0.assets", "sharedassets1.assets", "sharedassets2.assets",
            "Resources/unity default resources", "Resources/unity_builtin_extra",
        ];

        public static int Run(string dataDir, string spec)
        {
            int colon = spec.LastIndexOf(':');
            string name = spec[..colon], dir = Path.GetFullPath(spec[(colon + 1)..]);
            AssetSystem.Initialize(dataDir);
            ProjectSettings.Load();
            PlayerLoop.Initialize();
            foreach (var fileName in files)
            {
                var file = AssetSystem.Get(fileName);
                foreach (var info in file.Objects)
                {
                    if (file.TypeOf(info).ClassId != 48 || ObjectLoader.Load(file, info.PathId) is not Shader shader) continue;
                    var native = (ShaderNative)shader.NativeOrThrow;
                    if (native.ShaderName != name) continue;
                    Directory.CreateDirectory(dir);
                    File.WriteAllLines(Path.Combine(dir, "properties.txt"), native.Properties.Select(p =>
                        $"{p.Name} {p.Kind} default={(p.Kind == ShaderPropertyKind.Texture ? "\"" + p.DefaultTexture + "\" dim " + p.TextureDimension : p.Default.ToString())}"));
                    int written = 0;
                    for (int s = 0; s < native.SubShaders.Length; s++)
                        for (int p = 0; p < native.SubShaders[s].Passes.Length; p++)
                        {
                            var pass = native.SubShaders[s].Passes[p];
                            if (pass.Type != 0) continue;
                            var state = pass.Data.Struct("m_State");
                            string V(string field) => $"{field}={state.Struct(field).Float("val")}{(state.Struct(field).String("name") is { Length: > 0 } n && n != "<noninit>" ? "[" + n + "]" : "")}";
                            var blend = state.Struct("rtBlend0");
                            string B(string field) => $"{field}={blend.Struct(field).Float("val")}{(blend.Struct(field).String("name") is { Length: > 0 } n && n != "<noninit>" ? "[" + n + "]" : "")}";
                            File.WriteAllText(Path.Combine(dir, $"s{s}p{p}_state.txt"),
                                "tags=" + string.Join(";", native.SubShaders[s].Tags.Select(kv => kv.Key + "=" + kv.Value)) + "\n" + string.Join("\n", new[] { V("zTest"), V("zWrite"), V("culling"), B("srcBlend"), B("destBlend"), B("srcBlendAlpha"), B("destBlendAlpha"), B("colMask") }) + "\n");
                            foreach (var variant in pass.Variants)
                            {
                                var v = native.Programs.Variant(variant.BlobIndex);
                                var keys = v.Keywords.Length == 0 ? "default" : string.Join("+", v.Keywords);
                                var path = Path.Combine(dir, $"s{s}p{p}_{v.BlobIndex}_{keys}");
                                File.WriteAllText(path + ".vert", v.VertexSource);
                                File.WriteAllText(path + ".frag", v.FragmentSource);
                                written++;
                            }
                        }
                    Console.WriteLine($"{name}: {written} variants written to {dir}");
                    return 0;
                }
            }
            Console.Error.WriteLine($"No shader named '{name}'.");
            return 1;
        }
    }
}
