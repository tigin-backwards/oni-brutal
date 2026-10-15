using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using OniBrutal.Assets.Files;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine.Serialization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OniBrutal.Engine.Assets
{
    /// One object being loaded.
    internal sealed class LoadRequest
    {
        public SerializedFile File;
        public SerializedFile.ObjectInfo Info;
        public int ClassId;
        public IReferenceResolver Resolver;
        /// Scene objects get the scene; everything else is a persistent asset.
        public SceneRecord Scene;
        public bool Persistent => Scene == null;
        public OniBrutal.Assets.IO.DataReader Open() => File.OpenObject(Info);
    }

    /// Materializes engine objects from asset files, lazily by PPtr. Each object is registered
    /// before its data is read, so references between objects (including cycles) resolve to the
    /// same instance. Loading is main-thread only, as in Unity.
    internal static class ObjectLoader
    {
        readonly record struct Key(string File, long PathId);

        static readonly Dictionary<Key, Object> loaded = new();
        static readonly Queue<Action> afterLoad = new();
        static int depth;

        /// The scene a level file is being loaded into (null when loading assets).
        static readonly Dictionary<string, SceneRecord> sceneOfFile = new(StringComparer.Ordinal);

        public static void BeginSceneFile(SerializedFile file, SceneRecord scene) => sceneOfFile[AssetSystem.KeyOf(file)] = scene;
        public static void EndSceneFile(SerializedFile file) => sceneOfFile.Remove(AssetSystem.KeyOf(file));

        public static Object Load(SerializedFile file, long pathId)
        {
            if (pathId == 0) return null;
            var key = new Key(AssetSystem.KeyOf(file), pathId);
            if (loaded.TryGetValue(key, out var existing)) return existing;
            if (!file.ObjectsById.TryGetValue(pathId, out var info)) return null; // missing reference

            var type = file.TypeOf(info);
            var request = new LoadRequest
            {
                File = file,
                Info = info,
                ClassId = type.ClassId,
                Resolver = new FileResolver(file),
                Scene = sceneOfFile.GetValueOrDefault(key.File),
            };
            var binder = Binders.For(type.ClassId);
            depth++;
            try
            {
                var obj = binder.Create(request);
                var native = obj.NativeOrThrow;
                native.IsPersistent = request.Persistent;
                native.Source = new AssetSource(key.File, pathId);
                loaded[key] = obj;
                binder.Fill(obj, request);
                return obj;
            }
            finally
            {
                depth--;
                if (depth == 0)
                    while (afterLoad.Count > 0) afterLoad.Dequeue()();
            }
        }

        /// Work to run once the outermost load finishes (e.g. ScriptableObject Awake/OnEnable,
        /// which Unity calls after the whole object graph is in place).
        public static void AfterLoad(Action action) => afterLoad.Enqueue(action);

        /// The native class of the object a PPtr designates, without loading it (-1 if missing).
        public static int ClassIdOf(SerializedFile from, PPtr pptr)
        {
            if (pptr.IsNull) return -1;
            var file = AssetSystem.ResolveExternal(from, pptr.FileId);
            return file.ObjectsById.TryGetValue(pptr.PathId, out var info) ? file.TypeOf(info).ClassId : -1;
        }

        /// MonoScripts exist in player data but have no managed object in a player.
        public static bool IsScriptAsset(SerializedFile from, PPtr pptr) => ClassIdOf(from, pptr) == 115;

        public static Object Resolve(SerializedFile from, PPtr pptr)
            => pptr.IsNull ? null : Load(AssetSystem.ResolveExternal(from, pptr.FileId), pptr.PathId);

        /// Forgets a destroyed or unloaded object.
        public static void Forget(AssetSource source) => loaded.Remove(new Key(source.File, source.PathId));

        public static IEnumerable<Object> Loaded => loaded.Values;

        sealed class FileResolver(SerializedFile file) : IReferenceResolver
        {
            public Object Resolve(PPtr pptr) => ObjectLoader.Resolve(file, pptr);
        }

        /// Creates an engine object of a native class without running a managed constructor
        /// (Unity creates wrappers for native objects the same way) and binds it.
        public static T CreateNative<T>(Type type, NativeObject native) where T : Object
        {
            var obj = (T)RuntimeHelpers.GetUninitializedObject(type);
            obj.Bind(native);
            return obj;
        }
    }

    /// Turns one serialized object into an engine object.
    internal abstract class Binder
    {
        public abstract Object Create(LoadRequest request);
        public abstract void Fill(Object obj, LoadRequest request);
    }

    internal static partial class Binders
    {
        const int GameObjectId = 1, TransformId = 4, MonoBehaviourId = 114, RectTransformId = 224, TextAssetId = 49, MonoScriptId = 115;

        static readonly Dictionary<int, Binder> binders = new()
        {
            [GameObjectId] = new GameObjectBinder(),
            [TransformId] = new TransformBinder(),
            [RectTransformId] = new TransformBinder(),
            [MonoBehaviourId] = new ScriptBinder(),
            [TextAssetId] = new TextAssetBinder(),
            [74] = new Mecanim.AnimationClipBinder(), // AnimationClip
            [104] = new SceneSettingsBinder(), // RenderSettings
            [157] = new SceneSettingsBinder(), // LightmapSettings
            [196] = new SceneSettingsBinder(), // NavMeshSettings
        };

        static readonly Dictionary<int, Binder> generic = new();

        public static void Register(int classId, Binder binder) => binders[classId] = binder;

        static Dictionary<string, Type> engineTypes;

        /// The engine's UnityEngine.Object subclass for a native class name (any UnityEngine
        /// namespace: VideoPlayer lives in UnityEngine.Video, SpriteAtlas in UnityEngine.U2D…).
        public static Type EngineTypeOf(string nativeClassName)
        {
            if (engineTypes == null)
            {
                var map = new Dictionary<string, Type>(StringComparer.Ordinal);
                foreach (var t in typeof(Object).Assembly.GetTypes())
                    if (typeof(Object).IsAssignableFrom(t) && t.Namespace != null && t.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal) && !map.ContainsKey(t.Name))
                        map[t.Name] = t;
                engineTypes = map;
            }
            // Native classes exposed through another managed type at runtime.
            if (nativeClassName == "AnimatorController") return typeof(RuntimeAnimatorController);
            return engineTypes.GetValueOrDefault(nativeClassName);
        }

        public static Binder For(int classId)
        {
            if (binders.TryGetValue(classId, out var b)) return b;
            if (generic.TryGetValue(classId, out b)) return b;
            if (classId == MonoScriptId) throw new InvalidDataException("MonoScripts are resolved by the script table, not loaded as objects.");
            var info = TypeTreeDatabase.Get(classId);
            var engineType = EngineTypeOf(info.Name)
                ?? throw new InvalidDataException($"No engine type for native class {info.Name} ({classId}).");
            b = typeof(Component).IsAssignableFrom(engineType) ? new StoredComponentBinder(engineType, classId) : new StoredAssetBinder(engineType, classId);
            generic[classId] = b;
            return b;
        }
    }
}
