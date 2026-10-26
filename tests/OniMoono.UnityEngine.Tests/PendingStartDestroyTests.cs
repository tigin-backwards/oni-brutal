using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    /// UnityProbe scenario 20's parts: Start, OnDisable and OnDestroy logged with whether Start ran.
    public class PendingPart : MonoBehaviour
    {
        public static readonly List<string> Log = new();
        public string Tag = "";
        public bool Started;
        void Start() { Started = true; Log.Add(Tag + ":Start"); }
        void OnDisable() => Log.Add(Tag + ":OnDisable started=" + Started);
        void OnDestroy() => Log.Add(Tag + ":OnDestroy started=" + Started);
    }
    public class PendingPartU : PendingPart { void Update() { } }
    public class PendingPartL : PendingPart { void LateUpdate() { } }

    /// The screen (MinionSelectScreen): its Awake makes children with Start only, with Update
    /// and with LateUpdate, and a grandchild with Start only (the CharacterContainers lie deeper
    /// than the screen's direct children).
    public class PendingScreen : PendingPart
    {
        void Awake()
        {
            Tag = "screen";
            Child<PendingPart>("N", transform);
            Child<PendingPartU>("U", transform);
            Child<PendingPartL>("L", transform);
            Child<PendingPart>("deepN", Child<PendingPart>("mid", transform).transform);
        }
        static T Child<T>(string name, Transform parent) where T : PendingPart
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<T>();
            c.Tag = name;
            return c;
        }
    }

    /// Game: its Start destroys the screen.
    public class PendingGame : MonoBehaviour
    {
        public GameObject Target;
        bool once;
        void Start() { PendingPart.Log.Add("game:Start"); Object.Destroy(Target); }
        void Update() { if (!once) PendingPart.Log.Add("game:Update"); once = true; }
    }

    /// SaveLoader: a Start that makes the game, then the screen.
    public class PendingLoader : MonoBehaviour
    {
        void Start()
        {
            PendingPart.Log.Add("loader:Start");
            var game = new GameObject("game").AddComponent<PendingGame>();
            game.Target = new GameObject("screen", typeof(PendingScreen));
        }
    }

    /// In its first Update: a witness, Destroy of a screen made frames earlier, a second
    /// witness; or a screen made and destroyed at once.
    public class PendingDestroyer : MonoBehaviour
    {
        public GameObject Old;
        bool done;
        void Update()
        {
            if (done) return;
            done = true;
            if (Old == null)
            {
                Object.Destroy(new GameObject("screen", typeof(PendingScreen)));
                PendingPart.Log.Add("made and destroyed");
                return;
            }
            var deep = Old.transform.Find("mid/deepN").GetComponent<PendingPart>();
            new GameObject("w1").AddComponent<PendingWitness>().Watched = deep;
            Object.Destroy(Old);
            new GameObject("w2").AddComponent<PendingWitness>().Watched = deep;
            PendingPart.Log.Add("destroyed");
        }
    }

    public class PendingWitness : MonoBehaviour
    {
        public PendingPart Watched;
        void Start() => PendingPart.Log.Add("witness:Start " + (Watched == null ? "gone" : "there"));
    }

    public class PendingStartDestroyTests : EngineTest
    {
        public PendingStartDestroyTests() => PendingPart.Log.Clear();

        /// No fixed step in the frames of a test, so that the pass after FixedUpdate does not run.
        static void WithoutFixedSteps(System.Action body)
        {
            float step = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 1e6f;
            try { body(); }
            finally { Time.fixedDeltaTime = step; }
        }

        static readonly string[] Destruction =
        [
            "deepN:OnDisable started=True",
            "screen:OnDestroy started=False", "N:OnDestroy started=False", "U:OnDestroy started=False",
            "L:OnDestroy started=False", "mid:OnDestroy started=False", "deepN:OnDestroy started=True",
        ];

        static readonly string[] DisabledByDestroy =
        [
            "screen:OnDisable started=False", "N:OnDisable started=False", "U:OnDisable started=False",
            "L:OnDisable started=False", "mid:OnDisable started=False",
        ];

        /// Measured in the original (UnityProbe scenario 20, loadnf-): a Start in the frame's
        /// first pass makes Game and the screen; Game starts just before its first Update and
        /// destroys the screen. The pass of pending Starts after Update runs before the
        /// destruction, so the descendant Destroy left enabled starts first; the screen and its
        /// direct children, disabled by Destroy, never start but get OnDestroy. The game's
        /// CharacterContainers register in their Start and unregister in OnDestroy.
        [Fact]
        public void PendingStartsAfterUpdateRunBeforeItsDestruction()
        {
            WithoutFixedSteps(() =>
            {
                new GameObject("loader").AddComponent<PendingLoader>();
                Frame();
                Assert.Equal(new[] { "loader:Start", "game:Start" }, PendingPart.Log.GetRange(0, 2));
                Assert.Equal([.. new[] { "loader:Start", "game:Start" }, .. DisabledByDestroy, "game:Update", "deepN:Start", .. Destruction], PendingPart.Log);
            });
        }

        /// Measured in the original (scenario 20, upd-): made and destroyed in one Update.
        [Fact]
        public void AnObjectMadeAndDestroyedInUpdateStartsWhatDestroyLeftEnabled()
        {
            WithoutFixedSteps(() =>
            {
                new GameObject("destroyer").AddComponent<PendingDestroyer>();
                Frame();
                Assert.Equal([.. DisabledByDestroy, "made and destroyed", "deepN:Start", .. Destruction], PendingPart.Log);
            });
        }

        /// Measured in the original (scenario 20, old-): the Starts of objects made in Update
        /// still find what that Update destroyed.
        [Fact]
        public void StartsAfterUpdateStillFindWhatUpdateDestroyed()
        {
            WithoutFixedSteps(() =>
            {
                var old = new GameObject("screen", typeof(PendingScreen));
                new GameObject("destroyer").AddComponent<PendingDestroyer>().Old = old;
                Frame();
                int at = PendingPart.Log.IndexOf("destroyed");
                Assert.Equal(new[] { "witness:Start there", "witness:Start there", "deepN:OnDisable started=True", "screen:OnDestroy started=True" },
                    PendingPart.Log.GetRange(at + 1, 4));
                Assert.True(old == null);
            });
        }
    }
}
