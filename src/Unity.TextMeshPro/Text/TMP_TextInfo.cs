using System;
using UnityEngine;

namespace TMPro
{
    // The result of a layout: characters, words, links, lines, pages and the geometry per
    // material. Arrays only grow; entries beyond the counts keep stale data.
    public partial class TMP_TextInfo
    {
        // Initial capacities of the arrays of a new text info.
        const int InitialCharacters = 16, InitialWords = 8, InitialLinks = 0, InitialLines = 4, InitialPages = 4;

        public TMP_TextInfo()
        {
            characterInfo = new TMP_CharacterInfo[InitialCharacters];
            wordInfo = new TMP_WordInfo[InitialWords];
            linkInfo = new TMP_LinkInfo[InitialLinks];
            lineInfo = new TMP_LineInfo[InitialLines];
            pageInfo = new TMP_PageInfo[InitialPages];
            meshInfo = new TMP_MeshInfo[1];
        }

        public TMP_TextInfo(TMP_Text textComponent) : this()
        {
            this.textComponent = textComponent;
        }

        // Resets the counts (the arrays and the meshes stay).
        public void Clear()
        {
            characterCount = 0;
            spaceCount = 0;
            wordCount = 0;
            linkCount = 0;
            lineCount = 0;
            pageCount = 0;
            spriteCount = 0;
            for (int i = 0; i < meshInfo.Length; i++) meshInfo[i].vertexCount = 0;
        }

        public void ClearMeshInfo(bool updateMesh)
        {
            for (int i = 0; i < meshInfo.Length; i++) meshInfo[i].Clear(updateMesh);
        }

        public void ClearAllMeshInfo()
        {
            for (int i = 0; i < meshInfo.Length; i++) meshInfo[i].Clear(true);
        }

        public void ResetVertexLayout(bool isVolumetric)
        {
            for (int i = 0; i < meshInfo.Length; i++) meshInfo[i].ResizeMeshInfo(0, isVolumetric);
        }

        public void ClearLineInfo()
        {
            if (lineInfo == null) lineInfo = new TMP_LineInfo[InitialLines];
            Array.Clear(lineInfo, 0, lineInfo.Length);
        }

        public void ClearPageInfo()
        {
            if (pageInfo == null) pageInfo = new TMP_PageInfo[InitialPages];
            Array.Clear(pageInfo, 0, pageInfo.Length);
        }

        // Copies of the vertex data of every mesh (for animations that restore it).
        public TMP_MeshInfo[] CopyMeshInfoVertexData()
        {
            var copy = new TMP_MeshInfo[meshInfo.Length];
            for (int i = 0; i < meshInfo.Length; i++)
            {
                var m = meshInfo[i];
                copy[i] = new TMP_MeshInfo
                {
                    vertexCount = m.vertexCount,
                    vertices = Copy(m.vertices),
                    uvs0 = Copy(m.uvs0),
                    uvs2 = Copy(m.uvs2),
                    colors32 = Copy(m.colors32),
                    normals = Copy(m.normals),
                    tangents = Copy(m.tangents),
                    triangles = Copy(m.triangles),
                    material = m.material,
                };
            }
            return copy;
        }

        static T[] Copy<T>(T[] source) => source == null ? null : (T[])source.Clone();

        // Grows an array to at least `size` entries (the next power of two), keeping its contents.
        public static void Resize<T>(ref T[] array, int size) => Array.Resize(ref array, Mathf.NextPowerOfTwo(size));

        // Resizes to exactly `size` entries, or to the next power of two when block allocated.
        public static void Resize<T>(ref T[] array, int size, bool isBlockAllocated)
        {
            if (isBlockAllocated) size = Mathf.NextPowerOfTwo(size);
            if (array != null && size == array.Length) return;
            Array.Resize(ref array, size);
        }

        internal void EnsureCharacters(int count)
        {
            if (characterInfo.Length < count) Resize(ref characterInfo, count);
        }

        internal void EnsureWords(int count)
        {
            if (wordInfo.Length < count) Resize(ref wordInfo, count);
        }

        internal void EnsureLinks(int count)
        {
            if (linkInfo.Length < count) Resize(ref linkInfo, count);
        }

        internal void EnsureLines(int count)
        {
            if (lineInfo.Length < count) Resize(ref lineInfo, count);
        }

