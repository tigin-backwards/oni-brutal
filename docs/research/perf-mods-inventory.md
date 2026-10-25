# Performance mods inventory (2026-10-25)

Every Oxygen Not Included performance mod we could find, grouped by technique, set against what
Moono already does ([PERFORMANCE.md](../PERFORMANCE.md), [TWEAKS.md](../TWEAKS.md),
[overlays.md](overlays.md), [u57-frame-inventory.md](u57-frame-inventory.md)), to find ideas
worth borrowing.

## Method

- **Found through:** Steam Workshop searches for ONI (appid 457140), 49 terms (performance, fps,
  lag, optimiz*, frame rate, stutter, memory, gc, thread, multithread, save, load, cpu, cache,
  性能, 优化, 卡顿, 帧, 多线程…), each sorted by relevance, most recent and most subscribed, two
  pages each: 1,497 items, filtered on titles and descriptions. Cross-checked against
  `scripts/mod-catalog.tsv` and onimaxxing.com/mods (its catalog of 3,031 mods and its per-mod
  code reviews, which list each mod's Harmony targets), and GitHub (the known authors'
  repositories and a search).
- **Read:** Workshop descriptions; onimaxxing's reviews of each mod's current upload (patch targets
  and what the code does); public source where there is some (Fast Track, ONI Perf, oni-gc-runtime,
  Overlay Texture Memory Fix, CycleTrim, GCBudget); the earlier decompile of Colony Performance
  Fixes (`out/local-tools/moddecomp/3823893994`, an older version than the current 1.7.1). No mod
  was downloaded for this survey: of those not already decompiled, only Fast Save and
  Multithreaded Simulation (RC-3.0) are in `scripts/mod-catalog.tsv`, and both are described well
  enough by their source or review.
