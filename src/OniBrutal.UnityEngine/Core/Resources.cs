using System;
using System.Collections.Generic;
using OniBrutal.Engine;
using OniBrutal.Engine.Assets;

namespace UnityEngine
{
    public partial class Resources
    {
        /// Assets under a "Resources" folder, by path relative to it, without extension. Paths are
        /// matched case-insensitively (the container stores them lower-cased); when several assets
        /// share a path the first one of the requested type is returned.
        public static Object Load(string path) => Load(path, typeof(Object));

        public static T Load<T>(string path) where T : Object => (T)Load(path, typeof(T));

        public static Object Load(string path, Type systemTypeInstance)
        {
            Threads.AssertMainThread("Resources.Load");
            if (path == null) throw new ArgumentNullException(nameof(path));
            var key = path.ToLowerInvariant();
            foreach (var entry in ProjectSettings.ResourceContainer)
            {
                if (entry.Path != key || ObjectLoader.IsScriptAsset(ProjectSettings.Managers, entry.Target)) continue;
                var obj = ObjectLoader.Resolve(ProjectSettings.Managers, entry.Target);
                if (obj != null && systemTypeInstance.IsInstanceOfType(obj)) return obj;
            }
            return null;
        }

        public static T[] LoadAll<T>(string path) where T : Object
        {
            var list = new List<T>();
            foreach (var o in LoadAll(path, typeof(T))) list.Add((T)o);
            return list.ToArray();
        }

        public static Object[] LoadAll(string path) => LoadAll(path, typeof(Object));

        /// Every asset at `path` or in folders below it.
        public static Object[] LoadAll(string path, Type systemTypeInstance)
        {
            Threads.AssertMainThread("Resources.LoadAll");
            var key = path.ToLowerInvariant().TrimEnd('/');
            var result = new List<Object>();
            foreach (var entry in ProjectSettings.ResourceContainer)
            {
                if (!(key.Length == 0 || entry.Path == key || entry.Path.StartsWith(key + "/", StringComparison.Ordinal))) continue;
                if (ObjectLoader.IsScriptAsset(ProjectSettings.Managers, entry.Target)) continue;
                var obj = ObjectLoader.Resolve(ProjectSettings.Managers, entry.Target);
                if (obj != null && systemTypeInstance.IsInstanceOfType(obj) && !result.Contains(obj)) result.Add(obj);
            }
            var typed = (Object[])Array.CreateInstance(systemTypeInstance, result.Count);
            for (int i = 0; i < result.Count; i++) typed.SetValue(result[i], i);
            return typed;
        }

        /// Built-in engine assets by name (meshes such as "Quad.fbx", the default font…).
        public static T GetBuiltinResource<T>(string path) where T : Object => (T)GetBuiltinResource(typeof(T), path);

        public static Object GetBuiltinResource(Type type, string path)
        {
            var file = AssetSystem.Get("unity default resources");
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (var info in file.Objects)
            {
                var obj = BuiltinCandidate(file, info, name);
                if (obj != null && type.IsInstanceOfType(obj)) return obj;
            }
            Debug.LogError($"The resource {path} could not be loaded from the resource file!");
            return null;
        }

        static Object BuiltinCandidate(OniBrutal.Assets.Files.SerializedFile file, OniBrutal.Assets.Files.SerializedFile.ObjectInfo info, string name)
        {
            // Named built-in assets carry m_Name first; read just that to avoid loading everything.
            var r = file.OpenObject(info);
            if (r.Length < 4) return null;
            int length = r.ReadInt32();
            if (length < 0 || length > r.Remaining) return null;
            var objName = System.Text.Encoding.UTF8.GetString(r.ReadSpan(length));
            return objName == name ? ObjectLoader.Load(file, info.PathId) : null;
        }

        public static T[] FindObjectsOfTypeAll<T>() where T : Object
        {
            var list = new List<T>();
            foreach (var o in FindObjectsOfTypeAll(typeof(T))) list.Add((T)o);
            return list.ToArray();
        }

        /// Every live object of the type: loaded assets, scene objects and inactive ones.
        public static Object[] FindObjectsOfTypeAll(Type type)
        {
            var result = new List<Object>();
            foreach (var native in ObjectTable.Snapshot())
                if (native.Managed != null && type.IsInstanceOfType(native.Managed)) result.Add(native.Managed);
            var typed = (Object[])Array.CreateInstance(type, result.Count);
            for (int i = 0; i < result.Count; i++) typed.SetValue(result[i], i);
            return typed;
        }

        /// Unity destroys loaded assets that nothing references. Here loaded assets stay resident
        /// and the operation completes at once; reachability-based unloading is tracked in
        /// docs/PLAN.md (open items).
        public static AsyncOperation UnloadUnusedAssets()
        {
            var op = new AsyncOperation();
            op.Complete();
            return op;
        }
    }
}
