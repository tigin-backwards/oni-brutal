using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Sprites;
using UnityEngine.U2D;

namespace UnityEngine.UI
{
    /// Draws a sprite: whole (Simple), nine-sliced (Sliced), with a repeating centre (Tiled) or
    /// partly (Filled).
    public partial class Image
    {
        protected static Material s_ETC1DefaultUI = null;

        [NonSerialized] Sprite overriding;
        [NonSerialized] float alphaHitThreshold;
        [NonSerialized] bool tracked;
        [NonSerialized] float referencePixelsPerUnit = 100f;
        [NonSerialized] bool sameSize;
        [NonSerialized] bool sameTexture;

        static readonly List<Image> waitingForAtlas = new();
        static bool atlasHooked;

        protected Image()
        {
            m_FillCenter = true;
            m_FillMethod = FillMethod.Radial360;
            m_FillAmount = 1f;
            m_FillClockwise = true;
            m_FillOrigin = 0;
            m_PixelsPerUnitMultiplier = 1f;
            useLegacyMeshGeneration = false;
        }

        public enum OriginHorizontal { Left = 0, Right = 1 }
        public enum OriginVertical { Bottom = 0, Top = 1 }
        public enum Origin90 { BottomLeft = 0, TopLeft = 1, TopRight = 2, BottomRight = 3 }
        public enum Origin180 { Bottom = 0, Left = 1, Top = 2, Right = 3 }
        public enum Origin360 { Bottom = 0, Right = 1, Top = 2, Left = 3 }

        public Sprite sprite
        {
            get => m_Sprite;
            set
            {
                if (m_Sprite == value) return;
                // A sprite of the same size needs no new layout, one on the same texture no new
                // material.
                sameSize = m_Sprite != null && value != null && m_Sprite.rect.size == value.rect.size;
                sameTexture = m_Sprite != null && value != null && m_Sprite.texture == value.texture;
                m_Sprite = value;
                ResetAlphaHitThresholdIfNeeded();
                SetAllDirty();
                TrackSprite();
            }
        }

        /// Drawn instead of the sprite while set (Selectable's sprite swap uses it); reads the
        /// sprite in use.
        public Sprite overrideSprite
        {
            get => activeSprite;
            set
            {
                if (overriding == value) return;
                overriding = value;
                SetAllDirty();
                TrackSprite();
            }
        }

        Sprite activeSprite => overriding != null ? overriding : m_Sprite;

        /// Turns off the shortcuts taken when the sprite changes to one of the same size or texture.
        public void DisableSpriteOptimizations()
        {
            sameSize = false;
            sameTexture = false;
        }

        public override void SetLayoutDirty()
        {
            if (sameSize)
            {
                sameSize = false;
                return;
            }
            base.SetLayoutDirty();
        }

        public override void SetMaterialDirty()
        {
            if (sameTexture)
            {
                sameTexture = false;
                return;
            }
            base.SetMaterialDirty();
        }

        void ResetAlphaHitThresholdIfNeeded()
        {
            // Only a readable, unpacked texture can be sampled.
            if (!SpriteSupportsAlphaHitTest() && alphaHitThreshold > 0f)
            {
                Debug.LogWarning("Sprite was changed for one not readable or with Crunch Compression. Resetting the AlphaHitThreshold to 0.", this);
                alphaHitThreshold = 0f;
            }
        }

        bool SpriteSupportsAlphaHitTest() => m_Sprite != null && m_Sprite.texture != null && !GraphicsFormatUtility.IsCrunchFormat(m_Sprite.texture.format) && m_Sprite.texture.isReadable;

        public Type type
        {
            get => m_Type;
            set { if (m_Type == value) return; m_Type = value; SetVerticesDirty(); }
        }

        public bool preserveAspect
        {
            get => m_PreserveAspect;
            set { if (m_PreserveAspect == value) return; m_PreserveAspect = value; SetVerticesDirty(); }
        }

        public bool fillCenter
        {
            get => m_FillCenter;
            set { if (m_FillCenter == value) return; m_FillCenter = value; SetVerticesDirty(); }
        }

        /// Changing the method resets the origin.
        public FillMethod fillMethod
        {
            get => m_FillMethod;
            set
            {
                if (m_FillMethod == value) return;
                m_FillMethod = value;
                SetVerticesDirty();
                m_FillOrigin = 0;
            }
        }

        public float fillAmount
        {
            get => m_FillAmount;
            set
            {
                value = Mathf.Clamp01(value);
                if (m_FillAmount == value) return;
                m_FillAmount = value;
                SetVerticesDirty();
            }
        }

