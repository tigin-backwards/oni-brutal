using System.Collections.Generic;
using UnityEngine;

namespace TMPro
{
    // The registry rich-text tags find their assets in: font assets, sprite assets, font
    // materials and colour gradients, each by an int key (an asset's hashCode, a material's
    // materialHashCode). Nothing is in it until code adds it (the game's fonts and its default
    // sprite asset are not). A key that is taken keeps its first entry, except a font's
    // material, whose add throws ArgumentException (measured).
    public class MaterialReferenceManager
    {
        static MaterialReferenceManager s_Instance;

        readonly Dictionary<int, TMP_FontAsset> m_Fonts = new();
        readonly Dictionary<int, TMP_SpriteAsset> m_Sprites = new();
        readonly Dictionary<int, Material> m_Materials = new();
        readonly Dictionary<int, TMP_ColorGradient> m_Gradients = new();

        public static MaterialReferenceManager instance => s_Instance ??= new MaterialReferenceManager();

        // Registers the font under its hashCode and then its material under its
        // materialHashCode. The same font again does nothing; another font under a taken key is
        // ignored; a font whose material key is taken is registered and then the material's add
        // throws.
        public static void AddFontAsset(TMP_FontAsset fontAsset)
        {
            var self = instance;
            int key = fontAsset.hashCode;
            if (self.m_Fonts.ContainsKey(key)) return;
            self.m_Fonts.Add(key, fontAsset);
            self.m_Materials.Add(fontAsset.materialHashCode, fontAsset.material);
        }

        public static void AddSpriteAsset(TMP_SpriteAsset spriteAsset) => AddSpriteAsset(spriteAsset.hashCode, spriteAsset);

        public static void AddSpriteAsset(int hashCode, TMP_SpriteAsset spriteAsset) => instance.m_Sprites.TryAdd(hashCode, spriteAsset);

        public static void AddFontMaterial(int hashCode, Material material) => instance.m_Materials.TryAdd(hashCode, material);

        public static void AddColorGradientPreset(int hashCode, TMP_ColorGradient spriteAsset) => instance.m_Gradients.TryAdd(hashCode, spriteAsset);

        public bool Contains(TMP_FontAsset font) => font != null && m_Fonts.ContainsKey(font.hashCode);

        public bool Contains(TMP_SpriteAsset sprite) => sprite != null && m_Sprites.ContainsKey(sprite.hashCode);

        public static bool TryGetFontAsset(int hashCode, out TMP_FontAsset fontAsset) => instance.m_Fonts.TryGetValue(hashCode, out fontAsset);

        public static bool TryGetSpriteAsset(int hashCode, out TMP_SpriteAsset spriteAsset) => instance.m_Sprites.TryGetValue(hashCode, out spriteAsset);

        public static bool TryGetMaterial(int hashCode, out Material material) => instance.m_Materials.TryGetValue(hashCode, out material);

        public static bool TryGetColorGradientPreset(int hashCode, out TMP_ColorGradient gradientPreset) => instance.m_Gradients.TryGetValue(hashCode, out gradientPreset);

        // A font registered by a tag's request: the font only, never its material.
        internal static void AddRequestedFont(int hashCode, TMP_FontAsset fontAsset) => instance.m_Fonts.TryAdd(hashCode, fontAsset);
    }
}
