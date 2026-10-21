# Mod compatibility

This tracks the mods we have run in this player, and what happened.

**Where the list comes from:**
- mods players have reported (GitHub issues, Discord);
- the 100 most-subscribed Workshop mods in onimaxxing's catalog that are not flagged as malware.

Worldgen mods are out of scope.

**Levels:**
- **Played**: the save loaded and played with the mod, logs and screens compared with the original
  player.
- **Runs**: loaded with its batch and played 90 seconds of U57 (a large Spaced Out colony) with no
  error of ours.
- **Loads**: reaches the main menu with no error of ours; not yet played.
- **Broken upstream**: fails the same way in the original player (the mod needs an update).

Every run is on U57 with the Workshop build of each mod. "No error of ours" means the log has no
error that the original player's log lacks.

## Played

| Mod | Notes |
|---|---|
| Fast Track | 0 errors; about 30 fps, where the original runs 5–6 |
| Rocketry Expanded | 0 errors on U57 and on the Twinkleverse save it was played with |
| Synchronized Building Groups | side screen and group dialog are the same as in the original |
| Better Info Cards | its hover cards draw as in the original (needed ARCHITECTURE D17, D18) |
| True Tiles | its 2022.3 asset bundle loads |
| Unified Background, Preset Manager, Choose Neural Vacillator, Mod Profile Manager | issue #2's set, 0 errors with the mods above |

## Runs (top-100 sweep, batch 0)

Bigger Camera Zoom Out · Mod Updater · Build Over Plants · Show Building Ranges · DGSM · Research
Queue · Falling Sand · Better Automation Overlay · No Drop · Duplicant Stat Selector · Queue For
Sinks · Sweep By Type · select last care package · Customizable speed · Move This Here · Airlock
Door · GasOverlay · Bigger Building Menu · Plan Buildings Without Materials · MovableFeatures ·
Blueprints fixed · ChainedDeconstruction · Waste Not, Want Not · Geyser Calculated Average Output
Tooltip

## Loads (top-100 sweep, batches 1–3: play test pending)

Printing Pod Refresh · Mod Manager · German Translation · WGSM · Wounded Go To Med Bed · Crack
Reacher · Element Capture Capsule · Pliers fixed · Fast Save · Russian translation (Ony) · PT-BR
translation · Suppress Notifications · Clean Floors · Better Air Filters · 最后的补给包-Fix ·
Efficient Supply · Japanese Language Pack · Pip Plant Overlay · Traduction Française · Smart Drag
Tool · No Manual Delivery · Combined Conduit Display · Sandbox Tools · Starmap Queue · PrintPod
Select · Spanish translation · Favorite Resources · bottle emptier extend · No Long Commutes ·
Blueprints Expanded · Natural Plant Box · Insulated Door · Smart Mop · Full Miner Yield · Dup Stats
Configurator · Wallpaper · Flow Splitters · Korean language pack · Ronivans Legacy - Industrial
Revolution · Scaffolds · Speed Control 202107 · AI Improvements · Capsule 2.0 · More Marble
Sculptures · Chinese language pack · Cluster Generation Manager · Customize Geyser · Butcher &
Fishing Stations · Squirrel Wheel · Wall Pumps and Vents · Enable achievements · Pipe Flow Overlay ·
Demolish Neutronium · Simpler Pip Plant Rule · Smart Pumps · Tiny Door · FreeCamera · Remove
Sandbox Lock Mode · Freezer · Sealed Pitcher Pump · Drains · Auto-Eject · Immigrants Reroll ·
ConvenientScheduling · Rooms Expanded · Backwalls · No Research Alerts · Settings Change Tool ·
Steel Ladder

## Broken upstream

In both players, the game's crash reporter stops the run before the colony.

| Mod | Error |
|---|---|
| Deconstructable POI Props | Harmony patch of `Deconstructable.TriggerDestroy`, which now has two overloads (ambiguous match) |
| Refresh Immigrants | `STRINGS.UI.TOOLS.FILTERLAYERS.ALL` is no longer a field |
| Oxygen Not Included Together | `SetKAssertCB` is missing from the game's cimgui library |
| Buildable Natural Tile, Русификатор (Wiki) | `mod_info.yaml` still uses `lastWorkingBuild`, which KMod logs as an error |

## Known differences

- **Particle features no game data uses** (collision, lights, clamp velocity, sub emitters, some
  shapes): not simulated, and logged; mods' effects (Capsule 2.0) look simpler.
- **LineRenderer:** rounded corners and caps are drawn square.
- **Rephysicalized:** the reported build was the author's development build; not tracked.
