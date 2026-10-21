using OniBrutal.Engine.Rendering;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    // The texel-copy check (ONI_VERIFY_TEXEL_COPIES) looks for copies that read texels while
    // they were being written. A write that first waits for the copy (every engine access does, as
    // the font atlas's glyph writes) is not one; reported as one, it made a QA run fail falsely.
    public class TexelCopyTests : EngineTest
    {
        [Fact]
        public void AWriteThatWaitsForTheCopyIsNotATornCopy()
        {
            TexelCopies.Verify = true;
            try
            {
                long before = TexelCopies.Torn;
                var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                var native = (Texture2DNative)texture.NativeOrThrow;
                var source = native.Memory;
                byte original = source.Span[0];
                var target = new PixelMemory(source.Size);
                TexelCopies.Start(native, source, target);
                native.Memory.Span[0] = (byte)(original + 1); // waits for the copy first
                TexelCopies.Fence();
                Assert.Equal(before, TexelCopies.Torn);
                Assert.Equal(original, target.Span[0]);
            }
            finally
            {
                TexelCopies.Verify = false;
            }
        }
    }
}
