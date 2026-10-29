// A KScrollRect's normalized position and its inputs to the bit (lines "PROBE ... SG ..."), in
// any screen. Found with the side screens that kept the scroll-normalized-position finding after
// worldToLocalMatrix followed the original's (Better Checkpoints' AccessControlSideScreen, Buildable
// Wild Plant's PlanterSideScreen). After each LateUpdate of a KScrollRect whose hierarchy path
// contains PROBE_SG_NAME, when anything logged changed (at most 30 times per scroll rect): the
// normalized position, the content's world corners, the view's worldToLocalMatrix and
// localToWorldMatrix, and every level from the content and from the view to the root (local
// position, rotation and scale; for RectTransforms rect, anchors, pivot, sizeDelta and
// anchoredPosition), as float bits in hex. Run with PROBE_SCENARIO=scrollgeo in a spec that opens
// the screen.
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SemanticsProbe
{
    public static class ScrollGeo
    {
        static readonly string Name = System.Environment.GetEnvironmentVariable("PROBE_SG_NAME") ?? "SideScreen";
        static readonly Dictionary<int, string> last = new Dictionary<int, string>();
        static readonly Dictionary<int, int> count = new Dictionary<int, int>();

        static string H(float f) => System.BitConverter.ToInt32(System.BitConverter.GetBytes(f), 0).ToString("x8");
        static string H(Vector2 v) => H(v.x) + "," + H(v.y);
        static string H(Vector3 v) => H(v.x) + "," + H(v.y) + "," + H(v.z);

        static string H(Matrix4x4 m)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 16; i++) sb.Append(i == 0 ? "" : ",").Append(H(m[i / 4, i % 4]));
            return sb.ToString();
        }

        static string Path(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static string Chain(Transform t)
        {
            var sb = new StringBuilder();
            for (; t != null; t = t.parent)
            {
                var q = t.localRotation;
                sb.Append(" [").Append(t.name).Append(" lp=").Append(H(t.localPosition)).Append(" lr=").Append(H(q.x)).Append(',').Append(H(q.y)).Append(',')
                    .Append(H(q.z)).Append(',').Append(H(q.w)).Append(" ls=").Append(H(t.localScale));
                if (t is RectTransform rt)
                {
                    var r = rt.rect;
                    sb.Append(" rect=").Append(H(r.x)).Append(',').Append(H(r.y)).Append(',').Append(H(r.width)).Append(',').Append(H(r.height))
                        .Append(" amin=").Append(H(rt.anchorMin)).Append(" amax=").Append(H(rt.anchorMax)).Append(" piv=").Append(H(rt.pivot))
                        .Append(" sd=").Append(H(rt.sizeDelta)).Append(" ap=").Append(H(rt.anchoredPosition));
                }
                sb.Append(']');
            }
            return sb.ToString();
        }

        public static void Install(HarmonyLib.Harmony h)
        {
            var target = HarmonyLib.AccessTools.Method(typeof(KScrollRect), "LateUpdate");
            h.Patch(target, postfix: new HarmonyLib.HarmonyMethod(typeof(ScrollGeo), nameof(After)));
            Rec.Log("SG installed on " + target + " name=" + Name);
        }

        static void After(KScrollRect __instance)
        {
            var s = __instance;
            if (s == null || s.content == null) return;
            var path = Path(s.transform);
            if (!path.Contains(Name)) return;
            int id = s.GetInstanceID();
            count.TryGetValue(id, out int n);
            if (n >= 30) return;
            var view = s.viewport != null ? s.viewport : (RectTransform)s.transform;
            var corners = new Vector3[4];
            s.content.GetWorldCorners(corners);
            var sb = new StringBuilder();
            sb.Append(" np=").Append(H(s.normalizedPosition));
            sb.Append(" corners=");
            for (int i = 0; i < 4; i++) sb.Append(i == 0 ? "" : ",").Append(H(corners[i]));
            sb.Append(" vw2l=").Append(H(view.worldToLocalMatrix)).Append(" vl2w=").Append(H(view.localToWorldMatrix));
            sb.Append(" content:").Append(Chain(s.content)).Append(" view:").Append(Chain(view));
            var text = sb.ToString();
            if (last.TryGetValue(id, out var prev) && prev == text) return;
            last[id] = text;
            count[id] = n + 1;
            Rec.Log("SG f" + Time.frameCount + " " + path.Replace(' ', '_') + text);
        }
    }
}
