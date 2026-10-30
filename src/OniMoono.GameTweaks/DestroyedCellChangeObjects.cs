using System.Collections.Generic;
using HarmonyLib;

namespace OniMoono.GameTweaks
{
    /// Tweak fix-disease-overlay-crash (off by default; QA's verify turns it on): U60's CellChangeMonitor.RenderEveryTick, in
    /// Game.Update, reads `kobj.gameObject.transform` for every moved transform whose KObject has
    /// no pending cell. A KObject on a child of a destroyed object is never released (its
    /// activeSelf stays true in OnDestroy, so KMonoBehaviour does not queue it), and when such a
    /// child was moved after the last Game.Update (a parent moved, SetLocalPosition) and is
    /// destroyed before the next, the read throws NullReferenceException, the rest of the
    /// frame's cell changes are skipped and the game shows its crash dialog. The original does
    /// this too (UnityProbe cellchangecrash): in the Disease overlay a dying critter's
    /// DiseaseSourceVisualizer destroys its ResourceVisualizer, whose child FG carries a ToolTip,
    /// and the next Game.Update throws whenever a fixed step comes first. U59 skipped destroyed
    /// transforms (CellChangedEntry.Valid).
    ///
    /// Before RenderEveryTick, the KObjects it would throw on (no pending cell, GameObject
    /// destroyed) are unregistered, so it skips them as it does any transform without a KObject.
    /// Nothing else changes: every entry the game would read without throwing is left alone. Off
    /// by default, so the player crashes where the original does; on, it keeps long low-frame-rate
    /// runs (QA's verify) clear of Klei's bug.
    public static class DestroyedCellChangeObjects
    {
        public const string Name = "fix-disease-overlay-crash";

        static AccessTools.FieldRef<CellChangeMonitor, HybridListHashSet<int>> pending;
        static AccessTools.FieldRef<CellChangeMonitor, Dictionary<int, KObject>> objects;
        static int logged;

        /// Installs the fix only where the game still has the shape it was written for. A later
        /// build that renames these members (as a Klei fix of the bug may) leaves it out with a log
        /// line, rather than stopping start-up.
        public static void Install(Harmony harmony)
        {
            var render = AccessTools.Method(typeof(CellChangeMonitor), "RenderEveryTick");
            var pendingField = AccessTools.Field(typeof(CellChangeMonitor), "pendingDirtyTransforms");
            var objectsField = AccessTools.Field(typeof(CellChangeMonitor), "kObjectsByTransformID");
            var pendingCell = AccessTools.Field(typeof(KObject), "pendingCell");
            if (render == null || pendingField?.FieldType != typeof(HybridListHashSet<int>) || objectsField?.FieldType != typeof(Dictionary<int, KObject>)
                || pendingCell?.FieldType != typeof(int))
            {
                global::Debug.Log($"{Name}: left out, this build's CellChangeMonitor is not the one it fixes");
                return;
            }
            pending = AccessTools.FieldRefAccess<CellChangeMonitor, HybridListHashSet<int>>(pendingField);
            objects = AccessTools.FieldRefAccess<CellChangeMonitor, Dictionary<int, KObject>>(objectsField);
            harmony.Patch(render, prefix: new HarmonyMethod(typeof(DestroyedCellChangeObjects), nameof(RenderEveryTick)));
        }

        static void RenderEveryTick(CellChangeMonitor __instance)
        {
            var dirty = pending(__instance);
            var byTransform = objects(__instance);
            for (int i = 0; i < dirty.Count; i++)
            {
                int id = dirty[i];
                if (!byTransform.TryGetValue(id, out var kobj) || kobj.pendingCell != -1 || kobj.gameObject != null) continue;
                byTransform.Remove(id);
                if (logged < 10)
                {
                    logged++;
                    global::Debug.Log($"{Name}: a destroyed object's KObject {kobj.id} left the cell change monitor");
                }
            }
        }
    }
}
