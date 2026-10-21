# Mod compatibility

This tracks the mods we have run in this player, and what happened.

**Where the list comes from:**
- mods players have reported (GitHub issues, Discord);
- the 200 most-subscribed Workshop mods in onimaxxing's catalog that it does not flag (malware,
  not loading, breaking).

Worldgen mods are out of scope.

**Levels:**
- **Played**: the save loaded and played with the mod, logs and screens compared with the original
  player.
- **Runs**: loaded with its batch and played 90 seconds of U57 (a large Spaced Out colony) with no
  error of ours.
- **Loads**: reaches the main menu with no error of ours; not yet played.
- **Broken upstream**: fails the same way in the original player (the mod needs an update).

Every run is on U57 with the Workshop build of each mod, installed as a local mod. That is the
nearest a test can get to a subscription; Dev-folder installs get developer checks that
subscribers never see. "No error of ours" means the log has no error that the original player's
log lacks.

## Played

| Mod | Notes |
|---|---|
| Fast Track | 0 errors; about 30 fps, where the original runs 5–6 |
| Rocketry Expanded | 0 errors on U57 and on the Twinkleverse save it was played with; clicks select what is under the cursor (its flat-scaled world selector) |
| Synchronized Building Groups | side screen and group dialog are the same as in the original |
| Better Info Cards | its hover cards draw as in the original (needed ARCHITECTURE D17, D18) |
| True Tiles | its 2022.3 asset bundle loads; tiles connect as in the original (needed ARCHITECTURE D21) |
| Oxygen Not Included Together | menu and U57 colony, 0 errors (needed its IMGUI and asset bundle API, ARCHITECTURE D20, and the TMP caret fix); a multiplayer session not tested (needs two Steam accounts) |
| Unified Background, Preset Manager, Choose Neural Vacillator, Mod Profile Manager | issue #2's set, 0 errors with the mods above |

## Runs (top-100 sweep)

Each batch of about 25 ran together. Batch 3, with Backwalls and Rooms Expanded, ran at 13 fps, where
the original ran 7.

Bigger Camera Zoom Out · Mod Updater · Build Over Plants · Show Building Ranges · DGSM · Research
Queue · Falling Sand · Better Automation Overlay · No Drop · Duplicant Stat Selector · Queue For
Sinks · Sweep By Type · select last care package · Customizable speed · Move This Here · Airlock
Door · GasOverlay · Bigger Building Menu · Plan Buildings Without Materials · MovableFeatures ·
Blueprints fixed · ChainedDeconstruction · Waste Not, Want Not · Geyser Calculated Average Output
Tooltip · Printing Pod Refresh · Mod Manager · WGSM · Wounded Go To Med Bed · Crack Reacher ·
Element Capture Capsule · Pliers fixed · Fast Save · Suppress Notifications · Clean Floors · Better
Air Filters · 最后的补给包-Fix · Efficient Supply · Pip Plant Overlay · Smart Drag Tool · No Manual
Delivery · Combined Conduit Display · Sandbox Tools · Customize Geyser · Butcher & Fishing Stations
· Squirrel Wheel · Wall Pumps and Vents · Enable achievements · Pipe Flow Overlay · Demolish
Neutronium · Simpler Pip Plant Rule · Smart Pumps · Tiny Door · FreeCamera · Remove Sandbox Lock
Mode · Freezer · Sealed Pitcher Pump · Drains · Auto-Eject · Immigrants Reroll ·
ConvenientScheduling · Rooms Expanded · Backwalls · No Research Alerts · Settings Change Tool ·
Steel Ladder · Starmap Queue · Buildable Natural Tile · PrintPod Select · Favorite Resources · No
Long Commutes · Blueprints Expanded · Natural Plant Box · Insulated Door · Smart Mop · Full Miner
Yield · Dup Stats Configurator · Wallpaper · Flow Splitters · Ronivans Legacy - Industrial
Revolution · Scaffolds · Speed Control 202107 · AI Improvements · Capsule 2.0 · More Marble
Sculptures · Cluster Generation Manager

