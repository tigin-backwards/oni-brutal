using System;
using UnityEngine;
using UnityEngine.TextCore;

namespace TMPro
{
    // Geometry: a quad per visible character in the mesh buffer of its material, plus the
    // underline, strikethrough and highlight quads.
    public abstract partial class TMP_Text
    {
        static readonly Vector2 s_Uv2BL = new(0f, 0f), s_Uv2TL = new(0f, 1f), s_Uv2TR = new(1f, 1f), s_Uv2BR = new(1f, 0f);

        // The ratio of the text's scale to its canvas's, which the SDF scale of the vertices
        // includes (1 for a text not scaled relative to its canvas).
        private protected abstract float SdfScaleFactor();

        // Sends the mesh buffers of the first `materialCount` materials to the renderers.
        private protected abstract void UploadGeometry(int materialCount);

        // The mesh of a material slot (0: the text's own; others: its sub-meshes).
        private protected abstract Mesh MeshForMaterial(int index);

        // Writes the quads of the laid-out text into textInfo.meshInfo.
        private protected void BuildGeometry()
        {
            var info = textInfo;
            var parsed = m_Parsed;
            int materialCount = Mathf.Max(1, parsed.Materials.Count);
            var chars = info.characterInfo;
            int count = info.characterCount;

            // Quads per material: visible characters and their decorations.
            Span<int> needed = materialCount <= 32 ? stackalloc int[materialCount] : new int[materialCount];
            for (int i = 0; i < count; i++)
            {
                if (!chars[i].isVisible) continue;
                needed[chars[i].materialReferenceIndex]++;
            }
            CountDecorations(needed);

            if (info.meshInfo.Length < materialCount) Array.Resize(ref info.meshInfo, materialCount);
            for (int m = 0; m < materialCount; m++)
            {
                ref var mi = ref info.meshInfo[m];
                var mesh = MeshForMaterial(m);
                // A buffer holds the quads and one more: allocated to that size, it grows to the
                // next power of two when a later layout needs more.
                int capacity = needed[m] + 1;
                if (mi.vertices == null || mi.mesh != mesh || mi.QuadCapacity == 0) mi = new TMP_MeshInfo(mesh, capacity);
                else if (mi.QuadCapacity < capacity) mi.ResizeMeshInfo(Mathf.NextPowerOfTwo(capacity));
                mi.material = m < parsed.Materials.Count ? parsed.Materials[m].Material : m_sharedMaterial;
                mi.vertexCount = 0;
            }

            float sdfFactor = SdfScaleFactor();
            for (int i = 0; i < count; i++)
            {
                ref var ci = ref chars[i];
                if (!ci.isVisible) continue;
                ref var p = ref s_Placements[i];
                ref var el = ref parsed.Elements[p.Element];
                ref var mi = ref info.meshInfo[ci.materialReferenceIndex];
                int v = mi.vertexCount;
                ci.vertexIndex = v;
                if (el.Kind == ElementKind.Sprite) WriteSprite(ref ci, ref p, ref el, ref mi, v);
                else WriteCharacter(ref ci, ref p, ref el, ref mi, v, sdfFactor);
                mi.vertexCount = v + 4;
            }
            WriteDecorations(sdfFactor);

            var bounds = Extents.uninitialized;
            for (int m = 0; m < materialCount; m++)
            {
                ref var mi = ref info.meshInfo[m];
                mi.ClearUnusedVertices(mi.vertexCount);
                for (int k = 0; k < mi.vertexCount; k++)
                {
                    var pos = mi.vertices[k];
                    bounds.min.x = Mathf.Min(bounds.min.x, pos.x);
                    bounds.min.y = Mathf.Min(bounds.min.y, pos.y);
                    bounds.max.x = Mathf.Max(bounds.max.x, pos.x);
                    bounds.max.y = Mathf.Max(bounds.max.y, pos.y);
                }
            }
            if (bounds.max.x < bounds.min.x) m_MeshBounds = default;
            else m_MeshBounds = new Bounds(new Vector3((bounds.min.x + bounds.max.x) * 0.5f, (bounds.min.y + bounds.max.y) * 0.5f, 0f), new Vector3(bounds.max.x - bounds.min.x, bounds.max.y - bounds.min.y, 0f));
            if (m_geometrySortingOrder == VertexSortingOrder.Reverse)
                for (int m = 0; m < materialCount; m++) info.meshInfo[m].SortGeometry(VertexSortingOrder.Reverse);
        }

