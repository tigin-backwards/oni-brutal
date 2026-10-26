using System;
using OniMoono.Engine.Serialization;

namespace UnityEngine
{
    public static partial class JsonUtility
    {
        public static string ToJson(object obj) => ToJson(obj, prettyPrint: false);

        /// A null object gives ""; engine objects other than MonoBehaviour/ScriptableObject are
        /// rejected. Thread-safe, as in Unity.
        public static string ToJson(object obj, bool prettyPrint)
        {
            if (obj == null) return "";
            if (obj is Object && obj is not MonoBehaviour && obj is not ScriptableObject)
                throw new ArgumentException("JsonUtility.ToJson does not support engine types.");
            return JsonWriter.Write(obj, prettyPrint);
        }

        /// Null or "" gives null (as Unity: a struct type then throws NullReferenceException).
        public static T FromJson<T>(string json) => (T)FromJson(json, typeof(T));

        public static object FromJson(string json, Type type)
        {
            if (string.IsNullOrEmpty(json)) return null;
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (typeof(MonoBehaviour).IsAssignableFrom(type) || typeof(ScriptableObject).IsAssignableFrom(type))
                throw new ArgumentException($"Cannot deserialize JSON to new instances of type '{type.Name}.'");
            return JsonReader.Read(json, type);
        }

        public static void FromJsonOverwrite(string json, object objectToOverwrite)
        {
            if (string.IsNullOrEmpty(json)) return;
            if (objectToOverwrite == null) throw new ArgumentNullException(nameof(objectToOverwrite));
            JsonReader.Overwrite(json, objectToOverwrite);
        }
    }
}
