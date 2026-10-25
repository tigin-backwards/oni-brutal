using System;
using System.Linq;
using OniBrutal.GameTweaks;
using Xunit;

namespace OniBrutal.Tests
{
    /// The brain-rate game tweak's allowance (src/OniBrutal.GameTweaks/BrainRateAllowance.cs,
    /// compiled in here): brain updates a frame for a rate of real time, never more than the
    /// game's own count a frame, spread evenly.
    public class BrainRateAllowanceTests
    {
        /// The allowance of each of `frames` frames of `fps`.
        static int[] Run(double rate, int perFrame, double fps, int frames)
        {
            var allowance = new BrainRateAllowance(rate);
            return Enumerable.Range(0, frames).Select(_ => allowance.Take(1 / fps, perFrame)).ToArray();
        }

        [Theory]
        [InlineData(60)]
        [InlineData(79)]
        [InlineData(120)]
        [InlineData(240)]
        public void DuplicantsGetThirtyASecondAboveThirtyFps(double fps)
        {
            int frames = (int)(fps * 10);
            var taken = Run(30, 1, fps, frames);
            Assert.All(taken, n => Assert.InRange(n, 0, 1));
            // 10 s: 300 updates, give or take the first frame's.
            Assert.InRange(taken.Sum(), 299, 301);
        }

        [Fact]
        public void UpdatesAreSpreadEvenly()
        {
            // 120 fps at 30 a second: one update every fourth frame, never two frames running.
            var taken = Run(30, 1, 120, 1200);
            var at = Enumerable.Range(0, taken.Length).Where(i => taken[i] == 1).ToArray();
            for (int i = 1; i < at.Length; i++) Assert.Equal(4, at[i] - at[i - 1]);
            // Critters at 60 fps, 150 a second: 2 or 3 every frame, 5 every two frames.
            var critters = Run(150, 5, 60, 600);
            Assert.All(critters.Skip(1), n => Assert.InRange(n, 2, 3));
            for (int i = 1; i + 1 < critters.Length; i += 2) Assert.Equal(5, critters[i] + critters[i + 1]);
        }

        [Theory]
        [InlineData(30, 1)]
        [InlineData(20, 1)]
        [InlineData(5, 1)]
        [InlineData(30, 5)]
        [InlineData(11.4, 5)]
        public void AtThirtyFpsOrBelowEveryFrameGetsTheGamesCount(double fps, int perFrame)
        {
            var taken = Run(30 * perFrame, perFrame, fps, 3000);
            Assert.All(taken, n => Assert.Equal(perFrame, n));
        }

        [Fact]
        public void AfterASlowFrameNoFrameGetsMoreThanTheGamesCount()
        {
            var allowance = new BrainRateAllowance(150);
            for (int i = 0; i < 100; i++) allowance.Take(1 / 120.0, 5);
            // A 3 s autosave frame: the game's count, then at most the game's count a frame while
            // the second's worth carried over is spent, then the rate again.
            Assert.Equal(5, allowance.Take(3, 5));
            var after = Enumerable.Range(0, 240).Select(_ => allowance.Take(1 / 120.0, 5)).ToArray();
            Assert.All(after, n => Assert.InRange(n, 0, 5));
            Assert.InRange(after.Skip(120).Sum(), 149, 151);
        }

        [Fact]
        public void UnevenFramesUnderTheRateRunAsTheGame()
        {
            // Frames alternating 10 ms and 90 ms (20 a second, under 30): the game's one a frame.
            var allowance = new BrainRateAllowance(30);
            int sum = 0;
            for (int i = 0; i < 2000; i++) sum += allowance.Take(i % 2 == 0 ? 0.010 : 0.090, 1);
            Assert.InRange(sum, 1990, 2000);
        }

        [Fact]
        public void TheFirstFrameGetsTheGamesCount()
        {
            Assert.Equal(1, new BrainRateAllowance(30).Take(1 / 240.0, 1));
            Assert.Equal(5, new BrainRateAllowance(150).Take(0, 5));
        }

        [Fact]
        public void ACountOfZeroOrLessIsKept()
        {
            var allowance = new BrainRateAllowance(30);
            Assert.Equal(0, allowance.Take(1, 0));
            Assert.Equal(-1, allowance.Take(1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BrainRateAllowance(0));
        }

        [Fact]
        public void AFrameOfNoTimeEarnsNothing()
        {
            var allowance = new BrainRateAllowance(30);
            allowance.Take(1 / 120.0, 1);
            Assert.Equal(0, allowance.Take(0, 1));
            Assert.Equal(0, allowance.Take(-1, 1));
        }
    }
}
