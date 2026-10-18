using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Xunit;

namespace OniBrutal.Tests
{
    public class SceneRootsTests : EngineTest
    {
        static string[] RootNames() => SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name).ToArray();

        /// Roots stay in creation order through many removals (destroyed, parented away), which
        /// compact the list, and through additions and sibling moves.
        [Fact]
        public void RootsKeepTheirOrderThroughRemovalsAndMoves()
        {
            var all = Enumerable.Range(0, 1000).Select(i => new GameObject("r" + i)).ToArray();
            var holder = all[0];
            for (int i = 1; i < 1000; i++)
                if (i % 3 == 0) Object.Destroy(all[i]);
                else if (i % 3 == 1) all[i].transform.SetParent(holder.transform, false);
            Frame();
            var expected = Enumerable.Range(0, 1000).Where(i => i == 0 || i % 3 == 2).Select(i => "r" + i).ToList();
            Assert.Equal(expected, RootNames());
            Assert.Equal(expected.Count, SceneManager.GetActiveScene().rootCount);

            all[1].transform.SetParent(null, false);
            expected.Add("r1");
            Assert.Equal(expected, RootNames());

            all[5].transform.SetAsFirstSibling();
            expected.Remove("r5");
            expected.Insert(0, "r5");
            Assert.Equal(expected, RootNames());
        }

        /// FindObjectsByType in hierarchy order; an inactive object and everything under it count
        /// only when inactive objects are asked for.
        [Fact]
        public void FindObjectsByTypeSkipsInactiveSubtreesUnlessAsked()
        {
            var a = new GameObject("a", typeof(BoxCollider));
            var b = new GameObject("b", typeof(BoxCollider));
            var child = new GameObject("c", typeof(BoxCollider));
            child.transform.SetParent(b.transform, false);
            b.SetActive(false);
            var active = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            Assert.Equal(new[] { "a" }, active.Select(x => x.name));
            var all = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Equal(new[] { "a", "b", "c" }, all.Select(x => x.name));
        }
    }
}
