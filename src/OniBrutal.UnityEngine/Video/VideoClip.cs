using OniBrutal.Engine.Video;

namespace UnityEngine.Video
{
    /// A movie asset. Clips exist only as loaded assets (Unity's constructor is private).
    public partial class VideoClip
    {
        private VideoClip() { }

        VideoClipNative Data => (VideoClipNative)NativeOrThrow;

        /// The path of the source file in the project, as imported.
        public string originalPath => Data.OriginalPath;

        public ulong frameCount => Data.FrameCount;

        public double frameRate => Data.FrameRate;

        /// Duration in seconds: frameCount / frameRate.
        public double length => Data.Length;

        public uint width => Data.Width;

        public uint height => Data.Height;

        public uint pixelAspectRatioNumerator => Data.PixelAspectRatioNumerator;

        public uint pixelAspectRatioDenominator => Data.PixelAspectRatioDenominator;

        public bool sRGB => Data.Srgb;

        public ushort audioTrackCount => (ushort)Data.AudioChannelCounts.Length;
    }
}
