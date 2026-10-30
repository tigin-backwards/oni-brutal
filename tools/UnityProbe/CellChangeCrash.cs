// U60's CellChangeMonitor around a destroyed child, lines "PROBE ... CC ...", at the main menu
// with Klei's own CellChangeMonitor (a grid set for the probe): a "visualizer" whose child carries
// a KMonoBehaviour (so a KObject) is moved in Update, after RenderEveryTick, as
// DiseaseSourceVisualizer.Update moves its ResourceVisualizer after Game.Update; an "owner" whose
// OnDestroy destroys the visualizer (DiseaseSourceVisualizer.OnCleanUp) is destroyed in
// LateUpdate, as a critter dies from KBatchedAnimUpdater.LateUpdate. Each frame's Update first
// calls RenderEveryTick, as Game.Update does, and logs whether it threw: once with no fixed step
// in the next frame and once with fixed steps every frame.
using System.Collections;
using UnityEngine;

namespace SemanticsProbe
{
    public class CcKmb : KMonoBehaviour { }

    public class CellChangeCrash : MonoBehaviour
    {
        GameObject owner, vis, child;
        bool move, destroyOwner;
        int watch;

        static void L(string s) => Rec.Log("CC " + s);

        void Make(string tag)
        {
            vis = new GameObject(tag + "-vis");
            child = new GameObject(tag + "-child");
            child.transform.SetParent(vis.transform, false);
            child.AddComponent<CcKmb>();
            owner = new GameObject(tag + "-owner");
            var r = owner.AddComponent<DcRec>();
            r.Tag = tag + "-owner";
            r.DestroyOnDestroy = vis;
        }

        void Update()
        {
            var ccm = Singleton<CellChangeMonitor>.Instance;
            if (ccm == null) return;
            try
            {
                ccm.RenderEveryTick();
                if (watch > 0) L("RenderEveryTick ok, child=" + (child == null ? "null" : "alive"));
            }
            catch (System.Exception e)
            {
                L("RenderEveryTick threw " + e.GetType().Name + ", child=" + (child == null ? "null" : "alive"));
            }
            if (watch > 0) watch--;
            // DiseaseSourceVisualizer.Update: disabled with its owner when that is destroyed.
            if (move && owner != null && vis != null) vis.transform.SetPosition(new Vector3(10.5f, 10.5f + Time.frameCount % 3, 0f));
        }

        void LateUpdate()
        {
            if (!destroyOwner) return;
            destroyOwner = false;
            Destroy(owner);
            L("LateUpdate: Destroy(owner)");
            watch = 3;
        }

        IEnumerator Run(string tag, float fixedStep, float maxDelta)
        {
            Make(tag);
            move = true;
            yield return null;
            yield return null;
            Time.fixedDeltaTime = fixedStep;
            Time.maximumDeltaTime = maxDelta;
            destroyOwner = true;
            for (int i = 0; i < 5; i++) yield return null;
            move = false;
        }

        IEnumerator Start()
        {
            float fixedStep = Time.fixedDeltaTime, maxDelta = Time.maximumDeltaTime;
            for (int i = 0; i < 5; i++) yield return null;
            bool created = Singleton<CellChangeMonitor>.Instance == null;
            if (created) Singleton<CellChangeMonitor>.CreateInstance();
            Singleton<CellChangeMonitor>.Instance.SetGridSize(256, 256);
            L("monitor " + (created ? "created" : "found") + ", KObjectManager " + (KObjectManager.Instance != null ? "present" : "absent") + ", fixedDeltaTime " + fixedStep + ", maximumDeltaTime " + maxDelta);

            L("=== 1: no fixed step in the next frame");
            yield return Run("N", 100f, maxDelta);
            L("=== 2: fixed steps every frame");
            yield return Run("F", 0.0001f, 0.0003f);
            Time.fixedDeltaTime = fixedStep;
            Time.maximumDeltaTime = maxDelta;
            for (int i = 0; i < 3; i++) yield return null;
            Singleton<CellChangeMonitor>.Instance.SetGridSize(0, 0);
            if (created) Singleton<CellChangeMonitor>.DestroyInstance();

            var done = new GameObject("CellChangeCrashProbeDone", typeof(RectTransform));
            DontDestroyOnLoad(done);
            done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            L("=== done");
        }
    }
}
