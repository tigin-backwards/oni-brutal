using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    /// A component or ScriptableObject class without a parameterless constructor is created with
    /// no constructor run at all: its field initializers and its base classes' constructors leave
    /// their fields at zero, its messages run, and nothing is logged. Measured in the original
    /// player (UnityProbe's missingtypes, 2026-10-26); Trash Cans' storage components have only a
    /// constructor taking a state, and the game quit loading their buildings.
    public class NoParameterlessConstructorTests : EngineTest
    {
        public static readonly List<string> Calls = new();

        public class NoCtorMb : MonoBehaviour
        {
            public int Init = 7;
            public string Text = "init";
            [System.NonSerialized] public int Hidden = 9;
            public NoCtorMb(int value) { Init = value; Calls.Add("ctor"); }
            void Awake() => Calls.Add("Awake " + Init + " " + (Text ?? "null"));
            void OnEnable() => Calls.Add("OnEnable");
        }

        public class InitBase : MonoBehaviour
        {
            public int BaseInit = 3;
            public InitBase() { Calls.Add("base ctor"); }
        }

        public class NoCtorDerived : InitBase
        {
            public int Own = 4;
            public NoCtorDerived(int value) { Own = value; }
        }

        public class NoCtorSo : ScriptableObject
        {
            public int Init = 7;
            public NoCtorSo(int value) { Init = value; }
            void Awake() => Calls.Add("so Awake " + Init);
        }

        [Fact]
        public void AddComponentRunsNoConstructor()
        {
            Calls.Clear();
            var go = new GameObject("g");
            var c = go.AddComponent<NoCtorMb>();
            Assert.True(c != null);
            Assert.Same(c, go.GetComponent<NoCtorMb>());
            Assert.Same(go, c.gameObject);
            Assert.Equal((0, null, 0), (c.Init, c.Text, c.Hidden));
            Assert.Equal(new[] { "Awake 0 null", "OnEnable" }, Calls);
            Assert.True(go.AddComponent(typeof(NoCtorMb)) != null);
            Assert.Equal(3, go.GetComponents<Component>().Length);
        }

        [Fact]
        public void NorItsBaseClassConstructor()
        {
            Calls.Clear();
            var c = new GameObject("g").AddComponent<NoCtorDerived>();
            Assert.Equal((0, 0), (c.BaseInit, c.Own));
            Assert.DoesNotContain("base ctor", Calls);
        }

        [Fact]
        public void InstantiateCopiesSerializedFields()
        {
            var go = new GameObject("g");
            var c = go.AddComponent<NoCtorMb>();
            c.Init = 5;
            c.Text = "set";
            c.Hidden = 6;
            var copy = Object.Instantiate(go).GetComponent<NoCtorMb>();
            Assert.Equal((5, "set", 0), (copy.Init, copy.Text, copy.Hidden));
        }

        [Fact]
        public void CreateInstanceRunsNoConstructor()
        {
            Calls.Clear();
            var so = ScriptableObject.CreateInstance<NoCtorSo>();
            Assert.True(so != null);
            Assert.Equal(0, so.Init);
            Assert.Equal(new[] { "so Awake 0" }, Calls);
            Assert.True(ScriptableObject.CreateInstance(typeof(NoCtorSo)) != null);
        }
    }
}
