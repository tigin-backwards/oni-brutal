using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine.Assets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OniBrutal.Engine
{
    /// Loads build scenes (levelN files) with Unity's single-mode semantics.
    internal static class SceneLoader
    {
        static int pendingBuildIndex = -1;

        public static event Action<SceneRecord> SceneLoaded;

        static SceneLoader() => PlayerLoop.FrameStart += CompletePendingLoad;

        public static int BuildIndexOf(string nameOrPath)
        {
            var scenes = ProjectSettings.BuildScenes;
            for (int i = 0; i < scenes.Count; i++)
            {
                var path = scenes[i];
                if (string.Equals(path, nameOrPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileNameWithoutExtension(path), nameOrPath, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        /// SceneManager.LoadScene: the load completes at the start of the next frame.
        public static void Request(int buildIndex)
        {
            if (buildIndex < 0 || buildIndex >= ProjectSettings.BuildScenes.Count)
                throw new ArgumentException($"Scene with build index {buildIndex} couldn't be loaded because it has not been added to the build settings.");
            pendingBuildIndex = buildIndex;
        }

        static void CompletePendingLoad()
        {
            if (pendingBuildIndex < 0) return;
            int index = pendingBuildIndex;
            pendingBuildIndex = -1;
            LoadNow(index);
        }

        /// Replaces the loaded scenes with build scene `index` (the first scene uses this directly).
        public static SceneRecord LoadNow(int index)
        {
            foreach (var old in Scenes.Loaded.ToArray()) Unload(old);

            var path = ProjectSettings.BuildScenes[index];
            var scene = Scenes.Create(Path.GetFileNameWithoutExtension(path), path, index);
            Scenes.SetActive(scene);
            var file = AssetSystem.Get("level" + index);
            ObjectLoader.BeginSceneFile(file, scene);
            var gameObjects = new List<GameObjectNative>();
            try
            {
                foreach (var info in file.Objects)
                {
                    var obj = ObjectLoader.Load(file, info.PathId);
                    if (obj is GameObject go) gameObjects.Add((GameObjectNative)go.Native);
                }
            }
            finally
            {
                ObjectLoader.EndSceneFile(file);
            }

            foreach (var go in gameObjects)
            {
                go.Scene = scene;
                if (go.Transform.Parent == null) scene.Roots.Add(go);
            }
            Time.LevelLoaded();
            Activate(scene);
            EngineLog.Engine($"Loaded scene {scene.Name} ({gameObjects.Count} GameObjects)");
            SceneLoaded?.Invoke(scene);
            return scene;
        }

        /// Awake/OnEnable for a freshly loaded scene: active flags for every object first, then
        /// engine components, then scripts in script execution order (load order within an order).
        static void Activate(SceneRecord scene)
        {
            var active = new List<GameObjectNative>();
            void Walk(GameObjectNative go, bool parentActive)
            {
                go.ActiveInHierarchy = parentActive && go.ActiveSelf;
                if (go.ActiveInHierarchy) active.Add(go);
                foreach (var child in go.Transform.Children) Walk(child.GameObject, go.ActiveInHierarchy);
            }
            foreach (var root in scene.Roots) Walk(root, true);

            Lifecycle.AwakeAndEnableAll(active);
        }

        /// Player shutdown: every scene's objects are destroyed (OnDisable, OnDestroy), the
        /// DontDestroyOnLoad ones included.
        public static void UnloadAll()
        {
            foreach (var scene in Scenes.WithDontDestroyOnLoad().ToArray()) Unload(scene);
        }

        /// Destroys a scene's objects (DontDestroyOnLoad objects live in their own scene).
        static void Unload(SceneRecord scene)
        {
            foreach (var root in scene.Roots.ToArray())
                if (root.Managed != null && root.Managed.Native != null) Lifecycle.DestroyNow(root.Managed);
            Scenes.Remove(scene);
        }
    }

    /// Scene-level settings objects (RenderSettings, LightmapSettings, NavMeshSettings); the
    /// renderer reads RenderSettings data from the active scene.
    internal sealed class SceneSettingsObject : Object
    {
    }

    internal sealed class SceneSettingsBinder : Binder
    {
        public override Object Create(LoadRequest request)
        {
            var native = new NativeObject { Serialized = TypeTreeReader.ReadNative(request.ClassId, request.Open()) };
            return ObjectLoader.CreateNative<Object>(typeof(SceneSettingsObject), native);
        }

        public override void Fill(Object obj, LoadRequest request) { }
    }
}
