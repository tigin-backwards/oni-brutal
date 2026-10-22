using System.Collections.Generic;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class StartLogger : MonoBehaviour
    {
        public static readonly List<string> Log = new();
        public string Tag = "";
        void Start() => Log.Add(Tag);
    }

    public class StartOrderTests : EngineTest
    {
        /// Objects made or activated in one frame start in that order, measured in the original
        /// (tools/UnityProbe, scenario 12): created, cloned and activated alike.
        [Fact]
        public void StartsRunInCreationAndActivationOrder()
        {
            StartLogger.Log.Clear();
            for (int i = 0; i < 4; i++) new GameObject("S" + i).AddComponent<StartLogger>().Tag = "new" + i;
            var template = new GameObject("T");
            template.SetActive(false);
            template.AddComponent<StartLogger>();
            for (int i = 0; i < 4; i++)
            {
                var clone = Object.Instantiate(template);
                clone.GetComponent<StartLogger>().Tag = "clone" + i;
                clone.SetActive(true);
            }
            var inactive = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                inactive[i] = Object.Instantiate(template);
                inactive[i].GetComponent<StartLogger>().Tag = "activated" + i;
            }
            for (int i = 0; i < 4; i++) inactive[i].SetActive(true);
            Frame();
            Assert.Equal(new[] { "new0", "new1", "new2", "new3", "clone0", "clone1", "clone2", "clone3", "activated0", "activated1", "activated2", "activated3" }, StartLogger.Log);
        }
    }
}
