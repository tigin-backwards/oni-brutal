// The order Resources.FindObjectsOfTypeAll returns objects in, lines "PROBE ... FO ...", 10 s
// into the main menu. Duplicant Status Bar clones the first usable LocText it returns, so the
// order decides which label's pivot the mod's names get. It logs the LocTexts the call returns
// (instance id, scene, pivot) and whether their ids run in order, then objects made here in a
// known order, with some destroyed and others made after, as components and ScriptableObjects.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SemanticsProbe
{
    public class FoMarker : MonoBehaviour { }

    public class FoAsset : ScriptableObject { }

    public class FindOrder : MonoBehaviour
    {
        static void L(string s) => Rec.Log("FO " + s);

        static string Order(IList<int> ids)
        {
            bool asc = true, desc = true;
            for (int i = 1; i < ids.Count; i++)
            {
                if (ids[i] <= ids[i - 1]) asc = false;
                if (ids[i] >= ids[i - 1]) desc = false;
            }
            return asc ? "ascending" : desc ? "descending" : "unordered";
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        static void Markers(string label)
        {
            var found = Resources.FindObjectsOfTypeAll<FoMarker>();
            L(label + " markers " + string.Join(" ", found.Select(m => m.name + ":" + m.GetInstanceID())) + " ids " + Order(found.Select(m => m.GetInstanceID()).ToList()));
            var assets = Resources.FindObjectsOfTypeAll<FoAsset>();
            L(label + " assets " + string.Join(" ", assets.Select(m => m.name + ":" + m.GetInstanceID())) + " ids " + Order(assets.Select(m => m.GetInstanceID()).ToList()));
        }

        static FoMarker Make(string name, bool active = true)
        {
            var go = new GameObject(name);
            go.SetActive(active);
            return go.AddComponent<FoMarker>();
        }

        static FoAsset MakeAsset(string name)
        {
            var a = ScriptableObject.CreateInstance<FoAsset>();
            a.name = name;
            return a;
        }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(10f);
            L("begin");
            var labels = Resources.FindObjectsOfTypeAll<LocText>();
            var ids = labels.Select(t => t.GetInstanceID()).ToList();
            L("loctext count " + labels.Length + " ids " + Order(ids) + " negative " + ids.Count(i => i < 0) + " positive " + ids.Count(i => i > 0));
            int firstNegative = ids.FindIndex(i => i < 0), lastPositive = ids.FindLastIndex(i => i > 0);
            L("loctext firstNegative " + firstNegative + " lastPositive " + lastPositive);
            for (int i = 0; i < labels.Length; i++)
            {
                var t = labels[i];
                var rt = t.rectTransform;
                L("loctext " + i + " id " + ids[i] + " scene \"" + t.gameObject.scene.name + "\" active " + t.gameObject.activeInHierarchy
                    + " pivot " + rt.pivot.x + "," + rt.pivot.y + " path " + Path(t.transform));
            }

            var a = Make("A");
            var b = Make("B");
            var c = Make("C", active: false);
            var d = Make("D");
            var e = Make("E");
            var sa = MakeAsset("SA");
            var sb = MakeAsset("SB");
            var sc = MakeAsset("SC");
            Markers("made");
            DestroyImmediate(b.gameObject);
            DestroyImmediate(sb);
            Markers("destroyed B");
            var f = Make("F");
            var sd = MakeAsset("SD");
            Markers("made F");
            DestroyImmediate(d.gameObject);
            DestroyImmediate(a.gameObject);
            var g = Make("G");
            var h = Make("H");
            Markers("destroyed D A made G H");
            var parent = Make("P");
            var child = Make("Q");
            child.transform.SetParent(parent.transform, false);
            Markers("made P Q");
            yield return null;
            Markers("next frame");
            var copy = Instantiate(e.gameObject);
            copy.name = "E2";
            Markers("instantiated E");
            L("end");
        }
    }
}
