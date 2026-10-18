using UnityEngine;
using UnityEngine.UI;

namespace TMPro
{
    // A text in the world, drawn by a MeshRenderer: one mesh with a sub-mesh (and a renderer
    // material) per material the text uses. It lays out when it changed, before rendering.
    public partial class TextMeshPro
    {
        MeshFilter m_MeshFilter;
        Material[] m_RendererMaterials = new Material[0];

        public Renderer renderer
        {
            get
            {
                if (m_renderer == null) m_renderer = GetComponent<Renderer>();
                return m_renderer;
            }
        }

        public MeshFilter meshFilter
        {
            get
            {
                if (m_MeshFilter == null)
                {
                    m_MeshFilter = GetComponent<MeshFilter>();
                    if (m_MeshFilter == null) m_MeshFilter = gameObject.AddComponent<MeshFilter>();
                }
                return m_MeshFilter;
            }
        }

        public int sortingLayerID
        {
            get => renderer.sortingLayerID;
            set
            {
                renderer.sortingLayerID = value;
                _SortingLayerID = value;
            }
        }

        public int sortingOrder
        {
            get => renderer.sortingOrder;
            set
            {
                renderer.sortingOrder = value;
                _SortingOrder = value;
            }
        }

        public MaskingTypes maskType { get => m_maskType; set => m_maskType = value; }

        protected override void Awake()
        {
            base.Awake();
            if (MeshCache == null) MeshCache = new Mesh { name = "TextMeshPro Mesh" };
            meshFilter.sharedMesh = MeshCache;
            if (m_textInfo == null) m_textInfo = new TMP_TextInfo(this);
            else m_textInfo.textComponent = this;
            if (m_fontAsset == null) m_fontAsset = TMP_Settings.defaultFontAsset;
            if (m_sharedMaterial == null && m_fontAsset != null) m_sharedMaterial = m_fontAsset.material;
            m_fontColor32 = m_fontColor;
            ConvertAlignment();
            m_havePropertiesChanged = true;
        }

        protected override void OnEnable()
        {
            if (MeshCache == null) MeshCache = new Mesh { name = "TextMeshPro Mesh" };
            meshFilter.sharedMesh = MeshCache;
            m_havePropertiesChanged = true;
            m_ParseRequired = true;
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPreCull += OnCameraPreCull;
            if (renderer != null) renderer.enabled = true;
        }

        protected override void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
            if (renderer != null) renderer.enabled = false;
        }

        protected override void OnDestroy()
        {
            Camera.onPreCull -= OnCameraPreCull;
            if (MeshCache != null) Destroy(MeshCache);
            MeshCache = null;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            InvalidateLayoutCache();
            m_havePropertiesChanged = true;
        }

        void OnCameraPreCull(Camera camera)
        {
            if (this == null || !isActiveAndEnabled || !m_havePropertiesChanged) return;
            GenerateTextMesh();
        }

        // Graphic's canvas machinery does not apply to a world text.
        public override void SetVerticesDirty() => m_havePropertiesChanged = true;

        public override void SetLayoutDirty() => InvalidateLayoutCache();

        public override void SetMaterialDirty() => UpdateRendererMaterials();

        public override void SetAllDirty()
        {
            m_havePropertiesChanged = true;
            InvalidateLayoutCache();
        }

        public override void Rebuild(CanvasUpdate update) { }

        protected override void UpdateGeometry() { }

        protected override void UpdateMaterial() => UpdateRendererMaterials();

        public override Material material
        {
            get => m_sharedMaterial;
            set => fontSharedMaterial = value;
        }

        protected virtual void GenerateTextMesh()
        {
            if (m_fontAsset == null) return;
            if (m_sharedMaterial == null) m_sharedMaterial = m_fontAsset.material;
            LayoutForRendering();
            if (textInfo.characterCount == 0)
            {
                ClearRenderedGeometry();
                m_havePropertiesChanged = false;
                TMPro_EventManager.ON_TEXT_CHANGED(this);
                return;
            }
            BuildGeometry();
            RaisePreRender();
            UploadGeometry(Mathf.Max(1, Parsed.Materials.Count));
            m_havePropertiesChanged = false;
            TMPro_EventManager.ON_TEXT_CHANGED(this);
        }

        private protected override void RegenerateNow() => GenerateTextMesh();

        private protected override Rect LayoutRect => rectTransform.rect;

        private protected override float SdfScaleFactor() => Mathf.Abs(transform.lossyScale.y);

        private protected override Mesh MeshForMaterial(int index) => null;

        // A world text draws every material through sub-meshes of its own mesh: it has no
        // sub-mesh objects whose slots persist.
        internal override void AddSubMeshMaterialSlots(System.Collections.Generic.List<MaterialSlot> slots) { }

        void UpdateRendererMaterials()
        {
            var r = renderer;
            if (r == null) return;
            int count = Mathf.Max(1, Parsed.Materials.Count);
            if (m_RendererMaterials.Length != count) m_RendererMaterials = new Material[count];
            m_RendererMaterials[0] = m_sharedMaterial;
            for (int i = 1; i < count; i++) m_RendererMaterials[i] = Parsed.Materials[i].Material;
            r.sharedMaterials = m_RendererMaterials;
        }

        // Every material's quads go into one mesh, a sub-mesh each.
        private protected override void UploadGeometry(int materialCount)
        {
            var info = textInfo;
            var mesh = MeshCache;
            int total = 0;
            for (int m = 0; m < materialCount; m++) total += info.meshInfo[m].vertices.Length;
            var vertices = new Vector3[total];
            var uv0 = new Vector4[total];
            var uv2 = new Vector2[total];
            var colors = new Color32[total];
            var normals = new Vector3[total];
            var tangents = new Vector4[total];
            int offset = 0;
            var starts = new int[materialCount];
            for (int m = 0; m < materialCount; m++)
            {
                ref var mi = ref info.meshInfo[m];
                starts[m] = offset;
                mi.vertices.CopyTo(vertices, offset);
                mi.uvs0.CopyTo(uv0, offset);
                mi.uvs2.CopyTo(uv2, offset);
                mi.colors32.CopyTo(colors, offset);
                mi.normals.CopyTo(normals, offset);
                mi.tangents.CopyTo(tangents, offset);
                offset += mi.vertices.Length;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.SetUVs(0, uv0);
            mesh.uv2 = uv2;
            mesh.colors32 = colors;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.subMeshCount = materialCount;
            for (int m = 0; m < materialCount; m++)
            {
                var tris = (int[])info.meshInfo[m].triangles.Clone();
                for (int k = 0; k < tris.Length; k++) tris[k] += starts[m];
                mesh.SetTriangles(tris, m);
            }
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;
            UpdateRendererMaterials();
        }

        private protected override void ClearRenderedGeometry()
        {
            if (MeshCache != null) MeshCache.Clear();
        }

        public override void UpdateGeometry(Mesh mesh, int index)
        {
            mesh.RecalculateBounds();
            if (index == 0) meshFilter.sharedMesh = mesh;
        }

        public override void UpdateVertexData(TMP_VertexDataUpdateFlags flags) => UploadGeometry(Mathf.Max(1, Parsed.Materials.Count));

        public override void UpdateVertexData() => UpdateVertexData(TMP_VertexDataUpdateFlags.All);

        public override void SetVertices(Vector3[] vertices)
        {
            if (MeshCache != null) MeshCache.vertices = vertices;
        }

        public override Mesh mesh => MeshCache;

        // ----- Layout element -----

        public void CalculateLayoutInputHorizontal() { }

        public void CalculateLayoutInputVertical() { }
    }
}
