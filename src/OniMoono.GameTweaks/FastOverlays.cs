using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace OniMoono.GameTweaks
{
    /// Game tweak `fast-overlays` (opt-in): the overlays' per-frame work, the same results.
    ///
    /// Power: OverlayModes.Power.Update tints every power building in view by its circuit's
    /// load, and for each one asks CircuitManager.GetWattsNeededWhenActive for its circuit: a
    /// walk over the circuits reachable through transformers that allocates five collections
    /// and sums every consumer on them. Within one update nothing changes a circuit (the update
    /// only tints controllers and places labels), so the answer for a circuit is the same for
    /// each of its buildings; the tweak keeps the first answer per circuit for the rest of that
    /// update. Any other call (the details screens, a mod) runs the game's method as before, and
    /// the tweak stays out of the way while another Harmony owner patches either method.
    ///
    /// Temperature: Infrared.Update triggers an event on Game every frame that reaches every
    /// object with a temperature visualizer in every world (U57: tens of thousands), and each
    /// reads its transform's position to see whether it is in view; only those in view are
    /// coloured. The tweak lets that event run as before (the same handlers, in the same order),
    /// but the visualizers' handler (the static delegate they subscribe with) is the tweak's: it
    /// keeps, by the handler's place in that order, the position read there before when the
    /// object cannot move (a spawned building other than a rocket module), and passes over a kept
    /// place out of view without touching the object. A place now held by another visualizer
    /// (one was added or removed) is read again; objects that can move read their transform
    /// every time (through the transform kept for them), as before. Those in view are coloured
    /// by the game's own code. When another Harmony owner patches the game's handler, the
    /// patched handler runs for every visualizer.
    ///
    /// Targets (the materials, decor, plant, pipe, conveyor, power, automation and exosuit
    /// overlays): each frame a mode drops the targets now out of view (Mode.RemoveOffscreenTargets),
    /// adds the objects in view (Mode.AddTargetIfVisible, for each object its partition or the
    /// scene partitioner holds there) and highlights its targets (UpdateHighlightTypeOverlay).
    /// The modes' calls to these go through the tweak's versions, which run the same loops in the
    /// same order with the same effects but keep, by place in the target set's order, what each
    /// target's visit read: its transform, its controller and, if it cannot move, its position.
    /// AddTargetIfVisible ends at once for an object already among the targets, so that test
    /// comes first; TileMode tests an object's element against each filter before adding it, so
    /// an object already shown skips that too. The KPrefabID lookup RemoveOffscreenTargets
    /// makes and never uses is left out. While another Harmony owner patches one of the game's
    /// helpers, the game's (patched) helper runs instead.
    ///
    /// With ONI_TWEAK_VERIFY=1 every kept answer is computed again and compared bit for bit,
    /// every kept position, transform and controller is compared with the object's own, and an
    /// object already among the targets runs the game's AddTargetIfVisible, which must leave the
    /// targets as they were.
    ///
    /// U60 (761015) does each part's work in the game's own rewrite of the overlays, and removes
    /// what the targets and temperature parts patch; there every part is left out with a log
    /// line (a retire candidate, docs/research/u60-tweaks-overlays.md).
    public static class FastOverlays
    {
        public const string Name = "fast-overlays";

        static readonly bool verify = Environment.GetEnvironmentVariable("ONI_TWEAK_VERIFY") == "1";
        const string Owner = "OniMoono.Host.GameTweaks";

        public static void Install(Harmony harmony)
        {
            // U60 (761015) reworked each part's target in the game itself (docs/research/
            // u60-tweaks-overlays.md). A part whose U59 shape is gone stays out with a log line,
            // so the tweak cannot misbehave there; it installs nothing on U60.
            int parts = 0;
            if (InstallPower(harmony)) parts++;
            if (InstallInfrared(harmony)) parts++;
            if (InstallTargets(harmony)) parts++;
            if (verify && parts > 0) harmony.Patch(AccessTools.Method(typeof(OverlayScreen), nameof(OverlayScreen.ToggleOverlay)), prefix: new HarmonyMethod(typeof(FastOverlays), nameof(Report)));
        }

        static void Skip(string part, string why) => global::Debug.Log($"{Name}: {part} left out, {why}");

        static bool InstallPower(Harmony harmony)
        {
            // U60's Power.Update keeps each circuit's answers itself (a CachedNetwork per circuit id).
            if (AccessTools.Inner(typeof(OverlayModes.Power), "CachedNetwork") != null)
            {
                Skip("power", "the game's Power.Update asks each circuit once an update itself");
                return false;
            }
            var update = AccessTools.Method(typeof(OverlayModes.Power), nameof(OverlayModes.Power.Update));
            var needed = AccessTools.Method(typeof(CircuitManager), nameof(CircuitManager.GetWattsNeededWhenActive));
            powerPatched = new System.Reflection.MethodBase[] { update, needed };
            harmony.Patch(update, prefix: new HarmonyMethod(typeof(FastOverlays), nameof(PowerBegin)) { priority = Priority.Last },
                finalizer: new HarmonyMethod(typeof(FastOverlays), nameof(PowerEnd)));
            harmony.Patch(needed, prefix: new HarmonyMethod(typeof(FastOverlays), nameof(NeededBefore)) { priority = Priority.Last },
                postfix: new HarmonyMethod(typeof(FastOverlays), nameof(NeededAfter)));
            return true;
        }

        static bool InstallInfrared(Harmony harmony)
        {
            // U60: the visualizers no longer subscribe to Game's infrared event; Infrared.Update
            // colours only those whose controller is visible (UpdateAllVisible).
            var dispatcher = AccessTools.DeclaredField(typeof(TemperatureOverlayInfraredVisualizerBase), "OverlayInfraredUpdateDispatcher");
            var dispatch = AccessTools.DeclaredMethod(typeof(TemperatureOverlayInfraredVisualizerBase), "OnTemperatureOverlayInfraredUpdate");
            if (dispatcher == null || dispatch == null)
            {
                Skip("temperature", "the game colours only the visualizers in view itself");
                return false;
            }
            gameVisit = AccessTools.MethodDelegate<Action<TemperatureOverlayInfraredVisualizerBase, object>>(dispatch);
            infraredPatched = new System.Reflection.MethodBase[] { dispatch };
            harmony.Patch(AccessTools.Method(typeof(Infrared), "Update"), prefix: new HarmonyMethod(typeof(FastOverlays), nameof(InfraredBegin)));
            // Visualizers subscribe with this delegate as they are made, after the tweaks install.
            dispatcher.SetValue(null, (Action<object, object>)InfraredDispatch);
            return true;
        }

        static bool InstallTargets(Harmony harmony)
        {
            // The U59 helpers this part stands in for: ICollection targets, one AddTargetIfVisible
            // with should_add, UpdateHighlightTypeOverlay and TileMode.TryAddObject. U60 replaced
            // them with HybridListHashSet overloads and RemoveOffscreenAndUpdateHighlights.
            var declared = AccessTools.GetDeclaredMethods(typeof(OverlayModes.Mode));
            static bool OnCollection(System.Reflection.MethodInfo m, int index)
            {
                var parameters = m.GetParameters();
                return parameters.Length > index && parameters[index].ParameterType.IsGenericType
                    && parameters[index].ParameterType.GetGenericTypeDefinition() == typeof(ICollection<>);
            }
            addTarget = declared.FirstOrDefault(m => m.Name == "AddTargetIfVisible" && m.GetParameters().Length == 7 && OnCollection(m, 3));
            removeOffscreen = declared.FirstOrDefault(m => m.Name == "RemoveOffscreenTargets" && m.GetParameters().Length == 4 && OnCollection(m, 0));
            highlight = declared.FirstOrDefault(m => m.Name == "UpdateHighlightTypeOverlay" && OnCollection(m, 2));
            clearOutside = declared.FirstOrDefault(m => m.Name == "ClearOutsideViewObjects" && m.GetParameters().Length == 5 && m.GetParameters()[1].ParameterType == typeof(Vector2I) && OnCollection(m, 0));
            var tryAdd = AccessTools.DeclaredMethod(typeof(OverlayModes.TileMode), "TryAddObject");
            var layerTargets = AccessTools.DeclaredField(typeof(OverlayModes.TileMode), "layerTargets");
            if (addTarget == null || removeOffscreen == null || highlight == null || clearOutside == null || tryAdd == null
                || layerTargets?.FieldType != typeof(HashSet<PrimaryElement>))
            {
                Skip("targets", "the game's overlay target helpers are not the U59 ones it stands in for");
                return false;
            }
            tileTargets = AccessTools.FieldRefAccess<OverlayModes.TileMode, HashSet<PrimaryElement>>(layerTargets);
            var reroute = new HarmonyMethod(typeof(FastOverlays), nameof(RerouteAddTarget));
            foreach (var (type, method) in new[] { (typeof(OverlayModes.ConduitMode), "Update"), (typeof(OverlayModes.Crop), "Update"), (typeof(OverlayModes.Harvest), "Update"),
                         (typeof(OverlayModes.Decor), "Update"), (typeof(OverlayModes.Logic), "Update"), (typeof(OverlayModes.Power), "Update"),
                         (typeof(OverlayModes.SolidConveyor), "Update"), (typeof(OverlayModes.Suit), "Update"), (typeof(OverlayModes.TileMode), "TryAddObject"),
                         (typeof(OverlayModes.TileMode), "Update") })
                harmony.Patch(AccessTools.Method(type, method), transpiler: reroute);
            harmony.Patch(tryAdd, prefix: new HarmonyMethod(typeof(FastOverlays), nameof(TileTarget)) { priority = Priority.Last });
            return true;
        }

        /// Test runs: what each part checked so far, as each overlay closes.
        static void Report()
        {
            if (targetsChecked > 0) global::Debug.Log($"{Name}: {targetsChecked} kept targets checked, {targetsWrong} wrong");
            if (rowsChecked > 0) global::Debug.Log($"{Name}: {rowsChecked} kept rows checked, {rowsWrong} wrong");
            if (neededChecked > 0) global::Debug.Log($"{Name}: {neededChecked} power answers checked, {neededWrong} wrong");
            if (positionsChecked > 0) global::Debug.Log($"{Name}: {positionsChecked} kept positions checked, {positionsRead} read, {positionsWrong} wrong");
        }

        // ---- Targets already shown ----

        static System.Reflection.MethodInfo addTarget;
        static bool targetsForeign;
        static int targetChecks;
        static long targetsKept, targetsChecked, targetsWrong;
        /// Set when the part installs (the field is a HybridListHashSet from U60).
        static AccessTools.FieldRef<OverlayModes.TileMode, HashSet<PrimaryElement>> tileTargets;

        /// The modes' calls to Mode.AddTargetIfVisible, RemoveOffscreenTargets (the overload for
        /// components) and UpdateHighlightTypeOverlay go through the tweak's, same arguments.
        static IEnumerable<CodeInstruction> RerouteAddTarget(IEnumerable<CodeInstruction> instructions, System.Reflection.MethodBase original)
        {
            int rerouted = 0;
            foreach (var instruction in instructions)
            {
                if ((instruction.opcode == System.Reflection.Emit.OpCodes.Call || instruction.opcode == System.Reflection.Emit.OpCodes.Callvirt)
                    && instruction.operand is System.Reflection.MethodInfo m && m.IsGenericMethod)
                {
                    var definition = m.GetGenericMethodDefinition();
                    string ours = definition == addTarget ? nameof(AddTarget) : definition == removeOffscreen ? nameof(RemoveOffscreen) : definition == highlight ? nameof(Highlight) : null;
                    if (ours != null)
                    {
                        instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                        instruction.operand = AccessTools.Method(typeof(FastOverlays), ours).MakeGenericMethod(m.GetGenericArguments());
                        rerouted++;
                    }
                }
                yield return instruction;
            }
            // Each patched method calls at least one helper; none found means the game changed.
            if (rerouted == 0) throw new InvalidOperationException($"{Name}: {original.DeclaringType}.{original.Name} calls none of the helpers it reroutes");
        }

        /// The game's AddTargetIfVisible without its own visit to an object already among the
        /// targets: the method's test for that ends it with nothing done.
        static void AddTarget<T>(OverlayModes.Mode mode, T instance, Vector2I visMin, Vector2I visMax, ICollection<T> targets, int layer, Action<T> onAdded, Func<KMonoBehaviour, bool> shouldAdd)
            where T : IUniformGridObject
        {
            if (targetChecks++ % 100000 == 0) targetsForeign = Foreign(new System.Reflection.MethodBase[] { addTarget }, out _);
            if ((object)instance != null && !targetsForeign && targets.Contains(instance))
            {
                targetsKept++;
                if (!verify) return;
                int count = targets.Count;
                Original<T>.AddTarget(mode, instance, visMin, visMax, targets, layer, onAdded, shouldAdd);
                targetsChecked++;
                if (targets.Count != count || !targets.Contains(instance)) global::Debug.Log($"{Name}: a target already shown changed the targets ({++targetsWrong})");
                return;
            }
            Original<T>.AddTarget(mode, instance, visMin, visMax, targets, layer, onAdded, shouldAdd);
        }

        static class Original<T>
        {
            public static readonly Action<OverlayModes.Mode, T, Vector2I, Vector2I, ICollection<T>, int, Action<T>, Func<KMonoBehaviour, bool>> AddTarget =
                (Action<OverlayModes.Mode, T, Vector2I, Vector2I, ICollection<T>, int, Action<T>, Func<KMonoBehaviour, bool>>)Delegate.CreateDelegate(
                    typeof(Action<OverlayModes.Mode, T, Vector2I, Vector2I, ICollection<T>, int, Action<T>, Func<KMonoBehaviour, bool>>),
                    addTarget.MakeGenericMethod(typeof(T)));
        }

        /// The game's helpers the kept rows stand in for, for when another owner patches them.
        static class OriginalRows<T>
        {
            public static readonly Action<ICollection<T>, Vector2I, Vector2I, Action<T>> RemoveOffscreen =
                (Action<ICollection<T>, Vector2I, Vector2I, Action<T>>)Delegate.CreateDelegate(
                    typeof(Action<ICollection<T>, Vector2I, Vector2I, Action<T>>), removeOffscreen.MakeGenericMethod(typeof(T)));
            public static readonly Action<OverlayModes.Mode, Vector2I, Vector2I, ICollection<T>, ICollection<Tag>, OverlayModes.ColorHighlightCondition[], OverlayModes.BringToFrontLayerSetting, int> Highlight =
                (Action<OverlayModes.Mode, Vector2I, Vector2I, ICollection<T>, ICollection<Tag>, OverlayModes.ColorHighlightCondition[], OverlayModes.BringToFrontLayerSetting, int>)Delegate.CreateDelegate(
                    typeof(Action<OverlayModes.Mode, Vector2I, Vector2I, ICollection<T>, ICollection<Tag>, OverlayModes.ColorHighlightCondition[], OverlayModes.BringToFrontLayerSetting, int>),
                    highlight.MakeGenericMethod(typeof(T)));
        }

        static bool rowsForeign;
        static int rowChecks;

        /// Whether another owner patches a helper the kept rows stand in for (checked now and then).
        static bool RowsForeign()
        {
            if (rowChecks++ % 3000 == 0)
                rowsForeign = Foreign(new System.Reflection.MethodBase[] { removeOffscreen, highlight, clearOutside }, out _);
            return rowsForeign;
        }

        // ---- Targets' kept rows ----

        static System.Reflection.MethodInfo removeOffscreen, highlight, clearOutside;
        static readonly Action<KBatchedAnimController> resetDisplay = AccessTools.MethodDelegate<Action<KBatchedAnimController>>(
            AccessTools.Method(typeof(OverlayModes.Mode), "ResetDisplayValues", new[] { typeof(KBatchedAnimController) }));
        static long rowsKept, rowsChecked, rowsWrong;

        /// What a target's visit read before: its transform and controller (a component's own,
        /// unchanged while they live) and, if it cannot move, its position.
        struct Row
        {
            public object Target;
            public UnityEngine.Transform Transform;
            public KBatchedAnimController Controller;
            public UnityEngine.Vector3 Position;
            public bool Fixed;
        }

        /// A walk's rows, by place in its target set's order: one per set and helper.
        sealed class Rows
        {
            public Row[] Items = new Row[256];
        }

        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Rows> removeRows = new System.Runtime.CompilerServices.ConditionalWeakTable<object, Rows>();
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, Rows> highlightRows = new System.Runtime.CompilerServices.ConditionalWeakTable<object, Rows>();
        static readonly List<KMonoBehaviour> leaving = new List<KMonoBehaviour>();

        /// The target's row at place i, made again when another target holds it.
        static ref Row RowOf(Rows rows, int i, KMonoBehaviour target)
        {
            if (i >= rows.Items.Length) Array.Resize(ref rows.Items, Math.Max(i + 1, rows.Items.Length * 2));
            ref var row = ref rows.Items[i];
            if (!ReferenceEquals(row.Target, target))
            {
                row.Target = target;
                row.Transform = target.transform;
                row.Controller = null;
                row.Fixed = CannotMove(target);
                if (row.Fixed) row.Position = row.Transform.GetPosition();
            }
            else rowsKept++;
            return ref row;
        }

        /// The target's position, kept if it cannot move.
        static UnityEngine.Vector3 PositionOf(ref Row row, KMonoBehaviour target)
        {
            if (!row.Fixed) return row.Transform.GetPosition();
            if (verify)
            {
                rowsChecked++;
                var now = target.transform.GetPosition();
                // Only x and y are read (the overlays move some targets in depth).
                if (now.x != row.Position.x || now.y != row.Position.y || !ReferenceEquals(row.Transform, target.transform))
                    global::Debug.Log($"{Name}: {target.name} moved from {row.Position} to {now} ({++rowsWrong})");
                return now;
            }
            return row.Position;
        }

        /// Mode.RemoveOffscreenTargets(targets, min, max, on_removed): the game's
        /// ClearOutsideViewObjects with the targets' transforms and fixed positions kept, and
        /// its unused KPrefabID lookup left out (it filters only by prefab, and is given none).
        static void RemoveOffscreen<T>(ICollection<T> targets, Vector2I visMin, Vector2I visMax, Action<T> onRemoved) where T : KMonoBehaviour
        {
            if (RowsForeign())
            {
                OriginalRows<T>.RemoveOffscreen(targets, visMin, visMax, onRemoved);
                return;
            }
            var rows = removeRows.GetValue(targets, _ => new Rows());
            leaving.Clear();
            int i = 0, active = ClusterManager.Instance.activeWorldId;
            foreach (T target in targets)
            {
                int place = i++;
                if (target == null) continue;
                ref var row = ref RowOf(rows, place, target);
                Vector2I xy = Grid.PosToXY(PositionOf(ref row, target));
                if (!(visMin <= xy) || !(xy <= visMax) || WorldOf(ref row, target) != active) leaving.Add(target);
            }
            foreach (var removed in leaving)
            {
                var target = (T)removed;
                if (target == null) continue;
                // The game's on_remove: the controller's display reset, then the caller's.
                var controller = target.GetComponent<KBatchedAnimController>();
                if (controller != null) resetDisplay(controller);
                onRemoved?.Invoke(target);
                targets.Remove(target);
            }
            leaving.Clear();
        }

        /// GameObject.GetMyWorldId, from the kept position.
        static int WorldOf(ref Row row, KMonoBehaviour target)
        {
            int cell = Grid.PosToCell(PositionOf(ref row, target));
            return Grid.IsValidCell(cell) && Grid.WorldIdx[cell] != byte.MaxValue ? Grid.WorldIdx[cell] : -1;
        }

        /// Mode.UpdateHighlightTypeOverlay, the game's loop with the targets' transforms,
        /// controllers and fixed positions kept.
        static void Highlight<T>(OverlayModes.Mode mode, Vector2I min, Vector2I max, ICollection<T> targets, ICollection<Tag> itemIds,
            OverlayModes.ColorHighlightCondition[] highlights, OverlayModes.BringToFrontLayerSetting bringToFront, int layer) where T : KMonoBehaviour
        {
            if (RowsForeign())
            {
                OriginalRows<T>.Highlight(mode, min, max, targets, itemIds, highlights, bringToFront, layer);
                return;
            }
            var rows = highlightRows.GetValue(targets, _ => new Rows());
            int i = 0;
            foreach (T target in targets)
            {
                int place = i++;
                if (target == null) continue;
                ref var row = ref RowOf(rows, place, target);
                var position = PositionOf(ref row, target);
                int cell = Grid.PosToCell(position);
                if (!Grid.IsValidCell(cell) || !Grid.IsVisible(cell) || !(min <= position) || !(position <= max)) continue;
                var controller = row.Controller;
                if (controller == null)
                {
                    controller = target.GetComponent<KBatchedAnimController>();
                    row.Controller = controller;
                }
                else if (verify)
                {
                    rowsChecked++;
                    if (!ReferenceEquals(controller, target.GetComponent<KBatchedAnimController>())) global::Debug.Log($"{Name}: {target.name} has another controller ({++rowsWrong})");
                }
                if (controller == null) continue;
                int targetLayer = 0;
                UnityEngine.Color32 colour = UnityEngine.Color.clear;
                if (highlights != null)
                    foreach (var condition in highlights)
                        if (condition.highlight_condition(target))
                        {
                            colour = condition.highlight_color(target);
                            targetLayer = layer;
                            break;
                        }
                switch (bringToFront)
                {
                    case OverlayModes.BringToFrontLayerSetting.Constant: controller.SetLayer(layer); break;
                    case OverlayModes.BringToFrontLayerSetting.Conditional: controller.SetLayer(targetLayer); break;
                }
                controller.HighlightColour = colour;
            }
        }

        /// TileMode.TryAddObject tests the object's element against each filter before its
        /// AddTargetIfVisible; an object already shown is left there either way.
        static bool TileTarget(OverlayModes.TileMode __instance, PrimaryElement pe, bool __runOriginal)
        {
            if (!__runOriginal || verify || targetsForeign || (object)pe == null || !tileTargets(__instance).Contains(pe)) return __runOriginal;
            targetsKept++;
            return false;
        }

        // ---- Temperature (infrared) ----

        static System.Reflection.MethodBase[] infraredPatched;
        static readonly Action<TemperatureOverlayInfraredVisualizerBase, Infrared.TemperatureOverlayInfraredData> colour =
            AccessTools.MethodDelegate<Action<TemperatureOverlayInfraredVisualizerBase, Infrared.TemperatureOverlayInfraredData>>(
                AccessTools.Method(typeof(TemperatureOverlayInfraredVisualizerBase), "TemperatureOverlayInfraredUpdate"));

        /// The game's handler, as a patch of it would have it run.
        /// Set when the part installs (U60 has no such handler).
        static Action<TemperatureOverlayInfraredVisualizerBase, object> gameVisit;

        /// A visualizer at its place in the event's handler order, and its position if it cannot move.
        struct Place
        {
            public object Visualizer;
            /// The visualizer's transform (a component's never changes), read without it.
            public UnityEngine.Transform Transform;
            public UnityEngine.Vector3 Position;
            public bool Fixed;
        }

        static Place[] places = new Place[1024];
        static int nextPlace, placesUsed, infraredUpdates;
        static bool infraredForeign;
        static long positionsKept, positionsRead, positionsChecked, positionsWrong;

        static void InfraredBegin(bool __runOriginal)
        {
            // The places past the last update's visualizers let go of theirs.
            if (nextPlace < placesUsed) Array.Clear(places, nextPlace, placesUsed - nextPlace);
            placesUsed = nextPlace;
            nextPlace = 0;
            if (__runOriginal && infraredUpdates++ % 3000 == 0) infraredForeign = Foreign(infraredPatched, out _);
        }

        /// The visualizers' handler (the game's dispatcher, replaced): the game's test, with the
        /// position kept for objects that cannot move. A kept place out of view is passed over
        /// without touching the visualizer at all.
        static void InfraredDispatch(object context, object obj)
        {
            if (infraredForeign)
            {
                gameVisit((TemperatureOverlayInfraredVisualizerBase)context, obj);
                return;
            }
            var data = (Infrared.TemperatureOverlayInfraredData)obj;
            int i = nextPlace++;
            if (i >= places.Length) Array.Resize(ref places, places.Length * 2);
            ref var place = ref places[i];
            UnityEngine.Vector3 position;
            TemperatureOverlayInfraredVisualizerBase visualizer;
            if (ReferenceEquals(place.Visualizer, context) && place.Fixed)
            {
                position = place.Position;
                positionsKept++;
                if (verify)
                {
                    visualizer = (TemperatureOverlayInfraredVisualizerBase)context;
                    var now = visualizer.transform.GetPosition();
                    positionsChecked++;
                    if (now.x != position.x || now.y != position.y)
                        global::Debug.Log($"{Name}: {visualizer.name} moved from {position} to {now} ({++positionsWrong})");
                    position = now;
                }
                if (!(data.bounds.Min <= position && position <= data.bounds.Max)) return;
                visualizer = (TemperatureOverlayInfraredVisualizerBase)context;
            }
            else if (ReferenceEquals(place.Visualizer, context))
            {
                // An object that can move: its position, from the transform kept for it.
                position = place.Transform.GetPosition();
                positionsRead++;
                if (verify)
                {
                    positionsChecked++;
                    var now = ((TemperatureOverlayInfraredVisualizerBase)context).transform;
                    if (!ReferenceEquals(now, place.Transform)) global::Debug.Log($"{Name}: {now.name} has another transform ({++positionsWrong})");
                }
                if (!(data.bounds.Min <= position && position <= data.bounds.Max)) return;
                visualizer = (TemperatureOverlayInfraredVisualizerBase)context;
            }
            else
            {
                visualizer = (TemperatureOverlayInfraredVisualizerBase)context;
                place.Visualizer = context;
                place.Transform = visualizer.transform;
                place.Fixed = CannotMove(visualizer);
                position = place.Transform.GetPosition();
                place.Position = position;
                positionsRead++;
                if (!(data.bounds.Min <= position && position <= data.bounds.Max)) return;
            }
            colour(visualizer, data);
        }

        /// A spawned building, other than a rocket module (rockets fly), keeps its place.
        static bool CannotMove(KMonoBehaviour component) =>
            component.isSpawned && component.GetComponent<BuildingComplete>() != null
            && component.GetComponent<RocketModule>() == null && component.GetComponent<RocketModuleCluster>() == null;

        // ---- Power ----

        static System.Reflection.MethodBase[] powerPatched;
        /// Each circuit's answer so far in the running Power.Update.
        static readonly Dictionary<ushort, float> wattsNeeded = new Dictionary<ushort, float>();
        static bool inPowerUpdate, powerForeign;
        static int powerUpdates;
        static long neededKept, neededChecked, neededWrong;

        static void PowerBegin(bool __runOriginal)
        {
            if (!__runOriginal) return;
            // Mods patch while loading; checked again now and then for late ones.
            if (powerUpdates++ % 3000 == 0) powerForeign = Foreign(powerPatched, out _);
            wattsNeeded.Clear();
            inPowerUpdate = !powerForeign;
        }

        static Exception PowerEnd(Exception __exception)
        {
            inPowerUpdate = false;
            wattsNeeded.Clear();
            return __exception;
        }

        /// __state: 0 the game's call, 1 a first answer to keep, 2 a kept answer to verify.
        static bool NeededBefore(ushort originCircuitId, ref float __result, out int __state, bool __runOriginal)
        {
            __state = 0;
            if (!inPowerUpdate || !__runOriginal) return __runOriginal;
            if (!wattsNeeded.TryGetValue(originCircuitId, out float kept))
            {
                __state = 1;
                return true;
            }
            neededKept++;
            if (verify)
            {
                __state = 2;
                return true;
            }
            __result = kept;
            return false;
        }

        static void NeededAfter(ushort originCircuitId, float __result, int __state)
        {
            if (__state == 1) wattsNeeded[originCircuitId] = __result;
            else if (__state == 2)
            {
                neededChecked++;
                if (BitConverter.SingleToInt32Bits(wattsNeeded[originCircuitId]) != BitConverter.SingleToInt32Bits(__result))
                    global::Debug.Log($"{Name}: circuit {originCircuitId} needs {__result} W, kept {wattsNeeded[originCircuitId]} W ({++neededWrong})");
            }
        }

        /// Whether a Harmony owner other than the tweaks patches one of the methods.
        static bool Foreign(System.Reflection.MethodBase[] methods, out string owner)
        {
            owner = null;
            foreach (var method in methods)
            {
                var info = Harmony.GetPatchInfo(method);
                if (info == null) continue;
                // PerfProbe's stopwatch (test infrastructure) only times the methods it wraps.
                foreach (var o in info.Owners)
                    if (o != Owner && o != "PerfProbe.WallTime") { owner = o; return true; }
            }
            return false;
        }
    }
}
