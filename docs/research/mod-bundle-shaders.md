# Shaders in mods' AssetBundles

Which cached Workshop mods (`out/ws/<id>/`, 2,120 mods) ship AssetBundles with Shader objects, and the shader platforms each shader carries programs for, from `tools/ShaderSurvey bundles out/ws` (2026-10-30). U60 is built with Unity 6000.3.17f1. The OpenGL device runs a shader's GLCore programs (platform 15); the Vulkan device runs its Vulkan programs (18) when they read as U60's, else its GLCore programs compiled to SPIR-V (ARCHITECTURE §3.5, "Programs"). "Vulkan" below says whether the first Vulkan vertex variant of each pass reads as a U60 program entry, the check the Vulkan device makes when it chooses a shader's route.

## Summary

- **34 mods** ship bundles with shaders (364 shader objects in all, counting each bundle's copy).
- **5 mods** have shaders with no GLCore programs in any of their bundles; neither device draws those shaders today: their bundles carry only D3D11 or Metal programs for them: 2815406414 (True Tiles (texture mod): 2 shaders), 2968184715 (Portal Network: 3 shaders), 3152316922 (Spring Festival Pack: 3 shaders), 3519596146 (Proper Cyrillic Fonts: 2 shaders), 3732422991 (StorageNetwork-储存网络: 2 shaders).
- **13 mods** have shaders with GLCore programs but no Vulkan programs U60's player reads (other Unity versions' entries); the Vulkan device compiles their GLCore programs: 2119648603 (Font Loader for Traditional Chinese), 2787598894 (The Golden Throne (Buffed gold lavatories)), 2869608898 (Bio-Inks: Rechargeable Printing Pod), 2921809973 (Backwalls), 2966940337 (传送端口（Transfer Port）), 2977728791 (Preset Manager), 3135364297 (Japanese Font Patch), 3179880371 (The Capsules 2.0), 3371129389 (传送端口修复(TransferPost Fix)), 3433108302 (Chaos Events for Twitch Integration), 3519386331 ([FIXED]传送端口 (Transfer Port)), 3747431307 (WannaPrint: Bio-Inks), 3753518460 (Backwalls Fixed).
- **16 mods** have only shaders with readable Vulkan programs.

