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

        // Whether the sub-mesh is listed with its canvas's raycast targets. Unlike other
        // graphics, enabling does not list it: only its raycastTarget changing to true while it
        // is active and enabled does (measured on the shipped package). Disabling and destroying
        // drop it.
        bool m_RaycastListed;

        // Disabled by its text's OnDisable, to be enabled again by its OnEnable.
        internal bool DisabledWithText;

        // What the canvas renderer gets while no glyph uses the sub-mesh: the sub-mesh's own mesh
        // keeps its last fill.
        static Mesh s_EmptyMesh;

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

        public override bool raycastTarget
        {
            get => base.raycastTarget;
            set
            {
                if (base.raycastTarget == value) return;
                base.raycastTarget = value;
                m_RaycastListed = value && isActiveAndEnabled;
                if (!m_RaycastListed) GraphicRegistry.UnregisterRaycastGraphicForCanvas(canvas, this);
            }
        }

        // Graphic lists every raycast target it registers; the sub-mesh stays listed only when
        // its own property listed it.
        void KeepRaycastListing()
        {
            if (!m_RaycastListed) GraphicRegistry.UnregisterRaycastGraphicForCanvas(canvas, this);
        }

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

        // A new sub-mesh object under the text, filling its rect, hidden in the hierarchy. As
        // in the shipped player, the object keeps the default name ("New Game Object").
        internal static TMP_SubMeshUI AddSubTextObject(TextMeshProUGUI text, int materialIndex)
        {
            var go = new GameObject("New Game Object", typeof(RectTransform));
            go.hideFlags = TMP_Settings.hideSubTextObjects ? HideFlags.HideAndDontSave : HideFlags.DontSave;
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
            sub.maskable = text.maskable;
            sub.raycastTarget = text.raycastTarget;
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
                SetMaterialDirty();
            }
        }

        // Draws nothing; the mesh keeps its vertices and bounds from the last fill (measured on
        // the shipped package).
        internal void ClearGeometry()
        {
            if (m_Mesh == null) return;
            if (s_EmptyMesh == null) s_EmptyMesh = new Mesh { name = "TMP SubMeshUI Empty Mesh", hideFlags = HideFlags.HideAndDontSave };
            canvasRenderer.SetMesh(s_EmptyMesh);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_RaycastListed = false;
            KeepRaycastListing();
            if (m_TextComponent == null) m_TextComponent = GetComponentInParent<TextMeshProUGUI>();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            m_RaycastListed = false;
            GraphicRegistry.UnregisterRaycastGraphicForCanvas(canvas, this);
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            KeepRaycastListing();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            KeepRaycastListing();
        }

        // Dropped from the raycast targets, unlike the shipped package's, whose destroyed
        // sub-mesh stays listed and makes its canvas's raycasts throw.
        protected override void OnDestroy()
        {
            m_RaycastListed = false;
            GraphicRegistry.UnregisterRaycastGraphicForCanvas(canvas, this);
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

        // A sub-mesh created or reconfigured by its text's mesh generation (inside the canvas's
        // graphic rebuild) applies its material at once: the rebuild loop takes no new elements.
        public override void SetMaterialDirty()
        {
            if (CanvasUpdateRegistry.IsRebuildingGraphics())
            {
                if (IsActive()) UpdateMaterial();
                return;
            }
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
