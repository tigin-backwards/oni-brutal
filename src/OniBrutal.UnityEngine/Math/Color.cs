using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace UnityEngine
{
    public partial struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }

        public float this[int index]
        {
            get => index switch { 0 => r, 1 => g, 2 => b, 3 => a, _ => throw new IndexOutOfRangeException("Invalid Color index(" + index + ")!") };
            set
            {
                switch (index)
                {
                    case 0: r = value; break;
                    case 1: g = value; break;
                    case 2: b = value; break;
                    case 3: a = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Color index(" + index + ")!");
                }
            }
        }

        public static Color red => new Color(1f, 0f, 0f, 1f);
        public static Color green => new Color(0f, 1f, 0f, 1f);
        public static Color blue => new Color(0f, 0f, 1f, 1f);
        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color yellow => new Color(1f, 0.921568632f, 0.0156862754f, 1f);
        public static Color cyan => new Color(0f, 1f, 1f, 1f);
        public static Color magenta => new Color(1f, 0f, 1f, 1f);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color grey => new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);

        public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;
        public float maxColorComponent => Mathf.Max(Mathf.Max(r, g), b);

        public static Color Lerp(Color a, Color b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));

        public static Color LerpUnclamped(Color a, Color b, float t)
            => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);

        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color operator -(Color a, Color b) => new Color(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
        public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator *(Color a, float b) => new Color(a.r * b, a.g * b, a.b * b, a.a * b);
        public static Color operator *(float b, Color a) => new Color(a.r * b, a.g * b, a.b * b, a.a * b);
        public static Color operator /(Color a, float b) => new Color(a.r / b, a.g / b, a.b / b, a.a / b);

        /// Approximate equality through Vector4 ==.
        public static bool operator ==(Color lhs, Color rhs) => (Vector4)lhs == (Vector4)rhs;
        public static bool operator !=(Color lhs, Color rhs) => !(lhs == rhs);

        public static implicit operator Vector4(Color c) => new Vector4(c.r, c.g, c.b, c.a);
        public static implicit operator Color(Vector4 v) => new Color(v.x, v.y, v.z, v.w);





        public override bool Equals(object other) => other is Color c && Equals(c);
        public bool Equals(Color other) => r.Equals(other.r) && g.Equals(other.g) && b.Equals(other.b) && a.Equals(other.a);
        public override int GetHashCode() => ((Vector4)this).GetHashCode();

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) format = "F3";
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("RGBA({0}, {1}, {2}, {3})", UnityFormat.Number(r, format, formatProvider), UnityFormat.Number(g, format, formatProvider),
                UnityFormat.Number(b, format, formatProvider), UnityFormat.Number(a, format, formatProvider));
        }
    }

    /// Four bytes overlapping one int, as Unity declares it. KSerialization serializes Color32
    /// reflectively: its only public instance state is r, g, b, a (ARCHITECTURE §3.2).
    [StructLayout(LayoutKind.Explicit)]
    public partial struct Color32
    {
        [FieldOffset(0)] private int rgba;
        [FieldOffset(0)] public byte r;
        [FieldOffset(1)] public byte g;
        [FieldOffset(2)] public byte b;
        [FieldOffset(3)] public byte a;

        public Color32(byte r, byte g, byte b, byte a)
        {
            rgba = 0;
            this.r = r; this.g = g; this.b = b; this.a = a;
        }

        public byte this[int index]
        {
            get => index switch { 0 => r, 1 => g, 2 => b, 3 => a, _ => throw new IndexOutOfRangeException("Invalid Color32 index(" + index + ")!") };
            set
            {
                switch (index)
                {
                    case 0: r = value; break;
                    case 1: g = value; break;
                    case 2: b = value; break;
                    case 3: a = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Color32 index(" + index + ")!");
                }
            }
        }

        public static implicit operator Color32(Color c) => new Color32(
            (byte)Math.Round(Mathf.Clamp01(c.r) * 255f), (byte)Math.Round(Mathf.Clamp01(c.g) * 255f),
            (byte)Math.Round(Mathf.Clamp01(c.b) * 255f), (byte)Math.Round(Mathf.Clamp01(c.a) * 255f));

        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);

        public static Color32 Lerp(Color32 a, Color32 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));

        public static Color32 LerpUnclamped(Color32 a, Color32 b, float t) => new Color32(
            (byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), (byte)(a.a + (b.a - a.a) * t));

        public override bool Equals(object other) => other is Color32 c && Equals(c);
        public bool Equals(Color32 other) => rgba == other.rgba;
        public override int GetHashCode() => rgba.GetHashCode();

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("RGBA({0}, {1}, {2}, {3})", UnityFormat.Number(r, format, formatProvider), UnityFormat.Number(g, format, formatProvider),
                UnityFormat.Number(b, format, formatProvider), UnityFormat.Number(a, format, formatProvider));
        }
    }

    public partial class ColorUtility
    {
        public static string ToHtmlStringRGB(Color color)
        {
            Color32 c = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
            return string.Format(CultureInfo.InvariantCulture, "{0:X2}{1:X2}{2:X2}", c.r, c.g, c.b);
        }

        public static string ToHtmlStringRGBA(Color color)
        {
            Color32 c = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), Mathf.Clamp01(color.a));
            return string.Format(CultureInfo.InvariantCulture, "{0:X2}{1:X2}{2:X2}{3:X2}", c.r, c.g, c.b, c.a);
        }
    }
}