        internal void EnsurePages(int count)
        {
            if (pageInfo.Length < count) Resize(ref pageInfo, count);
        }
    }

    public partial struct TMP_WordInfo
    {
        // The word's characters, from the text info's character entries.
        public string GetWord()
        {
            var info = textComponent.textInfo.characterInfo;
            var chars = new char[characterCount];
            for (int i = 0; i < characterCount && firstCharacterIndex + i < info.Length; i++) chars[i] = info[firstCharacterIndex + i].character;
            return new string(chars);
        }
    }

    public partial struct TMP_LinkInfo
    {
        // The value of the link tag (its ID).
        public string GetLinkID() => textComponent != null ? textComponent.LinkId(linkIdFirstCharacterIndex, hashCode) : "";

        // The text of the characters inside the link.
        public string GetLinkText()
        {
            var info = textComponent.textInfo.characterInfo;
            var chars = new char[linkTextLength];
            for (int i = 0; i < linkTextLength && linkTextfirstCharacterIndex + i < info.Length; i++) chars[i] = info[linkTextfirstCharacterIndex + i].character;
            return new string(chars);
        }
    }

    public partial struct Extents
    {
        internal static readonly Extents zero = new(Vector2.zero, Vector2.zero);

        internal static readonly Extents uninitialized = new(new Vector2(32767f, 32767f), new Vector2(-32767f, -32767f));

        public Extents(Vector2 min, Vector2 max)
        {
            this.min = min;
            this.max = max;
        }

        public override string ToString() => "Min (" + min.x.ToString("f2") + ", " + min.y.ToString("f2") + ")   Max (" + max.x.ToString("f2") + ", " + max.y.ToString("f2") + ")";
    }

    public partial struct TMP_Offset
    {
        float m_Left, m_Right, m_Top, m_Bottom;

        public float left { get => m_Left; set => m_Left = value; }
        public float right { get => m_Right; set => m_Right = value; }
        public float top { get => m_Top; set => m_Top = value; }
        public float bottom { get => m_Bottom; set => m_Bottom = value; }
        public float horizontal { get => m_Left; set { m_Left = value; m_Right = value; } }
        public float vertical { get => m_Top; set { m_Top = value; m_Bottom = value; } }

        public static TMP_Offset zero => default;

        public TMP_Offset(float left, float right, float top, float bottom)
        {
            m_Left = left;
            m_Right = right;
            m_Top = top;
            m_Bottom = bottom;
        }

        public TMP_Offset(float horizontal, float vertical)
        {
            m_Left = m_Right = horizontal;
            m_Top = m_Bottom = vertical;
        }

        public static bool operator ==(TMP_Offset lhs, TMP_Offset rhs) =>
            lhs.m_Left == rhs.m_Left && lhs.m_Right == rhs.m_Right && lhs.m_Top == rhs.m_Top && lhs.m_Bottom == rhs.m_Bottom;

        public static bool operator !=(TMP_Offset lhs, TMP_Offset rhs) => !(lhs == rhs);

        public static TMP_Offset operator *(TMP_Offset a, float b) => new(a.m_Left * b, a.m_Right * b, a.m_Top * b, a.m_Bottom * b);

        public override int GetHashCode() => HashCode.Combine(m_Left, m_Right, m_Top, m_Bottom);

        public override bool Equals(object obj) => obj is TMP_Offset other && this == other;

        public bool Equals(TMP_Offset other) => this == other;
    }

    public partial struct HighlightState
    {
        public HighlightState(Color32 color, TMP_Offset padding)
        {
            this.color = color;
            this.padding = padding;
        }

        public static bool operator ==(HighlightState lhs, HighlightState rhs) =>
            lhs.color.r == rhs.color.r && lhs.color.g == rhs.color.g && lhs.color.b == rhs.color.b && lhs.color.a == rhs.color.a && lhs.padding == rhs.padding;

        public static bool operator !=(HighlightState lhs, HighlightState rhs) => !(lhs == rhs);

        public override int GetHashCode() => HashCode.Combine(color.r, color.g, color.b, color.a, padding);

        public override bool Equals(object obj) => obj is HighlightState other && this == other;

        public bool Equals(HighlightState other) => this == other;
    }
}
