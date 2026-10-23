using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class InitStyle : ScriptableObject { }
    public class InitSubject : MonoBehaviour { }

    /// A class's field initializers run before its base constructor, so objects they construct
    /// reach UnityEngine.Object's constructor while the engine's record for the outer component
    /// is pending. Closest Material's widget holds `new ColorStyleSetting()` this way; the style
    /// took the widget's record and AddComponent threw "did not chain".
    public class FieldInitializerBindingTests : EngineTest
    {
        public class HoldsNewStyle : MonoBehaviour { public ScriptableObject style = new InitStyle(); }
        public class HoldsCreatedStyle : MonoBehaviour { public InitStyle style = ScriptableObject.CreateInstance<InitStyle>(); }
        public class HoldsBoth : MonoBehaviour
        {
            public ScriptableObject made = new InitStyle();
            public InitStyle created = ScriptableObject.CreateInstance<InitStyle>();
        }

        [Fact]
        public void AComponentWhoseInitializerNewsAScriptableObjectKeepsItsRecord()
        {
            var go = new GameObject("g");
            var c = go.AddComponent<HoldsNewStyle>();
            Assert.True(c != null);
            Assert.Same(c, go.GetComponent<HoldsNewStyle>());
            Assert.Same(go, c.gameObject);
        }

        [Fact]
        public void AComponentWhoseInitializerCreatesAScriptableObjectKeepsItsRecord()
        {
            var go = new GameObject("g");
            var c = go.AddComponent<HoldsCreatedStyle>();
            Assert.True(c != null);
            Assert.Same(go, c.gameObject);
            Assert.True(c.style != null);
            Assert.NotEqual(c.GetInstanceID(), c.style.GetInstanceID());
        }

        [Fact]
        public void BothKindsInOneComponent()
        {
            var go = new GameObject("g");
            var c = go.AddComponent<HoldsBoth>();
            Assert.Same(go, c.gameObject);
            Assert.True(c.created != null);
            Assert.NotSame((object)c.made, c.created);
        }
    }
}
