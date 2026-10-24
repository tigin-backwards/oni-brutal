// The order Update and LateUpdate reach behaviours of the same execution order that were made
// at different times, lines "PROBE ... UO ...". Found with Resize Dup UI (Workshop 2549295369):
// OniMcp's main-thread bridge, made in its mod's OnLoad, opens the Vitals screen from its Update;
// the screen's OnActivate sets its scale to 0 and KScreenManager.Update (TableScreen.ScreenUpdate)
// sets it back to 1. Whether that happens before the screen's KScrollRect.LateUpdate depends on
// whether KScreenManager (made by LaunchInitializer, before the mods load) updates before or
// after the bridge.
//
// Run alone with PROBE_SCENARIO=updorder. Each "order" block lists, for one frame, every logged
// callback in the order it ran:
// - load: the behaviour made in OnLoad (UoA "onload"), KScreenManager (a Harmony prefix on its
//   Update), KProfilerEnd (a game type with empty Update and LateUpdate, also prefixed), at the
//   main menu, 5 s in;
// - made: UoA and UoB instances made in one frame in the order x1 (UoA), y1 (UoB), x2 (UoA),
//   y2 (UoB), then in the next frame y3 (UoB), x3 (UoA), and a KProfilerEnd "kpe1" added after
//   them; logged two and three frames later;
// - reenabled: x1 disabled and enabled again, onload's object deactivated and activated again;
// - game: the same set once a colony is loaded (if the profile resumes one), 120 frames in.
// Each instance's GetInstanceID and its script's is logged once ("ids").
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SemanticsProbe
{
    public class UoBase : MonoBehaviour
    {
        public string Tag = "";
        void Update() { if (UpdateOrder.Armed) UpdateOrder.Note("U " + Tag); }
        void LateUpdate() { if (UpdateOrder.Armed) UpdateOrder.Note("L " + Tag); }
    }

    public class UoA : UoBase { }
    public class UoB : UoBase { }

    public class UpdateOrder : MonoBehaviour
    {
        public static bool Armed;
        static readonly List<string> calls = new List<string>();
        static readonly Dictionary<Object, string> tags = new Dictionary<Object, string>();

        static void L(string s) => Rec.Log("UO " + s);
        public static void Note(string s) => calls.Add(s);

        public static void Install(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(HarmonyLib.AccessTools.Method(typeof(KScreenManager), "Update"),
                prefix: new HarmonyLib.HarmonyMethod(typeof(UpdateOrder), nameof(KsmUpdate)));
            harmony.Patch(HarmonyLib.AccessTools.Method(typeof(KProfilerEnd), "Update"),
                prefix: new HarmonyLib.HarmonyMethod(typeof(UpdateOrder), nameof(KpeUpdate)));
            harmony.Patch(HarmonyLib.AccessTools.Method(typeof(KProfilerEnd), "LateUpdate"),
                prefix: new HarmonyLib.HarmonyMethod(typeof(UpdateOrder), nameof(KpeLateUpdate)));
            var go = new GameObject("uo-onload");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<UoA>().Tag = "onload";
        }

        static string TagOf(Object o) => tags.TryGetValue(o, out var t) ? t : o.name;
        public static void KsmUpdate() { if (Armed) Note("U KScreenManager"); }
        public static void KpeUpdate(KProfilerEnd __instance) { if (Armed) Note("U kpe:" + TagOf(__instance)); }
        public static void KpeLateUpdate(KProfilerEnd __instance) { if (Armed) Note("L kpe:" + TagOf(__instance)); }

        // Collects from one end of frame to the next (the first block starts mid-frame, after
        // Update), and logs each frame's calls.
        IEnumerator Log(string label, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                calls.Clear();
                Armed = true;
                yield return new WaitForEndOfFrame();
                Armed = false;
                L(label + " frame " + i + " order " + string.Join(", ", calls));
            }
        }

        static T Make<T>(string tag) where T : UoBase
        {
            var go = new GameObject("uo-" + tag);
            Object.DontDestroyOnLoad(go);
            var c = go.AddComponent<T>();
            c.Tag = tag;
            L("ids " + tag + " " + c.GetInstanceID() + " go " + go.GetInstanceID());
            return c;
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(5f);
            L("begin scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            yield return Log("load", 3);
            var x1 = Make<UoA>("x1"); Make<UoB>("y1"); Make<UoA>("x2"); Make<UoB>("y2");
            yield return null;
            Make<UoB>("y3"); Make<UoA>("x3");
            var kpe = new GameObject("uo-kpe1");
            Object.DontDestroyOnLoad(kpe);
            tags[kpe.AddComponent<KProfilerEnd>()] = "kpe1";
            yield return null;
            yield return null;
            yield return Log("made", 3);
            x1.enabled = false;
            yield return null;
            x1.enabled = true;
            var onload = GameObject.Find("uo-onload");
            onload.SetActive(false);
            onload.SetActive(true);
            yield return null;
            yield return Log("reenabled", 3);
            while (Game.Instance == null) yield return new WaitForSecondsRealtime(1f);
            for (int i = 0; i < 120; i++) yield return null;
            L("game scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            yield return Log("game", 3);
            L("end");
        }
    }
}
