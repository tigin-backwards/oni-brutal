namespace UnityEngine
{
    /// Sample data of a sound. The game plays all audio through FMOD and ships no AudioClips; the
    /// clip properties exist for the playables API.
    public partial class AudioClip
    {
        internal OniBrutal.Engine.AudioClipNative Clip => (OniBrutal.Engine.AudioClipNative)NativeOrThrow;

        public int samples => Clip.Samples;
        public int channels => Clip.Channels;
        public int frequency => Clip.Frequency;
        public float length => Clip.Frequency == 0 ? 0f : (float)Clip.Samples / Clip.Frequency;
    }
}

namespace OniBrutal.Engine
{
    internal sealed class AudioClipNative : NativeObject
    {
        public int Samples;
        public int Channels;
        public int Frequency;
    }
}
