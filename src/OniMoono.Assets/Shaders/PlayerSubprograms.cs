using System;
using System.Collections.Generic;
using OniMoono.Assets.TypeTrees;

namespace OniMoono.Assets.Shaders
{
    /// A pass stage's player subprogram: its GPU program type, its program entry and parameter blob
    /// (indices into its platform's table; -1 for no parameter blob) and the keywords it was
    /// compiled for (names, in the order the subprogram lists them).
    public readonly record struct PlayerSubprogram(string Stage, int GpuProgramType, int BlobIndex, int ParameterBlobIndex, string[] Keywords)
    {
        public uint Platform => GpuProgramTypes.PlatformOf(GpuProgramType);
    }

    /// Reads the player subprograms of a pass (Unity 2021.2 and later: shader-wide keyword names).
    public static class PlayerSubprograms
    {
        /// SerializedPass stage fields, in pipeline order.
        public static readonly string[] Stages = ["progVertex", "progFragment", "progGeometry", "progHull", "progDomain", "progRayTracing"];

        /// Every player subprogram of a pass stage. `m_PlayerSubPrograms` holds one list per
        /// hardware tier (`m_ParameterBlobIndices` alike); all are returned, tier by tier.
        public static IEnumerable<PlayerSubprogram> Of(Fields pass, string stage, string[] keywordNames)
        {
            if (!pass.Contains(stage)) yield break;
            var program = pass.Struct(stage);
            if (!program.Contains("m_PlayerSubPrograms")) yield break;
            var lists = program.Array("m_PlayerSubPrograms");
            var parameterLists = program.Contains("m_ParameterBlobIndices") ? program.Array("m_ParameterBlobIndices") : [];
            for (int l = 0; l < lists.Length; l++)
            {
                var list = (object?[])lists[l]!;
                var parameterBlobs = l < parameterLists.Length ? (uint[])parameterLists[l]! : [];
                for (int k = 0; k < list.Length; k++)
                {
                    var sub = (Fields)list[k]!;
                    var indices = sub.Get<ushort[]>("m_KeywordIndices") ?? [];
                    yield return new PlayerSubprogram(stage, sub.Get<sbyte>("m_GpuProgramType"), (int)sub.Get<uint>("m_BlobIndex"),
                        k < parameterBlobs.Length ? (int)parameterBlobs[k] : -1, Array.ConvertAll(indices, i => keywordNames[i]));
                }
            }
        }

        /// The pass's name table (`m_NameIndices`: name → index), as index → name.
        public static Dictionary<int, string> Names(Fields pass)
        {
            var names = new Dictionary<int, string>();
            foreach (var item in pass.Array("m_NameIndices"))
            {
                var pair = (Fields)item!;
                names[Convert.ToInt32(pair["second"])] = pair.String("first");
            }
            return names;
        }

        /// The pass stage's common parameters, or empty ones.
        public static ShaderParameters Common(Fields pass, string stage, IReadOnlyDictionary<int, string> names)
        {
            if (!pass.Contains(stage) || !pass.Struct(stage).Contains("m_CommonParameters")) return new ShaderParameters();
            return ShaderParameters.ReadCommon(pass.Struct(stage).Struct("m_CommonParameters"), names);
        }
    }
}
