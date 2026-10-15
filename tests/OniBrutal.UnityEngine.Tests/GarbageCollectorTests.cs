using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using Xunit;

namespace OniBrutal.Tests
{
    public class GarbageCollectorTests
    {
        [Fact]
        public void GarbageCollectorModes()
        {
            var seen = new List<GarbageCollector.Mode>();
            Action<GarbageCollector.Mode> onChange = seen.Add;
            GarbageCollector.GCModeChanged += onChange;
            try
            {
                GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
                Assert.Equal(GarbageCollector.Mode.Disabled, GarbageCollector.GCMode);
                Assert.Equal(System.Runtime.GCLatencyMode.NoGCRegion, System.Runtime.GCSettings.LatencyMode);
                GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
                GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                Assert.NotEqual(System.Runtime.GCLatencyMode.NoGCRegion, System.Runtime.GCSettings.LatencyMode);
                Assert.Equal([GarbageCollector.Mode.Disabled, GarbageCollector.Mode.Enabled], seen);
                Assert.False(Unity.Jobs.LowLevel.Unsafe.JobsUtility.IsExecutingJob);
            }
            finally
            {
                GarbageCollector.GCModeChanged -= onChange;
                GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
            }
        }
    }
}
