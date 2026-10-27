# Batches for ranks 751–2274

The mods of ranks 751–2274 in `scripts/mod-catalog.tsv` (with ranks 1–750, the catalog's 2000 that
work in the original) as the load sweep `s2000` found them: `scripts/mod-sweep.py`, results in
[`sweeps/s2000.tsv`](sweeps/s2000.tsv), each batch in the original and in ours to the main menu and
then 90 s of the 2minnow colony. Spec writers take a batch from [Batches](#batches) and follow
[Batch workflow](README.md#batch-workflow).

- **1088 of the 1344 new rows swept** (the one the catalog has not analysed is not installed).
- **255 not swept yet** (ranks 1970–2274): the sweep goes on from its batch 48.
- **937 load in both players**: the batches below.
- **20 fail in ours only**: engine bugs, to fix before those mods are played.
- **99 fail to load in the original as well.**
- **32 not swept**: translations (installed disabled, as a subscription leaves them) and one mod SteamCMD cannot download.

**Hints** come from metadata, not from reading the mod (tools/ModTriage and onimaxxing's analysis):
`PLib options` (a PLib options dialog; set them through `options:`), `UI` (screens of its own or
patches of the game's), `bundle UI` (UI prefabs from an asset bundle), `user menu` (buttons on a
selected object), `worldgen`, `elements`, `needs ID` (Workshop mods it requires, installed with it),
`base game only`, and onimaxxing's traits beyond Harmony code (`noCode`, `nativeCalls`, `monomod`,
...). **Classes** are ModTriage's (mod-play-testing.md §3.1): P passive, B buildings, T tools, S
screens, K keys, O options, V overlays and visuals, M Duplicants, X no code.

## Fail in ours only

Each loads in the original with its batch and fails in ours. The error is what ours logs with the
mod alone (or with the mods named) that the original's run of the batch does not.

| Rank | Mod | Fails at | Error |
|---|---|---|---|
| 831 | [Darkness Not Excluded (Relit)](https://steamcommunity.com/sharedfiles/filedetails/?id=3609476592) | main menu | the player exits (1) while loading; DarknessNotExcluded: Failed to patch Workable.GetEfficiencyMultiplier() |
| 987 | [Text Input Hotkeys](https://steamcommunity.com/sharedfiles/filedetails/?id=3165114076) | main menu | step start: <urlopen error [Errno 111] Connection refused>; ours disables it ("Failed to load mod ...disabling"); ---> System.InvalidProgramException: Common Language Runtime detected an invalid program @ HarmonyLib.PatchFunctions.UpdateWrapper_PatchN; HarmonyLib.HarmonyException: Patching exception in method TMPro.EditState TMPro.TMP_InputField::KeyPressed(UnityEngine.Event evt) @ HarmonyLib.PatchFunctions.UpdateWrapper_PatchN; null Exception while loading mod Aze.DeleteFullWord at RUN/home/config/unityNd/Klei/Oxygen Not Included/mods/Local/N @ HarmonyLib.PatchFunctions.UpdateWrapper_PatchN |
| 999 | [Reversible Heater Clones](https://steamcommunity.com/sharedfiles/filedetails/?id=3710035894) | colony | ---> System.ArgumentException: Undefined target method for patch method static System.Boolean ReversibleHeaterClones.SpaceHeater_BoolModeSetter_CloneBlockPatch::Prefix(SpaceHeater __instance, System.Reflection.MethodBase __originalMethod) @ HarmonyLib.PatchClassProcessor.PatchWithAttributes; ---> System.ArgumentException: Undefined target method for patch method static System.Void ReversibleHeaterClones.HeaterSideScreen_PrepareTarget_CloneCleanupPatch::Prefix(System.Object[] __args) @ HarmonyLib.PatchClassProcessor.PatchWithAttributes; [ReversibleHeaterClones] Failed patch HeaterSideScreen_Pre |
| 1071 | [Trash Cans (Trashcans Fixed)](https://steamcommunity.com/sharedfiles/filedetails/?id=3398520692) | colony | the player exits (1) while loading; System.MissingMethodException: Cannot dynamically create an instance of type "SonJeremy.TrashCans.AutoConsumption.AutoFluidTrashCans". Reason: No parameterless constructor defined @ UnityEngine.Object.ConstructBound[T](Type type, NativeObject native) in /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/src/OniBrutal.UnityEngine/Core/Object.cs.line N; null Exception in RegisterBuilding for type SonJeremy.TrashCans.BuildingConfig.GasTrashCanConfig from TrashCans @ UnityEngine.Object.ConstructBound[T](Type type, NativeObject native |
| 1080 | [Printerceptor Reimagined + Minnow's Shop](https://steamcommunity.com/sharedfiles/filedetails/?id=3637505541) | colony | [GravitasMod] Patch failed for KModalScreen.OnCmpDisable: System.ArgumentException: You can only patch implemented methods/constructors. Patch the declared method virtual System.Void KModalScreen::OnCmpDisable() instead @ HarmonyLib.PatchProcessor.Patch_PatchN; [GravitasMod] Patch failed for KModalScreen.OnDeactivate: System.ArgumentException: You can only patch implemented methods/constructors. Patch the declared method virtual System.Void KModalScreen::OnDeactivate() instead @ HarmonyLib.PatchProcessor.Patch_PatchN; [GravitasMod] Patch failed for KScreen.OnCleanUp: System.ArgumentException: |
| 1120 | [小动物公寓bug修复](https://steamcommunity.com/sharedfiles/filedetails/?id=3773818206) | colony | the crash screen opens while the save loads; ---> System.InvalidCastException: Unable to cast object of type "Context[Rottable,Rottable+Instance,IStateMachineTarget,Rottable+Def]" to type "Context[CritterCondoInteractMontior,CritterCondoInteractMontior+Instance,IStateMac |
| 1127 | [更好的存档（BetterSave）](https://steamcommunity.com/sharedfiles/filedetails/?id=3805601277) | main menu | step start: Remote end closed connection without response; [更好的存档] OnLoad 失败: System.MissingMethodException: Method not found: "Void UnityEngine.Application.add_quitting(System.Action)" @ SaveOpt.ModLoader.InstallQuitHook |
| 1141 | [ManualMiner](https://steamcommunity.com/sharedfiles/filedetails/?id=3638972299) | colony | the crash screen opens while the save loads; TypeLoadException: Could not load type "LimitVelocityOverLifetimeModule" from assembly "UnityEngine.ParticleSystemModule, Version=N.N, Culture=neutral, PublicKeyToken=null" @ CursorLaserMining.Visual_Classic.ConfigureMasterFX |
| 1152 | [Delivery Temperature Limit](https://steamcommunity.com/sharedfiles/filedetails/?id=2963257205) | main menu | step start: [Errno 104] Connection reset by peer; ---> System.NullReferenceException: Object reference not set to an instance of an object @ DeliveryTemperatureLimit.StatusItemsUpdaterPatch.RenderNms; DeliveryTemperatureLimit: Failed to find FetchAreaChore.StatesInstance.Begin() delegate for patching; System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation @ DeliveryTemperatureLimit.StatusItemsUpdaterPatch.RenderNms (and 1 more) |
| 1209 | [Fixes and Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2967596358) | main menu | step start: [Errno 104] Connection reset by peer; ours disables it ("Failed to load mod ...disabling"); ---> System.InvalidProgramException: Common Language Runtime detected an invalid program @ HarmonyLib.PatchFunctions.UpdateWrapper_PatchN; HarmonyLib.HarmonyException: Patching exception in method System.Void Timelapser::SetPostionAndOrtho(System.IntN world_id) @ HarmonyLib.PatchFunctions.UpdateWrapper_PatchN; null Exception while loading mod llunak.FixesAndTweaks at RUN/home/config/unityNd/Klei/Oxygen Not Included/mods/Local/N @ HarmonyLib.PatchFunctions.UpdateWrapper_PatchN |
| 1228 | [Stacking bed 叠叠床](https://steamcommunity.com/sharedfiles/filedetails/?id=2857940400) | main menu | step start: the player exited (1) before OniMcp started; ours disables it ("Failed to load mod ...disabling"); System.IO.FileNotFoundException: Could not load file or assembly "PLib, Version=N.N, Culture=neutral, PublicKeyToken=null". The system cannot find the file specified @ Stacking_bed_叠叠床.ModLoad.OnLoad; null Exception while loading mod N.Local at RUN/home/config/unityNd/Klei/Oxygen Not Included/mods/Local/N @ Stacking_bed_叠叠床.ModLoad.OnLoad |
| 1230 | [Colony Cuisine / 殖民地食光](https://steamcommunity.com/sharedfiles/filedetails/?id=3799038247) | colony | the crash screen opens while the save loads; System.Collections.Generic.KeyNotFoundException: The given key "N" was not present in the dictionary @ ColonyDiagnosticUtility.GetDiagnostic; Assert failed: Clustercraft Exterior Door has no targetDoor |
| 1255 | [Twitch Integration](https://steamcommunity.com/sharedfiles/filedetails/?id=2983351482) | main menu | step start: the player exited (1) before OniMcp started; ours disables it ("Failed to load mod ...disabling"); System.NullReferenceException: Object reference not set to an instance of an object @ ONITwitch.DefaultCommands.SetupCommands; System.MissingMethodException: Method not found: "Void UnityEngine.PhysicsND.IgnoreLayerCollision(IntN, IntN, Boolean)" @ ONITwitch.Patches.PhysicsPatches.MainMenu_OnSpawn_Patch.Postfix; System.TypeLoadException: Could not load type "UnityEngine.Bindings.NativeHeaderAttribute" from assembly "UnityEngine.SharedIntern |
| 1429 | [Public Luxury Beds](https://steamcommunity.com/sharedfiles/filedetails/?id=2363171361) | colony | the crash screen opens while the save loads; N Exception in: (Minion).SleepChoreMonitor.root.checkforbed @ Public_Luxury_Beds.Patches.Patches.Postfix_UpdateBed(Instance __instance) in C.\Repos\MrPurpleN\ONI\Public Luxury Beds\Patches\Patches.cs.line N; System.MethodAcces |
| 1439 | [Rephysicalized](https://steamcommunity.com/sharedfiles/filedetails/?id=3609531831) | colony | NullReferenceException: Object reference not set to an instance of an object @ UnityEngine.Object.ThrowNullReference; SeaTurtle Exception in: (SeaTurtle).EatStates.root.eating.loop @ Rephysicalized.FueledDietController.TryApplyFuelConversion; System.NullReferenceException: Object reference not set to an instance of an object @ Rephysicalized.FueledDietController.TryApplyFuelConversion |
| 1597 | [Public Cots](https://steamcommunity.com/sharedfiles/filedetails/?id=2361043018) | colony | the crash screen opens while the save loads; N Exception in: (Minion).SleepChoreMonitor.root.checkforbed @ Public_Cots.Patches.Patches.Postfix_UpdateBed(Instance __instance) in C.\Repos\MrPurpleN\ONI\Public Cots\Patches\Patches.cs.line N; System.MethodAccessException: At |
| 1694 | [Super Generator超级发电机](https://steamcommunity.com/sharedfiles/filedetails/?id=3546567438) | colony | IndexOutOfRangeException: Index was outside the bounds of the array @ Internal.Runtime.CompilerHelpers.ThrowHelpers.ThrowIndexOutOfRangeException |
| 1764 | [Delivery Temperature Limit (Supercooled)](https://steamcommunity.com/sharedfiles/filedetails/?id=3759075866) | main menu | step start: the player exited (1) before OniMcp started; DeliveryTemperatureLimit: Failed to find FetchAreaChore.StatesInstance.Begin() delegate for patching |
| 1817 | [Chaos Events for Twitch Integration](https://steamcommunity.com/sharedfiles/filedetails/?id=3433108302) | colony | the player exits (1) while loading; ---> System.ArgumentException: The Object you want to instantiate is null @ UnityEngine.Object.Instantiate(Object original) in /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/src/OniBrutal.UnityEngine/Core/Instantiate.cs.line N; ArgumentException: The Object you want to instantiate is null @ UnityEngine.Object.Instantiate; ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection. (Parameter "index") @ BackwallManager+BackwallIndexer.get_Element (and 55 more) |
| 1946 | [Abyssalite深渊晶石](https://steamcommunity.com/sharedfiles/filedetails/?id=3600537542) | main menu | the player exits (1) while loading; ours disables it ("Failed to load mod ...disabling"); System.BadImageFormatException: Enclosing type(s) not found for type "‭⁬‪‌‎‫​⁯‭⁫‌‍‏⁪‪‭​‎‬​‎​⁭​‫‭‮⁮‏‌‫​‍‍⁮⁪‪‌‍⁬‮" in assembly "Katairite, Version=N.N, Culture=neutral, PublicKeyToken=null" @ KMod.DLLLoader.LoadDLLs; null Exception while loading mod Abyssalite at RUN/home/config/unityNd/Klei/Oxygen Not Included/mods/Local/N @ KMod.DLLLoader.LoadDLLs |

## Fail in the original as well

| Rank | Mod | Fails at | Error in the original |
|---|---|---|---|
| 804 | [Darkness Not Excluded (fixed)](https://steamcommunity.com/sharedfiles/filedetails/?id=3042467105) | colony | the crash screen opens while the save loads; System.MissingFieldException: Field not found: int .CavityInfo.numCells Due to: Could not find field in class @ Darkness |
| 846 | [Start with all Research](https://steamcommunity.com/sharedfiles/filedetails/?id=2034132436) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; System.MissingMethodException: Method not found: EdiblesManager/FoodInfo .FoodInfo.AddEffects(System.Collections.Generic.List`N<string>,string[]) @ EntityConfigManager.RegisterEntity; System.MissingMethodException: Method not found: void Klei.CustomSettings.SettingLevel..ctor(string,string,string,int,object) @ KMod.DLLLoader.LoadDLLs |
| 847 | [冰淇淋 Ice Cream -K13](https://steamcommunity.com/sharedfiles/filedetails/?id=3071641802) | colony | the player exits (1) while loading; ArgumentException: The Object you want to instantiate is null @ UnityEngine.Object.Instantiate; ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection @ BackwallManager+BackwallIndexer.get_Element |
| 863 | [气闭门-不卡小人版本(SelfSealingAirlocks-Don't get stuck)](https://steamcommunity.com/sharedfiles/filedetails/?id=2659596916) | colony | the crash screen opens while the save loads; DoorComplete Exception in: (Door).Door+Controller.root.closed.SetWorldStateClosed; System.MissingMethodException: Method not f |
| 880 | [Configurable Motion Sensor Range](https://steamcommunity.com/sharedfiles/filedetails/?id=1842284802) | colony | the player exits (-6) while loading; Assert failed: Cannot send a frame to the sim with zero active regions; Assert failed: NewGameFrame cannot be called with zero activeRegions |
| 911 | [Pope'r Power Plant](https://steamcommunity.com/sharedfiles/filedetails/?id=2001154549) | main menu | the player exits (1) while loading; System.NullReferenceException: Object reference not set to an instance of an object @ CustomGameSettings.OnPrefabInit; System.MissingMethodException: Method not found: void .Constraint..ctor(System.Func`N<KPrefabID, bool>,System.Func`N<Room, bool>,int,string,string,System.Collections.Generic.List`N<RoomConstraints/Constraint>) |
| 931 | [Partitioned Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=2705421437) | colony | the crash screen opens while the save loads; PeterHan.PLib.Detours.DetourException: Unable to find sideScreenConfigContentBody on type DetailsScre |
| 937 | [(OUTDATED) Geyser Cracking](https://steamcommunity.com/sharedfiles/filedetails/?id=1730968929) | colony | the crash screen opens while the save loads; Chlorine Gas Vent NAN‑N ~~~!System.NullReferenceException: Object reference not set to an instance of an object @ HexiGeyserCr |
| 955 | [Sleep Not Included](https://steamcommunity.com/sharedfiles/filedetails/?id=2044840370) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation. ---> System.TypeInitializationException: The type initializer for "SleepNotIncluded.SleepNotIncluded" threw an exception. ---> System.MissingMethodException: Method not found: void Klei.CustomSettings.SettingLevel..ctor(string,string,string,int,object); null Exception while loading mod Zonkeeh.SleepNotIncluded at RUN/home/config/unityNd/Klei/Oxyg |
| 990 | [(MG)SuperMiner(超级矿机)](https://steamcommunity.com/sharedfiles/filedetails/?id=2558070245) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; HarmonyLib.HarmonyException: Patching exception in method null ---> System.ArgumentException: Undefined target method for patch method static System.Void SuperMiner.AutoMiner_DigBlockingCB::Postfix(System.IntN cell, System.Boolean& __result) @ HarmonyLib.PatchClassProcessor.PatchWithAttributes; [ReversibleHeaterClones] Failed patch HeaterSideScreen_PrepareTarget_CloneCleanupPatch: HarmonyLib.HarmonyException: Patching exception in method null ---> |
| 1017 | [Map Overlay (Prehistoric DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=3555374847) | colony | the crash screen opens while the save loads; NullReferenceException: Object reference not set to an instance of an object @ OverlayScreen.LateUpdate; NullReferenceExceptio |
| 1025 | [Electrolyzer V2 NO LONGER MAINTAINED](https://steamcommunity.com/sharedfiles/filedetails/?id=2721559364) | colony | the player exits (1) while loading; System.MissingMethodException: Method not found: void .ConsumedElement..ctor(Tag,single) @ BuildingConfigManager.RegisterBuilding; null Exception in RegisterBuilding for type ElectrolyzerVN.ElectrolyzerVNConfig from ElectrolyzerVN @ BuildingConfigManager.RegisterBuilding |
| 1039 | [Larger Gas Reservoirs](https://steamcommunity.com/sharedfiles/filedetails/?id=2839892979) | colony | the player exits (1) while loading; System.UnauthorizedAccessException: Access to the path "/home/nigit\AppData\Local\BDR\ONI\LargerGasStorage" is denied. ---> System.IO.IOException: Permission denied @ LargerGasStorage.LargerGasStoragePatches+GasReservoirConfig_ConfigureBuildingTemplate_Patch.Prefix; null Exception in RegisterBuilding for type GasReservoirConfig from Assembly-CSharp @ LargerGasStorage.LargerGasStoragePatches+GasReservoirConfig_ConfigureBuildingTemplate_Patch.Prefix |
| 1041 | [传送端口修复(TransferPost Fix)](https://steamcommunity.com/sharedfiles/filedetails/?id=3371129389) | colony | the crash screen opens while the save loads; System.MethodAccessException: Method `MonoMod.Utils.Extensions.AddRange[HashedString, System.Func`N<bool>](System.Collections.Generic.Di |
| 1089 | [Density](https://steamcommunity.com/sharedfiles/filedetails/?id=2302559484) | main menu | the player exits (1) while loading; System.Collections.Generic.KeyNotFoundException: The given key "Database.Personalities+PersonalityLoader" was not present in the dictionary @ AsyncLoadManager`N.GetLoader; System.DllNotFoundException: mxN.rN assembly:<unknown assembly> type:<unknown type> member:(null) |
| 1091 | [Multi threaded simulation](https://steamcommunity.com/sharedfiles/filedetails/?id=3563131181) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; System.Collections.Generic.KeyNotFoundException: The given key "Database.Personalities+PersonalityLoader" was not present in the dictionary @ AsyncLoadManager`N.GetLoader; System.DllNotFoundException: mxN.rN assembly:<unknown assembly> type:<unknown type> member:(null) |
| 1122 | [(reupload) Wooden Set - Structures by Ronivan](https://steamcommunity.com/sharedfiles/filedetails/?id=2880319200) | colony | the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: WoodenDoor @ PlanScreen.BuildB |
| 1143 | [FasterResearchRevived](https://steamcommunity.com/sharedfiles/filedetails/?id=2814417161) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; HarmonyLib.HarmonyException: Patching exception in method Klei.AI.AttributeConverter Database.AttributeConverters::Create(System.String id, System.String name, System.String description, Klei.AI.Attribute attribute, System.Single multiplier, System.Single base_value, IAttributeFormatter formatter, System.String[] requiredDlcIds, System.String[] forbiddenDlcIds) ---> System.Exception: Parameter "available_dlcs" not found in method Klei.AI.Attribute |
| 1154 | [植物生长时间](https://steamcommunity.com/sharedfiles/filedetails/?id=3002268593) | main menu | fails alone in the original too: its DLL references PLib but ships without it (a FileNotFoundException for PLib in its OnLoad), so it loads only beside a mod of its batch that ships PLib |
| 1166 | [Bladder Not Included](https://steamcommunity.com/sharedfiles/filedetails/?id=2044107742) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation. ---> System.TypeInitializationException: The type initializer for "BladderNotIncluded.BladderNotIncludedPatches" threw an exception. ---> System.MissingMethodException: Method not found: void Klei.CustomSettings.SettingLevel..ctor(string,string,string,int,object); null Exception while loading mod Zonkeeh.BladderNotIncluded at RUN/home/con |
| 1174 | [Proper Cyrillic Fonts](https://steamcommunity.com/sharedfiles/filedetails/?id=3519596146) | main menu | the player exits (1) while loading; NullReferenceException: Object reference not set to an instance of an object @ TMPro.TMP_Settings.get_fallbackFontAssets |
| 1235 | [Vietnamese food](https://steamcommunity.com/sharedfiles/filedetails/?id=2616743480) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; System.NullReferenceException: Object reference not set to an instance of an object @ CustomGameSettings.OnPrefabInit; System.NullReferenceException: Object reference not set to an instance of an object @ OniPowerSensorMod.Mod.Prefix |
| 1261 | [Control Mining控制自动挖矿机](https://steamcommunity.com/sharedfiles/filedetails/?id=2849947497) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; ---> (Inner Exception #N) System.Threading.ThreadAbortException: Thread was being aborted @ ONITwitch.Voting.TwitchConnection.Writer; HarmonyLib.HarmonyException: Patching exception in method null ---> System.ArgumentException: Undefined target method for patch method static System.Void Control_Mining.Patch_d::Postfix(System.IntN cell, System.Boolean& __result) @ HarmonyLib.PatchClassProcessor.PatchWithAttributes |
| 1272 | [Robo-Miner digs 150 Hardness tiles](https://steamcommunity.com/sharedfiles/filedetails/?id=3047817227) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; HarmonyLib.HarmonyException: Patching exception in method null ---> System.ArgumentException: Undefined target method for patch method static System.Void ONIPatchAutoMinerHardness.Patch_AutoMiner_DigBlockingCB::Prefix(System.IntN cell, System.Boolean& __state) @ HarmonyLib.PatchClassProcessor.PatchWithAttributes; null Exception while loading mod N.Local at RUN/home/config/unityNd/Klei/Oxygen Not Included/mods/Local/N @ HarmonyLib.PatchClassProcess |
| 1274 | [Duplicant Lifecycles](https://steamcommunity.com/sharedfiles/filedetails/?id=1876444268) | colony | the crash screen opens while the save loads; N Exception in: (BionicMinion).DuplicantLifecycles.Traits.AgingTrait+States.root.noage @ DuplicantLifecycles.Traits.AgingTrait |
| 1281 | [Drink Buildings Tweaked](https://steamcommunity.com/sharedfiles/filedetails/?id=2900275864) | colony | the crash screen opens while the save loads; System.NullReferenceException: Object reference not set to an instance of an object @ WaterCooler.OnSp |
| 1295 | [Better OilWellCap【更好的油井】](https://steamcommunity.com/sharedfiles/filedetails/?id=2282851240) | colony | the player exits (1) while loading; System.MissingMethodException: Method not found: void .ConsumedElement..ctor(Tag,single); null Exception in RegisterBuilding for type OilWellCapConfig from Assembly-CSharp |
| 1311 | [Neutronium Cave](https://steamcommunity.com/sharedfiles/filedetails/?id=2275705494) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; System.NullReferenceException: Object reference not set to an instance of an object @ RonivansLegacy_ChemicalProcessing.Content.ModDb.BuildingDatabase.RegisterOilWellCapCustomPiping; HarmonyLib.HarmonyException: Patching exception in method System.Void ProcGenGame.WorldGen::DrawWorldBorder(ProcGenGame.WorldgenSimData& simData, Chunk world, SeededRandom rnd, System.Collections.Generic.HashSet`N& borderCells, System.Col |
| 1318 | [(Deprecated) Pacu Pathfinding Bugfix](https://steamcommunity.com/sharedfiles/filedetails/?id=2223329900) | colony | the crash screen opens while the save loads; Out of nav links on grid "FlyerNavGridNxN" at cell N. Needed N links but maxLinksPerCell is N. Increase maxLinksPerCell to at |
| 1319 | [Travel Tube Deconstruct Tool](https://steamcommunity.com/sharedfiles/filedetails/?id=3403693391) | colony | the player exits (-11) while loading |
| 1328 | [Entombed Critter Warning](https://steamcommunity.com/sharedfiles/filedetails/?id=3158699666) | colony | the crash screen opens while the save loads; System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation. ---> System.Typ |
| 1330 | [优化矿机Optimizing Mining -145](https://steamcommunity.com/sharedfiles/filedetails/?id=3050890676) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; System.MissingMethodException: Method not found: EdiblesManager/FoodInfo .FoodInfo.AddEffects(System.Collections.Generic.List`N<string>,string[]) @ EntityConfigManager.RegisterEntity; HarmonyLib.HarmonyException: Patching exception in method null ---> System.ArgumentException: Undefined target method for patch method static System.Void _N_优化矿机.mmNggN::Postfix(System.IntN cell, System.Boolean& __result) @ HarmonyLib.Pa |
| 1334 | [饺子 Dumpling -K16](https://steamcommunity.com/sharedfiles/filedetails/?id=3077533003) | colony | the player exits (1) while loading; System.NullReferenceException: Object reference not set to an instance of an object @ DiscoveredResources.Discover; System.MissingMethodException: Method not found: EdiblesManager/FoodInfo .FoodInfo.AddEffects(System.Collections.Generic.List`N<string>,string[]) @ EntityConfigManager.RegisterEntity |
| 1337 | [液体球 Plus (Liquid Sphere Plus)](https://steamcommunity.com/sharedfiles/filedetails/?id=3804257326) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1338 | [Advanced Coolers (高级冷却器修复版)](https://steamcommunity.com/sharedfiles/filedetails/?id=3762029573) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1339 | [(DLC) Radiation Rebalanced](https://steamcommunity.com/sharedfiles/filedetails/?id=2844088155) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1340 | [4kw electric wire](https://steamcommunity.com/sharedfiles/filedetails/?id=3384096443) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1341 | [Not So Fast Running Animations](https://steamcommunity.com/sharedfiles/filedetails/?id=3527869170) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1343 | [液体球 (Liquid Sphere)](https://steamcommunity.com/sharedfiles/filedetails/?id=3754754434) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1344 | [Unlimired Liquid Tepidizer 热得快](https://steamcommunity.com/sharedfiles/filedetails/?id=3173524865) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1345 | [Empty The Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=2021224960) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1346 | [AdBlock](https://steamcommunity.com/sharedfiles/filedetails/?id=3653933323) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1347 | [Dupes use Tidal Springs for recreation](https://steamcommunity.com/sharedfiles/filedetails/?id=3742805804) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1348 | [Bionic Oil Threshold/ 仿生人9天一补油](https://steamcommunity.com/sharedfiles/filedetails/?id=3520927380) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1349 | [温室藻类种植箱 AlgaeTerrarium](https://steamcommunity.com/sharedfiles/filedetails/?id=3644196006) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1350 | [(Vanilla + DLC) Symbiotic Germs](https://steamcommunity.com/sharedfiles/filedetails/?id=2390576196) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1351 | [Radbolt Automation](https://steamcommunity.com/sharedfiles/filedetails/?id=2533799658) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1352 | [Improved Rocket Controls](https://steamcommunity.com/sharedfiles/filedetails/?id=2873511998) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1353 | [超级挖机/Super Auto Miner](https://steamcommunity.com/sharedfiles/filedetails/?id=3369647711) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; HarmonyLib.HarmonyException: Patching exception in method null ---> System.ArgumentException: Undefined target method for patch method static System.Void 挖机::delete_hardnessN(System.IntN cell, System.Boolean& __result) @ HarmonyLib.PatchClassProcessor.PatchWithAttributes; null Exception while loading mod N.Local at RUN/home/config/unityNd/Klei/Oxygen Not Included/mods/Local/N @ HarmonyLib.PatchClassProcessor.PatchWithAttributes |
| 1354 | [谷神星单星球（Ceres Single Asteroid Cluster）](https://steamcommunity.com/sharedfiles/filedetails/?id=3298924449) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1355 | [Super air conditioners](https://steamcommunity.com/sharedfiles/filedetails/?id=2910303439) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1356 | [Container Tooltips](https://steamcommunity.com/sharedfiles/filedetails/?id=3576799899) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1357 | [SelfSealingAirlocksRevived(fixed again)](https://steamcommunity.com/sharedfiles/filedetails/?id=2794594117) | colony | the crash screen opens while the save loads; DoorComplete Exception in: (Door).Door+Controller.root.closed.SetWorldStateClosed; System.MissingMethodException: Method not f |
| 1358 | [Skill and Attribute - Analyser Door](https://steamcommunity.com/sharedfiles/filedetails/?id=2841162578) | colony | the crash screen opens while the save loads; PeterHan.PLib.Detours.DetourException: Unable to find sideScreenConfigContentBody on type DetailsScre |
| 1360 | [Configurable Timers](https://steamcommunity.com/sharedfiles/filedetails/?id=2904048344) | colony | fails in the original only with its batch: the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: LiquidBuildOrb @ PlanScreen.BuildButtonList; System.ArgumentException: A |
| 1379 | [Stacked Pipes & Rails](https://steamcommunity.com/sharedfiles/filedetails/?id=3757139552) | colony | the crash screen opens while the save loads; ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection @ ConduitFl |
| 1407 | [Print M0NTY!](https://steamcommunity.com/sharedfiles/filedetails/?id=3387669033) | main menu | the original disables it ("Failed to load mod ...disabling"): |
| 1418 | [Map Overlay 地图附加层 中文版](https://steamcommunity.com/sharedfiles/filedetails/?id=3576363415) | colony | the crash screen opens while the save loads; NullReferenceException: Object reference not set to an instance of an object @ OverlayScreen.LateUpdate; NullReferenceExceptio |
| 1420 | [Robot Miners Not Blocked/自动采矿机不受阻挡](https://steamcommunity.com/sharedfiles/filedetails/?id=2968172437) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; HarmonyLib.HarmonyException: Patching exception in method null ---> System.ArgumentException: Undefined target method for patch method static System.Boolean RobotMinerNotBlocked.Patches::DigBlockingCB(System.IntN cell, System.Boolean& __result) @ HarmonyLib.PatchClassProcessor.PatchWithAttributes; null Exception while loading mod RobotMinerNotBlocked at RUN/home/config/unityNd/Klei/Oxygen Not Included/mods/Local/N @ HarmonyLib.PatchClassPr |
| 1425 | [缺氧个人自用修改01](https://steamcommunity.com/sharedfiles/filedetails/?id=2985335138) | colony | the crash screen opens while the save loads; ArgumentException: Accessing mismatched handle version. Expected version=N but got version=N @ HandleVector`N.UnpackHandle; Nu |
| 1456 | [中子能源（已停更）](https://steamcommunity.com/sharedfiles/filedetails/?id=2146410674) | colony | the player exits (1) while loading; System.FieldAccessException: Field `ManualDeliveryKG:requestedItemTag" is inaccessible from method `NeutroniumEnergy.NeutroniumGeneratorConfig:ConfigureBuildingTemplate (UnityEngine.GameObject,Tag)" @ BuildingConfigManager.RegisterBuilding; System.MissingMethodException: Method not found: void .ConsumedElement..ctor(Tag,single) @ BuildingConfigManager.RegisterBuilding |
| 1458 | [Thermal Regulator Plate / Wall-Mounted Temperature Enforcement Unit](https://steamcommunity.com/sharedfiles/filedetails/?id=3699800250) | main menu | step start: the player exited (1) before OniMcp started; System.NullReferenceException: Object reference not set to an instance of an object @ Database.Personalities..ctor; System.Reflection.ReflectionTypeLoadException: Exception of type "System.Reflection.ReflectionTypeLoadException" was thrown |
| 1461 | [Map Overlay Updated(ish) (Deprecated - New Link in Description)](https://steamcommunity.com/sharedfiles/filedetails/?id=3422648901) | colony | the crash screen opens while the save loads; NullReferenceException: Object reference not set to an instance of an object @ OverlayScreen.LateUpdate; NullReferenceExceptio |
| 1468 | [Spring Festival Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3152316922) | main menu | step start: the player exited (1) before OniMcp started; NullReferenceException: Object reference not set to an instance of an object @ TMPro.TMP_Settings.get_fallbackFontAssets |
| 1478 | [OneStar单一星球](https://steamcommunity.com/sharedfiles/filedetails/?id=2873467357) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; HarmonyLib.HarmonyException: Patching exception in method System.Boolean ProcGenGame.WorldGen::RenderOffline(System.Boolean doSettle, System.UIntN simSeed, System.IO.BinaryWriter writer, ProcGenGame.WorldgenSimData& simData, System.IntN baseId, System.Collections.Generic.List`N& placedStoryTraits, System.Boolean isStartingWorld) ---> System.Exception: Parameter "cells" not found in method System.Boolean ProcGenGame.WorldGen::RenderOffline( |
| 1483 | [OilWellCapAnyWater](https://steamcommunity.com/sharedfiles/filedetails/?id=2836439995) | colony | the player exits (1) while loading; System.MissingMethodException: Method not found: void .ConsumedElement..ctor(Tag,single); null Exception in RegisterBuilding for type OilWellCapConfig from Assembly-CSharp |
| 1498 | [Pinned Resource List Extended (Discontinued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2633799000) | colony | the player exits (-11) while loading |
| 1513 | [Geyser Sensor Set](https://steamcommunity.com/sharedfiles/filedetails/?id=2122585608) | colony | the player exits (1) while loading; ArgumentException: The Object you want to instantiate is null @ UnityEngine.Object.Instantiate; ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection @ BackwallManager+BackwallIndexer.get_Element |
| 1520 | [简单密封机械气闸 Sealed Insulated Mech-Door](https://steamcommunity.com/sharedfiles/filedetails/?id=3620851546) | colony | the crash screen opens while the save loads; DoorComplete Exception in: (Door).Door+Controller.root.closed.SetWorldStateClosed; System.MissingMethodException: Method not found: void .SimMessages.SetCellProperties(int,byte) |
| 1536 | [月饼（DLC可用）](https://steamcommunity.com/sharedfiles/filedetails/?id=3043109730) | colony | the player exits (1) while loading; ArgumentException: The Object you want to instantiate is null @ UnityEngine.Object.Instantiate; ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection @ BackwallManager+BackwallIndexer.get_Element |
| 1539 | [Minibase 2 + Tunnels Fix（Minibase Mod修复）](https://steamcommunity.com/sharedfiles/filedetails/?id=3329613555) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; System.IO.FileNotFoundException: Could not find file "RUN/home/config/unityNd/Klei/Oxygen Not Included/mods/Local/N/asteroid_minibase.png"; HarmonyLib.HarmonyException: Patching exception in method System.Boolean ProcGenGame.WorldGen::RenderOffline(System.Boolean doSettle, System.UIntN simSeed, System.IO.BinaryWriter writer, ProcGenGame.WorldgenSimData& simData, System.IntN baseId, System.Collections.Generic.List`N& p |
| 1567 | [Alchemist](https://steamcommunity.com/sharedfiles/filedetails/?id=2586076206) | colony | the crash screen opens while the save loads; System.NullReferenceException: Object reference not set to an instance of an object @ SearchUtil.CanonicalizePh |
| 1591 | [Computer Parts (Automation)](https://steamcommunity.com/sharedfiles/filedetails/?id=2779162200) | colony | the crash screen opens while the save loads; PeterHan.PLib.Detours.DetourException: Unable to find sideScreenContentBody on type DetailsScreen @ P |
| 1632 | [Crack Reacher Classic](https://steamcommunity.com/sharedfiles/filedetails/?id=2545317979) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; System.TypeLoadException: Could not resolve type with token N from typeref (expected class "Harmony.HarmonyPatch" in assembly "NHarmony, Version=N.N, Culture=neutral, PublicKeyToken=null"); null Exception while loading mod N.Local at RUN/home/config/unityNd/Klei/Oxygen Not Included/mods/Local/N |
| 1639 | [(MG)Super Solid Transfer Arm(超级机械臂)](https://steamcommunity.com/sharedfiles/filedetails/?id=2557253018) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; System.MissingMethodException: Method not found: void Database.BuildingFacades.Add(string,LocString,LocString,Database.PermitRarity,string,string,System.Collections.Generic.Dictionary`N<string, string>); HarmonyLib.HarmonyException: Patching exception in method System.Boolean SolidTransferArm::AsyncUpdate() ---> System.Exception: Parameter "cell" not found in method System.Boolean SolidTransferArm::AsyncUpdate() @ HarmonyLib. |
| 1656 | [核废水处理桶](https://steamcommunity.com/sharedfiles/filedetails/?id=3025055489) | colony | the player exits (1) while loading; ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection @ BackwallManager+BackwallIndexer.get_Element; Assert failed: Cannot send a frame to the sim with zero active regions @ SubworldZoneRenderData.GenerateTexture |
| 1670 | [3 'UPGR4D3D' Dupes!](https://steamcommunity.com/sharedfiles/filedetails/?id=3387528357) | main menu | the original disables it ("Failed to load mod ...disabling"): |
| 1688 | [Dynamic Buildings 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2639316397) | colony | the crash screen opens while the save loads; System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation. ---> System. |
| 1693 | [最大等级上升 MaxLevelUp](https://steamcommunity.com/sharedfiles/filedetails/?id=3420064522) | main menu | the player exits (1) while loading; System.IO.FileNotFoundException: Could not load file or assembly "PLib, Version=N.N, Culture=neutral, PublicKeyToken=null" or one of its dependencies; null Exception while loading mod GetAllTheGoodTraits at RUN/home/config/unityNd/Klei/Oxygen Not Included/mods/Local/N |
| 1695 | [MultifunctionalElectrolyzer](https://steamcommunity.com/sharedfiles/filedetails/?id=2837711870) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; HarmonyLib.HarmonyException: Patching exception in method null ---> System.ArgumentException: Undefined target method for patch method static System.Void AutomaticOilRefinery.ElementConverterHasEnoughMass::Postfix(ElementConverter __instance, System.Boolean& __result) @ HarmonyLib.PatchClassProcessor.PatchWithAttributes; System.MissingMethodException: Method not found: void .ConsumedElement..ctor(Tag,single) |
| 1724 | [Small Tanks](https://steamcommunity.com/sharedfiles/filedetails/?id=3018359549) | colony | the player exits (1) while loading; System.IndexOutOfRangeException: Index was outside the bounds of the array @ BuildingLoader.GetEffectiveMass; null Exception in RegisterBuilding for type SmallTanks.GasCargoBaySmallConfig from SmallTanks @ BuildingLoader.GetEffectiveMass |
| 1726 | [自然种植盒f(临时)](https://steamcommunity.com/sharedfiles/filedetails/?id=3734915386) | colony | the crash screen opens while the save loads; System.ArgumentException: An item with the same key has already been added. Key: PiapiPlantHelper @ PlanScreen.BuildButtonList; System.ArgumentException: |
| 1728 | [baator  very hardcore](https://steamcommunity.com/sharedfiles/filedetails/?id=2931997161) | colony | step start: the fixture did not load within 15 minutes; ---> (Inner Exception #N) System.IO.FileNotFoundException: YamlIO tried loading a file that doesn"t exist: /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/out/sweep/.original-N/OxygenNotIncluded_Data/StreamingAssets/templates/poi/Baator_Geyser_Natoriumusen.yaml @ Klei.YamlIO.LoadFile[T]; FileNotFoundException: YamlIO tried loading a file that doesn"t exist: /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNf |
| 1747 | [Terarara](https://steamcommunity.com/sharedfiles/filedetails/?id=3109494913) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; FileNotFoundException: YamlIO tried loading a file that doesn"t exist: /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/out/sweep/.original-N/OxygenNotIncluded_Data/StreamingAssets/worldgen/subworlds/Terr/Start_Default.yaml @ Klei.YamlIO.LoadFile[T]; First anim file needs to be non-null. TorchComplete (UnityEngine.GameObject) @ KAnimControllerBase.set_AnimFiles |
| 1748 | [My Sweeper Range (DLC compatible) (update on hold)](https://steamcommunity.com/sharedfiles/filedetails/?id=2399652032) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; FileNotFoundException: YamlIO tried loading a file that doesn"t exist: /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/out/sweep/.original-N/OxygenNotIncluded_Data/StreamingAssets/worldgen/subworlds/Terr/Start_Default.yaml @ Klei.YamlIO.LoadFile[T]; First anim file needs to be non-null. TorchComplete (UnityEngine.GameObject) @ KAnimControllerBase.set_AnimFiles |
| 1763 | [Vision Not Included](https://steamcommunity.com/sharedfiles/filedetails/?id=3683507975) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; FileNotFoundException: YamlIO tried loading a file that doesn"t exist: /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/out/sweep/.original-N/OxygenNotIncluded_Data/StreamingAssets/worldgen/subworlds/Terr/Start_Default.yaml @ Klei.YamlIO.LoadFile[T]; First anim file needs to be non-null. TorchComplete (UnityEngine.GameObject) @ KAnimControllerBase.set_AnimFiles |
| 1777 | [C_废液桶-Waste liquid tank](https://steamcommunity.com/sharedfiles/filedetails/?id=3232567876) | colony | the player exits (1) while loading; ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection @ BackwallManager+BackwallIndexer.get_Element; Assert failed: Cannot send a frame to the sim with zero active regions @ SubworldZoneRenderData.GenerateTexture |
| 1801 | [PressurizedPipes-Fixed](https://steamcommunity.com/sharedfiles/filedetails/?id=2804306647) | colony | the player exits (1) while loading; ArgumentException: The Object you want to instantiate is null @ UnityEngine.Object.Instantiate; ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection @ BackwallManager+BackwallIndexer.get_Element |
| 1808 | [Canned Lice](https://steamcommunity.com/sharedfiles/filedetails/?id=2672305657) | colony | the player exits (1) while loading; System.NullReferenceException: Object reference not set to an instance of an object @ DiscoveredResources.Discover; System.MissingMethodException: Method not found: EdiblesManager/FoodInfo .FoodInfo.AddEffects(System.Collections.Generic.List`N<string>,string[]) @ EntityConfigManager.RegisterEntity |
| 1844 | [精准控制存储(Precise Control Storage)](https://steamcommunity.com/sharedfiles/filedetails/?id=2980240880) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) before OniMcp started; Fatal parse error in /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/out/sweep/.original-N/OxygenNotIncluded_Data/StreamingAssets/dlc/expansionN/worldgen/subworlds/sandstone/SandstoneStart.yaml; System.MissingMethodException: Method not found: UnityEngine.GameObject .BionicUpgradeComponentConfig.CreateNewUpgradeComponent(string,string,string,single,System.Func`N<StateMachine/Instance, StateMachine/I |
| 1847 | [Customize Building Categories!](https://steamcommunity.com/sharedfiles/filedetails/?id=2126470999) | colony | the crash screen opens while the save loads; KeyNotFoundException: The given key "N" was not present in the dictionary @ PlanScreen.Refresh; NullReferenceException: Object |
| 1857 | [全技能增幅块(AllAttributeBooster)](https://steamcommunity.com/sharedfiles/filedetails/?id=3577617954) | colony | the player exits (1) while loading; ArgumentException: The Object you want to instantiate is null @ UnityEngine.Object.Instantiate; ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection @ BackwallManager+BackwallIndexer.get_Element |
| 1868 | [Critical Notification Pauser i18n 遇险暂停](https://steamcommunity.com/sharedfiles/filedetails/?id=3085261382) | colony | the crash screen opens while the save loads; NullReferenceException: Object reference not set to an instance of an object @ NotificationsPauseINnMod.NotificationsPause+Notification_IsReady_Patch.P |
| 1874 | [Dangerous World](https://steamcommunity.com/sharedfiles/filedetails/?id=1880389930) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; System.NullReferenceException: Object reference not set to an instance of an object @ Database.Personalities..ctor; Fatal parse error in /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/out/sweep/.original-N/OxygenNotIncluded_Data/StreamingAssets/worldgen/subworlds/forest/ForestStartHot.yaml |
| 1875 | [(Outdated) Temperature Tresholds](https://steamcommunity.com/sharedfiles/filedetails/?id=2757266274) | main menu | the original disables it ("Failed to load mod ...disabling"): step start: the player exited (1) while loading; System.NullReferenceException: Object reference not set to an instance of an object @ Database.Personalities..ctor; Fatal parse error in /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/out/sweep/.original-N/OxygenNotIncluded_Data/StreamingAssets/worldgen/subworlds/forest/ForestStartHot.yaml |
| 1905 | [T_安的魔法厨房 An's Magic Kitchen（失效）](https://steamcommunity.com/sharedfiles/filedetails/?id=3222491570) | colony | the player exits (1) while loading; System.NullReferenceException: Object reference not set to an instance of an object @ Util.KInstantiate; Assert failed: Clustercraft Exterior Door has no targetDoor @ ClusterUtil.GetMyWorld |
| 1961 | [The Populist Challenge](https://steamcommunity.com/sharedfiles/filedetails/?id=2710574991) | colony | the crash screen opens while the save loads; NullReferenceException: Object reference not set to an instance of an object @ PopulistChallenge.AutoOpenImmigrantScreen.Postf |
| 1964 | [Biome Remix Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3815497375) | colony | the player exits (1) while loading; FileNotFoundException: YamlIO tried loading a file that doesn"t exist: /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/out/sweep/.original-N/OxygenNotIncluded_Data/StreamingAssets/worldgen/subworlds/Jungle/CGM_JungleMix.yaml @ Klei.YamlIO.LoadFile[T]; Rethrow as AggregateException: One or more errors occurred. (YamlIO tried loading a file that doesn"t exist: /home/nigit/projects/oni-brutal/.claude/worktrees/agent-aNdNfNeNfN/out/ |
| 1969 | [种子研究站](https://steamcommunity.com/sharedfiles/filedetails/?id=2894589931) | colony | the player exits (1) while loading; System.NullReferenceException: Object reference not set to an instance of an object @ DiscoveredResources.Discover; System.MissingMethodException: Method not found: UnityEngine.GameObject .EntityTemplates.ExtendEntityToBasicPlant(UnityEngine.GameObject,single,single,single,single,SimHashes[],bool,single,single,string,bool,bool,bool,bool,single,single,single,string,string) @ EntityConfigManager.Regist |

## Batches

The mods that load in both players, 22 to a batch by rank.

### Batch 01: ranks 751–776

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 751 | [Bigger Brushes](https://steamcommunity.com/sharedfiles/filedetails/?id=1882162095) | P |  |
| 752 | [EasySteamTurbine](https://steamcommunity.com/sharedfiles/filedetails/?id=2836461351) | P |  |
| 753 | [Mineralizer Reborn](https://steamcommunity.com/sharedfiles/filedetails/?id=2936854563) | B,O | PLib options |
| 754 | [OldFoodPreservation](https://steamcommunity.com/sharedfiles/filedetails/?id=2836897091) | P |  |
| 755 | [I 智能液体加热器 Intelligent Liquid Heater 2.1](https://steamcommunity.com/sharedfiles/filedetails/?id=3110572144) | P |  |
| 757 | [Advanced Liquid Heater](https://steamcommunity.com/sharedfiles/filedetails/?id=2119786479) | B |  |
| 759 | [Configurable Automatic Machine](https://steamcommunity.com/sharedfiles/filedetails/?id=2877460966) | P,O | PLib options |
| 760 | [Dig Through Tiles](https://steamcommunity.com/sharedfiles/filedetails/?id=3744532814) | T |  |
| 761 | [大智能电池(可配置)](https://steamcommunity.com/sharedfiles/filedetails/?id=3341977181) | B,O | PLib options, base game only |
| 762 | [Artifacts in Care Packages](https://steamcommunity.com/sharedfiles/filedetails/?id=2455735197) | S,O | PLib options, UI |
| 763 | [AutomaticForge](https://steamcommunity.com/sharedfiles/filedetails/?id=2836891839) | P |  |
| 764 | [FastTrack-Simplified Chinese Translation](https://steamcommunity.com/sharedfiles/filedetails/?id=3726410223) | P |  |
| 765 | [Geyser Cracking (Song of the Moo Update)](https://steamcommunity.com/sharedfiles/filedetails/?id=2874440740) | P,O | PLib options, user menu |
| 766 | [Fried Liceloaf](https://steamcommunity.com/sharedfiles/filedetails/?id=2019356525) | P |  |
| 767 | [(DEPRECATED) Bionics Access Control](https://steamcommunity.com/sharedfiles/filedetails/?id=3438001141) | P |  |
| 770 | [Tiny Battery](https://steamcommunity.com/sharedfiles/filedetails/?id=1843834993) | B |  |
| 771 | [Auto Remove Baby](https://steamcommunity.com/sharedfiles/filedetails/?id=3145638633) | P | user menu |
| 772 | [ForeverFresh（永久保鲜）](https://steamcommunity.com/sharedfiles/filedetails/?id=3497166042) | P |  |
| 773 | [P 永不淹没 Never Floodable 1.1](https://steamcommunity.com/sharedfiles/filedetails/?id=2859413444) | P |  |
| 774 | [变异农场实验室 (MutantFarmLab)](https://steamcommunity.com/sharedfiles/filedetails/?id=3648432502) | B,S,O | PLib options, UI |
| 775 | [Superpipe超级管道 -K34](https://steamcommunity.com/sharedfiles/filedetails/?id=2868798283) | P,O | PLib options |
| 776 | [Always Show Capacity](https://steamcommunity.com/sharedfiles/filedetails/?id=2026932869) | S | UI |

### Batch 02: ranks 777–806

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 777 | [Oxygen Mask Eye Protection & No CO2 Exhale](https://steamcommunity.com/sharedfiles/filedetails/?id=2976666381) | P,O | PLib options |
| 778 | [NeodymiumStyle Hairstyle MOD "AKANE"](https://steamcommunity.com/sharedfiles/filedetails/?id=2998905657) | X | needs 3172734005, noCode |
| 780 | [Mole Miner (Hot Shots)](https://steamcommunity.com/sharedfiles/filedetails/?id=1950944765) | P |  |
| 782 | [Wireless Power - Fixed](https://steamcommunity.com/sharedfiles/filedetails/?id=3671679281) | B,S | UI |
| 783 | [(FIX) Destructible Features (Geysers & Volcanoes)](https://steamcommunity.com/sharedfiles/filedetails/?id=2999435124) | P |  |
| 784 | [Reorderable Todo List](https://steamcommunity.com/sharedfiles/filedetails/?id=3718696016) | S | UI |
| 785 | [Alternative Voiceroid Hairstyle For Dupery](https://steamcommunity.com/sharedfiles/filedetails/?id=2870002358) | X | needs 2679326313, noCode |
| 786 | [Fixed Ore Scrubber](https://steamcommunity.com/sharedfiles/filedetails/?id=2172549146) | P,O | PLib options |
| 787 | [Help! My GPU screams at the main menu! (FPS Limiter)](https://steamcommunity.com/sharedfiles/filedetails/?id=3405082060) | P,O | PLib options |
| 789 | [Mod List Previews](https://steamcommunity.com/sharedfiles/filedetails/?id=3794018361) | V,M | UI, needs 2854869130, 2018291283 |
| 790 | [缺氧食物不会腐烂](https://steamcommunity.com/sharedfiles/filedetails/?id=2984793466) | P |  |
| 791 | [温度场Temperature Field -146](https://steamcommunity.com/sharedfiles/filedetails/?id=3054711075) | B,S | UI |
| 793 | [U&F Resize Dup UI 界面优化](https://steamcommunity.com/sharedfiles/filedetails/?id=3406758275) | S,O | PLib options, UI |
| 795 | [(MG)Bigger Storage(超级存储)](https://steamcommunity.com/sharedfiles/filedetails/?id=2558057221) | P |  |
| 796 | [Cooling Element (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2646217495) | B |  |
| 797 | [Instant Dirt & Wild Plants (AKA: Buildable Dirt Tile And Wild Plants)](https://steamcommunity.com/sharedfiles/filedetails/?id=3732005635) | B,O,M | PLib options |
| 798 | [冷冻风扇Freezing fan](https://steamcommunity.com/sharedfiles/filedetails/?id=3221051163) | P |  |
| 800 | [Notepad](https://steamcommunity.com/sharedfiles/filedetails/?id=1996618130) | B,S,O | PLib options, UI |
| 801 | [(Beta)传送服 Teleport Suit](https://steamcommunity.com/sharedfiles/filedetails/?id=2987670505) | B,T,S,K,O,V,M | PLib options, UI |
| 802 | [Mod Preset Manager (Light) (MPML)](https://steamcommunity.com/sharedfiles/filedetails/?id=3281716506) | S,M | bundle UI |
| 803 | [快用你万能的米虱想想办法啊/Firefighter Mealwood](https://steamcommunity.com/sharedfiles/filedetails/?id=2993850936) | P |  |
| 806 | [自动化-液体装桶器 Automation - Liquid Bucket Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3464314625) | P |  |

### Batch 03: ranks 807–834

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 807 | [Zinc Volcano](https://steamcommunity.com/sharedfiles/filedetails/?id=3743015529) | P | worldgen |
| 808 | [C 管道系统 Piping System 1.5](https://steamcommunity.com/sharedfiles/filedetails/?id=3334327664) | B,O | PLib options |
| 809 | [Font Replacement in Simplified Chinese](https://steamcommunity.com/sharedfiles/filedetails/?id=3561866116) | P | UI |
| 810 | [VignetteBegone-移除晕影](https://steamcommunity.com/sharedfiles/filedetails/?id=3460554894) | S | UI |
| 811 | [(FIX) Start with all Research](https://steamcommunity.com/sharedfiles/filedetails/?id=3001972449) | P |  |
| 812 | [Mutants don't need radiation to grow](https://steamcommunity.com/sharedfiles/filedetails/?id=2793754960) | P |  |
| 813 | [魔改吸顶灯7万64](https://steamcommunity.com/sharedfiles/filedetails/?id=2984782696) | B |  |
| 815 | [Don't Crab my Wild Ecosystem](https://steamcommunity.com/sharedfiles/filedetails/?id=3750737880) | P |  |
| 816 | [Outfits Included](https://steamcommunity.com/sharedfiles/filedetails/?id=3447551899) | P |  |
| 818 | [Atmo Suit-More Oxygen气压服更多氧气（停止更新）](https://steamcommunity.com/sharedfiles/filedetails/?id=2866118101) | P,O | PLib options |
| 819 | [指数级智能电池](https://steamcommunity.com/sharedfiles/filedetails/?id=2845739096) | B |  |
| 820 | [社交生活](https://steamcommunity.com/sharedfiles/filedetails/?id=3775109476) | S,V,M | UI, user menu |
| 821 | [Smart Coolers](https://steamcommunity.com/sharedfiles/filedetails/?id=2990501709) | S,O | PLib options, UI |
| 824 | [Gravitas Shipping Container](https://steamcommunity.com/sharedfiles/filedetails/?id=2942005501) | B,O | PLib options |
| 826 | [(MG)SwitchDoorImmediately(马上关门)](https://steamcommunity.com/sharedfiles/filedetails/?id=2557247469) | P |  |
| 827 | [Quota Storage定额存储箱](https://steamcommunity.com/sharedfiles/filedetails/?id=3709844715) | S | UI, user menu |
| 828 | [Super compost超级堆肥](https://steamcommunity.com/sharedfiles/filedetails/?id=2863558330) | P,O | PLib options |
| 829 | [Pipe Color Overlay](https://steamcommunity.com/sharedfiles/filedetails/?id=3678142367) | V |  |
| 830 | [Configurable Automation-controlled Doors](https://steamcommunity.com/sharedfiles/filedetails/?id=2820163949) | S | UI |
| 832 | [(DLC) Fragrant Flowers](https://steamcommunity.com/sharedfiles/filedetails/?id=2924528762) | B,O | PLib options, worldgen |
| 833 | [花盆种植一切](https://steamcommunity.com/sharedfiles/filedetails/?id=2881769456) | P,O | PLib options |
| 834 | [Critter Traits Reborn](https://steamcommunity.com/sharedfiles/filedetails/?id=2954005789) | S | UI |

### Batch 04: ranks 835–862

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 835 | [Conveyor Loaders Are Not Machinery](https://steamcommunity.com/sharedfiles/filedetails/?id=2584106067) | P |  |
| 836 | [Endless Science fix 无限学习](https://steamcommunity.com/sharedfiles/filedetails/?id=2876288128) | P | user menu |
| 837 | [Resource Dashboard](https://steamcommunity.com/sharedfiles/filedetails/?id=3671379574) | S,K,O | PLib options, UI |
| 838 | [(Vanilla + DLC) Free Skillpoints](https://steamcommunity.com/sharedfiles/filedetails/?id=2557246397) | P |  |
| 839 | [铅隔绝辐射Lead Isolate Radiation -K15](https://steamcommunity.com/sharedfiles/filedetails/?id=3078504174) | P |  |
| 840 | [(FIX) Deconstructable POI Props](https://steamcommunity.com/sharedfiles/filedetails/?id=2999788349) | P |  |
| 841 | [Sick Go To Med Bed](https://steamcommunity.com/sharedfiles/filedetails/?id=2962822663) | P |  |
| 842 | [Tinted Aquatuner Liquid (OBSOLETE)](https://steamcommunity.com/sharedfiles/filedetails/?id=2737502623) | P |  |
| 843 | [Gas Pump Power Tweak (Vanilla/DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=2481823855) | P,O | PLib options |
| 845 | [复制人属性修改(可配置)](https://steamcommunity.com/sharedfiles/filedetails/?id=3606719577) | T,O | PLib options |
| 848 | [Flip Anything (Vanilla, Spaced Out)](https://steamcommunity.com/sharedfiles/filedetails/?id=2470399096) | P |  |
| 850 | [Sealed Critter Feeders](https://steamcommunity.com/sharedfiles/filedetails/?id=2130729222) | P |  |
| 851 | [在泉上建造/GeysersNotOccupying](https://steamcommunity.com/sharedfiles/filedetails/?id=2816175461) | T |  |
| 852 | [(Deprecated) Per-planetoid Materials](https://steamcommunity.com/sharedfiles/filedetails/?id=2517456276) | P |  |
| 853 | [Refined Metals Are Still Metals](https://steamcommunity.com/sharedfiles/filedetails/?id=3538072658) | P |  |
| 855 | [Configurable Steam Turbine](https://steamcommunity.com/sharedfiles/filedetails/?id=2845493570) | P,O | PLib options |
| 856 | [Customize Recipe](https://steamcommunity.com/sharedfiles/filedetails/?id=2587551532) | P,O | PLib options, needs 2018291283 |
| 857 | [真·无限气液库V1.0旧版（Infinite Capacity Reservoir Retro Version）](https://steamcommunity.com/sharedfiles/filedetails/?id=3243965346) | B | UI |
| 858 | [No sublimation](https://steamcommunity.com/sharedfiles/filedetails/?id=3518862481) | P |  |
| 859 | [Water World](https://steamcommunity.com/sharedfiles/filedetails/?id=2992152587) | P | worldgen |
| 861 | [Packaged Bleach Stone](https://steamcommunity.com/sharedfiles/filedetails/?id=3262486834) | P |  |
| 862 | [Digital CPU 数字化CPU](https://steamcommunity.com/sharedfiles/filedetails/?id=3244925649) | B,S | UI |

### Batch 05: ranks 864–888

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 864 | [技能点兑换属性 / Skill Points to Attributes](https://steamcommunity.com/sharedfiles/filedetails/?id=3810697122) | S,K,O | PLib options, UI, base game only |
| 865 | [Pipe Pressure Valve Updated](https://steamcommunity.com/sharedfiles/filedetails/?id=2601665313) | B |  |
| 866 | [Preserved Food doesn't spoil](https://steamcommunity.com/sharedfiles/filedetails/?id=2791973376) | P |  |
| 867 | [Wall Attachment Pumps](https://steamcommunity.com/sharedfiles/filedetails/?id=3757698960) | B |  |
| 868 | [Buildable Dirt Tile (Missing String Fixed)](https://steamcommunity.com/sharedfiles/filedetails/?id=3599468153) | B |  |
| 870 | [催化CO2制乙醇 Catalytic CO2 to ethanol](https://steamcommunity.com/sharedfiles/filedetails/?id=2881000917) | B |  |
| 871 | [FixSteamTurbineBug](https://steamcommunity.com/sharedfiles/filedetails/?id=3739024899) | P |  |
| 872 | [Radiate Heat in Space (updated and configurable)](https://steamcommunity.com/sharedfiles/filedetails/?id=2755454101) | B,O | PLib options |
| 873 | [FreeWheezewort](https://steamcommunity.com/sharedfiles/filedetails/?id=3728428669) | P |  |
| 874 | [One cell battery一格电池](https://steamcommunity.com/sharedfiles/filedetails/?id=2870926065) | B,O | PLib options |
| 875 | [Help I accidentally broke my rocket interior ports and now the save crashes on load - Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3021827424) | P |  |
| 876 | [Fabricate it Now!](https://steamcommunity.com/sharedfiles/filedetails/?id=3794979035) | P |  |
| 877 | [C 黄金淋浴间 Gold Shower 1.7](https://steamcommunity.com/sharedfiles/filedetails/?id=2847163162) | B |  |
| 879 | [Trash Cans](https://steamcommunity.com/sharedfiles/filedetails/?id=3052222549) | B |  |
| 881 | [Meteor Migration (DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=2955350267) | P | worldgen |
| 882 | [Ethanol Geyser (rebuild)](https://steamcommunity.com/sharedfiles/filedetails/?id=2810438800) | P |  |
| 883 | [Better Checkpoints](https://steamcommunity.com/sharedfiles/filedetails/?id=3718142949) | S,O | PLib options, UI |
| 884 | [Small Generators, now fixed!](https://steamcommunity.com/sharedfiles/filedetails/?id=3008287025) | B,S | UI |
| 885 | [AlwaysRequestSuitLocker](https://steamcommunity.com/sharedfiles/filedetails/?id=2908269000) | T,O | PLib options |
| 886 | [超级堆肥堆 10x](https://steamcommunity.com/sharedfiles/filedetails/?id=3129448555) | P |  |
| 887 | [Circuit Not Included](https://steamcommunity.com/sharedfiles/filedetails/?id=3689351875) | B,S | UI |
| 888 | [U&F Adjustable Transfer Arm 可调节自动清扫器](https://steamcommunity.com/sharedfiles/filedetails/?id=3447175832) | P |  |

### Batch 06: ranks 889–913

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 889 | [香草之家 Rustic House](https://steamcommunity.com/sharedfiles/filedetails/?id=3004770489) | B | needs 2882418505 |
| 890 | [控温砖（无需电力版）(temperature adjustment tile requires no power)](https://steamcommunity.com/sharedfiles/filedetails/?id=3247617445) | B |  |
| 891 | [Change Some Chore Group](https://steamcommunity.com/sharedfiles/filedetails/?id=3016925992) | P |  |
| 892 | [喷泉修改(Geyser Set)](https://steamcommunity.com/sharedfiles/filedetails/?id=2223751324) | P |  |
| 893 | [Synchronized Building Groups](https://steamcommunity.com/sharedfiles/filedetails/?id=3427974009) | S | bundle UI |
| 894 | [Loading Pip - Customizable Loading Screen](https://steamcommunity.com/sharedfiles/filedetails/?id=3408518726) | S,O | PLib options, UI |
| 895 | [超级泵 Super Pump](https://steamcommunity.com/sharedfiles/filedetails/?id=2975152457) | P |  |
| 896 | [Fabricator Recipe Control](https://steamcommunity.com/sharedfiles/filedetails/?id=3478214979) | B,S,O | PLib options, bundle UI |
| 897 | [Duplicant Room Sensor](https://steamcommunity.com/sharedfiles/filedetails/?id=1921058858) | B |  |
| 898 | [Mutant Plant Drop Seed](https://steamcommunity.com/sharedfiles/filedetails/?id=2631256908) | P |  |
| 899 | [无负载电线](https://steamcommunity.com/sharedfiles/filedetails/?id=3104741758) | P |  |
| 900 | [Compact Research](https://steamcommunity.com/sharedfiles/filedetails/?id=3522638280) | P |  |
| 901 | [更智能的灯More intelligent lamp](https://steamcommunity.com/sharedfiles/filedetails/?id=2889776196) | P |  |
| 902 | [AutomaticHarvest-自动收获机](https://steamcommunity.com/sharedfiles/filedetails/?id=3623778703) | B | user menu |
| 904 | [5WFridge](https://steamcommunity.com/sharedfiles/filedetails/?id=3510014731) | P |  |
| 906 | [Geyser Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3746027329) | S,K,O,V,M | PLib options, UI |
| 907 | [Flood Fill Tools](https://steamcommunity.com/sharedfiles/filedetails/?id=3703748819) | T,O | PLib options |
| 908 | [人力发电机可获取所有属性经验](https://steamcommunity.com/sharedfiles/filedetails/?id=3606741605) | P,O | PLib options |
| 909 | [(New Building) Algae Grower藻类培养箱](https://steamcommunity.com/sharedfiles/filedetails/?id=3621278256) | B |  |
| 910 | [Super Duplicants!](https://steamcommunity.com/sharedfiles/filedetails/?id=3018763783) | P | user menu |
| 912 | [Resource Sensor](https://steamcommunity.com/sharedfiles/filedetails/?id=2911545239) | B,S | UI |
| 913 | [Water Sieve cleans germs](https://steamcommunity.com/sharedfiles/filedetails/?id=3520953238) | P | nativeCalls |

### Batch 07: ranks 914–941

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 914 | [Roller Snakes Reborn](https://steamcommunity.com/sharedfiles/filedetails/?id=2860569135) | P | UI, worldgen |
| 915 | [(Vanilla + DLC) Diseases Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=2627657947) | B,O,V | PLib options, user menu, mayCrashInPlay |
| 917 | [真.巨型电池](https://steamcommunity.com/sharedfiles/filedetails/?id=3016368916) | B |  |
| 918 | [Help, none of my mod options are working after the DLC update - fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3291978062) | P |  |
| 919 | [Longer Arms](https://steamcommunity.com/sharedfiles/filedetails/?id=3627507304) | P,O | PLib options |
| 920 | [Mod Origin Info Display](https://steamcommunity.com/sharedfiles/filedetails/?id=3757075889) | S | UI |
| 921 | [液体灌装器Liquid Bottler](https://steamcommunity.com/sharedfiles/filedetails/?id=2900816401) | B |  |
| 922 | [Polluted Oxygen Disguise 蓝色污染氧](https://steamcommunity.com/sharedfiles/filedetails/?id=3129436780) | V |  |
| 924 | [Bionic Overclock](https://steamcommunity.com/sharedfiles/filedetails/?id=3426316307) | P |  |
| 925 | [Sandbox: Mutant Plants](https://steamcommunity.com/sharedfiles/filedetails/?id=2493249462) | P | user menu |
| 926 | [Advanced Storage(V5)](https://steamcommunity.com/sharedfiles/filedetails/?id=3339050792) | B,S | UI |
| 929 | [复制人呼出氧气(or一切气体)](https://steamcommunity.com/sharedfiles/filedetails/?id=3343067865) | P,O | PLib options, base game only |
| 930 | [Microbe Musher With Light (Vanilla/DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=1837044017) | P |  |
| 932 | [(Deprecated) Rotatable Radbolt Chamber Output (DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=2886144715) | S | UI |
| 933 | [NeodymiumStyle Hairstyle MOD "REIMU"](https://steamcommunity.com/sharedfiles/filedetails/?id=2995869375) | X | needs 3172734005, noCode |
| 934 | [反熵热量中和器](https://steamcommunity.com/sharedfiles/filedetails/?id=3352492406) | B,O | PLib options |
| 935 | [更强的食物保鲜逃课（retain freshness module）](https://steamcommunity.com/sharedfiles/filedetails/?id=3246425770) | B |  |
| 936 | [Mega Battery](https://steamcommunity.com/sharedfiles/filedetails/?id=2940372971) | B |  |
| 938 | [主动洗手 Active hand washing](https://steamcommunity.com/sharedfiles/filedetails/?id=3020589190) | P,O | PLib options |
| 939 | [Quick Start](https://steamcommunity.com/sharedfiles/filedetails/?id=3160450307) | S,O | PLib options, UI, worldgen |
| 940 | [Super Rover](https://steamcommunity.com/sharedfiles/filedetails/?id=3687352069) | S,O | PLib options, UI, user menu |
| 941 | [High Flow Storage Updated](https://steamcommunity.com/sharedfiles/filedetails/?id=2428180934) | B |  |

### Batch 08: ranks 943–968

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 943 | [任务系统](https://steamcommunity.com/sharedfiles/filedetails/?id=3738243630) | S,V,M | UI |
| 944 | [NeodymiumStyle Hairstyle MOD "PEKORA"](https://steamcommunity.com/sharedfiles/filedetails/?id=2996322065) | X | needs 3172734005, noCode |
| 945 | [泥土发电制氧 Soil power generation and oxygen production](https://steamcommunity.com/sharedfiles/filedetails/?id=3287791163) | B | base game only |
| 946 | [Print Faraday](https://steamcommunity.com/sharedfiles/filedetails/?id=3383357240) | X | needs 3172734005, noCode |
| 947 | [Default Off Switch](https://steamcommunity.com/sharedfiles/filedetails/?id=2295306784) | P,O | PLib options |
| 948 | [Plastic Utilities](https://steamcommunity.com/sharedfiles/filedetails/?id=2821189161) | B,O | PLib options |
| 949 | [Single Small Asteroid With Everything](https://steamcommunity.com/sharedfiles/filedetails/?id=2923696064) | X | worldgen, noCode |
| 951 | [Force Eruption T2 强制喷发](https://steamcommunity.com/sharedfiles/filedetails/?id=3080318955) | P | user menu |
| 952 | [ONI Vietnamese Beautiful Font](https://steamcommunity.com/sharedfiles/filedetails/?id=2574634278) | P | UI |
| 953 | [NeodymiumStyle Hairstyle MOD "2B"](https://steamcommunity.com/sharedfiles/filedetails/?id=2997898111) | X | needs 3172734005, noCode |
| 954 | [Pliers++_LinkCut](https://steamcommunity.com/sharedfiles/filedetails/?id=3793606130) | T,K |  |
| 956 | [U&F-Instant Sandbox 即时沙盒](https://steamcommunity.com/sharedfiles/filedetails/?id=3464929672) | T |  |
| 957 | [MSJ Transmission Line梦世界 输电线（停止更新）](https://steamcommunity.com/sharedfiles/filedetails/?id=2853460032) | P,O | PLib options |
| 958 | [Gold Rush淘金热](https://steamcommunity.com/sharedfiles/filedetails/?id=2848299481) | B,O | PLib options |
| 960 | [NeodymiumStyle Hairstyle MOD "MARISA"](https://steamcommunity.com/sharedfiles/filedetails/?id=2995869693) | X | needs 3172734005, noCode |
| 961 | [Slide Down Ladders](https://steamcommunity.com/sharedfiles/filedetails/?id=2981741974) | P |  |
| 963 | [Sealed and Insulated Containers](https://steamcommunity.com/sharedfiles/filedetails/?id=2689785207) | B,O | PLib options |
| 964 | [Moo Reproduction](https://steamcommunity.com/sharedfiles/filedetails/?id=2400500058) | P,O | PLib options |
| 965 | [P 基础系统 Base System 1.4](https://steamcommunity.com/sharedfiles/filedetails/?id=3344810103) | S,O,V | PLib options, UI |
| 966 | [Leaky Caramel Fissure](https://steamcommunity.com/sharedfiles/filedetails/?id=3591826138) | P | worldgen |
| 967 | [Transparent Solar Panels](https://steamcommunity.com/sharedfiles/filedetails/?id=2693498411) | P |  |
| 968 | [Enable/Disable manual delivery](https://steamcommunity.com/sharedfiles/filedetails/?id=3387132147) | S | UI, user menu |

### Batch 09: ranks 969–994

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 969 | [Geyser Overlay](https://steamcommunity.com/sharedfiles/filedetails/?id=3765355897) | S,K,O,V | PLib options, UI |
| 971 | [植物生长时间和掉落量修改(Plant Growth Time and Drop Amount Set)](https://steamcommunity.com/sharedfiles/filedetails/?id=2283591824) | P |  |
| 972 | [高效按摩床 Efficient Massage Table](https://steamcommunity.com/sharedfiles/filedetails/?id=2894982816) | P,O | PLib options |
| 973 | [Salt Volcano](https://steamcommunity.com/sharedfiles/filedetails/?id=3740131305) | P | worldgen |
| 974 | [Bury Me Not!](https://steamcommunity.com/sharedfiles/filedetails/?id=3573674982) | P |  |
| 975 | [Make Dirt](https://steamcommunity.com/sharedfiles/filedetails/?id=2266128365) | P | needs 2018291283 |
| 976 | [Carry capacity modifier](https://steamcommunity.com/sharedfiles/filedetails/?id=3242595525) | P,O | PLib options |
| 977 | [(visual) Became Green](https://steamcommunity.com/sharedfiles/filedetails/?id=2586992909) | P |  |
| 978 | [Free Horizontal Build](https://steamcommunity.com/sharedfiles/filedetails/?id=3640719341) | T,S,K,O,M | UI, user menu |
| 979 | [Happy Digging: Dwarf Edition Fixed Fixed](https://steamcommunity.com/sharedfiles/filedetails/?id=3383726669) | P,O | PLib options, UI |
| 981 | [Better_Transfer_Arm更好的机械臂](https://steamcommunity.com/sharedfiles/filedetails/?id=3504886133) | P |  |
| 982 | [Bottle Emptier Variable Size FIXED](https://steamcommunity.com/sharedfiles/filedetails/?id=3641397524) | B | user menu |
| 983 | [Mutations++](https://steamcommunity.com/sharedfiles/filedetails/?id=3583419724) | P |  |
| 984 | [G 火箭系统 Rocket System 1.2](https://steamcommunity.com/sharedfiles/filedetails/?id=3357219777) | P,O | PLib options |
| 985 | [Visualization of sensor data](https://steamcommunity.com/sharedfiles/filedetails/?id=2845382372) | B | user menu |
| 986 | [Rotatable Power Transformers (Vanilla/DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=1727987491) | P |  |
| 988 | [Customize Elements](https://steamcommunity.com/sharedfiles/filedetails/?id=2549477669) | P | needs 2018291283 |
| 989 | [Atomic Suits Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3447554654) | X | needs 3447551899, noCode |
| 991 | [Automated Canisters Reloaded](https://steamcommunity.com/sharedfiles/filedetails/?id=3029304394) | B,O | PLib options |
| 992 | [Vine Trellis](https://steamcommunity.com/sharedfiles/filedetails/?id=3690514626) | B,O,V | PLib options, user menu |
| 993 | [Auto Wrangle Babies Too](https://steamcommunity.com/sharedfiles/filedetails/?id=2857018172) | P |  |
| 994 | [Bathroom Items Liquid Ports Swapper](https://steamcommunity.com/sharedfiles/filedetails/?id=2128525958) | P |  |

### Batch 10: ranks 995–1022

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 995 | [更有用的浮油生物 More useful Slicksters](https://steamcommunity.com/sharedfiles/filedetails/?id=2902461362) | P,O | PLib options |
| 996 | [Deconstructable Rocket Tiles (DLC only)](https://steamcommunity.com/sharedfiles/filedetails/?id=2377629562) | P |  |
| 997 | [Cuties](https://steamcommunity.com/sharedfiles/filedetails/?id=3362048907) | X | needs 3172734005, noCode |
| 998 | [CheapBuilding建筑费用减少](https://steamcommunity.com/sharedfiles/filedetails/?id=2915322068) | P |  |
| 1000 | [Handy Herbs](https://steamcommunity.com/sharedfiles/filedetails/?id=3286478591) | P |  |
| 1001 | [Basic 3 Worlds](https://steamcommunity.com/sharedfiles/filedetails/?id=3003736085) | P | worldgen |
| 1002 | [Custom Pipe Colors](https://steamcommunity.com/sharedfiles/filedetails/?id=2167499088) | P,O | PLib options |
| 1004 | [BUG测试工具 BUG test tool one -K20](https://steamcommunity.com/sharedfiles/filedetails/?id=2894769948) | P |  |
| 1006 | [Planet Balancer (fixed)](https://steamcommunity.com/sharedfiles/filedetails/?id=2673317898) | S | UI, worldgen, base game only |
| 1007 | [Refuel Turned-Off Generators (Frosty DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=3317506652) | P |  |
| 1008 | [No Zone Tint](https://steamcommunity.com/sharedfiles/filedetails/?id=2598601623) | P |  |
| 1009 | [Better Coolers](https://steamcommunity.com/sharedfiles/filedetails/?id=2970435539) | P |  |
| 1010 | [Cooling Element](https://steamcommunity.com/sharedfiles/filedetails/?id=1959636284) | B |  |
| 1011 | [Self-sealing Airlocks Revived (U59 Fix)](https://steamcommunity.com/sharedfiles/filedetails/?id=3759181620) | P |  |
| 1012 | [NeodymiumStyle Hairstyle MOD "ZUNDAMON"](https://steamcommunity.com/sharedfiles/filedetails/?id=2998869133) | X | needs 3172734005, noCode |
| 1013 | [SuperComic's Locale Configurator](https://steamcommunity.com/sharedfiles/filedetails/?id=3045956572) | P,O | PLib options, UI, base game only |
| 1014 | [R花瓣墙 Petal Wall](https://steamcommunity.com/sharedfiles/filedetails/?id=2931324205) | B |  |
| 1016 | [(Deprecated) - Trapdoors & Floor Hatches](https://steamcommunity.com/sharedfiles/filedetails/?id=1870621007) | B | mayCrashInPlay |
| 1018 | [Drink Tea Not Coffee](https://steamcommunity.com/sharedfiles/filedetails/?id=1969458444) | P,O | PLib options |
| 1020 | [Faster Drywall&Plate Construction 干板墙&变温板快建](https://steamcommunity.com/sharedfiles/filedetails/?id=1934714769) | P |  |
| 1021 | [Gold Hatch (FM)](https://steamcommunity.com/sharedfiles/filedetails/?id=3344327222) | P |  |
| 1022 | [超级氧气扩散器](https://steamcommunity.com/sharedfiles/filedetails/?id=3119833849) | P |  |

### Batch 11: ranks 1023–1050

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1023 | [Elemental Charcoal](https://steamcommunity.com/sharedfiles/filedetails/?id=3357294645) | P |  |
| 1024 | [Space Store Expansion Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3231852237) | S | UI, needs 3215097987 |
| 1026 | [优化空罐器Optimization Bottle Emptier](https://steamcommunity.com/sharedfiles/filedetails/?id=3029077166) | P |  |
| 1028 | [Glass Travel Tube](https://steamcommunity.com/sharedfiles/filedetails/?id=2022824531) | P |  |
| 1029 | [Drywall Colours](https://steamcommunity.com/sharedfiles/filedetails/?id=2942359329) | P |  |
| 1031 | [制氧机电解水大众版](https://steamcommunity.com/sharedfiles/filedetails/?id=3371878052) | B,O | PLib options, base game only |
| 1032 | [Natural Backwall](https://steamcommunity.com/sharedfiles/filedetails/?id=3746622017) | B | base game only |
| 1033 | [暖噗噗元气电解器](https://steamcommunity.com/sharedfiles/filedetails/?id=3467986764) | P |  |
| 1034 | [ONI QoL Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=3760101842) | S,O,V | PLib options, UI |
| 1035 | [High-Strength Atmo Suit](https://steamcommunity.com/sharedfiles/filedetails/?id=2880489644) | P |  |
| 1036 | [More Plant Mutations](https://steamcommunity.com/sharedfiles/filedetails/?id=3625100610) | S,O | PLib options, UI |
| 1037 | [Pipeline inspection in wall管道检测可穿墙](https://steamcommunity.com/sharedfiles/filedetails/?id=2867460635) | P |  |
| 1038 | [箱子箱子箱子](https://steamcommunity.com/sharedfiles/filedetails/?id=2997605883) | P |  |
| 1040 | [植物成熟自动掉落](https://steamcommunity.com/sharedfiles/filedetails/?id=3743875118) | P,O | PLib options |
| 1042 | [Cluster Generation Settings Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=2945098028) | O,M | PLib options, UI, worldgen |
| 1043 | [2x3 Shower 2x3淋浴间](https://steamcommunity.com/sharedfiles/filedetails/?id=2854756453) | P |  |
| 1044 | [小中大电池容量与漏电自定义](https://steamcommunity.com/sharedfiles/filedetails/?id=3010226731) | P,O | PLib options |
| 1046 | [Random Buildings Skin](https://steamcommunity.com/sharedfiles/filedetails/?id=3741717611) | T,K,O | PLib options, UI |
| 1047 | [Synthetic Evolution 机械飞升](https://steamcommunity.com/sharedfiles/filedetails/?id=3379701036) | P | user menu |
| 1048 | [Increased Meter Limits](https://steamcommunity.com/sharedfiles/filedetails/?id=2839508628) | P,O | PLib options |
| 1049 | [Violent Volcanoes](https://steamcommunity.com/sharedfiles/filedetails/?id=2895288475) | P,O | PLib options |
| 1050 | [Faster Bunker Door(Vanilla+DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=2018086455) | P |  |

### Batch 12: ranks 1051–1074

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1051 | [NeodymiumStyle Hairstyle MOD "PATCHOULI"](https://steamcommunity.com/sharedfiles/filedetails/?id=2995870038) | X | needs 3172734005, noCode |
| 1052 | [精炼镁铁质岩生产钢](https://steamcommunity.com/sharedfiles/filedetails/?id=2994004260) | P |  |
| 1053 | [间歇泉自动计算（Automatic Geyser Calculation中文版本）](https://steamcommunity.com/sharedfiles/filedetails/?id=3618564887) | P |  |
| 1054 | [Optimized Digging](https://steamcommunity.com/sharedfiles/filedetails/?id=3525432758) | P |  |
| 1055 | [R 家具系统 Furniture System 2.4](https://steamcommunity.com/sharedfiles/filedetails/?id=3323729185) | B,O | PLib options |
| 1056 | [Automatic Dispenser: transfer only from Lower Priority](https://steamcommunity.com/sharedfiles/filedetails/?id=1889255373) | P |  |
| 1058 | [Indestructible solar panels](https://steamcommunity.com/sharedfiles/filedetails/?id=3432731289) | P | base game only |
| 1059 | [Improved Radbolt Handling](https://steamcommunity.com/sharedfiles/filedetails/?id=3568711018) | S | UI |
| 1060 | [Shinebug Reactor (updated for Spaced Out)](https://steamcommunity.com/sharedfiles/filedetails/?id=2699868121) | B,S,O | PLib options, UI |
| 1061 | [Self Sealing Airlocks: Restored](https://steamcommunity.com/sharedfiles/filedetails/?id=3747421197) | P |  |
| 1062 | [Cells of Interest](https://steamcommunity.com/sharedfiles/filedetails/?id=3766829264) | T,O | PLib options, UI |
| 1063 | [Environment-More Volcano and Fountain 更多火山和泉水](https://steamcommunity.com/sharedfiles/filedetails/?id=3414157541) | P,O | PLib options, worldgen |
| 1064 | [(Vanilla + DLC) Illumination Sensor](https://steamcommunity.com/sharedfiles/filedetails/?id=2326745828) | B |  |
| 1065 | [更新鲜的美食小吃V2.0 (NEW Gourmet Complement Pack)](https://steamcommunity.com/sharedfiles/filedetails/?id=3614348529) | P |  |
| 1066 | [Flydo: The Flying Digger](https://steamcommunity.com/sharedfiles/filedetails/?id=3714990201) | P |  |
| 1067 | [(Vanilla + DLC) Eco-friendly Toilet & Farm+ OV3.0](https://steamcommunity.com/sharedfiles/filedetails/?id=1886183536) | B | user menu |
| 1068 | [Potato Power Banks](https://steamcommunity.com/sharedfiles/filedetails/?id=3386552780) | P,O | PLib options |
| 1069 | [Pinned Resource Row](https://steamcommunity.com/sharedfiles/filedetails/?id=3736878109) | T,S,O | PLib options, UI |
| 1070 | [Super High Wattage Wire （read only and stop update the mod ,you try to update from GITHUB for youself, look detail）](https://steamcommunity.com/sharedfiles/filedetails/?id=1963992940) | B |  |
| 1072 | [人造植物 / PlantExtension](https://steamcommunity.com/sharedfiles/filedetails/?id=3719204845) | B,O | PLib options |
| 1073 | [喷泉无压力反应](https://steamcommunity.com/sharedfiles/filedetails/?id=3006715942) | P |  |
| 1074 | [One Giant Leap](https://steamcommunity.com/sharedfiles/filedetails/?id=1999001228) | S | UI |

### Batch 13: ranks 1075–1104

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1075 | [Blueprints Button More Hints](https://steamcommunity.com/sharedfiles/filedetails/?id=3494066563) | S | UI |
| 1077 | [PinYinSearch](https://steamcommunity.com/sharedfiles/filedetails/?id=3235904679) | S | UI |
| 1078 | [汽水机咖啡榨汁机BUFF叠加](https://steamcommunity.com/sharedfiles/filedetails/?id=3556910379) | P |  |
| 1079 | [Dupes speed custom Settings](https://steamcommunity.com/sharedfiles/filedetails/?id=3456696039) | P,O | PLib options, base game only |
| 1081 | [U&F-More Food 更多食物](https://steamcommunity.com/sharedfiles/filedetails/?id=3380780873) | P |  |
| 1084 | [水氧機 (Water Oxygenator)](https://steamcommunity.com/sharedfiles/filedetails/?id=3625551075) | B,S,K | UI, user menu |
| 1085 | [Engies Tuneup Inactivity Refund](https://steamcommunity.com/sharedfiles/filedetails/?id=3373024595) | P,O | PLib options |
| 1086 | [浮油生物进化！Evolution！](https://steamcommunity.com/sharedfiles/filedetails/?id=3462398525) | P |  |
| 1087 | [T 安的魔法厨房 An's Magic Kitchen 2.9](https://steamcommunity.com/sharedfiles/filedetails/?id=3346698308) | B,O | PLib options |
| 1088 | [U&F-Resize Room 修改房间大小](https://steamcommunity.com/sharedfiles/filedetails/?id=3374595157) | P,O | PLib options |
| 1090 | [Show Undiscovered](https://steamcommunity.com/sharedfiles/filedetails/?id=3010706556) | P,O | PLib options, UI |
| 1092 | [SuperComic's MODs Language Pack - EN/JA/ZH (Reworked)](https://steamcommunity.com/sharedfiles/filedetails/?id=2785073113) | X | needs 3045956572, noCode |
| 1093 | [获得所有好特质Get All The Good Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=3419419199) | P |  |
| 1094 | [Sensor Graph (with CSV Export)](https://steamcommunity.com/sharedfiles/filedetails/?id=3651879332) | P | UI |
| 1095 | [Never Flood Critter Drop-Off fixed](https://steamcommunity.com/sharedfiles/filedetails/?id=2667192005) | P |  |
| 1097 | [Thermostatic Space恒温空间（停止更新）](https://steamcommunity.com/sharedfiles/filedetails/?id=2862928002) | B,O | PLib options |
| 1098 | [Sanishells Clear Water](https://steamcommunity.com/sharedfiles/filedetails/?id=2833476342) | P |  |
| 1099 | [Super Refrigerator](https://steamcommunity.com/sharedfiles/filedetails/?id=3594387679) | P |  |
| 1101 | [Planet Adder](https://steamcommunity.com/sharedfiles/filedetails/?id=1749025321) | S | UI, worldgen, base game only |
| 1102 | [水馒头](https://steamcommunity.com/sharedfiles/filedetails/?id=3743438381) | V |  |
| 1103 | [Automation Bypass](https://steamcommunity.com/sharedfiles/filedetails/?id=2906743782) | V | UI, user menu |
| 1104 | ["Move Here" Command Tile](https://steamcommunity.com/sharedfiles/filedetails/?id=2901813453) | B |  |

### Batch 14: ranks 1105–1133

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1105 | [NeodymiumStyle Hairstyle MOD "SAKUYA"](https://steamcommunity.com/sharedfiles/filedetails/?id=3247021036) | X | needs 3172734005, noCode |
| 1106 | [Oil Well石油井](https://steamcommunity.com/sharedfiles/filedetails/?id=2841918251) | B |  |
| 1107 | [用污水制造铁 Using sewage to produce iron](https://steamcommunity.com/sharedfiles/filedetails/?id=3130411418) | B | base game only |
| 1108 | [Don't use mutated plants/seeds](https://steamcommunity.com/sharedfiles/filedetails/?id=2712885117) | P,O | PLib options, user menu |
| 1109 | [Overjoy Chance Rebalance](https://steamcommunity.com/sharedfiles/filedetails/?id=3207798260) | P |  |
| 1110 | [跨越架Crossing Frame](https://steamcommunity.com/sharedfiles/filedetails/?id=3011189357) | B |  |
| 1111 | [氢气灶HydrogenStove](https://steamcommunity.com/sharedfiles/filedetails/?id=2848829914) | B,O | PLib options |
| 1112 | [Highlight Overlay](https://steamcommunity.com/sharedfiles/filedetails/?id=3170592482) | T,S,O,V,M | PLib options, UI, needs 2018291283 |
| 1113 | [取水大爷的量杯 / Bottle Emptier Measuring Cup](https://steamcommunity.com/sharedfiles/filedetails/?id=3768766063) | S | UI |
| 1115 | [Unblockable Rockets](https://steamcommunity.com/sharedfiles/filedetails/?id=1962983262) | P |  |
| 1117 | [Edge Detectors and Diode (Automation)](https://steamcommunity.com/sharedfiles/filedetails/?id=2777145427) | B |  |
| 1119 | [Amphibious Minnow (Species Mod)](https://steamcommunity.com/sharedfiles/filedetails/?id=3753181401) | S,O,M | PLib options, UI |
| 1121 | [ArchipelagoNotIncluded](https://steamcommunity.com/sharedfiles/filedetails/?id=3415553359) | T,S,O,M | PLib options, UI, needs 2018291283 |
| 1123 | [Relic Hunter](https://steamcommunity.com/sharedfiles/filedetails/?id=1927259233) | P | base game only |
| 1124 | [Upgradeable Dupes And Critters](https://steamcommunity.com/sharedfiles/filedetails/?id=3329512249) | B,O | PLib options |
| 1125 | [魔女的箱子](https://steamcommunity.com/sharedfiles/filedetails/?id=3022610348) | B |  |
| 1126 | [F 站台系统 Platform System 1.7](https://steamcommunity.com/sharedfiles/filedetails/?id=3316755325) | P,O | PLib options |
| 1128 | [Attribute XP Bars](https://steamcommunity.com/sharedfiles/filedetails/?id=3759482835) | S,O | PLib options, UI |
| 1129 | [Drecko Ranching](https://steamcommunity.com/sharedfiles/filedetails/?id=2094022117) | P |  |
| 1130 | [Hot Nuclear Power](https://steamcommunity.com/sharedfiles/filedetails/?id=2633732356) | B,M |  |
| 1131 | [移除碎片合成重量限制](https://steamcommunity.com/sharedfiles/filedetails/?id=3240628520) | P |  |
| 1133 | [RemoveDailyReports](https://steamcommunity.com/sharedfiles/filedetails/?id=1870873514) | P |  |

### Batch 15: ranks 1134–1167

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1134 | [Set Default Deconstruct Layer](https://steamcommunity.com/sharedfiles/filedetails/?id=1863180631) | T,S | UI |
| 1135 | [金属精炼器10倍产出](https://steamcommunity.com/sharedfiles/filedetails/?id=3302049214) | P |  |
| 1138 | [Debris Melts to Debris](https://steamcommunity.com/sharedfiles/filedetails/?id=2570834434) | P | base game only |
| 1140 | [Decoration of everything万物装饰](https://steamcommunity.com/sharedfiles/filedetails/?id=2868395237) | P,O | PLib options |
| 1144 | [R 精致梯子 Exquisite Ladder 1.2](https://steamcommunity.com/sharedfiles/filedetails/?id=2843896901) | B,O | PLib options |
| 1145 | [油井自动排放天然气和无天然气原油精炼器（Auto oilwell and None nature gas crude oil refiners）](https://steamcommunity.com/sharedfiles/filedetails/?id=3308456170) | P,O | PLib options |
| 1146 | [Build Priority Keys](https://steamcommunity.com/sharedfiles/filedetails/?id=2959309464) | S,K | UI |
| 1148 | [高级气压服 / Advanced Atmo Suit](https://steamcommunity.com/sharedfiles/filedetails/?id=3733064550) | P,O | PLib options |
| 1149 | [Pedestal Filter](https://steamcommunity.com/sharedfiles/filedetails/?id=3260591912) | S | UI |
| 1150 | [狗子的取水量杯](https://steamcommunity.com/sharedfiles/filedetails/?id=3630055851) | P |  |
| 1151 | [Trapdoors & Floor Hatches Revived](https://steamcommunity.com/sharedfiles/filedetails/?id=3523317587) | B |  |
| 1153 | [Auto-Sweeper Harvest](https://steamcommunity.com/sharedfiles/filedetails/?id=3566906492) | P |  |
| 1155 | [Soggy Carpets](https://steamcommunity.com/sharedfiles/filedetails/?id=2719163704) | O,V | PLib options |
| 1157 | [U&F-Super Oxygen Diffuser超级氧气扩散机](https://steamcommunity.com/sharedfiles/filedetails/?id=3377035496) | P,O | PLib options |
| 1159 | [Liquid Lock Builder](https://steamcommunity.com/sharedfiles/filedetails/?id=2627892585) | B |  |
| 1160 | [Water Boiler 沸水器](https://steamcommunity.com/sharedfiles/filedetails/?id=3306319445) | B |  |
| 1161 | [Sweep Those Eggs](https://steamcommunity.com/sharedfiles/filedetails/?id=1856796585) | B | needs 2018291283 |
| 1162 | [R 吊床 Hammock 1.8](https://steamcommunity.com/sharedfiles/filedetails/?id=2851100066) | B |  |
| 1163 | [存储箱修改(可配置)](https://steamcommunity.com/sharedfiles/filedetails/?id=3606730745) | P,O | PLib options |
| 1164 | [Auto Harvest - 植物自动收割](https://steamcommunity.com/sharedfiles/filedetails/?id=3732732522) | P |  |
| 1165 | [Not crowded （不在拥挤）](https://steamcommunity.com/sharedfiles/filedetails/?id=3566537088) | P |  |
| 1167 | [Flexible Drywalls](https://steamcommunity.com/sharedfiles/filedetails/?id=2138696495) | P |  |

### Batch 16: ranks 1168–1196

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1168 | [Multi Selection Sensors](https://steamcommunity.com/sharedfiles/filedetails/?id=3635077282) | B |  |
| 1169 | [Super radish超级萝卜](https://steamcommunity.com/sharedfiles/filedetails/?id=2871667916) | P,O | PLib options |
| 1171 | [Super Insulator and Abyssalite with ZERO thermal conductivity](https://steamcommunity.com/sharedfiles/filedetails/?id=3138192254) | P |  |
| 1173 | [Compact Water Processors](https://steamcommunity.com/sharedfiles/filedetails/?id=3716115973) | B,S | UI |
| 1175 | [Transparent Door](https://steamcommunity.com/sharedfiles/filedetails/?id=2032688772) | B |  |
| 1177 | [U&F-Super Wild Planter Box 超级种植箱和土培砖](https://steamcommunity.com/sharedfiles/filedetails/?id=3380661336) | B,O | PLib options |
| 1178 | [Steam Turbines do not generate heat](https://steamcommunity.com/sharedfiles/filedetails/?id=3129405632) | P |  |
| 1179 | [超级手动气闸](https://steamcommunity.com/sharedfiles/filedetails/?id=3004006350) | P |  |
| 1180 | [Never Flood Critter Drop-Off/Pick-Up.](https://steamcommunity.com/sharedfiles/filedetails/?id=2158035413) | P |  |
| 1181 | [吃人肉](https://steamcommunity.com/sharedfiles/filedetails/?id=3318646518) | P |  |
| 1182 | [Backup Suit  备用气压服](https://steamcommunity.com/sharedfiles/filedetails/?id=3103089853) | P |  |
| 1183 | [Sweepy liquid output](https://steamcommunity.com/sharedfiles/filedetails/?id=2950199712) | P | base game only |
| 1186 | [Highlight Debris](https://steamcommunity.com/sharedfiles/filedetails/?id=2971588345) | S | UI |
| 1188 | [Rocket's Larger Cargo Hold火箭更大货舱](https://steamcommunity.com/sharedfiles/filedetails/?id=2858196664) | P,O | PLib options |
| 1189 | [Graveyard Keeper](https://steamcommunity.com/sharedfiles/filedetails/?id=2885981437) | P,O | PLib options, UI |
| 1190 | [(Vanilla + DLC) Fast Wireless Automation+ OV3.0](https://steamcommunity.com/sharedfiles/filedetails/?id=1722812098) | B |  |
| 1191 | [Scrub Pack (10 now)](https://steamcommunity.com/sharedfiles/filedetails/?id=2106223486) | B,O | PLib options |
| 1192 | [DirectErrandAssignment](https://steamcommunity.com/sharedfiles/filedetails/?id=3798979006) | S | UI |
| 1193 | [Where's Flydo](https://steamcommunity.com/sharedfiles/filedetails/?id=3479234230) | P |  |
| 1194 | [Momentary switch](https://steamcommunity.com/sharedfiles/filedetails/?id=2624509351) | B,S,K | UI |
| 1195 | [Full of Juice](https://steamcommunity.com/sharedfiles/filedetails/?id=3706917570) | P |  |
| 1196 | [川菜 -K14](https://steamcommunity.com/sharedfiles/filedetails/?id=3021138574) | P |  |

### Batch 17: ranks 1197–1220

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1197 | [(Fix) Ethanol Geyser](https://steamcommunity.com/sharedfiles/filedetails/?id=2998321975) | P | worldgen |
| 1198 | [Expeditions: The Shattered Planet](https://steamcommunity.com/sharedfiles/filedetails/?id=3803506841) | B,S,K,O,V | PLib options, UI, worldgen |
| 1199 | [RT-Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3174662603) | B |  |
| 1200 | [(DLC Only) Spaced Out Rime (under construction)](https://steamcommunity.com/sharedfiles/filedetails/?id=2462236549) | X | worldgen, noCode |
| 1201 | [(Vanilla + DLC) Move Duplicant by Middle Mouse Button Updated](https://steamcommunity.com/sharedfiles/filedetails/?id=2848571032) | P |  |
| 1202 | [小人技能等级速升100上限](https://steamcommunity.com/sharedfiles/filedetails/?id=3257382751) | P |  |
| 1203 | [隐藏管道](https://steamcommunity.com/sharedfiles/filedetails/?id=3486126706) | P |  |
| 1204 | [太空服容量可调Space suit capacity adjustable -K24](https://steamcommunity.com/sharedfiles/filedetails/?id=3094695022) | P |  |
| 1205 | [更好的电解器](https://steamcommunity.com/sharedfiles/filedetails/?id=3671458367) | B,O | PLib options |
| 1206 | [Tune-Up: Prioritize Generators](https://steamcommunity.com/sharedfiles/filedetails/?id=1872113768) | P |  |
| 1207 | [喵喵床Meow bed](https://steamcommunity.com/sharedfiles/filedetails/?id=2879691917) | B |  |
| 1208 | [Dig Mass Tweak 挖掘质量调整](https://steamcommunity.com/sharedfiles/filedetails/?id=3586981589) | P,O | PLib options |
| 1210 | [Traits Now Included (DLC-compatible)](https://steamcommunity.com/sharedfiles/filedetails/?id=2682170372) | X | worldgen, noCode |
| 1211 | [脱盐器超级加强](https://steamcommunity.com/sharedfiles/filedetails/?id=3100867929) | P,O | PLib options |
| 1212 | [小人工作最大等级上限自定义](https://steamcommunity.com/sharedfiles/filedetails/?id=3571290810) | P |  |
| 1213 | [Building-Super Solar Panel 超级太阳能发电板](https://steamcommunity.com/sharedfiles/filedetails/?id=3376657142) | P,O | PLib options |
| 1214 | [控温砖（Temperature Moderation Tile）](https://steamcommunity.com/sharedfiles/filedetails/?id=3227539567) | B |  |
| 1215 | [Crabs flip Compost](https://steamcommunity.com/sharedfiles/filedetails/?id=3609497880) | P |  |
| 1216 | [调谐仪(多合一)](https://steamcommunity.com/sharedfiles/filedetails/?id=3519403656) | S,O | PLib options, UI |
| 1217 | [NeodymiumStyle Hairstyle MOD "LAYNA"](https://steamcommunity.com/sharedfiles/filedetails/?id=3427945960) | X | needs 3172734005, noCode |
| 1218 | [Fix Store Errand Priority](https://steamcommunity.com/sharedfiles/filedetails/?id=3177260238) | P |  |
| 1220 | [气体扩散器 Gas Diffuser](https://steamcommunity.com/sharedfiles/filedetails/?id=2962524595) | B |  |

### Batch 18: ranks 1221–1249

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1221 | [The Last Legacy](https://steamcommunity.com/sharedfiles/filedetails/?id=2161936214) | P |  |
| 1222 | [Advanced Wattage Sensor](https://steamcommunity.com/sharedfiles/filedetails/?id=2315290740) | B |  |
| 1224 | [Adjustable Sweeper](https://steamcommunity.com/sharedfiles/filedetails/?id=3143341732) | P | base game only |
| 1225 | [Luxury Drywall](https://steamcommunity.com/sharedfiles/filedetails/?id=2839171298) | B,S | UI |
| 1226 | [Conveyor Loaders Stop Loading with Disabled Automation Signal](https://steamcommunity.com/sharedfiles/filedetails/?id=3474301255) | K |  |
| 1227 | [Store Artifacts](https://steamcommunity.com/sharedfiles/filedetails/?id=2832650525) | P |  |
| 1229 | [Liquid Bottler](https://steamcommunity.com/sharedfiles/filedetails/?id=3100888062) | B | user menu |
| 1232 | [水生成氧气和乙醇  Water generates oxygen and ethanol](https://steamcommunity.com/sharedfiles/filedetails/?id=3297925875) | B |  |
| 1233 | [粉碎镁铁质岩生产铁](https://steamcommunity.com/sharedfiles/filedetails/?id=2994335358) | P |  |
| 1234 | [Master Electrolizer V2 (Updated)](https://steamcommunity.com/sharedfiles/filedetails/?id=3340413393) | B |  |
| 1236 | [Infinite Storage Configurable](https://steamcommunity.com/sharedfiles/filedetails/?id=3686492383) | P,O | PLib options |
| 1237 | [Extra Trait: Bioluminescent](https://steamcommunity.com/sharedfiles/filedetails/?id=3417626005) | S | UI |
| 1238 | [Deconstruct Only Buildings (Fix for Aquatic)](https://steamcommunity.com/sharedfiles/filedetails/?id=3743166025) | T |  |
| 1240 | [优化氧气面罩Optimize Oxygen Masks -K18](https://steamcommunity.com/sharedfiles/filedetails/?id=3087504657) | P |  |
| 1241 | [NeodymiumStyle Hairstyle MOD "METAN"](https://steamcommunity.com/sharedfiles/filedetails/?id=3429341925) | X | needs 3172734005, noCode |
| 1242 | [Synchronized Telepad (updated for Big Merge)](https://steamcommunity.com/sharedfiles/filedetails/?id=2686262124) | P |  |
| 1243 | [Bigger Bottle Emptier Conduit Gas](https://steamcommunity.com/sharedfiles/filedetails/?id=3537327790) | P |  |
| 1245 | [净水器消毒出水和肥料金汞齐泥土粘土硫藻](https://steamcommunity.com/sharedfiles/filedetails/?id=3276461530) | P |  |
| 1246 | [OverHeat+1000(Vanilla+DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=1994613812) | P |  |
| 1247 | [Insulated Pressure Door](https://steamcommunity.com/sharedfiles/filedetails/?id=3608466042) | B |  |
| 1248 | [医疗箱](https://steamcommunity.com/sharedfiles/filedetails/?id=2982751229) | B |  |
| 1249 | [Critter Settings Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=3734363283) | M | UI |

### Batch 19: ranks 1250–1278

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1250 | [植物相关](https://steamcommunity.com/sharedfiles/filedetails/?id=3019685469) | P,O | PLib options |
| 1251 | [Power Needed is 1W(Vanilla+DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=1994592210) | P |  |
| 1253 | [Master Electrolizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3339017519) | B |  |
| 1254 | [Auto Rename Dupes](https://steamcommunity.com/sharedfiles/filedetails/?id=3006462315) | P,O | PLib options |
| 1256 | [Any Ice Maker](https://steamcommunity.com/sharedfiles/filedetails/?id=3521556826) | P,O | PLib options |
| 1257 | [Rocket Drop Artifacts火箭自动释放工艺品](https://steamcommunity.com/sharedfiles/filedetails/?id=2876954518) | P |  |
| 1258 | [NeodymiumStyle Hairstyle MOD "YUZU"](https://steamcommunity.com/sharedfiles/filedetails/?id=3247018513) | X | needs 3172734005, noCode |
| 1262 | [ReshufflingArchetype](https://steamcommunity.com/sharedfiles/filedetails/?id=2641977549) | S | UI |
| 1263 | [Taller Mods Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=2553830824) | M |  |
| 1264 | [Gassy Moo Eat Flowers](https://steamcommunity.com/sharedfiles/filedetails/?id=3017358638) | P,O | PLib options, UI |
| 1265 | [NeodymiumStyle Hairstyle MOD "OLD-SNAKE"](https://steamcommunity.com/sharedfiles/filedetails/?id=2997380964) | X | needs 3172734005, noCode |
| 1266 | [The door of Mohism（墨家的门）](https://steamcommunity.com/sharedfiles/filedetails/?id=2842066009) | B |  |
| 1267 | [Efficient Filters](https://steamcommunity.com/sharedfiles/filedetails/?id=2581938773) | P,O | PLib options |
| 1268 | [植物生长温度修改(Plants Growing's Temperature Set)](https://steamcommunity.com/sharedfiles/filedetails/?id=2366912027) | P |  |
| 1269 | [(2027/QoL) Automatic Industry 2.5.0](https://steamcommunity.com/sharedfiles/filedetails/?id=3782701870) | S,K,O,M | PLib options, bundle UI, user menu |
| 1270 | [Heliconia - The Thermal Runaway](https://steamcommunity.com/sharedfiles/filedetails/?id=3663491695) | O,M | PLib options, worldgen |
| 1271 | [No pipe break](https://steamcommunity.com/sharedfiles/filedetails/?id=3518867699) | P |  |
| 1273 | [Tiny Worlds (updated for Big Merge)](https://steamcommunity.com/sharedfiles/filedetails/?id=2700665718) | P,O | PLib options, worldgen, base game only |
| 1275 | [K 医疗系统 Medical System 1.4](https://steamcommunity.com/sharedfiles/filedetails/?id=3319924525) | B,O | PLib options |
| 1276 | [HD Screenshot](https://steamcommunity.com/sharedfiles/filedetails/?id=3098068395) | S,O,V | PLib options, UI |
| 1277 | [Greater radiation rocket/accumulator capacity](https://steamcommunity.com/sharedfiles/filedetails/?id=2946478258) | P,O | PLib options |
| 1278 | [Vitals Display Rads](https://steamcommunity.com/sharedfiles/filedetails/?id=3235954752) | S | UI |

### Batch 20: ranks 1280–1308

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1280 | [Building-Super Manual Generator 超级人力发电机](https://steamcommunity.com/sharedfiles/filedetails/?id=3390256280) | P,O | PLib options |
| 1282 | [(Vanilla + DLC) Diet Variety](https://steamcommunity.com/sharedfiles/filedetails/?id=3235885361) | P,O | PLib options |
| 1283 | [Plant Settings Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=3733686186) | M | UI |
| 1285 | [Temperature Insulate Farm Tiles](https://steamcommunity.com/sharedfiles/filedetails/?id=3137678878) | P |  |
| 1286 | [Movable Graves](https://steamcommunity.com/sharedfiles/filedetails/?id=2871814023) | P | user menu |
| 1287 | [Large eye decoration大眼睛装饰](https://steamcommunity.com/sharedfiles/filedetails/?id=2870776303) | P |  |
| 1288 | [ACGN二次元之魂](https://steamcommunity.com/sharedfiles/filedetails/?id=2863350317) | B,S | UI |
| 1289 | [No-Water Tofu](https://steamcommunity.com/sharedfiles/filedetails/?id=2658864677) | P |  |
| 1290 | [浮油生物原型机 Floating oil bio prototype](https://steamcommunity.com/sharedfiles/filedetails/?id=3468473855) | B |  |
| 1293 | [地质调谐仪不再消耗漂白石//No Bleach Stone Be Needed](https://steamcommunity.com/sharedfiles/filedetails/?id=3464435901) | P,O | PLib options |
| 1294 | [Easy Achievements](https://steamcommunity.com/sharedfiles/filedetails/?id=3593359023) | P |  |
| 1296 | [Warp Portal Skip Cooldown](https://steamcommunity.com/sharedfiles/filedetails/?id=3377698389) | P |  |
| 1299 | [Supply strength training](https://steamcommunity.com/sharedfiles/filedetails/?id=3715884370) | P,O | PLib options |
| 1300 | [Ronivans_Patch工业革命机器自动工作补丁](https://steamcommunity.com/sharedfiles/filedetails/?id=3582320363) | P,O | PLib options, needs 3557584850 |
| 1301 | [Seeders](https://steamcommunity.com/sharedfiles/filedetails/?id=2561983293) | B,O | PLib options |
| 1302 | [智能空间加热器Intelligent Space Heater](https://steamcommunity.com/sharedfiles/filedetails/?id=3038950355) | P |  |
| 1303 | [Research Completed](https://steamcommunity.com/sharedfiles/filedetails/?id=2845882251) | P |  |
| 1304 | [Minnow Story Trait](https://steamcommunity.com/sharedfiles/filedetails/?id=3749738996) | S | UI, worldgen |
| 1305 | [Weeb Dupes](https://steamcommunity.com/sharedfiles/filedetails/?id=3208492885) | P |  |
| 1306 | [Force Mutate 手动添加变异](https://steamcommunity.com/sharedfiles/filedetails/?id=3075237915) | P | user menu |
| 1307 | [Morning Exercise](https://steamcommunity.com/sharedfiles/filedetails/?id=3629077549) | B,O | PLib options |
| 1308 | [Pip Planting Notification](https://steamcommunity.com/sharedfiles/filedetails/?id=3642824463) | P |  |

### Batch 21: ranks 1309–1362

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1309 | [Cross-Compatible Sculpture Skins](https://steamcommunity.com/sharedfiles/filedetails/?id=3652188798) | P |  |
| 1310 | [缺氧综合修改](https://steamcommunity.com/sharedfiles/filedetails/?id=3633033132) | T,S,O | PLib options, UI |
| 1312 | [Sumup](https://steamcommunity.com/sharedfiles/filedetails/?id=3307141077) | T |  |
| 1313 | [Lagoo Revived](https://steamcommunity.com/sharedfiles/filedetails/?id=3417326857) | P,O | PLib options, worldgen |
| 1314 | [Longer Bridges](https://steamcommunity.com/sharedfiles/filedetails/?id=3751316059) | B,V |  |
| 1315 | [全自动孵化器/automatic egg incubator](https://steamcommunity.com/sharedfiles/filedetails/?id=3369091307) | P,O | PLib options |
| 1316 | [(Vanilla + DLC) More Logic Ports](https://steamcommunity.com/sharedfiles/filedetails/?id=2967059775) | P |  |
| 1317 | [小人10倍技能效果](https://steamcommunity.com/sharedfiles/filedetails/?id=3545919882) | P |  |
| 1320 | [Amorbus](https://steamcommunity.com/sharedfiles/filedetails/?id=2899109675) | P,O | PLib options |
| 1321 | [Festive Halloween Spices](https://steamcommunity.com/sharedfiles/filedetails/?id=2877478087) | P,O | PLib options, UI |
| 1322 | [Endless Starmap Mining](https://steamcommunity.com/sharedfiles/filedetails/?id=3151236853) | P,O | PLib options |
| 1323 | [Frostbite Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3294665522) | P,O | PLib options |
| 1324 | [H 运输系统 Transport System 1.6](https://steamcommunity.com/sharedfiles/filedetails/?id=3436166490) | B,O | PLib options |
| 1325 | [ZTransport](https://steamcommunity.com/sharedfiles/filedetails/?id=2251056721) | B,S,M | UI, user menu |
| 1327 | [Sweepy Mega Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3752819733) | P,O | PLib options, user menu |
| 1329 | [Improved World Traits (UPDATED)](https://steamcommunity.com/sharedfiles/filedetails/?id=2994137080) | P | worldgen |
| 1332 | [In-Game Debug Menu (God Mode & Crisis Manager)](https://steamcommunity.com/sharedfiles/filedetails/?id=3686726286) | P | UI |
| 1333 | [Custom Chore Types](https://steamcommunity.com/sharedfiles/filedetails/?id=3496634352) | S | bundle UI |
| 1335 | [优化变压器Optimize transformer](https://steamcommunity.com/sharedfiles/filedetails/?id=2880488690) | B |  |
| 1336 | [Simplify display of booster assigned](https://steamcommunity.com/sharedfiles/filedetails/?id=3408422056) | S | UI |
| 1361 | [Untame Critters](https://steamcommunity.com/sharedfiles/filedetails/?id=2570841961) | P |  |
| 1362 | [Teleporter Buildable 传送器可建造](https://steamcommunity.com/sharedfiles/filedetails/?id=2871615016) | P |  |

### Batch 22: ranks 1363–1389

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1363 | [氧气模块](https://steamcommunity.com/sharedfiles/filedetails/?id=3532900285) | B |  |
| 1364 | [Advanced Critter Sensor](https://steamcommunity.com/sharedfiles/filedetails/?id=2791613657) | B,S | UI |
| 1367 | [ArtifactsPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=3494305999) | K,O | PLib options |
| 1368 | [Sweepn't - Ignore Sweep and Fetch](https://steamcommunity.com/sharedfiles/filedetails/?id=2957155887) | T,S,K,V | UI |
| 1369 | [Asteroid add Rare Resources.小行星增加稀有资源](https://steamcommunity.com/sharedfiles/filedetails/?id=3080513426) | P |  |
| 1370 | [(Vanilla + DLC) Chemical Burns Updated](https://steamcommunity.com/sharedfiles/filedetails/?id=2558069681) | P,O | PLib options |
| 1371 | [High Pump](https://steamcommunity.com/sharedfiles/filedetails/?id=2582168293) | B,S | UI |
| 1372 | [Large Smart Battery](https://steamcommunity.com/sharedfiles/filedetails/?id=3617546696) | B,O | PLib options |
| 1373 | [轮盘工具 (Roulette Tool)](https://steamcommunity.com/sharedfiles/filedetails/?id=3020979372) | T,S,K | UI |
| 1374 | [Oil Refinery Passthrough](https://steamcommunity.com/sharedfiles/filedetails/?id=2951159669) | P |  |
| 1375 | [Outfit Override](https://steamcommunity.com/sharedfiles/filedetails/?id=3674972348) | P,O | PLib options, UI |
| 1376 | [Smart Reservoirs](https://steamcommunity.com/sharedfiles/filedetails/?id=1892748957) | B |  |
| 1377 | [Empty Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3626760918) | P,O | PLib options, user menu |
| 1378 | [Advanced Wattage Sensor](https://steamcommunity.com/sharedfiles/filedetails/?id=3651614675) | B,S,O | PLib options, UI |
| 1380 | [自动制作(No Operation Building For 'Space_Out' In DLC")](https://steamcommunity.com/sharedfiles/filedetails/?id=2385787103) | P |  |
| 1381 | [哈夫克的净水器｜Pollution Recycler](https://steamcommunity.com/sharedfiles/filedetails/?id=3712019800) | B | user menu |
| 1382 | [Civil battery pack民用电池组](https://steamcommunity.com/sharedfiles/filedetails/?id=2862067964) | B,O | PLib options |
| 1385 | [Entomb Tempshiftplates](https://steamcommunity.com/sharedfiles/filedetails/?id=2983485002) | P |  |
| 1386 | [翻飞幻梦Flying Fantasy（失效）](https://steamcommunity.com/sharedfiles/filedetails/?id=2883456864) | B,S | UI |
| 1387 | [种子掉落概率与变异概率](https://steamcommunity.com/sharedfiles/filedetails/?id=3278197623) | P |  |
| 1388 | [窑炉10倍产量_木头出煤炭异构树脂](https://steamcommunity.com/sharedfiles/filedetails/?id=3302604134) | P |  |
| 1389 | [資源探索 (MaterialFinder) 繁/簡/EN](https://steamcommunity.com/sharedfiles/filedetails/?id=3630448775) | T,S,K | UI |

### Batch 23: ranks 1392–1416

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1392 | [StorageNetwork-储存网络](https://steamcommunity.com/sharedfiles/filedetails/?id=3732422991) | B,T,S,K,M | UI, user menu, nativeCalls |
| 1393 | [Sweepy Dock Output Ports](https://steamcommunity.com/sharedfiles/filedetails/?id=3785413427) | V | base game only |
| 1394 | [Closest Material（就近取材）mod汉化](https://steamcommunity.com/sharedfiles/filedetails/?id=3614378759) | T,S,K | UI |
| 1395 | [一格宽的饮水机](https://steamcommunity.com/sharedfiles/filedetails/?id=2861096848) | P |  |
| 1396 | [Petroleum Generator Gas Output](https://steamcommunity.com/sharedfiles/filedetails/?id=3387705078) | P |  |
| 1397 | [Insulate Storage Bin](https://steamcommunity.com/sharedfiles/filedetails/?id=3439686467) | B,O | PLib options, user menu |
| 1399 | [受困检测工具（Trapped Monitor Tool）](https://steamcommunity.com/sharedfiles/filedetails/?id=3248219744) | B,T |  |
| 1400 | [Drywall Tile Skins](https://steamcommunity.com/sharedfiles/filedetails/?id=3794025758) | V,M | UI, needs 2815406414, 2660131829 |
| 1401 | [AlwaysRequestSuitLocker fixed](https://steamcommunity.com/sharedfiles/filedetails/?id=3499114295) | P |  |
| 1402 | [(Vanilla + DLC) Configurable Build Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=2669076399) | V |  |
| 1403 | [(FIX) Storage Pod (贮藏舱)](https://steamcommunity.com/sharedfiles/filedetails/?id=2999492290) | B,O | PLib options |
| 1404 | [Retuned Power Transformer](https://steamcommunity.com/sharedfiles/filedetails/?id=2031115024) | P |  |
| 1405 | [Wood Pile Reconstructible](https://steamcommunity.com/sharedfiles/filedetails/?id=3535712801) | P |  |
| 1408 | [Lamp The Darkness](https://steamcommunity.com/sharedfiles/filedetails/?id=3126557675) | P |  |
| 1409 | [Korean - 한국어](https://steamcommunity.com/sharedfiles/filedetails/?id=3747002507) | P |  |
| 1410 | [可调节的气体罐装器Adjustable Gas Canner](https://steamcommunity.com/sharedfiles/filedetails/?id=3027108354) | P |  |
| 1411 | [永不过热液冷机1瓦耗电（Low power consumption, no overheating）](https://steamcommunity.com/sharedfiles/filedetails/?id=3133678595) | P |  |
| 1412 | [Powerless Smart Bins](https://steamcommunity.com/sharedfiles/filedetails/?id=2804447598) | P |  |
| 1413 | [P 红色和蓝色的垃圾桶 Red And Blue Trash Cans 1.4](https://steamcommunity.com/sharedfiles/filedetails/?id=2984873958) | B |  |
| 1414 | [Skills and Stats Progress FIXED](https://steamcommunity.com/sharedfiles/filedetails/?id=3662450561) | S,O | PLib options, UI |
| 1415 | [Wall Pedestal](https://steamcommunity.com/sharedfiles/filedetails/?id=2841251487) | B |  |
| 1416 | [Power Sensor](https://steamcommunity.com/sharedfiles/filedetails/?id=1733126901) | B |  |

### Batch 24: ranks 1417–1447

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1417 | [超大的食物BigFood](https://steamcommunity.com/sharedfiles/filedetails/?id=2885701792) | P |  |
| 1419 | [Dupery Addon - Echo and the Chaos Crew](https://steamcommunity.com/sharedfiles/filedetails/?id=3015603486) | X | needs 3172734005, noCode |
| 1421 | [Other Cluster](https://steamcommunity.com/sharedfiles/filedetails/?id=2879715930) | X | worldgen, noCode |
| 1422 | [导热管桥](https://steamcommunity.com/sharedfiles/filedetails/?id=3015929280) | B |  |
| 1423 | [无限调谐](https://steamcommunity.com/sharedfiles/filedetails/?id=3536161381) | S | UI |
| 1424 | [Delivery Temperature Limit (Fixed)](https://steamcommunity.com/sharedfiles/filedetails/?id=3479021027) | S,O | PLib options, UI |
| 1426 | [SuperShineBug](https://steamcommunity.com/sharedfiles/filedetails/?id=2141411697) | P |  |
| 1428 | [石油裂解器 Petroleum cracker](https://steamcommunity.com/sharedfiles/filedetails/?id=2897919789) | B |  |
| 1430 | [低温高压制氧](https://steamcommunity.com/sharedfiles/filedetails/?id=3002566087) | P,O | PLib options |
| 1432 | [Truthful Thermal Conductivity](https://steamcommunity.com/sharedfiles/filedetails/?id=3022436400) | P | UI |
| 1434 | [迷你净水器纯净水版](https://steamcommunity.com/sharedfiles/filedetails/?id=2999464166) | B |  |
| 1435 | [Hide air vents隐藏排气口](https://steamcommunity.com/sharedfiles/filedetails/?id=2872005064) | P |  |
| 1436 | [RadiationFarmTile 辐射种植砖](https://steamcommunity.com/sharedfiles/filedetails/?id=3580813956) | P,O | PLib options |
| 1437 | [Single Dupe Start](https://steamcommunity.com/sharedfiles/filedetails/?id=2369401506) | S | UI |
| 1440 | [Fertilizing Symbiosis](https://steamcommunity.com/sharedfiles/filedetails/?id=3760466471) | P |  |
| 1441 | [Supplying Exclusions](https://steamcommunity.com/sharedfiles/filedetails/?id=2781529336) | P,O | PLib options |
| 1442 | [Blood](https://steamcommunity.com/sharedfiles/filedetails/?id=1725334376) | V | elements |
| 1443 | [(DLC) Better Rocketry](https://steamcommunity.com/sharedfiles/filedetails/?id=2598059646) | P,O | PLib options |
| 1444 | [Enhanced rocket engine增强火箭引擎](https://steamcommunity.com/sharedfiles/filedetails/?id=2858701369) | P,O | PLib options |
| 1445 | [Pure Rust](https://steamcommunity.com/sharedfiles/filedetails/?id=2596397252) | P | worldgen |
| 1446 | [Mouse Edge Scroll](https://steamcommunity.com/sharedfiles/filedetails/?id=2744013381) | V |  |
| 1447 | [Midnight Sun (Eternal Daylight)](https://steamcommunity.com/sharedfiles/filedetails/?id=2278463564) | V |  |

### Batch 25: ranks 1448–1476

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1448 | [Frozen Geysers](https://steamcommunity.com/sharedfiles/filedetails/?id=3292573924) | P |  |
| 1451 | [Faster Spacecraft](https://steamcommunity.com/sharedfiles/filedetails/?id=2075672160) | T,O | PLib options |
| 1452 | [超级蒸汽机](https://steamcommunity.com/sharedfiles/filedetails/?id=3012838836) | P,O | PLib options |
| 1453 | [低新鲜度食物优先 · Stale Food First](https://steamcommunity.com/sharedfiles/filedetails/?id=3815691075) | P |  |
| 1454 | [高效钻头前锥 Efficient drill bit front cone 1.1](https://steamcommunity.com/sharedfiles/filedetails/?id=2979315574) | P,O | PLib options |
| 1455 | [No Free Idle Chore 删除太空服空闲任务](https://steamcommunity.com/sharedfiles/filedetails/?id=3080879491) | P |  |
| 1457 | [Controlled Crashes](https://steamcommunity.com/sharedfiles/filedetails/?id=3658365488) | S | UI |
| 1459 | [Nuclear waste disposal核废料处理](https://steamcommunity.com/sharedfiles/filedetails/?id=2877166424) | B,O | PLib options |
| 1460 | [Submersible Stations](https://steamcommunity.com/sharedfiles/filedetails/?id=3748029158) | P |  |
| 1462 | [Debris Thermostat](https://steamcommunity.com/sharedfiles/filedetails/?id=3253994678) | B,O | PLib options |
| 1463 | [Teleporter Transmitter Buildable 复制人传送器可建造](https://steamcommunity.com/sharedfiles/filedetails/?id=2871615937) | B |  |
| 1464 | [Bigger Capacity Fixed 容量调整修复](https://steamcommunity.com/sharedfiles/filedetails/?id=3799841474) | P,O | PLib options |
| 1466 | [No Notification Sounds](https://steamcommunity.com/sharedfiles/filedetails/?id=1882035088) | S,O | PLib options, UI |
| 1467 | [More Clothing](https://steamcommunity.com/sharedfiles/filedetails/?id=3765092134) | B | user menu |
| 1469 | [农业砖无需固态肥料](https://steamcommunity.com/sharedfiles/filedetails/?id=2996623294) | P |  |
| 1470 | [Starmap Shenanigans](https://steamcommunity.com/sharedfiles/filedetails/?id=3442937887) | T,S | UI |
| 1471 | [VaccumFreezer](https://steamcommunity.com/sharedfiles/filedetails/?id=3635020106) | B |  |
| 1472 | [(deprecated) Checkpoint Automation](https://steamcommunity.com/sharedfiles/filedetails/?id=2927019052) | P |  |
| 1473 | [Auto Follow Cam](https://steamcommunity.com/sharedfiles/filedetails/?id=2763419886) | V |  |
| 1474 | [Switch with fuse](https://steamcommunity.com/sharedfiles/filedetails/?id=3161746013) | P | user menu |
| 1475 | [(Deprecated) No Splash Screen](https://steamcommunity.com/sharedfiles/filedetails/?id=2361698345) | P |  |
| 1476 | [Schedule Sensor](https://steamcommunity.com/sharedfiles/filedetails/?id=2126597863) | B,S | UI, needs 2018291283 |

### Batch 26: ranks 1477–1502

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1477 | [remove mutant min radiation](https://steamcommunity.com/sharedfiles/filedetails/?id=2479453490) | P |  |
| 1479 | [刷新选人久霜版（Refreshpicks）](https://steamcommunity.com/sharedfiles/filedetails/?id=3802049911) | S | UI, base game only |
| 1480 | [迷你液泵 Mini Liquid Pump](https://steamcommunity.com/sharedfiles/filedetails/?id=2931131695) | B |  |
| 1481 | [LongReach: Advanced Duplicant Reach Extension](https://steamcommunity.com/sharedfiles/filedetails/?id=3689543178) | P,O | PLib options |
| 1482 | [Emptiable Rocketry](https://steamcommunity.com/sharedfiles/filedetails/?id=2688793227) | P |  |
| 1484 | [(OBSOLETE) Radioactive Wheezewort (Visual) (DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=2933501288) | X | noCode |
| 1485 | [No Dormancy](https://steamcommunity.com/sharedfiles/filedetails/?id=3064859946) | P |  |
| 1486 | [Fast Asleep](https://steamcommunity.com/sharedfiles/filedetails/?id=2553394896) | P |  |
| 1487 | [Pips can plant anywhere](https://steamcommunity.com/sharedfiles/filedetails/?id=2620167879) | P |  |
| 1488 | [Liquid Bottler rework](https://steamcommunity.com/sharedfiles/filedetails/?id=3561927851) | B,S,O | PLib options, UI, user menu |
| 1489 | [Automation Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=2661900022) | B,S | UI |
| 1490 | [工程梯 ladder](https://steamcommunity.com/sharedfiles/filedetails/?id=2870764515) | B |  |
| 1491 | [大容量材料研究终端](https://steamcommunity.com/sharedfiles/filedetails/?id=3749047120) | P |  |
| 1492 | [墙壁马桶抽水马桶污水产量修改](https://steamcommunity.com/sharedfiles/filedetails/?id=2984782124) | P,O | PLib options |
| 1494 | [Idle Notification Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=2943021953) | S,O | PLib options, UI, user menu |
| 1495 | [汉化补丁](https://steamcommunity.com/sharedfiles/filedetails/?id=3802147067) | P |  |
| 1496 | [HOEC 电力消耗减半 Half of electricity consumption](https://steamcommunity.com/sharedfiles/filedetails/?id=3420943389) | P |  |
| 1497 | [LiquefiableSculptures](https://steamcommunity.com/sharedfiles/filedetails/?id=1741146856) | P |  |
| 1499 | [超级小人（Superduplicant）](https://steamcommunity.com/sharedfiles/filedetails/?id=3804723854) | S | UI, base game only |
| 1500 | [Sensor Range to 99999Kg(Vanilla+DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=2018469750) | P |  |
| 1501 | [Super flow control超级流量控制](https://steamcommunity.com/sharedfiles/filedetails/?id=2872811575) | P |  |
| 1502 | [Extended Wires Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3490800912) | B |  |

### Batch 27: ranks 1504–1531

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1504 | [Crematorium](https://steamcommunity.com/sharedfiles/filedetails/?id=2778228823) | P |  |
| 1505 | [Weeb Pacu](https://steamcommunity.com/sharedfiles/filedetails/?id=2896083956) | P |  |
| 1506 | [机械小动物](https://steamcommunity.com/sharedfiles/filedetails/?id=3598090645) | B |  |
| 1508 | [Sandbox Default Setting](https://steamcommunity.com/sharedfiles/filedetails/?id=2846982791) | P |  |
| 1509 | [Allow Edibles In Storage Dispenser](https://steamcommunity.com/sharedfiles/filedetails/?id=2839845786) | P |  |
| 1511 | [Disease killer疾病杀手](https://steamcommunity.com/sharedfiles/filedetails/?id=2846672855) | P |  |
| 1512 | [强化制冰机Intensified ice maker](https://steamcommunity.com/sharedfiles/filedetails/?id=2900831873) | P |  |
| 1514 | [Super Smart Storage BOX](https://steamcommunity.com/sharedfiles/filedetails/?id=3133795636) | S | UI, user menu |
| 1515 | [Arknights Operators / 明日方舟干员 (0.3.3)](https://steamcommunity.com/sharedfiles/filedetails/?id=3765340857) | S,K,O,V | PLib options, UI |
| 1516 | [genshin food](https://steamcommunity.com/sharedfiles/filedetails/?id=3587387898) | P |  |
| 1517 | [Klei Stream Painting Skins](https://steamcommunity.com/sharedfiles/filedetails/?id=3819173399) | P |  |
| 1519 | [Oxygen Dreckos](https://steamcommunity.com/sharedfiles/filedetails/?id=3519438603) | P |  |
| 1521 | [Flydo uses Transit Tubes](https://steamcommunity.com/sharedfiles/filedetails/?id=3709304542) | P |  |
| 1522 | [Clean Slate Dupes](https://steamcommunity.com/sharedfiles/filedetails/?id=2755150556) | S,O | PLib options, UI |
| 1523 | [果虫也疯狂！Crazey Insects!Custom configuration settings are now supported!](https://steamcommunity.com/sharedfiles/filedetails/?id=3463668759) | P,O | PLib options |
| 1524 | [Very Gassy Moos](https://steamcommunity.com/sharedfiles/filedetails/?id=2662113602) | P,O | PLib options |
| 1525 | [(繁/簡/EN) 元素修改器 / Element Editor (God Mode Tool)](https://steamcommunity.com/sharedfiles/filedetails/?id=3639188007) | T,S,K,V | UI |
| 1526 | [I 强化制冰机 Enhanced Ice Maker](https://steamcommunity.com/sharedfiles/filedetails/?id=3037633290) | P |  |
| 1527 | [Independent Pufts (Hot Shots)](https://steamcommunity.com/sharedfiles/filedetails/?id=1955246030) | P |  |
| 1529 | [The FoodOverhaul Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3747771996) | S | UI |
| 1530 | [Controlled Power](https://steamcommunity.com/sharedfiles/filedetails/?id=3673526385) | B,S | UI |
| 1531 | [Zoned Auto Sweeper](https://steamcommunity.com/sharedfiles/filedetails/?id=3745253371) | T,S,K,V | UI, user menu |

### Batch 28: ranks 1532–1561

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1532 | [Wild Harvest](https://steamcommunity.com/sharedfiles/filedetails/?id=2594664745) | P |  |
| 1533 | [Excavation Yield Configurable](https://steamcommunity.com/sharedfiles/filedetails/?id=3686522238) | P,O | PLib options |
| 1534 | [缺氧属性修改(OxygenNotIncluded Attribute Modification)](https://steamcommunity.com/sharedfiles/filedetails/?id=3558002322) | P |  |
| 1535 | [Building-Super Petroleum Generator 超级石油发电机](https://steamcommunity.com/sharedfiles/filedetails/?id=3380824584) | P,O | PLib options |
| 1537 | [M 金属精炼器自动化Metal Refinery Automation 1.0](https://steamcommunity.com/sharedfiles/filedetails/?id=3598373160) | P |  |
| 1538 | [可运输的气体和液体](https://steamcommunity.com/sharedfiles/filedetails/?id=3554578231) | P | base game only |
| 1540 | [BuildableWildPlant](https://steamcommunity.com/sharedfiles/filedetails/?id=3664817564) | B,S | UI, user menu |
| 1541 | [Quantum Compressors](https://steamcommunity.com/sharedfiles/filedetails/?id=2368685352) | B,S | UI |
| 1544 | [Infinite Sources/Sinks – Fixed](https://steamcommunity.com/sharedfiles/filedetails/?id=3669083158) | B,S | UI |
| 1546 | [Research Included](https://steamcommunity.com/sharedfiles/filedetails/?id=3315374510) | P |  |
| 1547 | [种子变异 Seed Mutate](https://steamcommunity.com/sharedfiles/filedetails/?id=3600136029) | S,O | PLib options, UI, user menu |
| 1548 | [Quebec Pull And Radiation Collector魁拔之辐射收集器](https://steamcommunity.com/sharedfiles/filedetails/?id=2852156682) | P,O | PLib options |
| 1549 | [Plants have died - Where ?](https://steamcommunity.com/sharedfiles/filedetails/?id=3577468014) | P |  |
| 1550 | [Bubbling Chest](https://steamcommunity.com/sharedfiles/filedetails/?id=3752112457) | B | worldgen |
| 1551 | [Moonlet Cluster - Superconductive](https://steamcommunity.com/sharedfiles/filedetails/?id=3418019940) | P | worldgen |
| 1552 | [yo电线mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3344136862) | P,O | PLib options |
| 1553 | [U&F-Precise Control Storage 精准控制存储](https://steamcommunity.com/sharedfiles/filedetails/?id=3381590308) | S,K | UI, user menu |
| 1557 | [Hydrogenerator](https://steamcommunity.com/sharedfiles/filedetails/?id=3761372593) | B |  |
| 1558 | [背景灯Backlight -K41](https://steamcommunity.com/sharedfiles/filedetails/?id=2903353773) | B |  |
| 1559 | [Liquid Reservoir Boost储液库增强](https://steamcommunity.com/sharedfiles/filedetails/?id=2959805230) | P |  |
| 1560 | [Bigger RocketHabitatMod 更大的太空舱](https://steamcommunity.com/sharedfiles/filedetails/?id=3665333463) | P |  |
| 1561 | [Cook With Heat](https://steamcommunity.com/sharedfiles/filedetails/?id=1878289465) | P |  |

### Batch 29: ranks 1562–1586

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1562 | [碳氢聚合机(Hydrocarbon polymerization machine)](https://steamcommunity.com/sharedfiles/filedetails/?id=3091810586) | B |  |
| 1563 | [Mementos!  Recognize Individual Dupe Achievements](https://steamcommunity.com/sharedfiles/filedetails/?id=3552320454) | S,K | UI |
| 1564 | [Errand Notifier](https://steamcommunity.com/sharedfiles/filedetails/?id=3479125383) | T,S,K,O,V | PLib options, UI, needs 2018291283 |
| 1565 | [自动精炼设备 (Auto Refinery )](https://steamcommunity.com/sharedfiles/filedetails/?id=3736804781) | P,O | PLib options |
| 1566 | [Better Rad Pill Fixed](https://steamcommunity.com/sharedfiles/filedetails/?id=3175775975) | P,O | PLib options |
| 1568 | [No Planter Box Decor Penalty](https://steamcommunity.com/sharedfiles/filedetails/?id=2031107595) | P |  |
| 1569 | [Worldgen Designer (updated)](https://steamcommunity.com/sharedfiles/filedetails/?id=3807584811) | S,K,V,M | UI, worldgen |
| 1570 | [喷泉改造 / Geyser Conversion](https://steamcommunity.com/sharedfiles/filedetails/?id=3777868198) | T,S | UI, user menu |
| 1571 | [Kinda Useful Sulfur](https://steamcommunity.com/sharedfiles/filedetails/?id=2147011067) | P |  |
| 1572 | [Building-Super Methane Generator 超级天然气发电机](https://steamcommunity.com/sharedfiles/filedetails/?id=3377022279) | P,O | PLib options |
| 1573 | [热裂解甲烷制钻石](https://steamcommunity.com/sharedfiles/filedetails/?id=3002713481) | B,O | PLib options, needs 2933355725 |
| 1574 | [Overlay Values On Grid](https://steamcommunity.com/sharedfiles/filedetails/?id=3786116196) | V | UI |
| 1576 | [Custom Print Timer](https://steamcommunity.com/sharedfiles/filedetails/?id=2977493575) | M | UI, worldgen |
| 1577 | [100% Space Exposure Challenge](https://steamcommunity.com/sharedfiles/filedetails/?id=3426499384) | S | UI, worldgen |
| 1579 | [Medicine Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3208353436) | P |  |
| 1580 | [Bionics Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3798248283) | B,S | UI |
| 1581 | [Show Liquid on Airflow Tiles](https://steamcommunity.com/sharedfiles/filedetails/?id=3163125334) | P,O | PLib options |
| 1582 | [Easy planting 轻松种植](https://steamcommunity.com/sharedfiles/filedetails/?id=3001135560) | P,O | PLib options |
| 1583 | [Ranch station floodable](https://steamcommunity.com/sharedfiles/filedetails/?id=3743125812) | P |  |
| 1584 | [火箭舱外空间64x64_墙窗可拆可替换](https://steamcommunity.com/sharedfiles/filedetails/?id=3308085465) | P |  |
| 1585 | [More Tempshift Plates](https://steamcommunity.com/sharedfiles/filedetails/?id=3304951452) | B |  |
| 1586 | [机械小动物（重置版）](https://steamcommunity.com/sharedfiles/filedetails/?id=3623018439) | B,O | PLib options |

### Batch 30: ranks 1587–1612

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1587 | [Slim Transit Tube Access](https://steamcommunity.com/sharedfiles/filedetails/?id=3528100424) | B | user menu, nativeCalls |
| 1589 | [小动物野化器（Critter Undomesticator）](https://steamcommunity.com/sharedfiles/filedetails/?id=3286948775) | B |  |
| 1590 | [Dung To Fertilizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3434789737) | P | base game only |
| 1592 | [Booster Animation Begone!](https://steamcommunity.com/sharedfiles/filedetails/?id=3438530909) | P |  |
| 1593 | [Reworked Incubator](https://steamcommunity.com/sharedfiles/filedetails/?id=3013130259) | P,O | PLib options |
| 1594 | [(Vanilla + DLC) Rocket Presence Port](https://steamcommunity.com/sharedfiles/filedetails/?id=2339405225) | P |  |
| 1596 | [Flight planning](https://steamcommunity.com/sharedfiles/filedetails/?id=2813526647) | P |  |
| 1598 | [Auto-Exit-Neural-Vacillators](https://steamcommunity.com/sharedfiles/filedetails/?id=3674542335) | P |  |
| 1599 | [安全门Emergency Door](https://steamcommunity.com/sharedfiles/filedetails/?id=2932976861) | B |  |
| 1600 | [Vertical liquid pump垂直液泵](https://steamcommunity.com/sharedfiles/filedetails/?id=2845508297) | B,O | PLib options |
| 1601 | [No Conflicting Traits](https://steamcommunity.com/sharedfiles/filedetails/?id=3744164922) | P |  |
| 1602 | [造景灯 Landscape lamp](https://steamcommunity.com/sharedfiles/filedetails/?id=2928148711) | B |  |
| 1603 | [自动奶茶机V2（小吃豆&百合奶茶机）](https://steamcommunity.com/sharedfiles/filedetails/?id=3137470934) | B |  |
| 1604 | [Mars](https://steamcommunity.com/sharedfiles/filedetails/?id=3038061222) | P | worldgen |
| 1605 | [排液/排气管桥](https://steamcommunity.com/sharedfiles/filedetails/?id=2807545527) | B |  |
| 1606 | [Pipe Sterilisers](https://steamcommunity.com/sharedfiles/filedetails/?id=3733622245) | B | base game only |
| 1607 | [Skills Retainer](https://steamcommunity.com/sharedfiles/filedetails/?id=3130228843) | P |  |
| 1608 | [Ultra Coolant](https://steamcommunity.com/sharedfiles/filedetails/?id=2938461528) | V | elements |
| 1609 | [Tweaked Lights - Lights Out Fork](https://steamcommunity.com/sharedfiles/filedetails/?id=1862419428) | P |  |
| 1610 | [氢气扩散器（Hydrogen diffuser）](https://steamcommunity.com/sharedfiles/filedetails/?id=3269820224) | B |  |
| 1611 | [Pretty Wall 精致墙壁](https://steamcommunity.com/sharedfiles/filedetails/?id=2844082940) | P |  |
| 1612 | [Crematorium V2.0](https://steamcommunity.com/sharedfiles/filedetails/?id=2830365640) | P |  |

### Batch 31: ranks 1614–1638

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1614 | [Storage Tooltip](https://steamcommunity.com/sharedfiles/filedetails/?id=3637756953) | P |  |
| 1615 | [Transformer load increase](https://steamcommunity.com/sharedfiles/filedetails/?id=3322961363) | P |  |
| 1616 | [Better Electrolyzer (200kg)](https://steamcommunity.com/sharedfiles/filedetails/?id=2841507339) | P |  |
| 1617 | [Natural Backwalls Everywhere](https://steamcommunity.com/sharedfiles/filedetails/?id=3816744732) | P,O | PLib options, UI, worldgen |
| 1618 | [High-Temperature Cooking 高温烹饪](https://steamcommunity.com/sharedfiles/filedetails/?id=3244175954) | P |  |
| 1619 | [High Temperature Liquid Pump](https://steamcommunity.com/sharedfiles/filedetails/?id=3684473759) | B |  |
| 1620 | [Storage Refrigerator Thresholds](https://steamcommunity.com/sharedfiles/filedetails/?id=2959340381) | S,O | PLib options, UI |
| 1621 | [Corpses on Pedestals](https://steamcommunity.com/sharedfiles/filedetails/?id=3240895274) | S | UI |
| 1622 | [燃气灶排放氧气](https://steamcommunity.com/sharedfiles/filedetails/?id=3349984754) | P | base game only |
| 1623 | [石油油井](https://steamcommunity.com/sharedfiles/filedetails/?id=2897349471) | P |  |
| 1624 | [Controlled Extraction](https://steamcommunity.com/sharedfiles/filedetails/?id=3648996222) | S,O | PLib options, UI |
| 1625 | [电线相关修改(可配置)](https://steamcommunity.com/sharedfiles/filedetails/?id=3606733579) | P,O | PLib options |
| 1626 | [Building-Super Metal Refinery 超级金属精炼器](https://steamcommunity.com/sharedfiles/filedetails/?id=3480374510) | P,O | PLib options |
| 1627 | [Egg/Seed Replicator](https://steamcommunity.com/sharedfiles/filedetails/?id=3168592570) | B |  |
| 1628 | [植物施肥(Plants Fertilizer Set)](https://steamcommunity.com/sharedfiles/filedetails/?id=2366126698) | P |  |
| 1629 | [细电线 Thin Wire](https://steamcommunity.com/sharedfiles/filedetails/?id=2933355725) | B |  |
| 1631 | [Meep](https://steamcommunity.com/sharedfiles/filedetails/?id=2062103370) | P |  |
| 1633 | [Modified Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=1900617368) | B |  |
| 1634 | [Better Oxyferns](https://steamcommunity.com/sharedfiles/filedetails/?id=3807698557) | P |  |
| 1635 | [DupeTrek: Eras](https://steamcommunity.com/sharedfiles/filedetails/?id=3389100014) | X | needs 3172734005, noCode |
| 1637 | [No Empty Conveyors](https://steamcommunity.com/sharedfiles/filedetails/?id=2588729110) | P |  |
| 1638 | [Condenser](https://steamcommunity.com/sharedfiles/filedetails/?id=1848839827) | B |  |

### Batch 32: ranks 1640–1666

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1640 | [Insulated Farm Tiles (Fixed)](https://steamcommunity.com/sharedfiles/filedetails/?id=3768907966) | B |  |
| 1641 | [U&F-Liquid Lock Builder锁液器](https://steamcommunity.com/sharedfiles/filedetails/?id=3466461752) | B |  |
| 1642 | [High Precision Temperature](https://steamcommunity.com/sharedfiles/filedetails/?id=3022469420) | P |  |
| 1643 | [星舰修复版（starship）](https://steamcommunity.com/sharedfiles/filedetails/?id=3789181880) | B,S,O,V | PLib options, UI, worldgen |
| 1644 | [Spiced Food Filter](https://steamcommunity.com/sharedfiles/filedetails/?id=2893409616) | S | UI |
| 1645 | [淤泥机fixed！Silt Machine！](https://steamcommunity.com/sharedfiles/filedetails/?id=3468457710) | B |  |
| 1646 | [ONI Multiplayer (Co-op)](https://steamcommunity.com/sharedfiles/filedetails/?id=3807965885) | S,K,V | UI, user menu |
| 1647 | [Carbon Skimmer - Realistic](https://steamcommunity.com/sharedfiles/filedetails/?id=2845278436) | P |  |
| 1649 | [Omni Flora: Decorative Plants (Reloaded) (Test)](https://steamcommunity.com/sharedfiles/filedetails/?id=3534405622) | O,M | PLib options, worldgen, base game only |
| 1650 | [No Enough Storage](https://steamcommunity.com/sharedfiles/filedetails/?id=3767691401) | B,S | UI |
| 1651 | [Soups](https://steamcommunity.com/sharedfiles/filedetails/?id=3624622229) | P |  |
| 1652 | [活得久的海牛Longevity Moo -K11](https://steamcommunity.com/sharedfiles/filedetails/?id=3067514896) | P |  |
| 1653 | [Coal World Trait](https://steamcommunity.com/sharedfiles/filedetails/?id=3473481199) | O,M | PLib options, worldgen |
| 1655 | [狐鹿-木材强化 More Wood](https://steamcommunity.com/sharedfiles/filedetails/?id=3464293843) | P,O | PLib options |
| 1657 | [燃气灶魔改](https://steamcommunity.com/sharedfiles/filedetails/?id=2984769656) | B |  |
| 1658 | [Enhanced Radiation Engine增强辐射粒子发动机](https://steamcommunity.com/sharedfiles/filedetails/?id=2846006297) | P,O | PLib options |
| 1660 | [Only Tame](https://steamcommunity.com/sharedfiles/filedetails/?id=3159785377) | S,O | PLib options, UI |
| 1661 | [更多食物（盛宴版本更新）](https://steamcommunity.com/sharedfiles/filedetails/?id=3794321811) | S | UI, worldgen |
| 1662 | [食物保鲜逃课模块](https://steamcommunity.com/sharedfiles/filedetails/?id=3226787971) | B |  |
| 1664 | [Orehull harvests Plants](https://steamcommunity.com/sharedfiles/filedetails/?id=3747871003) | P |  |
| 1665 | [SaltBox 盐盒](https://steamcommunity.com/sharedfiles/filedetails/?id=3139285721) | B |  |
| 1666 | [Hatch diet changes](https://steamcommunity.com/sharedfiles/filedetails/?id=2658738587) | P |  |

### Batch 33: ranks 1667–1696

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1667 | [Super Dimensional Miner超次元矿机](https://steamcommunity.com/sharedfiles/filedetails/?id=2850723145) | P |  |
| 1669 | [Capacity Control](https://steamcommunity.com/sharedfiles/filedetails/?id=3635863743) | S,O | PLib options, UI |
| 1671 | [Twice Mutate 双重变异](https://steamcommunity.com/sharedfiles/filedetails/?id=2874123092) | P |  |
| 1672 | [Sight Not Included Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=2747526061) | P,O | PLib options |
| 1673 | [Achievements Tracker](https://steamcommunity.com/sharedfiles/filedetails/?id=3656635556) | S,K | UI |
| 1674 | [Controlled Mods](https://steamcommunity.com/sharedfiles/filedetails/?id=3640737967) | B,S,K,O,V,M | PLib options, UI |
| 1675 | [Instant Research Fixed](https://steamcommunity.com/sharedfiles/filedetails/?id=3034422919) | P,O | PLib options |
| 1676 | [ModTools](https://steamcommunity.com/sharedfiles/filedetails/?id=3241644477) | S | UI |
| 1677 | [Lumos Maxima](https://steamcommunity.com/sharedfiles/filedetails/?id=1793933820) | P |  |
| 1678 | [Advanced Storage(V4)](https://steamcommunity.com/sharedfiles/filedetails/?id=3332098282) | B,S | UI |
| 1679 | [Mass Sensor](https://steamcommunity.com/sharedfiles/filedetails/?id=2768128560) | B,S,O | PLib options, UI |
| 1680 | [除湿机Steam absorber](https://steamcommunity.com/sharedfiles/filedetails/?id=3012182492) | B |  |
| 1681 | [Super bigger Storage / 超大容量储物](https://steamcommunity.com/sharedfiles/filedetails/?id=3772304726) | P |  |
| 1682 | [Overheat Control](https://steamcommunity.com/sharedfiles/filedetails/?id=3519929015) | S,O | PLib options, UI |
| 1684 | [超级增幅块](https://steamcommunity.com/sharedfiles/filedetails/?id=3527264251) | P,O | PLib options |
| 1685 | [Rebalanced Skills](https://steamcommunity.com/sharedfiles/filedetails/?id=2687208049) | P |  |
| 1687 | [Reservoir Resources Counted](https://steamcommunity.com/sharedfiles/filedetails/?id=3770883011) | P |  |
| 1689 | [Named Transformers](https://steamcommunity.com/sharedfiles/filedetails/?id=2009051942) | P |  |
| 1690 | [Split World Trait](https://steamcommunity.com/sharedfiles/filedetails/?id=3726131088) | P | worldgen |
| 1691 | [4kw导线(4kw Wire)](https://steamcommunity.com/sharedfiles/filedetails/?id=3610828533) | P |  |
| 1692 | [Reveal everything! 显示全星图~](https://steamcommunity.com/sharedfiles/filedetails/?id=3695988693) | T,S,K,O,V | PLib options, UI, worldgen |
| 1696 | [MultiTemps](https://steamcommunity.com/sharedfiles/filedetails/?id=3506650482) | S | UI |

### Batch 34: ranks 1698–1722

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1698 | [D-FlipFlop](https://steamcommunity.com/sharedfiles/filedetails/?id=1956033615) | B |  |
| 1700 | [Outdated - Renewable Abyssalite (Spaced Out!)](https://steamcommunity.com/sharedfiles/filedetails/?id=2990535748) | P |  |
| 1701 | [魔改原油精炼器](https://steamcommunity.com/sharedfiles/filedetails/?id=2984783275) | B |  |
| 1702 | [Minecraft chest skin](https://steamcommunity.com/sharedfiles/filedetails/?id=3481545178) | P |  |
| 1703 | [Dupe Picker](https://steamcommunity.com/sharedfiles/filedetails/?id=2901544995) | S | UI, base game only |
| 1704 | [菌泥啵啵奶茶（生活质量类）](https://steamcommunity.com/sharedfiles/filedetails/?id=3136742398) | B |  |
| 1705 | [Furioza](https://steamcommunity.com/sharedfiles/filedetails/?id=3016468268) | P | worldgen |
| 1706 | [Signal bridge信号桥](https://steamcommunity.com/sharedfiles/filedetails/?id=2878495368) | B |  |
| 1707 | [快速抽真空（Easy Vacuum）](https://steamcommunity.com/sharedfiles/filedetails/?id=3744428492) | T,K |  |
| 1708 | [Aku Monument](https://steamcommunity.com/sharedfiles/filedetails/?id=2573950398) | P |  |
| 1709 | [温度调节器](https://steamcommunity.com/sharedfiles/filedetails/?id=3229764220) | P |  |
| 1710 | [AdvancedWaterPurifier高级净水器](https://steamcommunity.com/sharedfiles/filedetails/?id=3733715767) | B,O | PLib options, user menu |
| 1711 | [Geyser - EthanolGas Vent 乙醇蒸气喷孔](https://steamcommunity.com/sharedfiles/filedetails/?id=3469200379) | P | worldgen |
| 1712 | [Pip Licks Butt](https://steamcommunity.com/sharedfiles/filedetails/?id=3802090892) | P |  |
| 1713 | [Auto Priority Sorter / 自动优先级排序](https://steamcommunity.com/sharedfiles/filedetails/?id=3785020293) | S | UI |
| 1714 | [超级计算机](https://steamcommunity.com/sharedfiles/filedetails/?id=3257388693) | P |  |
| 1716 | [Grind Up Metals Updated](https://steamcommunity.com/sharedfiles/filedetails/?id=2778875391) | P |  |
| 1717 | [System Clock Remake](https://steamcommunity.com/sharedfiles/filedetails/?id=2546671384) | S,O | PLib options, UI |
| 1719 | [Interstellar Remote Workers](https://steamcommunity.com/sharedfiles/filedetails/?id=3438586580) | S | UI |
| 1720 | [皇帝的新砖V3.0 (Transparent Tile)](https://steamcommunity.com/sharedfiles/filedetails/?id=3138268148) | B |  |
| 1721 | [Build Hot Buildings](https://steamcommunity.com/sharedfiles/filedetails/?id=2964396728) | P |  |
| 1722 | [农民的呵护效果5倍时长 Farmers Touch Duration 5x](https://steamcommunity.com/sharedfiles/filedetails/?id=3771254169) | P |  |

### Batch 35: ranks 1723–1752

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1723 | [氢气转化天然气Hydrogen to natural gas conversion](https://steamcommunity.com/sharedfiles/filedetails/?id=3326254533) | B |  |
| 1725 | [Deconstruct Oxygen Masks](https://steamcommunity.com/sharedfiles/filedetails/?id=3514600142) | K,O | PLib options, user menu |
| 1729 | [SuperBattery](https://steamcommunity.com/sharedfiles/filedetails/?id=3012530445) | P |  |
| 1730 | [Utility Layers](https://steamcommunity.com/sharedfiles/filedetails/?id=3789151799) | B,T,S,K,V,M | UI, user menu |
| 1731 | [Crop Sensor](https://steamcommunity.com/sharedfiles/filedetails/?id=3341550483) | B |  |
| 1732 | [Ethanol Fermenter](https://steamcommunity.com/sharedfiles/filedetails/?id=2787109631) | B |  |
| 1733 | [EggCracker Signal](https://steamcommunity.com/sharedfiles/filedetails/?id=3075494652) | P |  |
| 1734 | [AllInSmall-Asteroid](https://steamcommunity.com/sharedfiles/filedetails/?id=3044677825) | P | worldgen |
| 1735 | [Better Isoresin Tree](https://steamcommunity.com/sharedfiles/filedetails/?id=2467498655) | P,O | PLib options |
| 1736 | [Lazy ranching](https://steamcommunity.com/sharedfiles/filedetails/?id=3514022158) | P |  |
| 1738 | [Base Overheat Temperature Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=2645894652) | P |  |
| 1739 | [Breathing Station Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3753632924) | P,O | PLib options |
| 1740 | [Fusion Energy Project](https://steamcommunity.com/sharedfiles/filedetails/?id=3546375849) | B,S | UI |
| 1741 | [Rocket Ladder is Included](https://steamcommunity.com/sharedfiles/filedetails/?id=3667413507) | P | user menu |
| 1742 | [火箭相关](https://steamcommunity.com/sharedfiles/filedetails/?id=2984798228) | P |  |
| 1743 | [Auto-Teleport](https://steamcommunity.com/sharedfiles/filedetails/?id=3674429822) | P |  |
| 1744 | [Gas Conduction Panel 气体导热板](https://steamcommunity.com/sharedfiles/filedetails/?id=3005077708) | B |  |
| 1745 | [AnimalFarm](https://steamcommunity.com/sharedfiles/filedetails/?id=3664816241) | B,S | UI |
| 1746 | [Customisable Plug Slugs](https://steamcommunity.com/sharedfiles/filedetails/?id=2482057864) | P,O | PLib options, needs 2018291283 |
| 1750 | [资源计算器](https://steamcommunity.com/sharedfiles/filedetails/?id=3772549326) | K | UI |
| 1751 | [Geotuner Less Limited](https://steamcommunity.com/sharedfiles/filedetails/?id=2907583139) | S | UI |
| 1752 | [Arabic Lang Fix - مصحح اللغة العربية](https://steamcommunity.com/sharedfiles/filedetails/?id=3482358273) | P | UI |

### Batch 36: ranks 1753–1784

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1753 | [(DLC) Ethanol as Rocket Fuel](https://steamcommunity.com/sharedfiles/filedetails/?id=2664006534) | P |  |
| 1754 | [Super Incubator](https://steamcommunity.com/sharedfiles/filedetails/?id=3738526308) | B,M | UI |
| 1755 | [Multiplayer Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3728274068) | T,S,K,V,M | UI, base game only |
| 1757 | [DirtCycle](https://steamcommunity.com/sharedfiles/filedetails/?id=3589054256) | P |  |
| 1758 | [Choose an Ingredient](https://steamcommunity.com/sharedfiles/filedetails/?id=3744010266) | P,O | PLib options |
| 1759 | [LauncherPlus: An On-Demand Supply Network](https://steamcommunity.com/sharedfiles/filedetails/?id=3577962693) | S,K,O | PLib options, UI |
| 1765 | [Cold Crash](https://steamcommunity.com/sharedfiles/filedetails/?id=3025430728) | P | worldgen |
| 1767 | [AutoSweeper with Temperature Filter](https://steamcommunity.com/sharedfiles/filedetails/?id=3474203860) | B,S,K | UI |
| 1768 | [No Radiation Exposure Animation](https://steamcommunity.com/sharedfiles/filedetails/?id=2843427720) | P |  |
| 1769 | [【2025FIXED】Always Joy Behaviour 永久开心状态](https://steamcommunity.com/sharedfiles/filedetails/?id=2543933197) | P |  |
| 1770 | [HeatMap](https://steamcommunity.com/sharedfiles/filedetails/?id=2563149160) | S,V | UI |
| 1771 | [CalYu's Building Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=2995808112) | B | mayCrashInPlay |
| 1772 | [Strata Lift](https://steamcommunity.com/sharedfiles/filedetails/?id=3806581240) | B,S,V | UI |
| 1773 | [Multiple Element Sensor (DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=3131826185) | B |  |
| 1774 | [Empty The Storage (Errand)](https://steamcommunity.com/sharedfiles/filedetails/?id=3035467814) | P |  |
| 1776 | [Thermal Tooltips Reworked](https://steamcommunity.com/sharedfiles/filedetails/?id=3780815871) | S,M | UI |
| 1778 | [机械哈奇meitanji(Coal synthesis machine)](https://steamcommunity.com/sharedfiles/filedetails/?id=3091670103) | B |  |
| 1779 | [圣诞树christmas tree](https://steamcommunity.com/sharedfiles/filedetails/?id=2906165124) | B |  |
| 1780 | [MoveThisHere 气液移动点](https://steamcommunity.com/sharedfiles/filedetails/?id=3775498466) | B,S | UI, user menu |
| 1781 | [Floating(fix)](https://steamcommunity.com/sharedfiles/filedetails/?id=3501501795) | P |  |
| 1783 | [Auto Underwater Ranching Station](https://steamcommunity.com/sharedfiles/filedetails/?id=3747850787) | B |  |
| 1784 | [蒸汽涡轮机](https://steamcommunity.com/sharedfiles/filedetails/?id=3024606677) | P |  |

### Batch 37: ranks 1785–1811

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1785 | [Swap Flow Colours](https://steamcommunity.com/sharedfiles/filedetails/?id=2006586451) | P,O | PLib options |
| 1786 | [Variety Included!](https://steamcommunity.com/sharedfiles/filedetails/?id=3439346200) | P |  |
| 1787 | [Copy Materials Tool](https://steamcommunity.com/sharedfiles/filedetails/?id=3626072188) | T,O | PLib options, user menu |
| 1788 | [REC 降低电力消耗 Reduce Electricity Consumption](https://steamcommunity.com/sharedfiles/filedetails/?id=3412947176) | P |  |
| 1789 | [God Tools - Instant Move & Delete](https://steamcommunity.com/sharedfiles/filedetails/?id=3630596422) | T | user menu |
| 1790 | [Esteemed Turbines](https://steamcommunity.com/sharedfiles/filedetails/?id=3189266934) | B |  |
| 1791 | [Natural-Planting-Flx（自然种植土块 ）](https://steamcommunity.com/sharedfiles/filedetails/?id=3809267164) | B,O,M | PLib options |
| 1792 | [Polluted Water Electrolyzer](https://steamcommunity.com/sharedfiles/filedetails/?id=2951142821) | P |  |
| 1793 | [Crafted Quality / 匠作品质](https://steamcommunity.com/sharedfiles/filedetails/?id=3803708767) | P,O | PLib options, user menu |
| 1794 | [Bot Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3701580252) | S,O | PLib options, UI |
| 1796 | [超大容量智能电池Ultra Smart Battery](https://steamcommunity.com/sharedfiles/filedetails/?id=3771250086) | B |  |
| 1797 | [ONI: Retro Edition (beta)](https://steamcommunity.com/sharedfiles/filedetails/?id=3655492674) | B,T,S,K,O,V,M | PLib options, UI, user menu, worldgen |
| 1798 | [Melted Building Rebuild](https://steamcommunity.com/sharedfiles/filedetails/?id=3478682836) | P |  |
| 1799 | [小人一级全技能](https://steamcommunity.com/sharedfiles/filedetails/?id=3545917660) | P |  |
| 1800 | [Cold Wheat Bread -> Spice Bread](https://steamcommunity.com/sharedfiles/filedetails/?id=2838619875) | P |  |
| 1802 | [Old Steam turbine](https://steamcommunity.com/sharedfiles/filedetails/?id=1718257423) | P |  |
| 1805 | [(TEMP FIX) Diseases Restored](https://steamcommunity.com/sharedfiles/filedetails/?id=3723792761) | B |  |
| 1806 | [调整藻类蒸馏器Adjust Algae Distillery](https://steamcommunity.com/sharedfiles/filedetails/?id=3033510103) | P |  |
| 1807 | [Building-Super Compost 超级堆肥堆](https://steamcommunity.com/sharedfiles/filedetails/?id=3414689587) | P,O | PLib options |
| 1809 | [(DLC) Interplanar Automation](https://steamcommunity.com/sharedfiles/filedetails/?id=2361374172) | B |  |
| 1810 | [孟丫杰的懒狗工具-无情萝卜](https://steamcommunity.com/sharedfiles/filedetails/?id=3286903205) | P |  |
| 1811 | [Controlled Automation](https://steamcommunity.com/sharedfiles/filedetails/?id=3653679511) | B,S,O | PLib options, UI |

### Batch 38: ranks 1812–1841

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1812 | [Reduce time cost of tinker Microchip](https://steamcommunity.com/sharedfiles/filedetails/?id=3128126787) | P |  |
| 1813 | [Drop The Salt](https://steamcommunity.com/sharedfiles/filedetails/?id=2961593835) | P |  |
| 1814 | [Liquid Vent Not Overflow](https://steamcommunity.com/sharedfiles/filedetails/?id=2846822603) | P |  |
| 1815 | [(DLC) Better Meter Valves](https://steamcommunity.com/sharedfiles/filedetails/?id=2547745654) | P,O | PLib options |
| 1818 | [HeatingElement](https://steamcommunity.com/sharedfiles/filedetails/?id=2853421859) | B,O | PLib options, base game only |
| 1819 | [Multi-Element Reservoirs(大型多物质气液库)](https://steamcommunity.com/sharedfiles/filedetails/?id=3743339926) | B,S | UI, user menu |
| 1820 | [BotCore (Robot overhaul + Sweepy enhancement)](https://steamcommunity.com/sharedfiles/filedetails/?id=3762252757) | P,O | PLib options, user menu |
| 1821 | [植物粉碎机改进(修复)](https://steamcommunity.com/sharedfiles/filedetails/?id=3452857906) | P,O | PLib options |
| 1824 | [Idle Suits](https://steamcommunity.com/sharedfiles/filedetails/?id=1748408711) | P | base game only |
| 1825 | [Room Style Randomizer](https://steamcommunity.com/sharedfiles/filedetails/?id=3766516655) | T |  |
| 1826 | [Backwalls Fixed](https://steamcommunity.com/sharedfiles/filedetails/?id=3753518460) | B,T,S,K,V,M | bundle UI, mayCrashInPlay |
| 1828 | [Dowsing Booster](https://steamcommunity.com/sharedfiles/filedetails/?id=3548583609) | P,O | PLib options |
| 1830 | [Agricultural Biotechnology / 农业生物工程](https://steamcommunity.com/sharedfiles/filedetails/?id=3799614365) | B,S | UI |
| 1832 | [Lua Scripting Engine](https://steamcommunity.com/sharedfiles/filedetails/?id=1812965265) | B,S,M | UI |
| 1833 | [迷你净水器加强版](https://steamcommunity.com/sharedfiles/filedetails/?id=2984783787) | B |  |
| 1834 | [Liquid Auto-Ejector 液桶自动弹出器](https://steamcommunity.com/sharedfiles/filedetails/?id=3642179304) | P | user menu |
| 1835 | [金属还原器](https://steamcommunity.com/sharedfiles/filedetails/?id=3342288080) | B | base game only |
| 1837 | [Controlled Overlay](https://steamcommunity.com/sharedfiles/filedetails/?id=3636600859) | P | UI |
| 1838 | [Remote Worker Errands Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=3473019759) | P |  |
| 1839 | [Resources Search Hotkey](https://steamcommunity.com/sharedfiles/filedetails/?id=3531123422) | S,K,O | PLib options, UI |
| 1840 | [Ethanol Rocket Engine Fixed](https://steamcommunity.com/sharedfiles/filedetails/?id=2646143900) | B,O | PLib options |
| 1841 | [Neural Vacillator Assignment Precondition](https://steamcommunity.com/sharedfiles/filedetails/?id=3361635195) | P |  |

### Batch 39: ranks 1842–1867

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1842 | [MSJ Dream bed 梦世界 漂亮床](https://steamcommunity.com/sharedfiles/filedetails/?id=2857925888) | P |  |
| 1843 | [Quick Vacuum / 快速抽真空 和 移除液体](https://steamcommunity.com/sharedfiles/filedetails/?id=3772248976) | S,K | UI |
| 1845 | [Fast climb fire pole](https://steamcommunity.com/sharedfiles/filedetails/?id=3509802691) | P | nativeCalls |
| 1846 | [R_圣诞礼物_Christmas_Gifts](https://steamcommunity.com/sharedfiles/filedetails/?id=2906182469) | B,S | UI |
| 1848 | [更大的燃料舱](https://steamcommunity.com/sharedfiles/filedetails/?id=3519081396) | P |  |
| 1849 | [Anti-Entropy Generator](https://steamcommunity.com/sharedfiles/filedetails/?id=3772839873) | B,S | UI |
| 1850 | [Remove Puft Prince](https://steamcommunity.com/sharedfiles/filedetails/?id=2807313402) | P |  |
| 1851 | [隔热隔气隔温门](https://steamcommunity.com/sharedfiles/filedetails/?id=3803052734) | B |  |
| 1852 | [Precision Pipette Plus (gas & liquid) / 精准滴管增强 (气体& 液体)](https://steamcommunity.com/sharedfiles/filedetails/?id=3779620887) | T,S,K,O | PLib options, UI |
| 1853 | [南瓜灯 Pumpkin Lantern 2.1](https://steamcommunity.com/sharedfiles/filedetails/?id=2919045981) | B |  |
| 1854 | [气体涡轮机 GasTurbine](https://steamcommunity.com/sharedfiles/filedetails/?id=3752029503) | B,O | PLib options |
| 1855 | [POB-Bionic Besties 仿生小伙伴](https://steamcommunity.com/sharedfiles/filedetails/?id=3404416193) | X | needs 3172734005, noCode |
| 1856 | [Direct Irrigation](https://steamcommunity.com/sharedfiles/filedetails/?id=3743535837) | P |  |
| 1858 | [Applied Logistics Network(ALN) 应用物流网络](https://steamcommunity.com/sharedfiles/filedetails/?id=3753045682) | B,T,S,K,O | PLib options, UI, user menu |
| 1859 | [飞吧，游戏君Fly, game](https://steamcommunity.com/sharedfiles/filedetails/?id=2972754416) | S | UI |
| 1860 | [Expanded Lights (Updated)](https://steamcommunity.com/sharedfiles/filedetails/?id=3800125328) | B |  |
| 1861 | [大型蒸汽涡轮机（Large steam turbine）](https://steamcommunity.com/sharedfiles/filedetails/?id=3547626800) | B |  |
| 1862 | [Robo-Pilot Tweaks](https://steamcommunity.com/sharedfiles/filedetails/?id=3577975375) | P,O | PLib options |
| 1864 | [Duplicant Lifecycles Updated](https://steamcommunity.com/sharedfiles/filedetails/?id=3375382664) | P,O | PLib options |
| 1865 | [Modular Power (Power Plant Systems)](https://steamcommunity.com/sharedfiles/filedetails/?id=3814416287) | B,V | UI |
| 1866 | [太空背景 / True Space Background](https://steamcommunity.com/sharedfiles/filedetails/?id=3810757236) | B |  |
| 1867 | [让煎蛋卷再次伟大  Good Old Omelette](https://steamcommunity.com/sharedfiles/filedetails/?id=3760385522) | P |  |

### Batch 40: ranks 1870–1898

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1870 | [Free Marine Drill](https://steamcommunity.com/sharedfiles/filedetails/?id=3745300476) | P |  |
| 1871 | [Rainbow Farts](https://steamcommunity.com/sharedfiles/filedetails/?id=2306600428) | S | UI |
| 1872 | [Turn Back the Clock](https://steamcommunity.com/sharedfiles/filedetails/?id=2553917491) | P,O | PLib options, base game only |
| 1876 | [Element Attributes](https://steamcommunity.com/sharedfiles/filedetails/?id=3078596122) | P |  |
| 1877 | [游戏速度控制](https://steamcommunity.com/sharedfiles/filedetails/?id=3784380147) | S | UI |
| 1878 | [Mutations: Cross-Pollinated](https://steamcommunity.com/sharedfiles/filedetails/?id=3767758558) | S,O,M | PLib options, UI, user menu |
| 1879 | [Improved Wood Tuning](https://steamcommunity.com/sharedfiles/filedetails/?id=3292488355) | P,O | PLib options |
| 1881 | [DLC Amalgam — Europa & Mashup](https://steamcommunity.com/sharedfiles/filedetails/?id=3797722065) | M | UI, worldgen |
| 1882 | [Advance Storage repaired](https://steamcommunity.com/sharedfiles/filedetails/?id=3248848464) | B,S | UI |
| 1883 | [Inferno dlc spaced out](https://steamcommunity.com/sharedfiles/filedetails/?id=2983164192) | P | worldgen |
| 1884 | [Drawable Drywalls](https://steamcommunity.com/sharedfiles/filedetails/?id=2839691577) | B,S | UI |
| 1885 | [Critters Can Pass](https://steamcommunity.com/sharedfiles/filedetails/?id=2841373684) | B |  |
| 1886 | [Mood Lamps Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=3792459208) | P | needs 2660131829 |
| 1887 | [医废桶Medical_Waste_Bucket](https://steamcommunity.com/sharedfiles/filedetails/?id=3038102790) | B |  |
| 1890 | [CafePlus: New Coffee Drinks](https://steamcommunity.com/sharedfiles/filedetails/?id=3542413342) | P |  |
| 1891 | [S 漫花果生长加速Vine Branch Accelerate Growth 1.0](https://steamcommunity.com/sharedfiles/filedetails/?id=3590596912) | P |  |
| 1892 | [LightingAndArt](https://steamcommunity.com/sharedfiles/filedetails/?id=3252090977) | B |  |
| 1893 | [可调节数量的分类存储箱](https://steamcommunity.com/sharedfiles/filedetails/?id=3509238377) | S | UI, user menu |
| 1895 | [污染物挥发器Fuifaji(Pollutant volatilization machine)](https://steamcommunity.com/sharedfiles/filedetails/?id=3095401174) | B |  |
| 1896 | [We Need More Phosphorus!](https://steamcommunity.com/sharedfiles/filedetails/?id=3739578072) | P,O | PLib options |
| 1897 | [Controlled Framerate](https://steamcommunity.com/sharedfiles/filedetails/?id=3670573352) | S,K,O | PLib options, UI |
| 1898 | [(Vanilla + DLC) Research Requirements](https://steamcommunity.com/sharedfiles/filedetails/?id=2405919736) | S | UI |

### Batch 41: ranks 1899–1922

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1899 | [Piped Soda Fountain](https://steamcommunity.com/sharedfiles/filedetails/?id=3792532797) | B |  |
| 1900 | [Shinebug Jar](https://steamcommunity.com/sharedfiles/filedetails/?id=3818420082) | B,S,O | PLib options, UI, user menu |
| 1902 | [No Disease](https://steamcommunity.com/sharedfiles/filedetails/?id=3634548850) | P |  |
| 1903 | [Mutant Plus — Stackable Plant Mutations](https://steamcommunity.com/sharedfiles/filedetails/?id=3807135177) | S,O | PLib options, UI, user menu |
| 1904 | [Controlled Morale](https://steamcommunity.com/sharedfiles/filedetails/?id=3689864363) | B,S,O | PLib options, UI, elements |
| 1906 | [Challenge collection](https://steamcommunity.com/sharedfiles/filedetails/?id=2014940545) | P | worldgen, needs 1812965265 |
| 1907 | [Camera Scroll Pan Speed](https://steamcommunity.com/sharedfiles/filedetails/?id=2844428595) | V |  |
| 1908 | [Helium Reborn](https://steamcommunity.com/sharedfiles/filedetails/?id=2915321826) | P,O | PLib options |
| 1909 | [帕库鱼幸福度加成](https://steamcommunity.com/sharedfiles/filedetails/?id=3744782341) | P,O | PLib options |
| 1910 | [(DLC) Mutant Plants Rebalanced](https://steamcommunity.com/sharedfiles/filedetails/?id=3025506650) | P |  |
| 1911 | [Transfer Statistics](https://steamcommunity.com/sharedfiles/filedetails/?id=2711203625) | P,O | PLib options |
| 1912 | [尖块兽可震落木材Stego Shake Wood](https://steamcommunity.com/sharedfiles/filedetails/?id=3771247584) | P |  |
| 1913 | [The True Luxury Bed - 真正的豪华床](https://steamcommunity.com/sharedfiles/filedetails/?id=2858439107) | P |  |
| 1914 | [小动物照料5倍时长 Critter Grooming Duration 5x](https://steamcommunity.com/sharedfiles/filedetails/?id=3771253264) | P |  |
| 1915 | [Mafic Pipes](https://steamcommunity.com/sharedfiles/filedetails/?id=2514097176) | P |  |
| 1916 | [Temperature Resistant Pips](https://steamcommunity.com/sharedfiles/filedetails/?id=3305749173) | P,O | PLib options |
| 1917 | [下雪啦 Snowing](https://steamcommunity.com/sharedfiles/filedetails/?id=2934219021) | B |  |
| 1918 | [Better Electrolyzer](https://steamcommunity.com/sharedfiles/filedetails/?id=2841504136) | P |  |
| 1919 | [ONI_BoosterPlus](https://steamcommunity.com/sharedfiles/filedetails/?id=3493536777) | S | UI |
| 1920 | [粉红灯Pink lamp](https://steamcommunity.com/sharedfiles/filedetails/?id=2918431513) | P |  |
| 1921 | [DeepFreezer](https://steamcommunity.com/sharedfiles/filedetails/?id=3789688350) | B,O | PLib options |
| 1922 | [Debug Fog of War](https://steamcommunity.com/sharedfiles/filedetails/?id=3627267062) | P |  |

### Batch 42: ranks 1923–1949

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1923 | [PlantManualMutate](https://steamcommunity.com/sharedfiles/filedetails/?id=3713083599) | S | UI |
| 1924 | [automatic Grooming Station](https://steamcommunity.com/sharedfiles/filedetails/?id=3747744676) | B |  |
| 1925 | [Research Cost Multiplier](https://steamcommunity.com/sharedfiles/filedetails/?id=3674785519) | P,O | PLib options |
| 1926 | [Wood Heater Automated](https://steamcommunity.com/sharedfiles/filedetails/?id=3594192836) | P |  |
| 1928 | [Heavy-Watt Wire Anywhere / 헤비와트 와이어 벽 타일 통과](https://steamcommunity.com/sharedfiles/filedetails/?id=3691848497) | P |  |
| 1929 | [更多烟熏食物](https://steamcommunity.com/sharedfiles/filedetails/?id=3752346568) | P |  |
| 1931 | [(MG)Particle collection speed 10 times(超级辐射收集器)](https://steamcommunity.com/sharedfiles/filedetails/?id=2567161929) | P |  |
| 1933 | [NamedStorage](https://steamcommunity.com/sharedfiles/filedetails/?id=2991018571) | P | UI |
| 1934 | [No Minimum Temperature](https://steamcommunity.com/sharedfiles/filedetails/?id=2800997047) | P | base game only |
| 1935 | [火箭套娃](https://steamcommunity.com/sharedfiles/filedetails/?id=3454818548) | P | worldgen |
| 1936 | [Project Expedition](https://steamcommunity.com/sharedfiles/filedetails/?id=3803418607) | B,T,S,K,V | UI, user menu, worldgen |
| 1937 | [Discord Rich Presence](https://steamcommunity.com/sharedfiles/filedetails/?id=3752842483) | S,O | PLib options, UI |
| 1939 | [Veggie Hatch eat Wood](https://steamcommunity.com/sharedfiles/filedetails/?id=2847963606) | P |  |
| 1940 | [Rocket Umbilicals (Deprecated)](https://steamcommunity.com/sharedfiles/filedetails/?id=3051161533) | S | UI |
| 1941 | [Early Game Nosh Beans](https://steamcommunity.com/sharedfiles/filedetails/?id=2790414858) | P |  |
| 1942 | [TemperaturE-05](https://steamcommunity.com/sharedfiles/filedetails/?id=2729922307) | P |  |
| 1943 | [植物外壳更多用途 / More Uses of Plant Husk](https://steamcommunity.com/sharedfiles/filedetails/?id=3719939081) | P,O | PLib options |
| 1944 | [Radial Menu](https://steamcommunity.com/sharedfiles/filedetails/?id=3742958503) | T,S,K | UI |
| 1945 | [Launch Pad Gantry (Deprecated)](https://steamcommunity.com/sharedfiles/filedetails/?id=3053330755) | P |  |
| 1947 | [Partitioned Storage (fixed)](https://steamcommunity.com/sharedfiles/filedetails/?id=3478498118) | S | UI, user menu |
| 1948 | [警示牌 Warning signs -K31](https://steamcommunity.com/sharedfiles/filedetails/?id=2865091697) | B,S | UI |
| 1949 | [Performance Assist / 性能辅助](https://steamcommunity.com/sharedfiles/filedetails/?id=3772297140) | P |  |

### Batch 43: ranks 1950–1968

| Rank | Mod | Classes | Hints |
|---|---|---|---|
| 1950 | [Controlled Conduits](https://steamcommunity.com/sharedfiles/filedetails/?id=3678742887) | B,S | UI |
| 1951 | [Cheety Creety 2](https://steamcommunity.com/sharedfiles/filedetails/?id=2515073640) | P,O | PLib options |
| 1952 | [快速打印（Easy Print）](https://steamcommunity.com/sharedfiles/filedetails/?id=3744473442) | T,K |  |
| 1953 | [Controlled Sublimation](https://steamcommunity.com/sharedfiles/filedetails/?id=3636578165) | B,O | PLib options |
| 1956 | [碎片恒温器+](https://steamcommunity.com/sharedfiles/filedetails/?id=3457655108) | B,O | PLib options |
| 1957 | [金属锻炉（金属精炼器的高级版本）](https://steamcommunity.com/sharedfiles/filedetails/?id=3639032470) | B,O | PLib options |
| 1958 | [全能增幅块](https://steamcommunity.com/sharedfiles/filedetails/?id=3737965162) | P,O | PLib options |
| 1959 | [太阳能舱电池舱太阳能板](https://steamcommunity.com/sharedfiles/filedetails/?id=3257386232) | P |  |
| 1962 | [Divers Lungs (UNSUPPORTED)](https://steamcommunity.com/sharedfiles/filedetails/?id=2535676257) | P,O | PLib options |
| 1965 | [梯子门（LadderDoor）](https://steamcommunity.com/sharedfiles/filedetails/?id=3762973536) | B |  |
| 1966 | [Rename Asteroids U59](https://steamcommunity.com/sharedfiles/filedetails/?id=3613508049) | S | UI |
| 1967 | [Isoresin Refiner // 树脂精炼器](https://steamcommunity.com/sharedfiles/filedetails/?id=3195409590) | B |  |
| 1968 | [醉酒机](https://steamcommunity.com/sharedfiles/filedetails/?id=3241546548) | B |  |

## Not swept

| Rank | Mod | Why |
|---|---|---|
| 756 | [Português (PT-BR) — Oxigênio Traduzido 2026](https://steamcommunity.com/sharedfiles/filedetails/?id=3745757494) | a translation: installed disabled, as a subscription leaves it; not swept |
| 758 | [( Türkçe ) Oygen Not Included](https://steamcommunity.com/sharedfiles/filedetails/?id=933183009) | a translation: installed disabled, as a subscription leaves it; not swept |
| 768 | [Українізатор + 6 DLC (UA)](https://steamcommunity.com/sharedfiles/filedetails/?id=3093901934) | a translation: installed disabled, as a subscription leaves it; not swept |
| 817 | [Mod Thai Language! by Atlastseason (Alpha 0.1)](https://steamcommunity.com/sharedfiles/filedetails/?id=1248960307) | a translation: installed disabled, as a subscription leaves it; not swept |
| 860 | [Tradução PT-BR "THE AQUATIC PLANET PACK"](https://steamcommunity.com/sharedfiles/filedetails/?id=3731631427) | a translation: installed disabled, as a subscription leaves it; not swept |
| 950 | [Oxygen Not Included - Tłumaczenie z DLC, wersji (U59 737195 z 14.06.2026) - v445](https://steamcommunity.com/sharedfiles/filedetails/?id=3424175679) | a translation: installed disabled, as a subscription leaves it; not swept |
| 980 | [ONI_WorkIng_Progress_78%](https://steamcommunity.com/sharedfiles/filedetails/?id=1764502347) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1030 | [Português-Portugal (descontinuada)](https://steamcommunity.com/sharedfiles/filedetails/?id=1266052762) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1083 | [Oxygen Not Included Ukraine](https://steamcommunity.com/sharedfiles/filedetails/?id=1332127867) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1139 | [Język Polski ONI & DLC W.I.P](https://steamcommunity.com/sharedfiles/filedetails/?id=2746246303) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1147 | [Polish Language 30% complete](https://steamcommunity.com/sharedfiles/filedetails/?id=3263949642) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1187 | [Oxygen Not Included - Traducción al español/castellano (ABANDONADA)](https://steamcommunity.com/sharedfiles/filedetails/?id=930994104) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1279 | [Talk Like a Pirate](https://steamcommunity.com/sharedfiles/filedetails/?id=1516818219) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1366 | [Arabic ONI بالعربي](https://steamcommunity.com/sharedfiles/filedetails/?id=2922482730) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1431 | [The RuTranslation Mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3523056620) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1493 | [UA LampTranslate](https://steamcommunity.com/sharedfiles/filedetails/?id=2822339758) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1588 | [Thermal Checkpoint](https://steamcommunity.com/sharedfiles/filedetails/?id=3616794207) | not downloadable with SteamCMD |
| 1595 | [Slovak Translation Pack](https://steamcommunity.com/sharedfiles/filedetails/?id=929247646) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1630 | [Русификатор и сортировка (Wiki)](https://steamcommunity.com/sharedfiles/filedetails/?id=3667070295) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1699 | [Фиксы официального перевода](https://steamcommunity.com/sharedfiles/filedetails/?id=3673652718) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1718 | [Bahasa Indonesia](https://steamcommunity.com/sharedfiles/filedetails/?id=1579169540) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1761 | [Türkçe / 2026](https://steamcommunity.com/sharedfiles/filedetails/?id=3620202317) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1766 | [Oxygen Not Included Dutch/Nederlands](https://steamcommunity.com/sharedfiles/filedetails/?id=1368234558) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1775 | [Swedish](https://steamcommunity.com/sharedfiles/filedetails/?id=1387598596) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1804 | [Finnish](https://steamcommunity.com/sharedfiles/filedetails/?id=1403530171) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1827 | [Oxygen Not Included - Tłumaczenie z DLC, wersji (U59 737195 z 14.06.2026) - v445 (English Names)](https://steamcommunity.com/sharedfiles/filedetails/?id=3654813505) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1938 | [](https://steamcommunity.com/sharedfiles/filedetails/?id=3095987024) | a translation: installed disabled, as a subscription leaves it; not swept |
| 1960 | [](https://steamcommunity.com/sharedfiles/filedetails/?id=2817183839) | a translation: installed disabled, as a subscription leaves it; not swept |
| 2052 | [ONI Português BR](https://steamcommunity.com/sharedfiles/filedetails/?id=3512323881) | a translation: installed disabled, as a subscription leaves it; not swept |
| 2151 | [Čeština s DLC](https://steamcommunity.com/sharedfiles/filedetails/?id=3740803157) | a translation: installed disabled, as a subscription leaves it; not swept |
| 2200 | [Čeština (DLC)](https://steamcommunity.com/sharedfiles/filedetails/?id=3617182868) | a translation: installed disabled, as a subscription leaves it; not swept |
| 2271 | [](https://steamcommunity.com/sharedfiles/filedetails/?id=2733975631) | a translation: installed disabled, as a subscription leaves it; not swept |