Mods often ship one bundle per platform and load the one for `Application.platform` (3561866116's `fonts_bundle_win` carries D3D11 programs only, its `fonts_bundle_other` GLCore and Vulkan). The summary counts a shader as having GLCore or Vulkan programs when any of the mod's bundles has them; which bundle a mod loads on Windows is outside this survey.

## Table

| Mod | Title | Bundle | Shader | Platforms | Vulkan | Unity |
|---|---|---|---|---|---|---|
| 2119648603 | Font Loader for Traditional Chinese | `Assets/other/font` | TextMeshPro/Distance Field | Metal (14), GLCore (15) |  | 2018.4.23f1 |
| 2119648603 | Font Loader for Traditional Chinese | `Assets/other/font` | TextMeshPro/Mobile/Distance Field | Metal (14), GLCore (15) |  | 2018.4.23f1 |
| 2119648603 | Font Loader for Traditional Chinese | `Assets/win/font` | TextMeshPro/Distance Field | D3D11 (4) |  | 2018.4.23f1 |
| 2119648603 | Font Loader for Traditional Chinese | `Assets/win/font` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2018.4.23f1 |
| 2660131829 | Decor Pack I - Classy Glass | `archived_versions/697549-1.6.3.1/assets/linux/decorpacki_assets` | Legacy Shaders/Particles/Additive (Soft) | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `archived_versions/697549-1.6.3.1/assets/linux/decorpacki_assets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `archived_versions/697549-1.6.3.1/assets/linux/decorpacki_assets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `archived_versions/697549-1.6.3.1/assets/mac/decorpacki_assets` | Legacy Shaders/Particles/Additive (Soft) | Metal (14) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `archived_versions/697549-1.6.3.1/assets/mac/decorpacki_assets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `archived_versions/697549-1.6.3.1/assets/mac/decorpacki_assets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `archived_versions/697549-1.6.3.1/assets/windows/decorpacki_assets` | Legacy Shaders/Particles/Additive (Soft) | D3D11 (4) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `archived_versions/697549-1.6.3.1/assets/windows/decorpacki_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `archived_versions/697549-1.6.3.1/assets/windows/decorpacki_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `assets/linux/decorpacki_assets` | Legacy Shaders/Particles/Additive (Soft) | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `assets/linux/decorpacki_assets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `assets/linux/decorpacki_assets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `assets/mac/decorpacki_assets` | Legacy Shaders/Particles/Additive (Soft) | Metal (14) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `assets/mac/decorpacki_assets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `assets/mac/decorpacki_assets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `assets/windows/decorpacki_assets` | Legacy Shaders/Particles/Additive (Soft) | D3D11 (4) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `assets/windows/decorpacki_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2660131829 | Decor Pack I - Classy Glass | `assets/windows/decorpacki_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2787598894 | The Golden Throne (Buffed gold lavatories) | `archived_versions/v1.2.3.0-640445/assets/goldenthroneassets` | Particles/Standard Unlit | D3D11 (4) |  | 2020.3.11f1 |
| 2787598894 | The Golden Throne (Buffed gold lavatories) | `archived_versions/v1.2.3.0-640445/assets/linux/goldenthroneassets` | Particles/Standard Unlit | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2787598894 | The Golden Throne (Buffed gold lavatories) | `archived_versions/v1.2.3.0-640445/assets/mac/goldenthroneassets` | Particles/Standard Unlit | Metal (14) |  | 2020.3.30f1 |
| 2787598894 | The Golden Throne (Buffed gold lavatories) | `archived_versions/v1.2.3.0-640445/assets/windows/goldenthroneassets` | Particles/Standard Unlit | D3D11 (4) |  | 2020.3.30f1 |
| 2787598894 | The Golden Throne (Buffed gold lavatories) | `assets/goldenthroneassets` | Particles/Standard Unlit | D3D11 (4) |  | 2020.3.11f1 |
| 2787598894 | The Golden Throne (Buffed gold lavatories) | `assets/linux/goldenthroneassets` | Particles/Standard Unlit | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2787598894 | The Golden Throne (Buffed gold lavatories) | `assets/mac/goldenthroneassets` | Particles/Standard Unlit | Metal (14) |  | 2020.3.30f1 |
| 2787598894 | The Golden Throne (Buffed gold lavatories) | `assets/windows/goldenthroneassets` | Particles/Standard Unlit | D3D11 (4) |  | 2020.3.30f1 |
| 2815406414 | True Tiles (texture mod) | `archived_versions/truetiles-v1.4/assets/truetilesassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 2815406414 | True Tiles (texture mod) | `archived_versions/truetiles-v1.4/assets/truetilesassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 2815406414 | True Tiles (texture mod) | `assets/truetilesassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 2815406414 | True Tiles (texture mod) | `assets/truetilesassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 2837919908 | Rocketry Expanded | `archived_versions/U58/assets/linux/rocketryexpanded_ui_assets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `archived_versions/U58/assets/linux/rocketryexpanded_ui_assets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `archived_versions/U58/assets/mac/rocketryexpanded_ui_assets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `archived_versions/U58/assets/mac/rocketryexpanded_ui_assets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `archived_versions/U58/assets/windows/rocketryexpanded_ui_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `archived_versions/U58/assets/windows/rocketryexpanded_ui_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `assets/linux/rocketryexpanded_ui_assets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `assets/linux/rocketryexpanded_ui_assets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `assets/mac/rocketryexpanded_ui_assets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `assets/mac/rocketryexpanded_ui_assets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `assets/windows/rocketryexpanded_ui_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2837919908 | Rocketry Expanded | `assets/windows/rocketryexpanded_ui_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U57/assets/linux/dss_uiassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U57/assets/linux/dss_uiassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U57/assets/mac/dss_uiassets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U57/assets/mac/dss_uiassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U57/assets/windows/dss_uiassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U57/assets/windows/dss_uiassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U58/assets/linux/dss_uiassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U58/assets/linux/dss_uiassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U58/assets/mac/dss_uiassets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U58/assets/mac/dss_uiassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U58/assets/windows/dss_uiassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `archived_versions/U58/assets/windows/dss_uiassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2856555858 | Duplicant Stat Selector | `assets/linux/dss_uiassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.3.8f1 |
| 2856555858 | Duplicant Stat Selector | `assets/linux/dss_uiassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.3.8f1 |
| 2856555858 | Duplicant Stat Selector | `assets/mac/dss_uiassets` | TextMeshPro/Distance Field | Metal (14) |  | 6000.3.8f1 |
| 2856555858 | Duplicant Stat Selector | `assets/mac/dss_uiassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 6000.3.8f1 |
| 2856555858 | Duplicant Stat Selector | `assets/windows/dss_uiassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.8f1 |
| 2856555858 | Duplicant Stat Selector | `assets/windows/dss_uiassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.8f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/archived_versions/v1.5.7-616718/assets/linux/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/archived_versions/v1.5.7-616718/assets/linux/pprechargeassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/archived_versions/v1.5.7-616718/assets/linux/pprechargeassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/archived_versions/v1.5.7-616718/assets/mac/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | Metal (14) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/archived_versions/v1.5.7-616718/assets/mac/pprechargeassets` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/archived_versions/v1.5.7-616718/assets/mac/pprechargeassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/archived_versions/v1.5.7-616718/assets/windows/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | D3D11 (4) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/archived_versions/v1.5.7-616718/assets/windows/pprechargeassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/archived_versions/v1.5.7-616718/assets/windows/pprechargeassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/assets/linux/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/assets/linux/pprechargeassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/assets/linux/pprechargeassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/assets/mac/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | Metal (14) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/assets/mac/pprechargeassets` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/assets/mac/pprechargeassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/assets/windows/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | D3D11 (4) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/assets/windows/pprechargeassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `archived_versions/v1.5.8/assets/windows/pprechargeassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `assets/linux/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `assets/linux/pprechargeassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `assets/linux/pprechargeassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `assets/mac/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | Metal (14) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `assets/mac/pprechargeassets` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `assets/mac/pprechargeassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `assets/windows/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | D3D11 (4) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `assets/windows/pprechargeassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2869608898 | Bio-Inks: Rechargeable Printing Pod | `assets/windows/pprechargeassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/linux/backwallassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/linux/backwallassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/linux/backwallassets` | Unlit/TileShader | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/linux/backwalls_tileshader` | Backwalls/SGTRainbow | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/linux/backwalls_tileshader` | Backwalls/TileShader | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/mac/backwallassets` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/mac/backwallassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/mac/backwallassets` | Unlit/TileShader | Metal (14) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/mac/backwalls_tileshader` | Backwalls/SGTRainbow | Metal (14) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/mac/backwalls_tileshader` | Backwalls/TileShader | Metal (14) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/windows/backwallassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/windows/backwallassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/windows/backwallassets` | Unlit/TileShader | D3D11 (4) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/windows/backwalls_tileshader` | Backwalls/SGTRainbow | D3D11 (4) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `archived_versions/v1.5.2-697549/assets/windows/backwalls_tileshader` | Backwalls/TileShader | D3D11 (4) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/linux/backwallassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/linux/backwallassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/linux/backwallassets` | Unlit/TileShader | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/linux/backwalls_tileshader` | Backwalls/SGTRainbow | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/linux/backwalls_tileshader` | Backwalls/TileShader | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/mac/backwallassets` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/mac/backwallassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/mac/backwallassets` | Unlit/TileShader | Metal (14) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/mac/backwalls_tileshader` | Backwalls/SGTRainbow | Metal (14) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/mac/backwalls_tileshader` | Backwalls/TileShader | Metal (14) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/windows/backwallassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/windows/backwallassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/windows/backwallassets` | Unlit/TileShader | D3D11 (4) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/windows/backwalls_tileshader` | Backwalls/SGTRainbow | D3D11 (4) |  | 2020.3.30f1 |
| 2921809973 | Backwalls | `assets/windows/backwalls_tileshader` | Backwalls/TileShader | D3D11 (4) |  | 2020.3.30f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `archived_versions/U58/assets/linux/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `archived_versions/U58/assets/linux/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `archived_versions/U58/assets/mac/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `archived_versions/U58/assets/mac/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `archived_versions/U58/assets/windows/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `archived_versions/U58/assets/windows/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `assets/linux/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.3.8f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `assets/linux/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.3.8f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `assets/mac/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Distance Field | Metal (14) |  | 6000.3.8f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `assets/mac/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 6000.3.8f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `assets/windows/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.8f1 |
| 2947429297 | Cluster Generation Manager [Vanilla/DLC] | `assets/windows/clustergenerationsettingsmanager_menuassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.8f1 |
| 2966940337 | 传送端口（Transfer Port） | `assets/linux/transferport` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2966940337 | 传送端口（Transfer Port） | `assets/linux/transferport` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 2966940337 | 传送端口（Transfer Port） | `assets/mac/transferport` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2966940337 | 传送端口（Transfer Port） | `assets/mac/transferport` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 2966940337 | 传送端口（Transfer Port） | `assets/windows/transferport` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2966940337 | 传送端口（Transfer Port） | `assets/windows/transferport` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 2968184715 | Portal Network | `assets/windows/temperatureeffects` | Cartoon FX/Remaster/Particle Procedural Glow | D3D11 (4) |  | 2020.3.30f1c1 |
| 2968184715 | Portal Network | `assets/windows/temperatureeffects` | Cartoon FX/Remaster/Particle Procedural Ring | D3D11 (4) |  | 2020.3.30f1c1 |
| 2968184715 | Portal Network | `assets/windows/temperatureeffects` | Cartoon FX/Remaster/Particle Ubershader | D3D11 (4) |  | 2020.3.30f1c1 |
| 2977728791 | Preset Manager | `archived_versions/preNov24/assets/linux/dupe_prio_preset_window` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.25f1 |
| 2977728791 | Preset Manager | `archived_versions/preNov24/assets/linux/dupe_prio_preset_window` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.25f1 |
| 2977728791 | Preset Manager | `archived_versions/preNov24/assets/mac/dupe_prio_preset_window` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.25f1 |
| 2977728791 | Preset Manager | `archived_versions/preNov24/assets/mac/dupe_prio_preset_window` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.25f1 |
| 2977728791 | Preset Manager | `archived_versions/preNov24/assets/windows/dupe_prio_preset_window` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.25f1 |
| 2977728791 | Preset Manager | `archived_versions/preNov24/assets/windows/dupe_prio_preset_window` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.25f1 |
| 2977728791 | Preset Manager | `assets/linux/dupe_prio_preset_window` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.25f1 |
| 2977728791 | Preset Manager | `assets/linux/dupe_prio_preset_window` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.25f1 |
| 2977728791 | Preset Manager | `assets/mac/dupe_prio_preset_window` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.25f1 |
| 2977728791 | Preset Manager | `assets/mac/dupe_prio_preset_window` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.25f1 |
| 2977728791 | Preset Manager | `assets/windows/dupe_prio_preset_window` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.25f1 |
| 2977728791 | Preset Manager | `assets/windows/dupe_prio_preset_window` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.25f1 |
| 3046440992 | 字型模組。新 | `Assets/linux/font` | TextMeshPro/Distance Field | Metal (14), GLCore (15) |  | 2018.4.23f1 |
| 3046440992 | 字型模組。新 | `Assets/linux/font` | TextMeshPro/Mobile/Distance Field | Metal (14), GLCore (15) |  | 2018.4.23f1 |
| 3046440992 | 字型模組。新 | `Assets/linux/openhuninn.u6` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.0.70f1 |
| 3046440992 | 字型模組。新 | `Assets/linux/openhuninn.u6` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.0.70f1 |
| 3046440992 | 字型模組。新 | `Assets/mac/font` | TextMeshPro/Distance Field | Metal (14), GLCore (15) |  | 2018.4.23f1 |
| 3046440992 | 字型模組。新 | `Assets/mac/font` | TextMeshPro/Mobile/Distance Field | Metal (14), GLCore (15) |  | 2018.4.23f1 |
| 3046440992 | 字型模組。新 | `Assets/mac/openhuninn.u6` | TextMeshPro/Distance Field | Metal (14) |  | 6000.3.11f1 |
| 3046440992 | 字型模組。新 | `Assets/mac/openhuninn.u6` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 6000.3.11f1 |
| 3046440992 | 字型模組。新 | `Assets/win/font` | TextMeshPro/Distance Field | D3D11 (4) |  | 2018.4.23f1 |
| 3046440992 | 字型模組。新 | `Assets/win/font` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2018.4.23f1 |
| 3046440992 | 字型模組。新 | `Assets/win/openhuninn.u6` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.11f1 |
| 3046440992 | 字型模組。新 | `archived_versions/pre2026mar/Assets/other/font` | TextMeshPro/Distance Field | Metal (14), GLCore (15) |  | 2018.4.23f1 |
| 3046440992 | 字型模組。新 | `archived_versions/pre2026mar/Assets/other/font` | TextMeshPro/Mobile/Distance Field | Metal (14), GLCore (15) |  | 2018.4.23f1 |
| 3046440992 | 字型模組。新 | `archived_versions/pre2026mar/Assets/other/openhuninn` | TextMeshPro/Mobile/Distance Field | Metal (14), GLCore (15) |  | 2018.4.14f1 |
| 3046440992 | 字型模組。新 | `archived_versions/pre2026mar/Assets/win/font` | TextMeshPro/Distance Field | D3D11 (4) |  | 2018.4.23f1 |
| 3046440992 | 字型模組。新 | `archived_versions/pre2026mar/Assets/win/font` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2018.4.23f1 |
| 3046440992 | 字型模組。新 | `archived_versions/pre2026mar/Assets/win/openhuninn` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2018.4.36f1 |
| 3135364297 | Japanese Font Patch | `assets/linux/fonts.bundle` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: the shader does not parse as U60's | 2018.3.0f1 |
| 3135364297 | Japanese Font Patch | `assets/linux/fonts.bundle` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: the shader does not parse as U60's | 2018.3.0f1 |
| 3135364297 | Japanese Font Patch | `assets/mac/fonts.bundle` | TextMeshPro/Distance Field | Metal (14), GLCore (15) |  | 2018.3.0f1 |
| 3135364297 | Japanese Font Patch | `assets/mac/fonts.bundle` | TextMeshPro/Mobile/Distance Field | Metal (14), GLCore (15) |  | 2018.3.0f1 |
| 3135364297 | Japanese Font Patch | `assets/win/fonts.bundle` | TextMeshPro/Distance Field | D3D11 (4) |  | 2018.3.0f1 |
| 3135364297 | Japanese Font Patch | `assets/win/fonts.bundle` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2018.3.0f1 |
| 3152316922 | Spring Festival Pack | `assets/linux/snoweffects` | Cartoon FX/Remaster/Particle Procedural Glow | D3D11 (4) |  | 2020.3.30f1c1 |
| 3152316922 | Spring Festival Pack | `assets/linux/snoweffects` | Cartoon FX/Remaster/Particle Ubershader | D3D11 (4) |  | 2020.3.30f1c1 |
| 3152316922 | Spring Festival Pack | `assets/linux/snoweffects` | Standard | D3D11 (4) |  | 2020.3.30f1c1 |
| 3152316922 | Spring Festival Pack | `assets/mac/snoweffects` | Cartoon FX/Remaster/Particle Procedural Glow | D3D11 (4) |  | 2020.3.30f1c1 |
| 3152316922 | Spring Festival Pack | `assets/mac/snoweffects` | Cartoon FX/Remaster/Particle Ubershader | D3D11 (4) |  | 2020.3.30f1c1 |
| 3152316922 | Spring Festival Pack | `assets/mac/snoweffects` | Standard | D3D11 (4) |  | 2020.3.30f1c1 |
| 3152316922 | Spring Festival Pack | `assets/windows/snoweffects` | Cartoon FX/Remaster/Particle Procedural Glow | D3D11 (4) |  | 2020.3.30f1c1 |
| 3152316922 | Spring Festival Pack | `assets/windows/snoweffects` | Cartoon FX/Remaster/Particle Ubershader | D3D11 (4) |  | 2020.3.30f1c1 |
| 3152316922 | Spring Festival Pack | `assets/windows/snoweffects` | Standard | D3D11 (4) |  | 2020.3.30f1c1 |
| 3179880371 | The Capsules 2.0 | `assets/linux/capsule` | Cartoon FX/Remaster/Particle Procedural Glow | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/linux/capsule` | Cartoon FX/Remaster/Particle Ubershader | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/linux/capsule` | Hovl/Particles/Add_CenterGlow | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/linux/capsule` | Hovl/Particles/Blend_CenterGlow | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/linux/capsule` | Legacy Shaders/Particles/Alpha Blended Premultiply | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/linux/capsule` | Particles/Standard Unlit | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/linux/capsule` | Standard | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/mac/capsule` | Cartoon FX/Remaster/Particle Procedural Glow | Metal (14) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/mac/capsule` | Cartoon FX/Remaster/Particle Ubershader | Metal (14) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/mac/capsule` | Hovl/Particles/Add_CenterGlow | Metal (14) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/mac/capsule` | Hovl/Particles/Blend_CenterGlow | Metal (14) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/mac/capsule` | Legacy Shaders/Particles/Alpha Blended Premultiply | Metal (14) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/mac/capsule` | Particles/Standard Unlit | Metal (14) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/mac/capsule` | Standard | Metal (14) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/windows/capsule` | Cartoon FX/Remaster/Particle Procedural Glow | D3D11 (4) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/windows/capsule` | Cartoon FX/Remaster/Particle Ubershader | D3D11 (4) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/windows/capsule` | Hovl/Particles/Add_CenterGlow | D3D11 (4) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/windows/capsule` | Hovl/Particles/Blend_CenterGlow | D3D11 (4) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/windows/capsule` | Legacy Shaders/Particles/Alpha Blended Premultiply | D3D11 (4) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/windows/capsule` | Particles/Standard Unlit | D3D11 (4) |  | 2020.3.30f1 |
| 3179880371 | The Capsules 2.0 | `assets/windows/capsule` | Standard | D3D11 (4) |  | 2020.3.30f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `archived_versions/U57/assets/linux/mpm_ui` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `archived_versions/U57/assets/linux/mpm_ui` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `archived_versions/U57/assets/mac/mpm_ui` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `archived_versions/U57/assets/mac/mpm_ui` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `archived_versions/U57/assets/windows/mpm_ui` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `archived_versions/U57/assets/windows/mpm_ui` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `assets/linux/mpm_ui` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `assets/linux/mpm_ui` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `assets/mac/mpm_ui` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `assets/mac/mpm_ui` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `assets/windows/mpm_ui` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3281716506 | Mod Preset Manager (Light) [MPML] | `assets/windows/mpm_ui` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3371129389 | 传送端口修复(TransferPost Fix) | `assets/linux/transferport` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3371129389 | 传送端口修复(TransferPost Fix) | `assets/linux/transferport` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3371129389 | 传送端口修复(TransferPost Fix) | `assets/mac/transferport` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 3371129389 | 传送端口修复(TransferPost Fix) | `assets/mac/transferport` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 3371129389 | 传送端口修复(TransferPost Fix) | `assets/windows/transferport` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 3371129389 | 传送端口修复(TransferPost Fix) | `assets/windows/transferport` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 3427974009 | Synchronized Building Groups | `assets/linux/buildingsettingsync_assets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/linux/buildingsettingsync_assets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/linux/buildingsettingsync_ui_assets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/linux/buildingsettingsync_ui_assets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/mac/buildingsettingsync_assets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/mac/buildingsettingsync_assets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/mac/buildingsettingsync_ui_assets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/mac/buildingsettingsync_ui_assets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/windows/buildingsettingsync_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/windows/buildingsettingsync_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/windows/buildingsettingsync_ui_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/windows/buildingsettingsync_ui_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/windows/rocketryexpanded_ui_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3427974009 | Synchronized Building Groups | `assets/windows/rocketryexpanded_ui_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3433108302 | Chaos Events for Twitch Integration | `assets/linux/chaos_twitch_assets` | Mobile/Particles/Additive | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.25f1 |
| 3433108302 | Chaos Events for Twitch Integration | `assets/linux/chaos_twitch_assets` | Mobile/Particles/Alpha Blended | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.25f1 |
| 3433108302 | Chaos Events for Twitch Integration | `assets/mac/chaos_twitch_assets` | Mobile/Particles/Additive | Metal (14) |  | 2020.3.25f1 |
| 3433108302 | Chaos Events for Twitch Integration | `assets/mac/chaos_twitch_assets` | Mobile/Particles/Alpha Blended | Metal (14) |  | 2020.3.25f1 |
| 3433108302 | Chaos Events for Twitch Integration | `assets/windows/chaos_twitch_assets` | Mobile/Particles/Additive | D3D11 (4) |  | 2020.3.25f1 |
| 3433108302 | Chaos Events for Twitch Integration | `assets/windows/chaos_twitch_assets` | Mobile/Particles/Alpha Blended | D3D11 (4) |  | 2020.3.25f1 |
| 3468585385 | Blueprints Expanded | `archived_versions/U57/assets/linux/blueprints_ui` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3468585385 | Blueprints Expanded | `archived_versions/U57/assets/linux/blueprints_ui` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3468585385 | Blueprints Expanded | `archived_versions/U57/assets/mac/blueprints_ui` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3468585385 | Blueprints Expanded | `archived_versions/U57/assets/mac/blueprints_ui` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3468585385 | Blueprints Expanded | `archived_versions/U57/assets/windows/blueprints_ui` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3468585385 | Blueprints Expanded | `archived_versions/U57/assets/windows/blueprints_ui` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3468585385 | Blueprints Expanded | `assets/linux/blueprints_ui` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.3.8f1 |
| 3468585385 | Blueprints Expanded | `assets/linux/blueprints_ui` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.3.8f1 |
| 3468585385 | Blueprints Expanded | `assets/mac/blueprints_ui` | TextMeshPro/Distance Field | Metal (14) |  | 6000.3.8f1 |
| 3468585385 | Blueprints Expanded | `assets/mac/blueprints_ui` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 6000.3.8f1 |
| 3468585385 | Blueprints Expanded | `assets/windows/blueprints_ui` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.8f1 |
| 3468585385 | Blueprints Expanded | `assets/windows/blueprints_ui` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.8f1 |
| 3478214979 | Fabricator Recipe Control | `archived_versions/U58/assets/linux/complexfabricatorribbon_assets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `archived_versions/U58/assets/linux/complexfabricatorribbon_assets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `archived_versions/U58/assets/mac/complexfabricatorribbon_assets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `archived_versions/U58/assets/mac/complexfabricatorribbon_assets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `archived_versions/U58/assets/windows/complexfabricatorribbon_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `archived_versions/U58/assets/windows/complexfabricatorribbon_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `assets/linux/complexfabricatorribbon_assets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `assets/linux/complexfabricatorribbon_assets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `assets/mac/complexfabricatorribbon_assets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `assets/mac/complexfabricatorribbon_assets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `assets/windows/complexfabricatorribbon_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3478214979 | Fabricator Recipe Control | `assets/windows/complexfabricatorribbon_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3519386331 | [FIXED]传送端口 (Transfer Port) | `assets/linux/transferport` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3519386331 | [FIXED]传送端口 (Transfer Port) | `assets/linux/transferport` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3519386331 | [FIXED]传送端口 (Transfer Port) | `assets/mac/transferport` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 3519386331 | [FIXED]传送端口 (Transfer Port) | `assets/mac/transferport` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 3519386331 | [FIXED]传送端口 (Transfer Port) | `assets/windows/transferport` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 3519386331 | [FIXED]传送端口 (Transfer Port) | `assets/windows/transferport` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 3519596146 | Proper Cyrillic Fonts | `Assets/font` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3519596146 | Proper Cyrillic Fonts | `Assets/font` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3557584850 | Ronivan's Legacy - Industrial Revolution | `assets/linux/ronivan_aio` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.3.8f1 |
| 3557584850 | Ronivan's Legacy - Industrial Revolution | `assets/linux/ronivan_aio` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.3.8f1 |
| 3557584850 | Ronivan's Legacy - Industrial Revolution | `assets/mac/ronivan_aio` | TextMeshPro/Distance Field | Metal (14) |  | 6000.3.8f1 |
| 3557584850 | Ronivan's Legacy - Industrial Revolution | `assets/mac/ronivan_aio` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 6000.3.8f1 |
| 3557584850 | Ronivan's Legacy - Industrial Revolution | `assets/windows/ronivan_aio` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.8f1 |
| 3557584850 | Ronivan's Legacy - Industrial Revolution | `assets/windows/ronivan_aio` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.8f1 |
| 3561866116 | Font Replacement in Simplified Chinese | `assets/fonts_bundle_other` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.3.5f2 |
| 3561866116 | Font Replacement in Simplified Chinese | `assets/fonts_bundle_other` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 6000.3.5f2 |
| 3561866116 | Font Replacement in Simplified Chinese | `assets/fonts_bundle_win` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3561866116 | Font Replacement in Simplified Chinese | `assets/fonts_bundle_win` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3565090654 | Attribute Info Screen | `assets/linux/attributescreen_assets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3565090654 | Attribute Info Screen | `assets/linux/attributescreen_assets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3565090654 | Attribute Info Screen | `assets/mac/attributescreen_assets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3565090654 | Attribute Info Screen | `assets/mac/attributescreen_assets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3565090654 | Attribute Info Screen | `assets/windows/attributescreen_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3565090654 | Attribute Info Screen | `assets/windows/attributescreen_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3568684741 | Forcefield Projectors | `assets/linux/forcefieldtile_assets` | Hidden/Shader Graph/FallbackError | GLCore (15), Vulkan (18) | read as U60's | 6000.3.8f1 |
| 3568684741 | Forcefield Projectors | `assets/linux/forcefieldtile_assets` | Shader Graphs/ForceField | GLCore (15), Vulkan (18) | read as U60's | 6000.3.8f1 |
| 3568684741 | Forcefield Projectors | `assets/mac/forcefieldtile_assets` | Hidden/Shader Graph/FallbackError | Metal (14) |  | 6000.3.8f1 |
| 3568684741 | Forcefield Projectors | `assets/mac/forcefieldtile_assets` | Shader Graphs/ForceField | Metal (14) |  | 6000.3.8f1 |
| 3568684741 | Forcefield Projectors | `assets/windows/forcefieldtile_assets` | Hidden/Shader Graph/FallbackError | D3D11 (4) |  | 6000.3.8f1 |
| 3568684741 | Forcefield Projectors | `assets/windows/forcefieldtile_assets` | Shader Graphs/ForceField | D3D11 (4) |  | 6000.3.8f1 |
| 3630759126 | ONI Together: A Multiplayer Mod | `assets/linux/oni_mp_ui_assets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3630759126 | ONI Together: A Multiplayer Mod | `assets/linux/oni_mp_ui_assets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3630759126 | ONI Together: A Multiplayer Mod | `assets/mac/oni_mp_ui_assets` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3630759126 | ONI Together: A Multiplayer Mod | `assets/mac/oni_mp_ui_assets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3630759126 | ONI Together: A Multiplayer Mod | `assets/windows/oni_mp_ui_assets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3630759126 | ONI Together: A Multiplayer Mod | `assets/windows/oni_mp_ui_assets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3637202410 | MAGNET!!! | `assets/linux/magnet` | Clover/MagnetComposite | GLCore (15), Vulkan (18) | read as U60's | 6000.3.5f2 |
| 3637202410 | MAGNET!!! | `assets/linux/magnet` | Clover/MagnetField | GLCore (15), Vulkan (18) | read as U60's | 6000.3.5f2 |
| 3637202410 | MAGNET!!! | `assets/mac/magnet` | Clover/MagnetComposite | Metal (14) |  | 6000.3.5f2 |
| 3637202410 | MAGNET!!! | `assets/mac/magnet` | Clover/MagnetField | Metal (14) |  | 6000.3.5f2 |
| 3637202410 | MAGNET!!! | `assets/windows/magnet` | Clover/MagnetComposite | D3D11 (4), Vulkan (18) | read as U60's | 6000.3.5f2 |
| 3637202410 | MAGNET!!! | `assets/windows/magnet` | Clover/MagnetField | D3D11 (4), Vulkan (18) | read as U60's | 6000.3.5f2 |
| 3732422991 | StorageNetwork-储存网络 | `Assets/AssetBundles/linux/storagenetwork_ui` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3732422991 | StorageNetwork-储存网络 | `Assets/AssetBundles/linux/storagenetwork_ui` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3732422991 | StorageNetwork-储存网络 | `Assets/AssetBundles/mac/storagenetwork_ui` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3732422991 | StorageNetwork-储存网络 | `Assets/AssetBundles/mac/storagenetwork_ui` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3732422991 | StorageNetwork-储存网络 | `Assets/AssetBundles/storagenetwork_ui` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3732422991 | StorageNetwork-储存网络 | `Assets/AssetBundles/storagenetwork_ui` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3732422991 | StorageNetwork-储存网络 | `Assets/AssetBundles/windows/storagenetwork_ui` | TextMeshPro/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3732422991 | StorageNetwork-储存网络 | `Assets/AssetBundles/windows/storagenetwork_ui` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 6000.3.5f2 |
| 3747431307 | WannaPrint: Bio-Inks | `assets/linux/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3747431307 | WannaPrint: Bio-Inks | `assets/linux/pprechargeassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3747431307 | WannaPrint: Bio-Inks | `assets/linux/pprechargeassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3747431307 | WannaPrint: Bio-Inks | `assets/mac/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | Metal (14) |  | 2020.3.30f1 |
| 3747431307 | WannaPrint: Bio-Inks | `assets/mac/pprechargeassets` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 3747431307 | WannaPrint: Bio-Inks | `assets/mac/pprechargeassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 3747431307 | WannaPrint: Bio-Inks | `assets/windows/pprechargeassets` | Legacy Shaders/Particles/Additive (Soft) | D3D11 (4) |  | 2020.3.30f1 |
| 3747431307 | WannaPrint: Bio-Inks | `assets/windows/pprechargeassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 3747431307 | WannaPrint: Bio-Inks | `assets/windows/pprechargeassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/linux/backwallassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/linux/backwallassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/linux/backwallassets` | Unlit/TileShader | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/linux/backwalls_tileshader` | Backwalls/SGTRainbow | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/linux/backwalls_tileshader` | Backwalls/TileShader | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/mac/backwallassets` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/mac/backwallassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/mac/backwallassets` | Unlit/TileShader | Metal (14) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/mac/backwalls_tileshader` | Backwalls/SGTRainbow | Metal (14) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/mac/backwalls_tileshader` | Backwalls/TileShader | Metal (14) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/windows/backwallassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/windows/backwallassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/windows/backwallassets` | Unlit/TileShader | D3D11 (4) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/windows/backwalls_tileshader` | Backwalls/SGTRainbow | D3D11 (4) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `archived_versions/assets/windows/backwalls_tileshader` | Backwalls/TileShader | D3D11 (4) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/linux/backwallassets` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/linux/backwallassets` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/linux/backwallassets` | Unlit/TileShader | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/linux/backwalls_tileshader` | Backwalls/SGTRainbow | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/linux/backwalls_tileshader` | Backwalls/TileShader | GLCore (15), Vulkan (18) | not read: another Unity version's program list | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/mac/backwallassets` | TextMeshPro/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/mac/backwallassets` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/mac/backwallassets` | Unlit/TileShader | Metal (14) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/mac/backwalls_tileshader` | Backwalls/SGTRainbow | Metal (14) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/mac/backwalls_tileshader` | Backwalls/TileShader | Metal (14) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/windows/backwallassets` | TextMeshPro/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/windows/backwallassets` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/windows/backwallassets` | Unlit/TileShader | D3D11 (4) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/windows/backwalls_tileshader` | Backwalls/SGTRainbow | D3D11 (4) |  | 2020.3.30f1 |
| 3753518460 | Backwalls Fixed | `assets/windows/backwalls_tileshader` | Backwalls/TileShader | D3D11 (4) |  | 2020.3.30f1 |
| 3782701870 | [2027/QoL] Automatic Industry 2.5.0 | `assets/linux/ai_building_editor` | TextMeshPro/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3782701870 | [2027/QoL] Automatic Industry 2.5.0 | `assets/linux/ai_building_editor` | TextMeshPro/Mobile/Distance Field | GLCore (15), Vulkan (18) | read as U60's | 2022.3.45f1 |
| 3782701870 | [2027/QoL] Automatic Industry 2.5.0 | `assets/mac/ai_building_editor` | TextMeshPro/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3782701870 | [2027/QoL] Automatic Industry 2.5.0 | `assets/mac/ai_building_editor` | TextMeshPro/Mobile/Distance Field | Metal (14) |  | 2022.3.45f1 |
| 3782701870 | [2027/QoL] Automatic Industry 2.5.0 | `assets/windows/ai_building_editor` | TextMeshPro/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
| 3782701870 | [2027/QoL] Automatic Industry 2.5.0 | `assets/windows/ai_building_editor` | TextMeshPro/Mobile/Distance Field | D3D11 (4) |  | 2022.3.45f1 |
