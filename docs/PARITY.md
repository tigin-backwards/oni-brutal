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
- [x] `ModsScreen` (empty list, Steam Workshop / Disable All / Close; no Workshop items with the test double) as the original
- [x] `ScenariosMenu`: not reachable in this build (no code opens it)
- [x] `LockerMenuScreen` (Supply Closet, offline) and its Duplicants outfit screen
- [ ] `PatchNotesScreen`, `KleiItemDropScreen`, `KleiInventoryScreen` contents (need network/Steam services)
- [x] `RetiredColonyInfoScreen`: colony list, achievements, colony detail (Duplicants with portraits, buildings, oxygen graph; "Timelapse Unavailable", see the preview open item)
- [x] Tutorial videos (`VideoScreen` from a message's video widget, render-texture output) play as the original, with libvpx available (verified with Ubuntu's libvpx9 unpacked locally)
- [x] Intro short on first launch: this build sets no intro short (`MainMenu.IntroShortName`), so neither player plays one (no `PlayShortOnLaunch` pref written by either)
- [ ] Victory videos (same `VideoScreen` path as the verified tutorial videos)

## Options

- [x] `OptionsMenuScreen` and its Graphics, Audio, Game, Data Communication, Feedback and Credits screens (compared with the original)
- [x] `GraphicsOptionsScreen`: fullscreen switching at runtime (window resized, UI re-laid out, "Accept Changes?"), resolution label as the original ("@ 0Hz" under Xvfb in both)
- [x] `GraphicsOptionsScreen`: resolution dropdown (placement and the same mode list as the original on a single-mode display), choosing a resolution resizes the window with "Accept Changes?"
- [x] `GraphicsOptionsScreen`: UI scale slider (whole UI re-scales), colour mode dropdown (palette swatches change) as the original
- [x] `AudioOptionsScreen`: volume sliders and settings checkboxes respond as the original (sound output itself: see Audio below)
- [x] `GameOptionsScreen`: camera pan speed slider, cloud-save checkbox, temperature unit. Slider clicks and drags started off the handle land on the same value as the original; drags started on the handle follow uGUI's handle-offset rule exactly here, while the original's result varies with the press point under XTEST (one step either way)
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
- [x] `BuildMenu` / `BuildingGroupScreen` / `ProductInfoScreen` / `MaterialSelectionPanel`: category menus, building info, material choice and placement (Storage Bin, Ladder, Dev Life Support, wire tool)
- [x] Management screens opened by hotkey and compared with the original: Vitals, Consumables, Priorities (jobs), Schedule (`ScheduleScreen`), Skills (`SkillsScreen`, Duplicant portrait in a stencil mask), Database (`CodexScreen`), Reports (`ReportScreen`)
- [x] Research and Starmap buttons before a Research Station / Telescope exists: locked, with the original's tooltips
- [x] Research tree interaction: hover, tooltips, selecting a tech (instant completion in debug mode) as the original
- [ ] Research progress over time at a real Research Station; `ClusterMapScreen` (Spaced Out DLC, not owned with the test double)
- [x] `PauseScreen`: open, save with overwrite confirmation (41 saves in one run)
- [x] `PauseScreen` → Main Menu ("Save and quit" prompt) → front end with Resume Game; the front end comes back at the smaller UI scale as in the original (once, ours returned to the normal scale a few seconds later; not reproduced since)
- [x] `PauseScreen`: Save As (save slots, New Save, typed name in `FileNameDialog`), Load (colony list, colony view, Load Game → the save loads in game)
- [x] `PauseScreen`: Options (the same options screens as the main menu)
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
- [~] `BaseUtilityBuildTool`: the wire tool (Power → Wire, material list, power overlay) opens and responds to a drag as in the original; a placed wire has not been observed in either player's scripted run yet
- [ ] Pipes, rotation, other categories

## Overlays

- [x] F1–F11 compared with the original (same keys, same frame of play): Oxygen, Power, Temperature, Materials (`TileMode`), Light, Plumbing (`LiquidConduit`), Ventilation (`GasConduit`), Decor, Germs (`Disease`), Farming (`Crop`), Rooms (with room highlight under the cursor)
- [x] Research-gated overlays with everything unlocked (debug instant-build mode, compared with the original in the same mode): Exosuits, Automation, Shipping, and the remaining Shift+F overlays
- [x] Build menu categories that become available later (Utilities, Automation, Shipping, Rocketry) appear and lay out as in the original
- [x] `ResearchScreen` tree and `StarmapScreen` open as in the original
- [x] Overlay legends (`OverlayLegend`) shown with every overlay compared above
- [ ] Overlay filter toggles

## Simulation and persistence

- [x] Ten cycles at speed 3 with digging and building, zero exceptions (M5 exit)
- [x] Autosave each cycle; save → quit to menu → load → continue (M5 exit)
- [x] Hi-res texture bundle streaming (`StreamedTextures`): bundles load while the Supply Closet is open; the Duplicant outfit screen renders from them

## Platform

- [x] Window, input (mouse, keyboard, text), cursor images
- [x] Clipboard: select all, copy and paste in the destination coordinates field (as the original)
- [x] Frame rate at parity with the original (PLAN §Status, Performance)
- [x] Windowed → fullscreen (borderless) switching at runtime
- [x] Resolution changes at runtime (window resized)
- [ ] Exclusive fullscreen
- [x] Focus loss and regain: switching X focus to another window and back delivers OnApplicationFocus(false/true) (held keys released), no exceptions
- [x] Audio output: FMOD initialises and mixes through ALSA (verified with a private ALSA configuration whose default device writes PCM to a file through a null sink: the main-menu music is present). Without libpulse or an ALSA device, FMOD runs without output in both players
- [ ] Audio content compared with the original (positional world sounds, mixer snapshots)

## Deviations

- `SystemInfo.supportsComputeShaders` is false (the engine implements no compute shaders; the game reads it only for its hardware metrics and an unused image-effect check). `SystemInfo.graphicsMemorySize` is the dedicated video memory (the original printed a slightly lower figure here, likely the memory available at the time).

- Floating-point results are not bit-exact (out of scope, ARCHITECTURE §5).
- Runtime deviations inherent to CoreCLR (ARCHITECTURE §2.1).
- `ScreenCapture.CaptureScreenshot` with superSize > 1 (debug screenshot keys only): open item, PLAN.
