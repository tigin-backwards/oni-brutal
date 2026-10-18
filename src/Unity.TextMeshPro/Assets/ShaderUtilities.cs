using UnityEngine;

namespace TMPro
{
    // The property IDs and keywords of TextMeshPro's shaders (the game's own shader assets
    // define them), and the material-derived values text layout needs.
    public static partial class ShaderUtilities
    {
        public static int ID_MainTex = Shader.PropertyToID("_MainTex");
        public static int ID_FaceTex = Shader.PropertyToID("_FaceTex");
        public static int ID_FaceColor = Shader.PropertyToID("_FaceColor");
        public static int ID_FaceDilate = Shader.PropertyToID("_FaceDilate");
        public static int ID_Shininess = Shader.PropertyToID("_FaceShininess");
        public static int ID_OutlineOffset1 = Shader.PropertyToID("_OutlineOffset1");
        public static int ID_OutlineOffset2 = Shader.PropertyToID("_OutlineOffset2");
        public static int ID_OutlineOffset3 = Shader.PropertyToID("_OutlineOffset3");
        public static int ID_OutlineMode = Shader.PropertyToID("_OutlineMode");
        public static int ID_IsoPerimeter = Shader.PropertyToID("_IsoPerimeter");
        public static int ID_Softness = Shader.PropertyToID("_Softness");
        public static int ID_UnderlayColor = Shader.PropertyToID("_UnderlayColor");
        public static int ID_UnderlayOffsetX = Shader.PropertyToID("_UnderlayOffsetX");
        public static int ID_UnderlayOffsetY = Shader.PropertyToID("_UnderlayOffsetY");
        public static int ID_UnderlayDilate = Shader.PropertyToID("_UnderlayDilate");
        public static int ID_UnderlaySoftness = Shader.PropertyToID("_UnderlaySoftness");
        public static int ID_UnderlayOffset = Shader.PropertyToID("_UnderlayOffset");
        public static int ID_UnderlayIsoPerimeter = Shader.PropertyToID("_UnderlayIsoPerimeter");
        public static int ID_WeightNormal = Shader.PropertyToID("_WeightNormal");
        public static int ID_WeightBold = Shader.PropertyToID("_WeightBold");
        public static int ID_OutlineTex = Shader.PropertyToID("_OutlineTex");
        public static int ID_OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        public static int ID_OutlineSoftness = Shader.PropertyToID("_OutlineSoftness");
        public static int ID_OutlineColor = Shader.PropertyToID("_OutlineColor");
        public static int ID_Outline2Color = Shader.PropertyToID("_Outline2Color");
        public static int ID_Outline2Width = Shader.PropertyToID("_Outline2Width");
        public static int ID_Padding = Shader.PropertyToID("_Padding");
        public static int ID_GradientScale = Shader.PropertyToID("_GradientScale");
        public static int ID_ScaleX = Shader.PropertyToID("_ScaleX");
        public static int ID_ScaleY = Shader.PropertyToID("_ScaleY");
        public static int ID_PerspectiveFilter = Shader.PropertyToID("_PerspectiveFilter");
        public static int ID_Sharpness = Shader.PropertyToID("_Sharpness");
        public static int ID_TextureWidth = Shader.PropertyToID("_TextureWidth");
        public static int ID_TextureHeight = Shader.PropertyToID("_TextureHeight");
        public static int ID_BevelAmount = Shader.PropertyToID("_Bevel");
        public static int ID_GlowColor = Shader.PropertyToID("_GlowColor");
        public static int ID_GlowOffset = Shader.PropertyToID("_GlowOffset");
        public static int ID_GlowPower = Shader.PropertyToID("_GlowPower");
        public static int ID_GlowOuter = Shader.PropertyToID("_GlowOuter");
        public static int ID_GlowInner = Shader.PropertyToID("_GlowInner");
        public static int ID_LightAngle = Shader.PropertyToID("_LightAngle");
        public static int ID_EnvMap = Shader.PropertyToID("_Cube");
        public static int ID_EnvMatrix = Shader.PropertyToID("_EnvMatrix");
        public static int ID_EnvMatrixRotation = Shader.PropertyToID("_EnvMatrixRotation");
        public static int ID_MaskCoord = Shader.PropertyToID("_MaskCoord");
        public static int ID_ClipRect = Shader.PropertyToID("_ClipRect");
        public static int ID_MaskSoftnessX = Shader.PropertyToID("_MaskSoftnessX");
        public static int ID_MaskSoftnessY = Shader.PropertyToID("_MaskSoftnessY");
        public static int ID_VertexOffsetX = Shader.PropertyToID("_VertexOffsetX");
        public static int ID_VertexOffsetY = Shader.PropertyToID("_VertexOffsetY");
        public static int ID_UseClipRect = Shader.PropertyToID("_UseClipRect");
        public static int ID_StencilID = Shader.PropertyToID("_Stencil");
        public static int ID_StencilOp = Shader.PropertyToID("_StencilOp");
        public static int ID_StencilComp = Shader.PropertyToID("_StencilComp");
        public static int ID_StencilReadMask = Shader.PropertyToID("_StencilReadMask");
        public static int ID_StencilWriteMask = Shader.PropertyToID("_StencilWriteMask");
        public static int ID_ShaderFlags = Shader.PropertyToID("_ShaderFlags");
        public static int ID_ScaleRatio_A = Shader.PropertyToID("_ScaleRatioA");
        public static int ID_ScaleRatio_B = Shader.PropertyToID("_ScaleRatioB");
        public static int ID_ScaleRatio_C = Shader.PropertyToID("_ScaleRatioC");
        public static int ID_CullMode = Shader.PropertyToID("_CullMode");
        public static int ID_ColorMask = Shader.PropertyToID("_ColorMask");

