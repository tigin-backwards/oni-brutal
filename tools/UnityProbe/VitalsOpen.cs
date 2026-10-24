// What runs in the frames around a table screen's activation, in a colony (lines "PROBE ... VO
// ..."). Found with Resize Dup UI (Workshop 2549295369), played through the e2e runner, whose
// OniMcp opens the Vitals screen from its main-thread bridge's Update: TableScreen.OnActivate sets
// the screen's scale to 0 and its next ScreenUpdate (from KScreenManager.Update) sets it back to
// 1. In ours the screen's KScrollRect had its first LateUpdate under scale 0 in every run; the
// original's scrollbar shows that it did not in four runs of five.
//
// Logged from OnActivate on for 40 frames: the starts and ends of KScreenManager.Update and of
// OniMcp's MainThreadBridge.Update (found by name once a colony is loaded), each table screen's
// OnActivate and ScreenUpdate, the KScrollRect LateUpdates under a table screen (the screen's
// scale, the content's anchoredPosition and the vertical scrollbar's value and size), each frame's
// unscaledDeltaTime and each end of frame; TableScreen.RefreshRows, the tracked scroll rect's
// Rebuild calls and OnRectTransformDimensionsChange, with the content's and viewport's heights;
// in its first three LateUpdates, before and after, the inputs of the content's bounds bit for
// bit ("geo").
// With PROBE_SCENARIO=vitalsopen only this probe runs. It needs a colony to be loaded and a table
// screen to be opened (the e2e runner's Resize Dup UI spec does both).
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace SemanticsProbe
{
    public class VitalsOpen : MonoBehaviour
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string F(float f) => f.ToString("R", Inv);
        static string V3(Vector3 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
        static string B(Bounds b) => V3(b.min) + ".." + V3(b.max);
        static void L(string s) => Rec.Log("VO " + s);
        static int armedUntil = -1;
        static bool Armed => Time.frameCount <= armedUntil;
        static HarmonyLib.Harmony harmony;
        static VitalsOpen instance;

        public static void Install(HarmonyLib.Harmony h)
        {
            harmony = h;
            var self = typeof(VitalsOpen);
            h.Patch(HarmonyLib.AccessTools.Method(typeof(KScreenManager), "Update"),
                prefix: new HarmonyLib.HarmonyMethod(self, nameof(KsmBefore)), postfix: new HarmonyLib.HarmonyMethod(self, nameof(KsmAfter)));
            h.Patch(HarmonyLib.AccessTools.Method(typeof(TableScreen), "OnActivate"),
                prefix: new HarmonyLib.HarmonyMethod(self, nameof(ActivateBefore)), postfix: new HarmonyLib.HarmonyMethod(self, nameof(ActivateAfter)));
            h.Patch(HarmonyLib.AccessTools.Method(typeof(TableScreen), "ScreenUpdate"),
                prefix: new HarmonyLib.HarmonyMethod(self, nameof(ScreenUpdateBefore)), postfix: new HarmonyLib.HarmonyMethod(self, nameof(ScreenUpdateAfter)));
            h.Patch(HarmonyLib.AccessTools.Method(typeof(TableScreen), "RefreshRows"),
                prefix: new HarmonyLib.HarmonyMethod(self, nameof(RefreshBefore)), postfix: new HarmonyLib.HarmonyMethod(self, nameof(RefreshAfter)));
            h.Patch(HarmonyLib.AccessTools.Method(typeof(UnityEngine.UI.ScrollRect), "Rebuild"),
                prefix: new HarmonyLib.HarmonyMethod(self, nameof(RebuildBefore)));
            h.Patch(HarmonyLib.AccessTools.Method(typeof(UnityEngine.UI.ScrollRect), "OnRectTransformDimensionsChange"),
                prefix: new HarmonyLib.HarmonyMethod(self, nameof(DimsBefore)));
            h.Patch(HarmonyLib.AccessTools.Method(typeof(KScrollRect), "LateUpdate"),
                prefix: new HarmonyLib.HarmonyMethod(self, nameof(ScrollBefore)), postfix: new HarmonyLib.HarmonyMethod(self, nameof(ScrollAfter)));
        }

        public static void KsmBefore() { if (Armed) L("KScreenManager.Update begin"); }
        public static void KsmAfter() { if (Armed) L("KScreenManager.Update end"); }
        public static void BridgeBefore() { if (Armed) L("MainThreadBridge.Update begin"); }
        public static void BridgeAfter() { if (Armed) L("MainThreadBridge.Update end"); }

        static string Scale(Component c) => F(c.transform.localScale.y);

        static KScrollRect Tracked(Component c)
        {
            var k = c as KScrollRect;
            if (k == null || k.verticalScrollbar == null) return null;
            return k.GetComponentInParent<TableScreen>() == null ? null : k;
        }

        public static void RefreshBefore(TableScreen __instance) { if (Armed) L(__instance.GetType().Name + ".RefreshRows begin"); }
        public static void RefreshAfter(TableScreen __instance) { if (Armed) L(__instance.GetType().Name + ".RefreshRows end"); }

        public static void RebuildBefore(UnityEngine.UI.ScrollRect __instance, UnityEngine.UI.CanvasUpdate executing)
        {
            var k = Tracked(__instance);
            if (Armed && k != null) L("KScrollRect.Rebuild " + executing + " " + ScrollState(k) + " rebuilding=" + UnityEngine.UI.CanvasUpdateRegistry.IsRebuildingLayout());
        }

        public static void DimsBefore(UnityEngine.UI.ScrollRect __instance)
        {
            var k = Tracked(__instance);
            if (Armed && k != null) L("KScrollRect.OnRectTransformDimensionsChange " + ScrollState(k) + " rebuilding=" + UnityEngine.UI.CanvasUpdateRegistry.IsRebuildingLayout());
        }

        public static void ActivateBefore(TableScreen __instance)
        {
            if (!Armed && instance != null) instance.StartCoroutine(instance.EndOfFrames());
            armedUntil = Time.frameCount + 40;
            L(__instance.GetType().Name + ".OnActivate begin dt=" + F(Time.unscaledDeltaTime) + " scale=" + Scale(__instance) + " stack=" + System.Environment.StackTrace.Replace("\n", " | "));
        }

        public static void ActivateAfter(TableScreen __instance) => L(__instance.GetType().Name + ".OnActivate end scale=" + Scale(__instance));

        public static void ScreenUpdateBefore(TableScreen __instance) { if (Armed) L(__instance.GetType().Name + ".ScreenUpdate begin scale=" + Scale(__instance)); }
        public static void ScreenUpdateAfter(TableScreen __instance) { if (Armed) L(__instance.GetType().Name + ".ScreenUpdate end scale=" + Scale(__instance)); }

        static string ScrollState(KScrollRect s)
        {
            var bar = s.verticalScrollbar;
            var view = s.viewport != null ? s.viewport : (RectTransform)s.transform;
            return "ap=" + (s.content == null ? "none" : F(s.content.anchoredPosition.y) + " content=" + F(s.content.rect.height)) + " view=" + F(view.rect.height)
                + " sv=" + F(((RectTransform)s.transform).rect.height) + " bar=" + (bar == null ? "none" : F(bar.value) + "/" + F(bar.size))
                + (s.content == null ? "" : " apx=" + F(s.content.anchoredPosition.x) + " cw=" + F(s.content.rect.width) + " vw=" + F(view.rect.width)
                    + " cpos=" + V3(s.content.position) + " vpos=" + V3(view.position) + " cscale=" + V3(s.content.lossyScale))
                + " cb=" + B(HarmonyLib.Traverse.Create(s).Field("m_ContentBounds").GetValue<Bounds>());
        }

        static int geometryLogged;

        static string M(Matrix4x4 m)
        {
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    sb.Append(c == 0 && r == 0 ? "" : ",").Append(F(m[r, c]));
            return sb.ToString();
        }

        /// The inputs of the content's bounds in the viewport's space, bit for bit: the content's
        /// rect and world corners, the viewport's matrices and rect, and every transform from the
        /// content to the root (local position, rotation, scale; rect, anchors, pivot, sizeDelta,
        /// anchoredPosition).
        static void Geometry(KScrollRect s, string when)
        {
            var view = s.viewport != null ? s.viewport : (RectTransform)s.transform;
            var corners = new Vector3[4];
            s.content.GetWorldCorners(corners);
            L("geo " + when + " corners " + string.Join(" ", System.Array.ConvertAll(corners, V3)));
            L("geo " + when + " view.worldToLocal " + M(view.worldToLocalMatrix));
            L("geo " + when + " view.localToWorld " + M(view.localToWorldMatrix));
            L("geo " + when + " content.localToWorld " + M(s.content.localToWorldMatrix));
            var r = view.rect;
            L("geo " + when + " view.rect " + F(r.x) + "," + F(r.y) + "," + F(r.width) + "," + F(r.height) + " cb " + B(HarmonyLib.Traverse.Create(s).Field("m_ContentBounds").GetValue<Bounds>()));
            for (Transform t = s.content; t != null; t = t.parent)
            {
                var q = t.localRotation;
                string line = t.name + " lp=" + V3(t.localPosition) + " lr=" + F(q.x) + "," + F(q.y) + "," + F(q.z) + "," + F(q.w) + " ls=" + V3(t.localScale);
                if (t is RectTransform rt)
                {
                    var rr = rt.rect;
                    line += " rect=" + F(rr.x) + "," + F(rr.y) + "," + F(rr.width) + "," + F(rr.height) + " amin=" + F(rt.anchorMin.x) + "," + F(rt.anchorMin.y)
                        + " amax=" + F(rt.anchorMax.x) + "," + F(rt.anchorMax.y) + " piv=" + F(rt.pivot.x) + "," + F(rt.pivot.y)
                        + " sd=" + F(rt.sizeDelta.x) + "," + F(rt.sizeDelta.y) + " ap=" + F(rt.anchoredPosition.x) + "," + F(rt.anchoredPosition.y);
                }
                L("geo " + when + " chain " + line);
            }
        }

        public static void ScrollBefore(KScrollRect __instance)
        {
            if (!Armed) return;
            var screen = __instance.GetComponentInParent<TableScreen>();
            if (screen == null || __instance.verticalScrollbar == null) return;
            L(screen.GetType().Name + " KScrollRect.LateUpdate begin dt=" + F(Time.unscaledDeltaTime) + " scale=" + Scale(screen) + " " + ScrollState(__instance));
            if (geometryLogged < 3) Geometry(__instance, "before" + geometryLogged);
        }

        public static void ScrollAfter(KScrollRect __instance)
        {
            if (!Armed) return;
            var screen = __instance.GetComponentInParent<TableScreen>();
            if (screen == null || __instance.verticalScrollbar == null) return;
            L(screen.GetType().Name + " KScrollRect.LateUpdate end " + ScrollState(__instance));
            if (geometryLogged < 3) Geometry(__instance, "after" + geometryLogged++);
        }

        IEnumerator EndOfFrames()
        {
            while (Armed)
            {
                yield return new WaitForEndOfFrame();
                L("end of frame");
            }
        }

        void Awake() => instance = this;

        IEnumerator Start()
        {
            while (Game.Instance == null) yield return new WaitForSecondsRealtime(1f);
            var bridge = HarmonyLib.AccessTools.TypeByName("OniMcp.Server.MainThreadBridge");
            L("colony loaded; bridge " + (bridge == null ? "not found" : "patched"));
            if (bridge != null)
                harmony.Patch(HarmonyLib.AccessTools.Method(bridge, "Update"),
                    prefix: new HarmonyLib.HarmonyMethod(typeof(VitalsOpen), nameof(BridgeBefore)),
                    postfix: new HarmonyLib.HarmonyMethod(typeof(VitalsOpen), nameof(BridgeAfter)));
        }

        void Update() { if (Armed) L("frame dt=" + F(Time.unscaledDeltaTime)); }
    }
}
