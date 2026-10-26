using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OniMoono.Assets.Files;
using OniMoono.Assets.Shaders;
using OniMoono.Assets.TypeTrees;
using OniMoono.Engine.Rendering;
using Xunit;

namespace OniMoono.Tests
{
    /// Program loading per graphics API: OpenGLCore programs hold both stages, keyed by the union
    /// of their keywords; Metal programs hold one stage, keyed by that stage's keywords, so each
    /// stage is selected from its own list.
    public class MetalShaderProgramTests
    {
        static ShaderPass Pass(ProgramVariant[] gl, ProgramVariant[] vertex, ProgramVariant[] fragment)
            => new() { Variants = gl, MetalVertexVariants = vertex, MetalFragmentVariants = fragment };

        [Fact]
        public void MetalSelectsEachStageByItsOwnKeywords()
        {
            // VERTEXLIGHT_ON is vertex-only and _ALPHABLEND_ON fragment-only: GLCore has a program
            // per combination, Metal two per stage.
            var pass = Pass(
                [new(10, []), new(11, ["VERTEXLIGHT_ON"]), new(12, ["_ALPHABLEND_ON"]), new(13, ["VERTEXLIGHT_ON", "_ALPHABLEND_ON"])],
                [new(1, [], 5), new(2, ["VERTEXLIGHT_ON"], 6)],
                [new(3, [], 7), new(4, ["_ALPHABLEND_ON"], 8)]);
            var enabled = new HashSet<string> { "_ALPHABLEND_ON", "VERTEXLIGHT_ON" };
            Assert.Equal(new StagePrograms(2, 6, 4, 8), ShaderPrograms.SelectStages(pass, ShaderNative.MetalPlatform, enabled.Contains));
            Assert.Equal(new StagePrograms(1, 5, 4, 8), ShaderPrograms.SelectStages(pass, ShaderNative.MetalPlatform, k => k == "_ALPHABLEND_ON"));
            Assert.Equal(new StagePrograms(1, 5, 3, 7), ShaderPrograms.SelectStages(pass, ShaderNative.MetalPlatform, _ => false));
            // OpenGLCore: one program for both stages, as SelectVariant picks it.
            Assert.Equal(new StagePrograms(13, -1, 13, -1), ShaderPrograms.SelectStages(pass, ShaderNative.OpenGLCorePlatform, enabled.Contains));
            Assert.Equal(12, ShaderPrograms.SelectVariant(pass, k => k == "_ALPHABLEND_ON"));
        }

        [Fact]
        public void SelectionPrefersMostMatchesThenFewestExtrasThenTheEarliest()
        {
            ProgramVariant[] variants = [new(1, ["A", "X"]), new(2, ["A"]), new(3, ["A", "B"]), new(4, ["A", "B"])];
            Assert.Equal(1, ShaderPrograms.SelectIndex(variants, k => k == "A")); // A alone: fewest extras
            Assert.Equal(2, ShaderPrograms.SelectIndex(variants, k => k is "A" or "B")); // first of the two best
            Assert.Equal(-1, ShaderPrograms.SelectIndex([], _ => true));
            Assert.Equal(new StagePrograms(-1, -1, -1, -1), ShaderPrograms.SelectStages(Pass([], [], []), ShaderNative.MetalPlatform, _ => true));
        }

        /// A shader of the macOS build, read straight from its file (no asset system).
        static Fields MacShader(string name)
        {
            var dataDir = MacDataDir;
            if (dataDir == null) Assert.Skip("macOS game data not found (set ONI_MAC_APP to OxygenNotIncluded.app).");
            foreach (var file in new[] { "sharedassets0.assets", "sharedassets1.assets", "sharedassets2.assets", "resources.assets", "Resources/unity_builtin_extra" })
            {
                var serialized = SerializedFile.Open(Path.Combine(dataDir, file));
                foreach (var info in serialized.Objects)
                {
                    var type = serialized.TypeOf(info);
                    if (type.ClassId != 48) continue;
                    var f = TypeTreeReader.ReadNative(48, serialized.OpenObject(info));
                    if (f.Struct("m_ParsedForm").String("m_Name") == name) return f;
                }
            }
            throw new InvalidOperationException(name + " not found");
        }

        internal static string MacDataDir
        {
            get
            {
                var app = Environment.GetEnvironmentVariable("ONI_MAC_APP") ?? "/mnt/c/Program Files (x86)/Steam/steamapps/content/app_457140/depot_457142/OxygenNotIncluded.app";
                var dir = Path.Combine(app, "Contents", "Resources", "Data");
                return Directory.Exists(dir) ? dir : null;
            }
        }

        [Fact]
        public void TheMacBuildsBatchedAnimationLoadsForBothPlatforms()
        {
            var f = MacShader("Klei/BatchedAnimation");
            var shader = new ShaderNative();
            shader.Bind(f, null);
            try
            {
                Assert.True(shader.HasProgramsFor(ShaderNative.OpenGLCorePlatform));
                Assert.True(shader.HasProgramsFor(ShaderNative.MetalPlatform));
                var pass = shader.SubShaders[0].Passes[0];
                // GLCore carries a DISABLE_K_LIGHTING program the Metal set lacks (one of the Mac
                // build's GLCore extras, docs/research/macos-feasibility.md §1.1): Metal falls back
                // to the keyword-less programs, as the selection rule does.
                Assert.Equal(["", "DISABLE_K_LIGHTING"], pass.Variants.Select(v => string.Join(" ", v.Keywords)));
                Assert.Equal([""], pass.MetalVertexVariants.Select(v => string.Join(" ", v.Keywords)));
                Assert.Equal([""], pass.MetalFragmentVariants.Select(v => string.Join(" ", v.Keywords)));

                var metal = shader.ProgramsFor(ShaderNative.MetalPlatform);
                Assert.Equal(ShaderNative.MetalPlatform, metal.Platform);
                Assert.Same(shader.Programs, shader.ProgramsFor(ShaderNative.OpenGLCorePlatform));
                var stages = ShaderPrograms.SelectStages(pass, ShaderNative.MetalPlatform, k => k == "DISABLE_K_LIGHTING");
                Assert.Equal(pass.MetalVertexVariants[0].BlobIndex, stages.Vertex);
                var vertex = metal.Metal(stages.Vertex);
                Assert.True(vertex.IsVertex);
                Assert.Empty(vertex.Keywords);
                Assert.Contains("VGlobals_Type", vertex.Source);
                var vp = metal.Parameters(pass.Data, "progVertex", stages.VertexParameters);
                Assert.Contains(vp.ConstantBuffers, b => b.Name == "UnityPerFrame" && b.Members.Any(m => m.Name == "unity_MatrixVP" && m.Offset == 272));
                var fragment = metal.Metal(stages.Fragment);
                Assert.False(fragment.IsVertex);
                var fp = metal.Parameters(pass.Data, "progFragment", stages.FragmentParameters);
                Assert.Contains(fp.Textures, t => t.Name == "_LightBufferTex");
                Assert.Same(fp, metal.Parameters(pass.Data, "progFragment", stages.FragmentParameters));
                // The GLCore programs are those the GL device runs.
                var gl = shader.Programs.Variant(ShaderPrograms.SelectVariant(pass, k => k == "DISABLE_K_LIGHTING"));
                Assert.Equal(["DISABLE_K_LIGHTING"], gl.Keywords);
                Assert.Throws<InvalidOperationException>(() => metal.Variant(stages.Vertex));
                Assert.Throws<InvalidOperationException>(() => shader.Programs.Metal(stages.Vertex));
            }
            finally
            {
                ShaderRegistry.Unregister(shader);
            }
        }
    }
}
