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

        static void States(string when)
        {
            var all = FindObjectsByType<StoryMessageScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Rec.Log("STATE " + when + ": StoryMessageScreens " + all.Length + (all.Length > 0 ? " active=" + all[0].gameObject.activeInHierarchy + " isActive=" + all[0].IsActive() : ""));
        }

        IEnumerator Victory()
        {
            // After any scripted input of the drivers (they click through the embark screens).
            yield return new WaitForSecondsRealtime(12f);
            // A story-trait popup, as the Mega Brain Tank shows when it is discovered.
            Rec.Log("TRIGGER event popup");
            var popup = EventInfoScreen.ShowPopup(EventInfoDataHelper.GenerateStoryTraitData(
                STRINGS.CODEX.STORY_TRAITS.MEGA_BRAIN_TANK.BEGIN_POPUP.NAME, STRINGS.CODEX.STORY_TRAITS.MEGA_BRAIN_TANK.BEGIN_POPUP.DESCRIPTION,
                STRINGS.CODEX.STORY_TRAITS.CLOSE_BUTTON, "braintankdiscovered_kanim", EventInfoDataHelper.PopupType.BEGIN));
            yield return new WaitForSecondsRealtime(3f);
            Capture("e1_event");
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
