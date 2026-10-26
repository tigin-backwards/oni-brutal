using System;
using System.Collections.Generic;
using OniMoono.Engine;
using Xunit;

namespace OniMoono.Tests
{
    /// The splash bar's value (StartupProgress): where it heads between checkpoints, and how the
    /// bar shown chases that.
    public class StartupProgressTests
    {
        static readonly List<(string Name, double Ms)> expected = [("window", 1000), ("scene0", 5000), ("menu", 10000)];

        [Fact]
        public void APhaseEasesTowardsItsNextCheckpoint()
        {
            Assert.Equal(0, StartupProgress.PhasePart(0));
            // About where a linear bar would stop short of the checkpoint, at the expected time.
            Assert.InRange(StartupProgress.PhasePart(1), 0.75, 0.85);
            // Quick at first, slower later: the first half covers more than the second.
            double half = StartupProgress.PhasePart(0.5);
            Assert.True(half > StartupProgress.PhasePart(1) - half);
            // An overrun keeps creeping, but never reaches the next checkpoint.
            double last = 0;
            foreach (double u in new[] { 1.0, 1.5, 2, 3, 5 })
            {
                double part = StartupProgress.PhasePart(u);
                Assert.True(part > last);
                Assert.True(part < 0.98);
                last = part;
            }
        }

        [Fact]
        public void TheTargetFollowsTheLastCheckpointReached()
        {
            // window reached at 1.2 s; 1 s on, a quarter of the 4 s phase to scene0.
            var reached = new List<(string, double)> { ("window", 1200) };
            double target = StartupProgress.Target(expected, reached, 2200, -1);
            Assert.Equal((1000 + StartupProgress.PhasePart(0.25) * 4000) / 10000, target, 9);
            // Overdue: towards scene0, never past it by the phase alone.
            double overdue = StartupProgress.Target(expected, reached, 5600, -1);
            Assert.Equal((1000 + StartupProgress.PhasePart(1.1) * 4000) / 10000, overdue, 9);
            Assert.True(overdue < 0.5);
            // Long overdue (scene0 not coming): on with start-up's time.
            Assert.Equal(StartupProgress.Floor(3), StartupProgress.Target(expected, reached, 30000, -1), 9);
            // Reached early: scene0's own place, past where the last phase was heading.
            reached.Add(("scene0", 1500));
            Assert.Equal(0.5, StartupProgress.Target(expected, reached, 1500, -1), 9);
            // A checkpoint the last launch did not pass leaves the target where it was.
            reached.Add(("unknown", 1600));
            Assert.Equal(StartupProgress.Target(expected, reached.GetRange(0, 2), 2000, -1), StartupProgress.Target(expected, reached, 2000, -1), 9);
        }

        [Fact]
        public void TheFloorKeepsMovingBehindTheExpectedPace()
        {
            double last = 0;
            foreach (double t in new[] { 0.1, 0.5, 1, 2, 4, 8 })
            {
                double floor = StartupProgress.Floor(t);
                Assert.True(floor > last);
                Assert.True(floor < Math.Min(t, 0.99));
                last = floor;
            }
            Assert.Equal(0, StartupProgress.Floor(0));
        }

        [Fact]
        public void TheEnginesFractionCountsWhenFurtherOn()
        {
            var reached = new List<(string, double)> { ("window", 1000) };
            double byTime = StartupProgress.Target(expected, reached, 1200, -1);
            Assert.Equal(byTime, StartupProgress.Target(expected, reached, 1200, 0.05), 9);
            Assert.Equal((1000 + StartupProgress.PhasePart(0.6) * 4000) / 10000, StartupProgress.Target(expected, reached, 1200, 0.6), 9);
            Assert.True(StartupProgress.Target(expected, reached, 1200, 1) < 0.5);
        }

        [Fact]
        public void TheBarChasesItsTargetWhateverTheFrameRate()
        {
            // 0.5 s towards a target 0.2 on, in steps of a 30 and a 144 fps frame: the same.
            double Run(double fps)
            {
                double shown = 0.1;
                for (int i = 0; i < fps / 2; i++) shown = StartupProgress.Chase(shown, 0.3, 1 / fps, 0.25);
                return shown;
            }
            Assert.Equal(Run(30), Run(144), 6);
            // Two time constants: about 86% of the way, not at once.
            Assert.InRange(Run(60), 0.1 + 0.2 * 0.85, 0.1 + 0.2 * 0.88);
            Assert.True(StartupProgress.Chase(0.1, 0.3, 1 / 60.0, 0.25) < 0.12);
        }

        [Theory]
        [InlineData("[21:46:43.565] [1] [INFO] DistributionPlatform initialized.", 26)]
        [InlineData("DistributionPlatform initialized.", 0)]
        [InlineData("[engine] splash", 9)]
        [InlineData("[unclosed", 0)]
        public void GameLogLinesAreReadAfterKleisHeader(string line, int start) => Assert.Equal(start, StartupProgress.TextStart(line));

        [Fact]
        public void TheBarNeverMovesBack()
        {
            Assert.Equal(0.5, StartupProgress.Chase(0.5, 0.4, 0.1, 0.25));
            Assert.Equal(0.5, StartupProgress.Chase(0.5, 0.9, 0, 0.25));
            Assert.Equal(0.5, StartupProgress.Chase(0.5, 0.9, -1, 0.25));
        }
    }
}
