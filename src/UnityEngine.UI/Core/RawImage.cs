namespace UnityEngine.UI
{
    /// Draws a texture (white without one) over the rect, with texture coordinates from uvRect.
    public partial class RawImage
    {
        protected RawImage()
        {
            m_UVRect = new Rect(0f, 0f, 1f, 1f);
            useLegacyMeshGeneration = false;
        }

        public override Texture mainTexture
        {
            get
            {
                if (m_Texture != null) return m_Texture;
                var m = material;
                if (m != null && m.mainTexture != null) return m.mainTexture;
                return s_WhiteTexture != null ? s_WhiteTexture : (s_WhiteTexture = Texture2D.whiteTexture);
            }
        }

        public Texture texture
        {
            get => m_Texture;
            set
            {
                if (m_Texture == value) return;
                m_Texture = value;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public Rect uvRect
        {
            get => m_UVRect;
            set
            {
                if (m_UVRect == value) return;
                m_UVRect = value;
                SetVerticesDirty();
            }
        }

        /// The texture's size times uvRect's size, with the anchors together.
        public override void SetNativeSize()
        {
            var t = mainTexture;
            if (t == null) return;
            int w = Mathf.RoundToInt(t.width * uvRect.width);
            int h = Mathf.RoundToInt(t.height * uvRect.height);
            rectTransform.anchorMax = rectTransform.anchorMin;
            rectTransform.sizeDelta = new Vector2(w, h);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            var t = mainTexture;
            vh.Clear();
            if (t == null) return;
            var r = GetPixelAdjustedRect();
            // Texture coordinates address the used part of a padded texture.
            float sx = t.width * t.texelSize.x, sy = t.height * t.texelSize.y;
            var uv = m_UVRect;
            Color32 c = color;
            vh.AddVert(new Vector3(r.xMin, r.yMin), c, new Vector2(uv.xMin * sx, uv.yMin * sy));
            vh.AddVert(new Vector3(r.xMin, r.yMax), c, new Vector2(uv.xMin * sx, uv.yMax * sy));
            vh.AddVert(new Vector3(r.xMax, r.yMax), c, new Vector2(uv.xMax * sx, uv.yMax * sy));
            vh.AddVert(new Vector3(r.xMax, r.yMin), c, new Vector2(uv.xMax * sx, uv.yMin * sy));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
