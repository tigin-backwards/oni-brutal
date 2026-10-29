// The research screen's pan and zoom when it opens, in a colony (lines "PROBE ... RP ..."). Found
// with Instant Research Fixed (Workshop 3034422919): opened from the paused early-base fixture,
// the screen settles scrolled down and right of the Food row in the original and at the tree's
// top left in ours. ResearchScreen.Update (Klei's) zooms from 1 to 0.6 about the pointer and pulls
// the content back inside its bounds, both by Time.unscaledDeltaTime.
//
// Logged: each ResearchScreen.Show call, the number of Update calls before the first Show(true),
// then for 120 Update calls after it, before and after each, the frame, unscaledDeltaTime,
// the content's anchoredPosition, localScale, rect, world position and lossyScale, currentZoom,
// targetZoom, the pointer (KInputManager.GetMousePos), zoomCenterLock, isDragging, keyPanDelta,
// dragInteria, the pan flags and zoomingToTarget.
// PROBE_RP_SLEEP=MS sleeps that long in every Update before the first Show(true) and in the first
// 20 after it, so the frames around the opening last about that long (the original player in the e2e
// runner draws a frame every 0.45 s or so; ours much faster).
// With PROBE_SCENARIO=researchpan only this probe runs; a spec opens the research screen.
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace SemanticsProbe
{
    public static class ResearchPan
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string F(float f) => f.ToString("R", Inv);
        static string V2(Vector2 v) => F(v.x) + "," + F(v.y);
        static string V3(Vector3 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
        static void L(string s) => Rec.Log("RP " + s);

        static int before, after = -1;
        static readonly int sleep = int.TryParse(System.Environment.GetEnvironmentVariable("PROBE_RP_SLEEP"), out var ms) ? ms : 0;

        public static void Install(Harmony h)
        {
            var t = typeof(ResearchScreen);
            h.Patch(AccessTools.Method(t, "Update"),
                prefix: new HarmonyMethod(typeof(ResearchPan), nameof(UpdateBefore)),
                postfix: new HarmonyMethod(typeof(ResearchPan), nameof(UpdateAfter)));
            h.Patch(AccessTools.Method(t, "Show"),
                prefix: new HarmonyMethod(typeof(ResearchPan), nameof(ShowBefore)));
        }

        static void ShowBefore(ResearchScreen __instance, bool show)
        {
            L("show " + show + " f" + Time.frameCount + " updatesBefore=" + before + " " + State(__instance));
            if (show && after < 0) after = 0;
        }

        static string State(ResearchScreen s)
        {
            var tr = Traverse.Create(s);
            var content = tr.Field("scrollContent").GetValue<GameObject>();
            var rt = content.GetComponent<RectTransform>();
            return "dt=" + F(Time.unscaledDeltaTime) + " ap=" + V2(rt.anchoredPosition) + " ls=" + V3(rt.localScale)
                + " size=" + V2(rt.rect.size) + " pos=" + V3(rt.position) + " lossy=" + V3(rt.lossyScale)
                + " zoom=" + F(tr.Field("currentZoom").GetValue<float>()) + " target=" + F(tr.Field("targetZoom").GetValue<float>())
                + " mouse=" + V3(KInputManager.GetMousePos()) + " lock=" + tr.Field("zoomCenterLock").GetValue<bool>()
                + " drag=" + tr.Field("isDragging").GetValue<bool>() + " key=" + V2(tr.Field("keyPanDelta").GetValue<Vector2>())
                + " inertia=" + V2(tr.Field("dragInteria").GetValue<Vector2>())
                + " pan=" + tr.Field("panUp").GetValue<bool>() + tr.Field("panDown").GetValue<bool>() + tr.Field("panLeft").GetValue<bool>() + tr.Field("panRight").GetValue<bool>()
                + " toTarget=" + tr.Field("zoomingToTarget").GetValue<bool>() + " active=" + s.IsScreenActive();
        }

        static void UpdateBefore(ResearchScreen __instance)
        {
            if (after < 0)
            {
                before++;
                if (sleep > 0) System.Threading.Thread.Sleep(sleep);
                return;
            }
            if (after >= 120) return;
            L("u" + after + " f" + Time.frameCount + " pre " + State(__instance));
            if (sleep > 0 && after < 20) System.Threading.Thread.Sleep(sleep);
        }

        static void UpdateAfter(ResearchScreen __instance)
        {
            if (after < 0 || after >= 120) return;
            L("u" + after + " f" + Time.frameCount + " post " + State(__instance));
            after++;
        }
    }
}
