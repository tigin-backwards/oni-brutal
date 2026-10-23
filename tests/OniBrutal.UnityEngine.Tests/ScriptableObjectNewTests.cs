using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class NewedSo : ScriptableObject
    {
        public static string Order = "";
        public int Field = 3;
        public NewedSo() { Order += "ctor "; }
        void Awake() => Order += "Awake ";
        void OnEnable() => Order += "OnEnable ";
        void OnDisable() => Order += "OnDisable ";
        void OnDestroy() => Order += "OnDestroy ";
    }

    public class NewedPlainSo : ScriptableObject { }

    public class NewedBehaviour : MonoBehaviour
    {
        public static string Order = "";
        void Awake() => Order += "Awake ";
    }

    /// `new` on a ScriptableObject, as UnityProbe's newobject scenario measured it in the
    /// original player: a live object, a warning, Awake and OnEnable before the subclass
    /// constructor's body. Ronivan's Legacy builds a TextStyleSetting this way, keeps it in a
    /// static and builds it again while it reads null, from a card whose style is unset
    /// (issue #7).
    public class ScriptableObjectNewTests : EngineTest
    {
        static List<(string message, LogType type)> Logged(System.Action action)
        {
            var logged = new List<(string, LogType)>();
            Application.LogCallback on = (m, s, t) => logged.Add((m, t));
            Application.logMessageReceived += on;
            try { action(); }
            finally { Application.logMessageReceived -= on; }
            return logged;
        }

        [Fact]
        public void NewMakesALiveObject()
        {
            NewedSo.Order = "";
            NewedSo so = null;
            var logged = Logged(() => so = new NewedSo());
            Assert.Equal("Awake OnEnable ctor ", NewedSo.Order);
            Assert.False(so == null);
            Assert.True((bool)so);
            Assert.NotEqual(0, so.GetInstanceID());
            Assert.Equal("", so.name);
            Assert.Equal(HideFlags.None, so.hideFlags);
            Assert.Equal(" (OniBrutal.Tests.NewedSo)", so.ToString());
            Assert.Equal(3, so.Field);
            var warning = Assert.Single(logged);
            Assert.Equal(LogType.Warning, warning.type);
            Assert.Equal("OniBrutal.Tests.NewedSo must be instantiated using the ScriptableObject.CreateInstance method instead of new NewedSo.", warning.message);

            so.name = "Named";
            var copy = Object.Instantiate(so);
            Assert.Equal("Named(Clone)", copy.name);
            Assert.NotEqual(so.GetInstanceID(), copy.GetInstanceID());

            NewedSo.Order = "";
            Object.DestroyImmediate(so);
            Assert.Equal("OnDisable OnDestroy ", NewedSo.Order);
            Assert.True(so == null);
        }

        [Fact]
        public void NewOnABareScriptableObjectWarnsWithItsName()
        {
            ScriptableObject bare = null;
            var logged = Logged(() => bare = new ScriptableObject());
            Assert.False(bare == null);
            Assert.Equal("UnityEngine.ScriptableObject must be instantiated using the ScriptableObject.CreateInstance method instead of new ScriptableObject.", Assert.Single(logged).message);
        }

        [Fact]
        public void CreateInstanceAndInstantiateDoNotWarn()
        {
            NewedPlainSo made = null, copy = null;
            var logged = Logged(() =>
            {
                made = ScriptableObject.CreateInstance<NewedPlainSo>();
                copy = Object.Instantiate(made);
            });
            Assert.Empty(logged);
            Assert.False(made == null);
            Assert.False(copy == null);
        }

        [Fact]
        public void DestroyEndsANewedObjectAtTheEndOfTheFrame()
        {
            var so = new NewedSo();
            NewedSo.Order = "";
            Object.Destroy(so);
            Assert.False(so == null);
            Frame();
            Assert.Equal("OnDisable OnDestroy ", NewedSo.Order);
            Assert.True(so == null);
        }

        [Fact]
        public void NewOnAMonoBehaviourStaysDeadAndSilent()
        {
            NewedBehaviour.Order = "";
            NewedBehaviour mb = null;
            var logged = Logged(() => mb = new NewedBehaviour());
            Assert.Empty(logged);
            Assert.Equal("", NewedBehaviour.Order);
            Assert.False(mb is null);
            Assert.True(mb == null);
            Assert.Equal(0, mb.GetInstanceID());
        }
    }
}
