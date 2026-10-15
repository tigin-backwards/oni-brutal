using System;
using System.Globalization;

namespace UnityEngine
{
    public partial struct Rect
    {
        private float m_XMin;
        private float m_YMin;
        private float m_Width;
        private float m_Height;

        public Rect(float x, float y, float width, float height) { m_XMin = x; m_YMin = y; m_Width = width; m_Height = height; }
        public Rect(Vector2 position, Vector2 size) { m_XMin = position.x; m_YMin = position.y; m_Width = size.x; m_Height = size.y; }
        public Rect(Rect source) { m_XMin = source.m_XMin; m_YMin = source.m_YMin; m_Width = source.m_Width; m_Height = source.m_Height; }

        public static Rect zero => new Rect(0f, 0f, 0f, 0f);

        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax) => new Rect(xmin, ymin, xmax - xmin, ymax - ymin);

        public void Set(float x, float y, float width, float height) { m_XMin = x; m_YMin = y; m_Width = width; m_Height = height; }

        public float x { get => m_XMin; set => m_XMin = value; }
        public float y { get => m_YMin; set => m_YMin = value; }
        public float width { get => m_Width; set => m_Width = value; }
        public float height { get => m_Height; set => m_Height = value; }

        public Vector2 position
        {
            get => new Vector2(m_XMin, m_YMin);
            set { m_XMin = value.x; m_YMin = value.y; }
        }

        public Vector2 center
        {
            get => new Vector2(x + m_Width / 2f, y + m_Height / 2f);
            set { m_XMin = value.x - m_Width / 2f; m_YMin = value.y - m_Height / 2f; }
        }

        public Vector2 min { get => new Vector2(xMin, yMin); set { xMin = value.x; yMin = value.y; } }
        public Vector2 max { get => new Vector2(xMax, yMax); set { xMax = value.x; yMax = value.y; } }
        public Vector2 size { get => new Vector2(m_Width, m_Height); set { m_Width = value.x; m_Height = value.y; } }

        public float xMin
        {
            get => m_XMin;
            set { float oldxmax = xMax; m_XMin = value; m_Width = oldxmax - m_XMin; }
        }

        public float yMin
        {
            get => m_YMin;
            set { float oldymax = yMax; m_YMin = value; m_Height = oldymax - m_YMin; }
        }

        public float xMax { get => m_Width + m_XMin; set => m_Width = value - m_XMin; }
        public float yMax { get => m_Height + m_YMin; set => m_Height = value - m_YMin; }

        /// Inclusive at min, exclusive at max.
        public bool Contains(Vector2 point) => point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;
        public bool Contains(Vector3 point) => point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;

        public bool Contains(Vector3 point, bool allowInverse)
        {
            if (!allowInverse) return Contains(point);
            bool xAxis = (width < 0f && point.x <= xMin && point.x > xMax) || (width >= 0f && point.x >= xMin && point.x < xMax);
            bool yAxis = (height < 0f && point.y <= yMin && point.y > yMax) || (height >= 0f && point.y >= yMin && point.y < yMax);
            return xAxis && yAxis;
        }

        static Rect OrderMinMax(Rect rect)
        {
            if (rect.xMin > rect.xMax) { float t = rect.xMin; rect.xMin = rect.xMax; rect.xMax = t; }
            if (rect.yMin > rect.yMax) { float t = rect.yMin; rect.yMin = rect.yMax; rect.yMax = t; }
            return rect;
        }

        public bool Overlaps(Rect other) => other.xMax > xMin && other.xMin < xMax && other.yMax > yMin && other.yMin < yMax;

        public bool Overlaps(Rect other, bool allowInverse)
        {
            Rect self = this;
            if (allowInverse)
            {
                self = OrderMinMax(self);
                other = OrderMinMax(other);
            }
            return self.Overlaps(other);
        }

        public static Vector2 NormalizedToPoint(Rect rectangle, Vector2 normalizedRectCoordinates)
            => new Vector2(Mathf.Lerp(rectangle.x, rectangle.xMax, normalizedRectCoordinates.x), Mathf.Lerp(rectangle.y, rectangle.yMax, normalizedRectCoordinates.y));

        public static Vector2 PointToNormalized(Rect rectangle, Vector2 point)
            => new Vector2(Mathf.InverseLerp(rectangle.x, rectangle.xMax, point.x), Mathf.InverseLerp(rectangle.y, rectangle.yMax, point.y));

        public static bool operator ==(Rect lhs, Rect rhs) => lhs.x == rhs.x && lhs.y == rhs.y && lhs.width == rhs.width && lhs.height == rhs.height;
        public static bool operator !=(Rect lhs, Rect rhs) => !(lhs == rhs);

