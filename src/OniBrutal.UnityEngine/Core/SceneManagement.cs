using System;
using System.Collections.Generic;
using OniBrutal.Engine;

namespace UnityEngine.SceneManagement
{
    public partial struct Scene
    {
        // Unity's field: the scene handle.
        private SceneHandle m_Handle;

        internal Scene(SceneRecord record) => m_Handle = new SceneHandle(record?.Handle ?? 0);

        internal SceneRecord Record
        {
            get
            {
                int h = m_Handle.m_Value;
                if (h == Scenes.DontDestroyOnLoadScene.Handle) return Scenes.DontDestroyOnLoadScene;
                foreach (var s in Scenes.Loaded) if (s.Handle == h) return s;
                return null;
            }
        }

        public int handle => m_Handle.m_Value;
        public bool IsValid() => Record != null;
        public string name => Record?.Name;
        public string path => Record?.Path;
        public int buildIndex => Record?.BuildIndex ?? -1;
        public bool isLoaded => Record?.IsLoaded ?? false;
        public int rootCount => Record?.Roots.Count ?? 0;

        public GameObject[] GetRootGameObjects()
        {
            var list = new List<GameObject>();
            GetRootGameObjects(list);
            return list.ToArray();
        }

        public void GetRootGameObjects(List<GameObject> rootGameObjects)
        {
            var record = Record;
            if (record == null) throw new ArgumentException("The scene is invalid.");
            if (!record.IsLoaded) throw new ArgumentException("The scene is not loaded.");
            rootGameObjects.Clear();
            foreach (var go in record.Roots) rootGameObjects.Add((GameObject)go.Managed);
        }

        public static bool operator ==(Scene lhs, Scene rhs) => lhs.handle == rhs.handle;
        public static bool operator !=(Scene lhs, Scene rhs) => lhs.handle != rhs.handle;
        public override bool Equals(object other) => other is Scene s && s.handle == handle;
        public override int GetHashCode() => handle;
    }

    public partial class SceneManager
    {
        public static int sceneCount => Scenes.Loaded.Count;

        public static Scene GetActiveScene() => new(Scenes.Active);

        /// Single-mode load; completes at the start of the next frame.
        public static void LoadScene(string sceneName)
        {
            Threads.AssertMainThread("LoadScene");
            int index = SceneLoader.BuildIndexOf(sceneName);
            if (index < 0)
            {
                Debug.LogError($"Scene '{sceneName}' couldn't be loaded because it has not been added to the build settings or the AssetBundle has not been loaded.");
                return;
            }
            SceneLoader.Request(index);
        }

        public static void LoadScene(int sceneBuildIndex)
        {
            Threads.AssertMainThread("LoadScene");
            SceneLoader.Request(sceneBuildIndex);
        }
    }
}
