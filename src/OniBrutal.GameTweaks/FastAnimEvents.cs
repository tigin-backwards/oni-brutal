using System;
using System.Collections.Generic;
using HarmonyLib;

namespace OniBrutal.GameTweaks
{
    /// Game tweak `fast-anim-events` (opt-in). Every frame AnimEventManager's loop visits each
    /// playing animation (U57: about 6,450), and for each reads its controller: whether it was
    /// destroyed, its frame index for the elapsed time (from its current animation and play
    /// mode), and its play speed. Those reads are a cache miss per controller and most of the
    /// loop's 2 ms; the loop's own entries are compact structs.
    ///
    /// The tweak runs the same loop, entry by entry in the same order with the same arithmetic,
    /// but takes those three inputs from a mirror kept per entry: the controller's current
    /// animation (frame count, total time, frame rate), play mode and speed, and whether it is
    /// destroyed. A mirror row is refreshed from the controller when its entry holds a different
    /// controller (a reference compare) or when the controller was flagged: every method that
    /// changes those inputs (StartQueuedAnim, Queue, the PlayMode setter, SwapAnims, OnDestroy)
    /// flags it. Frame events, completions and everything else run as in the game.
    ///
    /// With ONI_TWEAK_VERIFY=1 every entry's mirrored inputs are compared with the controller's
    /// own, and the controller's are used; differences are logged.
    public static class FastAnimEvents
    {
        public const string Name = "fast-anim-events";

        static readonly bool verify = Environment.GetEnvironmentVariable("ONI_TWEAK_VERIFY") == "1";
        static readonly AccessTools.FieldRef<AnimEventManager, List<KBatchedAnimController>> finishedCalls =
            AccessTools.FieldRefAccess<AnimEventManager, List<KBatchedAnimController>>("finishedCalls");
        static readonly AccessTools.FieldRef<KAnimControllerBase, KAnim.PlayMode> controllerMode = AccessTools.FieldRefAccess<KAnimControllerBase, KAnim.PlayMode>("mode");

        /// Each watched controller's index into `stale`. Indices are never reused: a row may still
        /// hold a destroyed controller, whose index stays stale.
        static readonly Dictionary<KAnimControllerBase, int> watches = new Dictionary<KAnimControllerBase, int>();
        static bool[] stale = new bool[4096];
        static int nextWatch;
        /// Rows by the game's entry list; weak, as each colony's manager has lists of its own.
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<List<AnimEventManager.EventPlayerData>, Row[]> mirrors =
            new System.Runtime.CompilerServices.ConditionalWeakTable<List<AnimEventManager.EventPlayerData>, Row[]>();
        static long checks, wrong, addedDuringLoop;

        /// A controller's inputs to the loop, as last read.
        struct Row
        {
            public KBatchedAnimController Controller;
            public int Watch;
            public bool Destroyed, HasAnim;
            public KAnim.PlayMode Mode;
            public int NumFrames;
            public float TotalTime, FrameRate, PlaySpeed;
        }

        public static void Install(Harmony harmony)
        {
            var flag = new HarmonyMethod(typeof(FastAnimEvents), nameof(Flag));
            harmony.Patch(AccessTools.Method(typeof(KAnimControllerBase), "StartQueuedAnim"), postfix: flag);
            harmony.Patch(AccessTools.Method(typeof(KAnimControllerBase), nameof(KAnimControllerBase.Queue)), postfix: flag);
            harmony.Patch(AccessTools.PropertySetter(typeof(KAnimControllerBase), nameof(KAnimControllerBase.PlayMode)), postfix: flag);
            harmony.Patch(AccessTools.Method(typeof(KBatchedAnimController), nameof(KBatchedAnimController.SwapAnims)), postfix: flag);
            harmony.Patch(AccessTools.Method(typeof(KAnimControllerBase), "OnDestroy"), postfix: new HarmonyMethod(typeof(FastAnimEvents), nameof(Destroyed)));
            harmony.Patch(AccessTools.Method(typeof(AnimEventManager), "Update", new[] { typeof(float), typeof(List<AnimEventManager.AnimData>), typeof(List<AnimEventManager.EventPlayerData>) }),
                prefix: new HarmonyMethod(typeof(FastAnimEvents), nameof(Update)));
        }

        static void Flag(KAnimControllerBase __instance)
        {
            if (watches.TryGetValue(__instance, out int watch)) stale[watch] = true;
        }

        static void Destroyed(KAnimControllerBase __instance)
        {
            if (!watches.TryGetValue(__instance, out int watch)) return;
            watches.Remove(__instance);
            stale[watch] = true; // rows holding it re-read it (and find it destroyed)
        }

