using System;

// UnityEngine.TextRenderingModule data types (legacy text generator: uGUI Text, TextMesh).
namespace UnityEngine
{
    public partial struct TextGenerationSettings
    {
        public Font font;
        public Color color;
        public int fontSize;
        public float lineSpacing;
        public bool richText;
        public float scaleFactor;
        public FontStyle fontStyle;
        public TextAnchor textAnchor;
        public bool alignByGeometry;
        public bool resizeTextForBestFit;
        public int resizeTextMinSize;
        public int resizeTextMaxSize;
        public bool updateBounds;
        public VerticalWrapMode verticalOverflow;
        public HorizontalWrapMode horizontalOverflow;
        public Vector2 generationExtents;
        public Vector2 pivot;
        public bool generateOutOfBounds;

        // Unity: floats (colour channels, scale factor, line spacing, extents, pivot) compare with
        // Mathf.Approximately, everything else exactly, the font with UnityEngine.Object ==.
        // Unity does not override Equals(object)/GetHashCode here.
        public bool Equals(TextGenerationSettings other) =>
            Approx(color, other.color)
            && fontSize == other.fontSize
            && Mathf.Approximately(scaleFactor, other.scaleFactor)
            && resizeTextMinSize == other.resizeTextMinSize
            && resizeTextMaxSize == other.resizeTextMaxSize
            && Mathf.Approximately(lineSpacing, other.lineSpacing)
            && fontStyle == other.fontStyle
            && richText == other.richText
            && textAnchor == other.textAnchor
            && alignByGeometry == other.alignByGeometry
            && resizeTextForBestFit == other.resizeTextForBestFit
            && updateBounds == other.updateBounds
            && horizontalOverflow == other.horizontalOverflow
            && verticalOverflow == other.verticalOverflow
            && Approx(generationExtents, other.generationExtents)
            && Approx(pivot, other.pivot)
            && font == other.font;

        private static bool Approx(Color a, Color b) =>
            Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g)
            && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);

        private static bool Approx(Vector2 a, Vector2 b) =>
            Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
    }

    public partial struct UICharInfo
    {
        public Vector2 cursorPos;
        public float charWidth;
    }

    public partial struct UILineInfo
    {
        public int startCharIdx;
        public int height;
        public float topY;
        public float leading;
    }

    public partial struct UIVertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector4 tangent;
        public Color32 color;
        public Vector4 uv0;
        public Vector4 uv1;
        public Vector4 uv2;
        public Vector4 uv3;

        // Unity's default vertex: origin, normal facing the camera (0,0,-1), tangent (1,0,0,-1),
        // opaque white, zero UVs. A mutable public static field, as in Unity.
        public static UIVertex simpleVert = new UIVertex
        {
            position = new Vector3(0f, 0f, 0f),
            normal = new Vector3(0f, 0f, -1f),
            tangent = new Vector4(1f, 0f, 0f, -1f),
            color = new Color32(255, 255, 255, 255),
            uv0 = new Vector4(0f, 0f, 0f, 0f),
            uv1 = new Vector4(0f, 0f, 0f, 0f),
            uv2 = new Vector4(0f, 0f, 0f, 0f),
            uv3 = new Vector4(0f, 0f, 0f, 0f),
        };
    }

    public partial class TextGenerator
    {
        // Unity's Dispose releases the native generator. This TextGenerator has no native
        // generator (and no layout members yet), so there is nothing to release; disposing
        // twice is harmless, as in Unity.
        void IDisposable.Dispose() { }
    }
}
