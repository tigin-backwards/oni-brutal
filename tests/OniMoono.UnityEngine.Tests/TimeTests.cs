using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    // Unity keeps its clocks in double precision and rounds them to float only when read, so a long
    // session's time does not drift from the sum of its frame times.
    public class TimeTests
    {
        [Fact]
        public void TimeDoesNotDriftOverLongSessions()
        {
            float scale = Time.timeScale;
            Time.timeScale = 1f;
            try
            {
                double start = Time.timeAsDouble, unscaledStart = Time.unscaledTimeAsDouble;
                const int frames = 1_000_000; // 4.6 hours at 60 fps
                for (int i = 0; i < frames; i++) Time.BeginFrame(1f / 60f);
                double expected = frames * (double)(1f / 60f);
                Assert.InRange(Time.timeAsDouble - start, expected - 0.001, expected + 0.001);
                Assert.InRange(Time.unscaledTimeAsDouble - unscaledStart, expected - 0.001, expected + 0.001);
                Assert.Equal((float)Time.timeAsDouble, Time.time);
            }
            finally
            {
                Time.timeScale = scale;
            }
        }

        [Fact]
        public void FixedTimeFollowsTimeInWholeSteps()
        {
            Time.BeginFrame(0.1f);
            int steps = 0;
            while (Time.BeginFixedStep())
            {
                Time.EndFixedStep();
                steps++;
            }
            Assert.True(Time.fixedTimeAsDouble <= Time.timeAsDouble);
            Assert.True(Time.fixedTimeAsDouble + Time.fixedDeltaTime > Time.timeAsDouble);
            Assert.True(steps >= 1);
        }
    }
}
