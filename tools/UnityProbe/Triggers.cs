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
