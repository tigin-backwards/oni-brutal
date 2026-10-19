using System.Collections.Generic;
using UnityEngine;

namespace TMPro
{
    // Materials for characters drawn from fallback font assets while the settings match material
    // presets: a copy of the text's material (same name and effects) drawing the fallback font's
    // atlas with its gradient scale and weights. One per pair of materials, shared by all texts.
    internal static class FallbackMaterials
    {
        static readonly Dictionary<(int Source, int Fallback), Material> s_Materials = new();

        public static Material Get(Material source, TMP_FontAsset fallback)
        {
            var fallbackMaterial = fallback.material;
            if (source == null || fallbackMaterial == null) return fallbackMaterial;
            var key = (source.GetInstanceID(), fallbackMaterial.GetInstanceID());
            if (s_Materials.TryGetValue(key, out var material) && material != null) return material;
            material = new Material(source) { name = source.name };
            material.SetTexture(ShaderUtilities.ID_MainTex, fallbackMaterial.GetTexture(ShaderUtilities.ID_MainTex));
            Copy(fallbackMaterial, material, ShaderUtilities.ID_TextureWidth);
            Copy(fallbackMaterial, material, ShaderUtilities.ID_TextureHeight);
            Copy(fallbackMaterial, material, ShaderUtilities.ID_GradientScale);
            Copy(fallbackMaterial, material, ShaderUtilities.ID_WeightNormal);
            Copy(fallbackMaterial, material, ShaderUtilities.ID_WeightBold);
            ShaderUtilities.UpdateShaderRatios(material);
            s_Materials[key] = material;
            return material;
        }

        static void Copy(Material from, Material to, int id)
        {
            if (from.HasProperty(id) && to.HasProperty(id)) to.SetFloat(id, from.GetFloat(id));
        }
    }
}
