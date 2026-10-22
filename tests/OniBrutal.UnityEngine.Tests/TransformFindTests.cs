using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class TransformFindTests : EngineTest
    {
        /// Paths as the original answers them (tools/UnityProbe, "Transform.Find" values): an empty
        /// path is the transform itself (a mod's buttons are wired through Find("")), one trailing
        /// '/' is ignored, and any other empty name finds nothing, even with a child named "".
        [Fact]
        public void EmptyNamesInPathsAnswerAsInTheOriginal()
        {
            var root = new GameObject("H0");
            var h1 = new GameObject("H1"); h1.transform.SetParent(root.transform);
            var h2 = new GameObject("H2"); h2.transform.SetParent(root.transform);
            var h11 = new GameObject("H11"); h11.transform.SetParent(h1.transform);
            new GameObject("").transform.SetParent(h2.transform);

            Assert.Same(root.transform, root.transform.Find(""));
            Assert.Same(h1.transform, root.transform.Find("H1/"));
            Assert.Same(h11.transform, root.transform.Find("H1/H11/"));
            Assert.Same(h2.transform, root.transform.Find("H2/"));
            foreach (var path in new[] { "/", "/H1", "H1//H11", ".", "..", "H2//" })
                Assert.Null(root.transform.Find(path));
        }
    }
}
