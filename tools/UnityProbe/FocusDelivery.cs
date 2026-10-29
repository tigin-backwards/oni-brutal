// Which MonoBehaviours OnApplicationFocus reaches ("PROBE ... AF ..."). Run alone with
// PROBE_SCENARIO=focusdelivery; change the window's focus from outside (the X input focus away
// and back) while the main menu is up.
//
// Found through a reporter's log of ours: MainMenu.OnApplicationFocus(true) ran
// RefreshResumeButton on a destroyed Button_ResumeGame while a save loaded from the main menu.
//
// Spies, each logging "AF <tag> <focus>" from OnApplicationFocus:
//   on          an active object, the spy enabled
//   disabled    an active object, the spy disabled (enabled = false)
//   inactive    an inactive object, the spy enabled
//   child-off   an enabled spy under an inactive parent
//   timed       an active object with Destroy(go, 1e6) pending
// Each focus change also logs the frame ("AF frame N") from a spy's Update when it changes.
using UnityEngine;

namespace SemanticsProbe
{
    public static class FocusDelivery
    {
        public static void Run()
        {
            Make("on", true, true);
            Make("disabled", true, false);
            Make("inactive", false, true);
            var parent = new GameObject("AF child-off parent");
            Object.DontDestroyOnLoad(parent);
            var child = new GameObject("AF child-off");
            child.transform.SetParent(parent.transform, false);
            child.AddComponent<AfSpy>().Tag = "child-off";
            parent.SetActive(false);
            var timed = Make("timed", true, true);
            Object.Destroy(timed, 1e6f);
            Rec.Log("AF ready");
        }

        static GameObject Make(string tag, bool active, bool enabled)
        {
            var go = new GameObject("AF " + tag);
            Object.DontDestroyOnLoad(go);
            var spy = go.AddComponent<AfSpy>();
            spy.Tag = tag;
            spy.enabled = enabled;
            go.SetActive(active);
            return go;
        }
    }

    public class AfSpy : MonoBehaviour
    {
        public string Tag;
        static bool last;
        void OnApplicationFocus(bool focus) => Rec.Log("AF " + Tag + " " + focus + " isFocused=" + Application.isFocused);
        void Update()
        {
            if (Tag != "on" || Application.isFocused == last) return;
            last = Application.isFocused;
            Rec.Log("AF frame isFocused=" + last);
        }
    }
}
