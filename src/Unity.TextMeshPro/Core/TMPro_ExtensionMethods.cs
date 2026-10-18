using System;
using UnityEngine;

namespace TMPro
{
    public static partial class TMPro_ExtensionMethods
    {
        // Equal when every component agrees once rounded to `accuracy` decimal places.
        public static bool Compare(this Vector3 v1, Vector3 v2, int accuracy) =>
            Same(v1.x, v2.x, accuracy) && Same(v1.y, v2.y, accuracy) && Same(v1.z, v2.z, accuracy);

        public static bool Compare(this Quaternion q1, Quaternion q2, int accuracy) =>
            Same(q1.x, q2.x, accuracy) && Same(q1.y, q2.y, accuracy) && Same(q1.z, q2.z, accuracy) && Same(q1.w, q2.w, accuracy);

        // RGB multiplied by the factor (clamped to a byte); alpha kept.
        public static Color32 Tint(this Color32 c1, float tint) => new(
            (byte)Mathf.Clamp(c1.r * tint, 0f, 255f),
            (byte)Mathf.Clamp(c1.g * tint, 0f, 255f),
            (byte)Mathf.Clamp(c1.b * tint, 0f, 255f),
            c1.a);

        static bool Same(float a, float b, int accuracy) => Math.Round(a, accuracy) == Math.Round(b, accuracy);
    }
}
