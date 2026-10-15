using System;
using OniBrutal.Engine.Serialization;

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
    }
}
