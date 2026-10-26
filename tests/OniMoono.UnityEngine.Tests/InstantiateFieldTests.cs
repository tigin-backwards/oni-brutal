using System;
using System.Collections.Generic;
using UnityEngine;
using Xunit;
using Object = UnityEngine.Object;

namespace OniMoono.Tests
{
    // Unity's serializer has no null for fields of [Serializable] classes: a clone gets a new, empty
    // instance (Unity's script serialization rules). The game's warp conduit receiver prefab carries
    // a ConduitSecondaryOutput whose port info was never set; its clones need one.
    //
    // Nor has it a null string: a clone reads "" (issue #17: WannaPrint's side screen leaves its
    // titleKey unset, and DetailsScreen titles the side screen with Strings.Get(titleKey)).
    public class InstantiateFieldTests : EngineTest
    {
        public enum PortKind { None, Liquid }

        [Serializable]
        public struct Offset
        {
            public int x, y;
        }

        [Serializable]
        public class PortInfo
        {
            public PortKind kind;
            public Offset offset;
            public PortInfo(PortKind kind, Offset offset) { this.kind = kind; this.offset = offset; }
        }

        public class SecondaryOutput : MonoBehaviour
        {
            [SerializeField]
            public PortInfo portInfo;
        }

        [Serializable]
        public class Outer
        {
            public PortInfo inner;
        }

        public class Holder : MonoBehaviour
        {
            public Outer outer;
        }

        // Measured in the original player (PerfProbe measure.txt): after Instantiate, the source's
        // null ConduitSecondaryOutput.portInfo is an instance of its own (conduit type None), and
        // the clone has another. The game then reads the prefab's ports (BuildingCellVisualizer).
        [Fact]
        public void InstantiateGivesTheSourcesNullSerializableClassFieldsAnInstanceOfTheirOwn()
        {
            var prefab = new GameObject("Prefab");
            var output = prefab.AddComponent<SecondaryOutput>();
            var holder = prefab.AddComponent<Holder>();
            Assert.Null(output.portInfo);
            var clone = Object.Instantiate(prefab);
            Assert.NotNull(output.portInfo);
            Assert.Equal(PortKind.None, output.portInfo.kind);
            Assert.NotSame(output.portInfo, clone.GetComponent<SecondaryOutput>().portInfo);
            Assert.NotNull(holder.outer);
            Assert.NotNull(holder.outer.inner);
            Assert.NotNull(clone.GetComponent<Holder>().outer.inner);
        }

        [Serializable]
        public class Named
        {
            public string name;
        }

        public class Titled : MonoBehaviour
        {
            [SerializeField]
            protected string titleKey;
            public string[] keys;
            public List<string> names;
            public Named named;
            public string TitleKey => titleKey;
        }

        // Measured in the original player (issue #17): WannaPrint's side screen, made with
        // AddComponent, keeps a null titleKey after it is instantiated; the clone's is "". Strings
        // in arrays, lists and serializable classes go through the same string serialization.
        [Fact]
        public void ANullStringIsEmptyInTheCloneAndStaysNullInTheSource()
        {
            var prefab = new GameObject("Prefab");
            var titled = prefab.AddComponent<Titled>();
            titled.keys = ["a", null];
            titled.names = [null, "b"];
            titled.named = new Named();
            Assert.Null(titled.TitleKey);
            var clone = Object.Instantiate(prefab).GetComponent<Titled>();
            Assert.Null(titled.TitleKey);
            Assert.Null(titled.keys[1]);
            Assert.Null(titled.named.name);
            Assert.Equal("", clone.TitleKey);
            Assert.Equal(["a", ""], clone.keys);
            Assert.Equal(["", "b"], clone.names);
            Assert.Equal("", clone.named.name);
        }

        [Fact]
        public void ANullSerializableClassFieldIsAnEmptyInstanceInTheClone()
        {
            var prefab = new GameObject("Prefab");
            prefab.AddComponent<SecondaryOutput>().portInfo = new PortInfo(PortKind.Liquid, new Offset { x = 1, y = 2 });
            prefab.AddComponent<SecondaryOutput>();
            var clone = Object.Instantiate(prefab);
            var outputs = clone.GetComponents<SecondaryOutput>();
            Assert.Equal(PortKind.Liquid, outputs[0].portInfo.kind);
            Assert.Equal(2, outputs[0].portInfo.offset.y);
            Assert.NotNull(outputs[1].portInfo);
            Assert.Equal(PortKind.None, outputs[1].portInfo.kind);
        }
    }
}