        public static string Keyword_Bevel = "BEVEL_ON";
        public static string Keyword_Glow = "GLOW_ON";
        public static string Keyword_Underlay = "UNDERLAY_ON";
        public static string Keyword_Ratios = "RATIOS_OFF";
        public static string Keyword_MASK_SOFT = "MASK_SOFT";
        public static string Keyword_MASK_HARD = "MASK_HARD";
        public static string Keyword_MASK_TEX = "MASK_TEX";
        public static string Keyword_Outline = "OUTLINE_ON";

        public static string ShaderTag_ZTestMode = "unity_GUIZTestMode";
        public static string ShaderTag_CullMode = "_CullMode";

        // Kept for API compatibility: the IDs are initialized with the class.
        public static void GetShaderPropertyIDs() { }

        static float Float(Material m, int id, float fallback = 0f) => m.HasProperty(id) ? m.GetFloat(id) : fallback;

        // The scale ratios keep the material's effects inside the distance field's range: the
        // range is (gradient scale - 1) / gradient scale of the field; A scales the face weight,
        // dilate, outline and softness, B the glow and C the underlay, each by the reach of its
        // effect, and B and C by what the face's own weight and dilation leave.
        public static void UpdateShaderRatios(Material mat)
        {
            if (mat == null) return;
            bool ratiosOff = mat.IsKeywordEnabled(Keyword_Ratios);
            if (!mat.HasProperty(ID_GradientScale)) return;
            float gradient = Float(mat, ID_GradientScale, 5f);
            float range = (gradient - 1f) / gradient;
            float weight = Mathf.Max(Float(mat, ID_WeightNormal), Float(mat, ID_WeightBold)) / 4f;
            float dilate = Float(mat, ID_FaceDilate);
            float outline = Float(mat, ID_OutlineWidth);
            float softness = Float(mat, ID_OutlineSoftness);
            float faceLeft = Mathf.Max(0f, 1f - weight - dilate);
            float a = ratiosOff ? 1f : range / Mathf.Max(1f, weight + dilate + outline + softness);
            mat.SetFloat(ID_ScaleRatio_A, a);
            if (mat.HasProperty(ID_GlowOffset))
            {
                float reach = Float(mat, ID_GlowOffset) + Float(mat, ID_GlowOuter);
                float b = ratiosOff ? 1f : range * faceLeft / Mathf.Max(1f, reach);
                mat.SetFloat(ID_ScaleRatio_B, b);
            }
            if (mat.HasProperty(ID_UnderlayOffsetX))
            {
                float offset = Mathf.Max(Mathf.Abs(Float(mat, ID_UnderlayOffsetX)), Mathf.Abs(Float(mat, ID_UnderlayOffsetY)));
                float reach = offset + Float(mat, ID_UnderlayDilate) + Float(mat, ID_UnderlaySoftness);
                float c = ratiosOff ? 1f : range * faceLeft / Mathf.Max(1f, reach);
                mat.SetFloat(ID_ScaleRatio_C, c);
            }
        }

        // The texels of padding a glyph quad needs around the glyph for the material's effects.
        public static float GetPadding(Material material, bool enableExtraPadding, bool isBold)
        {
            if (material == null) return 0f;
            if (enableExtraPadding) return Float(material, ID_GradientScale, 5f);
            return 1.25f;
        }

        // Whether the material clips to its rect or mask texture (soft or hard masking).
        public static bool IsMaskingEnabled(Material material)
        {
            if (material == null || !material.HasProperty(ID_ClipRect)) return false;
            return material.IsKeywordEnabled(Keyword_MASK_SOFT) || material.IsKeywordEnabled(Keyword_MASK_HARD) || material.IsKeywordEnabled(Keyword_MASK_TEX);
        }

        // The weight the material's shader uses for normal and bold text.
        public static float GetFontStyle(Material material) => Float(material, ID_WeightNormal);
    }
}
