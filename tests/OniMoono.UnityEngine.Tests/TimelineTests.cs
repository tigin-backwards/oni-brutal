using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Xunit;

namespace OniMoono.Tests
{
    public class TimelineTests : EngineTest
    {
        [TrackColor(0.1f, 0.2f, 0.3f)]
        [TrackClipType(typeof(TestClip))]
        [TrackBindingType(typeof(GameObject))]
        sealed class TestTrack : TrackAsset
        {
            internal void Add(TimelineClip clip) => m_Clips.Add(clip);
        }

        sealed class TestClip : PlayableAsset
        {
            public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => Playable.Create(graph);
        }

        // Clips have no public constructor (they come from the timeline's data).
        static TimelineClip NewClip() => (TimelineClip)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TimelineClip));

        [Fact]
        public void ATrackHoldsItsClipsInOrder()
        {
            Assert.Empty(ScriptableObject.CreateInstance<TestTrack>().GetClips());
            var track = ScriptableObject.CreateInstance<TestTrack>();
            var asset = ScriptableObject.CreateInstance<TestClip>();
            var first = NewClip();
            first.start = 1.5;
            first.duration = 2.0;
            first.asset = asset;
            var second = NewClip();
            second.start = 0.5;
            second.duration = 0.25;
            track.Add(first);
            track.Add(second);
            var clips = track.GetClips().ToList();
            Assert.Equal(new[] { first, second }, clips);
            Assert.Equal(3.5, clips[0].end);
            Assert.Same(asset, clips[0].asset);
            Assert.Equal(0.75, clips[1].end);
            Assert.Equal(0.5, track.start);
            Assert.Equal(3.5, track.end);
        }

        [Fact]
        public void TheAttributesKeepTheirArguments()
        {
            var color = (TrackColorAttribute)Attribute.GetCustomAttribute(typeof(TestTrack), typeof(TrackColorAttribute));
            Assert.Equal(new Color(0.1f, 0.2f, 0.3f, 1f), color.color);
            var clip = (TrackClipTypeAttribute)Attribute.GetCustomAttribute(typeof(TestTrack), typeof(TrackClipTypeAttribute));
            Assert.Equal(typeof(TestClip), clip.inspectedType);
            var binding = (TrackBindingTypeAttribute)Attribute.GetCustomAttribute(typeof(TestTrack), typeof(TrackBindingTypeAttribute));
            Assert.Equal(typeof(GameObject), binding.type);
        }

        [Fact]
        public void TheDefaultMixerHasAnInputPerClip()
        {
            var track = ScriptableObject.CreateInstance<TestTrack>();
            var graph = PlayableGraph.Create();
            try
            {
                var mixer = track.CreateTrackMixer(graph, null, 3);
                Assert.True(mixer.IsValid());
                Assert.Equal(3, mixer.GetInputCount());
            }
            finally { graph.Destroy(); }
        }
    }
}
