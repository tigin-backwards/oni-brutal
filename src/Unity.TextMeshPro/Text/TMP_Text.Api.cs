using System;
using UnityEngine;

namespace TMPro
{
    // Layout queries and immediate updates.
    public abstract partial class TMP_Text
    {
        // Lays the text out and regenerates its mesh now (normally the canvas rebuild does).
        public virtual void ForceMeshUpdate(bool ignoreActiveState = false, bool forceTextReparsing = false)
        {
            if (!ignoreActiveState && !isActiveAndEnabled) return;
            if (forceTextReparsing) m_ParseRequired = true;
            m_havePropertiesChanged = true;
            m_IgnoreActiveState = ignoreActiveState;
            RegenerateNow();
            m_IgnoreActiveState = false;
        }

        // Generates the layout and the mesh (through the class's virtual GenerateTextMesh).
        private protected abstract void RegenerateNow();

        // Sets the mesh of the renderer of material `index` (0: the text's own).
        public virtual void UpdateGeometry(Mesh mesh, int index) { }

        // Pushes the textInfo.meshInfo arrays back to the meshes and renderers.
        public virtual void UpdateVertexData(TMP_VertexDataUpdateFlags flags) { }

        public virtual void UpdateVertexData() => UpdateVertexData(TMP_VertexDataUpdateFlags.All);

        public virtual void SetVertices(Vector3[] vertices) { }

        // Recomputes the padding the material needs and regenerates the mesh.
        public virtual void UpdateMeshPadding()
        {
            m_Padding = 0f;
            m_havePropertiesChanged = true;
            SetVerticesDirty();
        }

        public void ClearMesh() => ClearMesh(true);

        public void ClearMesh(bool uploadGeometry)
        {
            textInfo.ClearMeshInfo(uploadGeometry);
            if (uploadGeometry) ClearRenderedGeometry();
        }

        // Empties the meshes the renderers draw.
        private protected abstract void ClearRenderedGeometry();

        public virtual void ComputeMarginSize() { }

        // ----- Preferred and rendered sizes -----

        // The size of the laid-out text: the width without wrapping, the height wrapped at the
        // rect's width; margins included.
        public Vector2 GetPreferredValues() => new(preferredWidth, preferredHeight);

        public Vector2 GetPreferredValues(float width, float height)
        {
            EnsureParsed();
            return new Vector2(PreferredWidthAt(width), PreferredHeightAt(width));
        }

        public Vector2 GetPreferredValues(string text)
        {
            string saved = m_text;
            m_text = text ?? "";
            m_ParseRequired = true;
            EnsureParsed();
            var size = new Vector2(PreferredWidthAt(float.PositiveInfinity), PreferredHeightAt(LayoutRect.width));
            m_text = saved;
            m_ParseRequired = true;
            InvalidateLayoutCache();
            return size;
        }

        public Vector2 GetPreferredValues(string text, float width, float height)
        {
            string saved = m_text;
            m_text = text ?? "";
            m_ParseRequired = true;
            EnsureParsed();
            var size = new Vector2(PreferredWidthAt(width), PreferredHeightAt(width));
            m_text = saved;
            m_ParseRequired = true;
            InvalidateLayoutCache();
            return size;
        }

        public virtual float preferredWidth
        {
            get
            {
                if (m_CachedPreferredWidth < 0f)
                {
                    EnsureParsed();
                    m_CachedPreferredWidth = PreferredWidthAt(float.PositiveInfinity);
                }
                return m_CachedPreferredWidth;
            }
        }

        public virtual float preferredHeight
        {
            get
            {
                float width = LayoutRect.width;
                if (m_CachedPreferredHeight < 0f || m_CachedPreferredHeightWidth != width)
                {
                    EnsureParsed();
                    m_CachedPreferredHeight = PreferredHeightAt(width);
                    m_CachedPreferredHeightWidth = width;
                }
                return m_CachedPreferredHeight;
            }
        }

