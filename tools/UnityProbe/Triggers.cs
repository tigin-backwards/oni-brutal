// Scripted game events that normal play reaches only after hours: the Thriving victory (story
// message, camera sequence, victory video and loop). Screenshots are taken by the probe itself
// (ScreenCapture), so both players are captured at the same points of the sequence. Enabled when
// PROBE_CAPTURE_DIR is set; starts after the in-game UI dump.
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SemanticsProbe
{
    public class Triggers : MonoBehaviour
    {
        public static bool Ready;
        bool started;
        string dir;

        void Awake() => dir = Environment.GetEnvironmentVariable("PROBE_CAPTURE_DIR");

        void Update()
        {
            if (started || !Ready || string.IsNullOrEmpty(dir)) return;
            started = true;
            StartCoroutine(Victory());
        }

        void Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            Rec.Log("CAPTURE " + name);
        }

        static readonly string[] SideScreenBuildings =
        {
            "Door", "LogicTemperatureSensor", "LogicSwitch", "LogicTimer", "StorageLocker", "LiquidValve", "Bed", "ManualGenerator",
            "ResearchCenter", "MicrobeMusher", "Outhouse", "AirFilter", "Electrolyzer", "BatterySmart", "FlowerVase", "SmartReservoir",
            "Refrigerator", "CookingStation", "PlanterBox", "LogicPressureSensorGas",
        };

        static void States(string when)
        {
            var all = FindObjectsByType<StoryMessageScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Rec.Log("STATE " + when + ": StoryMessageScreens " + all.Length + (all.Length > 0 ? " active=" + all[0].gameObject.activeInHierarchy + " isActive=" + all[0].IsActive() : ""));
        }

        IEnumerator Victory()
        {
            // After any scripted input of the drivers (they click through the embark screens).
            yield return new WaitForSecondsRealtime(12f);
            // A Mop order carried out: a puddle beside the printing pod, marked with the Mop tool,
            // then the game runs at speed 3.
            int pod = Grid.PosToCell(Components.Telepads.Items[0].transform.GetPosition());
            int puddle = Grid.OffsetCell(pod, 3, 0);
            SimMessages.AddRemoveSubstance(puddle, SimHashes.Water, CellEventLogger.Instance.FallingWaterAddToSim, 30f, 293.15f, byte.MaxValue, 0);
            yield return new WaitForSecondsRealtime(1f);
            Rec.Log("ORDER mop cell " + Grid.Element[puddle].id + " " + Grid.Mass[puddle].ToString("F1") + " kg");
            typeof(MopTool).GetMethod("OnDragTool", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(MopTool.Instance, new object[] { puddle, 0 });
            Rec.Log("ORDER mop placed " + (Grid.Objects[puddle, 8] != null));
            SpeedControlScreen.Instance.SetSpeed(2);
            SpeedControlScreen.Instance.Unpause(false);
            yield return new WaitForSecondsRealtime(40f);
            SpeedControlScreen.Instance.Pause(false);
            Rec.Log("ORDER mop after: cell " + Grid.Element[puddle].id + " " + Grid.Mass[puddle].ToString("F1") + " kg, order left " + (Grid.Objects[puddle, 8] != null));

            // Harvest, Disinfect and Attack orders carried out.
            var podPos = Components.Telepads.Items[0].transform.GetPosition();
            // A Mealwood planted beside the printing pod, fully grown.
            var mealwood = GameUtil.KInstantiate(Assets.GetPrefab("BasicSingleHarvestPlant"), Grid.CellToPosCBC(Grid.OffsetCell(pod, -3, 0), Grid.SceneLayer.BuildingBack), Grid.SceneLayer.BuildingBack);
            mealwood.SetActive(true);
            yield return null;
            var plant = mealwood.GetComponent<HarvestDesignatable>();
            if (plant != null)
            {
                plant.GetComponent<Growing>().OverrideMaturityLevel(1f);
                plant.SetHarvestWhenReady(true);
                Rec.Log("ORDER harvest " + plant.name + " grown " + plant.GetComponent<Growing>().PercentGrown().ToString("F2")
                    + " canBeHarvested=" + plant.GetComponent<Harvestable>().CanBeHarvested + " marked=" + plant.MarkedForHarvest);
            }
            var bin = Assets.GetBuildingDef("StorageLocker").Build(Grid.OffsetCell(pod, -4, 0), Orientation.Neutral, null, Assets.GetBuildingDef("StorageLocker").DefaultElements(), 293.15f, playsound: false);
            yield return null;
            bin.GetComponent<PrimaryElement>().AddDisease(Db.Get().Diseases.GetIndex(Db.Get().Diseases.FoodGerms.id), 100000, "probe");
            bin.GetComponent<Disinfectable>().MarkForDisinfect();
            Rec.Log("ORDER disinfect germs " + bin.GetComponent<PrimaryElement>().DiseaseCount);
            // A Hatch beside the printing pod (on the floor the Duplicants stand on).
            var hatch = GameUtil.KInstantiate(Assets.GetPrefab("Hatch"), Grid.CellToPosCBC(Grid.OffsetCell(pod, 3, 0), Grid.SceneLayer.Creatures), Grid.SceneLayer.Creatures);
            hatch.SetActive(true);
            yield return null;
            var critter = hatch.GetComponent<FactionAlignment>();
            if (critter != null)
            {
                critter.SetPlayerTargeted(true);
                Rec.Log("ORDER attack " + critter.name + " at " + critter.transform.GetPosition());
            }
            // Wrangle: every Duplicant learns Critter Ranching; a second Hatch is marked for capture.
            foreach (var minion in Components.LiveMinionIdentities.Items)
            {
                var resume = minion.GetComponent<MinionResume>();
                resume.MasterSkill("Farming1");
                resume.MasterSkill("Ranching1");
            }
            var hatch2 = GameUtil.KInstantiate(Assets.GetPrefab("Hatch"), Grid.CellToPosCBC(Grid.OffsetCell(pod, -5, 0), Grid.SceneLayer.Creatures), Grid.SceneLayer.Creatures);
            hatch2.SetActive(true);
            yield return null;
            hatch2.GetComponent<Capturable>().MarkForCapture(true);
            Rec.Log("ORDER wrangle marked " + hatch2.GetComponent<Capturable>().IsMarkedForCapture + " perk "
                + Components.LiveMinionIdentities.Items[0].GetComponent<MinionResume>().HasPerk(Db.Get().SkillPerks.CanWrangleCreatures));
            SpeedControlScreen.Instance.SetSpeed(2);
            SpeedControlScreen.Instance.Unpause(false);
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSecondsRealtime(20f);
                if (hatch2 == null) { Rec.Log("ORDER wrangle " + i + ": gone"); continue; }
                Rec.Log("ORDER wrangle " + i + ": cell offset " + (Grid.PosToCell(hatch2) - pod) + " bagged=" + hatch2.HasTag(GameTags.Creatures.Bagged)
                    + " burrowed=" + hatch2.HasTag(GameTags.Creatures.Burrowed) + " stored=" + hatch2.HasTag(GameTags.Stored)
                    + " marked=" + hatch2.GetComponent<Capturable>().IsMarkedForCapture);
            }
            SpeedControlScreen.Instance.Pause(false);
            if (plant == null) Rec.Log("ORDER harvest after: plant gone");
            else Rec.Log("ORDER harvest after: grown " + plant.GetComponent<Growing>().PercentGrown().ToString("F2")
                + " canBeHarvested=" + plant.GetComponent<Harvestable>().CanBeHarvested + " marked=" + plant.MarkedForHarvest);
            Rec.Log("ORDER disinfect after: germs " + bin.GetComponent<PrimaryElement>().DiseaseCount);
            Rec.Log("ORDER wrangle after: " + (hatch2 == null ? "gone" : "bagged=" + hatch2.HasTag(GameTags.Creatures.Bagged)));
            Rec.Log("ORDER attack after: " + (critter == null || critter.GetComponent<Health>() == null ? "gone" : "health " + critter.GetComponent<Health>().hitPoints.ToString("F0")));
            Util.KDestroyGameObject(bin);

            // Research by a Duplicant: a Research Station powered by a Dev Generator through a
            // wire, stocked with dirt, Basic Farming active.
            Research.Instance.SetActiveResearch(Db.Get().Techs.Get("FarmingTech"), true);
            var stationDef = Assets.GetBuildingDef("ResearchCenter");
            var generatorDef = Assets.GetBuildingDef("DevGenerator");
            int stationCell = Grid.OffsetCell(pod, 6, 0), generatorCell = Grid.OffsetCell(pod, 10, 0);
            // Clear the space beside the start room to air (the floor row stays).
            for (int dx = 4; dx <= 13; dx++)
                for (int dy = 0; dy <= 3; dy++)
                    SimMessages.ReplaceAndDisplaceElement(Grid.OffsetCell(pod, dx, dy), SimHashes.Oxygen, CellEventLogger.Instance.DebugTool, 1f, 293.15f);
            for (int dx = 4; dx <= 13; dx++)
                SimMessages.ReplaceAndDisplaceElement(Grid.OffsetCell(pod, dx, -1), SimHashes.SandStone, CellEventLogger.Instance.DebugTool, 1000f, 293.15f);
            yield return new WaitForSecondsRealtime(1f);
            var station = stationDef.Build(stationCell, Orientation.Neutral, null, stationDef.DefaultElements(), 293.15f, playsound: false);
            var generator = generatorDef == null ? null : generatorDef.Build(generatorCell, Orientation.Neutral, null, generatorDef.DefaultElements(), 293.15f, playsound: false);
            yield return null;
            int input = Grid.OffsetCell(stationCell, stationDef.PowerInputOffset);
            int output = generatorDef == null ? -1 : Grid.OffsetCell(generatorCell, generatorDef.PowerOutputOffset);
            Rec.Log("RESEARCH ports input " + Grid.CellToXY(input) + " output " + (output < 0 ? "none" : Grid.CellToXY(output).ToString()));
            if (output >= 0 && Grid.CellRow(input) == Grid.CellRow(output))
            {
                var wireDef = Assets.GetBuildingDef("Wire");
                int from = Math.Min(Grid.CellColumn(input), Grid.CellColumn(output)), to = Math.Max(Grid.CellColumn(input), Grid.CellColumn(output));
                var wires = new System.Collections.Generic.List<(GameObject Wire, int Cell, UtilityConnections Connections)>();
                for (int x = from; x <= to; x++)
                {
                    int c = Grid.XYToCell(x, Grid.CellRow(input));
                    var wire = wireDef.Build(c, Orientation.Neutral, null, wireDef.DefaultElements(), 293.15f, playsound: false);
                    wires.Add((wire, c, (x > from ? UtilityConnections.Left : 0) | (x < to ? UtilityConnections.Right : 0)));
                }
                // The wires' tile visualizers apply their own connections when they spawn.
                yield return null;
                foreach (var (wire, c, connections) in wires)
                {
                    wire.GetComponent<KAnimGraphTileVisualizer>().Connections = connections;
                    Game.Instance.electricalConduitSystem.SetConnections(connections, c, true);
                }
                Game.Instance.electricalConduitSystem.ForceRebuildNetworks();
            }
            station.GetComponent<Storage>().AddOre(SimHashes.Dirt, 500f, 293.15f, byte.MaxValue, 0);
            var farming = Research.Instance.Get(Db.Get().Techs.Get("FarmingTech"));
            Rec.Log("RESEARCH before " + farming.PercentageCompleteResearchType("basic").ToString("F3"));
            SpeedControlScreen.Instance.SetSpeed(2);
            SpeedControlScreen.Instance.Unpause(false);
            yield return new WaitForSecondsRealtime(150f);
            SpeedControlScreen.Instance.Pause(false);
            Rec.Log("RESEARCH after " + farming.PercentageCompleteResearchType("basic").ToString("F3") + " complete=" + farming.IsComplete()
                    + " powered=" + station.GetComponent<EnergyConsumer>().IsPowered + " operational=" + station.GetComponent<Operational>().IsOperational
                    + " dirt=" + station.GetComponent<Storage>().GetMassAvailable(SimHashes.Dirt).ToString("F0"));
            foreach (var item in station.GetComponent<KSelectable>().GetStatusItemGroup())
                Rec.Log("RESEARCH status " + item.item.Id);
            if (generator != null)
                foreach (var item in generator.GetComponent<KSelectable>().GetStatusItemGroup())
                    Rec.Log("RESEARCH generator status " + item.item.Id);

            // Side screens: each building is built finished next to the printing pod, selected,
            // captured and removed.
            int cell = Grid.OffsetCell(pod, 4, 0);
            foreach (var id in SideScreenBuildings)
            {
                var def = Assets.GetBuildingDef(id);
                if (def == null) { Rec.Log("SIDE missing " + id); continue; }
                var go = def.Build(cell, Orientation.Neutral, null, def.DefaultElements(), 293.15f, playsound: false);
                yield return null;
                var sel = go.GetComponent<KSelectable>();
                SelectTool.Instance.Select(sel, true);
                yield return new WaitForSecondsRealtime(1.5f);
                Rec.Log("SIDE " + id + " go active=" + go.activeInHierarchy + " pos=" + go.transform.position + " cell=" + cell + " selectable=" + (sel != null && sel.IsSelectable)
                        + " selected=" + (SelectTool.Instance.selected == sel) + " details=" + (DetailsScreen.Instance != null && DetailsScreen.Instance.gameObject.activeInHierarchy));
                Capture("s_" + id);
                yield return null; // the screenshot is taken at the end of the frame
                yield return null;
                SelectTool.Instance.Select(null, true);
                Util.KDestroyGameObject(go);
                yield return new WaitForSecondsRealtime(0.5f);
            }

            // Research in progress: half of Basic Farming's points, then the research screen.
            Research.Instance.SetActiveResearch(Db.Get().Techs.Get("FarmingTech"), true);
            Research.Instance.AddResearchPoints("basic", 7.5f);
            // The menu's research button is locked without a research station: show the screen.
            var research = (KScreen)typeof(ManagementMenu).GetField("researchScreen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ManagementMenu.Instance);
            research.Show(true);
            ((ResearchScreen)research).ZoomToTech("FarmingTech", true);
            yield return new WaitForSecondsRealtime(3f);
            Capture("r1_research");
            yield return null;
            yield return null;
            research.Show(false);
            yield return new WaitForSecondsRealtime(2f);

            // A story-trait popup, as the Mega Brain Tank shows when it is discovered.
            Rec.Log("TRIGGER event popup");
            var popup = EventInfoScreen.ShowPopup(EventInfoDataHelper.GenerateStoryTraitData(
                STRINGS.CODEX.STORY_TRAITS.MEGA_BRAIN_TANK.BEGIN_POPUP.NAME, STRINGS.CODEX.STORY_TRAITS.MEGA_BRAIN_TANK.BEGIN_POPUP.DESCRIPTION,
                STRINGS.CODEX.STORY_TRAITS.CLOSE_BUTTON, "braintankdiscovered_kanim", EventInfoDataHelper.PopupType.BEGIN));
            yield return new WaitForSecondsRealtime(3f);
            Capture("e1_event");
            yield return null;
            yield return null;
            popup.Deactivate();
            yield return new WaitForSecondsRealtime(2f);
            var tracker = SaveGame.Instance.GetComponent<ColonyAchievementTracker>();
            Rec.Log("TRIGGER victory Thriving");
            typeof(ColonyAchievementTracker).GetMethod("BeginVictorySequence", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tracker, new object[] { "Thriving" });
            States("right after");
            yield return null;
            States("next frame");
            yield return new WaitForSecondsRealtime(1f);
            States("after 1 s");
            yield return new WaitForSecondsRealtime(2f);
            Capture("v1_story");
            yield return null;
            yield return null;
            foreach (var sms in FindObjectsByType<StoryMessageScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Rec.Log("STATE StoryMessageScreen " + sms.name + " active=" + sms.gameObject.activeInHierarchy + " self=" + sms.gameObject.activeSelf + " parent=" + (sms.transform.parent == null ? "none" : sms.transform.parent.name + " active=" + sms.transform.parent.gameObject.activeInHierarchy));
            foreach (var eis in FindObjectsByType<EventInfoScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Rec.Log("STATE EventInfoScreen " + eis.name + " active=" + eis.gameObject.activeInHierarchy + " self=" + eis.gameObject.activeSelf);
            var story = FindFirstObjectByType<StoryMessageScreen>();
            if (story == null)
            {
                Rec.Log("TRIGGER no active StoryMessageScreen");
                yield break;
            }
            var button = (KButton)typeof(StoryMessageScreen).GetField("button", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(story);
            button.SignalClick(KKeyCode.Mouse0);
            Rec.Log("TRIGGER story closed");
            yield return new WaitForSecondsRealtime(6f);
            Capture("v2_sequence");
            yield return new WaitForSecondsRealtime(12f);
            Capture("v3_video");
            yield return new WaitForSecondsRealtime(20f);
            Capture("v4_loop");
            Rec.Log("TRIGGER done");
        }
    }
}
