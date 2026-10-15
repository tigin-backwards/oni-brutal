using OniBrutal.Engine.Serialization;

namespace UnityEngine
{
    public partial class Object
    {
        /// Engine hook: a copy of this asset (textures, meshes, materials…) for Instantiate;
        /// null when the type has no engine-side copy.
        internal virtual Object EngineCloneAsset() => null;

        /// Marks an asset as modified for the editor's save tracking; a player tracks nothing.
        internal void MarkDirty() { }

        public static Object Instantiate(Object original) => Cloner.Instantiate(original, null, false, null, null);
        public static Object Instantiate(Object original, Transform parent) => Cloner.Instantiate(original, parent, false, null, null);
        public static Object Instantiate(Object original, Transform parent, bool instantiateInWorldSpace) => Cloner.Instantiate(original, parent, instantiateInWorldSpace, null, null);
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation) => Cloner.Instantiate(original, null, true, position, rotation);
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation, Transform parent) => Cloner.Instantiate(original, parent, true, position, rotation);

        public static T Instantiate<T>(T original) where T : Object => (T)Instantiate((Object)original);
        public static T Instantiate<T>(T original, Transform parent) where T : Object => (T)Instantiate(original, parent, false);
        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object => (T)Instantiate((Object)original, parent, worldPositionStays);
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => (T)Instantiate((Object)original, position, rotation);
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object => (T)Instantiate((Object)original, position, rotation, parent);
    }
}
