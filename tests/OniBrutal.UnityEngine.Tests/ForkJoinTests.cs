using System.Threading;
using OniBrutal.Desktop.Gl;
using Xunit;

namespace OniBrutal.Tests
{
    /// The draw recorder's fork-join: every item of a run runs once, all before For returns, and
    /// helpers waking late never run an item of a later run twice or of an earlier one at all.
    public class ForkJoinTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void EachItemRunsOnceBeforeForReturns(int helpers)
        {
            var forkJoin = new ForkJoin(helpers);
            for (int run = 0; run < 2000; run++)
            {
                int count = 1 + run % 37;
                var hits = new int[count];
                int stamp = run;
                forkJoin.For(count, i =>
                {
                    Interlocked.Increment(ref hits[i]);
                    // A run's items see their own run.
                    Assert.Equal(run, stamp);
                    if (i % 5 == 0) Thread.SpinWait(200);
                });
                foreach (int h in hits) Assert.Equal(1, h);
            }
        }
    }
}
