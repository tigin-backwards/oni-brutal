using System.Collections.Generic;
using UnityEngine;

namespace TMPro
{
    // The geometry of one material of a text: quads of four vertices (bottom-left, top-left,
    // top-right, bottom-right) with two triangles each, in arrays sized for a number of quads.
    // A new buffer has every normal (0, 0, -1), tangent (-1, 0, 0, 1) and colour white, zero
    // positions and texture coordinates, and the triangles of all its quads.
    public partial struct TMP_MeshInfo
    {
        static readonly Vector3 s_Normal = new(0f, 0f, -1f);
        static readonly Vector4 s_Tangent = new(-1f, 0f, 0f, 1f);
        static readonly Color32 s_White = new(255, 255, 255, 255);

        public TMP_MeshInfo(Mesh mesh, int size)
        {
            this = default;
            this.mesh = mesh ?? new Mesh();
            Allocate(size);
            this.mesh.vertices = vertices;
            this.mesh.normals = normals;
            this.mesh.tangents = tangents;
            this.mesh.SetUVs(0, uvs0);
            this.mesh.uv2 = uvs2;
            this.mesh.colors32 = colors32;
            this.mesh.triangles = triangles;
            this.mesh.bounds = new Bounds();
        }

        public TMP_MeshInfo(Mesh mesh, int size, bool isVolumetric) : this(mesh, size)
        {
        }

        void Allocate(int quads)
        {
            if (quads < 0) quads = 0;
            int n = quads * 4;
            vertices = new Vector3[n];
            uvs0 = new Vector4[n];
            uvs2 = new Vector2[n];
            colors32 = new Color32[n];
            normals = new Vector3[n];
            tangents = new Vector4[n];
            triangles = new int[quads * 6];
            for (int i = 0; i < n; i++)
            {
                normals[i] = s_Normal;
                tangents[i] = s_Tangent;
                colors32[i] = s_White;
            }
            for (int q = 0; q < quads; q++) SetQuadTriangles(triangles, q);
        }

        static void SetQuadTriangles(int[] triangles, int quad)
        {
            int v = quad * 4, t = quad * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 2;
            triangles[t + 4] = v + 3;
            triangles[t + 5] = v;
        }

        // Grows the buffers to `size` quads, keeping the existing vertex data.
        public void ResizeMeshInfo(int size)
        {
            if (size < 0) size = 0;
            int oldQuads = vertices == null ? 0 : vertices.Length / 4;
            int n = size * 4;
            System.Array.Resize(ref vertices, n);
            System.Array.Resize(ref normals, n);
            System.Array.Resize(ref tangents, n);
            System.Array.Resize(ref uvs0, n);
            System.Array.Resize(ref uvs2, n);
            System.Array.Resize(ref colors32, n);
            System.Array.Resize(ref triangles, size * 6);
            // The added vertices get the normal and tangent; their colour stays zero (measured:
            // the shipped package's grown buffers are so, unlike its new ones).
            for (int i = oldQuads * 4; i < n; i++)
            {
                normals[i] = s_Normal;
                tangents[i] = s_Tangent;
            }
            for (int q = oldQuads; q < size; q++) SetQuadTriangles(triangles, q);
            if (vertexCount > n) vertexCount = n;
            if (mesh == null) return;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
        }

        public void ResizeMeshInfo(int size, bool isVolumetric) => ResizeMeshInfo(size);

        // The number of quads the buffers hold.
        internal int QuadCapacity => vertices == null ? 0 : vertices.Length / 4;

        // Zeroes the positions of every vertex (the other channels keep their data).
        public void Clear()
        {
            if (vertices == null) return;
            System.Array.Clear(vertices, 0, vertices.Length);
            vertexCount = 0;
            if (mesh != null) mesh.vertices = vertices;
        }

        public void Clear(bool uploadChanges)
        {
            if (vertices == null) return;
            System.Array.Clear(vertices, 0, vertices.Length);
            vertexCount = 0;
            if (uploadChanges && mesh != null) mesh.vertices = vertices;
        }

        // Zeroes the positions from vertex `startIndex` to the end.
        public void ClearUnusedVertices()
        {
            if (vertices == null) return;
            ClearUnusedVertices(vertexCount);
        }

        public void ClearUnusedVertices(int startIndex)
        {
            if (vertices == null || startIndex >= vertices.Length) return;
            System.Array.Clear(vertices, startIndex, vertices.Length - startIndex);
        }

        public void ClearUnusedVertices(int startIndex, bool updateMesh)
        {
            ClearUnusedVertices(startIndex);
            if (updateMesh && mesh != null) mesh.vertices = vertices;
        }

        // Exchanges the vertex data of quads `src` and `dst`.
        public void SwapVertexData(int src, int dst)
        {
            int a = src * 4, b = dst * 4;
            for (int k = 0; k < 4; k++)
            {
                (vertices[a + k], vertices[b + k]) = (vertices[b + k], vertices[a + k]);
                (uvs0[a + k], uvs0[b + k]) = (uvs0[b + k], uvs0[a + k]);
                (uvs2[a + k], uvs2[b + k]) = (uvs2[b + k], uvs2[a + k]);
                (colors32[a + k], colors32[b + k]) = (colors32[b + k], colors32[a + k]);
            }
        }

        // Reorders the quads: quad i takes the data quad sortingOrder[i] had.
        public void SortGeometry(IList<int> sortingOrder)
        {
            int count = sortingOrder.Count;
            var position = new int[count];   // where each original quad currently is
            var content = new int[count];    // which original quad each slot holds
            for (int i = 0; i < count; i++) position[i] = content[i] = i;
            for (int i = 0; i < count; i++)
            {
                int wanted = sortingOrder[i];
                int at = position[wanted];
                if (at == i) continue;
                SwapVertexData(i, at);
                int displaced = content[i];
                content[at] = displaced;
                position[displaced] = at;
                content[i] = wanted;
                position[wanted] = i;
            }
        }

        // Reverses the order of the used quads, or restores the normal order.
        public void SortGeometry(VertexSortingOrder order)
        {
            if (order != VertexSortingOrder.Reverse) return;
            int quads = vertexCount / 4;
            for (int i = 0; i < quads / 2; i++) SwapVertexData(i, quads - 1 - i);
        }
    }
}