        // The texels a synthesized bold glyph's edge moves out by: the shader's bold weight (a
        // quarter of it in field units) over the gradient scale, as the material's face ratio
        // scales it.
        float BoldPadding(TMP_FontAsset font)
        {
            var material = m_sharedMaterial;
            float gradient = material != null && material.HasProperty(ShaderUtilities.ID_GradientScale) ? material.GetFloat(ShaderUtilities.ID_GradientScale) : font.atlasPadding + 1;
            float ratio = material != null && material.HasProperty(ShaderUtilities.ID_ScaleRatio_A) ? material.GetFloat(ShaderUtilities.ID_ScaleRatio_A) : 1f;
            return font.boldStyle / 4f * gradient * ratio;
        }

        void WriteCharacter(ref TMP_CharacterInfo ci, ref Placement p, ref ParsedElement el, ref TMP_MeshInfo mi, int v, float sdfFactor)
        {
            var glyph = el.Element.glyph;
            var font = ci.fontAsset;
            var gm = glyph.metrics;
            var gr = glyph.glyphRect;
            // Synthesized bold widens the quad by the weight's padding; its texture rect grows
            // by it on every side.
            float pad = m_Padding;
            float stylePad = p.Bold ? BoldPadding(font) : 0f;
            float s = p.Scale;
            float xs = s * p.XScale;
            float origin = ci.origin + p.MonoOffset;
            float baseline = ci.baseLine + p.KernY;
            float x0 = origin + (gm.horizontalBearingX - pad - stylePad) * xs;
            float x1 = origin + (gm.horizontalBearingX + gm.width + pad + stylePad) * xs;
            float y1 = baseline + (gm.horizontalBearingY + pad) * s;
            float y0 = baseline + (gm.horizontalBearingY - gm.height - pad) * s;
            float shearTop = 0f, shearBottom = 0f;
            if (p.Italic && !el.AlternateTypeface)
            {
                float shear = ItalicShear(font);
                shearTop = shear * (y1 - baseline);
                shearBottom = shear * (y0 - baseline);
            }
            mi.vertices[v] = new Vector3(x0 + shearBottom, y0, 0f);
            mi.vertices[v + 1] = new Vector3(x0 + shearTop, y1, 0f);
            mi.vertices[v + 2] = new Vector3(x1 + shearTop, y1, 0f);
            mi.vertices[v + 3] = new Vector3(x1 + shearBottom, y0, 0f);

            float aw = font.atlasWidth, ah = font.atlasHeight;
            var atlas = glyph.atlasIndex > 0 && font.atlasTextures != null && glyph.atlasIndex < font.atlasTextures.Length ? font.atlasTextures[glyph.atlasIndex] : null;
            if (atlas != null) { aw = atlas.width; ah = atlas.height; }
            float uvPad = pad + stylePad;
            float u0 = (gr.x - uvPad) / aw, u1 = (gr.x + gr.width + uvPad) / aw;
            float v0 = (gr.y - uvPad) / ah, v1 = (gr.y + gr.height + uvPad) / ah;
            float w = s * sdfFactor * (p.Bold ? -1f : 1f);
            mi.uvs0[v] = new Vector4(u0, v0, 0f, w);
            mi.uvs0[v + 1] = new Vector4(u0, v1, 0f, w);
            mi.uvs0[v + 2] = new Vector4(u1, v1, 0f, w);
            mi.uvs0[v + 3] = new Vector4(u1, v0, 0f, w);
            WriteMapping(ref ci, ref mi, v, x0, x1);
            WriteColors(ref ci, ref mi, v);
            ci.vertex_BL = new TMP_Vertex { position = mi.vertices[v], uv = mi.uvs0[v], uv2 = mi.uvs2[v], color = mi.colors32[v] };
            ci.vertex_TL = new TMP_Vertex { position = mi.vertices[v + 1], uv = mi.uvs0[v + 1], uv2 = mi.uvs2[v + 1], color = mi.colors32[v + 1] };
            ci.vertex_TR = new TMP_Vertex { position = mi.vertices[v + 2], uv = mi.uvs0[v + 2], uv2 = mi.uvs2[v + 2], color = mi.colors32[v + 2] };
            ci.vertex_BR = new TMP_Vertex { position = mi.vertices[v + 3], uv = mi.uvs0[v + 3], uv2 = mi.uvs2[v + 3], color = mi.colors32[v + 3] };
        }

