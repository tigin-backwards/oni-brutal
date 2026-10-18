using System;

namespace UnityEngine.Timeline
{
    // A clip of a track: a playable asset placed in time (seconds).
    public partial class TimelineClip
    {
        const double DefaultDuration = 5.0;

        public TimelineClip()
        {
            m_Duration = DefaultDuration;
            m_TimeScale = 1.0;
            m_PostExtrapolationMode = ClipExtrapolation.None;
            m_PreExtrapolationMode = ClipExtrapolation.None;
        }

        public double start
        {
            get => m_Start;
            set => m_Start = Math.Max(value, 0.0);
        }

        public double duration
        {
            get => m_Duration;
            set => m_Duration = Math.Max(value, double.Epsilon);
        }

        public double end => m_Start + m_Duration;

        public double clipIn
        {
            get => m_ClipIn;
            set => m_ClipIn = Math.Max(value, 0.0);
        }

        public double timeScale
        {
            get => m_TimeScale;
            set => m_TimeScale = Math.Max(value, 0.001);
        }

        public string displayName
        {
            get => m_DisplayName;
            set => m_DisplayName = value;
        }

        // The clip's playable asset.
        public Object asset
        {
            get => m_Asset;
            set => m_Asset = value;
        }

        public TrackAsset GetParentTrack() => m_ParentTrack;

        internal void SetParentTrack(TrackAsset track) => m_ParentTrack = track;

        // The current serialized version needs no upgrade.
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() { }
    }

    partial struct MarkerList
    {
        // Markers are timeline content, which the game does not ship; the serialized list
        // needs no processing.
        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize() { }
    }
}
