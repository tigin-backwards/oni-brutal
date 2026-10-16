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
- [x] Supply Closet "All" (`KleiInventoryScreen`: blueprint categories, building diorama with the animate-in, details) and Wardrobe (`OutfitBrowserScreen`; its initial outfit is random) as the original
- [x] `PatchNotesScreen`: unreachable in this build (`ShouldShowScreen` returns false); `KleiItemDropScreen`: needs claimable items from a Klei account (the offline test double has no valid ticket), unreachable in both players
- [x] `RetiredColonyInfoScreen`: colony list, achievements, colony detail (Duplicants with portraits, buildings, oxygen graph); colony cards show the timelapse thumbnail
- [x] Tutorial videos (`VideoScreen` from a message's video widget, render-texture output) play as the original, with libvpx available (verified with Ubuntu's libvpx9 unpacked locally)
- [x] Intro short on first launch: this build sets no intro short (`MainMenu.IntroShortName`), so neither player plays one (no `PlayShortOnLaunch` pref written by either)
- [x] Victory videos: the Thriving sequence's short and victory loop with its message panel (`VideoScreen.PlayShortWithVictoryLoop`), triggered with tools/UnityProbe in both players and captured at the same points

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
- [x] Thought and conversation bubbles: pop-in by the legacy `Animation` component (the game's only legacy clips), icons inside the bubble at the clip's final scale
- [x] `TopLeftControlScreen`, `DateTime` (cycle clock), `MeterScreen` (Duplicants, stress, rations)
- [x] `SpeedControlScreen` (pause, speeds 1–3)
- [x] `ColonyDiagnosticScreen` panel, `PinnedResourcesPanel` / resources panel
- [x] Notifications (`NotificationScreen`), tutorial messages
- [x] `DetailsScreen` (selection: status, properties tabs with nested canvases and RectMask2D)
- [x] `HoverTextScreen` (element hover cards)
- [x] `SideDetailsScreen`: Ration Box element filter (unticking → "Filters Not Designated"), capacity slider (drag → new capacity), Material tab
- [x] Liquid Valve flow control side screen: a slider click sets 4673 g/s in both players
- [x] Pneumatic Door access control (Duplicant rows with portraits, Open/Auto/Lock) and Thermo Sensor threshold side screens render and respond (not yet compared with the original)
- [x] Side screens of 18 buildings built and selected by tools/UnityProbe in both players (door access, thermo/atmo sensors, signal switch, storage bin, liquid valve, cot and outhouse assignment, manual generator and smart battery thresholds, research station, microbe musher and electric grill orders, deodorizer, electrolyzer, flower pot and planter box seeds, refrigerator) match the original
- [x] `BuildMenu` / `BuildingGroupScreen` / `ProductInfoScreen` / `MaterialSelectionPanel`: category menus, building info, material choice and placement (Storage Bin, Ladder, Dev Life Support, wire tool)
- [x] Management screens opened by hotkey and compared with the original: Vitals, Consumables, Priorities (jobs), Schedule (`ScheduleScreen`), Skills (`SkillsScreen`, Duplicant portrait in a stencil mask), Database (`CodexScreen`), Reports (`ReportScreen`)
- [x] Research and Starmap buttons before a Research Station / Telescope exists: locked, with the original's tooltips
- [x] Research tree interaction: hover, tooltips, selecting a tech (instant completion in debug mode) as the original
- [x] Research progress: Basic Farming at 7.5/15 points (set through `Research`) shows the same progress bar and search results in the research screen as the original
- [ ] Research points accumulated by a Duplicant at a powered Research Station
- [x] `ClusterMapScreen`, `WorldSelector`: Spaced Out content is not installed in this game copy (no `StreamingAssets/dlc`), unreachable in both players
- [x] `PauseScreen`: open, save with overwrite confirmation (41 saves in one run)
- [x] `PauseScreen` → Main Menu ("Save and quit" prompt) → front end with Resume Game; the front end comes back at the smaller UI scale as in the original (once, ours returned to the normal scale a few seconds later; not reproduced since)
- [x] `PauseScreen`: Save As (save slots, New Save, typed name in `FileNameDialog`), Load (colony list, colony view, Load Game → the save loads in game)
- [x] `PauseScreen`: Options (the same options screens as the main menu)
- [x] Printing pod recharge (cycle 3–4): "New Printables are available", pod side screen "Choose a Blueprint" → selection screen with Duplicants and a care package → Print → the new Duplicant arrives
- [x] `GameOverScreen` (colony lost)
- [x] `StoryMessageScreen` ("IMPERATIVE ACHIEVED" alert, Proceed → camera sequence) as the original, triggered with tools/UnityProbe; `VictoryScreen` is never opened in this build (prefab field only)
- [x] `EventInfoScreen.ShowPopup` (the Somnium Synthesizer story-trait popup) as the original, triggered with tools/UnityProbe
- [x] Timelapse capture and save preview (`Timelapser`: base and overlay cameras rendered from an end-of-frame coroutine into a render texture, `ReadPixels` + `EncodeToPNG`): the world around the printing pod as in the original's images
- [x] Camera: wheel zoom out/in, keyboard pan, Home (camera return)
- [x] Camera: right- and middle-button drag pan move the view by the same pixel offsets as the original (−200,−100 and −250,+200, the latter clamped at the world edge, measured by image registration); Ctrl+1 / Shift+1 bookmark returns to the exact view in both. ONI has no edge pan (the edge padding only clamps the camera)
- [x] Camera follow: stress meter click selects and focuses a Duplicant, Follow Cam zooms in and keeps the Duplicant centred while it walks

## Tools

- [x] Dig (`DigTool`)
- [x] Activation with tool label, filter panel and overlay: Cancel, Deconstruct, Prioritize (`PriorityScreen`), Disinfect (germ overlay), Sweep (`ClearTool`), Mop, Attack, Wrangle (`CaptureTool`), Harvest, Empty Pipe, Disconnect (compared with the original where its scripted run activated the same tool)
- [x] Move To (`MoveToLocationTool`): the target sets the "Move to" errand and the Duplicant walks there; an unreachable target keeps the tool active with "UNREACHABLE" (seen in the original)
- [x] Deconstruct (instant-build mode: the Storage Bin goes, its material drops as debris), Sweep (marker on the debris), Cancel (removes the sweep order; the filter gains Sweep & Mop / Dig Orders)
- [x] Copy Settings: Storage Bin capacity set by a slider click (9911 kg in both players) copied onto a second bin as in the original; the element filter lists only Critter Egg at that point in both, so All Standard changes nothing in either
- [x] Mop order carried out (tools/UnityProbe: a 30 kg puddle beside the printing pod, Mop tool, 40 s at speed 3): mopped and the order removed in both players
- [x] Disinfect order carried out (Storage Bin with 100000 food-poisoning germs → 0 within 120 s at speed 3) in both players
- [~] Harvest (a fully grown plant marked Harvest When Ready) and Attack (a Shine Bug marked) are not carried out within 120 s at speed 3 in either player — same behaviour, outcome not observed; Wrangle needs a Duplicant with the Critter Ranching skill
- [x] Build placement with materials: Storage Bin and a dragged Ladder from the Base menu; placement refused with "Missing resources" until the colony has the materials (as the original); Duplicants deliver and build
- [~] `BaseUtilityBuildTool`: the wire tool (Power → Wire, material list, power overlay) opens and responds to a drag as in the original; a placed wire has not been observed in either player's scripted run yet
- [x] Liquid pipe drag (plumbing overlay, pipe preview, built in instant-build mode); building rotation (O turns the Liquid Valve preview from vertical to horizontal and it is built rotated); build menu search and the grid layout with everything researched

## Overlays

- [x] F1–F11 compared with the original (same keys, same frame of play): Oxygen, Power, Temperature, Materials (`TileMode`), Light, Plumbing (`LiquidConduit`), Ventilation (`GasConduit`), Decor, Germs (`Disease`), Farming (`Crop`), Rooms (with room highlight under the cursor)
- [x] Research-gated overlays with everything unlocked (debug instant-build mode, compared with the original in the same mode): Exosuits, Automation, Shipping, and the remaining Shift+F overlays
- [x] Build menu categories that become available later (Utilities, Automation, Shipping, Rocketry) appear and lay out as in the original
- [x] `ResearchScreen` tree and `StarmapScreen` open as in the original
- [x] Overlay legends (`OverlayLegend`) shown with every overlay compared above
- [x] Overlay filter toggles: Temperature overlay modes (Absolute, Relative with its range slider, Thermal Tolerance, State Change) switch the view and legend as the original

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
- [x] Exclusive fullscreen (`-window-mode exclusive`, or a script request): as in Unity's Linux player it gives a fullscreen window, and the game reports FullScreenWindow like the original
- [x] Focus loss and regain: switching X focus to another window and back delivers OnApplicationFocus(false/true) (held keys released), no exceptions
- [x] Audio output: FMOD initialises and mixes through ALSA (verified with a private ALSA configuration whose default device writes PCM to a file through a null sink: the main-menu music is present). Without libpulse or an ALSA device, FMOD runs without output in both players
- [x] Audio content compared with the original, recorded in real time through a private paced ALSA sink (`scripts/audio-sink/record.sh`): main-menu music matches the original's (10 ms RMS envelopes correlate 0.97, aligned waveforms 0.90 over 30 s, level ratio 0.9997); colony ambience on the same seed has the original's level (RMS 0.035–0.040 against 0.036–0.043), stereo balance and spectrum (band energy shares within a few percent)

## Engine semantics

- [x] Lifecycle and timing measured in both players with `tools/UnityProbe` (a mod): Destroy timing (objects and direct children disabled at once, deeper ones at destruction, deepest first), objects awaiting destruction not re-enabled, request order of destruction, component Destroy, `Time.frameCount`/`renderedFrameCount`, startup frames (mods load at frame 2, frontend from frame 3), `yield return null` resuming the next frame, Start and LateUpdate in the same frame for behaviours created by coroutines: output identical apart from a time-based wait landing on another frame at another frame rate
- [x] Values (tools/UnityProbe): vector/colour/matrix formatting, maths, JsonUtility (see ARCHITECTURE D10), hierarchy queries, SetParent, Instantiate callback order, SendMessage, root sibling indices — identical
- [x] Main-menu UI layout (tools/UnityProbe UiDump): every RectTransform's rect and world corners and every text's content, size and preferred size across all canvases, compared with the original at 1024×768 — identical apart from TextMeshPro auto-size and one preferred width differing by 0.1 (float rounding)
- [x] In-game HUD layout (UiDump once the game clock has run 120 frames after embarking): 3415 RectTransforms of the overlay canvases (HUD, management screens, research tree) compared with the original — differences only 0.1-pixel rounding, the hover card's state at the pointer and game-state texts
- [x] Local mods load (KMod, `Assembly.LoadFrom`, Harmony patches applied on CoreCLR); see ARCHITECTURE D3 for their version stamp

## Debug mode

- [x] Dev tools (debug mode, Ctrl+\`): `ImGuiRenderer` creates its target (`RenderTextureDescriptor`, `GetTemporary`) as in the original; the game's Linux `cimgui.so` has no `SetKAssertCB`, so both players log `EntryPointNotFoundException` and then crash natively in `igRender` (the dev tools do not work in the Linux build). The command-buffer path ImGui draws with (`DrawMesh`, scissor rectangles, view/projection matrices, `Graphics.ExecuteCommandBuffer`) is implemented for a cimgui that has the entry point
- [x] Instant build (Ctrl+F4), Dev Life Support, Dev Generator in the build menu

## Deviations

- `SystemInfo.supportsComputeShaders` is false (the engine implements no compute shaders; the game reads it only for its hardware metrics and an unused image-effect check). `SystemInfo.graphicsMemorySize` is the dedicated video memory (the original printed a slightly lower figure here, likely the memory available at the time).

- Floating-point results are not bit-exact (out of scope, ARCHITECTURE §5).
- Runtime deviations inherent to CoreCLR (ARCHITECTURE §2.1).
- `ScreenCapture.CaptureScreenshot` with superSize > 1 (debug screenshot keys only): open item, PLAN.