        public bool fillClockwise
        {
            get => m_FillClockwise;
            set { if (m_FillClockwise == value) return; m_FillClockwise = value; SetVerticesDirty(); }
        }

        public int fillOrigin
        {
            get => m_FillOrigin;
            set { if (m_FillOrigin == value) return; m_FillOrigin = value; SetVerticesDirty(); }
        }

        public bool useSpriteMesh
        {
            get => m_UseSpriteMesh;
            set { if (m_UseSpriteMesh == value) return; m_UseSpriteMesh = value; SetVerticesDirty(); }
        }

        [Obsolete("eventAlphaThreshold has been deprecated. Use eventMinimumAlphaThreshold instead (UnityUpgradable) -> alphaHitTestMinimumThreshold")]
        public float eventAlphaThreshold
        {
            get => 1f - alphaHitTestMinimumThreshold;
            set => alphaHitTestMinimumThreshold = 1f - value;
        }

        /// The least sprite alpha at a point that a raycast accepts; 0 accepts the whole rect.
        public float alphaHitTestMinimumThreshold
        {
            get => alphaHitThreshold;
            set => alphaHitThreshold = value;
        }

        public static Material defaultETC1GraphicMaterial
        {
            get
            {
                if (s_ETC1DefaultUI == null) s_ETC1DefaultUI = Canvas.GetETC1SupportedCanvasMaterial();
                return s_ETC1DefaultUI;
            }
        }

        /// The sprite's texture; without a sprite, the material's texture or white.
        public override Texture mainTexture
        {
            get
            {
                var s = activeSprite;
                if (s == null)
                {
                    var m = material;
                    if (m != null && m.mainTexture != null) return m.mainTexture;
                    return s_WhiteTexture != null ? s_WhiteTexture : (s_WhiteTexture = Texture2D.whiteTexture);
                }
                return s.texture;
            }
        }

        public bool hasBorder => activeSprite != null && activeSprite.border.sqrMagnitude > 0f;

        public float pixelsPerUnitMultiplier
        {
            get => m_PixelsPerUnitMultiplier;
            set => m_PixelsPerUnitMultiplier = Mathf.Max(0.01f, value);
        }

        /// Sprite pixels per canvas unit: the sprite's pixels per unit over the canvas's
        /// reference pixels per unit.
        public float pixelsPerUnit
        {
            get
            {
                float spritePixels = activeSprite != null ? activeSprite.pixelsPerUnit : 100f;
                if (canvas != null) referencePixelsPerUnit = canvas.referencePixelsPerUnit;
                return spritePixels / referencePixelsPerUnit;
            }
        }

        protected float multipliedPixelsPerUnit => pixelsPerUnit * m_PixelsPerUnitMultiplier;

        /// Without its own material, a sprite with a separate alpha texture uses the ETC1 material.
        public override Material material
        {
            get
            {
                if (m_Material != null) return m_Material;
                if (activeSprite != null && activeSprite.associatedAlphaSplitTexture != null) return defaultETC1GraphicMaterial;
                return defaultMaterial;
            }
            set => base.material = value;
        }

        public virtual void OnBeforeSerialize() { }

        /// Brings serialized fill settings into range.
        public virtual void OnAfterDeserialize()
        {
            if (m_FillOrigin < 0) m_FillOrigin = 0;
            else if (m_FillMethod == FillMethod.Horizontal && m_FillOrigin > 1) m_FillOrigin = 0;
            else if (m_FillMethod == FillMethod.Vertical && m_FillOrigin > 1) m_FillOrigin = 0;
            else if (m_FillOrigin > 3) m_FillOrigin = 0;
            m_FillAmount = Mathf.Clamp(m_FillAmount, 0f, 1f);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            TrackSprite();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (tracked) Untrack(this);
        }

