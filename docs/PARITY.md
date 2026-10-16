# Parity checklist

The M6 exit criterion (PLAN §M6): every item ticked, or listed with a documented deviation.
Items come from the game's own screens, tools and features (Assembly-CSharp: `KScreen`
subclasses, `OverlayModes`, `InterfaceTool` subclasses, options screens). An item is ticked when
a run on this engine exercised it and the result matched the original player (screenshots
compared with the original under the same conditions, ARCHITECTURE §9), with no exceptions in
the log.

Legend: `[x]` verified · `[ ]` not yet verified · `[~]` deviation (see the note).

## Start-up and front end

- [x] Launch scene, Klei/Unity splash, `LaunchInitializer`, Steam initialisation (offline test double)
- [x] Main menu: backdrop, buttons, MOTD/news panels (offline), build watermark
- [x] Main menu: Resume Game / Load Game (`LoadScreen`, colony and save lists)
- [x] New game flow: `ModeSelectScreen` (Survival / No Sweat), `ClusterCategorySelectionScreen`
- [x] `ColonyDestinationSelectScreen` (asteroid, seed, settings panels)
- [ ] Custom game settings and story traits panels
- [x] `WorldGenScreen` (threaded worldgen with SimDLL settle, progress)
- [x] `CharacterSelectionController` / `CharacterContainer` (Duplicant cards, reroll, embark)
- [ ] `ScenariosMenu`, `ModsScreen` (no Workshop items with the test double)
- [ ] `CreditsScreen`, `PatchNotesScreen`, `KleiItemDropScreen`, `LockerMenuScreen` / `KleiInventoryScreen` (need network/Steam services)
- [ ] `RetiredColonyInfoScreen` (colony summaries)
- [ ] Intro and tutorial videos (`VideoScreen`; needs system libvpx)

## Options

- [ ] `OptionsMenuScreen`
- [ ] `GraphicsOptionsScreen`: resolution list, fullscreen/windowed switching, UI scale, colour mode
- [ ] `AudioOptionsScreen`: volumes, device
- [ ] `GameOptionsScreen`: autosave interval, temperature unit, camera speed, save/cloud settings
- [ ] `ControlsScreen` / `InputBindingsScreen`: rebinding, persistence
- [ ] `LanguageOptionsScreen`: language switch (font swap, restart)
- [ ] `MetricsOptionsScreen`, `FeedbackScreen`
- [ ] Settings persistence across restarts (`kplayerprefs.yaml`)

## Colony HUD and screens

- [x] World composite: terrain, tiles, backwall, gas, liquids, light buffer, property textures, fog of war
- [x] Duplicants and buildings (KAnim batches), printing pod, status items, name displays
- [x] `TopLeftControlScreen`, `DateTime` (cycle clock), `MeterScreen` (Duplicants, stress, rations)
- [x] `SpeedControlScreen` (pause, speeds 1–3)
- [x] `ColonyDiagnosticScreen` panel, `PinnedResourcesPanel` / resources panel
- [x] Notifications (`NotificationScreen`), tutorial messages
- [x] `DetailsScreen` (selection: status, properties tabs with nested canvases and RectMask2D)
- [x] `HoverTextScreen` (element hover cards)
- [ ] `SideDetailsScreen` side screens (per-building settings)
- [ ] `BuildMenu` / `BuildingGroupScreen` / `ProductInfoScreen` / `MaterialSelectionPanel` (planning and placing buildings)
- [x] Management screens opened by hotkey and compared with the original: Vitals, Consumables, Priorities (jobs), Schedule (`ScheduleScreen`), Skills (`SkillsScreen`, Duplicant portrait in a stencil mask), Database (`CodexScreen`), Reports (`ReportScreen`)
- [ ] Research (`ResearchScreen`), Starmap (`StarmapScreen` / `ClusterMapScreen`): their hotkeys open nothing at cycle 1 in either player; verify through the HUD buttons
- [x] `PauseScreen`: open, save with overwrite confirmation (41 saves in one run)
- [ ] `PauseScreen`: save as (`FileNameDialog`), load, options, quit to menu
- [ ] `PrinterceptorScreen` / care packages (`CarePackageContainer`) at the printing pod's recharge
- [x] `GameOverScreen` (colony lost)
- [ ] `VictoryScreen`, `StoryMessageScreen`, `EventInfoScreen`
- [ ] Timelapse capture (`ReadPixels` + `EncodeToPNG`)
- [ ] Camera: pan (keys, edge, drag), zoom, follow, bookmarks (`WorldSelector` for multiple asteroids)

## Tools

- [x] Dig (`DigTool`)
- [ ] Cancel, Deconstruct, Prioritize (`PriorityScreen`), Disinfect, Sweep (`ClearTool`), Mop, Attack, Wrangle (`CaptureTool`), Harvest, Empty Pipe, Disconnect, Copy Settings, Move To
- [~] Build placement: a debug-only building placed in instant-build mode; regular construction with materials, `BaseUtilityBuildTool` (wires, pipes), rotation not yet verified

## Overlays

- [x] F1–F11 compared with the original (same keys, same frame of play): Oxygen, Power, Temperature, Materials (`TileMode`), Light, Plumbing (`LiquidConduit`), Ventilation (`GasConduit`), Decor, Germs (`Disease`), Farming (`Crop`), Rooms (with room highlight under the cursor)
- [ ] Exosuits (`Suit`), Automation (`Logic`), Shipping (`SolidConveyor`), Radiation, Heat flow, Thermal conductivity, Priorities, Harvest (Shift+F1–F4 select nothing new in either player at this point; open through the overlay bar)
- [ ] Overlay legends (`OverlayLegend`) and filters

## Simulation and persistence

- [x] Ten cycles at speed 3 with digging and building, zero exceptions (M5 exit)
- [x] Autosave each cycle; save → quit to menu → load → continue (M5 exit)
- [ ] Hi-res texture bundle streaming (`StreamedTextures`)

## Platform

- [x] Window, input (mouse, keyboard, text), cursor images
- [ ] Clipboard (seed and colony-name fields)
- [x] Frame rate at parity with the original (PLAN §Status, Performance)
- [ ] Fullscreen / borderless / windowed switching and resolution changes at runtime
- [ ] Focus loss and regain (audio pause, input release)
- [ ] Audio (FMOD): music, ambience, UI and world sounds

## Deviations

- Floating-point results are not bit-exact (out of scope, ARCHITECTURE §5).
- Runtime deviations inherent to CoreCLR (ARCHITECTURE §2.1).
- `ScreenCapture.CaptureScreenshot` with superSize > 1 (debug screenshot keys only): open item, PLAN.
