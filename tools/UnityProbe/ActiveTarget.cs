// What RenderTexture.active is across a frame ("PROBE ... AT ..."), in a colony (it starts 120
// frames after Game.Instance exists). With PROBE_SCENARIO=activetarget only this probe runs.
//
// The game blits into render textures outside camera rendering (TextureBuffer's Graphics.Blit in
// PropertyTextures.LateUpdate), which makes the destination RenderTexture.active, and its
// Timelapser reads the screen with ReadPixels at WaitForEndOfFrame (the freeze frame shown while
// a save writes its preview). Whether the player resets RenderTexture.active between them decides
// what that ReadPixels reads.
//
// Each line is "AT <step> <where> active=<name or null>". Steps:
//   natural      three frames as the game leaves them: Update, LateUpdate, each camera's
//                onPreCull / onPreRender / onPostRender, WaitForEndOfFrame, next Update;
//   blit-update  Graphics.Blit into a probe texture in Update;
//   set-late     RenderTexture.active set in LateUpdate;
//   set-eof      RenderTexture.active set at WaitForEndOfFrame, followed through the next frame;
//   render       Camera.Render of a camera with a target texture, from Update, with active set
//                to another texture before, and with it null.
using System.Collections;
using UnityEngine;

namespace SemanticsProbe
{
    public class ActiveTarget : MonoBehaviour
    {
        static string step = "";
        static bool logCameras;
        static void L(string where) => Rec.Log("AT " + step + " " + where + " active=" + Name(RenderTexture.active));
        static string Name(RenderTexture t) => t == null ? "null" : "'" + t.name + "' " + t.width + "x" + t.height;

        RenderTexture probe, probe2;
        int wait = -1;
        bool started;
        string setIn = "";

        void Update()
        {
            if (!started)
            {
                if (Game.Instance == null) return;
                if (wait < 0) wait = Time.frameCount + 120;
                if (Time.frameCount < wait) return;
                started = true;
                probe = new RenderTexture(64, 32, 0) { name = "ProbeTarget" };
                probe.Create();
                probe2 = new RenderTexture(16, 16, 24) { name = "ProbeCameraTarget" };
                probe2.Create();
                Camera.onPreCull += c => { if (logCameras) L("onPreCull " + c.name + " target=" + Name(c.targetTexture)); };
                Camera.onPreRender += c => { if (logCameras) L("onPreRender " + c.name + " target=" + Name(c.targetTexture)); };
                Camera.onPostRender += c => { if (logCameras) L("onPostRender " + c.name + " target=" + Name(c.targetTexture)); };
                StartCoroutine(Run());
                return;
            }
            L("Update");
            if (setIn == "update-blit")
            {
                Graphics.Blit(Texture2D.whiteTexture, probe);
                L("Update after Blit");
            }
        }

        void LateUpdate()
        {
            if (!started) return;
            L("LateUpdate");
            if (setIn == "late-set")
            {
                RenderTexture.active = probe;
                L("LateUpdate after set");
            }
        }

        IEnumerator Run()
        {
            var eof = new WaitForEndOfFrame();
            step = "natural";
            logCameras = true;
            for (int i = 0; i < 3; i++)
            {
                yield return eof;
                L("WaitForEndOfFrame");
            }
            logCameras = false;
            yield return null;

            // Set after this frame's Update: the next frame's Update blits.
            step = "blit-update";
            setIn = "update-blit";
            yield return eof;
            yield return eof;
            setIn = "";
            L("WaitForEndOfFrame");
            yield return eof;
            L("WaitForEndOfFrame next frame");

            step = "set-late";
            setIn = "late-set";
            yield return eof;
            setIn = "";
            L("WaitForEndOfFrame");
            yield return eof;
            L("WaitForEndOfFrame next frame");

            step = "set-eof";
            yield return eof;
            RenderTexture.active = probe;
            L("WaitForEndOfFrame after set");
            logCameras = true;
            yield return eof;
            logCameras = false;
            L("WaitForEndOfFrame next frame");
            RenderTexture.active = null;

            step = "render";
            yield return null;
            var go = new GameObject("ProbeRenderCamera");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.targetTexture = probe2;
            cam.cullingMask = 0;
            RenderTexture.active = probe;
            L("before Render (active set)");
            cam.Render();
            L("after Render (active set)");
            RenderTexture.active = null;
            cam.Render();
            L("after Render (active null)");
            yield return eof;
            L("WaitForEndOfFrame");
            Destroy(go);

            step = "done";
            var done = new GameObject("ActiveTargetProbeDone", typeof(RectTransform));
            done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Rec.Log("AT === done");
        }
    }
}
