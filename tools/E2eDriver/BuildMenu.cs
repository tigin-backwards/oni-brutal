using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace E2eDriver
{
    /// buildmenu.settled: whether the build menu (PlanScreen) shows what the game would compute
    /// for it now. The game catches up over frames: WorldInventory recounts one tag a frame,
    /// PlanScreen re-checks ten BuildingDefs' requirements a frame and repaints ten building
    /// buttons a frame (and one category button), so after a change a button can show a stale
    /// state for dozens of frames. The menu is settled when, all at once:
    ///
    /// - every world's WorldInventory holds, for each tag, the amount its Update would count now;
    /// - every building button is shown or hidden as the game's filter decides now (the
    ///   category's research filter, or the search's scores);
    /// - every shown button's PlanScreen cache holds GetBuildableStateForDef's state now, and
    ///   the button's MultiToggle state, icon material and research overlay show that state;
    /// - no shown button was repainted earlier in this frame: a repaint sets its label's font
    ///   size, which TextMeshPro's auto-sizing replaces only at the frame's end, so a dump would
    ///   read a size that depends on where the round-robin repaint happens to be;
    /// - every category button's images, visibility and research icon show what its buildings'
    ///   states give now;
    /// - no panel resize is pending and the panel's expand or collapse has ended.
    ///
    /// The states are computed by the game's own methods (GetBuildableStateForDef,
    /// TechRequirementsMet, the search caches' IsPassingScore); only the inventory recount
    /// repeats WorldInventory.Update's loop, which is not a method of its own.
    static class BuildMenu
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static FieldInfo F(Type type, string name) =>
            type.GetField(name, Any) ?? throw new DriverError($"no field {name} on {type.FullName}");

        static readonly FieldInfo AllToggles = F(typeof(PlanScreen), "allBuildingToggles");
        static readonly FieldInfo States = F(typeof(PlanScreen), "_buildableStatesByID");
        static readonly FieldInfo PanelSizePending = F(typeof(PlanScreen), "categoryPanelSizeNeedsRefresh");
        static readonly FieldInfo DefSearch = F(typeof(PlanScreen), "buildingDefSearchCaches");
        static readonly FieldInfo SubcategorySearch = F(typeof(PlanScreen), "subcategorySearchCachesByBuildingPrefab");
        static readonly FieldInfo Entries = F(typeof(PlanScreen), "toggleEntries");
        static readonly FieldInfo RefreshIndex = F(typeof(PlanScreen), "building_button_refresh_idx");
        static readonly FieldInfo RefreshPerFrame = F(typeof(PlanScreen), "maxToggleRefreshPerFrame");
        static readonly Func<PlanScreen, BuildingDef, PlanScreen.RequirementsState> StateForDef =
            (Func<PlanScreen, BuildingDef, PlanScreen.RequirementsState>)Delegate.CreateDelegate(
                typeof(Func<PlanScreen, BuildingDef, PlanScreen.RequirementsState>),
                typeof(PlanScreen).GetMethod("GetBuildableStateForDef", Any)
                    ?? throw new DriverError("no method GetBuildableStateForDef on PlanScreen"));
        static readonly FieldInfo ToggleDef = F(typeof(PlanBuildingToggle), "def");
        static readonly FieldInfo ToggleCategory = F(typeof(PlanBuildingToggle), "buildingCategory");
        static readonly FieldInfo ToggleTech = F(typeof(PlanBuildingToggle), "techItem");
        static readonly FieldInfo ToggleMulti = F(typeof(PlanBuildingToggle), "toggle");
        static readonly FieldInfo ToggleIcon = F(typeof(PlanBuildingToggle), "buildingIcon");
        static readonly FieldInfo Inventory = F(typeof(WorldInventory), "Inventory");
        static readonly FieldInfo ImageState = F(typeof(ImageToggleState), "currentState");

        // PlanScreen.ToggleEntry is private: its members by reflection, found on first use.
        static FieldInfo entryInfo, entryDefs, entryImages, entryHide;
        static MethodInfo entryTechAvailable;

        const int MaxReasons = 12;

        /// One check now: {settled, open, visible, reasons, ms}. `open` is whether a category
        /// (or a search's results) is open; a closed menu checks only the category buttons.
        public static JObject Check()
        {
            var watch = Stopwatch.StartNew();
            var reasons = new List<string>();
            int visible = 0, stale = 0;
            bool open = false;
            void Stale(string reason)
            {
                stale++;
                if (reasons.Count < MaxReasons) reasons.Add(reason);
            }

            var screen = PlanScreen.Instance;
            bool present = screen != null && screen.isActiveAndEnabled && Game.Instance != null;
            if (present)
            {
                CheckInventories(Stale);
                bool instant = DebugHandler.InstantBuildMode || Game.Instance.SandboxModeActive;
                var cache = (Dictionary<string, PlanScreen.RequirementsState>)States.GetValue(screen);
                var fresh = new Dictionary<BuildingDef, PlanScreen.RequirementsState>();
                PlanScreen.RequirementsState Fresh(BuildingDef def)
                {
                    if (!fresh.TryGetValue(def, out var state))
                        fresh[def] = state = StateForDef(screen, def);
                    return state;
                }

                CheckCategories(screen, instant, cache, Fresh, Stale);
                var groups = screen.buildingGroupsRoot;
                open = groups != null && groups.gameObject.activeInHierarchy;
                if (open)
                {
                    var reveal = groups.GetComponent<ExpandRevealUIContent>();
                    if (reveal != null && (reveal.Expanding || reveal.Collapsing))
                        Stale("the panel is " + (reveal.Expanding ? "expanding" : "collapsing"));
                    if ((bool)PanelSizePending.GetValue(screen)) Stale("a panel resize is pending");
                    visible = CheckButtons(screen, instant, cache, Fresh, Stale);
                }
            }
            watch.Stop();
            return new JObject
            {
                ["settled"] = stale == 0,
                ["menu"] = present,
                ["open"] = open,
                ["visible"] = visible,
                ["stale"] = stale,
                ["reasons"] = new JArray(reasons),
                ["ms"] = Math.Round(watch.Elapsed.TotalMilliseconds, 3),
            };
        }

        /// Each world's counted amounts against a recount now, as WorldInventory.Update counts a
        /// tag: the amounts of its pickupables in that world, not privately stored.
        static void CheckInventories(Action<string> stale)
        {
            if (ClusterManager.Instance == null) return;
            foreach (var world in ClusterManager.Instance.WorldContainers)
            {
                var inventory = world != null ? world.worldInventory : null;
                if (inventory == null) continue;
                var counted = inventory.GetAccessibleAmounts();
                int id = world.id;
                foreach (var pair in (Dictionary<Tag, HashSet<Pickupable>>)Inventory.GetValue(inventory))
                {
                    float sum = 0f;
                    foreach (var item in pair.Value)
                        if (item != null && item.GetMyWorldId() == id && !item.KPrefabID.HasTag(GameTags.StoredPrivate))
                            sum += item.TotalAmount;
                    counted.TryGetValue(pair.Key, out float have);
                    if (have != sum) stale($"inventory: world {id} counts {pair.Key.Name} as {have}, now {sum}");
                }
            }
        }

        /// The building buttons: shown as the filter decides, and the shown ones painted with
        /// their state now (PlanBuildingToggle.RefreshDisplay's mapping). Returns how many are
        /// shown.
        static int CheckButtons(PlanScreen screen, bool instant, Dictionary<string, PlanScreen.RequirementsState> cache,
                                Func<BuildingDef, PlanScreen.RequirementsState> fresh, Action<string> stale)
        {
            bool search = !BuildingGroupScreen.SearchIsEmpty;
            var defSearch = search ? (IDictionary)DefSearch.GetValue(screen) : null;
            var subSearch = search ? (IDictionary)SubcategorySearch.GetValue(screen) : null;
            var category = screen.ActiveCategoryToggleInfo;
            int visible = 0;
            var toggles = (Dictionary<string, PlanBuildingToggle>)AllToggles.GetValue(screen);
            // The batch UpdateBuildingButtonList repainted last: the maxToggleRefreshPerFrame
            // indices before building_button_refresh_idx, in allBuildingToggles' order.
            int next = (int)RefreshIndex.GetValue(screen), batch = (int)RefreshPerFrame.GetValue(screen), index = -1;
            bool JustRepainted(int i) => toggles.Count > 0 && ((next - 1 - i) % toggles.Count + toggles.Count) % toggles.Count < batch;
            foreach (var pair in toggles)
            {
                index++;
                var toggle = pair.Value;
                if (toggle == null) continue;
                bool want;
                if (search)
                    want = ((SearchUtil.BuildingDefCache)defSearch[pair.Key]).IsPassingScore()
                        || ((SearchUtil.SubcategoryCache)subSearch[pair.Key]).subcategory.IsPassingScore();
                else
                    // PlanBuildingToggle.StandardDisplayFilter, with the research checked now.
                    want = (category == null || (HashedString)ToggleCategory.GetValue(toggle) == (HashedString)category.userData)
                        && (instant || PlanScreen.TechRequirementsMet((TechItem)ToggleTech.GetValue(toggle)));
                bool shown = toggle.gameObject.activeSelf;
                if (shown != want)
                {
                    stale($"{pair.Key}: {(shown ? "shown" : "hidden")}, wants {(want ? "shown" : "hidden")}");
                    continue;
                }
                if (!shown || !toggle.gameObject.activeInHierarchy) continue;
                visible++;
                if (JustRepainted(index))
                    stale($"{pair.Key}: repainted this frame, its label is auto-sized at the frame's end");
                var def = (BuildingDef)ToggleDef.GetValue(toggle);
                var now = fresh(def);
                if (cache.TryGetValue(def.PrefabID, out var cached) && cached != now)
                    stale($"{pair.Key}: PlanScreen holds {cached}, now {now}");
                bool available = now == PlanScreen.RequirementsState.Complete || instant;
                bool selected = toggle.gameObject == screen.SelectedBuildingGameObject;
                int wantState = available ? (selected ? 1 : 0) : (selected ? 3 : 2);
                var multi = (MultiToggle)ToggleMulti.GetValue(toggle);
                if (multi != null && multi.CurrentState != wantState)
                    stale($"{pair.Key}: shows state {multi.CurrentState}, wants {wantState} ({now})");
                var icon = (Image)ToggleIcon.GetValue(toggle);
                var material = available ? screen.defaultUIMaterial : screen.desaturatedUIMaterial;
                if (icon != null && icon.material != material)
                    stale($"{pair.Key}: icon {(available ? "desaturated" : "in colour")}, wants {(available ? "in colour" : "desaturated")} ({now})");
                if (toggle.fgImage != null && toggle.fgImage.gameObject.activeSelf != (now == PlanScreen.RequirementsState.Tech))
                    stale($"{pair.Key}: research overlay {(toggle.fgImage.gameObject.activeSelf ? "shown" : "hidden")} ({now})");
            }
            return visible;
        }

        /// The category buttons: PlanScreen.SetCategoryButtonState's outcome for the buildings'
        /// states now, against what each button shows.
        static void CheckCategories(PlanScreen screen, bool instant, Dictionary<string, PlanScreen.RequirementsState> cache,
                                    Func<BuildingDef, PlanScreen.RequirementsState> fresh, Action<string> stale)
        {
            var entries = (IList)Entries.GetValue(screen);
            if (entries == null || entries.Count == 0) return;
            if (entryInfo == null)
            {
                var type = entries[0].GetType();
                entryDefs = F(type, "buildingDefs");
                entryImages = F(type, "toggleImages");
                entryHide = F(type, "hideIfNotResearched");
                entryTechAvailable = type.GetMethod("AreAnyRequiredTechItemsAvailable", Any)
                    ?? throw new DriverError("no method AreAnyRequiredTechItemsAvailable on " + type.FullName);
                entryInfo = F(type, "toggleInfo");
            }
            var active = screen.ActiveCategoryToggleInfo;
            foreach (var entry in entries)
            {
                var info = (KIconToggleMenu.ToggleInfo)entryInfo.GetValue(entry);
                if (info?.toggle == null) continue;
                bool any = instant, anyCached = instant;
                if (!instant)
                    foreach (var def in (List<BuildingDef>)entryDefs.GetValue(entry))
                    {
                        if (!anyCached && cache.TryGetValue(def.PrefabID, out var c) && c == PlanScreen.RequirementsState.Complete) anyCached = true;
                        if (!any && fresh(def) == PlanScreen.RequirementsState.Complete) any = true;
                        if (any && anyCached) break;
                    }
                string name = info.text ?? info.toggle.name;
                if (any != anyCached)
                {
                    stale($"category {name}: PlanScreen holds {(anyCached ? "" : "no ")}buildable building, now {(any ? "one" : "none")}");
                    continue;
                }
                bool locked = !any && !instant && !(bool)entryTechAvailable.Invoke(entry, null);
                bool wantShown = !(locked && (bool)entryHide.GetValue(entry));
                if (info.toggle.gameObject.activeSelf != wantShown)
                {
                    stale($"category {name}: {(wantShown ? "hidden" : "shown")}, wants {(wantShown ? "shown" : "hidden")}");
                    continue;
                }
                if (!wantShown) continue;
                var research = info.toggle.fgImage != null ? info.toggle.fgImage.transform.Find("ResearchIcon") : null;
                if (research != null && research.gameObject.activeSelf != (!any && locked))
                    stale($"category {name}: research icon {(research.gameObject.activeSelf ? "shown" : "hidden")}");
                bool current = active != null && info.userData == active.userData;
                var wantImage = any ? (current ? ImageToggleState.State.Active : ImageToggleState.State.Inactive)
                                    : (current ? ImageToggleState.State.DisabledActive : ImageToggleState.State.Disabled);
                var images = (ImageToggleState[])entryImages.GetValue(entry);
                if (images == null) continue;
                foreach (var image in images)
                {
                    if (image == null) continue;
                    var shows = (ImageToggleState.State)ImageState.GetValue(image);
                    if (shows != wantImage)
                    {
                        stale($"category {name}: shows {shows}, wants {wantImage}");
                        break;
                    }
                }
            }
        }

        /// buildmenu.settled {frames?}: one check now; with frames, checks again every frame
        /// until the menu is settled, or fails after that many frames with the last check.
        public static JToken Settled(Request request) => Then(request, (int?)request.Args["frames"] ?? 0, check => check);

        /// Waits up to FRAMES frames for the menu to settle, then answers with what DONE makes of
        /// the last check, in the frame the menu was found settled (this one, when it already
        /// is): a command run after the wait sees the frame the check saw.
        public static JToken Then(Request request, int frames, Func<JObject, JToken> done)
        {
            var first = Check();
            first["frames"] = 0;
            first["maxMs"] = first["ms"];
            if ((bool)first["settled"] || frames <= 0) return done(first);
            double worst = (double)first["ms"];
            request.Pending = r =>
            {
                var check = Check();
                worst = Math.Max(worst, (double)check["ms"]);
                int waited = Time.frameCount - r.StartFrame;
                check["frames"] = waited;
                check["maxMs"] = worst;
                if ((bool)check["settled"]) { r.State = done(check); return true; }
                if (waited >= frames)
                    throw new WaitTimeout($"timeout: the build menu did not settle within {frames} frames", check);
                return false;
            };
            return null;
        }
    }
}
