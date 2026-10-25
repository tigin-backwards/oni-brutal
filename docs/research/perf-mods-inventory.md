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

Each entry opens with one of four verdicts: **covered** (Moono does it, or does better), **worth
investigating** (with a plan), **dropped before** (measured, see PERFORMANCE.md "Tried and
dropped") and **not applicable** (the cost doesn't exist in Moono, or the change is gameplay, not
performance).

### Frame pacing and process settings

- **VSync off, target frame rate set (240, 120 or user value)** — covered.
  [Unlock FPS](https://steamcommunity.com/sharedfiles/filedetails/?id=3772228364),
  [FPS Limiter](https://steamcommunity.com/sharedfiles/filedetails/?id=3405082060) and
  [Performance Assist](https://steamcommunity.com/sharedfiles/filedetails/?id=3772297140) set
  `QualitySettings.vSyncCount = 0` and `Application.targetFrameRate`, a display setting, so it is
  exact. Moono's player paces by vSync, else by `targetFrameRate`, and `--max-frame-rate` caps
  either (`OniBrutal.Host/Player.cs`, `Throttle`); these mods work through the same API. Frame time
  doesn't change, but the cap stops a menu running at thousands of fps.
- **Process priority High, re-applied every 10 s** — not applicable (the player's choice, not the
  engine's). Performance Assist changes the OS scheduling, which is exact; Moono doesn't do it. It
  claims it "may reduce stutter" from background contention; nobody has measured it.
- **Full speed while the window is unfocused** — not applicable (a preference; could be a player
  option). [Colony Performance Fixes](https://steamcommunity.com/sharedfiles/filedetails/?id=3823893994)
  (an option, off by default) and Performance Assist (which sets `runInBackground` as a side effect)
  stop the game's sleep while unfocused. That isn't exact, since the game slows itself on purpose.
  Moono keeps the game's behaviour, and PerfProbe turns it off only for measuring. It is not a
  frame-time gain.
- **Adaptive game speed held to a target fps, benchmark per save** — not applicable (a feature,
  works as a mod). [Controlled Framerate](https://steamcommunity.com/sharedfiles/filedetails/?id=3670573352)
  sets `Time.timeScale` every frame, which changes game speed, so it isn't exact.
- **Unity job workers raised (`job-worker-count` in boot.config)** — not applicable.
  [ONI Boost Kit](https://steamcommunity.com/sharedfiles/filedetails/?id=3760687013) raises Unity's
  native job threads (skinning, culling), which is exact. The engine has no Unity native jobs, and
  the game's job threads are covered by `fast-jobs`. The gain is only claimed.

### Garbage collection

- **A rebuilt Mono runtime with parallel marking** — not applicable.
  [oni-gc-runtime](https://github.com/Salacious-Oni-Mods/oni-gc-runtime) and ONI Boost Kit replace
  `mono-2.0-bdwgc.dll`, which is exact. Moono's runtime covers it: CoreCLR's collector runs in
  parallel on every core and pauses about a quarter as long, about 1.4% of a frame (PERFORMANCE
  "Why the engine alone is faster"). oni-gc-runtime claims a full collection drops from 220–240 ms
  to 30–50 ms on a 2–3.5 GB heap.
- **Incremental GC (`gc-max-time-slice=3`)** — not applicable. Colony Performance Fixes has it as an
  option, off by default. It spreads Boehm's collection over frames, which is exact, and CoreCLR's
  collector covers it as above. Colony Performance Fixes claims GC freezes drop from about 370 ms to
  60 ms.
- **GC latency mode, manual mode with collections on a timer or heap budget, a collection at focus
  loss** — not applicable. Performance Assist (`SustainedLowLatency`),
  [BetterSave](https://steamcommunity.com/sharedfiles/filedetails/?id=3805601277) (GC disabled, a
  full collection every 10 min), [GCBudget](https://github.com/shikyo13/oni-gc-budget),
  [Clean Memory & Performance Capture](https://steamcommunity.com/sharedfiles/filedetails/?id=3091570212)
  and GC Timing (in oni-gc-runtime) change when collections happen, which is exact. The engine maps
  `GarbageCollector.GCMode` onto CoreCLR (`ManagedHeap`), so these modes still do what they ask;
  what they work around is the collector's pauses. BetterSave claims a 0.5 s collection every 50 s
  becomes one every 10 min, at about 1 GB more memory.
- **The collector held off while a colony loads** — dropped before.
  [ONI Perf](https://github.com/Salacious-Oni-Mods/oni-perf-mod)'s `loadGcHold` (with 24 GB or more
  installed) runs one collection at the end of the load. It is exact, but memory grows. We measured
  U57's load pausing 3.2–3.9 s in collections, but neither a larger gen0 budget nor the
  non-concurrent collector loaded faster, and holding collections would keep 12 GB of 2minnow's
  garbage (PERFORMANCE "Tried and dropped"). ONI Perf claims Euphoria (239,000 objects) loads in
  about 111 s instead of 730 s on Mono.
- **The forced collection after each save skipped** — covered by `no-save-gc`. BetterSave and Fast
  Save skip the `GC.Collect` at the end of `SaveLoader.Save`, which is exact.

### Saving and loading

- **Compression and the file write on a background thread** — covered by `background-save` and
  `fast-save-compression`. [Fast Save](https://steamcommunity.com/sharedfiles/filedetails/?id=1867707267)
  (Background Save, autosaves only) and BetterSave move `SaveLoader.CompressContents` and the write
  off the main thread. The file is the same, so it is exact, though BetterSave only logs write
  errors. We measured the autosave freeze at 11.7 s before the save tweaks and 4.8 s after on Linux,
  and 2.3 s after on Windows. BetterSave claims 3.6 s before and 0.56 s after on a 103 MB save.
- **Compiled serialization delegates, type names cached as bytes** — covered by
  `fast-serialization` and `fast-save-type-checks`. Fast Save (Use Delegates, "may crash") and
  BetterSave (checked byte for byte against vanilla on the first two saves) replace
  `SerializationTemplate.SerializeData` and `Manager.GetSerializationTemplate`, exactly.
- **A reused save buffer** — worth investigating (small; measure the autosave's allocations first).
  BetterSave reuses the `MemoryStream` the save is written into, which is exact. The gain is only
  claimed; the next step is to measure the allocation per autosave on U57.
- **The colony preview: async GPU read-back, PNG encoded in the background** — worth investigating.
  Fast Save does this for autosaves in `Timelapser.RenderAndPrint`. Only the time the pixels are
  read and the PNG written changes, so it is exact: the same image, later. Moono doesn't do it, and
  the remaining freeze includes "the colony preview's GPU read-back" (PLAN.md, save tweaks).
  BetterSave claims the preview stalls about 1.1 s on its first save.
- **The preview captured once a session, then copied** — not applicable (prefer the exact
  read-back). BetterSave does this, so the save list's thumbnail goes stale and the preview image
  isn't exact. It saves the same stall as above.
- **Old daily reports pruned or emptied** — not applicable as a tweak (for the record, measure what
  U57's 17,000 reports cost the save). Fast Save (Moderate keeps 20, Aggressive 4) and
  [RemoveDailyReports](https://steamcommunity.com/sharedfiles/filedetails/?id=1870873514) (keeps 10)
  trim the save's report history. It isn't exact: report screens, some achievements and the save's
  contents change. Fast Save claims saving and loading "up to 50%" faster late game.
- **Conveyor network rebuild from an index** — covered by `fast-conveyor-paths`. ONI Perf's
  `conveyorPaths` replaces the path search in the `SolidConduitFlow` rebuild, exactly. ONI Perf's
  README lists it as its own, and the design is the same. We measured a rebuild at 50–510 ms
  before and 2–14 ms after, and the load's rebuilds at 840–850 ms before and 14 ms after.
- **`Type.GetType` cached for the state-machine load; the restore's 400-character type-name strings
  decoded once and shared** — worth investigating (profile U57's load for `StateMachineSerializer`
  and `Type.GetType` first). ONI Perf's `typeNameCache` and `smRestore` speed up the
  `StateMachineSerializer` restore, exactly (same strings, same choices). Moono doesn't do it. The
  loading side of `fast-serialization` was measured and not kept (31.3 s against 31.6, PLAN.md),
  but that did not cover the state-machine restore. ONI Perf claims these are part of its default
  load levers' gain, which took Some light torture from 52 s to 43 s (with `loadGcHold`).
- **The load spread over frames, far objects built after the loading screen** — not applicable
  (Moono's U57 load is 35 s; not exact). ONI Perf's `fastLoad` (experimental, off by default)
  changes object spawn order and timing, so objects far away appear after the colony starts,
  paused. It claims Euphoria's window answers after 16.6 s instead of 110 s.

### Overlays

- **Temperature overlay colouring only what's in view, a quarter a frame, for changes of 0.05 K or
  more** — covered. Colony Performance Fixes changes `Infrared.Update` and the visualizers' event.
  Colours come up to four frames late and small changes aren't shown, so it isn't exact.
  `fast-overlays` covers it exactly by passing over out-of-view buildings without reading them. We
  measured `Infrared.Update` at 10.4 ms before and 6.0 ms after on U57; Colony Performance Fixes
  claims overlays go from 29–41 fps to 60–95.
- **Materials, decor, power and pipe overlays refreshed on camera moves or at 4 Hz; power overlay
  at 0.5 Hz** — covered (the throttle itself isn't exact). Colony Performance Fixes and Clean
  Memory & Performance Capture throttle the modes' `Update`, so targets, tints and labels can be up
  to 0.25 s or 2 s old. `fast-overlays` covers it exactly where it paid, by keeping what the
  targets' visits read (overlays.md). We measured materials at 4.05 ms before and 3.8 ms
  after, and decor at 4.32 ms before and 3.6–4.0 ms after.
- **A circuit's wattage computed once a frame** — covered. Colony Performance Fixes caches
  `CircuitManager.GetWattsNeededWhenActive`. It is almost exact, as the cache is cleared on
  `Refresh` only. `fast-overlays` does it exactly by keeping the value within one `Power.Update`. We
  measured 0.52 ms before and 0.07 ms after.
- **Displaced render targets released when overlays switch the MRT setup** — worth investigating
  (memory, not fps). `MultipleRenderTargetProxy.CreateRenderTarget` leaves the replaced
  `TexturesCopies` alive, and
  [Overlay Texture Memory Fix](https://github.com/pastadiablo/ONI-OverlayTextureMemoryFix) releases
  and destroys them. That is exact: it fixes a leak, and nothing reads the old textures. Moono
  didn't do it when this was written; the engine frees a texture when the game destroys it, as
  Unity does, so the leak should be the same. The mod claims 74 MB committed per toggle of the
  temperature or germ overlay, kept until the main menu. It has since been done (Shortlist item 9).

### Animation

- **Idle animations advanced together at about 30 Hz, so their batches upload on fewer frames** —
  worth investigating (count batch uploads a frame at 79 fps first). Colony Performance Fixes (on
  by default) changes `KBatchedAnimUpdater.LateUpdate`'s loop. It isn't exact: an animation's frame
  steps shift by up to a frame, and so do its frame events. Moono doesn't do it. `fast-anim-batches`
  and `fast-symbol-overrides` cut the cost of each upload, but the number of uploads stays the
  same. Colony Performance Fixes claims the normal view goes from about 70 fps to 85–90 with its
  other fixes; we measured animation updates at 0.9 ms and batch uploads at 0.6 ms at 79 fps.
- **Inactive controllers with no batch taken off the anim updater's list** — worth investigating
  (count such calls on Gluttony's 67 Duplicants). ONI Perf's `parkAnims` changes
  `KBatchedAnimController.UpdateAnim`. It claims to be exact, since the calls removed do nothing,
  though the updater's list order changes. Moono doesn't do it. ONI Perf claims 280 of 646
  `UpdateAnim` calls a frame were no-ops, on 200 Duplicants.
- **`CellChangeMonitor.MarkDirty` stops at inactive children without a cell-change handler** — worth
  investigating (count them on Gluttony, with a verify mode). With ONI Perf's `markSkip`, an
  inactive laser effect reads "not moving". It is almost exact: no cell event is lost, but an
  inactive object's moving state differs. Moono doesn't do it. ONI Perf claims 73% of transforms
  marked as moved were switched-off laser effects (14 per Duplicant), and a saving of 0.20–0.40 ms.
- **Symbol overrides re-derived only when changed; only the changed range uploaded** — covered by
  `fast-symbol-overrides`, which doesn't upload unchanged blocks. ONI Perf's `symbolMemo` and
  `overrideRange` cut per-Duplicant override rebuilds and uploads, exactly. We measured 1.47 ms
  before and 1.20 ms after; ONI Perf claims savings of 0.98–2.14 ms and 0.38–0.59 ms.
- **Looping animations whose frames are identical played once** — dropped before. Fast Track's
  `AnimOpts` does this in `KAnimLoopOptimizer`. It isn't exact, since anim-complete events and
  repeating sounds change. It was reviewed in u57-frame-inventory.md ("Fast Track reviewed").
- **Animation batch cleanup, spatial index cleanup** — covered by `fast-anim-cleanup` and
  `fast-partitioner-cleanup`. Colony Performance Fixes has them as "anim batch cleanup" and
  "partitioner cleanup", on by default, in `KBatchedAnimUpdater` and `ScenePartitioner`. They are
  likely as exact as ours, but our decompile predates them. We measured 0.48 ms before and 0.12 ms
  after, and on Gluttony 0.38 ms before and 0.16 ms after.

### Interface and per-frame housekeeping

- **Closed screens put to sleep** — covered. ONI Perf's `screenSleep` disables closed screens'
  canvases, toggles, graphs and scroll views, which isn't exact since they are the game's
  components. Moono covers it in the engine: the UI walk, rect masks and raycasts skip closed
  screens. What is left (the toggles' and portraits' scripts, about 0.7 ms on 2minnow) needs
  disabling game components (PERFORMANCE "Tried and dropped"). We measured masks at 6.3 ms
  before and 0.3–0.4 ms after on 2minnow; ONI Perf claims one visit to Consumables adds 17.95 ms a
  frame.
- **The UI hover raycast reused while the mouse is still (up to 0.1 s), 30 Hz while it moves** —
  covered (what's left isn't exact). Colony Performance Fixes and ONI Perf's `raycastCache` change
  `StandaloneInputModule`'s pointer raycast, so hover state can lag a screen change by 0.1 s. Moono
  covers it in part, as the raycast leaves out closed screens' graphics. We measured U57's raycast
  at 2.0–2.3 ms before and 0.6–0.9 ms after; Colony Performance Fixes claims about 1 ms each time.
- **The hover card rebuilt only when something it shows changes** — dropped before. ONI Perf's
  `hoverCache` and Fast Track's `InfoCardOpts` cache `SelectToolHoverTextCard`, which isn't exact
  since the card shows live mass and temperature. We measured the card at about 0.3 ms.
- **Property textures refilled on a sim tick or view change** — dropped before. ONI Perf's
  `propTex` does this to `PropertyTextures.LateUpdate`, and Fast Track's `ReduceTileUpdates` also
  threads it. It isn't exact, since revealed cells and lights change between ticks. We measured
  0.5–0.9 ms in all, and the engine already skips uploading unchanged rows.
- **Unused cameras off, lerper passes as blits** — dropped before. ONI Perf's `idleCams` and
  `lerpBlit` turn off the timelapse, sim overlay and cursor cameras and blit six texture lerpers,
  exactly. We measured them together under 0.25 ms.
- **Idle trackers, heat effects, workers, outfits and progress bars put to sleep; pipe contents
  cached while paused** — dropped before. ONI Perf's `idleSleep`, `workerSleep`, `barRate`,
  `pipeCache` and `pausedSkip` cut their per-frame updates. They are exact, or act only while
  paused. We measured 0.02–0.12 ms each.
- **Steam Input polled at 20 Hz while no pad is in use; Steam callbacks at 30 Hz** — worth
  investigating (time both calls first). ONI Perf's `steamPoll` throttles `SteamInput.RunFrame` and
  `SteamAPI.RunCallbacks`. Gameplay is the same, but a pad tap under 50 ms can be missed and Steam
  callbacks arrive up to 33 ms later. Moono doesn't do it; the engine calls the real Steam API
  (ARCHITECTURE, Steamworks.NET), so the native cost is the same. ONI Perf claims 0.41–0.49 ms and
  0.38–0.46 ms a frame. In Moono the bound is lower: `Global.Update`, which holds the input update,
  measures 0.3 ms in all on U57.
- **Achievement checks at a fixed rate** — not applicable (too small, and not exact). ONI Perf's
  `achieveRate` and Fast Track's `DisableAchievements` throttle `ColonyAchievementTracker`, so an
  achievement can register seconds later. ONI Perf claims a saving of 0.10 ms; we haven't
  measured the cost in Moono.
- **The audio mixer's visible-Duplicant count at a fixed rate** — not applicable (too small). ONI
  Perf's `mixRate` throttles `AudioMixer.SetVisibleDuplicants`, so mixer parameters can be up to
  1/Hz late. It claims 0.10 ms with 200 Duplicants; we haven't measured the cost in Moono.
- **Menu expand animations skipped** — not applicable (and doesn't load: no mod_info.yaml).
  [Lightning fast Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=1852077143) changes
  the `ExpandRevealUIContent` curves, a visual change, so it isn't exact.

### Simulation and Duplicant logic

- **Brains at the rate the game was tuned for (30 a second of real time), not once per rendered
  frame** — worth investigating (a non-exact tweak). `BrainScheduler` probes one Duplicant brain and
  five critter brains per rendered frame, with no `dt` term (`BrainScheduler.cs` in the game, whose
  constants state a 30 fps frame). ONI Perf's `brainRate` (off by default) and CycleTrim (critters
  only, at most 5 a frame and 400 a second) cap that count. Chore searches then happen less often
  per real second, so it isn't exact. Moono doesn't do it yet. It runs about 7 times the original's
  frame rate on U57, so it already searches chores about 7 times as often per game second as the
  original; capping at 30 a second would still search more often than the original does. We
  measured brains at 1.4 ms of the 12.6 ms frame on U57, and 2.4 ms on Gluttony. ONI Perf claims
  every lever together, `brainRate` and `moveHold` included, takes 54.9 fps to 123.8, against 77.2
  with its defaults; `brainRate` alone wasn't measured.
- **Busy Duplicants' pickup and chore refreshes throttled; pickup sensor every 3rd brain update;
  every sensor every 2nd** — not applicable on its own (a brain-rate cap, above, covers the same
  cost more uniformly). [CycleTrim](https://steamcommunity.com/sharedfiles/filedetails/?id=3807166689) and
  [Lag Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3781498375) throttle
  `PickupableSensor.Update`, `Sensors.UpdateSensors` and chore searches, so new errands and items
  are noticed brain updates later. Moono doesn't do it. We measured sensors' scans at 0.064 ms a
  frame on U57 (u57-frame-inventory.md), and fetch updates at 0.5 ms with `fast-offset-tables`.
  CycleTrim claims brain time drops from 5.1 s to 2.4 s, and fps rises from 70.6 to 85.1.
- **Stationary critters' path probes throttled; repeated async probes deduplicated** — not
  applicable (would only save worker-thread time). CycleTrim throttles `Navigator.UpdateProbe` and
  `AsyncPathProber` with eight skips, then a forced refresh, so it isn't exact. Moono covers it in
  effect: the path prober runs on a separate thread and takes about 0.02 ms of the main thread to
  hand out its work (PERFORMANCE "Where a frame goes now"). CycleTrim claims the Navigator hot path
  drops from 827 ms to 48 ms in total, and fps rises from 81 to 109.
- **Pickup candidate costs cached in `UpdatePickups`** — not applicable. CycleTrim and Fast Track's
  `FastUpdatePickups` cache them in `FetchManager.FetchablesByPrefabId.UpdatePickups`, and CycleTrim
  claims its version is exact. We reviewed it: the call already runs on the game's worker jobs.
  CycleTrim claims a call drops from 41.3 µs to 35.7 µs.
- **Nav-grid links rebuilt in parallel for batches of 64 cells or more** — dropped before. Lag Fix
  does this to `NavGridUpdater.UpdateLinks`, exactly. We measured each update at about 40 µs, too
  small for a fork and join (PERFORMANCE "Tried and dropped"), and big batches are rare.
- **The round-robin nav-grid update restored** — not applicable. Lag Fix has it in
  `Pathfinding.UpdateNavGrids` as an experimental option, off by default. It isn't exact.
- **Reachability checked only when something nearby changed** — covered by `slow-reachability`.
  Fast Track's `FastReachability` replaces `ReachabilityMonitor` with a prober that refreshes every
  4 s, which isn't exact. `slow-reachability` isn't exact either; its delay is one the project
  accepts. We measured 1.88 ms before and 0.53 ms after.
- **A walking Duplicant stays "moving" between sim sub-ticks** — not applicable. ONI Perf's
  `moveHold` (off by default) delays the stopped event by up to 0.1 s, so it isn't exact. Its gain
  wasn't measured on its own.
- **Drained generators skipped when circuits draw power** — worth investigating (read the game's
  `PowerFromGenerator` for side effects of a zero draw). Colony Performance Fixes does this in
  `CircuitManager.PowerFromGenerator` and `Sim200msLast` ("skip drained generators", on by default;
  not in our decompile). Whether it is exact is unknown until we read it. Moono doesn't do it. Power
  costs 1.6 ms for 109 generators (older Linux profile).
- **Rocket condition results cached** — worth investigating (time it on U57's rockets first; drop if
  under 0.1 ms). Colony Performance Fixes caches `CraftModuleInterface.EvaluateConditionSet`
  ("rocket status cache", on by default). Whether that is exact is unknown. It claims no gain, and
  we haven't measured the cost in Moono.
- **A rewritten, multithreaded native simulation** — not applicable (outside the engine; the wait
  bounds any gain at about 1 ms on U57).
  [Multithreaded Simulation (RC-3.0)](https://steamcommunity.com/sharedfiles/filedetails/?id=3779276157)
  (Rust, [source](https://github.com/KeringFa/ONI-SimDLL_Rust)),
  [Multi threaded simulation](https://steamcommunity.com/sharedfiles/filedetails/?id=3563131181)
  (C++) and CycleTrim's OniSim (Rust, on by default in 0.4.0) replace SimDLL, which simulates heat,
  gas, liquid and elements. The C++ one isn't exact (its author says results differ). OniSim claims
  identical final states on its test fixtures, untested in real games. Moono runs Klei's SimDLL, and
  on U57 the main thread waits on it about 1.0 ms a frame. OniSim claims 9% to 52% less CPU per
  scene on fixtures; ONI Perf claims 54.94 fps against 54.91 with a faster sim.
- **Duplicants twice as fast, tasks twice as fast, more food** — not applicable (and fails to load
  on this build). [Better Performance](https://steamcommunity.com/sharedfiles/filedetails/?id=2785899934)
  changes gameplay tuning so that lower speeds suffice, which isn't exact.

### Fast Track's other options

[Fast Track](https://github.com/peterhaneve/ONIMods) (0.18.5 beta, MIT) read option by option
from its source, against the decompiled game. AnimOpts, FastReachability, FastUpdatePickups,
ConduitOpts, FastCellChangeMonitor and the visual and feature switches were reviewed before
(u57-frame-inventory.md). One correction there: FastUpdatePickups also replaces
`FetchListStatusItemUpdater.Render200ms` on the main thread, and not exactly (it sums
`TotalAmount` where the game sums `FetchTotalAmount`, and drops the game's skip of objects with no
world), so the status items it sets can differ.

As written, none of these options keeps the game's results, except SensorOpts, which never
takes effect in this build. The entries that point at an exact version Moono could write say so.

- **PickupOpts, ChorePriorityMode** — not applicable. They move Duplicant brains' fetch updates
  onto worker threads during `World.LateUpdate`, replace `BrainScheduler`, defer anim-complete
  triggers and limit priority brains. They aren't exact: brain order and timing change, and the
  fetch list loses the game's de-duplication. In Moono, brains cost 1.4 ms and fetch updates 0.5 ms
  (`fast-offset-tables` covers part).
- **ENetOpts** — not applicable (a building check). It replaces `CircuitManager.Refresh` and
  `Sim200msLast` and batches battery drains. It isn't exact: power values and unpowered flags
  differ, and a building counts as powered at 0 J where the game allows 0.01. We haven't measured
  the cost in Moono.
- **FlattenAverages** — worth investigating (an exact one-pass irrigation update in the game's
  float order; first split the 1.8 ms between the event dispatch and the handler). It runs amounts'
  batch update on separate jobs, starts accumulators at NaN and rewrites
  `IrrigationMonitor.UpdateIrrigation` as one pass (`PlantElementPatches.cs:109`). It isn't exact:
  averages differ in the first 3 s, and the irrigation rewrite multiplies in another float order,
  which can flip its threshold. In Moono, plant element absorbers cost 1.8 ms (older Linux
  profile), mostly the storage-change event into irrigation monitors.
- **FastAttributesMode** — worth investigating, low priority (an exact side table keeping the game's
  checks). It uses dictionaries for attribute levels and converters and rewrites the experience
  code in `StandardWorker.Work`. It isn't exact: it skips the `maxAttributeLevel == 0` check, so
  some Duplicants gain experience, and it matches by Id instead of by reference. In Moono it is
  part of workers doing work, 1.6 ms (older Linux profile).
- **CritterConsumers** — not applicable. It rewrites `GasAndLiquidConsumerMonitor.FindTargetCell`
  as a best-cost query, which picks other target cells. In Moono its cost is part of critter
  brains.
- **SensorOpts** — not applicable. `SafeCellQuery.IsMatch` stops once every flag is found. That
  would be exact, but the path finder already stops at such a cell, so it never fires and there is
  no cost to save.
- **LogicUpdates** — not applicable. Comet detectors and smart reservoirs send logic signals only
  on change. It isn't exact, since handlers of a same-value send stop running. The cost in Moono is
  tiny.
- **MiscOpts** — worth investigating (measure first). It runs suit markers at 200 ms, checks
  achievements incrementally, moves the `Chatty` tag, skips `WorldInventory.Update` while paused
  and adds small caches; most of it isn't exact. We haven't measured its cost in Moono. Two parts
  deserve a stopwatch. `ColonyAchievementTracker.RenderEveryTick` checks one unfinished achievement
  a frame, and some walk every building (U57 has 62,000). In `SuitMarker.Update`, each checkpoint
  finds its lockers every frame; caching them, invalidated on building changes, would be exact.
- **ReduceColonyTracking** — worth investigating (measure first; only exact speed-ups of single
  trackers would do). It cuts `TrackerTool` from 50 to 10 world trackers a frame and has
  `ChoreDriver` report every 1 s, which isn't exact. We haven't measured the cost in Moono; some
  trackers walk every network or chore.
- **AllocOpts, MinimalKCV, CustomStringFormat** — not applicable. They allocate less with pooled
  lists, `Cmps` add and remove, and Ryu number formatting. They aren't exact: lists are shared, and
  the `Components` list order and number text change. The cost they target is GC pressure, which
  matters under Mono's Boehm collector.
- **ParallelInventory** — not applicable. It runs `WorldInventory.Update` on a worker thread and
  leaves some tags out, which isn't exact. We haven't measured the cost in Moono.
- **BackgroundRoomRebuild, DecorProvider patches** — not applicable (rooms are building checks).
  Rooms are flood-filled in the background and decor refreshes queued, so room timing changes and
  it isn't exact. The cost isn't in Moono's profile.
- **RadiationOpts** — not applicable. Radiators emit in slices once a second, which changes
  emission timing and random draws. In Moono, radbolts are covered by `fast-particle-collision`.
- **FastStructureTemperature** — not applicable (temperature). It rewrites
  `StructureTemperatureComponents.Sim200ms`, which changes temperature values. The cost isn't in
  Moono's profile.
- **CachePaths, AsyncPathProbe** — not applicable. CachePaths skips path probes if no nav cell
  nearby changed for 6 s, which isn't exact; AsyncPathProbe runs probes on Fast Track's jobs, which
  changes only timing. In Moono the path prober is on a separate thread, 0.02 ms of the main thread.
- **ReduceSoundUpdates** — not applicable. Ambience and mix update at 200 ms and looping sounds
  every other frame, which changes audio timing. The cost isn't in Moono's profile.
- **InfoCardOpts, SideScreenOpts, VirtualScroll** — not applicable (exact clip culling in the
  engine would be the way for long lists). They rewrite hover selection, side screens and scroll
  lists, which changes order, refresh rates and row activation. In Moono the hover card costs about
  0.3 ms, and the rest costs only while open.
- **MeshRendererOptions** — not applicable (Moono records draws itself). Persistent mesh renderers
  replace `Graphics.DrawMesh`; the differences are visual. The cost it targets is Unity's per-call
  draw cost.
- **RenderTicks** — not applicable. Light trackers update at 200 ms, so Lux lags, and sublimation
  effects run only in view, which changes instance ids. It isn't exact. The cost in Moono is small.
- **LoadOpts** — not applicable (slow reflection under Mono, cheap on .NET). The async loader
  scans two assemblies and folds event hashes in. Only start-up changes, but mods' loaders are
  skipped. The cost is at start-up.

### Diagnostics only

Not techniques, listed for completeness: [Performance Observer](https://steamcommunity.com/sharedfiles/filedetails/?id=3800923048)
(系统性能观察器; Harmony timing probes on 54 game methods and a panel), Clean Memory &
Performance Capture's CSV capture, Fast Track's `Metrics`, shikyo13's oni-profiler.

## The mods

Last updated is the Workshop's time or the latest GitHub release or push; the build is the
`minimumSupportedBuild` of the current upload. The current game is U59, build 744825. Each entry
opens with the mod's state.

- **Colony Performance Fixes 1.7.1** (Saito,
  [Workshop 3823893994](https://steamcommunity.com/sharedfiles/filedetails/?id=3823893994)) —
  current. Updated 2026-10-24, build 469369. It isn't in our catalog (too new), and our decompile is
  of an older version.
- **CycleTrim 0.4.0** (LIghtJUNction,
  [Workshop 3807166689](https://steamcommunity.com/sharedfiles/filedetails/?id=3807166689),
  [source](https://github.com/LIghtJUNction/OniMods/tree/main/mods/CycleTrim)) — early development.
  Updated 2026-10-21, build 525812. By default it maps SimDLL to a Rust core of its own.
- **BetterSave v2.10.0** (更好的存档,
  [Workshop 3805601277](https://steamcommunity.com/sharedfiles/filedetails/?id=3805601277)) —
  current. Updated 2026-09-29, build 719533.
- **Performance Observer 0.2.0-dev** (系统性能观察器,
  [Workshop 3800923048](https://steamcommunity.com/sharedfiles/filedetails/?id=3800923048)) —
  development release, diagnostics. Updated 2026-09-27, build 744825.
- **Multithreaded Simulation RC-3.0** (多线程模拟,
  [Workshop 3779276157](https://steamcommunity.com/sharedfiles/filedetails/?id=3779276157)) —
  current, Windows only. Updated 2026-09-06, build 744825. It is catalog rank 524 and failed to load
  in the original in our sweep (MOD_COMPATIBILITY.md).
- **Performance Assist 1.1.4**
  ([Workshop 3772297140](https://steamcommunity.com/sharedfiles/filedetails/?id=3772297140)) —
  current. Updated 2026-08-29, build 493472.
- **Unlock FPS 1.0.1**
  ([Workshop 3772228364](https://steamcommunity.com/sharedfiles/filedetails/?id=3772228364)) —
  current. Updated 2026-08-29, build 493472.
- **Lag Fix (Fetch/Sensor/NavGrid) 1.0.0**
  ([Workshop 3781498375](https://steamcommunity.com/sharedfiles/filedetails/?id=3781498375)) —
  current. Updated 2026-08-11, build 744825.
- **ONI Boost Kit 1.1.0**
  ([Workshop 3760687013](https://steamcommunity.com/sharedfiles/filedetails/?id=3760687013)) —
  current. Updated 2026-07-09, build 736649. On Windows, a hand-run installer replaces the Mono
  runtime.
- **Controlled Framerate 1.2.1**
  ([Workshop 3670573352](https://steamcommunity.com/sharedfiles/filedetails/?id=3670573352)) —
  current. Updated 2026-04-30, build 700386.
- **Fast Save 3.11** (Peter Han,
  [Workshop 1867707267](https://steamcommunity.com/sharedfiles/filedetails/?id=1867707267)) —
  current. Updated 2026-04-01, build 719533. It is catalog rank 37 and works in Moono
  (MOD_COMPATIBILITY.md).
- **Multi threaded simulation 0.4.1** (R9MX4,
  [Workshop 3563131181](https://steamcommunity.com/sharedfiles/filedetails/?id=3563131181)) —
  outdated. Updated 2025-10-25, build 600112. Its sim library (October 2025) replaces this build's
  July 2026 SimDLL.
- **RemoveDailyReports 4.0.1**
  ([Workshop 1870873514](https://steamcommunity.com/sharedfiles/filedetails/?id=1870873514)) —
  current. Updated 2025-01-31, build 651155.
- **FPS Limiter** ("Help! My GPU screams at the main menu!",
  [Workshop 3405082060](https://steamcommunity.com/sharedfiles/filedetails/?id=3405082060)) —
  works, as it only sets two values. Updated 2025-01-11, build 648807.
- **Clean Memory & Performance Capture**
  ([Workshop 3091570212](https://steamcommunity.com/sharedfiles/filedetails/?id=3091570212)) —
  outdated. Updated 2023-12-12, build 509600. It patches `EnergyInfoScreen`, which is gone in this
  build, so it fails to load.
- **Better Performance**
  ([Workshop 2785899934](https://steamcommunity.com/sharedfiles/filedetails/?id=2785899934)) —
  outdated. Updated 2022-03-28, build 467401. It throws while mods load.
- **Lightning fast Menus**
  ([Workshop 1852077143](https://steamcommunity.com/sharedfiles/filedetails/?id=1852077143)) —
  outdated. Updated 2019-09-02, no build given. It has no mod_info.yaml, so it isn't loaded.
- **Fast Track 0.18.5 beta** (Peter Han,
  [GitHub](https://github.com/peterhaneve/ONIMods/releases/tag/FastTrackBeta)) — current beta,
  GitHub only. Updated 2026-08-31, build U59. It works in Moono (MOD_COMPATIBILITY.md); ONI Perf
  reports it stalls on the original at this build without ONI Perf's `cpuFix`.
- **ONI Perf 0.3.0** ([GitHub](https://github.com/Salacious-Oni-Mods/oni-perf-mod)) — current,
  GitHub only (no Workshop item found). Updated 2026-10-12, build 744825.
- **oni-gc-runtime 0.1.0** ([GitHub](https://github.com/Salacious-Oni-Mods/oni-gc-runtime)) —
  alpha, Windows. Updated 2026-10-12, build 744825. It replaces the Mono runtime.
- **Overlay Texture Memory Fix**
  ([GitHub](https://github.com/pastadiablo/ONI-OverlayTextureMemoryFix)) — current, GitHub only.
  Updated 2026-08-28, build U59-744825.
- **GCBudget 1.0.0** ([GitHub](https://github.com/shikyo13/oni-gc-budget)) — GitHub only. Updated
  2026-09-13, build 526233.

Not performance mods despite their names: Optimized Digging, the 优化… building mods, Touch
Optimization, Smoother Light, Smooth Borders, Lagoo.

## Shortlist

Ranked by expected value in Moono. Each one starts with a measurement. Since this was written, 9 is done and 1–3, 7 and 8 are being worked on.

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

   Done (2026-10-25): it grows in the original and in Moono alike, about 18 MB a toggle at
   1024×768; the `fast-overlay-textures` tweak frees them (PLAN.md).
