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

        // Each channel, alpha included, is c1·c2 / 255 by integer division (128×128 = 64).
        public static Color32 Multiply(this Color32 c1, Color32 c2) => new(
            (byte)(c1.r * c2.r / 255),
            (byte)(c1.g * c2.g / 255),
            (byte)(c1.b * c2.b / 255),
            (byte)(c1.a * c2.a / 255));

        // The same as Multiply, alpha included.
        public static Color32 Tint(this Color32 c1, Color32 c2) => Multiply(c1, c2);

        // c1's RGB with the smaller of the two alphas.
        public static Color MinAlpha(this Color c1, Color c2) => new(c1.r, c1.g, c1.b, Math.Min(c1.a, c2.a));

        // Exact equality per channel (no tolerance); the RGB forms ignore alpha.
        public static bool Compare(this Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        public static bool CompareRGB(this Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        public static bool Compare(this Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        public static bool CompareRGB(this Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b;

        static bool Same(float a, float b, int accuracy) => Math.Round(a, accuracy) == Math.Round(b, accuracy);
    }
}
