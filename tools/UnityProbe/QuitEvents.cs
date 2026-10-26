// When Application's quit events run (lines "PROBE ... QE ..."), at the main menu: two
// wantsToQuit handlers (the first refuses the first request and throws at the second), three
// quitting handlers (the second throws), an unloading handler, and OnApplicationQuit of a scene
// object and of a DontDestroyOnLoad one. The probe calls Application.Quit() up to three times,
// three frames apart, logging the frame's steps around each, so the order shows. Better Save
// (3805601277) subscribes to quitting.
using System;
using UnityEngine;

namespace SemanticsProbe
{
    public class QuitEventsLog : MonoBehaviour
    {
        public string Tag;
        void OnApplicationQuit() => QuitEvents.L("OnApplicationQuit " + Tag);
        void OnDisable() => QuitEvents.L("OnDisable " + Tag);
        void OnDestroy() => QuitEvents.L("OnDestroy " + Tag);
    }

    public class QuitEvents : MonoBehaviour
    {
        public static void L(string s) => Rec.Log("QE " + s);

        int frame, asked, quitAt;

        void Start()
        {
            L("=== quit events");
            // Request 1: the first handler refuses; request 2: it throws; request 3: both agree.
            Application.wantsToQuit += () =>
            {
                asked++;
                L("wantsToQuit 1 request " + asked + (asked == 1 ? " answers False" : asked == 2 ? " throws" : " answers True"));
                if (asked == 2) throw new InvalidOperationException("wantsToQuit 1");
                return asked > 1;
            };
            Application.wantsToQuit += () => { L("wantsToQuit 2 request " + asked + " answers True"); return true; };
            Application.quitting += () => L("quitting 1");
            Application.quitting += () => { L("quitting 2 throws"); throw new InvalidOperationException("quitting 2"); };
            Application.quitting += () => L("quitting 3");
            Application.unloading += () => L("unloading");
            gameObject.AddComponent<QuitEventsLog>().Tag = "ddol";
            new GameObject("QuitEventsScene").AddComponent<QuitEventsLog>().Tag = "scene";
        }

        void Update()
        {
            frame++;
            if (frame == 5 || frame == 8 || frame == 11)
            {
                L("calling Quit, frame " + frame);
                quitAt = frame;
                Application.Quit();
                L("Quit returned");
                StartCoroutine(EndOfFrame(frame));
            }
            else if (quitAt > 0 && frame <= 15)
            {
                L("Update frame " + frame + " after Quit at " + quitAt);
            }
        }

        void LateUpdate()
        {
            if (quitAt == frame) L("LateUpdate frame " + frame);
        }

        int guiFrame;

        void OnGUI()
        {
            if (quitAt == frame && guiFrame != frame && Event.current.type == EventType.Repaint)
            {
                guiFrame = frame;
                L("OnGUI repaint frame " + frame);
            }
        }

        System.Collections.IEnumerator EndOfFrame(int at)
        {
            yield return new WaitForEndOfFrame();
            L("WaitForEndOfFrame frame " + at);
        }
    }
}
