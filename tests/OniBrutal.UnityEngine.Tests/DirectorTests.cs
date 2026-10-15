using System.Collections.Generic;
using OniBrutal.Engine.Playables;
using UnityEngine;
using UnityEngine.Playables;
using Xunit;
using Object = UnityEngine.Object;

namespace OniBrutal.Tests
{
    public class RecordingBehaviour : PlayableBehaviour
    {
        public readonly List<string> Log = new();
        public readonly List<double> ProcessedTimes = new();
        public object LastPlayerData;

        public override void OnGraphStart(Playable playable) => Log.Add("graphStart");
        public override void OnGraphStop(Playable playable) => Log.Add("graphStop");
        public override void OnBehaviourPlay(Playable playable, FrameData info) => Log.Add("play");
        public override void OnBehaviourPause(Playable playable, FrameData info) => Log.Add("pause");
        public override void OnPlayableDestroy(Playable playable) => Log.Add("destroy");

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            ProcessedTimes.Add(playable.GetTime());
            LastPlayerData = playerData;
        }
    }

    /// A one-second asset whose root is a recording script playable driving one script output
    /// (bound by the asset itself as its key).
    public class RecordingAsset : PlayableAsset
    {
        public RecordingBehaviour Last;

        public override double duration => 1.0;

        public override IEnumerable<PlayableBinding> outputs => new[] { ScriptPlayableBinding.Create("Recording", this, typeof(Object)) };

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<RecordingBehaviour>.Create(graph);
            Last = playable.GetBehaviour();
            var output = ScriptPlayableOutput.Create(graph, "Recording");
            output.SetReferenceObject(this);
            output.SetSourcePlayable(playable);
            return playable;
        }
    }

    public class DirectorTests : EngineTest
    {
        static (PlayableDirector director, RecordingAsset asset, List<string> events) Setup(DirectorWrapMode mode)
        {
            var go = new GameObject("Director");
            go.SetActive(false);
            var director = go.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            go.SetActive(true);
            var asset = ScriptableObject.CreateInstance<RecordingAsset>();
            director.playableAsset = asset;
            director.extrapolationMode = mode;
            var events = new List<string>();
            director.played += d => events.Add("played");
            director.paused += d => events.Add("paused");
            director.stopped += d => events.Add("stopped");
            return (director, asset, events);
        }

        [Fact]
        public void PlayAdvancesTimePauseHoldsItStopResets()
        {
            var (director, asset, events) = Setup(DirectorWrapMode.Hold);
            Assert.Equal(PlayState.Paused, director.state);
            Assert.False(director.playableGraph.IsValid());
            Assert.Equal(1.0, director.duration);

            director.Play();
            Assert.Equal(PlayState.Playing, director.state);
            Assert.True(director.playableGraph.IsValid());
            Assert.Equal(new[] { "played" }, events);
            Assert.Same(director, director.playableGraph.GetResolver());

            // The first update evaluates the start time; later ones advance by deltaTime.
            Frame(0.1f);
            Assert.Equal(0.0, director.time, 5);
            Frame(0.1f);
            Frame(0.1f);
            Assert.Equal(0.2, director.time, 5);
            var behaviour = asset.Last;
            Assert.Equal(new[] { "graphStart", "play" }, behaviour.Log);
            Assert.Equal(3, behaviour.ProcessedTimes.Count);

            director.Pause();
            Assert.Equal(PlayState.Paused, director.state);
            Assert.Equal(new[] { "played", "paused" }, events);
            Assert.Equal(new[] { "graphStart", "play", "pause", "graphStop" }, behaviour.Log);
            Frame(0.1f);
            Assert.Equal(0.2, director.time, 5);
            Assert.Equal(3, behaviour.ProcessedTimes.Count);

            director.Play();
            Frame(0.1f);
            Assert.Equal(0.3, director.time, 5);

            director.Stop();
            Assert.Equal(new[] { "played", "paused", "played", "stopped" }, events);
            Assert.False(director.playableGraph.IsValid());
            Assert.Contains("destroy", behaviour.Log);
            Assert.Equal(0.0, director.time);
            Object.DestroyImmediate(director.gameObject);
        }

        [Fact]
        public void WrapModesApplyAtTheDuration()
        {
            var (hold, holdAsset, _) = Setup(DirectorWrapMode.Hold);
            hold.Play();
            for (int i = 0; i < 7; i++) Frame(0.25f);
            Assert.Equal(1.0, hold.time, 5);
            Assert.Equal(PlayState.Playing, hold.state);
            Assert.Equal(1.0, holdAsset.Last.ProcessedTimes[^1], 5);

            var (loop, _, _) = Setup(DirectorWrapMode.Loop);
            loop.time = 0.5;
            loop.Play();
            Frame(0.25f);
            Frame(0.25f);
            Frame(0.3125f);
            Assert.Equal(0.0625, loop.time, 5);
            Assert.Equal(PlayState.Playing, loop.state);

            var (none, _, events) = Setup(DirectorWrapMode.None);
            none.initialTime = 0.25;
            none.Play();
            Assert.Equal(0.25, none.time);
            for (int i = 0; i < 4; i++) Frame(0.3f);
            Assert.Equal(new[] { "played", "stopped" }, events);
            Assert.Equal(PlayState.Paused, none.state);
            Assert.False(none.playableGraph.IsValid());
            Assert.Equal(0.25, none.time);

            Object.DestroyImmediate(hold.gameObject);
            Object.DestroyImmediate(loop.gameObject);
            Object.DestroyImmediate(none.gameObject);
        }

        [Fact]
        public void EvaluateBuildsTheGraphAndBindingsReachOutputs()
        {
            var (director, asset, events) = Setup(DirectorWrapMode.Hold);
            var bound = new GameObject("Bound");
            director.SetGenericBinding(asset, bound);
            Assert.Same(bound, director.GetGenericBinding(asset));
            Assert.True(director.HasGenericBinding(asset));

            director.time = 0.4;
            director.Evaluate();
            Assert.True(director.playableGraph.IsValid());
            Assert.Equal(PlayState.Paused, director.state);
            Assert.Empty(events);
            Assert.Equal(new[] { 0.4 }, asset.Last.ProcessedTimes);
            Assert.Same(bound, asset.Last.LastPlayerData);

            // Deferred evaluation happens in the next director update.
            director.time = 0.6;
            director.DeferredEvaluate();
            Assert.Single(asset.Last.ProcessedTimes);
            Frame(0.1f);
            Assert.Equal(new[] { 0.4, 0.6 }, asset.Last.ProcessedTimes);

            director.ClearGenericBinding(asset);
            Assert.Null(director.GetGenericBinding(asset));

            // Disabling the component stops the director.
            director.Play();
            director.enabled = false;
            Assert.Equal(new[] { "played", "stopped" }, events);
            Assert.False(director.playableGraph.IsValid());

            Object.DestroyImmediate(director.gameObject);
            Object.DestroyImmediate(bound);
        }

        [Fact]
        public void PlayOnFrameRateAdvancesInWholeFrames()
        {
            var (director, _, _) = Setup(DirectorWrapMode.Hold);
            director.Play(new FrameRate(10));
            Assert.True(director.playableGraph.IsMatchFrameRateEnabled());
            Frame(0.05f);
            Assert.Equal(0.0, director.time, 9);
            Frame(0.02f);
            Assert.Equal(0.0, director.time, 9);
            Frame(0.05f);
            Assert.Equal(0.1, director.time, 9);
            Frame(0.1f);
            Assert.Equal(0.2, director.time, 9);
            Object.DestroyImmediate(director.gameObject);
        }
    }
}
