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

        // Every named colour Unity 6 has, as the original player gives it (tools/UnityProbe,
        // PROBE_SCENARIO=statics; sv-check.py cs wrote these lines from its log). The values are
        // the measured float bits, which are not always n / 255f.
        public static Color aliceBlue => new Color(0.9411765f, 0.9725491f, 1f, 1f);
        public static Color antiqueWhite => new Color(0.9803922f, 0.9215687f, 0.8431373f, 1f);
        public static Color aquamarine => new Color(0.4980392f, 1f, 0.8313726f, 1f);
        public static Color azure => new Color(0.9411765f, 1f, 1f, 1f);
        public static Color beige => new Color(0.9607844f, 0.9607844f, 0.8627452f, 1f);
        public static Color bisque => new Color(1f, 0.8941177f, 0.7686275f, 1f);
        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color blanchedAlmond => new Color(1f, 0.9215687f, 0.8039216f, 1f);
        public static Color blue => new Color(0f, 0f, 1f, 1f);
        public static Color blueViolet => new Color(0.5411765f, 0.1686275f, 0.8862746f, 1f);
        public static Color brown => new Color(0.6470588f, 0.1647059f, 0.1647059f, 1f);
        public static Color burlywood => new Color(0.8705883f, 0.7215686f, 0.5294118f, 1f);
        public static Color cadetBlue => new Color(0.372549f, 0.6196079f, 0.627451f, 1f);
        public static Color chartreuse => new Color(0.4980392f, 1f, 0f, 1f);
        public static Color chocolate => new Color(0.8235295f, 0.4117647f, 0.1176471f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
        public static Color coral => new Color(1f, 0.4980392f, 0.3137255f, 1f);
        public static Color cornflowerBlue => new Color(0.3921569f, 0.5843138f, 0.9294118f, 1f);
        public static Color cornsilk => new Color(1f, 0.9725491f, 0.8627452f, 1f);
        public static Color crimson => new Color(0.8627452f, 0.07843138f, 0.2352941f, 1f);
        public static Color cyan => new Color(0f, 1f, 1f, 1f);
        public static Color darkBlue => new Color(0f, 0f, 0.5450981f, 1f);
        public static Color darkCyan => new Color(0f, 0.5450981f, 0.5450981f, 1f);
        public static Color darkGoldenRod => new Color(0.7215686f, 0.5254902f, 0.04313726f, 1f);
        public static Color darkGray => new Color(0.6627451f, 0.6627451f, 0.6627451f, 1f);
        public static Color darkGreen => new Color(0f, 0.3921569f, 0f, 1f);
        public static Color darkKhaki => new Color(0.7411765f, 0.7176471f, 0.4196079f, 1f);
        public static Color darkMagenta => new Color(0.5450981f, 0f, 0.5450981f, 1f);
        public static Color darkOliveGreen => new Color(0.3333333f, 0.4196079f, 0.1843137f, 1f);
        public static Color darkOrange => new Color(1f, 0.5490196f, 0f, 1f);
        public static Color darkOrchid => new Color(0.6f, 0.1960784f, 0.8000001f, 1f);
        public static Color darkRed => new Color(0.5450981f, 0f, 0f, 1f);
        public static Color darkSalmon => new Color(0.9137256f, 0.5882353f, 0.4784314f, 1f);
        public static Color darkSeaGreen => new Color(0.5607843f, 0.7372549f, 0.5607843f, 1f);
        public static Color darkSlateBlue => new Color(0.282353f, 0.2392157f, 0.5450981f, 1f);
        public static Color darkSlateGray => new Color(0.1843137f, 0.3098039f, 0.3098039f, 1f);
        public static Color darkTurquoise => new Color(0f, 0.8078432f, 0.8196079f, 1f);
        public static Color darkViolet => new Color(0.5803922f, 0f, 0.8274511f, 1f);
        public static Color deepPink => new Color(1f, 0.07843138f, 0.5764706f, 1f);
        public static Color deepSkyBlue => new Color(0f, 0.7490196f, 1f, 1f);
        public static Color dimGray => new Color(0.4117647f, 0.4117647f, 0.4117647f, 1f);
        public static Color dodgerBlue => new Color(0.1176471f, 0.5647059f, 1f, 1f);
        public static Color firebrick => new Color(0.6980392f, 0.1333333f, 0.1333333f, 1f);
        public static Color floralWhite => new Color(1f, 0.9803922f, 0.9411765f, 1f);
        public static Color forestGreen => new Color(0.1333333f, 0.5450981f, 0.1333333f, 1f);
        public static Color gainsboro => new Color(0.8627452f, 0.8627452f, 0.8627452f, 1f);
        public static Color ghostWhite => new Color(0.9725491f, 0.9725491f, 1f, 1f);
        public static Color gold => new Color(1f, 0.8431373f, 0f, 1f);
        public static Color goldenRod => new Color(0.854902f, 0.6470588f, 0.1254902f, 1f);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color gray1 => new Color(0.1f, 0.1f, 0.1f, 1f);
        public static Color gray2 => new Color(0.2f, 0.2f, 0.2f, 1f);
        public static Color gray3 => new Color(0.3f, 0.3f, 0.3f, 1f);
        public static Color gray4 => new Color(0.4f, 0.4f, 0.4f, 1f);
        public static Color gray5 => new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color gray6 => new Color(0.6f, 0.6f, 0.6f, 1f);
        public static Color gray7 => new Color(0.7f, 0.7f, 0.7f, 1f);
        public static Color gray8 => new Color(0.8f, 0.8f, 0.8f, 1f);
        public static Color gray9 => new Color(0.9f, 0.9f, 0.9f, 1f);
        public static Color green => new Color(0f, 1f, 0f, 1f);
        public static Color greenYellow => new Color(0.6784314f, 1f, 0.1843137f, 1f);
        public static Color grey => new Color(0.5f, 0.5f, 0.5f, 1f);
        public static Color honeydew => new Color(0.9411765f, 1f, 0.9411765f, 1f);
        public static Color hotPink => new Color(1f, 0.4117647f, 0.7058824f, 1f);
        public static Color indianRed => new Color(0.8039216f, 0.3607843f, 0.3607843f, 1f);
        public static Color indigo => new Color(0.2941177f, 0f, 0.509804f, 1f);
        public static Color ivory => new Color(1f, 1f, 0.9411765f, 1f);
        public static Color khaki => new Color(0.9411765f, 0.9019608f, 0.5490196f, 1f);
        public static Color lavender => new Color(0.9019608f, 0.9019608f, 0.9803922f, 1f);
        public static Color lavenderBlush => new Color(1f, 0.9411765f, 0.9607844f, 1f);
        public static Color lawnGreen => new Color(0.4862745f, 0.9882354f, 0f, 1f);
        public static Color lemonChiffon => new Color(1f, 0.9803922f, 0.8039216f, 1f);
        public static Color lightBlue => new Color(0.6784314f, 0.8470589f, 0.9019608f, 1f);
        public static Color lightCoral => new Color(0.9411765f, 0.5019608f, 0.5019608f, 1f);
        public static Color lightCyan => new Color(0.8784314f, 1f, 1f, 1f);
        public static Color lightGoldenRod => new Color(0.9333334f, 0.8666667f, 0.509804f, 1f);
        public static Color lightGoldenRodYellow => new Color(0.9803922f, 0.9803922f, 0.8235295f, 1f);
        public static Color lightGray => new Color(0.8274511f, 0.8274511f, 0.8274511f, 1f);
        public static Color lightGreen => new Color(0.5647059f, 0.9333334f, 0.5647059f, 1f);
        public static Color lightPink => new Color(1f, 0.7137255f, 0.7568628f, 1f);
        public static Color lightSalmon => new Color(1f, 0.627451f, 0.4784314f, 1f);
        public static Color lightSeaGreen => new Color(0.1254902f, 0.6980392f, 0.6666667f, 1f);
        public static Color lightSkyBlue => new Color(0.5294118f, 0.8078432f, 0.9803922f, 1f);
        public static Color lightSlateBlue => new Color(0.5176471f, 0.4392157f, 1f, 1f);
        public static Color lightSlateGray => new Color(0.4666667f, 0.5333334f, 0.6f, 1f);
        public static Color lightSteelBlue => new Color(0.6901961f, 0.7686275f, 0.8705883f, 1f);
        public static Color lightYellow => new Color(1f, 1f, 0.8784314f, 1f);
        public static Color limeGreen => new Color(0.1960784f, 0.8039216f, 0.1960784f, 1f);
        public static Color linen => new Color(0.9803922f, 0.9411765f, 0.9019608f, 1f);
        public static Color magenta => new Color(1f, 0f, 1f, 1f);
        public static Color maroon => new Color(0.6901961f, 0.1882353f, 0.3764706f, 1f);
        public static Color mediumAquamarine => new Color(0.4f, 0.8039216f, 0.6666667f, 1f);
        public static Color mediumBlue => new Color(0f, 0f, 0.8039216f, 1f);
        public static Color mediumOrchid => new Color(0.7294118f, 0.3333333f, 0.8274511f, 1f);
        public static Color mediumPurple => new Color(0.5764706f, 0.4392157f, 0.8588236f, 1f);
        public static Color mediumSeaGreen => new Color(0.2352941f, 0.7019608f, 0.4431373f, 1f);
        public static Color mediumSlateBlue => new Color(0.482353f, 0.4078432f, 0.9333334f, 1f);
        public static Color mediumSpringGreen => new Color(0f, 0.9803922f, 0.6039216f, 1f);
        public static Color mediumTurquoise => new Color(0.282353f, 0.8196079f, 0.8000001f, 1f);
        public static Color mediumVioletRed => new Color(0.7803922f, 0.08235294f, 0.5215687f, 1f);
        public static Color midnightBlue => new Color(0.09803922f, 0.09803922f, 0.4392157f, 1f);
        public static Color mintCream => new Color(0.9607844f, 1f, 0.9803922f, 1f);
        public static Color mistyRose => new Color(1f, 0.8941177f, 0.882353f, 1f);
        public static Color moccasin => new Color(1f, 0.8941177f, 0.7098039f, 1f);
        public static Color navajoWhite => new Color(1f, 0.8705883f, 0.6784314f, 1f);
        public static Color navyBlue => new Color(0f, 0f, 0.5019608f, 1f);
        public static Color oldLace => new Color(0.9921569f, 0.9607844f, 0.9019608f, 1f);
        public static Color olive => new Color(0.5019608f, 0.5019608f, 0f, 1f);
        public static Color oliveDrab => new Color(0.4196079f, 0.5568628f, 0.1372549f, 1f);
        public static Color orange => new Color(1f, 0.6470588f, 0f, 1f);
        public static Color orangeRed => new Color(1f, 0.2705882f, 0f, 1f);
        public static Color orchid => new Color(0.854902f, 0.4392157f, 0.8392158f, 1f);
        public static Color paleGoldenRod => new Color(0.9333334f, 0.909804f, 0.6666667f, 1f);
        public static Color paleGreen => new Color(0.5960785f, 0.9843138f, 0.5960785f, 1f);
        public static Color paleTurquoise => new Color(0.6862745f, 0.9333334f, 0.9333334f, 1f);
        public static Color paleVioletRed => new Color(0.8588236f, 0.4392157f, 0.5764706f, 1f);
        public static Color papayaWhip => new Color(1f, 0.937255f, 0.8352942f, 1f);
        public static Color peachPuff => new Color(1f, 0.854902f, 0.7254902f, 1f);
        public static Color peru => new Color(0.8039216f, 0.5215687f, 0.2470588f, 1f);
        public static Color pink => new Color(1f, 0.7529413f, 0.7960785f, 1f);
        public static Color plum => new Color(0.8666667f, 0.627451f, 0.8666667f, 1f);
        public static Color powderBlue => new Color(0.6901961f, 0.8784314f, 0.9019608f, 1f);
        public static Color purple => new Color(0.627451f, 0.1254902f, 0.9411765f, 1f);
        public static Color rebeccaPurple => new Color(0.4f, 0.2f, 0.6f, 1f);
        public static Color red => new Color(1f, 0f, 0f, 1f);
        public static Color rosyBrown => new Color(0.7372549f, 0.5607843f, 0.5607843f, 1f);
        public static Color royalBlue => new Color(0.254902f, 0.4117647f, 0.882353f, 1f);
        public static Color saddleBrown => new Color(0.5450981f, 0.2705882f, 0.07450981f, 1f);
        public static Color salmon => new Color(0.9803922f, 0.5019608f, 0.4470589f, 1f);
        public static Color sandyBrown => new Color(0.9568628f, 0.6431373f, 0.3764706f, 1f);
        public static Color seaGreen => new Color(0.1803922f, 0.5450981f, 0.3411765f, 1f);
        public static Color seashell => new Color(1f, 0.9607844f, 0.9333334f, 1f);
        public static Color sienna => new Color(0.627451f, 0.3215686f, 0.1764706f, 1f);
        public static Color silver => new Color(0.7529413f, 0.7529413f, 0.7529413f, 1f);
        public static Color skyBlue => new Color(0.5294118f, 0.8078432f, 0.9215687f, 1f);
        public static Color slateBlue => new Color(0.4156863f, 0.3529412f, 0.8039216f, 1f);
        public static Color slateGray => new Color(0.4392157f, 0.5019608f, 0.5647059f, 1f);
        public static Color snow => new Color(1f, 0.9803922f, 0.9803922f, 1f);
        public static Color softBlue => new Color(0.1882353f, 0.682353f, 0.7490196f, 1f);
        public static Color softGreen => new Color(0.5490196f, 0.7882354f, 0.1411765f, 1f);
        public static Color softRed => new Color(0.8627452f, 0.1921569f, 0.1960784f, 1f);
        public static Color softYellow => new Color(1f, 0.9333334f, 0.5490196f, 1f);
        public static Color springGreen => new Color(0f, 1f, 0.4980392f, 1f);
        public static Color steelBlue => new Color(0.2745098f, 0.509804f, 0.7058824f, 1f);
        public static Color tan => new Color(0.8235295f, 0.7058824f, 0.5490196f, 1f);
        public static Color teal => new Color(0f, 0.5019608f, 0.5019608f, 1f);
        public static Color thistle => new Color(0.8470589f, 0.7490196f, 0.8470589f, 1f);
        public static Color tomato => new Color(1f, 0.3882353f, 0.2784314f, 1f);
        public static Color turquoise => new Color(0.2509804f, 0.8784314f, 0.8156863f, 1f);
        public static Color violet => new Color(0.9333334f, 0.509804f, 0.9333334f, 1f);
        public static Color violetRed => new Color(0.8156863f, 0.1254902f, 0.5647059f, 1f);
        public static Color wheat => new Color(0.9607844f, 0.8705883f, 0.7019608f, 1f);
        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color whiteSmoke => new Color(0.9607844f, 0.9607844f, 0.9607844f, 1f);
        public static Color yellow => new Color(1f, 0.92156863f, 0.015686275f, 1f);
        public static Color yellowGreen => new Color(0.6039216f, 0.8039216f, 0.1960784f, 1f);
        public static Color yellowNice => new Color(1f, 0.92156863f, 0.015686275f, 1f);

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