        float PreferredSize => m_enableAutoSizing ? m_fontSizeMax : (m_fontSizeBase > 0f ? m_fontSizeBase : m_fontSize);

        // The widest line (pen advance) when wrapped at `width` (unwrapped when infinite).
        float PreferredWidthAt(float width)
        {
            if (m_fontAsset == null) return 0f;
            float rectWidth = float.IsInfinity(width) ? float.PositiveInfinity : width;
            LayoutUnadjusted(rectWidth);
            double max = 0.0;
            for (int k = 0; k < s_LineCount; k++) max = Math.Max(max, (double)s_Lines[k].MarginLeft + s_Lines[k].StartX + s_Lines[k].Length);
            return RoundUp(max + m_margin.x + m_margin.z);
        }

        // Preferred sizes are rounded up to hundredths (layouts do not change with the last
        // digits of a float). The sums they come from are taken in double precision, as the
        // shipped package's are.
        static float RoundUp(double value) => (float)(Math.Ceiling(value * 100.0) / 100.0);

        // A layout at the preferred size without auto-size's adjustments (which stay as they are).
        void LayoutUnadjusted(float width)
        {
            float lineAdjust = m_LineSpacingAdjustment, widthAdjust = m_CharWidthAdjustment;
            m_LineSpacingAdjustment = 0f;
            m_CharWidthAdjustment = 0f;
            Layout(PreferredSize, width, float.PositiveInfinity);
            m_LineSpacingAdjustment = lineAdjust;
            m_CharWidthAdjustment = widthAdjust;
        }

        // From the first line's ascender to the last line's descender.
        float PreferredHeightAt(float width)
        {
            if (m_fontAsset == null) return 0f;
            LayoutUnadjusted(width);
            if (s_LineCount == 0) return RoundUp(m_margin.y + m_margin.w);
            ref var last = ref s_Lines[s_LineCount - 1];
            double height = -((double)last.Baseline + last.Descender);
            return RoundUp(height + m_margin.y + m_margin.w);
        }

        public virtual float flexibleWidth => -1f;

        public virtual float flexibleHeight => -1f;

        public virtual float minWidth => 0f;

        public virtual float minHeight => 0f;

        public virtual float maxWidth => 10000f;

        public virtual float maxHeight => 10000f;

        public virtual int layoutPriority => 0;

        // The size of the visible characters of the last layout.
        public virtual float renderedWidth => GetRenderedValues(true).x;

        public virtual float renderedHeight => GetRenderedValues(true).y;

        public Vector2 GetRenderedValues() => GetRenderedValues(true);

        public Vector2 GetRenderedValues(bool onlyVisibleCharacters)
        {
            if (m_textInfo == null || m_textInfo.characterCount == 0) return Vector2.zero;
            var e = onlyVisibleCharacters ? m_TextExtents : AllExtents();
            if (e.max.x < e.min.x) return Vector2.zero;
            return new Vector2(e.max.x - e.min.x, e.max.y - e.min.y);
        }

        Extents AllExtents()
        {
            var e = Extents.uninitialized;
            var chars = m_textInfo.characterInfo;
            for (int i = 0; i < m_textInfo.characterCount; i++)
            {
                e.min.x = Mathf.Min(e.min.x, chars[i].origin);
                e.max.x = Mathf.Max(e.max.x, chars[i].xAdvance);
                e.min.y = Mathf.Min(e.min.y, chars[i].descender);
                e.max.y = Mathf.Max(e.max.y, chars[i].ascender);
            }
            return e;
        }

        private protected void RaisePreRender() => OnPreRenderText?.Invoke(textInfo);

        // Lays out `text` (replacing the current text) and returns the text info.
        public TMP_TextInfo GetTextInfo(string text)
        {
            SetText(text);
            ForceMeshUpdate(true, true);
            return textInfo;
        }
    }
}