        public override bool Equals(object other) => other is Rect r && Equals(r);
        public bool Equals(Rect other) => x.Equals(other.x) && y.Equals(other.y) && width.Equals(other.width) && height.Equals(other.height);
        public override int GetHashCode() => x.GetHashCode() ^ (width.GetHashCode() << 2) ^ (y.GetHashCode() >> 2) ^ (height.GetHashCode() >> 1);

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) format = "F2";
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("(x:{0}, y:{1}, width:{2}, height:{3})", x.ToString(format, formatProvider), y.ToString(format, formatProvider),
                width.ToString(format, formatProvider), height.ToString(format, formatProvider));
        }
    }

    public partial struct RectInt
    {
        private int m_XMin;
        private int m_YMin;
        private int m_Width;
        private int m_Height;

        public RectInt(int xMin, int yMin, int width, int height) { m_XMin = xMin; m_YMin = yMin; m_Width = width; m_Height = height; }
        public RectInt(Vector2Int position, Vector2Int size) { m_XMin = position.x; m_YMin = position.y; m_Width = size.x; m_Height = size.y; }

        public int x { get => m_XMin; set => m_XMin = value; }
        public int y { get => m_YMin; set => m_YMin = value; }
        public int width { get => m_Width; set => m_Width = value; }
        public int height { get => m_Height; set => m_Height = value; }

        public Vector2 center => new Vector2(x + m_Width / 2f, y + m_Height / 2f);

        public int xMin { get => Math.Min(m_XMin, m_XMin + m_Width); set { int oldxmax = xMax; m_XMin = value; m_Width = oldxmax - m_XMin; } }
        public int yMin { get => Math.Min(m_YMin, m_YMin + m_Height); set { int oldymax = yMax; m_YMin = value; m_Height = oldymax - m_YMin; } }
        public int xMax { get => Math.Max(m_XMin, m_XMin + m_Width); set => m_Width = value - m_XMin; }
        public int yMax { get => Math.Max(m_YMin, m_YMin + m_Height); set => m_Height = value - m_YMin; }

        public Vector2Int min { get => new Vector2Int(xMin, yMin); set { xMin = value.x; yMin = value.y; } }
        public Vector2Int max { get => new Vector2Int(xMax, yMax); set { xMax = value.x; yMax = value.y; } }
        public Vector2Int position { get => new Vector2Int(m_XMin, m_YMin); set { m_XMin = value.x; m_YMin = value.y; } }
        public Vector2Int size { get => new Vector2Int(m_Width, m_Height); set { m_Width = value.x; m_Height = value.y; } }

        public void SetMinMax(Vector2Int minPosition, Vector2Int maxPosition) { min = minPosition; max = maxPosition; }

        public bool Contains(Vector2Int position)
            => position.x >= xMin && position.y >= yMin && position.x < xMax && position.y < yMax;

        public bool Overlaps(RectInt other) => other.xMin < xMax && other.xMax > xMin && other.yMin < yMax && other.yMax > yMin;

        public override bool Equals(object other) => other is RectInt r && Equals(r);
        public bool Equals(RectInt other) => m_XMin == other.m_XMin && m_YMin == other.m_YMin && m_Width == other.m_Width && m_Height == other.m_Height;

        public override int GetHashCode()
        {
            int xMinHash = xMin.GetHashCode(), yMinHash = yMin.GetHashCode(), xMaxHash = xMax.GetHashCode(), yMaxHash = yMax.GetHashCode();
            return xMinHash ^ (yMinHash << 4) ^ (yMinHash >> 28) ^ (xMaxHash >> 4) ^ (xMaxHash << 28) ^ (yMaxHash >> 4) ^ (yMaxHash << 28);
        }

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("(x:{0}, y:{1}, width:{2}, height:{3})", x.ToString(format, formatProvider), y.ToString(format, formatProvider),
                width.ToString(format, formatProvider), height.ToString(format, formatProvider));
        }
    }

    /// Engine-managed class in Unity; here the four values are the object's state.
    public partial class RectOffset
    {
        private int m_Left;
        private int m_Right;
        private int m_Top;
        private int m_Bottom;

        public RectOffset() { }

        public RectOffset(int left, int right, int top, int bottom)
        {
            m_Left = left; m_Right = right; m_Top = top; m_Bottom = bottom;
        }

        public int left { get => m_Left; set => m_Left = value; }
        public int right { get => m_Right; set => m_Right = value; }
        public int top { get => m_Top; set => m_Top = value; }
        public int bottom { get => m_Bottom; set => m_Bottom = value; }
        public int horizontal => m_Left + m_Right;
        public int vertical => m_Top + m_Bottom;

        public Rect Add(Rect rect) => new Rect(rect.x - m_Left, rect.y - m_Top, rect.width + m_Left + m_Right, rect.height + m_Top + m_Bottom);
        public Rect Remove(Rect rect) => new Rect(rect.x + m_Left, rect.y + m_Top, rect.width - m_Left - m_Right, rect.height - m_Top - m_Bottom);

        public override string ToString() => ToString(null, null);
        public string ToString(string format) => ToString(format, null);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
            return string.Format("RectOffset (l:{0} r:{1} t:{2} b:{3})", left.ToString(format, formatProvider), right.ToString(format, formatProvider),
                top.ToString(format, formatProvider), bottom.ToString(format, formatProvider));
        }
    }
}
