using System.Collections.Generic;
using System.IO;
using System.Linq;
using OniMoono.Engine.Assets;
using OniMoono.Engine.Rendering;
using Xunit;

namespace OniMoono.Tests
{
    /// A texture's texels still to be read from an AssetBundle stay readable after the bundle is
    /// unmounted (AssetBundle.Unload(false)): the source holds the bundle's resource node, as an
    /// upload queued for the next frame or handed to background readers reads it later.
    public class TexelSourceTests
    {
        [Fact]
        public unsafe void UnreadTexelsOutliveTheirBundle()
        {
            const string key = "archive:/CAB-texelsourcetest/CAB-texelsourcetest.resS";
            var bytes = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
            AssetSystem.Mount(new List<(string, byte[])> { (key, bytes) });
            var source = new TexelSource(null, key, 16, 8, AssetSystem.MountedNode(key));
            AssetSystem.UnmountBundle([key]);
            Assert.Throws<DirectoryNotFoundException>(() => AssetSystem.ReadStream(key, 16, 8));
            Assert.Equal(bytes[16..24], source.Read());
            var memory = source.ReadMemory();
            Assert.Equal(bytes[16..24], memory.Span.ToArray());
            memory.Free();
        }
    }
}