        void WriteSprite(ref TMP_CharacterInfo ci, ref Placement p, ref ParsedElement el, ref TMP_MeshInfo mi, int v)
        {
            var sprite = (TMP_SpriteCharacter)el.Element;
            var glyph = sprite.glyph;
            var asset = (TMP_SpriteAsset)el.Asset;
            var gm = glyph.metrics;
            var gr = glyph.glyphRect;
            float s = p.Scale;
            float xs = s * p.XScale;
            float origin = ci.origin + p.MonoOffset;
            float baseline = ci.baseLine;
            float x0 = origin + gm.horizontalBearingX * xs;
            float x1 = origin + (gm.horizontalBearingX + gm.width) * xs;
            float y1 = baseline + gm.horizontalBearingY * s;
            float y0 = baseline + (gm.horizontalBearingY - gm.height) * s;
            mi.vertices[v] = new Vector3(x0, y0, 0f);
            mi.vertices[v + 1] = new Vector3(x0, y1, 0f);
            mi.vertices[v + 2] = new Vector3(x1, y1, 0f);
            mi.vertices[v + 3] = new Vector3(x1, y0, 0f);
            var sheet = asset.spriteSheet;
            float aw = sheet != null ? sheet.width : 1f, ah = sheet != null ? sheet.height : 1f;
            float u0 = gr.x / aw, u1 = (gr.x + gr.width) / aw, v0 = gr.y / ah, v1 = (gr.y + gr.height) / ah;
            mi.uvs0[v] = new Vector4(u0, v0, 0f, 0f);
            mi.uvs0[v + 1] = new Vector4(u0, v1, 0f, 0f);
            mi.uvs0[v + 2] = new Vector4(u1, v1, 0f, 0f);
            mi.uvs0[v + 3] = new Vector4(u1, v0, 0f, 0f);
            WriteMapping(ref ci, ref mi, v, x0, x1);
            WriteColors(ref ci, ref mi, v);
            ci.vertex_BL = new TMP_Vertex { position = mi.vertices[v], uv = mi.uvs0[v], uv2 = mi.uvs2[v], color = mi.colors32[v] };
            ci.vertex_TL = new TMP_Vertex { position = mi.vertices[v + 1], uv = mi.uvs0[v + 1], uv2 = mi.uvs2[v + 1], color = mi.colors32[v + 1] };
            ci.vertex_TR = new TMP_Vertex { position = mi.vertices[v + 2], uv = mi.uvs0[v + 2], uv2 = mi.uvs2[v + 2], color = mi.colors32[v + 2] };
            ci.vertex_BR = new TMP_Vertex { position = mi.vertices[v + 3], uv = mi.uvs0[v + 3], uv2 = mi.uvs2[v + 3], color = mi.colors32[v + 3] };
        }

