using System;
using System.Runtime.CompilerServices;
using OniBrutal.Engine.Rendering;
using Xunit;

namespace OniBrutal.Tests
{
    // Texel memory is unmanaged. Textures that UnloadUnusedAssets reclaims are collected without
    // being destroyed, so their memory must go with them.
    public class PixelMemoryTests
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void AllocateAndDrop(long size) => _ = new PixelMemory(size, 0);

        [Fact]
        public void CollectedMemoryIsFreed()
        {
            const long size = 64L << 20;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long before = PixelMemory.LiveBytes;
            AllocateAndDrop(size);
            Assert.True(PixelMemory.LiveBytes >= before + size);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.True(PixelMemory.LiveBytes < before + size);
        }

        [Fact]
        public void FreeingTwiceIsHarmless()
        {
            var m = new PixelMemory(1024, 7);
            long live = PixelMemory.LiveBytes;
            m.Free();
            m.Free();
            Assert.Equal(live - 1024, PixelMemory.LiveBytes);
        }
    }
}
