using System;

namespace UnityEngine
{
    /// A glyph of a legacy font as Font.GetCharacterInfo gives it (docs/cleanroom/ugui.md,
    /// "Glyph metrics"). The box is held in `vert` (x the left, y the top, a negative height
    /// down from it) and the texture rect in `uv`; the properties read and write them. For a
    /// dynamic font the box is the rendered bitmap grown by the font's character padding, and
    /// `width` (the advance) is a whole number of pixels.
    public partial struct CharacterInfo
    {
        /// The character.
        public int index;

        /// The texture rect: x, y at the bottom-left corner (or, flipped, the corner the
        /// glyph's bottom-left is stored at).
        [Obsolete("CharacterInfo.uv is deprecated; use uvBottomLeft, uvBottomRight, uvTopRight or uvTopLeft.")]
        public Rect uv;

        /// The glyph box relative to the pen on the baseline: x the left edge, y the top edge,
        /// height negative (downwards).
        [Obsolete("CharacterInfo.vert is deprecated; use minX, maxX, minY, maxY.")]
        public Rect vert;

        /// The advance.
        [Obsolete("CharacterInfo.width is deprecated; use advance.")]
        public float width;

        /// The pixel size asked for (0: the font's own size).
        public int size;

        public FontStyle style;

        /// Whether the glyph is stored rotated in the texture.
        [Obsolete("CharacterInfo.flipped is deprecated; use uvBottomLeft, uvBottomRight, uvTopRight or uvTopLeft.")]
        public bool flipped;

#pragma warning disable CS0618 // the properties are the obsolete fields' view
        public int advance
        {
            get => (int)Math.Round(width, MidpointRounding.AwayFromZero);
            set => width = value;
        }

        public int glyphWidth
        {
            get => (int)vert.width;
            set => vert.width = value;
        }

        public int glyphHeight
        {
            get => (int)-vert.height;
            set
            {
                float top = vert.y;
                vert.height = -value;
                vert.y = top;
            }
        }

        public int bearing
        {
            get => (int)vert.x;
            set => vert.x = value;
        }

        public int minX
        {
            get => (int)vert.x;
            set
            {
                float right = vert.x + vert.width;
                vert.x = value;
                vert.width = right - value;
            }
        }

        public int maxX
        {
            get => (int)(vert.x + vert.width);
            set => vert.width = value - vert.x;
        }

        public int minY
        {
            get => (int)(vert.y + vert.height);
            set => vert.height = value - vert.y;
        }

        public int maxY
        {
            get => (int)vert.y;
            set
            {
                float bottom = vert.y + vert.height;
                vert.y = value;
                vert.height = bottom - value;
            }
        }

        // uv's x/y corner is the bottom-left; its opposite corner the top-right. A flipped glyph
        // is stored rotated: its top-left is at (xMax, y) and its bottom-right at (x, yMax).
        Vector2 UvMin
        {
            get => new(uv.x, uv.y);
            set { float xMax = uv.x + uv.width, yMax = uv.y + uv.height; uv.x = value.x; uv.y = value.y; uv.width = xMax - value.x; uv.height = yMax - value.y; }
        }

        Vector2 UvMax
        {
            get => new(uv.x + uv.width, uv.y + uv.height);
            set { uv.width = value.x - uv.x; uv.height = value.y - uv.y; }
        }

        Vector2 UvMinXMaxY
        {
            get => new(uv.x, uv.y + uv.height);
            set { float xMax = uv.x + uv.width; uv.x = value.x; uv.width = xMax - value.x; uv.height = value.y - uv.y; }
        }

        Vector2 UvMaxXMinY
        {
            get => new(uv.x + uv.width, uv.y);
            set { float yMax = uv.y + uv.height; uv.y = value.y; uv.height = yMax - value.y; uv.width = value.x - uv.x; }
        }

        public Vector2 uvBottomLeft
        {
            get => UvMin;
            set => UvMin = value;
        }

        public Vector2 uvTopRight
        {
            get => UvMax;
            set => UvMax = value;
        }

        public Vector2 uvTopLeft
        {
            get => flipped ? UvMaxXMinY : UvMinXMaxY;
            set { if (flipped) UvMaxXMinY = value; else UvMinXMaxY = value; }
        }

        public Vector2 uvBottomRight
        {
            get => flipped ? UvMinXMaxY : UvMaxXMinY;
            set { if (flipped) UvMinXMaxY = value; else UvMaxXMinY = value; }
        }
#pragma warning restore CS0618
    }
}