        // uv1: the face and outline texture coordinates, 0 to 1 over the character, or over the
        // line, the paragraph (text) or matching the aspect.
        void WriteMapping(ref TMP_CharacterInfo ci, ref TMP_MeshInfo mi, int v, float x0, float x1)
        {
            float uLeft = 0f, uRight = 1f, vBottom = 0f, vTop = 1f;
            var info = textInfo;
            switch (m_horizontalMapping)
            {
                case TextureMappingOptions.Line:
                {
                    var ext = info.lineInfo[ci.lineNumber].lineExtents;
                    float wLine = ext.max.x - ext.min.x;
                    if (wLine > 0f)
                    {
                        uLeft = (x0 - ext.min.x) / wLine + m_uvLineOffset * ci.lineNumber;
                        uRight = (x1 - ext.min.x) / wLine + m_uvLineOffset * ci.lineNumber;
                    }
                    break;
                }
                case TextureMappingOptions.Paragraph:
                {
                    var ext = m_TextExtents;
                    float wText = ext.max.x - ext.min.x;
                    if (wText > 0f)
                    {
                        uLeft = (x0 - ext.min.x) / wText + m_uvLineOffset * ci.lineNumber;
                        uRight = (x1 - ext.min.x) / wText + m_uvLineOffset * ci.lineNumber;
                    }
                    break;
                }
            }
            switch (m_verticalMapping)
            {
                case TextureMappingOptions.Line:
                {
                    ref var li = ref info.lineInfo[ci.lineNumber];
                    float h = li.ascender - li.descender;
                    if (h > 0f)
                    {
                        vBottom = (ci.bottomLeft.y - li.descender) / h;
                        vTop = (ci.topLeft.y - li.descender) / h;
                    }
                    break;
                }
                case TextureMappingOptions.Paragraph:
                {
                    var ext = m_TextExtents;
                    float h = ext.max.y - ext.min.y;
                    if (h > 0f)
                    {
                        vBottom = (ci.bottomLeft.y - ext.min.y) / h;
                        vTop = (ci.topLeft.y - ext.min.y) / h;
                    }
                    break;
                }
                case TextureMappingOptions.MatchAspect:
                {
                    float wq = uRight - uLeft;
                    float aspect = ci.aspectRatio > 0f ? ci.aspectRatio : 1f;
                    vBottom = 0f;
                    vTop = wq / aspect;
                    break;
                }
            }
            mi.uvs2[v] = new Vector2(uLeft, vBottom);
            mi.uvs2[v + 1] = new Vector2(uLeft, vTop);
            mi.uvs2[v + 2] = new Vector2(uRight, vTop);
            mi.uvs2[v + 3] = new Vector2(uRight, vBottom);
        }

        void WriteColors(ref TMP_CharacterInfo ci, ref TMP_MeshInfo mi, int v)
        {
            Color32 c = ci.color;
            if (m_enableVertexGradient && ci.elementType == TMP_TextElementType.Character)
            {
                var g = m_fontColorGradientPreset != null
                    ? new VertexGradient(m_fontColorGradientPreset.topLeft, m_fontColorGradientPreset.topRight, m_fontColorGradientPreset.bottomLeft, m_fontColorGradientPreset.bottomRight)
                    : m_fontColorGradient;
                mi.colors32[v] = Tint(g.bottomLeft, c);
                mi.colors32[v + 1] = Tint(g.topLeft, c);
                mi.colors32[v + 2] = Tint(g.topRight, c);
                mi.colors32[v + 3] = Tint(g.bottomRight, c);
                return;
            }
            mi.colors32[v] = c;
            mi.colors32[v + 1] = c;
            mi.colors32[v + 2] = c;
            mi.colors32[v + 3] = c;
        }

        static Color32 Tint(Color gradient, Color32 c) => new(
            (byte)(gradient.r * c.r + 0.5f), (byte)(gradient.g * c.g + 0.5f), (byte)(gradient.b * c.b + 0.5f), (byte)(gradient.a * c.a + 0.5f));

        // ----- Underline, strikethrough and highlight -----

