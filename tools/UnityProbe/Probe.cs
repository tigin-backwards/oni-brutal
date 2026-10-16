// A mod for the ORIGINAL game (Unity player) that records Unity's lifecycle semantics in
// Player.log (lines starting with "PROBE"): when OnDisable/OnDestroy run around Destroy, whether
// objects awaiting destruction are enabled again, the order of OnApplicationQuit. Its results
// are what the engine's Lifecycle reproduces (LifecycleTests). See README.md.
using System.Collections;
using UnityEngine;

namespace SemanticsProbe
{
    public class Mod : KMod.UserMod2
    {
        public override void OnLoad(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(HarmonyLib.AccessTools.Method(typeof(Game), "OnApplicationQuit"), prefix: new HarmonyLib.HarmonyMethod(typeof(Mod), nameof(GameQuitPrefix)), postfix: new HarmonyLib.HarmonyMethod(typeof(Mod), nameof(GameQuitPostfix)));
            harmony.Patch(HarmonyLib.AccessTools.Method(typeof(DetailsScreen), "OnCmpDisable"), prefix: new HarmonyLib.HarmonyMethod(typeof(Mod), nameof(DetailsDisablePrefix)));
            var go = new GameObject("SemanticsProbe");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Probe>();
            go.AddComponent<QuitLog>().Tag = "ddol-at-load";
            go.AddComponent<FrameLog>();
            go.AddComponent<UiDump>();
            Rec.Log("OnLoad renderedFrameCount=" + Time.renderedFrameCount + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        public static void GameQuitPrefix()
        {
            var d = DetailsScreen.Instance;
            Rec.Log("Game.OnApplicationQuit prefix: DetailsScreen " + (d == null ? "null" : "activeInHierarchy=" + d.gameObject.activeInHierarchy + " isActiveAndEnabled=" + d.isActiveAndEnabled) + " App.IsExiting=" + App.IsExiting);
        }

        public static void GameQuitPostfix(Game __instance)
        {
            var d = DetailsScreen.Instance;
            var mgr = HarmonyLib.Traverse.Create(__instance).Field("screenMgr").GetValue<UnityEngine.Object>();
            var mgrGo = mgr is Component c ? c.gameObject : mgr as GameObject;
            Rec.Log("Game.OnApplicationQuit postfix: screenMgr=" + (mgrGo == null ? "null" : mgrGo.name) + " details under it=" + (d != null && mgrGo != null && d.transform.IsChildOf(mgrGo.transform)) + " details isActiveAndEnabled=" + (d != null && d.isActiveAndEnabled) + " details path=" + (d == null ? "" : Path(d.transform)) + " mgr comp enabled=" + (mgr is Behaviour b && b.isActiveAndEnabled));
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        public static void DetailsDisablePrefix() => Rec.Log("DetailsScreen.OnCmpDisable called, App.IsExiting=" + App.IsExiting + " AudioMixer.instance null=" + (AudioMixer.instance == null));
    }

    public class Rec : MonoBehaviour
    {
        public string Tag;
        public Rec Other;
        public bool DestroyOnDisable;
        public static void Log(string s) => Debug.Log("PROBE f" + Time.frameCount + " " + s);
        void Awake() => Log(Tag + " Awake");
        void OnEnable() => Log(Tag + " OnEnable");
        void OnDisable()
        {
            Log(Tag + " OnDisable other=" + (Other == null ? "null" : "alive"));
            if (!DestroyOnDisable) return;
            try { Destroy(Other.gameObject); Log(Tag + " destroyed other ok"); }
            catch (System.Exception e) { Log(Tag + " EXCEPTION " + e.GetType().Name); }
            Destroy(gameObject);
        }
        void OnDestroy() => Log(Tag + " OnDestroy other=" + (Other == null ? "null" : "alive"));
    }

    public class Upd : MonoBehaviour
    {
        public static bool Armed;
        void Update()
        {
            Rec.Log("U Update");
            if (Armed) { Armed = false; Destroy(gameObject); Rec.Log("U destroyed self in Update, enabled=" + enabled); }
        }
        void LateUpdate() => Rec.Log("U LateUpdate");
        void OnDisable() => Rec.Log("U OnDisable");
        void OnDestroy() => Rec.Log("U OnDestroy");
    }

    /// When `yield return null` resumes, for coroutines started from Start, Update and LateUpdate.
    public class CoTiming : MonoBehaviour
    {
        int updates, lates;
        IEnumerator Co(string from)
        {
            Rec.Log("co[" + from + "] started");
            yield return null;
            Rec.Log("co[" + from + "] after 1st null");
            yield return null;
            Rec.Log("co[" + from + "] after 2nd null");
            yield return new WaitForEndOfFrame();
            Rec.Log("co[" + from + "] after WaitForEndOfFrame");
            yield return null;
            Rec.Log("co[" + from + "] after null following end of frame");
        }
        void Start() { Rec.Log("CoTiming Start"); StartCoroutine(Co("Start")); }
        void Update() { Rec.Log("CoTiming Update"); if (updates++ == 1) StartCoroutine(Co("Update")); }
        void LateUpdate() { Rec.Log("CoTiming LateUpdate"); if (lates++ == 3) StartCoroutine(Co("LateUpdate")); if (lates > 8) enabled = false; }
    }

    /// The first frames: frame counters and the active scene at Update and LateUpdate.
    public class FrameLog : MonoBehaviour
    {
        int n;
        void Update() { if (n < 12) Rec.Log("Update rendered=" + Time.renderedFrameCount + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name); }
        void LateUpdate() { if (n++ < 12) Rec.Log("LateUpdate rendered=" + Time.renderedFrameCount + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name); }
    }

    public class QuitLog : MonoBehaviour
    {
        public string Tag;
        void OnApplicationQuit() => Rec.Log("QUIT " + Tag + " App.IsExiting=" + App.IsExiting);
    }

    public class Probe : MonoBehaviour
    {
        Rec quitVictim;
        void OnApplicationQuit()
        {
            Rec.Log("Probe OnApplicationQuit: destroying QV");
            if (quitVictim != null) Destroy(quitVictim.gameObject);
            Rec.Log("Probe OnApplicationQuit: after Destroy, QV enabled=" + (quitVictim != null && quitVictim.enabled));
        }

        static Rec Make(string tag, Transform parent)
        {
            var go = new GameObject(tag);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<Rec>();
            r.Tag = tag;
            return r;
        }

        IEnumerator Start()
        {
            yield return null;
            Rec.Log("=== scenario 1: disable (destroys sibling + self), reactivate, same frame");
            var p = new GameObject("P").transform;
            var x = Make("X", p);
            var a = Make("A", p);
            a.Other = x; a.DestroyOnDisable = true;
            yield return null;
            p.gameObject.SetActive(false);
            Rec.Log("after SetActive(false)");
            p.gameObject.SetActive(true);
            Rec.Log("after SetActive(true)");
            yield return null;
            Rec.Log("scenario 1 next frame: A=" + (a == null ? "null" : "alive") + " X=" + (x == null ? "null" : "alive"));

            Rec.Log("=== scenario 2: Destroy(B) then Destroy(C), order of callbacks");
            var b = Make("B", null); var c = Make("C", null); var d = Make("D", null);
            b.Other = c; c.Other = b; d.Other = b;
            yield return null;
            Destroy(b.gameObject); Destroy(c.gameObject); Destroy(d.gameObject);
            Rec.Log("requested B, C, D");
            yield return null;
            Rec.Log("=== scenario 3: Destroy(X2) then Destroy(A2) (A2 active, OnDisable reads X2)");
            var x2 = Make("X2", null); var a2 = Make("A2", null); a2.Other = x2;
            yield return null;
            Destroy(x2.gameObject); Destroy(a2.gameObject);
            yield return null;
            Rec.Log("=== scenario 4: SetActive(true) on an object pending destroy");
            var e = Make("E", null);
            yield return null;
            e.gameObject.SetActive(false);
            Destroy(e.gameObject);
            e.gameObject.SetActive(true);
            Rec.Log("E after reactivation enabled=" + e.isActiveAndEnabled);
            yield return null;
            Rec.Log("=== scenario 5: flags after Destroy(go), child included");
            var f = Make("F", null); var fc = Make("FC", f.transform);
            yield return null;
            Destroy(f.gameObject);
            Rec.Log("F activeSelf=" + f.gameObject.activeSelf + " activeInHierarchy=" + f.gameObject.activeInHierarchy + " enabled=" + f.enabled + " isActiveAndEnabled=" + f.isActiveAndEnabled + " FC activeInHierarchy=" + fc.gameObject.activeInHierarchy + " FC isActiveAndEnabled=" + fc.isActiveAndEnabled + " F==null " + (f == null));
            yield return null;
            Rec.Log("=== scenario 6: Destroy(component)");
            var g = Make("G", null);
            yield return null;
            Destroy(g);
            Rec.Log("G after Destroy(component): enabled=" + g.enabled + " isActiveAndEnabled=" + g.isActiveAndEnabled + " null=" + (g == null));
            yield return null;
            Rec.Log("=== scenario 7: Destroy(go, 0.3)");
            var h = Make("H", null);
            yield return null;
            Destroy(h.gameObject, 0.3f);
            Rec.Log("H requested delayed");
            yield return new WaitForSeconds(0.6f);
            Rec.Log("=== scenario 8: Update on a pending-destroy object this frame");
            var u = new GameObject("U").AddComponent<Upd>();
            yield return null; yield return null;
            Upd.Armed = true;
            yield return null; yield return null;
            Rec.Log("=== scenario 9: SetActive(false) then Destroy, then parent toggled");
            var q = Make("Q", null); var qc = Make("QC", q.transform);
            yield return null;
            qc.gameObject.SetActive(false);
            Destroy(qc.gameObject);
            Rec.Log("QC activeSelf after destroy=" + qc.gameObject.activeSelf);
            q.gameObject.SetActive(false); q.gameObject.SetActive(true);
            Rec.Log("QC after parent toggle activeInHierarchy=" + qc.gameObject.activeInHierarchy);
            yield return null;
            Rec.Log("=== scenario 10: deep hierarchy destroyed from the root");
            var d0 = Make("D0", null); var d1 = Make("D1", d0.transform); var d2 = Make("D2", d1.transform); var d3 = Make("D3", d2.transform); var d4 = Make("D4", d3.transform);
            yield return null;
            Destroy(d0.gameObject);
            Rec.Log("after Destroy(D0): D4 isActiveAndEnabled=" + d4.isActiveAndEnabled);
            yield return null;
            quitVictim = Make("QV", null);
            new GameObject("late").AddComponent<QuitLog>().Tag = "scene-late";
            Rec.Log("=== scenario 11: coroutine timing");
            new GameObject("CoTiming").AddComponent<CoTiming>();
            for (int i = 0; i < 10; i++) yield return null;
            yield return Values.Run();
            Rec.Log("=== done");
        }
    }
}