- **Claims and measurements:** a mod author's figure is a **claim** (their colony, their machine,
  their method); a **measured** figure is ours, from PERFORMANCE.md or the research notes. Moono's
  current frame is about 12.6 ms on U57 at speed 3 with every tweak, on Windows; the
  per-system times below come from there unless marked as the older Linux profile (where the
  game's code ran about half as fast).
- **Exact** means the game's results stay the same (what our `exact` tweaks promise). Anything
  that changes when or how often the game decides something is not exact.

## The techniques

Verdicts: **covered** (Moono does it, or does better), **worth investigating** (with a plan),
**dropped before** (measured, see PERFORMANCE.md "Tried and dropped"), **not applicable** (the
cost doesn't exist in Moono, or the change is gameplay, not performance).

### Frame pacing and process settings

| Technique | Mods | What it changes | Exact? | Moono | Gains | Verdict |
|---|---|---|---|---|---|---|
| VSync off, target frame rate set (240, 120 or user value) | [Unlock FPS](https://steamcommunity.com/sharedfiles/filedetails/?id=3772228364), [FPS Limiter](https://steamcommunity.com/sharedfiles/filedetails/?id=3405082060), [Performance Assist](https://steamcommunity.com/sharedfiles/filedetails/?id=3772297140) | `QualitySettings.vSyncCount = 0`, `Application.targetFrameRate` | yes (a display setting) | covered: the player paces by vSync, else `targetFrameRate`, and `--max-frame-rate` caps either (`OniBrutal.Host/Player.cs`, `Throttle`); these mods work through the same API | none to frame time; caps a menu running at thousands of fps | covered |
| Process priority High, re-applied every 10 s | Performance Assist | OS scheduling | yes | not done | claim: "may reduce stutter" from background contention; none measured | not applicable (the player's choice, not the engine's) |
| Full speed while the window is unfocused | [Colony Performance Fixes](https://steamcommunity.com/sharedfiles/filedetails/?id=3823893994) (option, off), Performance Assist (sets `runInBackground` as a side effect) | stops the game's own sleep while unfocused | no (the game deliberately slows itself) | the game's behaviour is kept; PerfProbe turns it off only for measuring | not a frame-time gain | not applicable (a preference; could be a player option) |
| Adaptive game speed held to a target fps, benchmark per save | [Controlled Framerate](https://steamcommunity.com/sharedfiles/filedetails/?id=3670573352) | `Time.timeScale` every frame | no (changes game speed) | — | — | not applicable (a feature, works as a mod) |
| Unity job workers raised (`job-worker-count` in boot.config) | [ONI Boost Kit](https://steamcommunity.com/sharedfiles/filedetails/?id=3760687013) | Unity's native job threads (skinning, culling) | yes | the engine has no Unity native jobs; the game's own job threads are covered by `fast-jobs` | claim only | not applicable |

### Garbage collection

| Technique | Mods | What it changes | Exact? | Moono | Gains | Verdict |
|---|---|---|---|---|---|---|
| A rebuilt Mono runtime with parallel marking | [oni-gc-runtime](https://github.com/Salacious-Oni-Mods/oni-gc-runtime), ONI Boost Kit | replaces `mono-2.0-bdwgc.dll` | yes | covered by the runtime: CoreCLR's collector runs in parallel on every core, pauses about a quarter as long, about 1.4% of a frame (PERFORMANCE "Why the engine alone is faster") | claim: a full collection 220–240 → 30–50 ms on a 2–3.5 GB heap (oni-gc-runtime) | not applicable |
| Incremental GC (`gc-max-time-slice=3`) | Colony Performance Fixes (option, off) | spreads Boehm's collection over frames | yes | as above | claim: GC freezes about 370 → 60 ms | not applicable |
| GC latency mode, manual mode with collections on a timer or heap budget, a collection at focus loss | Performance Assist (`SustainedLowLatency`), [BetterSave](https://steamcommunity.com/sharedfiles/filedetails/?id=3805601277) (GC disabled, a full collection every 10 min), [GCBudget](https://github.com/shikyo13/oni-gc-budget), [Clean Memory & Performance Capture](https://steamcommunity.com/sharedfiles/filedetails/?id=3091570212), GC Timing (in oni-gc-runtime) | when collections happen | yes | the engine maps `GarbageCollector.GCMode` onto CoreCLR (`ManagedHeap`), so these modes still do what they ask; the collector's pauses are what they work around | claim (BetterSave): a 0.5 s collection every 50 s → every 10 min, at about 1 GB more memory | not applicable |
| The collector held off while a colony loads | [ONI Perf](https://github.com/Salacious-Oni-Mods/oni-perf-mod) `loadGcHold` (24 GB or more installed) | one collection at the end of the load | yes (memory grows) | measured and dropped: U57's load pauses 3.2–3.9 s in collections, but a larger gen0 budget or the non-concurrent collector loaded no faster, and holding collections would keep 12 GB of 2minnow's garbage (PERFORMANCE "Tried and dropped") | claim: Euphoria (239,000 objects) about 730 → 111 s on Mono | dropped before |
| The forced collection after each save skipped | BetterSave, Fast Save | `GC.Collect` at the end of `SaveLoader.Save` | yes | covered: `no-save-gc` | | covered |

### Saving and loading

| Technique | Mods | What it changes | Exact? | Moono | Gains | Verdict |
|---|---|---|---|---|---|---|
| Compression and the file write on a background thread | [Fast Save](https://steamcommunity.com/sharedfiles/filedetails/?id=1867707267) (Background Save, autosaves only), BetterSave | `SaveLoader.CompressContents`, the write | yes (the file is the same; write errors only logged in BetterSave) | covered: `background-save`, `fast-save-compression` | measured (ours): autosave freeze 11.7 → 4.8 s with the save tweaks (Linux), 2.3 s on Windows; claim (BetterSave): 3.6 → 0.56 s on a 103 MB save | covered |
| Compiled serialization delegates, type names cached as bytes | Fast Save (Use Delegates, "may crash"), BetterSave (checked byte for byte against vanilla on the first two saves) | `SerializationTemplate.SerializeData`, `Manager.GetSerializationTemplate` | yes | covered: `fast-serialization`, `fast-save-type-checks` | | covered |
| A reused save buffer | BetterSave | the `MemoryStream` the save is written into | yes | to measure: allocation per autosave on U57 | claim only | worth investigating (small; measure the autosave's allocations first) |
| The colony preview: async GPU read-back, PNG encoded in the background | Fast Save (`Timelapser.RenderAndPrint`, autosaves) | when the pixels are read and the PNG written | yes (same image, later) | not done; the remaining freeze includes "the colony preview's GPU read-back" (PLAN.md, save tweaks) | claim (BetterSave): the preview's stall about 1.1 s on its first save | worth investigating |
| The preview captured once a session, then copied | BetterSave | the save list's thumbnail goes stale | no (the preview image) | — | as above | not applicable (prefer the exact read-back) |
| Old daily reports pruned or emptied | Fast Save (Moderate: 20 kept, Aggressive: 4), [RemoveDailyReports](https://steamcommunity.com/sharedfiles/filedetails/?id=1870873514) (10 kept) | the save's report history | no (report screens, some achievements, the save's contents) | — | claim (Fast Save): saving and loading "up to 50%" faster late game | not applicable as a tweak; to measure what U57's 17,000 reports cost the save, for the record |
| Conveyor network rebuild from an index | ONI Perf `conveyorPaths` | `SolidConduitFlow` rebuild's path search | yes | covered: `fast-conveyor-paths` (ONI Perf's README lists it as its own; the same design) | measured (ours): a rebuild 50–510 → 2–14 ms, the load's 840–850 → 14 ms | covered |
| `Type.GetType` cached for the state-machine load; the restore's 400-character type-name strings decoded once and shared | ONI Perf `typeNameCache`, `smRestore` | `StateMachineSerializer` restore | yes (same strings, same choices) | not done; the loading side of `fast-serialization` was measured and not kept (31.3 s against 31.6, PLAN.md), but that did not cover the state-machine restore | claim: part of the default load levers' gain (Some light torture 52 → 43 s, with `loadGcHold`) | worth investigating (profile U57's load for `StateMachineSerializer` and `Type.GetType` first) |
| The load spread over frames, far objects built after the loading screen | ONI Perf `fastLoad` (experimental, off) | object spawn order and timing | no (objects far away appear after the colony starts, paused) | — | claim: Euphoria's window answers after 16.6 s instead of 110 s | not applicable (Moono's U57 load is 35 s; not exact) |

### Overlays

| Technique | Mods | What it changes | Exact? | Moono | Gains | Verdict |
|---|---|---|---|---|---|---|
| Temperature overlay: only what's in view coloured, a quarter a frame, changes of 0.05 K or more | Colony Performance Fixes | `Infrared.Update` and the visualizers' event | no (colours up to four frames late, small changes not shown) | covered exactly: `fast-overlays` passes over out-of-view buildings without reading them | measured (ours): `Infrared.Update` 10.4 → 6.0 ms on U57; claim (CPF): overlays 29–41 → 60–95 fps | covered |
| Materials, decor, power and pipe overlays refreshed on camera moves or at 4 Hz; power overlay at 0.5 Hz | Colony Performance Fixes, Clean Memory & Performance Capture | the modes' `Update` | no (targets, tints and labels up to 0.25 s or 2 s old) | covered exactly where it paid: `fast-overlays` keeps what the targets' visits read (overlays.md) | measured (ours): materials 4.05 → 3.8, decor 4.32 → 3.6–4.0 ms | covered (the throttle itself isn't exact) |
| A circuit's wattage computed once a frame | Colony Performance Fixes | `CircuitManager.GetWattsNeededWhenActive` | almost (cleared on `Refresh` only) | covered exactly: `fast-overlays` keeps it within one `Power.Update` | measured (ours): 0.52 → 0.07 ms | covered |
| Displaced render targets released when overlays switch the MRT setup | [Overlay Texture Memory Fix](https://github.com/pastadiablo/ONI-OverlayTextureMemoryFix) | `MultipleRenderTargetProxy.CreateRenderTarget` leaves the replaced `TexturesCopies` alive; the fix releases and destroys them | yes (a leak fix; nothing reads the old textures) | not done; the engine frees a texture when the game destroys it, as Unity does, so the leak should be the same | claim: 74 MB committed per toggle of the temperature or germ overlay, until the main menu | worth investigating (memory, not fps) |

### Animation

| Technique | Mods | What it changes | Exact? | Moono | Gains | Verdict |
|---|---|---|---|---|---|---|
| Idle animations advanced together at about 30 Hz, so their batches upload on fewer frames | Colony Performance Fixes (on by default) | `KBatchedAnimUpdater.LateUpdate`'s loop | no (an animation's frame steps shift by up to a frame, and so do its frame events) | not done; `fast-anim-batches` and `fast-symbol-overrides` cut the cost of each upload, not how often | claim (CPF): the normal view about 70 → 85–90 fps, with its other fixes; ours: animation updates 0.9 ms, batch uploads 0.6 ms at 79 fps | worth investigating (count batch uploads a frame at 79 fps first) |
| Inactive controllers with no batch taken off the anim updater's list | ONI Perf `parkAnims` | `KBatchedAnimController.UpdateAnim` | claimed yes (the calls removed do nothing); the updater's list order changes | not done | claim: 280 of 646 `UpdateAnim` calls a frame were no-ops, on 200 Duplicants | worth investigating (count such calls on Gluttony's 67 Duplicants) |
| `CellChangeMonitor.MarkDirty` stops at inactive children without a cell-change handler | ONI Perf `markSkip` | an inactive laser effect reads "not moving" | almost: no cell event is lost; an inactive object's moving state differs | not done | claim: 73% of transforms marked as moved were switched-off laser effects (14 per Duplicant); −0.20 to −0.40 ms | worth investigating (count them on Gluttony, with a verify mode) |
| Symbol overrides re-derived only when changed; only the changed range uploaded | ONI Perf `symbolMemo`, `overrideRange` | per-Duplicant override rebuilds and uploads | yes | covered: `fast-symbol-overrides` (unchanged blocks aren't uploaded) | measured (ours): 1.47 → 1.20 ms; claim: −0.98 to −2.14 ms and −0.38 to −0.59 ms | covered |
| Looping animations whose frames are identical played once | Fast Track `AnimOpts` | `KAnimLoopOptimizer` | no (anim-complete events, repeating sounds) | reviewed (u57-frame-inventory.md, "Fast Track reviewed") | | dropped before |
| Animation batch cleanup, spatial index cleanup | Colony Performance Fixes ("anim batch cleanup", "partitioner cleanup", on by default) | `KBatchedAnimUpdater` cleanup, `ScenePartitioner` | likely the same as ours (not visible in our decompile, which predates them) | covered: `fast-anim-cleanup`, `fast-partitioner-cleanup` | measured (ours): 0.48 → 0.12 ms, Gluttony 0.38 → 0.16 ms | covered |

### Interface and per-frame housekeeping

| Technique | Mods | What it changes | Exact? | Moono | Gains | Verdict |
|---|---|---|---|---|---|---|
| Closed screens put to sleep | ONI Perf `screenSleep` | disables closed screens' canvases, toggles, graphs, scroll views | no (the game's own components disabled) | covered in the engine: the UI walk, rect masks and raycasts skip closed screens; what is left (the toggles' and portraits' scripts, about 0.7 ms on 2minnow) needs disabling game components (PERFORMANCE "Tried and dropped") | measured (ours): masks 6.3 → 0.3–0.4 ms on 2minnow; claim: +17.95 ms a frame after one visit to Consumables | covered |
| The UI hover raycast reused while the mouse is still (up to 0.1 s), 30 Hz while it moves | Colony Performance Fixes, ONI Perf `raycastCache` | `StandaloneInputModule`'s pointer raycast | no (hover state can lag a screen change by 0.1 s) | covered in part: the raycast leaves out closed screens' graphics | measured (ours): U57 2.0–2.3 → 0.6–0.9 ms; claim (CPF): about 1 ms each time | covered (what's left isn't exact) |
| The hover card rebuilt only when something it shows changes | ONI Perf `hoverCache`, Fast Track `InfoCardOpts` | `SelectToolHoverTextCard` | no (it shows live mass and temperature) | measured: about 0.3 ms | | dropped before |
| Property textures refilled on a sim tick or view change | ONI Perf `propTex`, Fast Track `ReduceTileUpdates` (threads it) | `PropertyTextures.LateUpdate` | no (revealed cells, lights change between ticks) | measured: 0.5–0.9 ms in all; the engine already skips uploading unchanged rows | | dropped before |
| Unused cameras off, lerper passes as blits | ONI Perf `idleCams`, `lerpBlit` | the timelapse, sim overlay and cursor cameras; six texture lerpers | yes | measured: together under 0.25 ms | | dropped before |
| Idle trackers, heat effects, workers, outfits and progress bars put to sleep; pipe contents cached while paused | ONI Perf `idleSleep`, `workerSleep`, `barRate`, `pipeCache`, `pausedSkip` | their per-frame updates | yes, or paused only | measured: 0.02–0.12 ms each | | dropped before |
| Steam Input polled at 20 Hz while no pad is in use; Steam callbacks at 30 Hz | ONI Perf `steamPoll` | `SteamInput.RunFrame`, `SteamAPI.RunCallbacks` | gameplay yes; a pad tap under 50 ms can be missed, Steam callbacks arrive up to 33 ms later | not done; the engine calls the real Steam API (ARCHITECTURE, Steamworks.NET), so the native cost is the same | claim: 0.41–0.49 ms and 0.38–0.46 ms a frame; ours bounded: `Global.Update` (which holds the input update) is 0.3 ms in all on U57 | worth investigating (time both calls first) |
| Achievement checks at a fixed rate | ONI Perf `achieveRate`, Fast Track `DisableAchievements` | `ColonyAchievementTracker` | no (an achievement can register seconds later) | to measure | claim: −0.10 ms | not applicable (too small, and not exact) |
| The audio mixer's visible-Duplicant count at a fixed rate | ONI Perf `mixRate` | `AudioMixer.SetVisibleDuplicants` | no (mixer parameters up to 1/Hz late) | to measure | claim: 0.10 ms with 200 Duplicants | not applicable (too small) |
| Menu expand animations skipped | [Lightning fast Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=1852077143) | `ExpandRevealUIContent` curves | no (visual) | — | | not applicable (and doesn't load: no mod_info.yaml) |

### Simulation and Duplicant logic

| Technique | Mods | What it changes | Exact? | Moono | Gains | Verdict |
|---|---|---|---|---|---|---|
| Brains at the rate the game was tuned for (30 a second of real time), not once per rendered frame | ONI Perf `brainRate` (off by default), CycleTrim (critters only: at most 5 a frame and 400 a second) | `BrainScheduler`'s probe count: 1 Duplicant brain and 5 critter brains per rendered frame, with no `dt` term (`BrainScheduler.cs` in the game; its own constants state a 30 fps frame) | no (chore searches happen less often per real second) | not done. Moono runs about 7 times the original's frame rate on U57, so it already searches chores about 7 times as often per game second as the original; capping at 30 a second would still search more often than the original does | measured (ours): brains 1.4 ms of the 12.6 ms frame on U57, 2.4 ms on Gluttony; claim (ONI Perf): every lever, `brainRate` and `moveHold` included, 54.9 → 123.8 fps against 77.2 with the defaults; `brainRate` alone not measured | worth investigating (a non-exact tweak) |
| Busy Duplicants' pickup and chore refreshes throttled; pickup sensor every 3rd brain update; every sensor every 2nd | [CycleTrim](https://steamcommunity.com/sharedfiles/filedetails/?id=3807166689), [Lag Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3781498375) | `PickupableSensor.Update`, `Sensors.UpdateSensors`, chore searches | no (new errands and items noticed brain updates later) | not done; sensors' scans measured at 0.064 ms a frame on U57 (u57-frame-inventory.md), fetch updates 0.5 ms with `fast-offset-tables` | claim (CycleTrim): brain time 5.1 → 2.4 s, fps 70.6 → 85.1 | not applicable on its own; a brain-rate cap (above) covers the same cost more uniformly |
| Stationary critters' path probes throttled; repeated async probes deduplicated | CycleTrim | `Navigator.UpdateProbe`, `AsyncPathProber` | no (eight skips then a forced refresh) | covered in effect: the path prober runs on its own thread, about 0.02 ms of the main thread to hand out its work (PERFORMANCE "Where a frame goes now") | claim: Navigator hot path 827 → 48 ms total, fps 81 → 109 | not applicable (would only save worker-thread time) |
| Pickup candidate costs cached in `UpdatePickups` | CycleTrim, Fast Track `FastUpdatePickups` | `FetchManager.FetchablesByPrefabId.UpdatePickups` | CycleTrim: claimed yes | reviewed: runs on the game's worker jobs already | claim (CycleTrim): 41.3 → 35.7 µs a call | not applicable |
| Nav-grid links rebuilt in parallel for batches of 64 cells or more | Lag Fix | `NavGridUpdater.UpdateLinks` | yes | measured and dropped: each update is about 40 µs, too small for a fork and join (PERFORMANCE "Tried and dropped"); big batches are rare | | dropped before |
| The round-robin nav-grid update restored | Lag Fix (experimental, off) | `Pathfinding.UpdateNavGrids` | no | — | | not applicable |
| Reachability checked only when something nearby changed | Fast Track `FastReachability` | `ReachabilityMonitor` | no (its own prober, refreshes every 4 s) | covered by `slow-reachability` (non-exact, the delay the project accepts) | measured (ours): 1.88 → 0.53 ms | covered |
| A walking Duplicant stays "moving" between sim sub-ticks | ONI Perf `moveHold` (off) | the stopped event delayed up to 0.1 s | no | — | not measured on its own | not applicable |
| Drained generators skipped when circuits draw power | Colony Performance Fixes ("skip drained generators", on by default; not in our decompile) | `CircuitManager.PowerFromGenerator`, `Sim200msLast` | unknown until read | not done | power 1.6 ms for 109 generators (older Linux profile) | worth investigating (read the game's `PowerFromGenerator` for side effects of a zero draw) |
| Rocket condition results cached | Colony Performance Fixes ("rocket status cache", on by default) | `CraftModuleInterface.EvaluateConditionSet` | unknown | to measure | none | worth investigating (time it on U57's rockets first; drop if under 0.1 ms) |
| A rewritten, multithreaded native simulation | [Multithreaded Simulation (RC-3.0)](https://steamcommunity.com/sharedfiles/filedetails/?id=3779276157) (Rust, [source](https://github.com/KeringFa/ONI-SimDLL_Rust)), [Multi threaded simulation](https://steamcommunity.com/sharedfiles/filedetails/?id=3563131181) (C++), CycleTrim's OniSim (Rust, default on in 0.4.0) | replaces SimDLL: heat, gas, liquid, element simulation | no for the C++ one (its author says results differ); OniSim claims identical final states on its own fixtures, untested in real games | Moono runs Klei's SimDLL; on U57 the main thread waits on it about 1.0 ms a frame | claim (OniSim): CPU −9% to −52% per scene on fixtures; ONI Perf measured 54.94 against 54.91 fps with a faster sim | not applicable (outside the engine; the wait bounds any gain at about 1 ms on U57) |
| Duplicants twice as fast, tasks twice as fast, more food | [Better Performance](https://steamcommunity.com/sharedfiles/filedetails/?id=2785899934) | gameplay tuning, so lower speeds suffice | no | — | — | not applicable (and fails to load on this build) |

### Fast Track's other options

[Fast Track](https://github.com/peterhaneve/ONIMods) (0.18.5 beta, MIT) read option by option
from its source, against the decompiled game. AnimOpts, FastReachability, FastUpdatePickups,
ConduitOpts, FastCellChangeMonitor and the visual and feature switches were reviewed before
(u57-frame-inventory.md). One correction there: FastUpdatePickups also replaces
`FetchListStatusItemUpdater.Render200ms` on the main thread, and not exactly (it sums
`TotalAmount` where the game sums `FetchTotalAmount`, and drops the game's skip of objects with no
world), so the status items it sets can differ.

As written, none of these options keeps the game's results, except SensorOpts, which never
takes effect in this build. The rows that point at an exact version Moono could write say so.

| Option | What it changes | Exact? | Moono cost | Verdict |
|---|---|---|---|---|
| PickupOpts, ChorePriorityMode | Duplicant brains' fetch updates on worker threads during `World.LateUpdate`, `BrainScheduler` replaced, anim-complete triggers deferred, priority brains limited | no (brain order and timing; the fetch list loses the game's de-duplication) | brains 1.4 ms, fetch updates 0.5 ms (`fast-offset-tables` covers part) | not applicable |
| ENetOpts | `CircuitManager.Refresh` and `Sim200msLast` replaced, battery drains batched | no (power values and unpowered flags; powered at 0 J where the game allows 0.01) | not measured | not applicable (a building check) |
| FlattenAverages | amounts' batch update on its own jobs; accumulators start at NaN; `IrrigationMonitor.UpdateIrrigation` rewritten as one pass (`PlantElementPatches.cs:109`) | no (averages in the first 3 s; the irrigation rewrite multiplies in another float order, which can flip its threshold) | plant element absorbers 1.8 ms (older Linux profile), mostly the storage-change event into irrigation monitors | worth investigating: an exact one-pass irrigation update in the game's float order; first split the 1.8 ms between the event dispatch and the handler |
| FastAttributesMode | dictionaries for attribute levels and converters; `StandardWorker.Work` experience rewritten | no (skips the `maxAttributeLevel == 0` check, so some Duplicants gain experience; matches by Id, not reference) | part of workers doing work, 1.6 ms (older Linux profile) | worth investigating, low: an exact side table keeping the game's checks |
| CritterConsumers | `GasAndLiquidConsumerMonitor.FindTargetCell` as a best-cost query | no (other target cells) | critter brains | not applicable |
| SensorOpts | `SafeCellQuery.IsMatch` stops once every flag is found | yes, but the path finder already stops at such a cell, so it never fires | none | not applicable |
| LogicUpdates | comet detectors and smart reservoirs send logic signals only on change | no (handlers of a same-value send stop running) | tiny | not applicable |
| MiscOpts | suit markers at 200 ms, incremental achievement checks, the `Chatty` tag moved, `WorldInventory.Update` skipped while paused, small caches | mostly no | not measured | worth investigating, measure first: `ColonyAchievementTracker.RenderEveryTick` (one unfinished achievement a frame; some walk every building, U57 has 62,000) and `SuitMarker.Update` (each checkpoint finds its lockers every frame; caching them, invalidated on building changes, would be exact) |
| ReduceColonyTracking | `TrackerTool` 50 → 10 world trackers a frame, `ChoreDriver` reports at 1 s | no | not measured; some trackers walk every network or chore | worth investigating, measure first; only exact speed-ups of single trackers would do |
| AllocOpts, MinimalKCV, CustomStringFormat | fewer allocations (pooled lists, `Cmps` add and remove, Ryu number formatting) | no (shared lists, `Components` list order, number text) | GC pressure, which matters under Mono's Boehm collector | not applicable |
| ParallelInventory | `WorldInventory.Update` on a worker thread, some tags left out | no | not measured | not applicable |
| BackgroundRoomRebuild, DecorProvider patches | rooms flood-filled in the background, decor refreshes queued | no (room timing) | not in the profile | not applicable (rooms are building checks) |
| RadiationOpts | radiators emit in slices once a second | no (emission timing, random draws) | radbolts covered by `fast-particle-collision` | not applicable |
| FastStructureTemperature | `StructureTemperatureComponents.Sim200ms` rewritten | no (temperature values) | not in the profile | not applicable (temperature) |
| CachePaths, AsyncPathProbe | path probes skipped if no nav cell nearby changed for 6 s; probes on its own jobs | no / timing only | the path prober is on its own thread, 0.02 ms of the main thread | not applicable |
| ReduceSoundUpdates | ambience and mix at 200 ms, looping sounds every other frame | no (audio timing) | not in the profile | not applicable |
| InfoCardOpts, SideScreenOpts, VirtualScroll | hover selection, side screens and scroll lists rewritten | no (order, refresh rates, row activation) | hover card about 0.3 ms; the rest only while open | not applicable (exact clip culling in the engine would be the way for long lists) |
| MeshRendererOptions | persistent mesh renderers in place of `Graphics.DrawMesh` | visual | Unity's per-call draw cost | not applicable (Moono records its own draws) |
| RenderTicks | light trackers at 200 ms (Lux lags), sublimation effects only in view (instance ids change) | no | small | not applicable |
| LoadOpts | the async loader scans two assemblies, event hashes folded in | start-up; mods' loaders skipped | start-up | not applicable (slow reflection under Mono, cheap on .NET) |

### Diagnostics only

Not techniques, listed for completeness: [Performance Observer](https://steamcommunity.com/sharedfiles/filedetails/?id=3800923048)
(系统性能观察器; Harmony timing probes on 54 game methods and a panel), Clean Memory &
Performance Capture's CSV capture, Fast Track's `Metrics`, shikyo13's oni-profiler.

## The mods

Last updated is the Workshop's time or the latest GitHub release or push; the build is the
`minimumSupportedBuild` of the current upload. The current game is U59, build 744825.

| Mod | Where | Last updated | Build | Status |
|---|---|---|---|---|
| Colony Performance Fixes 1.7.1 (Saito) | [Workshop 3823893994](https://steamcommunity.com/sharedfiles/filedetails/?id=3823893994) | 2026-10-24 | 469369 | current; not in our catalog (too new); our decompile is an older version |
| CycleTrim 0.4.0 (LIghtJUNction) | [Workshop 3807166689](https://steamcommunity.com/sharedfiles/filedetails/?id=3807166689), [source](https://github.com/LIghtJUNction/OniMods/tree/main/mods/CycleTrim) | 2026-10-21 | 525812 | early development; maps SimDLL to its own Rust core by default |
| BetterSave v2.10.0 (更好的存档) | [Workshop 3805601277](https://steamcommunity.com/sharedfiles/filedetails/?id=3805601277) | 2026-09-29 | 719533 | current |
| Performance Observer 0.2.0-dev (系统性能观察器) | [Workshop 3800923048](https://steamcommunity.com/sharedfiles/filedetails/?id=3800923048) | 2026-09-27 | 744825 | development release, diagnostics |
| Multithreaded Simulation RC-3.0 (多线程模拟) | [Workshop 3779276157](https://steamcommunity.com/sharedfiles/filedetails/?id=3779276157) | 2026-09-06 | 744825 | current, Windows only (catalog rank 524; failed to load in the original in our sweep, MOD_COMPATIBILITY.md) |
| Performance Assist 1.1.4 | [Workshop 3772297140](https://steamcommunity.com/sharedfiles/filedetails/?id=3772297140) | 2026-08-29 | 493472 | current |
| Unlock FPS 1.0.1 | [Workshop 3772228364](https://steamcommunity.com/sharedfiles/filedetails/?id=3772228364) | 2026-08-29 | 493472 | current |
| Lag Fix (Fetch/Sensor/NavGrid) 1.0.0 | [Workshop 3781498375](https://steamcommunity.com/sharedfiles/filedetails/?id=3781498375) | 2026-08-11 | 744825 | current |
| ONI Boost Kit 1.1.0 | [Workshop 3760687013](https://steamcommunity.com/sharedfiles/filedetails/?id=3760687013) | 2026-07-09 | 736649 | current; replaces the Mono runtime by a hand-run installer, Windows |
| Controlled Framerate 1.2.1 | [Workshop 3670573352](https://steamcommunity.com/sharedfiles/filedetails/?id=3670573352) | 2026-04-30 | 700386 | current |
| Fast Save 3.11 (Peter Han) | [Workshop 1867707267](https://steamcommunity.com/sharedfiles/filedetails/?id=1867707267) | 2026-04-01 | 719533 | current (catalog rank 37, works in Moono, MOD_COMPATIBILITY.md) |
| Multi threaded simulation 0.4.1 (R9MX4) | [Workshop 3563131181](https://steamcommunity.com/sharedfiles/filedetails/?id=3563131181) | 2025-10-25 | 600112 | outdated: its sim library (October 2025) replaces this build's July 2026 SimDLL |
| RemoveDailyReports 4.0.1 | [Workshop 1870873514](https://steamcommunity.com/sharedfiles/filedetails/?id=1870873514) | 2025-01-31 | 651155 | current |
| FPS Limiter ("Help! My GPU screams at the main menu!") | [Workshop 3405082060](https://steamcommunity.com/sharedfiles/filedetails/?id=3405082060) | 2025-01-11 | 648807 | works (sets two values) |
| Clean Memory & Performance Capture | [Workshop 3091570212](https://steamcommunity.com/sharedfiles/filedetails/?id=3091570212) | 2023-12-12 | 509600 | outdated: patches `EnergyInfoScreen`, gone in this build, so it fails to load |
| Better Performance | [Workshop 2785899934](https://steamcommunity.com/sharedfiles/filedetails/?id=2785899934) | 2022-03-28 | 467401 | outdated: throws while mods load |
| Lightning fast Menus | [Workshop 1852077143](https://steamcommunity.com/sharedfiles/filedetails/?id=1852077143) | 2019-09-02 | — | outdated: no mod_info.yaml, not loaded |
| Fast Track 0.18.5 beta (Peter Han) | [GitHub](https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta) | 2026-08-31 | U59 | current beta, GitHub only; works in Moono (MOD_COMPATIBILITY.md); ONI Perf reports it stalls on the original at this build without its `cpuFix` |
| ONI Perf 0.3.0 | [GitHub](https://github.com/Salacious-Oni-Mods/oni-perf-mod) | 2026-10-12 | 744825 | current, GitHub only (no Workshop item found) |
| oni-gc-runtime 0.1.0 | [GitHub](https://github.com/Salacious-Oni-Mods/oni-gc-runtime) | 2026-10-12 | 744825 | alpha, Windows, replaces the Mono runtime |
| Overlay Texture Memory Fix | [GitHub](https://github.com/pastadiablo/ONI-OverlayTextureMemoryFix) | 2026-08-28 | U59-744825 | current, GitHub only |
| GCBudget 1.0.0 | [GitHub](https://github.com/shikyo13/oni-gc-budget) | 2026-09-13 | 526233 | GitHub only |

Not performance mods despite their names: Optimized Digging, the 优化… building mods, Touch
Optimization, Smoother Light, Smooth Borders, Lagoo.

## Shortlist

Ranked by expected value in Moono. Each one starts with a measurement; none is done.

1. **Brains at 30 a second of real time** (non-exact, from ONI Perf `brainRate`). The game updates
   one Duplicant brain and five critter brains per rendered frame, whatever the frame rate. Moono
   runs U57 about 7 times as fast as the original (78–84 against 11.4 fps), so it already does
   about 7 times the original's chore searches per game second. Brains cost 1.4 ms of U57's
   12.6 ms frame and 2.4 ms on Gluttony. Held at 30 a second, they would run about 60% less often
   at 79 fps and still more often than in the original. Measure first: brain updates a second and
   `BrainScheduler.RenderEveryTick` time at speed 3 on U57 and Gluttony, then the same with the
   probe count scaled to a real-time allowance (ONI Perf's "spread" form, which keeps the frames
   even). This changes when Duplicants decide, so it needs the user's call on whether it belongs
   with the timing tweaks (like `slow-reachability`) or not at all.
2. **Switched-off effects left out of the cell-change and animation lists** (ONI Perf `markSkip` and
   `parkAnims`, both on by default there, near exact). Every Duplicant carries 14 laser-effect
   children, almost always off, which are still marked dirty whenever it moves and then put on the
   animation updater's list for calls that do nothing. ONI Perf claims 73% of moved transforms and
   43% of `UpdateAnim` calls on 200 Duplicants. The cell-change monitor and animation updates are
   among Moono's largest remaining costs. Measure first: on Gluttony (67 Duplicants), how many
   `MarkDirty` transforms are inactive and have no cell-change handler, and how many `UpdateAnim`
   calls reach an inactive controller with no batch. A verify mode would check that every skipped
   call really did nothing.
3. **The autosave's colony preview read asynchronously** (exact, from Fast Save's Background Save).
   The remaining autosave freeze includes the preview's GPU read-back (PLAN.md). Reading the same
   pixels a few frames later and encoding the PNG in the background gives the same image. BetterSave
   claims about 1.1 s for that stall on the first save. Measure first: how much of U57's 2.3 s
   freeze is `Timelapser`'s capture, read-back and encode.
4. **Steam polled less often while no pad is in use** (from ONI Perf `steamPoll`; gameplay is the
   same, Steam callbacks arrive up to 33 ms later). The engine calls the real Steam API, so
   `SteamInput.RunFrame` and `SteamAPI.RunCallbacks` cost what they cost in the original, claimed
   0.4–0.5 ms each. The bound in Moono is lower: `Global.Update`, which holds the input update, is
   0.3 ms in all on U57. Measure first: a stopwatch around both calls on Windows.
5. **The state-machine load's strings and types cached** (exact, from ONI Perf `typeNameCache` and
   `smRestore`). The restore decodes and compares the same few hundred 400-character type names over
   a million times on a big colony. Our earlier load-side serialization work didn't cover this.
   Measure first: the share of U57's 35 s load spent in `StateMachineSerializer` and
   `Type.GetType`.
6. **Idle animations advanced on a shared 30 Hz beat** (non-exact, from Colony Performance Fixes).
   Animations step at 30 fps, but each one steps on its own frame, so at 79 fps some batch
   re-uploads on almost every frame. Stepping still animations together would upload most batches
   on fewer frames, at the cost of shifting their frames and frame events by up to one animation
   frame. Measure first: batches uploaded a frame on U57, and how many of them hold only idle
   controllers.
7. **An exact one-pass irrigation update** (from Fast Track FlattenAverages). Plant element
   absorbers cost 1.8 ms in the older Linux profile, and `IrrigationMonitor.UpdateIrrigation` scans
   storage once per consumed element and again for wrong elements. Measure first: how much of that
   is the handler's body rather than the event dispatch.
8. **Smaller, measure-first items:** `ColonyAchievementTracker.RenderEveryTick`, `TrackerTool.Update`,
   `SuitMarker.Update` (exact caching of attached lockers), and Colony Performance Fixes' "skip
   drained generators" (`CircuitManager.PowerFromGenerator`) and "rocket status cache"
   (`CraftModuleInterface.EvaluateConditionSet`). Each is worth a stopwatch on U57 and nothing more
   unless it shows 0.3 ms or more.
9. **The overlay render-target leak** (exact, from Overlay Texture Memory Fix). This saves memory,
   not frame time: a claimed 74 MB per temperature or germ overlay toggle, kept until the main
   menu. Measure first: GL texture memory after toggling the temperature overlay 100 times in
   Moono. If it grows, release the targets the game displaces, as the mod does.
