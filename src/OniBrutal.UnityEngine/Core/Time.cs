using System.Diagnostics;

namespace UnityEngine
{
    /// Frame timing. The player loop advances it (OniBrutal.Engine.PlayerLoop); reads are
    /// lock-free from any thread.
    public partial class Time
    {
        static readonly Stopwatch clock = Stopwatch.StartNew();

        static float s_Time, s_UnscaledTime, s_FixedTime, s_FixedUnscaledTime;
        static float s_DeltaTime, s_UnscaledDeltaTime;
        static float s_TimeScale = 1f;
        static float s_FixedDeltaTime = 0.02f;
        static float s_MaximumDeltaTime = 1f / 3f;
        static float s_MaximumParticleDeltaTime = 0.03f;
        static float s_LevelLoadTime;
        static int s_FrameCount, s_RenderedFrameCount;
        static bool s_InFixedTimeStep;

        public static float time => s_InFixedTimeStep ? s_FixedTime : s_Time;
        public static double timeAsDouble => time;
        public static float unscaledTime => s_InFixedTimeStep ? s_FixedUnscaledTime : s_UnscaledTime;
        public static double unscaledTimeAsDouble => unscaledTime;
        public static float fixedTime => s_FixedTime;
        public static double fixedTimeAsDouble => s_FixedTime;
        public static float fixedUnscaledTime => s_FixedUnscaledTime;
        public static float deltaTime => s_InFixedTimeStep ? s_FixedDeltaTime : s_DeltaTime;
        public static float unscaledDeltaTime => s_InFixedTimeStep ? s_FixedDeltaTime : s_UnscaledDeltaTime;
        public static float fixedUnscaledDeltaTime => s_FixedDeltaTime;
        public static float timeSinceLevelLoad => time - s_LevelLoadTime;
        public static bool inFixedTimeStep => s_InFixedTimeStep;
        public static int frameCount => s_FrameCount;
        public static int renderedFrameCount => s_RenderedFrameCount;
        public static float realtimeSinceStartup => (float)clock.Elapsed.TotalSeconds;
        public static double realtimeSinceStartupAsDouble => clock.Elapsed.TotalSeconds;

        public static float timeScale
        {
            get => s_TimeScale;
            set => s_TimeScale = value;
        }

        public static float fixedDeltaTime
        {
            get => s_FixedDeltaTime;
            set => s_FixedDeltaTime = value < 0.0001f ? 0.0001f : value;
        }

        public static float maximumDeltaTime
        {
            get => s_MaximumDeltaTime;
            set => s_MaximumDeltaTime = value;
        }

        public static float maximumParticleDeltaTime
        {
            get => s_MaximumParticleDeltaTime;
            set => s_MaximumParticleDeltaTime = value;
        }

        // ---- Player loop interface ----

        /// Starts a new frame given the real time elapsed since the previous one.
        internal static void BeginFrame(float realDelta)
        {
            s_FrameCount++;
            s_UnscaledDeltaTime = realDelta;
            s_UnscaledTime += realDelta;
            float clamped = realDelta < s_MaximumDeltaTime ? realDelta : s_MaximumDeltaTime;
            s_DeltaTime = clamped * s_TimeScale;
            s_Time += s_DeltaTime;
            // Exponential smoothing of deltaTime; the first frame starts the average.
            s_SmoothDeltaTime = s_FrameCount <= 1 ? s_DeltaTime : s_SmoothDeltaTime + (s_DeltaTime - s_SmoothDeltaTime) * 0.2f;
        }

        static float s_SmoothDeltaTime;

        /// deltaTime smoothed over recent frames.
        public static float smoothDeltaTime => s_SmoothDeltaTime;

        /// Advances fixed time by one step; returns false when fixed time has caught up with time.
        internal static bool BeginFixedStep()
        {
            if (s_FixedTime + s_FixedDeltaTime > s_Time) return false;
            s_FixedTime += s_FixedDeltaTime;
            s_FixedUnscaledTime += s_FixedDeltaTime;
            s_InFixedTimeStep = true;
            return true;
        }

        internal static void EndFixedStep() => s_InFixedTimeStep = false;

        internal static void FrameRendered() => s_RenderedFrameCount++;

        internal static void LevelLoaded() => s_LevelLoadTime = s_Time;

        /// Settings from the project's TimeManager.
        internal static void Configure(float fixedTimestep, float maximumAllowedTimestep, float timeScaleSetting, float maximumParticleTimestep)
        {
            s_FixedDeltaTime = fixedTimestep;
            s_MaximumDeltaTime = maximumAllowedTimestep;
            s_TimeScale = timeScaleSetting;
            s_MaximumParticleDeltaTime = maximumParticleTimestep;
        }
    }
}
