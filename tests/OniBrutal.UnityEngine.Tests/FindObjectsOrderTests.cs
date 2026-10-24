using System.Linq;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class OrderMarker : MonoBehaviour { }

    public class OrderAsset : ScriptableObject { }

    /// Resources.FindObjectsOfTypeAll's order, as UnityProbe's findorder scenario measured it
    /// in the original player: ascending instance ids, so the newest run-time object first,
    /// whatever was destroyed in between. Duplicant Status Bar clones the first usable LocText
    /// it returns; taken in slot order, a reused slot or a load on the loading thread chose
    /// another label (and its pivot) from run to run.
    public class FindObjectsOrderTests : EngineTest
    {
        static OrderMarker Make(string name, bool active = true)
        {
            var go = new GameObject(name);
            go.SetActive(active);
            return go.AddComponent<OrderMarker>();
        }

        static OrderAsset MakeAsset(string name)
        {
            var asset = ScriptableObject.CreateInstance<OrderAsset>();
            asset.name = name;
            return asset;
        }

        static string Names<T>() where T : Object
            => string.Join(" ", Resources.FindObjectsOfTypeAll<T>().Select(o => o.name));

        [Fact]
        public void NewestComponentComesFirst()
        {
            var a = Make("A");
            var b = Make("B");
            Make("C", active: false);
            var d = Make("D");
            Make("E");
            Assert.Equal("E D C B A", Names<OrderMarker>());
            Object.DestroyImmediate(b.gameObject);
            Make("F");
            Assert.Equal("F E D C A", Names<OrderMarker>());
            Object.DestroyImmediate(d.gameObject);
            Object.DestroyImmediate(a.gameObject);
            Make("G");
            Make("H");
            Assert.Equal("H G F E C", Names<OrderMarker>());
        }

        [Fact]
        public void NewestScriptableObjectComesFirst()
        {
            MakeAsset("SA");
            var sb = MakeAsset("SB");
            MakeAsset("SC");
            Object.DestroyImmediate(sb);
            MakeAsset("SD");
            Assert.Equal("SD SC SA", Names<OrderAsset>());
        }

        [Fact]
        public void IdsAscend()
        {
            for (int i = 0; i < 20; i++) Make("M" + i);
            foreach (var m in Resources.FindObjectsOfTypeAll<OrderMarker>().Where((_, i) => i % 3 == 0).ToArray())
                Object.DestroyImmediate(m.gameObject);
            for (int i = 0; i < 10; i++) Make("N" + i);
            var ids = Resources.FindObjectsOfTypeAll(typeof(OrderMarker)).Select(o => o.GetInstanceID()).ToArray();
            Assert.Equal(ids.OrderBy(i => i), ids);
        }
    }
}
