using System;
using HarmonyLib;
using UnityEngine;

namespace OniBrutal.GameTweaks
{
    /// Game tweak `fast-symbol-overrides` (opt-in). An animation batch writes a controller's
    /// symbol-override block (12 floats per symbol of its build) into its texture whenever the
    /// block's version changed. The controller re-applies its overrides every time it is
    /// written (SymbolOverrideController.ApplyOverrides on each dirty mark), and both
    /// SymbolOverrideInfoGpuData.SetSymbolOverrideInfo overloads raise the version even when
    /// they store the values already there: so every dirty mark of a controller with overrides
    /// (Duplicants' clothes and items, critters, many buildings) re-uploads an unchanged block.
    ///
    /// The tweak skips a setter call that would store only the values already held: the block
    /// stays the same and its version is not raised, so the texture keeps the same bytes. These
    /// setters are the only writers of the block, and its version is read only by the batch's
    /// write check. A call that changes any value runs as before. Exact by construction: a
    /// skipped call would have written each field's current value.
    public static class FastSymbolOverrides
    {
        public const string Name = "fast-symbol-overrides";

        static readonly AccessTools.FieldRef<SymbolOverrideInfoGpuData, SymbolOverrideInfoGpuData.SymbolOverrideInfoToByteConverter> converter =
            AccessTools.FieldRefAccess<SymbolOverrideInfoGpuData, SymbolOverrideInfoGpuData.SymbolOverrideInfoToByteConverter>("symbolOverrideInfoConverter");

        public static void Install(Harmony harmony)
        {
            var type = typeof(SymbolOverrideInfoGpuData);
            harmony.Patch(AccessTools.Method(type, nameof(SymbolOverrideInfoGpuData.SetSymbolOverrideInfo),
                    new[] { typeof(int), typeof(int), typeof(int), typeof(KBatchGroupData), typeof(int), typeof(int) }),
                prefix: new HarmonyMethod(typeof(FastSymbolOverrides), nameof(SetRange)));
            harmony.Patch(AccessTools.Method(type, nameof(SymbolOverrideInfoGpuData.SetSymbolOverrideInfo),
                    new[] { typeof(int), typeof(KAnim.Build.SymbolFrameInstance).MakeByRefType() }),
                prefix: new HarmonyMethod(typeof(FastSymbolOverrides), nameof(SetOne)));
        }

        /// Whether an entry already holds what the setters store for this frame instance, value for
        /// value (Vector2's == is approximate, so components are compared).
        static bool Holds(in SymbolOverrideInfoGpuData.SymbolOverrideInfo info, float atlas, in KAnim.Build.SymbolFrameInstance frame)
            => info.atlas == atlas && info.isoverriden == 1f && Same(info.bboxMin, frame.bboxMin) && Same(info.bboxMax, frame.bboxMax)
               && Same(info.uvMin, frame.uvMin) && Same(info.uvMax, frame.uvMax);

        static bool Same(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;

        /// SetSymbolOverrideInfo(start, frames, atlas, source, sourceStart, sourceFrames): false
        /// (skipped) when every entry it writes already holds its value.
        static bool SetRange(SymbolOverrideInfoGpuData __instance, int symbol_start_idx, int symbol_num_frames, int atlas_idx, KBatchGroupData source_data, int source_start_idx, int source_num_frames)
        {
            var infos = converter(__instance).symbolOverrideInfos;
            for (int i = 0; i < symbol_num_frames; i++)
            {
                var frame = source_data.symbolFrameInstances[source_start_idx + Math.Min(source_num_frames - 1, i)];
                if (!Holds(infos[symbol_start_idx + i], atlas_idx, frame)) return true;
            }
            return false;
        }

        /// SetSymbolOverrideInfo(index, ref frame): false (skipped) when the entry already holds it.
        static bool SetOne(SymbolOverrideInfoGpuData __instance, int symbol_idx, ref KAnim.Build.SymbolFrameInstance symbol_frame_instance)
            => !Holds(converter(__instance).symbolOverrideInfos[symbol_idx], symbol_frame_instance.buildImageIdx, symbol_frame_instance);
    }
}
