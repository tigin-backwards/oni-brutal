using UnityEngine;
using UnityEngine.UI;

namespace TMPro
{
    // The geometry of one material of a TextMeshProUGUI other than its own (a fallback font's or
    // a sprite asset's), drawn by a child object's CanvasRenderer. The text writes the mesh; the
    // sub-mesh renders it with its material, masked and clipped like the text.
    public partial class TMP_SubMeshUI
    {
        Mesh m_Mesh;
        bool m_MaterialDirtyFlag;

        public TMP_FontAsset fontAsset { get => m_fontAsset; set => m_fontAsset = value; }

        public TMP_SpriteAsset spriteAsset { get => m_spriteAsset; set => m_spriteAsset = value; }

        public override Texture mainTexture => m_sharedMaterial != null ? m_sharedMaterial.GetTexture(ShaderUtilities.ID_MainTex) : null;

        public override Material material
        {
            get => m_sharedMaterial;
            set
            {
                if (m_sharedMaterial == value) return;
                m_sharedMaterial = value;
                m_padding = 0f;
                SetMaterialDirty();
            }
        }

        public Material sharedMaterial
        {
            get => m_sharedMaterial;
            set
            {
                if (m_sharedMaterial == value) return;
                m_sharedMaterial = value;
                SetMaterialDirty();
            }
        }

        public Material fallbackMaterial { get; set; }

        public Material fallbackSourceMaterial { get; set; }

        public bool isDefaultMaterial { get => m_isDefaultMaterial; set => m_isDefaultMaterial = value; }

        public float padding { get => m_padding; set => m_padding = value; }

        public Mesh mesh
        {
            get
            {
                if (m_Mesh == null) m_Mesh = new Mesh { name = "TMP SubMeshUI Mesh" };
                return m_Mesh;
            }
            set => m_Mesh = value;
        }

        public TMP_Text textComponent
        {
            get
            {
                if (m_TextComponent == null) m_TextComponent = GetComponentInParent<TextMeshProUGUI>();
                return m_TextComponent;
            }
        }

        internal int MaterialReferenceIndex => m_materialReferenceIndex;

        internal bool HasGeometry => m_Mesh != null && m_Mesh.vertexCount > 0;

        // A new sub-mesh object under the text, filling its rect, hidden in the hierarchy.
        internal static TMP_SubMeshUI AddSubTextObject(TextMeshProUGUI text, int materialIndex)
        {
            var go = new GameObject("TMP SubMeshUI", typeof(RectTransform));
            if (TMP_Settings.hideSubTextObjects) go.hideFlags = HideFlags.HideAndDontSave;
            go.layer = text.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(text.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = text.rectTransform.pivot;
            rt.localPosition = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            var element = go.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            var sub = go.AddComponent<TMP_SubMeshUI>();
            sub.m_TextComponent = text;
            sub.m_materialReferenceIndex = materialIndex;
            sub.raycastTarget = false;
            return sub;
        }

        // Points the sub-mesh at the material slot it draws.
        internal void Configure(Material material, TMP_Asset asset, int materialIndex)
        {
            m_materialReferenceIndex = materialIndex;
            m_fontAsset = asset as TMP_FontAsset;
            m_spriteAsset = asset as TMP_SpriteAsset;
            if (m_sharedMaterial != material)
            {
                m_sharedMaterial = material;
                m_isDefaultMaterial = false;
                if (material != null) gameObject.name = "TMP SubMeshUI [" + material.name + "]";
                SetMaterialDirty();
            }
        }

        internal void ClearGeometry()
        {
            if (m_Mesh == null) return;
            m_Mesh.Clear();
            canvasRenderer.SetMesh(m_Mesh);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_TextComponent == null) m_TextComponent = GetComponentInParent<TextMeshProUGUI>();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_Mesh != null) Destroy(m_Mesh);
        }

        // The text writes the geometry; the Graphic's own vertex generation is not used.
        protected override void UpdateGeometry() { }

        protected override void UpdateMaterial()
        {
            if (canvasRenderer == null) return;
            canvasRenderer.materialCount = 1;
            canvasRenderer.SetMaterial(materialForRendering, 0);
        }

        public override void SetMaterialDirty()
        {
            m_MaterialDirtyFlag = true;
            base.SetMaterialDirty();
        }

        // The parent text owns the geometry and the layout: it regenerates the sub-mesh's mesh
        // whenever its own changes.
        public override void SetVerticesDirty() { }

        public override void SetLayoutDirty() { }

        public override void Rebuild(CanvasUpdate update)
        {
            if (update != CanvasUpdate.PreRender || !m_MaterialDirtyFlag) return;
            m_MaterialDirtyFlag = false;
            UpdateMaterial();
        }

        public override void SetAllDirty() => SetMaterialDirty();
    }
}
