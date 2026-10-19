using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore;

namespace TMPro
{
    // The project's TextMeshPro settings: the asset "TMP Settings" in Resources. Every static
    // property reads the loaded asset's serialized fields.
    public partial class TMP_Settings
    {
        static TMP_Settings s_Instance;

        public static string version => instance?.assetVersion;

        public static TMP_Settings instance
        {
            get
            {
                if (s_Instance == null) s_Instance = Resources.Load<TMP_Settings>("TMP Settings");
                return s_Instance;
            }
        }

        public static TMP_Settings LoadDefaultSettings() => instance;

        public static TMP_Settings GetSettings() => instance;

        public static TMP_FontAsset GetFontAsset() => instance?.m_defaultFontAsset;

        public static TMP_SpriteAsset GetSpriteAsset() => instance?.m_defaultSpriteAsset;

        public static TMP_StyleSheet GetStyleSheet() => instance?.m_defaultStyleSheet;

        public static TextWrappingModes textWrappingMode
        {
            get => instance?.m_TextWrappingMode ?? TextWrappingModes.Normal;
            set { if (instance != null) instance.m_TextWrappingMode = value; }
        }

        public static bool enableWordWrapping
        {
            get => textWrappingMode == TextWrappingModes.Normal || textWrappingMode == TextWrappingModes.PreserveWhitespace;
            set => textWrappingMode = value ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        }

        public static bool enableKerning
        {
            get => instance?.m_enableKerning ?? true;
            set { if (instance != null) instance.m_enableKerning = value; }
        }

        public static List<OTL_FeatureTag> fontFeatures
        {
            get => instance?.m_ActiveFontFeatures;
            set { if (instance != null) instance.m_ActiveFontFeatures = value; }
        }

        public static bool enableExtraPadding
        {
            get => instance?.m_enableExtraPadding ?? false;
            set { if (instance != null) instance.m_enableExtraPadding = value; }
        }

        public static bool enableTintAllSprites
        {
            get => instance?.m_enableTintAllSprites ?? false;
            set { if (instance != null) instance.m_enableTintAllSprites = value; }
        }

        public static bool enableParseEscapeCharacters
        {
            get => instance?.m_enableParseEscapeCharacters ?? true;
            set { if (instance != null) instance.m_enableParseEscapeCharacters = value; }
        }

        public static bool enableRaycastTarget
        {
            get => instance?.m_EnableRaycastTarget ?? true;
            set { if (instance != null) instance.m_EnableRaycastTarget = value; }
        }

        public static bool getFontFeaturesAtRuntime
        {
            get => instance?.m_GetFontFeaturesAtRuntime ?? true;
            set { if (instance != null) instance.m_GetFontFeaturesAtRuntime = value; }
        }

        // U+25A1 (white square) unless the settings name another character.
        public static int missingGlyphCharacter
        {
            get => instance?.m_missingGlyphCharacter ?? 0x25A1;
            set { if (instance != null) instance.m_missingGlyphCharacter = value; }
        }

        public static bool clearDynamicDataOnBuild
        {
            get => instance?.m_ClearDynamicDataOnBuild ?? true;
            set { if (instance != null) instance.m_ClearDynamicDataOnBuild = value; }
        }

        public static bool warningsDisabled
        {
            get => instance?.m_warningsDisabled ?? false;
            set { if (instance != null) instance.m_warningsDisabled = value; }
        }

        public static TMP_FontAsset defaultFontAsset
        {
            get => instance?.m_defaultFontAsset;
            set { if (instance != null) instance.m_defaultFontAsset = value; }
        }

        public static string defaultFontAssetPath
        {
            get => instance?.m_defaultFontAssetPath;
            set { if (instance != null) instance.m_defaultFontAssetPath = value; }
        }

        public static float defaultFontSize
        {
            get => instance?.m_defaultFontSize ?? 36f;
            set { if (instance != null) instance.m_defaultFontSize = value; }
        }

        public static float defaultTextAutoSizingMinRatio
        {
            get => instance?.m_defaultAutoSizeMinRatio ?? 0.5f;
            set { if (instance != null) instance.m_defaultAutoSizeMinRatio = value; }
        }

        public static float defaultTextAutoSizingMaxRatio
        {
            get => instance?.m_defaultAutoSizeMaxRatio ?? 2f;
            set { if (instance != null) instance.m_defaultAutoSizeMaxRatio = value; }
        }

        public static Vector2 defaultTextMeshProTextContainerSize
        {
            get => instance?.m_defaultTextMeshProTextContainerSize ?? new Vector2(20f, 5f);
            set { if (instance != null) instance.m_defaultTextMeshProTextContainerSize = value; }
        }