        // Runs of consecutive visible characters on a line sharing a decoration.
        struct Run
        {
            public int First, Last;
            public int Material;
            public byte Kind; // 0 underline, 1 strikethrough, 2 highlight
        }

        static Run[] s_Runs = new Run[8];
        static int s_RunCount;

        void FindRuns()
        {
            s_RunCount = 0;
            var info = textInfo;
            var chars = info.characterInfo;
            for (byte kind = 0; kind < 3; kind++)
            {
                var flag = kind == 0 ? FontStyles.Underline : kind == 1 ? FontStyles.Strikethrough : FontStyles.Highlight;
                int start = -1;
                for (int i = 0; i <= info.characterCount; i++)
                {
                    bool on = i < info.characterCount && (chars[i].style & flag) != 0 && IsDecorated(i)
                        && (start < 0 || (chars[i].lineNumber == chars[start].lineNumber && chars[i].materialReferenceIndex == chars[start].materialReferenceIndex));
                    if (on && start < 0) start = i;
                    if (!on && start >= 0)
                    {
                        int last = i - 1;
                        // Trailing whitespace is not decorated.
                        while (last > start && !chars[last].isVisible) last--;
                        if (chars[last].isVisible || last > start)
                        {
                            if (s_RunCount == s_Runs.Length) Array.Resize(ref s_Runs, s_Runs.Length * 2);
                            s_Runs[s_RunCount++] = new Run { First = start, Last = last, Material = chars[start].materialReferenceIndex, Kind = kind };
                        }
                        start = -1;
                        if (i < info.characterCount && (chars[i].style & flag) != 0 && IsDecorated(i)) start = i;
                    }
                }
            }
        }

        bool IsDecorated(int i)
        {
            var info = textInfo;
            ref var ci = ref info.characterInfo[i];
            if (ci.elementType == TMP_TextElementType.Sprite) return ci.isVisible;
            if (ci.isVisible) return true;
            // Spaces inside a decorated run are decorated too.
            return s_Placements[i].Whitespace && !s_Placements[i].LineBreak && ci.lineNumber >= 0 && i < info.characterCount && LineShown(ci.lineNumber);
        }

        bool LineShown(int line) => line < m_FirstHiddenLine;

        void CountDecorations(Span<int> needed)
        {
            FindRuns();
            for (int r = 0; r < s_RunCount; r++) needed[s_Runs[r].Material] += s_Runs[r].Kind == 2 ? 1 : 3;
        }

        void WriteDecorations(float sdfFactor)
        {
            var info = textInfo;
            var chars = info.characterInfo;
            for (int r = 0; r < s_RunCount; r++)
            {
                ref var run = ref s_Runs[r];
                ref var first = ref chars[run.First];
                ref var last = ref chars[run.Last];
                var font = first.fontAsset ?? m_fontAsset;
                ref var mi = ref info.meshInfo[run.Material];
                var face = font.faceInfo;
                float s = s_Placements[run.First].FontScale;
                float baseline = info.lineInfo[first.lineNumber].baseline;
                float x0 = first.origin, x1 = last.xAdvance;
                if (run.Kind == 2)
                {
                    // Highlight: a quad behind the characters, over the line's ascender to descender.
                    var hs = first.highlightState;
                    float top = info.lineInfo[first.lineNumber].ascender + hs.padding.top;
                    float bottom = info.lineInfo[first.lineNumber].descender - hs.padding.bottom;
                    WriteSolidQuad(ref mi, font, x0 - hs.padding.left, bottom, x1 + hs.padding.right, top, hs.color, sdfFactor * s);
                    continue;
                }
                float offset = run.Kind == 0 ? face.underlineOffset : face.strikethroughOffset;
                float thickness = run.Kind == 0 ? face.underlineThickness : face.strikethroughThickness;
                if (run.Kind == 1 && thickness == 0f) thickness = face.underlineThickness;
                var color = run.Kind == 0 ? first.underlineColor : first.strikethroughColor;
                float yCenter = baseline + offset * s;
                WriteLineQuads(ref mi, font, x0, x1, yCenter, thickness * s, color, s, sdfFactor);
            }
        }

