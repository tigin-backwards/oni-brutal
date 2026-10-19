using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host.GameTweaks
{
    /// Game tweak `fast-particle-collision` (opt-in). Every frame, each radbolt
    /// (HighEnergyParticle.CheckCollision) gathers every collider in the 3×3 cells around it and
    /// calls GetComponent&lt;HighEnergyParticle&gt;() on each one's GameObject to find other
    /// radbolts. A colony has tens of thousands of colliders (every stored item) and only a handful
    /// of radbolts, so nearly all of those lookups scan an item's components and find nothing
    /// (U57: 0.9 ms a frame for 4 radbolts).
    ///
    /// Two changes, each giving the method the same outcome:
    /// - The gather is skipped when no other radbolt has a collider registered in those cells (the
    ///   colliders' partitioner registrations, KCollider2D.cachedExtents while its entry is valid,
    ///   are checked against the 3×3 area). The gathered entries only matter when they include
    ///   another radbolt's collider, so an empty list gives the same result.
    /// - When it gathers, GetComponent&lt;HighEnergyParticle&gt;() on each collider's GameObject is a
    ///   lookup among the live radbolt components (registered as they are constructed): a radbolt
    ///   carries one HighEnergyParticle (its prefab), so the lookup returns what GetComponent does.
    /// With ONI_TWEAK_VERIFY=1 (test runs) the skipped gathers are still made and checked for
    /// another radbolt, and both lookups are compared; differences are logged.
    internal static class FastParticleCollision
    {
        public const string Name = "fast-particle-collision";

        static readonly List<UnityEngine.Component> live = new();
        static readonly bool verify = Environment.GetEnvironmentVariable("ONI_TWEAK_VERIFY") == "1";
        static long checks, mismatches, skipped, gathered, wrongSkips;
        static MethodInfo getComponent = null!, gatherEntries = null!;
        static Action<object, int, int, int, int, object, object> gather = null!;
        /// A collider's partitioner registration: valid, and the cells it covers.
        static Func<object, (bool Valid, int X, int Y, int W, int H)> registration = null!;
        static Type colliderType = null!;

        public static void Install(Harmony harmony)
        {
            var particle = AccessTools.TypeByName("HighEnergyParticle");
            getComponent = AccessTools.Method(typeof(UnityEngine.GameObject), nameof(UnityEngine.GameObject.GetComponent), Type.EmptyTypes).MakeGenericMethod(particle);
            colliderType = AccessTools.TypeByName("KCollider2D");
            var partitioner = AccessTools.TypeByName("GameScenePartitioner");
            gatherEntries = AccessTools.Method(partitioner, "GatherEntries", [typeof(int), typeof(int), typeof(int), typeof(int), AccessTools.TypeByName("ScenePartitionerLayer"), typeof(List<>).MakeGenericType(AccessTools.TypeByName("ScenePartitionerEntry"))]);
            gather = CompileGather();
            registration = CompileRegistration();
            harmony.Patch(AccessTools.Constructor(particle), postfix: new HarmonyMethod(typeof(FastParticleCollision), nameof(Constructed)));
            harmony.Patch(AccessTools.Method(particle, "CheckCollision"), transpiler: new HarmonyMethod(typeof(FastParticleCollision), nameof(Transpiler)));
        }

        static void Constructed(UnityEngine.Component __instance)
        {
            lock (live) live.Add(__instance);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var replacement = AccessTools.Method(typeof(FastParticleCollision), nameof(ParticleOf));
            var gatherIfNeeded = AccessTools.Method(typeof(FastParticleCollision), nameof(GatherIfNeeded));
            int replaced = 0, gathers = 0;
            foreach (var i in instructions)
            {
                if (i.Calls(getComponent))
                {
                    replaced++;
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, replacement) { labels = i.labels, blocks = i.blocks };
                }
                else if (i.Calls(gatherEntries))
                {
                    gathers++;
                    // The particle doing the check, as a last argument.
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0) { labels = i.labels, blocks = i.blocks };
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, gatherIfNeeded);
                }
                else yield return i;
            }
            if (replaced != 1 || gathers != 1) throw new InvalidOperationException($"{Name}: expected one GetComponent<HighEnergyParticle>() and one GatherEntries in CheckCollision, found {replaced} and {gathers}");
        }

        /// GameScenePartitioner.GatherEntries(x, y, width, height, layer, list), made only when another
        /// radbolt has a collider registered in the area.
        static void GatherIfNeeded(object partitioner, int x, int y, int width, int height, object layer, object list, UnityEngine.Component self)
        {
            if (AnotherParticleIn(self, x, y, width, height))
            {
                gathered++;
                gather(partitioner, x, y, width, height, layer, list);
                return;
            }
            skipped++;
            if (!verify) return;
            gather(partitioner, x, y, width, height, layer, list);
            foreach (var entry in (System.Collections.IEnumerable)list)
            {
                var obj = AccessTools.Field(entry.GetType(), "obj").GetValue(entry) as UnityEngine.Component;
                if (obj == null) continue;
                var other = getComponent.Invoke(obj.gameObject, null) as UnityEngine.Component;
                if (other != null && !ReferenceEquals(other, self)) EngineLog.Engine($"{Name}: a skipped gather held radbolt {other.name} ({++wrongSkips})");
            }
            ((System.Collections.IList)list).Clear();
            if ((skipped + gathered) % 20000 == 0) EngineLog.Engine($"{Name}: {skipped} gathers skipped, {gathered} made, {wrongSkips} wrong skips");
        }

        static bool AnotherParticleIn(UnityEngine.Component self, int x, int y, int width, int height)
        {
            lock (live)
                foreach (var c in live)
                {
                    if (ReferenceEquals(c, self) || c == null) continue;
                    foreach (var collider in c.gameObject.GetComponents(colliderType))
                    {
                        var (valid, cx, cy, cw, ch) = registration(collider);
                        if (valid && cx < x + width && x < cx + cw && cy < y + height && y < cy + ch) return true;
                    }
                }
            return false;
        }

        static Action<object, int, int, int, int, object, object> CompileGather()
        {
            var ps = new[] { typeof(object), typeof(int), typeof(int), typeof(int), typeof(int), typeof(object), typeof(object) }
                .Select((t, i) => System.Linq.Expressions.Expression.Parameter(t, "p" + i)).ToArray();
            var args = gatherEntries.GetParameters();
            var call = System.Linq.Expressions.Expression.Call(System.Linq.Expressions.Expression.Convert(ps[0], gatherEntries.DeclaringType!), gatherEntries,
                ps[1], ps[2], ps[3], ps[4], System.Linq.Expressions.Expression.Convert(ps[5], args[4].ParameterType), System.Linq.Expressions.Expression.Convert(ps[6], args[5].ParameterType));
            return System.Linq.Expressions.Expression.Lambda<Action<object, int, int, int, int, object, object>>(call, ps).Compile();
        }

        static Func<object, (bool, int, int, int, int)> CompileRegistration()
        {
            var arg = System.Linq.Expressions.Expression.Parameter(typeof(object), "collider");
            var collider = System.Linq.Expressions.Expression.Convert(arg, colliderType);
            var entry = System.Linq.Expressions.Expression.Field(collider, AccessTools.Field(colliderType, "partitionerEntry"));
            var valid = System.Linq.Expressions.Expression.Call(entry, entry.Type.GetMethod("IsValid", Type.EmptyTypes)!);
            var extents = System.Linq.Expressions.Expression.Field(collider, AccessTools.Field(colliderType, "cachedExtents"));
            var tuple = typeof(ValueTuple<bool, int, int, int, int>).GetConstructor([typeof(bool), typeof(int), typeof(int), typeof(int), typeof(int)])!;
            var body = System.Linq.Expressions.Expression.New(tuple, valid,
                System.Linq.Expressions.Expression.Field(extents, "x"), System.Linq.Expressions.Expression.Field(extents, "y"),
                System.Linq.Expressions.Expression.Field(extents, "width"), System.Linq.Expressions.Expression.Field(extents, "height"));
            return System.Linq.Expressions.Expression.Lambda<Func<object, (bool, int, int, int, int)>>(body, arg).Compile();
        }

        /// GameObject.GetComponent&lt;HighEnergyParticle&gt;() for a collider's GameObject.
        static UnityEngine.Component? ParticleOf(UnityEngine.GameObject go)
        {
            UnityEngine.Component? found = null;
            lock (live)
            {
                for (int i = live.Count - 1; i >= 0; i--)
                {
                    var c = live[i];
                    if (c == null) { live.RemoveAt(i); continue; }   // destroyed
                    if (found == null && ReferenceEquals(c.gameObject, go)) found = c;
                }
            }
            if (verify)
            {
                var expected = (UnityEngine.Component?)getComponent.Invoke(go, null);
                checks++;
                if (!ReferenceEquals(expected, found) && !(expected == null && found == null))
                    EngineLog.Engine($"{Name}: mismatch {++mismatches} of {checks} on {go.name}");
                if (checks % 100000 == 0) EngineLog.Engine($"{Name}: {checks} checks, {mismatches} mismatches");
            }
            return found;
        }
    }
}
