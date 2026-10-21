using System;
using System.Collections.Generic;
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
        // CSS's named colours, as Unity 6 gives them (measured in the original player).
        public static Color darkGray => new Color(169f / 255f, 169f / 255f, 169f / 255f, 1f);
        public static Color orange => new Color(1f, 165f / 255f, 0f, 1f);
        public static Color violet => new Color(238f / 255f, 130f / 255f, 238f / 255f, 1f);
        public static Color grey => new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);

        public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;
        public float maxColorComponent => Mathf.Max(Mathf.Max(r, g), b);

        /// Hexcone HSV of a colour: V is the largest component; the dominant component (blue when
        /// it exceeds both others, else green when it exceeds red, else red) picks H's sector.
        /// Black gives H = S = 0; a grey keeps the sector offset in H with S = 0.
        public static void RGBToHSV(Color rgbColor, out float H, out float S, out float V)
        {
            float r = rgbColor.r, g = rgbColor.g, b = rgbColor.b;
            float sector, first, second;
            if (b > g && b > r) { V = b; sector = 4f; first = r; second = g; }
            else if (g > r) { V = g; sector = 2f; first = b; second = r; }
            else { V = r; sector = 0f; first = g; second = b; }

            if (V == 0f)
            {
                H = 0f;
                S = 0f;
                return;
            }
            float spread = V - Mathf.Min(r, Mathf.Min(g, b));
            if (spread == 0f)
            {
                S = 0f;
                H = (sector + (first - second)) / 6f;
            }
            else
            {
                S = spread / V;
                H = (sector + (first - second) / spread) / 6f;
            }
            if (H < 0f) H += 1f;
        }

        public static Color HSVToRGB(float H, float S, float V) => HSVToRGB(H, S, V, true);

        /// Inverse of RGBToHSV with alpha 1. Without hdr the components are clamped to [0, 1].
        /// H is used as given: sectors outside [-1, 6] give white.
        public static Color HSVToRGB(float H, float S, float V, bool hdr)
        {
            Color result;
            if (S == 0f)
                result = new Color(V, V, V, 1f);
            else if (V == 0f)
                result = new Color(0f, 0f, 0f, 1f);
            else
            {
                float h = H * 6f;
                float sector = Mathf.Floor(h);
                float f = h - sector;
                float p = V * (1f - S);
                float q = V * (1f - S * f);
                float t = V * (1f - S * (1f - f));
                result = (int)sector switch
                {
                    0 or 6 => new Color(V, t, p, 1f),
                    1 => new Color(q, V, p, 1f),
                    2 => new Color(p, V, t, 1f),
                    3 => new Color(p, q, V, 1f),
                    4 => new Color(t, p, V, 1f),
                    5 or -1 => new Color(V, p, q, 1f),
                    _ => new Color(1f, 1f, 1f, 1f),
                };
            }
            if (!hdr)
            {
                result.r = Mathf.Clamp01(result.r);
                result.g = Mathf.Clamp01(result.g);
                result.b = Mathf.Clamp01(result.b);
            }
            return result;
        }

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
        /// The names Unity accepts, case-insensitively, as measured in the original player
        /// (PerfProbe's measure mode: "grey" is one, "gray", "pink" and "clear" are not).
        static readonly Dictionary<string, Color32> HtmlNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["red"] = new(255, 0, 0, 255), ["cyan"] = new(0, 255, 255, 255), ["blue"] = new(0, 0, 255, 255),
            ["darkblue"] = new(0, 0, 139, 255), ["lightblue"] = new(173, 216, 230, 255), ["purple"] = new(128, 0, 128, 255),
            ["yellow"] = new(255, 255, 0, 255), ["lime"] = new(0, 255, 0, 255), ["fuchsia"] = new(255, 0, 255, 255),
            ["white"] = new(255, 255, 255, 255), ["silver"] = new(192, 192, 192, 255), ["grey"] = new(128, 128, 128, 255),
            ["black"] = new(0, 0, 0, 255), ["orange"] = new(255, 165, 0, 255), ["brown"] = new(165, 42, 42, 255),
            ["maroon"] = new(128, 0, 0, 255), ["green"] = new(0, 128, 0, 255), ["olive"] = new(128, 128, 0, 255),
            ["navy"] = new(0, 0, 128, 255), ["teal"] = new(0, 128, 128, 255), ["aqua"] = new(0, 255, 255, 255),
            ["magenta"] = new(255, 0, 255, 255),
        };

        /// "#RGB", "#RGBA", "#RRGGBB", "#RRGGBBAA" or a name, surrounding white space ignored; white
        /// (and false) for anything else, null included (measured).
        public static bool TryParseHtmlString(string htmlString, out Color color)
        {
            bool parsed = TryParseHtml(htmlString?.Trim(), out var c);
            color = parsed ? c : Color.white;
            return parsed;
        }

        static bool TryParseHtml(string text, out Color32 color)
        {
            color = default;
            if (string.IsNullOrEmpty(text)) return false;
            if (text[0] != '#') return HtmlNames.TryGetValue(text, out color);
            var digits = text.AsSpan(1);
            if (digits.Length is not (3 or 4 or 6 or 8)) return false;
            foreach (char d in digits)
                if (!char.IsAsciiHexDigit(d)) return false;
            bool shorthand = digits.Length <= 4;
            int width = shorthand ? 1 : 2, count = digits.Length / width;
            Span<byte> channels = stackalloc byte[4] { 0, 0, 0, 255 };
            for (int i = 0; i < count; i++)
            {
                int value = int.Parse(digits.Slice(i * width, width), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                channels[i] = (byte)(shorthand ? value * 17 : value);
            }
            color = new Color32(channels[0], channels[1], channels[2], channels[3]);
            return true;
        }

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
