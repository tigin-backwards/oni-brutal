using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class StartLogger : MonoBehaviour
    {
        public static readonly List<string> Log = new();
        public string Tag = "";
        void Start() => Log.Add(Tag);
    }

    /// Start-order probes like UnityProbe's StartSets: Start only, or Start and Update or
    /// LateUpdate (the first call logged).
    public class StartTagged : MonoBehaviour { public string Tag = ""; }
    public class StartN : StartTagged { void Start() => StartLogger.Log.Add(Tag + ":Start"); }
    public class StartU : StartTagged
    {
        bool once;
        void Start() => StartLogger.Log.Add(Tag + ":Start");
        void Update() { if (!once) StartLogger.Log.Add(Tag + ":Update"); once = true; }
    }
    public class StartL : StartTagged
    {
        bool once;
        void Start() => StartLogger.Log.Add(Tag + ":Start");
        void LateUpdate() { if (!once) StartLogger.Log.Add(Tag + ":LateUpdate"); once = true; }
    }

    public static class StartSet
    {
        /// Objects a and b, each with StartN, StartU, StartN, StartL tagged NAME#INDEX.
        public static void Make(string prefix)
        {
            foreach (var name in new[] { "a", "b" })
            {
                var go = new GameObject(prefix + name);
                go.SetActive(false);
                go.AddComponent<StartN>(); go.AddComponent<StartU>(); go.AddComponent<StartN>(); go.AddComponent<StartL>();
                var all = go.GetComponents<StartTagged>();
                for (int i = 0; i < all.Length; i++) all[i].Tag = prefix + name + "#" + i;
                go.SetActive(true);
            }
        }
    }

    public class SetMakingStart : MonoBehaviour
    {
        public string Prefix = "";
        void Start() { StartLogger.Log.Add("spawner:Start"); StartSet.Make(Prefix); }
    }

    public class LateSpawner : MonoBehaviour
    {
        bool done;
        void LateUpdate() { if (done) return; done = true; new GameObject("spawner").AddComponent<SetMakingStart>(); }
    }

    public class CoroutineSpawner : MonoBehaviour
    {
        void Start() => StartCoroutine(Make());
        IEnumerator Make()
        {
            yield return null;
            StartLogger.Log.Add("made");
            StartSet.Make("");
        }
    }

    public class AddedLog : MonoBehaviour
    {
        public virtual string Name => "?";
        bool once;
        protected virtual void Awake() => StartLogger.Log.Add(Name + ":Awake");
        void OnEnable() => StartLogger.Log.Add(Name + ":OnEnable");
        void Start() => StartLogger.Log.Add(Name + ":Start");
        void Update() { if (!once) StartLogger.Log.Add(Name + ":Update"); once = true; }
    }
    public class AddingA : AddedLog
    {
        public override string Name => "A";
        protected override void Awake() { base.Awake(); if (GetComponent<AddedC>() == null) gameObject.AddComponent<AddedC>(); }
    }
    public class FollowingB : AddedLog { public override string Name => "B"; }
    public class AddedC : AddedLog { public override string Name => "C"; }

    public class StartOrderTests : EngineTest
    {
        /// Measured in the original (UnityProbe scenario 19): a component that another one's
        /// Awake adds while the object activates (the game's MyCmpAdd) wakes and is enabled at
        /// once, but starts and updates after the component that added it, as if queued when
        /// that one's activation began.
        [Fact]
        public void AComponentAddedByAnAwakeStartsAndUpdatesAfterItsAdder()
        {
            WithoutFixedSteps(() =>
            {
                var go = new GameObject("adder");
                go.SetActive(false);
                go.AddComponent<AddingA>();
                go.AddComponent<FollowingB>();
                StartLogger.Log.Clear();
                go.SetActive(true);
                Assert.Equal(new[] { "A:Awake", "C:Awake", "C:OnEnable", "A:OnEnable", "B:Awake", "B:OnEnable" }, StartLogger.Log);
                Assert.Equal(new[] { "A", "B", "C" }, System.Array.ConvertAll(go.GetComponents<AddedLog>(), c => c.Name));
                StartLogger.Log.Clear();
                Frame();
                Assert.Equal(new[] { "A:Start", "C:Start", "B:Start", "A:Update", "C:Update", "B:Update" }, StartLogger.Log);
            });
        }

        /// Objects made or activated in one frame start in that order, measured in the original
        /// (tools/UnityProbe, scenario 12): created, cloned and activated alike.
        [Fact]
        public void StartsRunInCreationAndActivationOrder()
        {
            StartLogger.Log.Clear();
            for (int i = 0; i < 4; i++) new GameObject("S" + i).AddComponent<StartLogger>().Tag = "new" + i;
            var template = new GameObject("T");
            template.SetActive(false);
            template.AddComponent<StartLogger>();
            for (int i = 0; i < 4; i++)
            {
                var clone = Object.Instantiate(template);
                clone.GetComponent<StartLogger>().Tag = "clone" + i;
                clone.SetActive(true);
            }
            var inactive = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                inactive[i] = Object.Instantiate(template);
                inactive[i].GetComponent<StartLogger>().Tag = "activated" + i;
            }
            for (int i = 0; i < 4; i++) inactive[i].SetActive(true);
            Frame();
            Assert.Equal(new[] { "new0", "new1", "new2", "new3", "clone0", "clone1", "clone2", "clone3", "activated0", "activated1", "activated2", "activated3" }, StartLogger.Log);
        }

        /// No fixed step in the frames of a test, so that the pass after FixedUpdate does not run.
        static void WithoutFixedSteps(System.Action body)
        {
            float step = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 1e6f;
            try { body(); }
            finally { Time.fixedDeltaTime = step; }
        }

        /// Measured in the original (UnityProbe scenario 17): objects a Start makes in the
        /// frame's first pass of pending Starts do not start in that pass. Those with Update
        /// start just before their first Update, all objects' together; the rest start after
        /// Update in creation order; LateUpdate follows. The game's SaveLoader loads a save from
        /// such a Start, which is why the Duplicants' StandardWorker starts (and spawns their
        /// MinionIdentity) before their other components.
        [Fact]
        public void ObjectsMadeByAStartStartTheirUpdateBehavioursFirst()
        {
            WithoutFixedSteps(() =>
            {
                Frame();
                StartLogger.Log.Clear();
                new GameObject("spawner").AddComponent<SetMakingStart>();
                Frame();
                Assert.Equal(new[]
                {
                    "spawner:Start",
                    "a#1:Start", "a#1:Update", "b#1:Start", "b#1:Update",
                    "a#0:Start", "a#2:Start", "a#3:Start", "b#0:Start", "b#2:Start", "b#3:Start",
                    "a#3:LateUpdate", "b#3:LateUpdate",
                }, StartLogger.Log);
            });
        }

        /// Measured in the original (UnityProbe scenario 13): objects made by a coroutine after
        /// Update start those with LateUpdate just before it; the others start after LateUpdate,
        /// and get their first Update the next frame.
        [Fact]
        public void ObjectsMadeAfterUpdateStartTheirLateUpdateBehavioursFirst()
        {
            WithoutFixedSteps(() =>
            {
                new GameObject("co").AddComponent<CoroutineSpawner>();
                Frame();
                StartLogger.Log.Clear();
                Frame();
                Assert.Equal(new[]
                {
                    "made",
                    "a#3:Start", "a#3:LateUpdate", "b#3:Start", "b#3:LateUpdate",
                    "a#0:Start", "a#1:Start", "a#2:Start", "b#0:Start", "b#1:Start", "b#2:Start",
                }, StartLogger.Log);
                StartLogger.Log.Clear();
                Frame();
                Assert.Equal(new[] { "a#1:Update", "b#1:Update" }, StartLogger.Log);
            });
        }

        /// Measured in the original (UnityProbe scenarios 14 and 17): a Start that runs in the
        /// pass after LateUpdate makes objects that start in the next frame's first pass, in
        /// creation order.
        [Fact]
        public void ObjectsMadeByAStartAfterLateUpdateStartNextFrameInOrder()
        {
            WithoutFixedSteps(() =>
            {
                new GameObject("late").AddComponent<LateSpawner>();
                StartLogger.Log.Clear();
                Frame();
                Assert.Equal(new[] { "spawner:Start" }, StartLogger.Log);
                StartLogger.Log.Clear();
                Frame();
                Assert.Equal(new[]
                {
                    "a#0:Start", "a#1:Start", "a#2:Start", "a#3:Start", "b#0:Start", "b#1:Start", "b#2:Start", "b#3:Start",
                    "a#1:Update", "b#1:Update", "a#3:LateUpdate", "b#3:LateUpdate",
                }, StartLogger.Log);
            });
        }
    }
}
