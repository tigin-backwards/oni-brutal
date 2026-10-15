using OniBrutal.Engine;
using UnityEngine;
using Xunit;


namespace OniBrutal.Tests
{
    /// Gives each test a fresh active scene and a running player loop.
    public abstract class EngineTest : System.IDisposable
    {
        static bool initialized;
        internal readonly SceneRecord Scene;

        protected EngineTest()
        {
            if (!initialized)
            {
                PlayerLoop.Initialize();
                Layers.Install(["Default", "TransparentFX", "Ignore Raycast", "", "Water", "UI"]);
                initialized = true;
            }
            Scene = Scenes.Create("Test", "Assets/Test.unity", 0);
            Scenes.SetActive(Scene);
        }

        protected static void Frame(float dt = 1f / 60f) => PlayerLoop.RunFrame(dt);

        public void Dispose()
        {
            foreach (var root in Scene.Roots.ToArray()) Object.DestroyImmediate((GameObject)root.Managed);
            Lifecycle.ProcessDestroyQueue();
            Scenes.Remove(Scene);
        }
    }
}
