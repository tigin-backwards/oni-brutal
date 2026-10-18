using System.Collections.Generic;

namespace UnityEngine.UI
{
    /// A component that edits its graphic's mesh after OnPopulateMesh.
    public abstract partial class BaseMeshEffect
    {
        [System.NonSerialized] Graphic cachedGraphic;

        protected BaseMeshEffect() { }

        protected Graphic graphic => cachedGraphic ??= GetComponent<Graphic>();

        protected override void OnEnable()
        {
            base.OnEnable();
            if (graphic != null) graphic.SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            if (graphic != null) graphic.SetVerticesDirty();
            base.OnDisable();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            if (graphic != null) graphic.SetVerticesDirty();
            base.OnDidApplyAnimationProperties();
        }

        public virtual void ModifyMesh(Mesh mesh)
        {
            using var vh = new VertexHelper(mesh);
            ModifyMesh(vh);
            vh.FillMesh(mesh);
        }
    }

    /// A copy of the mesh behind it, offset by effectDistance in effectColor.
    public partial class Shadow
    {
        const float MaxDistance = 600f;

        protected Shadow()
        {
            m_EffectColor = new Color(0f, 0f, 0f, 0.5f);
            m_EffectDistance = new Vector2(1f, -1f);
            m_UseGraphicAlpha = true;
        }

        public Color effectColor
        {
            get => m_EffectColor;
            set
            {
                m_EffectColor = value;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        public Vector2 effectDistance
        {
            get => m_EffectDistance;
            set
            {
                value.x = Mathf.Clamp(value.x, -MaxDistance, MaxDistance);
                value.y = Mathf.Clamp(value.y, -MaxDistance, MaxDistance);
                if (m_EffectDistance == value) return;
                m_EffectDistance = value;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        /// The copies take the graphic's vertex alpha times effectColor's.
        public bool useGraphicAlpha
        {
            get => m_UseGraphicAlpha;
            set
            {
                m_UseGraphicAlpha = value;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        /// Turns verts[start, end) into the offset, recoloured copy and appends the originals.
        protected void ApplyShadowZeroAlloc(List<UIVertex> verts, Color32 color, int start, int end, float x, float y)
        {
            int needed = verts.Count + end - start;
            if (verts.Capacity < needed) verts.Capacity = needed;
            for (int i = start; i < end; i++)
            {
                var v = verts[i];
                verts.Add(v);
                var p = v.position;
                p.x += x;
                p.y += y;
                v.position = p;
                var c = color;
                if (m_UseGraphicAlpha) c.a = (byte)(c.a * verts[i].color.a / 255);
                v.color = c;
                verts[i] = v;
            }
        }

        protected void ApplyShadow(List<UIVertex> verts, Color32 color, int start, int end, float x, float y)
            => ApplyShadowZeroAlloc(verts, color, start, end, x, y);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var stream = new List<UIVertex>();
            vh.GetUIVertexStream(stream);
            ApplyShadow(stream, effectColor, 0, stream.Count, effectDistance.x, effectDistance.y);
            vh.Clear();
            vh.AddUIVertexTriangleStream(stream);
        }
    }

    /// Four shadows, at (x, y), (x, -y), (-x, y) and (-x, -y).
    public partial class Outline
    {
        protected Outline() { }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var stream = new List<UIVertex>();
            vh.GetUIVertexStream(stream);
            int n = stream.Count;
            if (stream.Capacity < n * 5) stream.Capacity = n * 5;
            var d = effectDistance;
            // Each pass offsets the originals, which stay at the end of the stream.
            ApplyShadowZeroAlloc(stream, effectColor, 0, n, d.x, d.y);
            ApplyShadowZeroAlloc(stream, effectColor, n, 2 * n, d.x, -d.y);
            ApplyShadowZeroAlloc(stream, effectColor, 2 * n, 3 * n, -d.x, d.y);
            ApplyShadowZeroAlloc(stream, effectColor, 3 * n, 4 * n, -d.x, -d.y);
            vh.Clear();
            vh.AddUIVertexTriangleStream(stream);
        }
    }
}