        /// AnimEventManager.Update(dt, anim_data, event_data), the game's loop with mirrored inputs.
        static bool Update(AnimEventManager __instance, float dt, List<AnimEventManager.AnimData> anim_data, List<AnimEventManager.EventPlayerData> event_data)
        {
            if (dt <= 0f) return false;
            mirrors.TryGetValue(event_data, out var rows);
            var finished = finishedCalls(__instance);
            int i = 0, startCount = event_data.Count;
            // The count is read on every pass, as the game's loop does: an event can start an
            // animation (a speech event its Duplicant's mouth), which appends an entry to visit
            // in this same update.
            for (; i < event_data.Count; i++)
            {
                if (rows == null || rows.Length <= i) rows = Grow(event_data, rows);
                var data = event_data[i];
                ref var row = ref rows[i];
                if (!ReferenceEquals(row.Controller, data.controller) || stale[row.Watch]) Refresh(ref row, data.controller);
                if (verify) Verify(ref row, data.controller);
                // controller == null, as Unity compares it
                if (row.Destroyed || data.mode == KAnim.PlayMode.Paused) continue;
                data.currentFrame = row.HasAnim ? FrameIdx(row.Mode, row.NumFrames, row.TotalTime, row.FrameRate, data.elapsedTime) : -1;
                event_data[i] = data;
                for (int e = 0; e < data.events.Count; e++) data.events[e].Play(data);
                data.previousFrame = data.currentFrame;
                // The speed as the game reads it, after the events: one of them may have started
                // another animation on this controller, which flags it.
                float speed = stale[row.Watch] ? data.controller.GetPlaySpeed() : row.PlaySpeed;
                if (verify && speed != data.controller.GetPlaySpeed())
                    global::Debug.Log($"{Name}: stale play speed after the events of {data.controller.name} ({++wrong})");
                data.elapsedTime += dt * speed;
                event_data[i] = data;
                if (data.updatingEvents != null)
                    for (int e = 0; e < data.updatingEvents.Count; e++) data.updatingEvents[e].OnUpdate(data);
                event_data[i] = data;
                if (data.mode != KAnim.PlayMode.Loop && data.currentFrame >= anim_data[i].numFrames - 1)
                {
                    for (int e = 0; e < data.events.Count; e++) data.events[e].Stop(data);
                    data.updatingEvents?.Clear();
                    finished.Add(data.controller);
                }
            }
            if (verify)
            {
                // Entries started by an event during this update, visited in it as the game does.
                if (event_data.Count > startCount) addedDuringLoop += event_data.Count - startCount;
            }
            return false;
        }

        static Row[] Grow(List<AnimEventManager.EventPlayerData> event_data, Row[] rows)
        {
            var grown = new Row[Math.Max(event_data.Count, Math.Max(16, (rows?.Length ?? 0) * 2))];
            if (rows != null) Array.Copy(rows, grown, rows.Length);
            mirrors.AddOrUpdate(event_data, grown);
            return grown;
        }

        static void Refresh(ref Row row, KBatchedAnimController controller)
        {
            row.Controller = controller;
            if (controller is null)
            {
                row.Destroyed = true;
                row.Watch = 0;
                return;
            }
            if (!watches.TryGetValue(controller, out int watch))
            {
                watch = ++nextWatch;
                if (watch >= stale.Length) Array.Resize(ref stale, stale.Length * 2);
                watches[controller] = watch;
            }
            stale[watch] = false;
            row.Watch = watch;
            row.Destroyed = controller == null;
            if (row.Destroyed) return;
            var anim = controller.CurrentAnim;
            row.HasAnim = anim != null;
            row.Mode = controllerMode(controller);
            row.NumFrames = anim?.numFrames ?? 0;
            row.TotalTime = anim?.totalTime ?? 0f;
            row.FrameRate = anim?.frameRate ?? 0f;
            row.PlaySpeed = controller.GetPlaySpeed();
        }

        /// KAnim.Anim.GetFrameIdx(mode, elapsedSeconds), with the animation's numbers.
        static int FrameIdx(KAnim.PlayMode mode, int numFrames, float totalTime, float frameRate, float elapsedSeconds)
        {
            if (numFrames <= 0) return -1;
            int result = 0;
            if (mode == KAnim.PlayMode.Loop) elapsedSeconds %= totalTime;
            if (elapsedSeconds > 0f)
            {
                float num = 0.01f / frameRate;
                float num2 = elapsedSeconds * frameRate + num;
                result = Math.Min(numFrames - 1, (int)num2);
            }
            return result;
        }

        /// Test runs: the mirrored inputs against the controller's, and the controller's kept.
        static void Verify(ref Row row, KBatchedAnimController controller)
        {
            checks++;
            bool destroyed = controller == null;
            var anim = destroyed ? null : controller.CurrentAnim;
            bool same = row.Destroyed == destroyed
                        && (destroyed || (row.HasAnim == (anim != null) && row.Mode == controllerMode(controller) && row.PlaySpeed == controller.GetPlaySpeed()
                                          && row.NumFrames == (anim?.numFrames ?? 0) && row.TotalTime == (anim?.totalTime ?? 0f) && row.FrameRate == (anim?.frameRate ?? 0f)));
            if (!same)
            {
                global::Debug.Log($"{Name}: stale inputs for {(destroyed ? "a destroyed controller" : controller.name)} ({++wrong})");
                Refresh(ref row, controller);
            }
            if (checks % 2000000 == 0) global::Debug.Log($"{Name}: {checks} entries checked, {wrong} stale, {addedDuringLoop} added during the loop");
        }
    }
}
