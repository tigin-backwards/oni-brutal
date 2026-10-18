using System;
using System.Collections.Generic;

namespace UnityEngine.UI
{
    /// Builds a UI mesh vertex by vertex: positions, colours, four texture coordinate sets,
    /// normals and tangents, and triangle indices.
    public partial class VertexHelper
    {
        static readonly Vector3 DefaultNormal = Vector3.back;
        static readonly Vector4 DefaultTangent = new(1f, 0f, 0f, -1f);

        List<Vector3> positions;
        List<Color32> colors;
        List<Vector4> uv0, uv1, uv2, uv3;
        List<Vector3> normals;
        List<Vector4> tangents;
        List<int> indices;

        public VertexHelper() { }

        /// Starts from the mesh's vertices and triangles.
        public VertexHelper(Mesh m)
        {
            Allocate();
            positions.AddRange(m.vertices);
            colors.AddRange(m.colors32);
            var uvs = new List<Vector4>();
            m.GetUVs(0, uvs); uv0.AddRange(uvs);
            m.GetUVs(1, uvs); uv1.AddRange(uvs);
            m.GetUVs(2, uvs); uv2.AddRange(uvs);
            m.GetUVs(3, uvs); uv3.AddRange(uvs);
            normals.AddRange(m.normals);
            tangents.AddRange(m.tangents);
            indices.AddRange(m.GetIndices(0));
        }

        void Allocate()
        {
            if (positions != null) return;
            positions = new List<Vector3>();
            colors = new List<Color32>();
            uv0 = new List<Vector4>();
            uv1 = new List<Vector4>();
            uv2 = new List<Vector4>();
            uv3 = new List<Vector4>();
            normals = new List<Vector3>();
            tangents = new List<Vector4>();
            indices = new List<int>();
        }

        /// Releases the buffers; the helper allocates new ones when used again.
        public void Dispose()
        {
            positions = null;
            colors = null;
            uv0 = uv1 = uv2 = uv3 = null;
            normals = null;
            tangents = null;
            indices = null;
        }

        public void Clear()
        {
            if (positions == null) return;
            positions.Clear();
            colors.Clear();
            uv0.Clear();
            uv1.Clear();
            uv2.Clear();
            uv3.Clear();
            normals.Clear();
            tangents.Clear();
            indices.Clear();
        }

        public int currentVertCount => positions?.Count ?? 0;

        public int currentIndexCount => indices?.Count ?? 0;

        public void PopulateUIVertex(ref UIVertex vertex, int i)
        {
            Allocate();
            vertex.position = positions[i];
            vertex.color = colors[i];
            vertex.uv0 = uv0[i];
            vertex.uv1 = uv1[i];
            vertex.uv2 = uv2[i];
            vertex.uv3 = uv3[i];
            vertex.normal = normals[i];
            vertex.tangent = tangents[i];
        }

        public void SetUIVertex(UIVertex vertex, int i)
        {
            Allocate();
            positions[i] = vertex.position;
            colors[i] = vertex.color;
            uv0[i] = vertex.uv0;
            uv1[i] = vertex.uv1;
            uv2[i] = vertex.uv2;
            uv3[i] = vertex.uv3;
            normals[i] = vertex.normal;
            tangents[i] = vertex.tangent;
        }

        /// Replaces the mesh's contents. A UI mesh is limited to 65000 vertices.
        public void FillMesh(Mesh mesh)
        {
            Allocate();
            mesh.Clear();
            if (positions.Count >= 65000) throw new ArgumentException("Mesh can not have more than 65000 vertices");
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            mesh.SetUVs(3, uv3);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
        }

        public void AddVert(Vector3 position, Color32 color, Vector4 uv0, Vector4 uv1, Vector4 uv2, Vector4 uv3, Vector3 normal, Vector4 tangent)
        {
            Allocate();
            positions.Add(position);
            colors.Add(color);
            this.uv0.Add(uv0);
            this.uv1.Add(uv1);
            this.uv2.Add(uv2);
            this.uv3.Add(uv3);
            normals.Add(normal);
            tangents.Add(tangent);
        }

        public void AddVert(Vector3 position, Color32 color, Vector4 uv0, Vector4 uv1, Vector3 normal, Vector4 tangent)
            => AddVert(position, color, uv0, uv1, Vector4.zero, Vector4.zero, normal, tangent);

        public void AddVert(Vector3 position, Color32 color, Vector4 uv0)
            => AddVert(position, color, uv0, Vector4.zero, DefaultNormal, DefaultTangent);

        public void AddVert(UIVertex v) => AddVert(v.position, v.color, v.uv0, v.uv1, v.uv2, v.uv3, v.normal, v.tangent);

        public void AddTriangle(int idx0, int idx1, int idx2)
        {
            Allocate();
            indices.Add(idx0);
            indices.Add(idx1);
            indices.Add(idx2);
        }

        /// Four vertices, as two triangles (0, 1, 2) and (2, 3, 0).
        public void AddUIVertexQuad(UIVertex[] verts)
        {
            int start = currentVertCount;
            for (int i = 0; i < 4; i++) AddVert(verts[i]);
            AddTriangle(start, start + 1, start + 2);
            AddTriangle(start + 2, start + 3, start);
        }

        public void AddUIVertexStream(List<UIVertex> verts, List<int> indices)
        {
            if (verts != null)
            {
                Allocate();
                CanvasRenderer.AddUIVertexStream(verts, positions, colors, uv0, uv1, uv2, uv3, normals, tangents);
            }
            if (indices != null)
            {
                Allocate();
                this.indices.AddRange(indices);
            }
        }

        /// A triangle list: every three vertices are a triangle, replacing what was there.
        public void AddUIVertexTriangleStream(List<UIVertex> verts)
        {
            if (verts == null) return;
            Allocate();
            CanvasRenderer.SplitUIVertexStreams(verts, positions, colors, uv0, uv1, uv2, uv3, normals, tangents, indices);
        }

        /// The mesh as a triangle list, one UIVertex per index.
        public void GetUIVertexStream(List<UIVertex> stream)
        {
            if (stream == null) return;
            Allocate();
            CanvasRenderer.CreateUIVertexStream(stream, positions, colors, uv0, uv1, uv2, uv3, normals, tangents, indices);
        }
    }
}
