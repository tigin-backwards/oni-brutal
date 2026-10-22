# Mod triage: play-testing classes for the ranked Workshop mods

Made by `tools/ModTriage` (mod-play-testing.md §3.2) on 2026-10-22, from the Workshop builds in
`out/ws` and the game build 744825. It reads metadata only and never loads a mod. The classes and
flags are the ones of mod-play-testing.md §3.1. Per-mod records, with the reason behind every
class and flag, are in `out/modtriage/ID.json` (not committed).

To make it again, from the repository root:

```sh
dotnet build tools/ModTriage
out/bin/ModTriage/debug/ModTriage --doc docs/research/mod-triage.md    # or: ... ID... for some mods
```

**Which mods.** The ranked rows of `docs/MOD_COMPATIBILITY.md` (ranks 1–500 today; the tool takes
up to `--max-rank 750`) that have a folder in `out/ws`: 416 mods. The 84 without one are 58
flagged by onimaxxing, 23 translations and 3 worldgen mods. For each mod the tool reads the
version the game would load: the root folder, or the `archived_versions/*` entry with the highest
`minimumSupportedBuild` the game meets.

**How classes are set.**
- Merged PLib (in 202 mods, sometimes renamed into the mod's namespace) is removed by its type
  names, as are Harmony and Newtonsoft; separate `PLib*.dll`, `0Harmony.dll` and
  `Newtonsoft.Json.dll` are skipped. Calls from the mod into PLib still count: `POptions` → O,
  `PAction` → K, `PBuilding` → B, `PDialog` → S.
- An author's shared code merged into each of their mods (UtilLibs, Ony's common code, CaiLib,
  AzeLib, FUtility and others in `tools/ModTriage/shared-namespaces.txt`) stays in the scan as
  *helper* code. A mod's screens may derive from it (UtilLibs' `FScreen` → `KScreen`), but its
  own references and patches, the same in every one of those mods (`ModsScreen.BuildDisplay`,
  `MainMenu.OnSpawn`, …), set no class. They are listed in the JSON with `helper: true`.
- Base types of the mod's types are followed into the game's assemblies: `InterfaceTool` → T,
  `KScreen` or `SideScreenContent` → S, `IBuildingConfig` → B, `OverlayModes.Mode` → V.
- Harmony patch targets come from `[HarmonyPatch]` attributes and from `ldtoken` and `ldstr` next
  to `Patch` calls (and in `TargetMethod`). Each maps by its declaring type, as §3.2 says, with
  two refinements: a screen's `OnKeyDown`/`OnKeyUp` counts as K, and a screen's lifecycle method
  (`OnSpawn`, `OnDeactivate`, …) patched by a mod with no UI of its own counts as P.
- P is set when nothing else a player drives is found: no B, T, S, K, V or M.
- `asset_bundle_ui`: a Unity bundle in `assets/`, prefabs loaded from it
  (`AssetBundle.LoadAsset<GameObject>`), and UI in the mod. `touches_ui` also counts a patch
  that counts as S.

**Known limits.**
- These are signals, not a reading of the mod: an agent still confirms the classes from the
  decompiled mod when writing its card (§3.2). A class can come from a single reference, for
  example V from `Shader` or `RenderTexture` used to copy a texture.
- Patch targets found by a lookup the scan cannot follow are missed: 206 `Patch` calls in 53 mods
  have no type next to them (targets kept in fields or tables, patch helpers in other libraries).
- `needs_late_game` is not set: metadata cannot tell whether a feature needs a late save.
- X is only "no code": translations and content-only mods. Multiplayer (ONI Together) and the
  worldgen mods with code are classed by their code; their notes in MOD_COMPATIBILITY still apply.
- Shared helper namespaces were found by counting the mods in `out/ws` that define them; a mod's
  own code that happens to sit in such a namespace is treated as a helper's, unless it holds the
  mod's entry class (its `UserMod2`).

**The pilot (§8) against its sketches.**

| Mod | Sketch | Triage | Difference |
|---|---|---|---|
| Blueprints Expanded | S, T, K, O | T, S, K, O, V | V: its tile previews colour cells through `BlockTileRenderer.GetCellColour` |
| Duplicant Stat Selector | M, S | S, O, M | O: it registers a PLib options dialog (`POptions.RegisterOptions`) |
| Research Queue | K on a vanilla screen | S, K | S: it writes the queue position into each research entry's name (a `LocText`), and patches `ResearchScreen.OnSpawn` |
| Sweep By Type | T, S, O | T, S, K, O | K: a "default sweep" key binding (`PActionManager.CreateAction`) |
| Airlock Door | B | B | – |
| Bigger Camera Zoom Out | V | K, V | K: a transpiler on `ClusterMapScreen.OnKeyDown`, the starmap's scroll zoom |
| Full Miner Yield | P | P | – |

Each extra class was checked in the decompiled mod and is a real surface. None of the sketches'
classes is missing. Duplicant Stat Selector's M comes from its `MinionSelectScreen` patches, and
Blueprints Expanded and DSS lose the M, V and S that UtilLibs' shared tweaks would give them.

<!-- table -->

**416 mods.** Per class (a mod counts once in each of its classes): P 137, B 119, T 49, S 122, K 46, O 152, V 51, M 19, X 24.
Flags: touches_ui 144, asset_bundle_ui 17, plib_options 151, user_menu 30.

| Class set | Mods |
|---|---|
| P | 102 |
| B | 45 |
| B,O | 41 |
| P,O | 35 |
| S | 26 |
| X | 24 |
| S,O | 16 |
| B,S | 9 |
| B,S,O | 9 |
| S,V | 6 |
| T | 6 |
| T,S,K,O | 6 |
| V | 6 |
| O,V | 5 |
| S,O,V | 5 |
| T,S,K,O,V | 5 |
| K | 4 |
| S,K | 4 |
| T,K | 4 |
| T,K,O | 4 |
| T,O | 4 |
| M | 3 |
| S,K,O | 3 |
| T,S | 3 |
| 31 other sets | 41 |

