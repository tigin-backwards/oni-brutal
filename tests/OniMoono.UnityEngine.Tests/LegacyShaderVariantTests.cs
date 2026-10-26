using System.Linq;
using OniMoono.Assets.TypeTrees;
using OniMoono.Engine.Rendering;
using Xunit;

namespace OniMoono.Tests
{
    /// Shaders of mods' bundles built before Unity 2021.2 name their keywords per pass, through
    /// the pass's name table. From 2019.1 a sub-program lists global and local keyword indices;
    /// before it (Japanese Font Patch's bundle, Unity 2018.3) one list of indices.
    public class LegacyShaderVariantTests
    {
        static Fields Pass(params Fields[] subPrograms)
        {
            var pass = new Fields();
            var names = new object[] { Pair("_A", 1), Pair("_B", 2), Pair("_C", 3) };
            pass.Add("m_NameIndices", names);
            var vertex = new Fields();
            vertex.Add("m_SubPrograms", subPrograms.Cast<object>().ToArray());
            pass.Add("progVertex", vertex);
            return pass;
        }

        static Fields Pair(string name, int index)
        {
            var f = new Fields();
            f.Add("first", name);
            f.Add("second", index);
            return f;
        }

        static Fields Sub(uint blob, sbyte gpuType, params (string list, ushort[] indices)[] lists)
        {
            var f = new Fields();
            f.Add("m_BlobIndex", blob);
            foreach (var (list, indices) in lists) f.Add(list, indices);
            f.Add("m_GpuProgramType", gpuType);
            return f;
        }

        [Fact]
        public void Unity2018SubProgramsNameTheirKeywordsInOneList()
        {
            var variants = ShaderPrograms.LegacyVariants(Pass(
                Sub(4, 7, ("m_KeywordIndices", new ushort[] { 1, 3 })),
                Sub(5, 2, ("m_KeywordIndices", new ushort[] { 2 })),     // not OpenGLCore
                Sub(6, 7, ("m_KeywordIndices", new ushort[0]))));
            Assert.Equal(new[] { 4, 6 }, variants.Select(v => v.BlobIndex));
            Assert.Equal(new[] { "_A", "_C" }, variants[0].Keywords);
            Assert.Empty(variants[1].Keywords);
        }

        [Fact]
        public void LaterSubProgramsNameGlobalThenLocalKeywords()
        {
            var variants = ShaderPrograms.LegacyVariants(Pass(
                Sub(9, 7, ("m_GlobalKeywordIndices", new ushort[] { 2 }), ("m_LocalKeywordIndices", new ushort[] { 1 }))));
            Assert.Equal(new[] { "_B", "_A" }, Assert.Single(variants).Keywords);
        }
    }
}