## Runs (the second hundred)

The next hundred by subscriptions (Workshop ranks 115–226), in batches of about 20, run as
local installs. Two of them, Liquid Bottler and Auto Drop Gas Bottler Filler, both add a
liquid bottler; the game reports the duplicate (any player would).

Replace Floors · Traffic Visualizer · Debug Buttons · Thermal Tooltips · Aquatic Farm · Drywall
Hides Pipes · Research Manager · Dupery Fixed · Material Selection Properties · Algae Grower ·
MaterialColor · Reveal Map · Planning Tool · Stairs · Germicidal UV Lamps · Critter Inventory ·
Decor Pack I - Classy Glass · More AirFilter · Visual Fixes N Tweaks · Smart Dig · The Duplicant
Reacharound · Free Resource Buildings - Updated · No Mop Limits · More Small Sculptures · Rotate
Everything · Double Sweeper Range · Double Bed · BalanceGasPumpConsumption · Better Refresh · Geyser
Control · EmptyWorlds (SkyBlock) · PAUSE on ready to print · Advanced Electrolyzers · Deselect New
Materials · Big Storage: Restoraged · Schedule Master · Better Deselect · Drag Area More Visible ·
Pipette · Achievement Enabler · Oil Well - Any Water · Conveyor Rail Filter · Liquid Bottler · Piped
Everything · Rational Priority · Mass Move Tool · Free Resource Buildings Branch · Colorful
Shinebugs · 刷新打印舱 · Athletic Manual Generator · Better Air Filters updated · 技能自定义-fix · 技能自定义 ·
Auto Drop Gas Bottler Filler and Liquid Filler · Pliers 2021 · Faster Jetpacks · Multiple Power
Generator · AllBiomesWorld - Fuleria · Duplicant Status Bar · Big box · Wrangle & Carry · Smart Egg
Incubator · Customize Buildings · Paint Walls · Suit Recharger · Storage Pod · Rebuildable AETN ·
Better Repair · Real Insulation Tile · Closest Material · Solar System Worlds · Hideable Heavy-Watt
· Asphalt Tile

## Broken upstream

Each fails the same way in the original player.

| Mod | Error |
|---|---|
| Deconstructable POI Props | Harmony patch of `Deconstructable.TriggerDestroy`, which now has two overloads (ambiguous match) |
| Refresh Immigrants | `STRINGS.UI.TOOLS.FILTERLAYERS.ALL` is no longer a field |
| Bottle Emptier Extend | calls `TreeFilterable.GetTags()`, which the game no longer has |
| 自动收获 Fixed, automatic harvest, Expanded Lights | Harmony patches whose targets now have overloads (ambiguous match) |
| Stats Unlimited | a Harmony patch whose target method is gone |
| Map Overlay | `OverlayModes.Mode.CreateDefaultFilters` returns another type now |
| Portal Network | its animations fail to load, and its asset loading throws |
| Self-sealing Airlocks Revived, Fast Insulated Self Sealing AirLock | call `SimMessages.SetCellProperties(int, byte)`, which the game no longer has |
| Font Loader for Traditional Chinese, 字型模組。新 | use TextMeshPro's `FaceInfo`, which the game's TextMeshPro lacks |

## Translations

Translation packs (German, Russian, Portuguese, Japanese, French, Spanish, Korean, Chinese, and
others) are not tested. The game applies a translation only from a Steam subscription. Installed
locally, KMod marks it failed, and the main menu's mod report throws, in the original player too.

## Known differences

- **Particle features no game data uses** (collision, lights, clamp velocity, sub emitters, some
  shapes): not simulated, and logged; mods' effects (Capsule 2.0) look simpler.
- **LineRenderer:** rounded corners and caps are drawn square.
- **Rephysicalized:** the reported build was the author's development build; not tracked.
