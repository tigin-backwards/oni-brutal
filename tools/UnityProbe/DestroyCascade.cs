// Destroy called from an OnDestroy while the player carries out Destroy requests, lines
// "PROBE ... DC ...": an owner whose OnDestroy calls Destroy on another object with a child (as
// DiseaseSourceVisualizer.OnCleanUp destroys its ResourceVisualizer), the owner destroyed in
// Update, in LateUpdate (with and without fixed steps in the next frame) and with DestroyImmediate.
// Is the other object destroyed in the same pass, or at a later destruction point, and which?
// U60's CellChangeMonitor.RenderEveryTick, in Game.Update, reads the GameObject of every KObject
// whose transform was marked moved; a destroyed child's KObject is never released, so a child
// destroyed between the move and Game.Update throws there.
using System.Collections;
using UnityEngine;

namespace SemanticsProbe
{
    public class DcRec : MonoBehaviour
    {
        public string Tag;
        public GameObject DestroyOnDestroy;
        void OnDisable() => Rec.Log("DC " + Tag + " OnDisable");
        void OnDestroy()
        {
            Rec.Log("DC " + Tag + " OnDestroy");
            if (DestroyOnDestroy != null) { Destroy(DestroyOnDestroy); Rec.Log("DC " + Tag + " destroyed its other"); }
        }
    }

    public class DestroyCascade : MonoBehaviour
    {
        GameObject owner, other, child;
        int mode; // 1: destroy the owner in Update, 2: in LateUpdate
        int watch;

        static void L(string s) => Rec.Log("DC " + s);

        void Make(string tag)
        {
            other = new GameObject(tag + "-other");
            other.AddComponent<DcRec>().Tag = tag + "-other";
            child = new GameObject(tag + "-child");
            child.transform.SetParent(other.transform, false);
            child.AddComponent<DcRec>().Tag = tag + "-child";
            owner = new GameObject(tag + "-owner");
            var r = owner.AddComponent<DcRec>();
            r.Tag = tag + "-owner";
            r.DestroyOnDestroy = other;
        }

        string State() => "owner=" + (owner == null ? "null" : "alive") + " other=" + (other == null ? "null" : "alive") + " child=" + (child == null ? "null" : "alive");

        void FixedUpdate()
        {
            if (watch > 0) L("FixedUpdate " + State());
        }

        void Update()
        {
            if (watch > 0) { L("Update " + State()); watch--; }
            if (mode == 1 && owner != null) { Destroy(owner); L("Update: Destroy(owner) " + State()); mode = 0; watch = 3; }
        }

        void LateUpdate()
        {
            if (watch > 0) L("LateUpdate " + State());
            if (mode == 2 && owner != null) { Destroy(owner); L("LateUpdate: Destroy(owner) " + State()); mode = 0; watch = 3; }
        }

        IEnumerator Start()
        {
            float fixedStep = Time.fixedDeltaTime;
            for (int i = 0; i < 5; i++) yield return null;
            L("=== 1: owner destroyed in Update");
            Make("U");
            yield return null;
            mode = 1;
            for (int i = 0; i < 6; i++) yield return null;

            L("=== 2: owner destroyed in LateUpdate (fixedDeltaTime " + fixedStep + ")");
            Make("L");
            yield return null;
            mode = 2;
            for (int i = 0; i < 6; i++) yield return null;

            L("=== 3: owner destroyed in LateUpdate, no fixed steps (fixedDeltaTime 100)");
            Time.fixedDeltaTime = 100f;
            Make("N");
            yield return null;
            mode = 2;
            for (int i = 0; i < 6; i++) yield return null;

            L("=== 4: owner destroyed in LateUpdate, fixed steps every frame (fixedDeltaTime 0.0001)");
            Time.fixedDeltaTime = 0.0001f;
            Time.maximumDeltaTime = 0.0003f;
            Make("F");
            yield return null;
            mode = 2;
            for (int i = 0; i < 6; i++) yield return null;
            Time.fixedDeltaTime = fixedStep;
            Time.maximumDeltaTime = 1f / 3f;

            L("=== 5: owner destroyed with DestroyImmediate in a coroutine");
            Make("I");
            yield return null;
            DestroyImmediate(owner);
            L("after DestroyImmediate(owner) " + State());
            watch = 3;
            for (int i = 0; i < 6; i++) yield return null;

            var done = new GameObject("DestroyCascadeProbeDone", typeof(RectTransform));
            DontDestroyOnLoad(done);
            done.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            L("=== done");
        }
    }
}