| Rank | ID | Mod | Classes | Flags | Patch targets by class |
|---|---|---|---|---|---|
| 1 | 1717463209 | Bigger Camera Zoom Out | K,V | – | P 1: WattsonMessage.OnDeactivate; K 1: ClusterMapScreen.OnKeyDown; V 3: CameraController.OnPrefabInit, CameraController.SetMaxOrthographicSize, CameraController.ConstrainToWorld |
| 2 | 2018291283 | Mod Updater | K,O,M | touches_ui | P 12: Mod.SetCrashed, Manager.Subscribe, Manager.Update, …; M 2: MainMenu.OnPrefabInit, ModsScreen.BuildDisplay |
| 3 | 1835394161 | [Obsolete] Build Over Plants | P | – | – |
| 4 | 1960996649 | Show Building Ranges | V | – | P 1: GeneratedBuildings.LoadGeneratedBuildings; V 1: CameraController.OnPrefabInit |
| 5 | 1838445101 | DGSM  - Duplicants Generation Settings Manager … | S,M | touches_ui asset_bundle_ui | P 6: BionicUpgradesMonitor.SpawnAndInstallInitialUpgrade, MinionStartingStats.GenerateAptitudes, MinionStartingStats.GenerateAttributes, …; S 5: CharacterContainer.OnSpawn, CharacterSelectionController.InitializeContainers, ImmigrantScreen.Initialize, …; M 1: MinionSelectScreen.OnSpawn |
| 6 | 1893887916 | Research Queue | S,K | touches_ui | P 7: Research.AddTechToQueue, Research.CancelResearch, Research.SetActiveResearch, …; S 1: ResearchScreen.OnSpawn |
| 7 | 1960947963 | Better Info Cards | T,S,K,O | touches_ui | P 16: HoverTextDrawer.BeginDrawing, HoverTextDrawer.BeginShadowBar, HoverTextDrawer.DrawIcon, …; T 2: InterfaceTool.GetObjectUnderCursor, SelectTool.Select; S 1: DetailsScreen.Refresh |
| 8 | 1855163252 | Falling Sand | P | – | P 5: Diggable.OnWorkTick, Diggable.OnCleanUp, UnstableGroundManager.RemoveFromPending, … |
| 9 | 1878896484 | Better Automation Overlay | O,V | touches_ui | P 4: ElementLoader.CopyEntryToElement, Localization.SetLocale, GeneratedBuildings.LoadGeneratedBuildings, …; V 5: OverlayModes.UpdateUI, OverlayModes.FreeUI, OverlayModes.AddUI, … |
| 10 | 1868454866 | No Drop // by @Ony 👾 | P | – | P 4: FetchAreaChore.SetupDeliverables, FetchChore.FindFetchTarget, MinionModifiers.OnBeginChore, … |
| 11 | 2856555858 | Duplicant Stat Selector | S,O,M | touches_ui asset_bundle_ui | P 23: MinionStartingStats.GenerateAttributes, Assets.OnPrefabInit, Traits.OnSpawn, …; S 20: ImmigrantScreen.Initialize, ImmigrantScreen.OnProceed, ImmigrantScreen.OnPressBack, …; M 2: MinionSelectScreen.OnSpawn, MinionSelectScreen.SetDefaultMinionsRoutine |
| 12 | 1881925531 | Queue for Sinks | P | – | P 2: HandSanitizer.OnPrefabInit, OreScrubber.OnPrefabInit |
| 13 | 1863428350 | Sweep By Type | T,S,K,O | touches_ui | P 1: SaveGame.OnPrefabInit; T 2: PlayerController.OnPrefabInit, ToolMenu.CreateBasicTools |
| 14 | 1879935708 | select last care package | S | touches_ui | P 2: SaveGame.OnPrefabInit, Immigration.RandomCarePackage; S 3: ImmigrantScreen.OnProceed, ImmigrantScreen.OnRejectAll, ImmigrantScreen.OnSpawn |
| 15 | 2002421412 | Customizable Speed | S,O | touches_ui | P 2: Game.Load, SpeedControlScreen.OnPrefabInit; S 2: SpeedControlScreen.ResetToolTip, SpeedControlScreen.OnChanged |
| 16 | 2667816955 | Move This Here | B,S | touches_ui | P 2: GeneratedBuildings.LoadGeneratedBuildings, BuildingDef.Instantiate; S 2: ProductInfoScreen.SetMaterials, ResourceRemainingDisplayScreen.GetString |
| 17 | 2094698134 | Airlock Door | B | – | P 4: BaseRoverConfig.OnSpawn, Constructable.OnSpawn, DoorTransitionLayer.BeginTransition, … |
| 18 | 1737859934 | GasOverlay | S,V | touches_ui | S 1: OverlayMenu.InitializeToggles; V 2: SimDebugView.GetOxygenMapColour, OverlayLegend.OnSpawn |
| 19 | 1703611962 | Bigger Building Menu | S | touches_ui | S 1: PlanScreen.ConfigurePanelSize |
| 20 | 1717526174 | Plan Buildings Without Materials | S | touches_ui | S 2: MaterialSelector.AllowInsufficientMaterialBuild, PlanScreen.GetBuildableStateForDef |
| 21 | 3007505281 | Movable Features 位移信标 | T,S,M | touches_ui asset_bundle_ui | P 3: Assets.AddBuildingDef, EntityTemplates.ConfigPlacedEntity, Localization.Initialize; T 1: PlayerController.OnPrefabInit; M 1: ModsScreen.BuildDisplay |
| 22 | 2435244304 | Blueprints fixed | T,S,K,O,V | touches_ui | P 1: Game.DestroyInstances; T 3: PlayerController.OnPrefabInit, ToolMenu.OnPrefabInit, ToolMenu.CreateBasicTools; S 1: FileNameDialog.OnSpawn; V 1: BlockTileRenderer.GetCellColour |
| 23 | 1737893485 | ChainedDeconstruction | P | – | P 1: Deconstructable.TriggerDestroy |
| 24 | 2567122431 | Waste Not, Want Not | P | – | P 8: FetchManager.IsFetchablePickup_Exclude, FetchManager.AddPickupable, FetchManager.IsFetchablePickup, … |
| 25 | 1706181611 | Geyser Calculated Average Output Tooltip | P | – | P 1: Geyser.GetDescriptors |
| 26 | 1843965353 | Mod Manager // by @Ony 👾 | M | touches_ui | P 12: DragMe.OnBeginDrag, DragMe.SetDraggedPosition, KCrashReporter.HandleLog, …; M 3: ModsScreen.BuildDisplay, ModsScreen.OnActivate, ModsScreen.OnDeactivate |
| 27 | 2841963310 | PrintingPodRefresh / 刷新选人 | S | touches_ui | S 1: ImmigrantScreen.OnRejectAll |
| 28 | 929139073 | German Translation [inkl. DLCs] | X | – | – |
| 29 | 1833427385 | WGSM - World Generation Settings Manager // by … | M | touches_ui | P 4: AsteroidDescriptorPanel.EnableClusterLocationLabels, AsteroidDescriptorPanel.SpawnAsteroidLine, SettingsCache.GetRandomTraits, …; M 3: ColonyDestinationSelectScreen.BackClicked, ColonyDestinationSelectScreen.OnCleanUp, ColonyDestinationSelectScreen.OnSpawn |
| 30 | 2815406414 | True Tiles (texture mod) | S,V,M | touches_ui asset_bundle_ui | P 8: Game.OnSpawn, Assets.OnPrefabInit, Db.Initialize, …; V 4: BlockTileRenderer.GetConnectionBits, BlockTileRenderer.MatchesDef, BlockTileRenderer.AddBlock, …; M 1: ModsScreen.BuildDisplay |
| 31 | 1822160024 | Wounded Go To Med Bed | P | – | P 1: WoundMonitor.InitializeStates |
| 32 | 1854704869 | Crack Reacher | P | – | P 2: Db.Initialize, Game.OnPrefabInit |
| 33 | 1715248368 | [Vanilla + DLC] Deconstructable POI Props | P | – | P 6: PropFacilityChandelierConfig.CreatePrefab, VendingMachineConfig.CreatePrefab, GeneShufflerConfig.CreatePrefab, … |
| 34 | 2781902332 | Element Captured Capsule | B,T,S,K,V | touches_ui | P 5: Db.Initialize, StatusItem.GetStatusItemOverlayBySimViewMode, SaveGame.OnPrefabInit, …; T 3: PlayerController.OnPrefabInit, ToolMenu.CreateBasicTools, ToolMenu.OnPrefabInit; S 3: OverlayMenu.InitializeToggles, ProductInfoScreen.SetMaterials, ResourceRemainingDisplayScreen.GetString; V 3: SimDebugView.OnPrefabInit, OverlayScreen.RegisterModes, OverlayLegend.OnSpawn |
| 35 | 2479475997 | Pliers fixed [Obsolete] | T,K | – | P 1: Game.DestroyInstances; T 3: PlayerController.OnPrefabInit, ToolMenu.OnPrefabInit, ToolMenu.CreateBasicTools |
| 37 | 1867707267 | Fast Save | S,O,V | touches_ui | P 15: Game.DelayedSave, ReportManager.OnNightTime, Timelapser.RenderAndPrint, …; S 2: ReportScreen.Refresh, RetiredColonyInfoScreen.ConfigureGraph |
| 38 | 927642162 | +Русский язык [Полный перевод] // by @Ony 👾 | X | – | – |
| 39 | 1198657258 | Português foi Incluído! (PT-BR) | X | – | – |
| 42 | 1832319118 | Suppress Notifications | T,S,O | touches_ui | P 7: Assets.CreatePrefabs, Notifier.Add, Notifier.Remove, …; T 1: PlayerController.OnPrefabInit; S 1: NotificationScreen.AddNotification |
| 43 | 1937854017 | Clean Floors | P,O | – | P 2: Assets.OnPrefabInit, Localization.Initialize |
| 44 | 1718888149 | Better Air Filters | P | – | P 1: AirFilterConfig.CreateBuildingDef |
| 45 | 3280056840 | 最后的补给包-Fix | S | touches_ui | P 2: Immigration.RandomCarePackage, SaveGame.OnPrefabInit; S 7: ImmigrantScreen.OnProceed, ImmigrantScreen.OnRejectAll, ImmigrantScreen.OnSpawn, … |
| 46 | 1874600082 | Efficient Supply | P,O | – | P 2: FetchChore.FindFetchTarget, FetchManager.UpdatePickups |
| 47 | 928606476 | Japanese Language Pack | X | – | – |
| 48 | 2493100777 | Pip Plant Overlay | S,K,V | touches_ui | S 1: OverlayMenu.InitializeToggles; V 3: OverlayLegend.OnSpawn, OverlayScreen.RegisterModes, SimDebugView.OnPrefabInit |
| 49 | 928348692 | Traduction Française | X | – | – |
| 50 | 2590260868 | Smart Drag Tool | T,V | touches_ui | T 6: DragTool.OnLeftClickUp, DragTool.OnDeactivateTool, DragTool.CancelDragging, …; V 1: CameraController.NormalCamUpdate |
| 51 | 2047308624 | No Manual Delivery | S,O | touches_ui | P 16: Pickupable.CouldBePickedUpByMinion, Tinkerable.UpdateChore, BingeEatChore.FindFood, …; S 1: AutomatableSideScreen.IsValidForTarget |
| 52 | 2634633299 | [Vanilla + DLC] Combined Conduit Display | V | – | P 55: GasVentConfig.DoPostConfigureComplete, GasVentHighPressureConfig.DoPostConfigureComplete, GasValveConfig.DoPostConfigureComplete, …; V 2: OverlayModes.Disable, OverlayModes.Update |
| 54 | 1928837153 | Sandbox Tools | T,S | touches_ui | P 1: BuildingDef.Build; T 3: PlayerController.OnPrefabInit, ToolMenu.CreateSandBoxTools, ToolMenu.OnPrefabInit; S 1: SandboxToolParameterMenu.ConfigureEntitySelector |
| 55 | 1907780517 | Starmap Queue | P | – | P 1: SpacecraftManager.EarnDestinationAnalysisPoints |
| 57 | 1840755803 | Buildable Natural Tile | B | – | P 3: GeneratedBuildings.LoadGeneratedBuildings, Techs.Init, BuildingComplete.OnSpawn |
| 59 | 2019780544 | 打印舱物品刷新(PrintPod Select) | T,S,K,O | touches_ui | P 8: OptionsEntry.PeterHan.PLib.Options.OptionsEntry, Game.DestroyInstances, Immigration.OnPrefabInit, …; T 2: PlayerController.OnPrefabInit, ToolMenu.CreateBasicTools; S 2: ImmigrantScreen.OnRejectAll, ImmigrantScreen.OnShow |
| 61 | 1902735059 | Traducción al Español 100% (2026) + TODOS LOS D… | X | – | – |
| 62 | 1872111766 | Favorite Resources // by @Ony 👾 | S | touches_ui | S 10: AllResourcesScreen.OnSpawn, AllResourcesScreen.Populate, AllResourcesScreen.SpawnRows, … |
| 63 | 1885337332 | bottle emptier extend | P | – | P 3: BottleEmptier.OnCopySettings, BottleEmptierConfig.ConfigureBuildingTemplate, BottleEmptier.CreateChore |
| 64 | 1725950119 | No 'Long Commutes' | P | – | P 1: Tutorial.LongTravelTimes |
| 65 | 3468585385 | Blueprints Expanded | T,S,K,O,V | touches_ui asset_bundle_ui | P 17: Game.DestroyInstances, BuildingLoader.CreateBuildingUnderConstruction, BuildingLoader.CreateBuildingComplete, …; T 7: PlayerController.OnPrefabInit, CancelTool.GetDefaultFilters, CancelTool.OnDragTool, …; S 7: FileNameDialog.OnSpawn, DetailsScreen.OnPrefabInit, FilterSideScreen.IsValidForTarget, …; V 2: BlockTileRenderer.GetCellColour, CameraController.OnPrefabInit |
| 67 | 3243440327 | 自然种植盒（Natural Plant Box） | B | – | P 4: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, PlantablePlot.SpawnOccupyingObject, … |
| 68 | 1269554374 | Русификатор (Wiki) | X | – | – |
| 69 | 2054432034 | Insulated Door | B | – | P 4: Door.OnPrefabInit, Localization.Initialize, Db.Initialize, … |
| 70 | 1863275516 | Smart Mop // by @Ony 👾 | T | – | T 1: MopTool.OnDragTool |
| 71 | 2527442674 | Full Miner Yield | P | – | P 1: WorldDamage.OnDigComplete |
| 72 | 1717999057 | Dup Stats Configurator | S,O | touches_ui | P 3: MinionStartingStats.GenerateAptitudes, MinionStartingStats.GenerateTraits, HeadquartersConfig.ConfigureBuildingTemplate; S 1: CharacterContainer.GenerateCharacter |
| 73 | 1717534457 | Wallpaper | B,V | – | P 4: Game.OnSpawn, GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, …; V 1: OverlayScreen.ToggleOverlay |
| 74 | 1708823360 | Flow Splitters | B | – | P 2: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize |
| 75 | 1454392573 | 484114 한글 언어팩 | X | – | – |
| 76 | 3557584850 | Ronivan's Legacy - Industrial Revolution | B,T,S,O,V,M | touches_ui asset_bundle_ui | P 138: HarvestablePOIConfig.GenerateConfigs, ValveBaseTemperature.CustomizeBuildings.ValveBaseTemperature not found, skipping patch., AdvancedCraftingTableConfig.ConfigureRecipes, …; T 3: BuildTool.Activate, BuildTool.SetColor, BaseUtilityBuildTool.CheckValidPathPiece; S 5: DetailsScreen.OnPrefabInit, OverlayMenu.InitializeToggles, PlanScreen.OnClickCopyBuilding, …; V 3: OverlayScreen.RegisterModes, SimDebugView.OnPrefabInit, OverlayModes.Update; M 1: MainMenu.OnSpawn |
| 77 | 3103376569 | Scaffolds / 脚手架 | B,T,S,O | touches_ui | P 2: GeneratedBuildings.LoadGeneratedBuildings, BuildingDef.Instantiate; T 1: FilteredDragTool.GetFilterLayerFromObjectLayer; S 2: ProductInfoScreen.SetMaterials, ResourceRemainingDisplayScreen.GetString |
| 79 | 2552175137 | Speed Control 202107 | S | touches_ui | S 1: SpeedControlScreen.OnChanged |
| 80 | 2128885110 | AI Improvements | P,O | – | P 3: Chore.FinishPreconditions, Chore.SetPriority, ChoreConsumer.FindNextChore |
| 81 | 3179880371 | The Capsules 2.0 | B,T,S,V | touches_ui asset_bundle_ui | P 2: Db.Initialize, BuildingDef.Instantiate; T 1: PlayerController.OnPrefabInit; S 3: DetailsScreen.OnPrefabInit, ProductInfoScreen.SetMaterials, ResourceRemainingDisplayScreen.GetString |
| 82 | 2789182100 | More Marble Sculptures | P | – | P 3: ArtableStages..ctor, InventoryOrganization.GenerateSubcategories, Localization.Initialize |
| 83 | 929036307 | Chinese language pack (by deloquac) | X | – | – |
| 84 | 2947429297 | Cluster Generation Manager [Vanilla/DLC] | S,K,O,M | touches_ui asset_bundle_ui | P 37: SaveGame.GetColonyToolTip, ColonyAchievement.IsValidForSave, CustomGameSettings.GetCurrentClusterLayout, …; S 4: RetiredColonyInfoScreen.IsAchievementValidForDLCContext, PauseScreen.OnSpawn, FileNameDialog.OnActivate, …; M 9: ColonyDestinationSelectScreen.ShuffleClicked, ColonyDestinationSelectScreen.CoordinateChanged, LoadScreen.ShowColonySave, … |
| 86 | 1861107947 | Customize Geyser | S,O | touches_ui | P 17: GeoTunerConfig.ConfigureBuildingTemplate, GeoTuner.RefreshStorageRequirements, GeoTuner.DropStorageIfNotMatching, …; S 2: AllResourcesScreen.SpawnCategoryRow, GeoTunerSideScreen.RefreshOptions |
| 87 | 3630759126 | ONI Together: A Multiplayer Mod | T,S,K,O,V,M | touches_ui asset_bundle_ui | P 202: Door.QueueStateChange, Door.Sim200ms, Door.OnCleanUp, …; T 19: DisinfectTool.OnDragTool, PrioritizeTool.OnDragTool, PlayerController.ActivateTool, …; S 49: LoadingOverlay.Load, PauseScreen.OnQuitConfirm, SpeedControlScreen.Pause, …; K 2: KInputManager.Update, KScreen.OnKeyDown; V 1: CameraController.OnKeyDown; M 2: MainMenu.OnPrefabInit, NewGameFlow.Previous |
| 88 | 1907824546 | Butcher & Fishing Stations | B,S,O | touches_ui | P 10: WaterTrapConfig.AddGuide, BaseSeaFairyConfig.BaseSeaFairy, RanchStation.FindRanchable, … |
| 89 | 2086704949 | Squirrel Wheel | B,O | – | P 3: BaseSquirrelConfig.BaseSquirrel, SeedPlantingStates.InitializeStates, Constructable.OnSpawn |
| 90 | 1845579729 | Wall Pumps and Vents | B | – | P 5: ElementConsumer.GetSampleCell, Exhaust.UpdateEmission, Pump.IsPumpable, … |
| 91 | 2968583663 | Pipe Flow Overlay | O,V | touches_ui | P 6: Utils.UpdateCrossingDirection, Game.OnSpawn, Conduit.OnSpawn, …; V 2: SimDebugView.SetMode, OverlayLegend.OnSpawn |
| 92 | 2415088657 | Enable achievements | P | – | P 1: Game.Load |
| 93 | 2721654252 | Demolish Neutronium | P,O | – | P 8: SaveLoader.OnSpawn, Diggable.Undiggable, Diggable.UpdateColor, … |
| 94 | 1859560108 | Simpler Pip Plant Rule (Vanilla/DLC) | B,O | – | P 3: SeedPlantingMonitor..ctor, PlantableCellQuery..ctor, PlantableCellQuery.CountNearbyPlants |
| 95 | 1887986467 | Smart Pumps | B,S,O | touches_ui | S 1: FilterSideScreen.IsValidForTarget |
| 96 | 2051059692 | Tiny Door | B | – | P 2: Localization.Initialize, Db.Initialize |
| 97 | 1737603705 | FreeCamera | S,V | touches_ui | P 1: WattsonMessage.OnDeactivate; S 1: NewBaseScreen.SetInitialCamera; V 2: CameraController.OnPrefabInit, CameraController.ConstrainToWorld |
| 98 | 2549472747 | Remove Sandbox Lock Mode | S | touches_ui | P 1: Game.Save; S 2: GameOptionsScreen.OnUnlockSandboxMode, GameOptionsScreen.SetSandboxModeActive |
| 99 | 2618339179 | Freezer | B,O | – | P 2: Db.Initialize, Localization.Initialize |
| 100 | 2732419691 | Sealed Pump | B | – | P 2: GeneratedBuildings.LoadGeneratedBuildings, Localization.Initialize |
| 102 | 1866754178 | Drains | B,O | – | P 1: Db.Initialize |
| 104 | 2551341883 | Auto-Eject | P | – | P 2: GeneShuffler.InitializeStates, WarpPortal.InitializeStates |
| 105 | 2537508393 | Immigrants Reroll | S | touches_ui | P 1: Localization.Initialize; S 1: ImmigrantScreen.OnRejectAll |
| 106 | 3369089274 | Convenient Scheduling | P | touches_ui | P 5: ScheduleManager.OnAddDupe, ScheduleManager.OnSpawn, ScheduleManager.DeleteSchedule, … |
| 107 | 2348079481 | [Vanilla + DLC] Rooms Expanded | O,V | – | P 39: RoomTypes.RoomsExpanded: Error - RoomTypes type is null..., DataMiner.ComputeWorkProgress, GunkEmptierConfig.ConfigureBuildingTemplate, …; V 1: OverlayModes.GetCustomLegendData |
| 108 | 2921809973 | Backwalls | B,T,S,K,V,M | touches_ui asset_bundle_ui | P 7: TileAssets.Add, BuildToolHoverTextCard.UpdateHoverElements, Db.Initialize, …; T 5: BuildTool.OnDeactivateTool, BuildTool.UpdateVis, BuildTool.OnDragTool, …; S 2: DetailsScreen.OnPrefabInit, PlanScreen.OnClickCopyBuilding; K 1: DetailsScreen.OnKeyUp; M 1: ModsScreen.BuildDisplay |
| 110 | 2363561445 | Refresh Immigrants / 刷新选人 | S | touches_ui | P 1: Localization.Initialize; S 1: ImmigrantScreen.OnRejectAll |
| 111 | 1872885565 | No Research Alerts | S,O | touches_ui | P 3: ResearchEntry.ResearchCompleted, PlanCategoryNotifications.ToggleAttention, Localization.Initialize; S 1: PlanScreen.CloseCategoryPanel |
| 112 | 1843647561 | Settings Change Tool | T,K | touches_ui | T 3: PlayerController.OnPrefabInit, ToolMenu.CreateBasicTools, ToolMenu.OnPrefabInit |
| 113 | 1706075206 | Steel Ladder | B | – | P 2: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize |
| 115 | 2722663035 | Replace Floors | P | – | P 7: FarmTileConfig.DoPostConfigureComplete, HydroponicFarmConfig.DoPostConfigureComplete, WireBridgeHighWattageConfig.DoPostConfigureComplete, … |
| 117 | 2208398090 | Traffic Visualizer | S,V | touches_ui | P 5: Db.Initialize, TransitionDriver.EndTransition, Game.OnDestroy, …; S 1: OverlayMenu.InitializeToggles; V 2: SimDebugView.OnPrefabInit, OverlayScreen.RegisterModes |
| 119 | 3120193648 | Debug Buttons | S,K,O | touches_ui | S 4: TopLeftControlScreen.UpdateSandboxToggleState, TopLeftControlScreen.OnActivate, TopLeftControlScreen.OnForcedCleanUp, …; K 1: InputInit.Awake |
| 120 | 1983504552 | Thermal Tooltips | S,O | touches_ui | P 1: SelectToolHoverTextCard.UpdateHoverElements; S 3: MaterialSelector.SetEffects, ProductInfoScreen.Close, ProductInfoScreen.SetMaterials |
| 121 | 2542160819 | Self-sealing Airlocks Revived | P | – | P 3: Door.OnPrefabInit, Door.OnCleanUp, Door.SetSimState |
| 122 | 1910961538 | Aquatic Farm | B | – | – |
| 123 | 1872105871 | Drywall Hides Pipes | P | – | P 2: ExteriorWallConfig.CreateBuildingDef, ThermalBlockConfig.CreateBuildingDef |
| 124 | 1897096779 | Research Manager // by @Ony 👾 | S,K | touches_ui | P 11: ResearchEntry.OnResearchCanceled, ResearchEntry.OnResearchClicked, ResearchEntry.OnSpawn, …; S 6: NewBaseScreen.SpawnMinions, ResearchScreen.OnCleanUp, ResearchScreen.OnResearchComplete, …; K 2: ResearchScreen.OnKeyDown, ResearchScreen.OnKeyUp |
| 125 | 3172734005 | Dupery Fixed | S,V | touches_ui | P 11: Db.Initialize, Localization.Initialize, MinionStartingStats.CreateBodyData, …; S 2: CharacterContainer.RefreshOutfitSelector, CharacterContainer.GenerateCharacter |
| 127 | 2600818338 | Material Selection Properties | S | touches_ui | P 1: GameUtil.GetSignificantMaterialPropertyDescriptors; S 1: MaterialSelector.SetEffects |
| 128 | 1703606095 | Algae Grower | B | – | P 2: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize |
| 129 | 3130257075 | Reveal Map | S | touches_ui | S 1: PauseScreen.GetClipboardText |
| 130 | 1739635014 | MaterialColor | S,K,V | touches_ui | P 6: Game.OnSpawn, Game.DestroyInstances, Global.GenerateDefaultBindings, …; S 3: OverlayMenu.InitializeToggles, OverlayMenu.OnOverlayChanged, OverlayMenu.OnToggleSelect; K 1: KInputController..ctor; V 1: BlockTileRenderer.GetCellColour |
| 131 | 2982316671 | Planning Tool | T,S,K,O,V | touches_ui | P 7: SaveGame.OnPrefabInit, Db.Initialize, GridSettings.Reset, …; T 3: ToolMenu.CreateBasicTools, PlayerController.OnPrefabInit, ToolMenu.OnPrefabInit |
| 133 | 2012810337 | Stairs | B,T | – | P 16: MinionConfig.OnSpawn, BionicMinionConfig.OnSpawn, GridSettings.Reset, …; T 1: DeconstructTool.DeconstructCell |
| 134 | 2229356245 | Germicidal UV Lamps | B,O | – | P 3: Db.Initialize, LightBugOrangeConfig.CreatePrefab, LightBugPurpleConfig.CreatePrefab |
| 135 | 3231839363 | Fast Insulated Self Sealing AirLock | B,O | – | P 4: Door.OnPrefabInit, Db.Initialize, Door.OnCleanUp, … |
| 136 | 1831438994 | Critter Inventory | T,S | touches_ui | P 7: ResourceCategoryHeader.Hover, ResourceCategoryHeader.RefreshChart, ResourceCategoryHeader.UpdateContents, …; T 1: TrackerTool.AddNewWorldTrackers; S 12: AllResourcesScreen.OnPrefabInit, AllResourcesScreen.RefreshCharts, AllResourcesScreen.RefreshRows, … |
| 137 | 2660131829 | Decor Pack I - Classy Glass | B,T,S,V,M | touches_ui asset_bundle_ui | P 22: AdditionalDetailsPanel.RefreshDetailsPanel, BuildingFacades, Utilities.IsBuildable, …; T 1: BuildTool.Activate; S 5: DetailsScreen.OnPrefabInit, MaterialSelector.SetToggleBGImage, MaterialSelector.UpdateScrollBar, …; M 1: ModsScreen.BuildDisplay |
| 138 | 2877805072 | 更多的空气过滤器 More AirFilter | B,O | – | P 5: AirFilterConfig.ConfigureBuildingTemplate, AirFilterConfig.CreateBuildingDef, GeneratedBuildings.LoadGeneratedBuildings, … |
| 140 | 3455710156 | Visual Fixes N Tweaks | T,O | – | P 53: AnimTileable.UpdateEndCaps, Assets.OnPrefabInit, BaseMinionConfig.BaseMinion, …; T 6: BuildTool.SetToolOrientation, BuildTool.TryRotate, BuildTool.OnActivateTool, … |
| 141 | 1844888487 | Smart Dig // by @Ony 👾 | T,K | touches_ui | P 1: HoverTextConfiguration.DrawInstructions; T 4: DigTool.OnActivateTool, DigTool.OnDeactivateTool, DigTool.PlaceDig, … |
| 142 | 1854895085 | The Duplicant Reacharound | P | – | P 1: Game.OnPrefabInit |
| 144 | 2839006500 | Free Resource Buildings - Updated | B,S,O | touches_ui | P 2: Db.Initialize, Localization.Initialize; S 3: FilterSideScreen.IsValidForTarget, HighEnergyParticleDirectionSideScreen.IsValidForTarget, SideScreenContent.GetSideScreenSortOrder |
| 145 | 2313125589 | No Mop Limits | T | – | T 1: MopTool.OnDragTool |
| 146 | 2875363453 | More Small Sculptures | P | – | P 3: ArtableStages..ctor, InventoryOrganization.GenerateSubcategories, Localization.Initialize |
| 147 | 1715709940 | Rotate Everything | S,V | touches_ui | P 24: BuildingTemplates.CreateBuildingDef, ElementConverter.ConvertMass, BuildingElementEmitter.UpdateSimState, …; S 1: ReceptacleSideScreen.ValidRotationForDeposit |
| 148 | 1732534140 | Double Sweeper Range | P | – | P 2: SolidTransferArmConfig.AddVisualizer, SolidTransferArmConfig.DoPostConfigureComplete |
| 149 | 2584828134 | Double Bed [ BROKEN ] | B | – | P 4: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, Localization.Initialize, … |
| 151 | 1822071175 | BalanceGasPumpConsumption | P | – | P 3: GasPumpConfig.CreateBuildingDef, GasMiniPumpConfig.CreateBuildingDef, GasFilterConfig.CreateBuildingDef |
| 152 | 2878830936 | Better Refresh复制人基础属性提高 | S,O | touches_ui | P 2: MinionStartingStats.GenerateAptitudes, Localization.Initialize; S 3: ImmigrantScreen.OnPrefabInit, ImmigrantScreen.OnSpawn, ImmigrantScreen.Initialize |
| 153 | 3369395347 | Geyser Control | P,O | – | P 3: Studyable.OnSidescreenButtonPressed, Localization.Initialize, GeyserGenericConfig.CreateGeyser |
| 154 | 1871382174 | Expanded Lights | B | – | P 7: GeneratedBuildings.LoadGeneratedBuildings, CeilingLightConfig.CreateBuildingDef, CeilingLightConfig.DoPostConfigurePreview, … |
| 155 | 1825146346 | EmptyWorlds (SkyBlock) | P,O | touches_ui | P 4: Localization.Initialize, Db.Initialize, TemplateCache.GetTemplate, … |
| 156 | 2854869130 | Mod Profile Manager [MPM] | S,O,M | touches_ui | P 10: Steam.steam.makemod was null, IReader, DragMe.OnBeginDrag, …; M 7: ModsScreen.BuildDisplay, ModsScreen.OnActivate, LoadScreen.ShowColony, … |
| 157 | 2129768894 | PAUSE on ready to print | P | – | P 1: Telepad.InitializeStates |
| 158 | 1758223146 | Stats Unlimited | S,O | touches_ui | P 8: AttributeLevel.AddExperience, MinionStartingStats.ApplyExperience, MinionStartingStats.Deliver, …; S 2: NewBaseScreen.SpawnMinions, PopFXManager.SpawnFX |
| 159 | 1801004057 | Advanced Electrolyzers | B,O | – | P 2: Db.Initialize, Localization.Initialize |
| 160 | 1863598374 | Deselect New Materials | K,O | – | P 2: TreeFilterable.OnDiscover, TreeFilterable.OnPrefabInit |
| 162 | 3059711743 | Big Storage: Restoraged | B,O | – | P 4: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, Mod.Load, … |
| 163 | 2705951522 | 自动收获 Fixed | P | – | P 1: EntityTemplates.ExtendEntityToBasicPlant |
| 164 | 1865119054 | Schedule Master // by @Ony 👾 | S | touches_ui | P 5: ScheduleManager.OnAddDupe, ScheduleScreenEntry.AddTimetableRow, ScheduleScreenEntry.RemoveTimetableRow, …; S 3: ScheduleScreen.OnChangeCurrentTimetable, ScheduleScreen.OnSpawn, WattsonMessage.OnDeactivate |
| 166 | 1870696175 | Better Deselect | T,S,O | touches_ui | P 1: Localization.Initialize; T 3: ToolMenu.OnKeyDown, ToolMenu.OnKeyUp, InterfaceTool.DeactivateTool; S 1: PlanScreen.OnActiveToolChanged |
| 167 | 2844843044 | Drag Area More Visible | T | touches_ui | T 2: DragTool.OnActivateTool, DragTool.OnDeactivateTool |
| 168 | 2837919908 | Rocketry Expanded | B,S,K,O,V | touches_ui asset_bundle_ui | P 161: Grid.FreeGridSpace, ElementLoader.Load, HydrocarbonRocketEnginesPatches.HydrocarbonRocketEngines mod target method ClusterCraftPatches not found on type HydrocarbonRocketEngines.HydrocarbonRocketEnginesPatches, …; S 28: DetailsScreen.OnPrefabInit, HighEnergyParticleDirectionSideScreen.IsValidForTarget, SelectModuleSideScreen.OnSpawn, …; K 1: KScreen.OnKeyDown; V 1: CameraController.ConstrainToWorld |
| 169 | 2630036712 | Pipette | T,K,O | – | P 1: Game.DestroyInstances; T 2: PlayerController.OnPrefabInit, ToolMenu.CreateBasicTools |
| 172 | 1830166724 | Achievement Enabler | S | touches_ui | P 1: ColonyAchievementTracker.UnlockPlatformAchievement; S 2: PauseScreen.OnPrefabInit, RetiredColonyInfoScreen.OnShow |
| 174 | 2271373459 | Map Overlay | S,O,V | touches_ui | P 2: Db.Initialize, StatusItem.GetStatusItemOverlayBySimViewMode; S 1: OverlayMenu.InitializeToggles; V 3: OverlayScreen.RegisterModes, OverlayLegend.OnSpawn, SimDebugView.OnPrefabInit |
| 175 | 1717519620 | Oil Well - Any Water | P | – | P 1: OilWellCapConfig.ConfigureBuildingTemplate |
| 176 | 1738906344 | Conveyor Rail Filter | B | – | P 2: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize |
| 179 | 1820638089 | Liquid Bottler | B | – | P 3: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, Localization.Initialize |
| 181 | 3033409170 | Piped Everything | V | – | P 21: AdvancedEnergyGenerator, GameUtil.GetFinalTemperature, ComplexFabricator.StartWorkingOrder, … |
| 182 | 2632758219 | Rational Priority | P | – | P 7: Chore.CompareTo, GlobalChoreProvider.IsBetterThan, GlobalChoreProvider.FetchComparer, … |
| 183 | 3301490799 | Mass Move Tool | T,S,K,O | touches_ui | P 7: Assets.OnPrefabInit, CancellableMove.OnCancel, CancellableMove.SetMovable, …; T 2: PlayerController.OnPrefabInit, ToolMenu.CreateBasicTools |
| 184 | 3047547550 | Free Resource Buildings Branch | B,S,O | touches_ui | P 2: Db.Initialize, Localization.Initialize; S 3: FilterSideScreen.IsValidForTarget, HighEnergyParticleDirectionSideScreen.IsValidForTarget, SideScreenContent.GetSideScreenSortOrder |
| 185 | 3381262702 | U&F-Choose Neural Vacillator 神经振荡器自由选择 | S | touches_ui | P 2: GeneShuffler.ApplyRandomTrait, Localization.Initialize |
| 186 | 1702980293 | Colorful Shinebugs | P | – | P 6: LightBugConfig.CreateLightBug, LightBugOrangeConfig.CreateLightBug, LightBugPurpleConfig.CreateLightBug, … |
| 190 | 3615308766 | 刷新打印舱 | S | touches_ui | P 1: Localization.Initialize; S 3: ImmigrantScreen.OnPrefabInit, ImmigrantScreen.OnSpawn, ImmigrantScreen.Initialize |
| 192 | 2949194697 | Athletic Manual Generator | P,O | – | P 2: ManualGeneratorConfig.CreateBuildingDef, ManualGeneratorConfig.DoPostConfigureComplete |
| 193 | 3458128254 | 技能自定义-fix | P,O | – | P 3: MinionStartingStats.GenerateAptitudes, HeadquartersConfig.ConfigureBuildingTemplate, MinionStartingStats.GenerateTraits |
| 194 | 2556074790 | [Vanilla + DLC] Better Air Filters updated | P | – | P 2: AirFilterConfig.CreateBuildingDef, RailGunConfig.CreateBuildingDef |
| 195 | 2851636970 | 技能自定义 | P,O | – | P 3: MinionStartingStats.GenerateAptitudes, HeadquartersConfig.ConfigureBuildingTemplate, MinionStartingStats.GenerateTraits |
| 196 | 3477214095 | Auto Drop Gas Bottler Filler and Liquid Filler | S | touches_ui | P 5: GasBottlerConfig.ConfigureBuildingTemplate, GasBottlerConfig.CreateBuildingDef, LiquidBottlerConfig.ConfigureBuildingTemplate, …; S 1: AutomatableSideScreen.IsValidForTarget |
| 197 | 2629866529 | Pliers 2021 | T,K,O | – | P 11: Game.DestroyInstances, GasConduitConfig.DoPostConfigureComplete, GasConduitRadiantConfig.DoPostConfigureComplete, …; T 3: PlayerController.OnPrefabInit, ToolMenu.OnPrefabInit, ToolMenu.CreateBasicTools |
| 199 | 1822175925 | Faster Jetpacks | P | – | P 1: BipedTransitionLayer.BeginTransition |
| 200 | 2380984533 | Multiple Power Generator | P,O | – | P 10: Generator.get_WattageRating, Generator.get_BaseWattageRating, Generator.CalculateCapacity, … |
| 201 | 1794510050 | AllBiomesWorld - Fuleria | P | – | P 1: Db.Initialize |
| 202 | 3682732647 | Duplicant Status Bar - All DLC Status HUD | S,O,V | touches_ui | P 2: Game.OnPrefabInit, Db.Initialize; S 1: ManagementMenu.ToggleScreen |
| 204 | 2873919932 | Big box大箱子 | P,O | – | P 4: StorageLockerConfig.DoPostConfigureComplete, StorageLockerSmartConfig.DoPostConfigureComplete, LiquidReservoirConfig.ConfigureBuildingTemplate, … |
| 205 | 2950117755 | Wrangle & Carry | P | – | P 6: Db.Initialize, ChoreTypes.Add, Capturable.OnCompleteWork, … |
| 206 | 2206905516 | Smart Egg Incubator(DLC Compatible) | P | – | P 2: EggIncubatorConfig.CreateBuildingDef, EggIncubator.Sim1000ms |
| 207 | 1818138009 | Customize Buildings (more capacity, less dupes) | S,O | touches_ui | P 80: BreathingGeyser.ExhaleUpdate, AirConditioner.UpdateState, AlgaeHabitatConfig.ConfigureBuildingTemplate, …; S 6: MaterialSelector.ConfigureScreen, MaterialSelector.AutoSelectAvailableMaterial, ThresholdSwitchSideScreen.UpdateLabels, … |
| 208 | 1732578172 | Paint Walls | V | – | P 1: BuildingComplete.OnSpawn; V 1: OverlayScreen.ToggleOverlay |
| 209 | 2688074001 | Suit Recharger | B,S,O | touches_ui | P 6: BreathMonitor.CreateRecoverBreathChore, RecoverBreathChore.UpdateLocator, HelmetController.OnBeginRecoverBreath, … |
| 210 | 1873476551 | Storage Pod | B,O | – | P 1: Db.Initialize |
| 211 | 2539984678 | Rebuildable AETN | P,O | touches_ui | P 15: MassiveHeatSinkConfig.CreateBuildingDef, MassiveHeatSinkConfig.DoPostConfigureComplete, Deconstructable.OnSpawn, … |
| 212 | 3542667178 | Better Repair | P,O | – | P 11: Game.OnSpawn, Localization.Initialize, BuildingHP.OnPrefabInit, … |
| 215 | 2119648603 | Font Loader for Traditional Chinese | P | touches_ui | P 1: Localization.GetLocale |
| 217 | 2869487421 | Real Insulation Tile真正的隔热砖 | P | – | P 1: InsulationTileConfig.CreateBuildingDef |
| 219 | 1971998416 | automatic harvest | P | – | P 1: EntityTemplates.ExtendEntityToBasicPlant |
| 220 | 2968184715 | Portal Network | B,S,V | touches_ui asset_bundle_ui | P 3: Db.Initialize, BuildingStatusItems.CreateStatusItems, Reactable.UpdateLocation; S 2: DetailsScreen.OnPrefabInit, DetailsScreen.CanObjectDisplayTabOfType |
| 221 | 2891895988 | Closest Material | T,S,K | touches_ui | P 2: BuildingDef.Instantiate, Game.DestroyInstances; T 3: PlayerController.OnPrefabInit, ToolMenu.OnPrefabInit, ToolMenu.CreateBasicTools; S 2: MaterialSelectionPanel.OnPrefabInit, MaterialSelectionPanel.ConfigureScreen |
| 222 | 1930918856 | Solar System Worlds | P | touches_ui | P 8: BackgroundEarthConfig.CreatePrefab, WorldGen.ReportWorldGenError, AsteroidGridEntity.Init, … |
| 223 | 3046440992 | 字型模組。新 | P | touches_ui | P 2: Localization.GetLocale, Db.Initialize |
| 224 | 2558292946 | Hideable Heavy-Watt | P | – | P 4: WireHighWattageConfig.CreateBuildingDef, WireBridgeHighWattageConfig.CreateBuildingDef, WireRefinedHighWattageConfig.CreateBuildingDef, … |
| 226 | 1979475408 | Asphalt Tile | B,S,M | touches_ui asset_bundle_ui | P 7: BaseHatchConfig.BasicRockDiet, Db.Initialize, ElementLoader.CollectElementsFromYAML, …; M 1: ModsScreen.BuildDisplay |
| 227 | 3657612749 | Better Dupe Assignment | S,O,V | touches_ui | P 3: GlobalChoreProvider.CollectChores, ChoreDriver.SetChore, Game.DestroyInstances; S 1: OverlayMenu.InitializeToggles; V 4: OverlayScreen.RegisterModes, SimDebugView..ctor, OverlayScreen.ToggleOverlay, … |
| 228 | 3599672848 | True Tiles Material Pack | X | – | – |
| 229 | 1729816134 | Refined Metals Usable As Raw Metals | P | – | P 1: ElementLoader.FinaliseElementsTable |
| 230 | 2631833478 | Automatic Geyser Calculation | P | – | P 1: Geyser.GetDescriptors |
| 231 | 2558054404 | [MG]Super Wire(超级电线) | P | – | P 4: DevGeneratorConfig.CreateBuildingDef, DevRadiationGeneratorConfig.CreateBuildingDef, BaseBatteryConfig.DoPostConfigureComplete, … |
| 232 | 2889982891 | ภาษาไทย / Thai Language | X | – | – |
| 233 | 1708809748 | Critical Notification Pauser | P | – | P 1: Notification.IsReady |
| 234 | 2830869494 | 1000点基本属性的小人 | P | – | P 1: BaseMinionConfig.AddMinionTraits |
| 235 | 2819586273 | 한국어 언어팩 | X | – | – |
| 236 | 2582905897 | Baator | P | – | P 5: ElementLoader.FinaliseElementsTable, WorldContainer.RefreshFixedTraits, Assets.OnPrefabInit, … |
| 237 | 1821957996 | 非官方繁中 | X | – | – |
| 238 | 1837883076 | Buildable Dirt Tile | B | – | P 3: GeneratedBuildings.LoadGeneratedBuildings, Techs.Init, BuildingComplete.OnSpawn |
| 239 | 2373109589 | Rest for the Weary | P | touches_ui | P 6: StandardChoreBase.AddPrecondition, MovePickupableChore..ctor, MingleMonitor.InitializeStates, … |
| 240 | 2958243747 | 统一背景 Unified Background MOD | O,V | – | P 1: SubworldZoneRenderData.OnActiveWorldChanged |
| 241 | 3430960564 | Transit Tubes Expanded | B,S,O | touches_ui | P 6: TravelTubeEntranceConfig.DoPostConfigureComplete, TravelTubeEntrance.TubeConnectionsChanged, Patches.AddLongJump, …; S 1: AccessControlSideScreen.RefreshContainerObjects |
| 242 | 3228435563 | 真·无限气液库V2.0（Infinite Capacity Reservoir） | B | touches_ui | P 2: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize |
| 243 | 2650613352 | Piped Super Computer (Advanced Research Station… | P,O | – | P 3: AdvancedResearchCenterConfig.CreateBuildingDef, AdvancedResearchCenterConfig.ConfigureBuildingTemplate, Assets.AddBuildingDef |
| 245 | 3487819075 | True Tiles Skin Selector | S,V | touches_ui | P 5: Game.DestroyInstances, BlockTileRendererPatch.MatchesElement, TileAssets.Add, …; S 1: DetailsScreen.OnPrefabInit; V 1: BlockTileRenderer.MatchesDef |
| 246 | 2366597538 | Piped Deodorizer | B,O | – | P 2: Db.Initialize, Localization.Initialize |
| 247 | 3327324912 | Tradução e Adaptação ao idioma Português Brasil… | X | – | – |
| 248 | 2610275728 | Threshold Walls | B,O | – | P 3: Db.Initialize, Localization.Initialize, OvercrowdingMonitor.get_IsConfined |
| 250 | 2873888171 | 移气管 | T,K,O | – | P 1: Game.DestroyInstances; T 2: PlayerController.OnPrefabInit, ToolMenu.CreateBasicTools |
| 251 | 2548787152 | Powerless Deodorizers | P | – | P 3: AirFilterConfig, AirFilterConfig.CreateBuildingDef, AirFilterConfig.DoPostConfigureComplete |
| 254 | 3135906971 | Skill Queue | S,K,O | touches_ui | P 5: BaseMinionConfig.BaseMinion, MinionResume.MasterSkill, SkillWidget.Refresh, …; S 1: SkillsScreen.RefreshProgressBars |
| 255 | 3637202410 | MAGNET!!! | S,K,O,V,M | touches_ui | P 4: Localization.Initialize, Db.Initialize, Game.OnSpawn, …; S 2: DetailsScreen.OnPrefabInit, HoverTextScreen.Update; M 1: ModsScreen.BuildDisplay |
| 256 | 1708810461 | Fervine | P | – | P 3: EntityConfigManager.LoadGeneratedEntities, Immigration.ConfigureCarePackages, SupermaterialRefineryConfig.ConfigureBuildingTemplate |
| 257 | 3028181310 | Advanced Flow Management | T,S,V | touches_ui | P 12: EntityCellVisualizer.DrawUtilityIcon, Localization.Initialize, CopyBuildingSettings.ApplyCopy, …; T 1: DragTool.OnLeftClickUp; S 1: DetailsScreen.OnPrefabInit; V 5: OverlayModes.Disable, OverlayModes..ctor, OverlayLegend.OnSpawn, … |
| 258 | 2869608898 | Bio-Inks: Rechargeable Printing Pod | S,V,M | touches_ui asset_bundle_ui | P 26: Geyser.OnSpawn, Accessorizer.OnDeserialized, Assets.OnPrefabInit, …; S 11: CharacterSelectionController.InitializeContainers, CarePackageContainer.GetSpawnableQuantityOnly, CarePackageContainer.GetCurrentQuantity, …; M 1: ModsScreen.BuildDisplay |
| 259 | 1758933220 | Oxygen Not Included - Hungarian Language Pack | X | – | – |
| 260 | 2154537340 | Don't Idle in Doors | P,O | – | P 5: IdleCellQuery.IsMatch, IdleChore.InitializeStates, GeneratedBuildings.LoadGeneratedBuildings, … |
| 261 | 2837000215 | Missing Element Textures | P | – | P 1: ElementLoader.Load |
| 262 | 2219686881 | Better Multitool Animations | P | – | – |
| 263 | 930830432 | 加油菌汉化(V1.35) | X | – | – |
| 264 | 3392404591 | Supply to Closest | P | – | P 3: FetchAreaChore.InitializeStates, GlobalChoreProvider.CollectChores, GlobalChoreProvider.UpdateFetches |
| 265 | 2679005270 | Worn Suit doesn't drop Canister & Suit Docks Tu… | P | – | P 8: SuitLocker.UnequipFrom, SuitLocker.OnCompleteWork, SuitMarker.UnequipSuitReactable, … |
| 266 | 1831722850 | Spanish Translation ES-es 2026 71% (All DLCs) | X | – | – |
| 267 | 3196698830 | Conveyor Tile | B,O | – | P 6: Game.DestroyInstances, GeneratedBuildings.LoadGeneratedBuildings, AnimTileable.HasTileableNeighbour, … |
| 269 | 2992024030 | auto machine机器自动工作 | P,O | – | P 25: CookingStationConfig.DoPostConfigureComplete, GourmetCookingStationConfig.DoPostConfigureComplete, RockCrusherConfig.ConfigureBuildingTemplate, … |
| 270 | 2701205009 | Français | X | – | – |
| 271 | 2126548651 | Stirling Engine | B,O | – | P 2: Db.Initialize, Localization.Initialize |
| 272 | 2356326908 | Wireless Power | B,S | touches_ui | P 10: Game.OnPrefabInit, Game.OnLoadLevel, Db.Initialize, …; S 1: DetailsScreen.OnPrefabInit |
| 273 | 1843550325 | Hatch Morphs | P | – | P 4: KAnimGroupFile.Load, ModifierSet.Initialize, EntityTemplates.ExtendEntityToFertileCreature, … |
| 274 | 2135682628 | Build Straight Up | P | – | P 1: Boolean |
| 275 | 1850356486 | Insulated Farm Tiles | B | – | P 2: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize |
| 276 | 1873666181 | ImbaStorages | P,O | – | P 4: LiquidReservoirConfig.ConfigureBuildingTemplate, GasReservoirConfig.ConfigureBuildingTemplate, RefrigeratorConfig.DoPostConfigureComplete, … |
| 277 | 2524767809 | Bottle Emptier Variable Size | B | – | P 1: GeneratedBuildings.LoadGeneratedBuildings |
| 278 | 2077848080 | Rocket Calculator | T,O | – | P 5: FuelTank.OnPrefabInit, OxidizerTank.OnPrefabInit, Db.Initialize, …; T 1: PlayerController.OnPrefabInit |
| 279 | 2407398520 | Custom Reservoirs | B,O | – | P 3: Game.LateUpdateComponents, Db.Initialize, GeneratedBuildings.LoadGeneratedBuildings |
| 280 | 1847165596 | I Love Slicksters - Morphs ! | P,O | – | P 12: CREATURES.CreateTemperatureModifier, WildnessMonitor.IsWild, BaseOilFloaterConfig.SetupDiet, … |
| 282 | 2965547030 | Skills Master // by @Ony 👾 | S | touches_ui | P 1: SkillMinionWidget.OnPointerClick; S 2: TableScreen.RefreshRows, SkillsScreen.OnShow |
| 283 | 2041219184 | Debug Console // by @Ony 👾 | P | – | P 1: JobManager.Run |
| 285 | 2163720966 | Adjustable Coolers | P | – | P 4: AirConditionerConfig.ConfigureBuildingTemplate, LiquidConditionerConfig.ConfigureBuildingTemplate, AirConditionerConfig.CreateBuildingDef, … |
| 286 | 2795878144 | Space Radiator | B,O | – | P 3: Localization.Initialize, Db.Initialize, GeneratedBuildings.LoadGeneratedBuildings |
| 287 | 2556517152 | Plastic Door | B,S | touches_ui | P 4: Db.Initialize, Localization.Initialize, BuildingStatusItems.CreateStatusItems, …; S 1: DetailsScreen.OnPrefabInit |
| 288 | 1823182964 | Palmera Tree | P | – | P 3: EntityConfigManager.LoadGeneratedEntities, Immigration.ConfigureCarePackages, SupermaterialRefineryConfig.ConfigureBuildingTemplate |
| 289 | 2964691906 | 更大的施工距离 | P | – | P 1: Game.OnPrefabInit |
| 290 | 3434920353 | Insulated Joint Plate [FIXED] | B | – | P 3: Db.Initialize, BuildingComplete.OnSpawn, Localization.Initialize |
| 291 | 1882896929 | Giant Doors | B | – | P 1: Db.Initialize |
| 292 | 1878592057 | CustomTemperatureOverlay | V | – | V 2: SimDebugView.NormalizedTemperature, OverlayModes.RefreshLegendValues |
| 293 | 1718226085 | Wireless Automation | B | – | P 4: Game.OnPrefabInit, Game.OnLoadLevel, GeneratedBuildings.LoadGeneratedBuildings, … |
| 294 | 1972768699 | Priority Zero | T,S,V | touches_ui | P 2: StandardChoreBase..ctor, MinionTodoChoreEntry.Apply; T 2: PrioritizeTool.OnPrefabInit, PrioritizeTool.Update; S 3: MinionTodoSideScreen.get_priorityInfo, PriorityScreen.InstantiateButtons, PriorityScreen.SetScreenPriority; V 2: PrioritizableRenderer..ctor, PrioritizableRenderer.RenderEveryTick |
| 295 | 2888290233 | Dupes Machinery | B,O | – | P 3: ElementLoader.FinaliseElementsTable, Db.Initialize, GeneratedBuildings.LoadGeneratedBuildings |
| 296 | 1970894610 | Mineral Processing - Metallurgy | B,O | – | P 3: Db.Initialize, GeneratedBuildings.LoadGeneratedBuildings, ComplexFabricator.CompleteWorkingOrder |
| 297 | 2642931981 | Refresh Printing Pod | P | – | – |
| 298 | 3336359859 | 量杯 | P | – | P 4: SerializationConfig.MemberSerialization.OptIn, BottleEmptier.OnCopySettings, BottleEmptierConfig.ConfigureBuildingTemplate, … |
| 299 | 3204363352 | More Emotions | P,O | touches_ui | P 18: SleepChore.InitializeStates, Sleepable.GetWorkPstAnims, SleepChore.SetAnim, … |
| 300 | 2021324045 | Dupes Refrigeration | B,O | – | P 2: Db.Initialize, GeneratedBuildings.LoadGeneratedBuildings |
| 302 | 2429077517 | skill experience 技能经验 | P | – | P 1: MinionResume.AddExperience |
| 303 | 2536616489 | [DLC+Vanilla]Travel Tube Anywhere | P | – | P 1: TravelTubeConfig.CreateBuildingDef |
| 304 | 1934934314 | Advanced Generators | B | – | P 2: Db.Initialize, ThermoelectricGenerator.CreateBuildingDef |
| 305 | 1493391479 | Optimized official Chinese【SU】 | X | – | – |
| 306 | 3096204182 | Giga-watt Wire | B,O | – | P 16: Game.Load, ElementLoader.Load, BuildingDef.Build, … |
| 307 | 2806200110 | High Pressure Applications | B,O,V | – | P 33: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, ConduitBridge.OnPrefabInit, …; V 1: OverlayModes.Update |
| 308 | 3303494244 | Chain Tool | T,S,K,O,V | touches_ui | P 7: Localization.Initialize, Vignette.Reset, Assets.OnPrefabInit, …; T 6: DragTool.OnLeftClickDown, DragTool.OnLeftClickUp, DragTool.OnDeactivateTool, …; S 1: TopLeftControlScreen.OnActivate; V 4: BlockTileRenderer.Render, BlockTileRenderer.Rebuild, OverlayLegend.OnSpawn, … |
| 309 | 2557269577 | [MG]Only Good Traits(仅好特性) | P | – | P 1: Game.OnPrefabInit |
| 310 | 2765256496 | AI Controlled Rockets (DLC) | B,S,O,V | touches_ui | P 24: Localization.Initialize, GeneShufflerRechargeConfig.CreatePrefab, LimitOneCommandModule.EvaluateCondition, …; S 5: AssignPilotAndCrewSideScreen.IsValidForTarget, RocketInteriorSectionSideScreen.IsValidForTarget, HabitatModuleSideScreen.RefreshModulePanel, …; V 1: CameraController.ActiveWorldStarWipe |
| 312 | 2566414917 | [Tiếng Việt] VN Language Pack by sant1ago | X | – | – |
| 313 | 1419578320 | Traducción completa al español actualización Ca… | X | – | – |
| 314 | 3513358591 | Omnificia Asteroid | P | – | P 8: GameplaySeasons.UnusedSeasons, GameplaySeasonInstance.StartEvent, EstablishColonies.GetColonyCount, … |
| 315 | 2557243691 | [MG]Travel Tube Crossing Wall(传送管穿墙) | P | – | – |
| 316 | 3231749807 | Fast Door | P,O | – | P 2: PressureDoorConfig.DoPostConfigureComplete, ManualPressureDoorConfig.ConfigureBuildingTemplate |
| 317 | 2883096049 | [Vanilla + DLC] Signs, Tags and Ribbons | B,S | touches_ui | P 2: Localization.Initialize, GeneratedBuildings.LoadGeneratedBuildings; S 1: DetailsScreen.OnPrefabInit |
| 319 | 2787598894 | The Golden Throne (Buffed gold lavatories) | O,V | – | P 12: BuildingFacades, Assets.OnPrefabInit, Db.Initialize, … |
| 320 | 1861473661 | Autosave Drag Fix | K | – | P 2: Game.OnPrefabInit, Game.DelayedSave |
| 321 | 2612676878 | More Building Info [DEPRECATED] | P | – | – |
| 322 | 2093302246 | Airtight/Airlock Door With Half Door | B | – | P 5: Door.OnPrefabInit, Door.OnCleanUp, Door.Sim200ms, … |
| 324 | 2874973256 | S 增强冰箱 Enhanced Refrigerator 2.0 | P,O | – | P 5: MiniFridgeConfig.CreateBuildingDef, MiniFridgeConfig.DoPostConfigureComplete, RefrigeratorConfig.CreateBuildingDef, … |
| 325 | 2603191165 | Deconstructable Mini-Pod | P | – | P 1: BuildingComplete.OnSpawn |
| 327 | 2018048391 | SuperSmartBattery[Vanilla+DLC] | P | – | P 1: BatterySmartConfig.DoPostConfigureComplete |
| 328 | 2119934242 | Advanced Space Cooler | B | – | P 2: Db.Initialize, GeneratedBuildings.LoadGeneratedBuildings |
| 329 | 2880433226 | Grouped Resources | S,O | touches_ui | P 1: SaveGame.OnPrefabInit; S 3: PinnedResourcesPanel.OnPrefabInit, PinnedResourcesPanel.SortRows, PinnedResourcesPanel.CreateRow |
| 330 | 2962220957 | Modify Difficulty Settings | S,K | touches_ui asset_bundle_ui | P 3: CustomGameSettings.OnDeserialized, Assets.OnPrefabInit, Localization.Initialize; S 1: PauseScreen.ConfigureButtonInfos; K 1: PauseScreen.OnKeyDown |
| 332 | 2555723006 | Rotate Buildings | P | – | P 17: BuildingTemplates.CreateBuildingDef, BunkerDoorConfig.CreateBuildingDef, DoorConfig.CreateBuildingDef, … |
| 333 | 2818855295 | Canned Food | P,O | – | P 6: Localization.Initialize, CraftingTableConfig.ConfigureRecipes, Db.Initialize, … |
| 335 | 2000481647 | Русский (Sci-Tech) | X | – | – |
| 336 | 3390581599 | Decor Pack S - Winters Delight | B,S,O,V | touches_ui asset_bundle_ui | P 18: ArtableStages, InventoryOrganization.GenerateSubcategories, CodexEntryGenerator_Elements.GetElementEntryContext, …; S 1: DetailsScreen.OnPrefabInit; V 1: OverlayModes..ctor |
| 337 | 3746576803 | Too Many Dupes | X | – | – |
| 338 | 3430682737 | Voidria | O,M | – | P 3: MutatedWorldData..ctor, Db.Initialize, ClusterPOIManager.RegisterTemporalTear; M 1: ColonyDestinationSelectScreen.OnSpawn |
| 339 | 1870540864 | DraggableUI | K | – | P 2: KScreen.OnPrefabInit, KScreen.OnSpawn |
| 341 | 3239657659 | 2格水门 | B | – | P 2: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize |
| 342 | 1706161210 | Suit Docks Store 75kg Oxygen | P | – | P 2: SuitLockerConfig.ConfigureBuildingTemplate, JetSuitLockerConfig.ConfigureBuildingTemplate |
| 344 | 3614371572 | Move This Here汉化版 | B,S | touches_ui | P 2: GeneratedBuildings.LoadGeneratedBuildings, BuildingDef.Instantiate; S 2: ProductInfoScreen.SetMaterials, ResourceRemainingDisplayScreen.GetString |
| 345 | 1959130035 | No Pointless Scrollbars | S,O | touches_ui | P 1: Localization.Initialize; S 2: MaterialSelector.UpdateScrollBar, PlanScreen.ConfigurePanelSize |
| 346 | 1860377458 | [CURRENTLY OUT OF DATE] Lights Out (Darkness Re… | V | – | P 7: PauseScreen.OnPrefabInit, MinionConfig.CreatePrefab, RationalAi.InitializeStates, …; V 1: PropertyTextures.UpdateFogOfWar |
| 348 | 3462174357 | Real Time Clock | S,O | touches_ui | P 1: GameClock.Render1000ms; S 1: DateTime.OnSpawn |
| 349 | 1818145851 | Customize Plants | K,O | – | P 25: ModPlants: loading external dll:.CreatePrefab, ColdBreatherConfig.CreatePrefab, ColdBreather.OnReplanted, … |
| 350 | 2169776807 | Clarified Max Decor | P,O | – | P 3: SelectToolHoverTextCard.UpdateHoverElements, DecorDisplayer.GetTooltip, Localization.Initialize |
| 351 | 2462402015 | Passive Gas Vent Input | B,O | – | P 3: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, Localization.Initialize |
| 352 | 3664750796 | Grip Now Included | S,O | touches_ui | P 14: BaseMinionConfig.BaseMinion, BaseRoverConfig.BaseRover, ElementSplitterComponents.CanFirstAbsorbSecond, …; S 1: PauseScreen.ConfigureButtonInfos |
| 353 | 2661907148 | Chemical Processing: Industrial Overhaul Edition | B,V | – | P 24: HarvestablePOIConfig.GenerateConfigs, EntityConfigManager.LoadGeneratedEntities, OxygenBreather.Sim200ms, … |
| 354 | 1989737447 | Default Building Settings | S,O | touches_ui | P 6: BatterySmart.OnPrefabInit, Repairable.InitializeStates, ManualGeneratorConfig.DoPostConfigureComplete, …; S 1: TreeFilterableSideScreen.IsValidForTarget |
| 355 | 3384457117 | Instant Switch Toggle | T,K | – | T 1: SelectTool.OnLeftClickDown |
| 356 | 2032187035 | Popup Control | T,K,O | – | P 5: FetchAreaChore.Complete, LiquidPumpingStation.OnStopWork, Pickupable.OnCompleteWork, …; T 1: ToolMenu.OnKeyDown |
| 357 | 3586927171 | Water Gate | B,O | – | P 2: Localization.Initialize, Db.Initialize |
| 359 | 2593265755 | Fan Tiles | B,O | – | P 3: ElementConsumer.GetSampleCell, Db.Initialize, Localization.Initialize |
| 360 | 1909037839 | Clean HUD | S,O,V | touches_ui | P 4: LightBufferCompositor.Start, LightBufferCompositor.OnRenderImage, Vignette.OnSpawn, …; S 1: ManagementMenu.OnPrefabInit |
| 361 | 3405454008 | Stats Unlimited Fixed | S,O | touches_ui | P 8: AttributeLevel.AddExperience, MinionStartingStats.ApplyExperience, MinionStartingStats.Deliver, …; S 2: NewBaseScreen.SpawnMinions, PopFXManager.SpawnFX |
| 363 | 3113986230 | Wall Pumps - Fixed | B,O | – | P 10: ElementConsumer.GetSampleCell, Exhaust.UpdateEmission, Pump.IsPumpable, … |
| 364 | 2037089892 | Trashcans | B,S | touches_ui | P 2: Db.Initialize, Game.OnPrefabInit |
| 365 | 2890083659 | 游手的小工具箱 | T,O | – | P 20: Localization.Initialize, SolidConduitDispenser.ConduitUpdate, Wire.GetMaxWattageAsFloat, …; T 1: MopTool.OnPrefabInit |
| 366 | 2451195132 | Insulated Radbolt Joint Plate | B,O | – | – |
| 368 | 3145815349 | Advanced Coolers | B,O | – | P 1: Db.Initialize |
| 369 | 1967921388 | Stock Bug Fix | O,M | touches_ui | P 29: ChoreDriver.<InitializeStates>b__6_3, HoverTextHelper.MassStringsReadOnly, Steam.Bugs.ModUpdateRace, …; M 1: MainMenu.OnSpawn |
| 371 | 1911094447 | Any Starting Dupe | P | – | P 1: Db.Initialize |
| 372 | 3505882053 | Better Wires | P | – | P 1: Wire.GetMaxWattageAsFloat |
| 373 | 1717499632 | Less Waste From Jetpacks | P | – | P 1: JetSuitMonitor.Emit |
| 374 | 2684601534 | Dismantle the rocket | P | – | P 2: RocketWallTileConfig.DoPostConfigureComplete, RocketEnvelopeWindowTileConfig.DoPostConfigureComplete |
| 375 | 1868495239 | Huge Battery | B | – | P 3: GeneratedBuildings.LoadGeneratedBuildings, Battery.AddEnergy, Db.Initialize |
| 376 | 2877532470 | Make the game easier-轻松游戏 | P,O | – | P 65: CeilingLightConfig.ConfigureBuildingTemplate, ElectrobankCharger.ChargeInternal, InsulatedDoorConfig.CreateBuildingDef, … |
| 377 | 1899088142 | Carousel | B,O | – | – |
| 379 | 2053191586 | New Infinite Sources[Stop updating] | B,S | touches_ui | P 5: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, Manager.GetType, …; S 1: FilterSideScreen.IsValidForTarget |
| 382 | 2359640901 | Rename Asteroids | S | touches_ui | S 3: WorldSelector.AddWorld, WorldSelector.SpawnToggles, DetailsScreen.OnPrefabInit |
| 383 | 2808978019 | Hold Construction | T,S,K | touches_ui | P 8: Constructable.OnRefreshUserMenu, Constructable.PlaceDiggables, Deconstructable.OnRefreshUserMenu, …; T 4: DeconstructTool.OnActivateTool, DeconstructTool.OnDeactivateTool, DragTool.OnKeyDown, …; S 2: PriorityScreen.InstantiateButtons, UserMenuScreen.RefreshPrioritizable; K 1: KInputController.GenerateActionFlagTable |
| 384 | 1914501780 | Food Supply Tooltips | S | touches_ui | P 4: CreatureCalorieMonitor.GetDescriptors, Crop.InformationDescriptors, MeterScreen_Rations.OnTooltip, …; S 2: SimpleInfoScreen.OnSelectTarget, SimpleInfoScreen.OnDeselectTarget |
| 385 | 2644716140 | Neveroverpress(永不超压) | B | – | P 1: GeneratedBuildings.LoadGeneratedBuildings |
| 386 | 3114318122 | Travel Tube Crossing Ladder | P | – | – |
| 387 | 2871950472 | S 杂交水稻 Hybrid Rice 1.7 | P,O | – | P 2: BasicSingleHarvestPlantConfig.CreatePrefab, Db.Initialize |
| 389 | 2383900750 | [Vanilla + DLC] Conveyor Rail Display | P | – | – |
| 390 | 2898540318 | 环保户外厕所 EP Outhouse | P | – | P 1: OuthouseConfig.ConfigureBuildingTemplate |
| 391 | 1913589787 | Big Storage | B,O | – | P 4: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, FilteredStorage.OnFilterChanged, … |
| 393 | 1766302971 | No Leaky Walls | P | – | P 2: ExteriorWallConfig.CreateBuildingDef, ThermalBlockConfig.CreateBuildingDef |
| 395 | 2277619848 | No Sensor Limits | S | touches_ui | S 5: CapacityControlSideScreen.SetTarget, CapacityControlSideScreen.UpdateMaxCapacity, ThresholdSwitchSideScreen.SetTarget, … |
| 396 | 2111768571 | Dupes Cuisine [ Currently Broken / Under Mainte… | B | – | P 6: EntityConfigManager.LoadGeneratedEntities, Immigration.ConfigureCarePackages, SettingsCache.LoadFiles, … |
| 397 | 2713769536 | Temperature Controller | B,O | – | P 4: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, CoolingElementConfig.CreateBuildingDef, … |
| 398 | 2093080114 | Unrestricted Transit Tubes | P | – | P 1: UtilityNetworkTubesManager.CanAddConnection |
| 399 | 3303976854 | True Spices (texture mod) | P | – | P 2: Assets.OnPrefabInit, Spices..ctor |
| 400 | 2433887112 | Workshop Profiles | S,O | touches_ui | P 2: StandardChoreBase.AddPrecondition, GeneratedBuildings.LoadGeneratedBuildings; S 1: DetailsScreen.OnPrefabInit |
| 401 | 1891350955 | Puft Morphs | P | – | P 2: KAnimGroupFile.Load, CreatureCalorieMonitor.Poop |
| 402 | 1911357229 | Diseases Restored | B | – | P 34: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, HandSanitizer.OnWorkTick, … |
| 403 | 1722760223 | Reset Build Priority | S | touches_ui | S 2: PlanScreen.CloseCategoryPanel, PlanScreen.OnClickCategory |
| 404 | 2794010196 | Dumping Sign | B,O | – | P 1: Db.Initialize |
| 405 | 3333197339 | Priority Master // by @Ony 👾 | T,S | touches_ui | P 4: ChoreConsumer.ChooseChore, EntityModifierSet.Initialize, GameUtil.AreChoresUIMergeable, …; T 3: InterfaceTool.OnMouseMove, PlayerController.ActivateTool, ToolMenu.OnPriorityClicked; S 4: PriorityScreen.ResetPriority, ProductInfoScreen.SetMaterials, MaterialSelectionPanel.OnPriorityClicked, … |
| 407 | 2897886291 | Sweep Zones | T,S,K,V | touches_ui | P 4: Localization.Initialize, SaveGame.OnPrefabInit, StatusItem.GetStatusItemOverlayBySimViewMode, …; T 3: PlayerController.OnPrefabInit, ToolMenu.CreateBasicTools, ToolMenu.OnPrefabInit; S 1: OverlayMenu.InitializeToggles; V 3: OverlayLegend.OnSpawn, OverlayScreen.RegisterModes, SimDebugView.OnPrefabInit |
| 408 | 2871638199 | High speed experience高速经验 | P,O | – | P 1: MinionResume.AddExperience |
| 410 | 3749231019 | Natural Construction - Tiles & Backwalls | B,S,O | touches_ui | P 4: Constructable.FinishConstruction, CopyBuildingSettings.ResolveLayer, GeneratedBuildings.LoadGeneratedBuildings, …; S 2: MaterialSelector.UpdateScrollBar, MaterialSelector.ConfigureScreen |
| 411 | 2432751162 | Wild Planter Box | B,O | – | P 3: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, ReceptacleMonitor.SetReceptacle |
| 412 | 1879993030 | Repeat Last Build // by @Ony 👾 | T | – | P 1: Game.DestroyInstances; T 2: BaseUtilityBuildTool.Activate, BuildTool.Activate |
| 413 | 1991239137 | Micro Transformer | B | – | P 1: Db.Initialize |
| 414 | 2381541381 | Egg Cryopreservation | B,S | touches_ui | P 6: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, Localization.Initialize, …; S 2: LoadingOverlay.Load, SimpleInfoScreen.RefreshStoragePanel |
| 415 | 2562813586 | Custom Generators | B | – | P 3: Db.Initialize, GeneratedBuildings.LoadGeneratedBuildings, ElementLoader.FinaliseElementsTable |
| 418 | 1872801058 | Raincheck (Printing Pod Time Refund) | P,O | – | P 1: Telepad.RejectAll |
| 419 | 2836891707 | AutomaticMetalRefinery | P | – | P 2: RockCrusherConfig.ConfigureBuildingTemplate, MetalRefineryConfig.ConfigureBuildingTemplate |
| 420 | 2883629440 | Dupes Engineering | B,O | – | P 3: ElementLoader.FinaliseElementsTable, Db.Initialize, GeneratedBuildings.LoadGeneratedBuildings |
| 421 | 1737903327 | DisplayAllTemps | S | touches_ui | P 1: GameUtil.GetFormattedTemperature; S 4: UnitConfigurationScreen.DisplayCurrentUnit, UnitConfigurationScreen.OnCelsiusClicked, UnitConfigurationScreen.OnFahrenheitClicked, … |
| 422 | 2732972618 | Clean Deconstruction | P | – | P 1: Deconstructable.OnCompleteWork |
| 423 | 3215097987 | Space Store 太空商店 | T,S,K,O | touches_ui | P 6: GeyserGenericConfig.CreateGeyser, EntityTemplates.CreateOreEntity, EntityTemplates.CreateAndRegisterSeedForPlant, …; T 2: PlayerController.OnPrefabInit, ToolMenu.CreateBasicTools |
| 424 | 2151864710 | Pip Morphs | P | – | P 5: KAnimGroupFile.Load, Localization.Initialize, ColdWheatConfig.CreatePrefab, … |
| 425 | 3284635963 | Mods Presets Organizer (MPOrganizer) // by @Ony… | M | touches_ui | M 1: MainMenu.MakeButton |
| 428 | 3547651475 | 无限擦拭 | T | – | T 1: MopTool.OnPrefabInit |
| 429 | 2761462871 | Rover's Recharger | B,O | – | P 4: RobotBatteryMonitor.InitializeStates, ScoutModuleConfig.DoPostConfigureComplete, DropToUserCapacity.OnCompleteWork, … |
| 430 | 2013164217 | System Clock | P | touches_ui | P 1: MeterScreen_Rations.InternalRefresh |
| 431 | 1703400426 | Debug Does Not Discover Map | P | – | P 1: DebugHandler.OnKeyDown |
| 432 | 2557346794 | Sharing Is Caring | P,O | – | P 3: SleepChoreMonitor.UpdateBed, SleepChore.InitializeStates, EatChore.InitializeStates |
| 433 | 2836853288 | AutomaticDesalinator | P | – | P 1: Desalinator.UpdateStorageLeft |
| 436 | 1992978572 | Deconstruct Only Buildings | T | – | T 1: FilteredDragTool.GetDefaultFilters |
| 437 | 1938117526 | Mechanics Station | B,O | – | P 13: MachinePartsConfig.CreatePrefab, ComplexFabricator.DropExcessIngredients, Tinkerable.HasCorrectRoom, … |
| 438 | 2834936494 | Forbid Items | K | – | P 7: ChoreConsumer.CanReach, EntityTemplates.CreateBaseOreTemplates, EntityTemplates.CreateLooseEntity, … |
| 439 | 2263396355 | Drecko Morphs | P | – | P 5: KAnimGroupFile.Load, SeaLettuceConfig.CreatePrefab, SaltPlantConfig.CreatePrefab, … |
| 440 | 3582340906 | Buildable Natural Tile – Fix | B,S,O | touches_ui | P 3: Localization.Initialize, Db.Initialize, BuildingComplete.OnSpawn; S 1: MaterialSelector.GetValidMaterials |
| 441 | 3164212090 | Build Menu Search Quick Select Hotkeys | S,K,O | touches_ui | P 1: Localization.Initialize; S 1: BuildingGroupScreen.OnSpawn |
| 442 | 2627472154 | IceBox | B,O | – | P 6: Db.Initialize, Localization.Initialize, RefrigeratorConfig.CreateBuildingDef, … |
| 443 | 3734826909 | Search Overlay (Previously known as Material Se… | S,K,O,V | touches_ui | S 1: OverlayMenu.InitializeToggles; V 3: OverlayLegend.OnSpawn, OverlayScreen.RegisterModes, SimDebugView.OnPrefabInit |
| 444 | 3321776010 | 简易研究simple research | P | – | P 3: NuclearResearchCenterConfig.ConfigureBuildingTemplate, CosmicResearchCenterConfig.ConfigureBuildingTemplate, DLC1CosmicResearchCenterConfig.ConfigureBuildingTemplate |
| 445 | 1834180425 | Hyper Reservoirs (Vanilla/DLC) | B,O | – | – |
| 446 | 2893508694 | Food Poisoning Dies In Pure Water | P | – | P 1: FoodGerms.PopulateElemGrowthInfo |
| 447 | 3286559448 | Bubbles Bathtub | B | – | P 8: BuildingFacades, InventoryOrganization.GenerateSubcategories, BuildingStatusItems.CreateStatusItems, … |
| 449 | 3024728464 | Extended Building Width | T,S,K,O | touches_ui | P 5: BuildingTemplates.CreateBuildingDef, GeneratedBuildings.RegisterWithOverlay, GeneratedBuildings.LoadGeneratedBuildings, …; T 1: BuildTool.OnKeyDown; S 1: PlanScreen.OnClickCopyBuilding |
| 450 | 3305595229 | Build Over Plants | P | – | P 4: String.IsAreaClear, Boolean.IsAreaClear, RoomProber.AddRoomContentsToCavities, … |
| 451 | 2982985962 | Auto Suit Delivery | P,O | – | P 9: SuitLocker.ConfigRequestSuit, SuitLocker.ConfigNoSuit, SuitLocker.DropSuit, … |
| 452 | 2350553957 | Smelter | B,O | – | P 5: LiquidCooledFueledRefinery.OnSpawn, ComplexFabricator.StartWorkingOrder, ComplexFabricator.SpawnOrderProduct, … |
| 453 | 2852279246 | Rebuild Tiles | S | touches_ui | P 3: GeneratedBuildings.LoadGeneratedBuildings, BuildingHP.OnDoBuildingDamage, BuildingHP.DestroyOnDamaged; S 1: LoadingOverlay.Load |
| 454 | 1716259377 | Power Tweaks | P,O | – | P 6: GasFilterConfig.CreateBuildingDef, GasPumpConfig.CreateBuildingDef, PressureDoorConfig.CreateBuildingDef, … |
| 455 | 1938747693 | Thermal Interface Plate | B | – | P 1: Blueprints..ctor |
| 456 | 3108337158 | B 超级氧齿蕨 Super Oxyfern 1.1 | P | – | P 2: OxyfernConfig.CreatePrefab, Oxyfern.SetConsumptionRate |
| 457 | 1885249412 | Adjustable Transformers | S | touches_ui | P 5: PowerTransformerConfig.CreateBuildingDef, PowerTransformerConfig.ConfigureBuildingTemplate, PowerTransformerSmallConfig.CreateBuildingDef, …; S 1: CapacityControlSideScreen.SetTarget |
| 460 | 2840104621 | Scan & Relay Satellites (DLC) | B,S,O | touches_ui | P 9: Assets.OnPrefabInit, MissionControlCluster.UpdateWorkableRocketsInRange, CraftingTableConfig.ConfigureRecipes, …; S 4: DetailsScreen.OnPrefabInit, GeoTunerSideScreen.RefreshOptions, RemoteWorkTerminalSidescreen.RefreshOptions, … |
| 461 | 2995311574 | 自动堆肥/ Auto Compost | P,O | – | P 3: CompostConfig.CreateBuildingDef, CompostConfig.ConfigureBuildingTemplate, Compost.InitializeStates |
| 462 | 2778941969 | Better Farming Effects and Tweaks | P,O | – | P 51: ClimbableTreeMonitor.FindClimbableTree, TreeClimbStates.GetClimbableCell, TreeClimbStates.Rummage, … |
| 463 | 2853319334 | Super Hydrogen Generator超级氢气发电机 | P,O | – | P 2: HydrogenGeneratorConfig.CreateBuildingDef, HydrogenGeneratorConfig.DoPostConfigureComplete |
| 465 | 2820896505 | Extended Bridges Fix - Bring Order To Your Fact… | B | – | P 3: GeneratedBuildings.LoadGeneratedBuildings, Techs.Init, Building.OnSpawn |
| 466 | 1887236601 | Favorite Buildings ⭐ Custom Key Bindings // by … | S,K | touches_ui | P 2: TMP_InputField.ActivateInputFieldInternal, TMP_InputField.DeactivateInputField; S 2: BuildMenu.UseHotkeyBuildMenu, PlanScreen.GetBuildingPriority; K 2: KIconToggleMenu.OnKeyDown, KInputController.Bind |
| 467 | 3536603467 | Random Clothes | T,O | – | P 1: MinionIdentity.OnSpawn; T 1: PlayerController.OnPrefabInit |
| 468 | 2567579363 | [MG]Growing fast(快速生长) | P | – | P 1: Growing.OnPrefabInit |
| 469 | 2845101493 | J 优化制氧机 Optimize Oxygen Generator 2.2 | B,O | – | P 2: GeneratedBuildings.LoadGeneratedBuildings, Localization.Initialize |
| 470 | 2607620648 | Mismatched Wire Finder | K | – | P 4: Conduit.OnSpawn, FlowUtilityNetwork.AddItem, FlowUtilityNetwork.Reset, … |
| 471 | 2859877461 | Moveable Printing Pods | P | – | P 4: HeadquartersConfig.CreateBuildingDef, BuildingConfigManager.RegisterBuilding, BuildingComplete.OnSpawn, … |
| 473 | 3160195957 | Paint your Pipes & Wires | T,S,K,O,V | touches_ui | P 9: Patches.ChangeBuildingColor, ReverseBridgeButton.MakeNewBuilding, ConduitFlowVisualizer.GetCellTintColour, …; T 2: BuildTool.OnDeactivateTool, PlayerController.OnKeyDown; S 4: PlanScreen.OnClickCopyBuilding, TopLeftControlScreen.OnActivate, DetailsScreen.OnPrefabInit, …; V 4: OverlayModes.Enable, OverlayModes.Update, OverlayModes.Disable, … |
| 474 | 2119775064 | Advanced Liquid Cooler | B | – | P 2: Db.Initialize, GeneratedBuildings.LoadGeneratedBuildings |
| 475 | 3385429972 | More Bionics | X | – | – |
| 476 | 2982311017 | Reverse Bridges | P | – | P 3: GasConduitBridgeConfig.ConfigureBuildingTemplate, LiquidConduitBridgeConfig.ConfigureBuildingTemplate, SolidConduitBridgeConfig.ConfigureBuildingTemplate |
| 477 | 3292486533 | N 电力系统 Power System 1.8 | B,O | – | P 23: ElectrobankChargerConfig.CreateBuildingDef, ElectrobankCharger.ChargeInternal, Electrobank.OnPrefabInit, … |
| 478 | 3358334623 | Mineral Processing - Mining | B | – | P 5: ComplexFabricator.CompleteWorkingOrder, ComplexFabricator.Sim200ms, Db.Initialize, … |
| 479 | 2014558219 | Automation Expanded | B,S | touches_ui | P 5: GeneratedBuildings.LoadGeneratedBuildings, Techs.Load, LogicCircuitNetwork.AddItem, …; S 1: DetailsScreen.OnPrefabInit |
| 480 | 2020793956 | Pacu Morphs | P | – | P 3: KAnimGroupFile.Load, EntityConfigManager.LoadGeneratedEntities, ModifierSet.Initialize |
| 481 | 3486175417 | 1000TLargeStorageLocker | P | – | P 1: StorageLockerConfig.ConfigureBuildingTemplate |
| 482 | 2197860553 | [DLC support] BUILDINGS CAN`T  BE DEMAGED// 建筑和… | P | – | P 1: BuildingHP.DoDamage |
| 483 | 3755915137 | Fast Insulated Self Sealing AirLock Continued | B,O | – | P 12: BuildingDef.Build, Deconstructable.SpawnItemsFromConstruction, Db.Initialize, … |
| 486 | 2844433909 | Safe Autosave | S | touches_ui | P 1: Game.DelayedSave; S 1: KScreen.OnShow |
| 487 | 3609497409 | Rendezvous Table | B | – | P 5: MultiMinionDiningTable.SpawnSeat, MultiMinionDiningTable.OnSpawn, MultiMinionDiningTable.get_SeatConfig, … |
| 488 | 2616068109 | No Suit Wear | P | – | P 1: Durability.DeltaDurability |
| 489 | 2984782334 | 魔改电冰箱 | B | – | P 2: Db.Initialize, GeneratedBuildings.LoadGeneratedBuildings |
| 490 | 3560856988 | POIs Now Included | P,O | – | P 4: GeneratedBuildings.LoadGeneratedBuildings, TilePOIConfig.CreateBuildingDef, Db.Initialize, … |
| 491 | 1820831167 | Laser Turret (Vanilla/DLC) | B,O | – | – |
| 493 | 3544371748 | Splitters MK II | B | – | P 2: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize |
| 494 | 2825634502 | Gambling Machine | B | – | P 1: Db.Initialize |
| 495 | 1861918351 | Buildable POI Doors | B | – | P 1: Db.Initialize |
| 497 | 2861126557 | Magpie Bridge鹊桥 | B | – | P 1: GeneratedBuildings.LoadGeneratedBuildings |
| 498 | 3121297403 | Utility Glass | B | – | P 3: GeneratedBuildings.LoadGeneratedBuildings, Db.Initialize, Localization.Initialize |
| 499 | 3612606893 | Filterable Auto-Compost | P,O | touches_ui | P 5: Compostable.MarkForCompost, WorldInventory.OnSpawn, FilteredStorage.IsFunctional, … |
| 500 | 2046407638 | Clothing Locker | B | – | – |
