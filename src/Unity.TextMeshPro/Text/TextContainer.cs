using UnityEngine;

namespace TMPro
{
    // The older way to size a text's area (obsolete, still referenced): an anchor position that
    // sets the rect transform's pivot, and margins.
    public partial class TextContainer
    {
        RectTransform m_RectTransform;

        public RectTransform rectTransform
        {
            get
            {
                if (m_RectTransform == null) m_RectTransform = GetComponent<RectTransform>();
                return m_RectTransform;
            }
        }

        public TextContainerAnchors anchorPosition
        {
            get => m_anchorPosition;
            set
            {
                m_anchorPosition = value;
                if (value == TextContainerAnchors.Custom) return;
                m_pivot = PivotOf(value);
                rectTransform.pivot = m_pivot;
            }
        }

        public Vector2 pivot
        {
            get => m_pivot;
            set
            {
                m_pivot = value;
                m_anchorPosition = AnchorOf(value);
                rectTransform.pivot = value;
            }
        }

        public Rect rect
        {
            get => m_rect;
            set
            {
                m_rect = value;
                rectTransform.sizeDelta = value.size;
            }
        }

        public Vector2 size
        {
            get => m_rect.size;
            set
            {
                m_rect.size = value;
                rectTransform.sizeDelta = value;
            }
        }

        public float width
        {
            get => m_rect.width;
            set => size = new Vector2(value, m_rect.height);
        }

        public float height
        {
            get => m_rect.height;
            set => size = new Vector2(m_rect.width, value);
        }

        public Vector4 margins
        {
            get => m_margins;
            set => m_margins = value;
        }

        static Vector2 PivotOf(TextContainerAnchors anchor) => anchor switch
        {
            TextContainerAnchors.TopLeft => new Vector2(0f, 1f),
            TextContainerAnchors.Top => new Vector2(0.5f, 1f),
            TextContainerAnchors.TopRight => new Vector2(1f, 1f),
            TextContainerAnchors.Left => new Vector2(0f, 0.5f),
            TextContainerAnchors.Middle => new Vector2(0.5f, 0.5f),
            TextContainerAnchors.Right => new Vector2(1f, 0.5f),
            TextContainerAnchors.BottomLeft => new Vector2(0f, 0f),
            TextContainerAnchors.Bottom => new Vector2(0.5f, 0f),
            TextContainerAnchors.BottomRight => new Vector2(1f, 0f),
            _ => new Vector2(0.5f, 0.5f),
        };

        static TextContainerAnchors AnchorOf(Vector2 pivot)
        {
            for (var a = TextContainerAnchors.TopLeft; a <= TextContainerAnchors.BottomRight; a++)
                if (PivotOf(a) == pivot) return a;
            return TextContainerAnchors.Custom;
        }
    }
}
