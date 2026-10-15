using System;
using System.Collections.Generic;
using System.Linq;
using OniBrutal.Engine;
using OniBrutal.Engine.Assets;
using OniBrutal.Engine.Rendering;
using UnityEngine;

namespace OniBrutal.Host
{
    /// Rendering-data gate: every texture, shader, material, mesh, sprite and sprite atlas in the
    /// player data loads through the engine's binders, and texel data matches the size implied
    /// by its format and dimensions.
    internal static class BindCheck
    {
        static readonly string[] files =
        [
            "level0", "level1", "level2", "resources.assets", "sharedassets0.assets", "sharedassets1.assets", "sharedassets2.assets",
            "Resources/unity default resources", "Resources/unity_builtin_extra",
        ];

        static int ShaderVariants;

        static readonly HashSet<int> classes = [28, 187, 84, 48, 21, 43, 213, 687078895];

        public static int Run(string dataDir)
        {
            AssetSystem.Initialize(dataDir);
            ProjectSettings.Load();
            PlayerLoop.Initialize();
            var counts = new SortedDictionary<string, int>();
            var failures = new List<string>();
            foreach (var name in files)
            {
                var file = AssetSystem.Get(name);
                foreach (var info in file.Objects)
                {
                    int classId = file.TypeOf(info).ClassId;
                    if (!classes.Contains(classId)) continue;
                    try
                    {
                        var obj = ObjectLoader.Load(file, info.PathId);
                        Check(obj);
                        counts[obj.GetType().Name] = counts.GetValueOrDefault(obj.GetType().Name) + 1;
                    }
                    catch (Exception e)
                    {
                        failures.Add($"{name}:{info.PathId} ({classId}): {e.GetType().Name}: {e.Message}");
                    }
                }
            }
            foreach (var (type, n) in counts) Console.WriteLine($"{type,-16} {n,6}");
            Console.WriteLine($"shader variants  {ShaderVariants,6}");
            foreach (var f in failures.Take(60)) Console.WriteLine("FAIL " + f);
            Console.WriteLine($"{failures.Count} failures");
            return failures.Count == 0 ? 0 : 1;
        }

        static void Check(UnityEngine.Object obj)
        {
            switch (obj.Native)
            {
                case TexelTextureNative t:
                {
                    long expected = t is Texture2DArrayNative ? t.SliceSize * t.Depth : t.SliceSize;
                    long actual = t.Memory.Size;
                    t.DropTexels();
                    if (actual != expected) throw new InvalidOperationException($"{t.Name}: {t.Format} {t.Width}x{t.Height} mips {t.MipCount}: {actual} bytes, expected {expected}");
                    break;
                }
                case ShaderNative shader when shader.HasOpenGLCorePrograms:
                {
                    int variants = 0;
                    foreach (var sub in shader.SubShaders)
                        foreach (var pass in sub.Passes)
                        {
                            if (pass.Type != 0) continue;
                            foreach (var list in pass.Data.Struct("progVertex").Array("m_PlayerSubPrograms"))
                                foreach (var o in (object?[])list!)
                                {
                                    var p = (OniBrutal.Assets.TypeTrees.Fields)o!;
                                    if (!ShaderPrograms.IsGlCore(p.Get<sbyte>("m_GpuProgramType"))) continue;
                                    var v = shader.Programs.Variant((int)p.Get<uint>("m_BlobIndex"));
                                    if (!v.VertexSource.Contains("void main") || !v.FragmentSource.Contains("void main") || (v.GeometrySource != null && !v.GeometrySource.Contains("void main")))
                                        throw new InvalidOperationException($"shader {shader.ShaderName}: variant {v.BlobIndex} lacks a stage");
                                    variants++;
                                }
                        }
                    ShaderVariants += variants;
                    break;
                }
                case MaterialNative m when m.Shader == null:
                    throw new InvalidOperationException($"material {m.Name} has no shader");
                case SpriteNative s:
                {
                    var r = s.Render;
                    if (r.Texture == null) throw new InvalidOperationException($"sprite {s.Name} has no texture");
                    foreach (var uv in s.ComputeUvs())
                        if (uv.x < -0.001f || uv.x > 1.001f || uv.y < -0.001f || uv.y > 1.001f)
                            throw new InvalidOperationException($"sprite {s.Name}: uv {uv} outside the texture");
                    break;
                }
            }
        }
    }
}
