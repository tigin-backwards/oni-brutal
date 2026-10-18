using System;
using System.Collections.Generic;
using UnityEngine.Playables;

namespace UnityEngine.Timeline
{
    // A track of a timeline: a playable asset holding its clips in serialized order. The game
    // ships no Timeline content: tracks exist so that FMODUnity's track type loads and binds.
    public abstract partial class TrackAsset
    {
        protected TrackAsset()
        {
            m_Clips = new List<TimelineClip>();
        }

        // The clips, in serialized order.
        public IEnumerable<TimelineClip> GetClips() => m_Clips ??= new List<TimelineClip>();

        public bool hasClips => m_Clips != null && m_Clips.Count > 0;

        public bool isEmpty => !hasClips;

        public bool muted { get => m_Muted; set => m_Muted = value; }

        public bool locked { get => m_Locked; set => m_Locked = value; }

        public PlayableAsset parent => m_Parent;

        // The earliest clip start and the latest clip end.
        public double start
        {
            get
            {
                if (!hasClips) return 0.0;
                double s = double.MaxValue;
                foreach (var c in m_Clips) if (c != null) s = Math.Min(s, c.start);
                return s == double.MaxValue ? 0.0 : s;
            }
        }

        public double end
        {
            get
            {
                if (!hasClips) return 0.0;
                double e = 0.0;
                foreach (var c in m_Clips) if (c != null) e = Math.Max(e, c.end);
                return e;
            }
        }

        public override double duration => end - start;

        // As documented: the track's mixer is a playable with an input per clip.
        public virtual Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) => Playable.Create(graph, inputCount);

        // Tracks are played through their timeline, not as assets of their own.
        public override Playable CreatePlayable(PlayableGraph graph, GameObject go) => Playable.Null;

        public virtual void GatherProperties(PlayableDirector director, IPropertyCollector driver)
            => throw new NotSupportedException("Timeline playback is not implemented: the game ships no Timeline content.");

        // The clips know their track once loaded.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (m_Clips == null) return;
            foreach (var c in m_Clips) c?.SetParentTrack(this);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
    }
}