        public static Vector2 defaultTextMeshProUITextContainerSize
        {
            get => instance?.m_defaultTextMeshProUITextContainerSize ?? new Vector2(200f, 50f);
            set { if (instance != null) instance.m_defaultTextMeshProUITextContainerSize = value; }
        }

        public static bool autoSizeTextContainer
        {
            get => instance?.m_autoSizeTextContainer ?? false;
            set { if (instance != null) instance.m_autoSizeTextContainer = value; }
        }

        public static bool isTextObjectScaleStatic
        {
            get => instance?.m_IsTextObjectScaleStatic ?? false;
            set { if (instance != null) instance.m_IsTextObjectScaleStatic = value; }
        }

        public static List<TMP_FontAsset> fallbackFontAssets
        {
            get => instance?.m_fallbackFontAssets;
            set { if (instance != null) instance.m_fallbackFontAssets = value; }
        }

        public static bool matchMaterialPreset
        {
            get => instance?.m_matchMaterialPreset ?? false;
            set { if (instance != null) instance.m_matchMaterialPreset = value; }
        }

        public static bool hideSubTextObjects
        {
            get => instance?.m_HideSubTextObjects ?? true;
            set { if (instance != null) instance.m_HideSubTextObjects = value; }
        }

        public static TMP_SpriteAsset defaultSpriteAsset
        {
            get => instance?.m_defaultSpriteAsset;
            set { if (instance != null) instance.m_defaultSpriteAsset = value; }
        }

        public static string defaultSpriteAssetPath
        {
            get => instance?.m_defaultSpriteAssetPath;
            set { if (instance != null) instance.m_defaultSpriteAssetPath = value; }
        }

        public static bool enableEmojiSupport
        {
            get => instance?.m_enableEmojiSupport ?? true;
            set { if (instance != null) instance.m_enableEmojiSupport = value; }
        }

        public static uint missingCharacterSpriteUnicode
        {
            get => instance?.m_MissingCharacterSpriteUnicode ?? 0u;
            set { if (instance != null) instance.m_MissingCharacterSpriteUnicode = value; }
        }

        public static List<TMP_Asset> emojiFallbackTextAssets
        {
            get => instance?.m_EmojiFallbackTextAssets;
            set { if (instance != null) instance.m_EmojiFallbackTextAssets = value; }
        }

        public static string defaultColorGradientPresetsPath
        {
            get => instance?.m_defaultColorGradientPresetsPath;
            set { if (instance != null) instance.m_defaultColorGradientPresetsPath = value; }
        }

        public static TMP_StyleSheet defaultStyleSheet
        {
            get => instance?.m_defaultStyleSheet;
            set { if (instance != null) instance.m_defaultStyleSheet = value; }
        }

        public static string styleSheetsResourcePath
        {
            get => instance?.m_StyleSheetsResourcePath;
            set { if (instance != null) instance.m_StyleSheetsResourcePath = value; }
        }

        public static TextAsset leadingCharacters
        {
            get => instance?.m_leadingCharacters;
            set { if (instance != null) instance.m_leadingCharacters = value; }
        }

        public static TextAsset followingCharacters
        {
            get => instance?.m_followingCharacters;
            set { if (instance != null) instance.m_followingCharacters = value; }
        }

        // The characters that may not start a line (leading) and that may not end one
        // (following), read from the two text assets on first use.
        public static LineBreakingTable linebreakingRules
        {
            get
            {
                var settings = instance;
                if (settings == null) return null;
                if (settings.m_linebreakingRules == null) LoadLinebreakingRules();
                return settings.m_linebreakingRules;
            }
        }

        public static bool useModernHangulLineBreakingRules
        {
            get => instance?.m_UseModernHangulLineBreakingRules ?? false;
            set { if (instance != null) instance.m_UseModernHangulLineBreakingRules = value; }
        }

        public static void LoadLinebreakingRules()
        {
            var settings = instance;
            if (settings == null) return;
            settings.m_linebreakingRules = new LineBreakingTable
            {
                leadingCharacters = CodePointSet(settings.m_leadingCharacters),
                followingCharacters = CodePointSet(settings.m_followingCharacters),
            };
        }

        static HashSet<uint> CodePointSet(TextAsset asset)
        {
            var set = new HashSet<uint>();
            var text = asset != null ? asset.text : null;
            if (string.IsNullOrEmpty(text)) return set;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    set.Add((uint)char.ConvertToUtf32(text[i], text[i + 1]));
                    i++;
                }
                else set.Add(text[i]);
            }
            return set;
        }
    }
}
