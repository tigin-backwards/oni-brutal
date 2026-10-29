// When a building's card collapses after the pointer leaves the build menu, in a colony (lines
// "PROBE ... CC ..."). Found with Christmas Tree (Workshop 2906165124): the e2e runner moves the
// pointer off the UI before a shot; ProductInfoScreen.CheckMouseOver then hides the card's
// requirement, effect and description panes. In ours the shot always shows the collapsed card,
// in the original mostly the full one.
//
// Logged: every frame in which Input.mousePosition changes (its value), each KScreen pointer
// enter and exit (the screen's type and the pointer), each ProductInfoScreen.ToggleExpandedInfo
// (its state), each end of frame for 6 frames after any of these, and the screenshots OniMcp
// takes (ScreenCapture.CaptureScreenshot and ScreenCapture.CaptureScreenshotAsTexture calls).
// With PROBE_SCENARIO=cardcollapse only this probe runs; a spec opens a card and takes a shot.
using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SemanticsProbe
{
    public class CardCollapse : MonoBehaviour
    {
        static void L(string s) => Rec.Log("CC " + s);
        static string M() { var p = Input.mousePosition; return ((int)p.x) + "," + ((int)p.y); }
        static int tail;
        Vector3 last = new Vector3(-1, -1, -1);

        public static void Install(Harmony h)
        {
            var self = typeof(CardCollapse);
            h.Patch(AccessTools.Method(typeof(KScreen), "OnPointerEnter"), prefix: new HarmonyMethod(self, nameof(Enter)));
            h.Patch(AccessTools.Method(typeof(KScreen), "OnPointerExit"), prefix: new HarmonyMethod(self, nameof(Exit)));
            h.Patch(AccessTools.Method(typeof(ProductInfoScreen), "ToggleExpandedInfo"), prefix: new HarmonyMethod(self, nameof(Toggle)));
            foreach (var m in typeof(ScreenCapture).GetMethods())
                if (m.Name.StartsWith("CaptureScreenshot") && !m.IsGenericMethod)
                    h.Patch(m, prefix: new HarmonyMethod(self, nameof(Capture)));
        }

        static void Enter(KScreen __instance) { L("f" + Time.frameCount + " enter " + __instance.GetType().Name + " m=" + M()); tail = 6; }
        static void Exit(KScreen __instance) { L("f" + Time.frameCount + " exit " + __instance.GetType().Name + " m=" + M()); tail = 6; }
        static void Toggle(bool state) { L("f" + Time.frameCount + " toggle " + state + " m=" + M()); tail = 6; }
        static void Capture() { L("f" + Time.frameCount + " capture m=" + M()); tail = 6; }

        void Update()
        {
            var p = Input.mousePosition;
            if (p != last) { L("f" + Time.frameCount + " mouse " + M() + " dt=" + Time.unscaledDeltaTime); last = p; tail = 6; }
        }

        void Start() => StartCoroutine(Ends());

        IEnumerator Ends()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                if (tail > 0) { tail--; L("f" + Time.frameCount + " end m=" + M()); }
            }
        }
    }
}
