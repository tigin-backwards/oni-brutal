using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace OniBrutal.GameTweaks
{
    /// Game tweak `off-screen-conveyors` (opt-in; changes behaviour, see below). Every frame
    /// SolidConduitFlow.RenderEveryTick moves every item travelling on a conveyor rail a step
    /// along it (U57: about 950 items a frame, 85% of them off screen), and each move marks the
    /// item for the cell-change monitor and its animation for an update.
    ///
    /// This makes the same moves, in the same order, for items whose rail section (the chunk of
    /// their cell or the cell they come from) is on screen, and skips them for the rest. An item
    /// skipped while moving is placed at its cell's centre once it stops (where a finished move
    /// leaves it); one that comes on screen moves from there on as before.
    ///
    /// What changes: while off screen, a travelling item's transform (and with it the cell the
    /// game files it under for partitioner lookups, decor and radiation) lags behind its place in
    /// the conveyor's flow until it stops or comes on screen. The flow itself — what is carried
    /// where, and when it arrives — is the conveyor grid's and is not affected.
    public static class OffscreenConveyors
    {
        public const string Name = "off-screen-conveyors";

        /// Items skipped while moving and not yet placed again.
        static readonly HashSet<int> skipped = new HashSet<int>();

        public static void Install(Harmony harmony)
            => harmony.Patch(AccessTools.Method(typeof(SolidConduitFlow), nameof(SolidConduitFlow.RenderEveryTick)), prefix: new HarmonyMethod(typeof(OffscreenConveyors), nameof(RenderEveryTick)));

        /// SolidConduitFlow.RenderEveryTick(float), with off-screen moves skipped.
        static bool RenderEveryTick(SolidConduitFlow __instance)
        {
            var updater = Singleton<KBatchedAnimUpdater>.Instance;
            var soa = __instance.GetSOAInfo();
            float lerp = __instance.ContinuousLerpPercent;
            for (int i = 0; i < soa.NumEntries; i++)
            {
                var conduit = soa.GetConduit(i);
                var flow = conduit.GetLastFlowInfo(__instance);
                int cell = conduit.GetCell(__instance);
                if (flow.direction == SolidConduitFlow.FlowDirection.None)
                {
                    if (skipped.Count > 0) PlaceIfSkipped(__instance, cell);
                    continue;
                }
                int from = SolidConduitFlow.GetCellFromDirection(cell, flow.direction);
                var contents = __instance.GetContents(from);
                if (!contents.pickupableHandle.IsValid()) continue;
                if (updater != null && !Visible(updater, cell) && !Visible(updater, from))
                {
                    skipped.Add(contents.pickupableHandle.index);
                    continue;
                }
                skipped.Remove(contents.pickupableHandle.index);
                Vector3 a = Grid.CellToPosCCC(cell, Grid.SceneLayer.SolidConduitContents);
                Vector3 b = Grid.CellToPosCCC(from, Grid.SceneLayer.SolidConduitContents);
                var pickupable = __instance.GetPickupable(contents.pickupableHandle);
                if (pickupable != null) pickupable.transform.SetPosition(Vector3.Lerp(a, b, lerp));
            }
            return false;
        }

        /// An item at rest in `cell` that was skipped while moving goes to the cell's centre.
        static void PlaceIfSkipped(SolidConduitFlow flow, int cell)
        {
            var contents = flow.GetContents(cell);
            if (!contents.pickupableHandle.IsValid() || !skipped.Remove(contents.pickupableHandle.index)) return;
            var pickupable = flow.GetPickupable(contents.pickupableHandle);
            if (pickupable != null) pickupable.transform.SetPosition(Grid.CellToPosCCC(cell, Grid.SceneLayer.SolidConduitContents));
        }

        static bool Visible(KBatchedAnimUpdater updater, int cell)
            => updater.IsChunkVisible(KBatchedAnimUpdater.PosToChunkXY(Grid.CellToPos(cell)));
    }
}
