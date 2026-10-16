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
- [x] Story Traits list, Game Settings panel (seed, difficulty steppers, checkboxes respond)
- [x] `WorldGenScreen` (threaded worldgen with SimDLL settle, progress)
- [x] Worldgen parity: the same coordinates (typed into the destination screen) generate the same terrain as the original player
- [x] `CharacterSelectionController` / `CharacterContainer` (Duplicant cards, reroll, embark)
- [ ] `ScenariosMenu`, `ModsScreen` (no Workshop items with the test double)
- [x] `LockerMenuScreen` (Supply Closet, offline) and its Duplicants outfit screen
- [ ] `PatchNotesScreen`, `KleiItemDropScreen`, `KleiInventoryScreen` contents (need network/Steam services)
- [x] `RetiredColonyInfoScreen`: colony list, achievements, colony detail (Duplicants with portraits, buildings, oxygen graph; "Timelapse Unavailable", see the preview open item)
- [x] Tutorial videos (`VideoScreen` from a message's video widget, render-texture output) play as the original, with libvpx available (verified with Ubuntu's libvpx9 unpacked locally)
- [ ] Intro short on first launch, victory videos

## Options

- [x] `OptionsMenuScreen` and its Graphics, Audio, Game, Data Communication, Feedback and Credits screens (compared with the original)
- [x] `GraphicsOptionsScreen`: fullscreen switching at runtime (window resized, UI re-laid out, "Accept Changes?"), resolution label as the original ("@ 0Hz" under Xvfb in both)
- [ ] `GraphicsOptionsScreen`: resolution list choice, UI scale, colour mode
- [ ] `AudioOptionsScreen`: volumes, device
- [ ] `GameOptionsScreen`: autosave interval, temperature unit, camera speed, save/cloud settings
- [x] `InputBindingsScreen`: rebinding (conflict notice when a key is taken), persistence across restarts (`keybindings.json`)
- [x] `LanguageOptionsScreen`: built-in languages listed; choosing Chinese prompts in Chinese, restarts through `Restarter`, and the menus come back in Chinese (CJK font)
- [x] Settings persistence across restarts (`kplayerprefs.yaml`: temperature unit)

## Colony HUD and screens

- [x] World composite: terrain, tiles, backwall, gas, liquids, light buffer, property textures, fog of war
- [x] Duplicants and buildings (KAnim batches), printing pod, status items, name displays
- [x] `TopLeftControlScreen`, `DateTime` (cycle clock), `MeterScreen` (Duplicants, stress, rations)
- [x] `SpeedControlScreen` (pause, speeds 1–3)
- [x] `ColonyDiagnosticScreen` panel, `PinnedResourcesPanel` / resources panel
- [x] Notifications (`NotificationScreen`), tutorial messages
- [x] `DetailsScreen` (selection: status, properties tabs with nested canvases and RectMask2D)
- [x] `HoverTextScreen` (element hover cards)
- [x] `SideDetailsScreen`: Ration Box element filter (unticking → "Filters Not Designated"), capacity slider (drag → new capacity), Material tab
- [ ] Other buildings' side screens
- [ ] `BuildMenu` / `BuildingGroupScreen` / `ProductInfoScreen` / `MaterialSelectionPanel` (planning and placing buildings)
- [x] Management screens opened by hotkey and compared with the original: Vitals, Consumables, Priorities (jobs), Schedule (`ScheduleScreen`), Skills (`SkillsScreen`, Duplicant portrait in a stencil mask), Database (`CodexScreen`), Reports (`ReportScreen`)
- [x] Research and Starmap buttons before a Research Station / Telescope exists: locked, with the original's tooltips
- [ ] `ResearchScreen`, `StarmapScreen` / `ClusterMapScreen` contents (need a Research Station / Telescope)
- [x] `PauseScreen`: open, save with overwrite confirmation (41 saves in one run)
- [x] `PauseScreen` → Main Menu ("Save and quit" prompt) → front end with Resume Game; the front end comes back at the smaller UI scale as in the original (once, ours returned to the normal scale a few seconds later; not reproduced since)
- [x] `PauseScreen`: Save As (save slots, New Save, typed name in `FileNameDialog`), Load (colony list, colony view, Load Game → the save loads in game)
- [ ] `PauseScreen`: Options
- [x] Printing pod recharge (cycle 3–4): "New Printables are available", pod side screen "Choose a Blueprint" → selection screen with Duplicants and a care package → Print → the new Duplicant arrives
- [x] `GameOverScreen` (colony lost)
- [ ] `VictoryScreen`, `StoryMessageScreen`, `EventInfoScreen`
- [ ] Timelapse capture (`ReadPixels` + `EncodeToPNG`)
- [x] Camera: wheel zoom out/in, keyboard pan, Home (camera return)
- [ ] Camera: edge and drag pan, follow, bookmarks (`WorldSelector` for multiple asteroids)

## Tools

- [x] Dig (`DigTool`)
- [x] Activation with tool label, filter panel and overlay: Cancel, Deconstruct, Prioritize (`PriorityScreen`), Disinfect (germ overlay), Sweep (`ClearTool`), Mop, Attack, Wrangle (`CaptureTool`), Harvest, Empty Pipe, Disconnect (compared with the original where its scripted run activated the same tool)
- [ ] Tool orders carried out (beyond Dig), Copy Settings, Move To
- [x] Build placement with materials: Storage Bin and a dragged Ladder from the Base menu; placement refused with "Missing resources" until the colony has the materials (as the original); Duplicants deliver and build
- [ ] `BaseUtilityBuildTool` (wires, pipes), rotation, other categories

## Overlays

- [x] F1–F11 compared with the original (same keys, same frame of play): Oxygen, Power, Temperature, Materials (`TileMode`), Light, Plumbing (`LiquidConduit`), Ventilation (`GasConduit`), Decor, Germs (`Disease`), Farming (`Crop`), Rooms (with room highlight under the cursor)
- [ ] Exosuits (`Suit`), Automation (`Logic`), Shipping (`SolidConveyor`), Radiation, Heat flow, Thermal conductivity, Priorities, Harvest: unlocked by research (not in the overlay bar at cycle 1 in either player)
- [ ] Overlay legends (`OverlayLegend`) and filters

## Simulation and persistence

- [x] Ten cycles at speed 3 with digging and building, zero exceptions (M5 exit)
- [x] Autosave each cycle; save → quit to menu → load → continue (M5 exit)
- [x] Hi-res texture bundle streaming (`StreamedTextures`): bundles load while the Supply Closet is open; the Duplicant outfit screen renders from them

## Platform

- [x] Window, input (mouse, keyboard, text), cursor images
- [ ] Clipboard (seed and colony-name fields)
- [x] Frame rate at parity with the original (PLAN §Status, Performance)
- [x] Windowed → fullscreen (borderless) switching at runtime
- [ ] Exclusive fullscreen, resolution changes at runtime
- [ ] Focus loss and regain (audio pause, input release)
- [ ] Audio (FMOD): music, ambience, UI and world sounds. Not verifiable on the development machine: FMOD loads and initialises in both players, but no PulseAudio client library is installed, so neither produces output

## Deviations

- Floating-point results are not bit-exact (out of scope, ARCHITECTURE §5).
- Runtime deviations inherent to CoreCLR (ARCHITECTURE §2.1).
- `ScreenCapture.CaptureScreenshot` with superSize > 1 (debug screenshot keys only): open item, PLAN.