        protected override void UpdateMaterial()
        {
            base.UpdateMaterial();
            var s = activeSprite;
            if (s == null)
            {
                canvasRenderer.SetAlphaTexture(null);
                return;
            }
            var alpha = s.associatedAlphaSplitTexture;
            if (alpha != null) canvasRenderer.SetAlphaTexture(alpha);
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            if (canvas == null)
            {
                referencePixelsPerUnit = 100f;
                return;
            }
            if (canvas.referencePixelsPerUnit == referencePixelsPerUnit) return;
            referencePixelsPerUnit = canvas.referencePixelsPerUnit;
            if (m_Type == Type.Sliced || m_Type == Type.Tiled)
            {
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }

        /// The sprite's natural size: its pixels over pixelsPerUnit, with the anchors together.
        public override void SetNativeSize()
        {
            var s = activeSprite;
            if (s == null) return;
            float w = s.rect.width / pixelsPerUnit;
            float h = s.rect.height / pixelsPerUnit;
            rectTransform.anchorMax = rectTransform.anchorMin;
            rectTransform.sizeDelta = new Vector2(w, h);
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            if (activeSprite == null)
            {
                base.OnPopulateMesh(toFill);
                return;
            }
            switch (m_Type)
            {
                case Type.Simple:
                    if (m_UseSpriteMesh) ImageMesh.SpriteMesh(this, toFill, activeSprite, m_PreserveAspect);
                    else ImageMesh.Simple(this, toFill, activeSprite, m_PreserveAspect);
                    break;
                case Type.Sliced:
                    ImageMesh.Sliced(this, toFill, activeSprite, multipliedPixelsPerUnit, m_FillCenter);
                    break;
                case Type.Tiled:
                    ImageMesh.Tiled(this, toFill, activeSprite, multipliedPixelsPerUnit, m_FillCenter);
                    break;
                case Type.Filled:
                    ImageMesh.Filled(this, toFill, activeSprite, m_PreserveAspect, m_FillMethod, m_FillAmount, m_FillOrigin, m_FillClockwise);
                    break;
            }
        }

        // ----- Layout: the sprite's size (its borders for Sliced and Tiled), nothing flexible -----

        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }

        public virtual float minWidth => 0f;

        public virtual float preferredWidth
        {
            get
            {
                var s = activeSprite;
                if (s == null) return 0f;
                if (m_Type == Type.Sliced || m_Type == Type.Tiled) return DataUtility.GetMinSize(s).x / pixelsPerUnit;
                return s.rect.size.x / pixelsPerUnit;
            }
        }

        public virtual float flexibleWidth => -1f;

        public virtual float minHeight => 0f;

        public virtual float preferredHeight
        {
            get
            {
                var s = activeSprite;
                if (s == null) return 0f;
                if (m_Type == Type.Sliced || m_Type == Type.Tiled) return DataUtility.GetMinSize(s).y / pixelsPerUnit;
                return s.rect.size.y / pixelsPerUnit;
            }
        }

        public virtual float flexibleHeight => -1f;

        public virtual int layoutPriority => 0;

        // ----- Raycasting -----

        /// With a threshold above 0, the sprite's alpha under the point must reach it (the
        /// texture must be readable).
        public virtual bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (alphaHitThreshold <= 0f) return true;
            if (alphaHitThreshold > 1f) return false;
            var s = activeSprite;
            if (s == null) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local)) return false;
            var r = GetPixelAdjustedRect();
            if (m_PreserveAspect) r = ImageMesh.FitAspect(r, s.rect.size, rectTransform.pivot);
            // From the rect's corner, in sprite pixels.
            var p = ImageMesh.ToSpritePixels(new Vector2(local.x - r.x, local.y - r.y), r, s, m_Type, multipliedPixelsPerUnit);
            var tr = s.textureRect;
            float u = Mathf.Lerp(tr.x, tr.xMax, p.x / s.rect.width) / s.texture.width;
            float v = Mathf.Lerp(tr.y, tr.yMax, p.y / s.rect.height) / s.texture.height;
            try
            {
                return s.texture.GetPixelBilinear(u, v).a >= alphaHitThreshold;
            }
            catch (UnityException e)
            {
                Debug.LogError("Using alphaHitTestMinimumThreshold greater than 0 on Image whose sprite texture cannot be read. " + e.Message + " Also make sure to disable sprite packing for this sprite.", this);
                return true;
            }
        }

        // ----- Sprites in atlases that are not loaded yet -----

        /// A sprite without a texture belongs to an atlas not registered yet: the image redraws
        /// once the atlas comes.
        void TrackSprite()
        {
            if (activeSprite == null || activeSprite.texture != null || tracked) return;
            if (!atlasHooked)
            {
                SpriteAtlasManager.atlasRegistered += AtlasRegistered;
                atlasHooked = true;
            }
            waitingForAtlas.Add(this);
            tracked = true;
        }

        static void Untrack(Image image)
        {
            waitingForAtlas.Remove(image);
            image.tracked = false;
        }

        static void AtlasRegistered(SpriteAtlas atlas)
        {
            for (int i = waitingForAtlas.Count - 1; i >= 0; i--)
            {
                var image = waitingForAtlas[i];
                if (image == null)
                {
                    waitingForAtlas.RemoveAt(i);
                    continue;
                }
                if (image.activeSprite == null || !atlas.CanBindTo(image.activeSprite)) continue;
                image.SetAllDirty();
                Untrack(image);
            }
        }
    }
}