        // A decoration line drawn with the font's underline glyph ('_'): its two ends keep their
        // proportions and the middle stretches.
        void WriteLineQuads(ref TMP_MeshInfo mi, TMP_FontAsset font, float x0, float x1, float yCenter, float thickness, Color32 color, float s, float sdfFactor)
        {
            if (!font.TryGetLocalCharacter('_', out var underscore)) return;
            var glyph = underscore.glyph;
            var gr = glyph.glyphRect;
            float pad = m_Padding;
            float aw = font.atlasWidth, ah = font.atlasHeight;
            float h = (glyph.metrics.height + pad * 2f) * s;
            float y1 = yCenter + thickness * 0.5f + pad * s, y0 = y1 - h;
            float cap = (glyph.metrics.height * 0.5f + pad) * s;
            if (x1 - x0 < cap * 2f) cap = (x1 - x0) * 0.5f;
            float u0 = (gr.x - pad) / aw, u3 = (gr.x + gr.width + pad) / aw;
            float uMid = (gr.x + gr.width * 0.5f) / aw;
            float v0 = (gr.y - pad) / ah, v1 = (gr.y + gr.height + pad) / ah;
            float w = s * sdfFactor;
            WriteQuad(ref mi, x0 - pad * s, y0, x0 - pad * s + cap, y1, u0, v0, uMid, v1, w, color, 0f, 0.5f);
            WriteQuad(ref mi, x0 - pad * s + cap, y0, x1 + pad * s - cap, y1, uMid, v0, uMid, v1, w, color, 0.5f, 0.5f);
            WriteQuad(ref mi, x1 + pad * s - cap, y0, x1 + pad * s, y1, uMid, v0, u3, v1, w, color, 0.5f, 1f);
        }

        void WriteSolidQuad(ref TMP_MeshInfo mi, TMP_FontAsset font, float x0, float y0, float x1, float y1, Color32 color, float w)
        {
            // The centre of the underline glyph is solid face.
            float u = 0f, v = 0f;
            if (font.TryGetLocalCharacter('_', out var underscore))
            {
                var gr = underscore.glyph.glyphRect;
                u = (gr.x + gr.width * 0.5f) / font.atlasWidth;
                v = (gr.y + gr.height * 0.5f) / font.atlasHeight;
            }
            WriteQuad(ref mi, x0, y0, x1, y1, u, v, u, v, w, color, 0f, 1f);
        }

        static void WriteQuad(ref TMP_MeshInfo mi, float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, float w, Color32 color, float uv2Left, float uv2Right)
        {
            int v = mi.vertexCount;
            if (v + 4 > mi.vertices.Length) return;
            mi.vertices[v] = new Vector3(x0, y0, 0f);
            mi.vertices[v + 1] = new Vector3(x0, y1, 0f);
            mi.vertices[v + 2] = new Vector3(x1, y1, 0f);
            mi.vertices[v + 3] = new Vector3(x1, y0, 0f);
            mi.uvs0[v] = new Vector4(u0, v0, 0f, w);
            mi.uvs0[v + 1] = new Vector4(u0, v1, 0f, w);
            mi.uvs0[v + 2] = new Vector4(u1, v1, 0f, w);
            mi.uvs0[v + 3] = new Vector4(u1, v0, 0f, w);
            mi.uvs2[v] = new Vector2(uv2Left, 0f);
            mi.uvs2[v + 1] = new Vector2(uv2Left, 1f);
            mi.uvs2[v + 2] = new Vector2(uv2Right, 1f);
            mi.uvs2[v + 3] = new Vector2(uv2Right, 0f);
            mi.colors32[v] = mi.colors32[v + 1] = mi.colors32[v + 2] = mi.colors32[v + 3] = color;
            mi.vertexCount = v + 4;
        }
    }
}
