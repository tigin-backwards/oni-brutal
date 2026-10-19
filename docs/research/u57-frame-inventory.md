# U57 frame inventory (2026-10-19)

What a large colony's frame is made of, system by system, and what could be made faster without
changing what the game does. U57: Spaced Out with every DLC, cycle 17077, 24 Duplicants, 1,186
critters, about 62,000 buildings, 90,000 animated objects; played at speed 3.

## Method

- **Profiler:** `perf` on Linux (cpu-clock, 499 Hz, every thread, JIT symbols through perf maps),
  90 s of steady play from frame 1,500. It samples any instruction, native frames included.
- **Why Linux:** the Windows EventPipe sampler stops threads only at safe points, which charges
  time to lock slow paths, allocation helpers and GC polls. It put our engine at 26–35% of the main
  thread where perf measures 13%, and showed lock time that perf does not see at all.
- **Getting U57 onto Linux:** the Linux install (build 744825, the same build as the Windows one)
  plus the Windows install's DLC content (StreamingAssets banks, bundles and the `dlc` folder) as
  symlinks in a scratch game folder, with the offline Steam double owning every DLC
  (a local variant of tools/SteamStub). Neither install is modified.
- **Instance counts:** PerfProbe's `counts.txt` mode at colony load.
- **Mechanisms:** read from the game's decompiled code, method by method, for the systems below.

Scripts (scratchpad, not in the repo): `u57-linux.sh` and `u57-prof.sh` (run and profile),
`perf2pkl.py`, `inventory.py` (phase > root > system) and `drill.py` (children, self, owner
module).

## The frame

82 ms (12.2 fps). The main thread is the limit: it is busy for 75.5 of the 90 s sampled. Other
threads have headroom: the GL worker about 20%, the game's async path prober 18%, the native sim
(SimDLL) 9%, and FMOD a few percent per thread.

| Owner of main-thread time | Share | ms/frame |
|---|---|---|
| Game (`Assembly-CSharp`, `-firstpass`) | 71% | 58 |
| This project's engine (`OniBrutal.UnityEngine`, `.Desktop`) | 13% | 11 |
| Native (libc, libm, CoreCLR: memcpy, maths, GC) | 7% | 6 |
| Ionic.Zlib (the autosave in this window) | 6% | 5 |
| uGUI, TextMeshPro (this project's) | 1.5% | 1 |

By player-loop entry: `Game.Update` 41 ms, autosave 9 ms (averaged over the window; the game
tweaks take most of it off the main thread), rendering 6 ms, `Game.LateUpdate` 5 ms,
`Global.LateUpdate` 4.5 ms, `Global.Update` 2.6 ms, canvas build 2.4 ms, `World.LateUpdate` 2.2 ms.

## System by system

Columns: ms per frame (main thread), count of the objects involved where known, the mechanism
(what makes it cost that), and the candidate. Safety classes:
- **A:** the same results by construction (caching an invariant, a faster equivalent check);
- **B:** the same results, different timing or order of work;
- **C:** changes behaviour (frequency, what is simulated).

| System | ms | Count | Mechanism | Candidate | Class |
|---|---|---|---|---|---|
| Rendering (engine) | 6.3 | ~1,500 draws | draw recording, canvas overlay drawing, texture-upload copies (20 MB/frame of animation-batch textures the game re-applies) | engine work: canvas dirty tracking, per-draw bookkeeping | A |
| `KBatchedAnimUpdater.UpdateRegisteredAnims` | 3.2 | 89,794 anim controllers | `UpdateAnim` per registered (playing) animation per frame, `KAnimSynchronizer.SyncTime`, dirty marking | measure registered counts; skip work for controllers whose frame does not change | A/B, to study |
| `CellChangeMonitor.RenderEveryTick` | 2.8 | objects moving per frame | per moved transform: dictionary lookup, recomputing its cell, and the cell-change handlers (object layers, decor, partitioner) | mostly driven by conveyor items moving each frame (below) | — |
| `BrainScheduler.RenderEveryTick` | 2.7 | 1,186 critters, 24 Duplicants | brain updates; `ClosestPickupableSensor.Update` 25%, fetch updates, food and tree searches | sensor result caching between updates of the same cells | B, to study |
| `AnimEventManager.Update` | 2.5 | playing animations with events | per event-carrying animation per frame: frame events (sounds 13%) | — (game logic) | — |
| `ReachabilityMonitor` (`UpdateReachabilityCB`) | 2.5 | tracked workables (subset of 441,253 `Workable`) | once per game second per workable: cell from the transform, the work offsets, `MinionGroupProber.IsAllReachable` (cheap array reads). Volume and cache misses | for workables that cannot move (buildings), cache cell and offsets | A |
| `Sublimates.Sim200ms` | 2.4 | 2,457 items | per item per 200 ms: cell from the transform, a tag check, `GetComponent<Pickupable>` (a scan of ~30 components), element lookup by hash, before deciding. Items sealed in storage return after these | cache the Pickupable and the element; test the cheap early-outs first | A |
| Canvas collection (engine) | 2.4 | ~11,000 UI objects | the engine re-walks every active UI hierarchy each frame | dirty tracking: rebuild only on change, as Unity does | A |
| `PlantElementAbsorbers.Sim200ms` | 1.8 | plants absorbing elements | the batch itself is cheap; 70% is `EventSystem.Trigger` (irrigation monitors), whose handler lookup does type checks per handler | — (event system) | — |
| `KAnimBatchManager.UpdateDirty` | 1.7 | dirty animation batches | writing per-instance data into batch textures; 23% is memcpy | — | — |
| Workers doing work (`ToggleWork` → `StandardWorker.Work`) | 1.6 | 24 Duplicants, 62 remote worker docks | work ticks; remote worker oil refill and gunk removal split element chunks (Instantiate) each tick | — | — |
| `EnergySim.EnergySim200ms` | 1.6 | 109 generators, steam turbines | per generator per 200 ms; type checks and connection status | — | — |
| `SolidConduitFlow.Sim200ms` | 1.5 | 209 consumers, bridges | bridge and consumer updates, object-layer refreshes, splits | — | — |
| `ConduitFlow.Sim200ms` (liquid, gas) | 1.5 | 821 consumers | consumers, dispensers, bridges | — | — |
| `RequireInputs.CheckRequirements` | 1.4 | 1,918 buildings | per building: power and conduit checks, `Db.Get()` status-item toggles, a `GetComponent<Light2D>` when the state flips | small A gains only | A |
| State transitions (`TransitionUpdater`) | 1.2 | state machines with polled transitions | radiation vulnerability, fertility, incubation, gunk and reach checks | — | — |
| `CritterTemperatureMonitor` | 1.1 | 1,186 critters | target state per critter per tick; `IsEntirelyInVaccum` loops the critter's occupied cells | — | — |
| `SolidConduitFlow.RenderEveryTick` | 1.1 | every conveyor segment | every frame, every item in transit is moved along its rail with `transform.SetPosition`. Each move also marks it dirty for `CellChangeMonitor` and its animation batch | skipping off-screen items would change cell-change timing | C |
| `HighEnergyParticle.CheckCollision` | 0.9 | **4 particles** | each particle each frame gathers every collider in the 3×3 cells around it (93,840 colliders in the colony, stacked items in storage) and calls `GetComponent<HighEnergyParticle>` on each to find other particles | test the entry against the few live particles before any `GetComponent` | A |
| `RanchStation` | 0.9 | ranch stations × critters | each station checks ranchable critters; type checks dominate | — | — |
| Input (`StandaloneInputModule`) | 0.7 | — | UI raycasts on mouse events | — | — |
| `KCollider2D.RenderEveryTick` | 0.7 | moving colliders | partitioner re-insert of moved colliders, extents through the transform | — | — |
| `NavGrid.UpdateGraph`, `ScenePartitioner.Sim1000ms`, conveyor loaders, meters, rockets | ≤0.6 each | | | | |

## What the engine still costs (perf)

| Entry | ms |
|---|---|
| Rendering phase | 6.25 |
| Canvas build (collection) | 2.38 |
| `Components.First` (GetComponent from game code) | 0.87 |
| Canvas update | 0.47 |
| `NativeArray.Copy` (animation batch data) | 0.41 |
| Instantiate, activation, destroy | 0.6 |

## What the first tweaks measured

- **`fast-particle-collision`: 0.94 → 0.44 ms.** Done (docs/PLAN.md "Game tweaks"). The cost was
  the collider gather around each radbolt, not the component lookups; the exact skip of gathers
  with no other radbolt registered nearby (74% of them in U57) is what saved time. Verified on
  40,000 checks.
- **Sublimation: no gain, dropped.** 76% of the 2,490 calls a frame are items in full gas cells
  already marked blocked, for which the method changes nothing; an exact early-out for them
  (verified on 4 million calls) still costs 2.55 ms against 2.37 without it. What remains
  (reading the item's position, the cell's mass, its status, element and temperature) costs as
  much as the whole method: the time is in touching 2,457 scattered objects, not in the lookups
  skipped. Only processing fewer items would save time, which changes behaviour (class C).

- **Canvas build cache: no gain, dropped.** A cache of the whole canvas build, invalidated by
  what the build reads (activation, components and children of the objects it visited, element
  materials, emptiness and culling, groups, canvas sorting), never hit in U57: per frame the UI
  changes about 6 element states, 4 activations, 2 groups and 2 hierarchies, nearly all inside
  the one screen-space canvas that holds the whole HUD. Only incremental building (per subtree,
  depths renumbered) would save time there.

- **Animation updates: real work.** Per frame about 4,400 registered controllers are visited:
  3,400 visible and playing (advancing their frame and batch data), 770 off-screen (returning
  early), 270 inactive; 1,400 of them moved this frame (conveyor items and critters, the same
  movement behind CellChangeMonitor). 3.2 ms is about 0.8 µs per playing animation on screen:
  nothing to skip exactly; it scales with how much of the colony is in view.

- **Cell-change handling: real work.** Per frame 1,300–1,400 transforms are marked moved and
  280–420 of them enter a new cell, running the game's handlers (object layers, partitioner,
  decor) that make up most of CellChangeMonitor's 2.8 ms. Nothing to skip exactly; most of the
  moves are conveyor items interpolated along their rails every frame (SolidConduitFlow
  RenderEveryTick), which could only be reduced by changing when items move (class C).

- **Draw recording, per-block uniform ownership cache: no gain, dropped.** Caching, per program and
  block, which uniforms a draw's block sets (valid while the block's set of properties is
  unchanged; 1.6 million cached answers verified) removed the lookups from the profile but moved
  GlDevice.Draw only from 4.12 to 4.05 ms. The rest of recording resolves values that do change;
  what remains is structural (recording on another thread, pipelined with the next frame).
- **Canvas walk, hidden screens skipped: 2.1 → 0.8 ms.** Done. Per frame the walk visited
  11,500 objects: 3,100 inactive, and 8,900 below CanvasGroups at alpha 0 (the game's closed
  screens stay active, hidden by their groups), whose 5,500 renderers only had their depth set
  to -1. Each object now carries a version bumped up its ancestor chain at every change that
  could alter such a walk (children, activation, components, enabled state, canvas and group
  settings). The top of a hidden subtree remembers the version at which a walk found it all
  hidden with no group that ignores its parents. While that version holds, its renderers
  already hold -1 and the walk skips it. Verified (ONI_VERIFY_CANVAS_SKIPS=1) on 20,000 skips,
  including runs that open and close the management screens, with no difference.
- **World rotation and lossy scale cached: 1.1 ms off every frame.** TextMeshPro checks every
  text's lossyScale each frame (RescaleIfScaleChanged, from willRenderCanvases), and the
  transform computed it, with its world rotation, by walking up the parents on every read. Both
  are now kept with the world matrix and invalidated with it (MarkDirty); the arithmetic is
  unchanged. RescaleIfScaleChanged and the rotation reads went from 1.46 to 0.35 ms a frame
  (perf). U57 paused in a quiet view, back to back over the same frames: 81–90 fps against
  72–78 before. The machine's other load moves a single build by 10% between runs.
- **Script dispatch:** the per-call check for behaviours removed earlier in the phase
  is skipped while none are pending (nearly always), and removals rescan only the lists of the
  removed behaviours' execution orders instead of every list (0.1–0.2 ms a frame).
- **Canvas walk, renderer-free subtrees: not worth it.** Only about 7% of its visits are in
  subtrees without any CanvasRenderer.

- **Parallel draw recording: scoped, not done.** A draw's uniforms are resolved from its inputs,
  its material and the shader globals, which command buffers change in the middle of a frame;
  recording draws on worker threads would need a flush at every global change, a per-draw copy of
  the inputs, and thread-safe material and texture caches. Only value resolution (about 60% of
  the 3.5 ms) could move, for about 1.5 ms: not worth that risk.

The same applies to the other per-item systems (reachability, critter temperature, requirement
checks): caching what a call looks up cannot remove the cost of visiting each object. Gains need
whole loops skipped exactly (as the radbolt gather) or the engine work they cause reduced.

## A quiet view

Is the frame rate low only where much is on screen? PerfProbe (`camera.txt quiet`, `speed.txt`)
holds the view on the planetoid's emptiest window. U57's main planetoid is built over nearly
everywhere (27,186 building cells); its emptiest 48×28-cell window still holds 248 of them.

| U57, quiet view | fps | ms/frame |
|---|---|---|
| paused | 83–87 | ~12 |
| speed 1 | 41–45 | ~23 |
| speed 3 | 20–22 | ~49 |
| speed 3, the save's busy view (for comparison) | ~15.5 | ~64 |

The busy view costs about 15 ms more per frame: world rendering (3.9 → 1.0 ms), animation
batch uploads (1.5 → 0) and animation updates for what is visible. In the quiet view at speed 3,
the game's own code is 38 of the 49 ms. Most of it is simulation, which has a fixed amount of
work per game second, so at speed 3 it takes three times the share of each real second that it
takes at speed 1. Some game systems also run every frame whether or not anything is in view:
- BrainScheduler, 2.5 ms;
- CellChangeMonitor, 2.0 ms;
- AnimEventManager, 1.8 ms: it also detects animations ending, which state machines wait on, so
  off-screen animations cannot be skipped;
- KBatchedAnimUpdater, 1.5 ms.

The engine's part of a paused frame was about 4 ms: drawing the HUD, the world pass, the canvas
walk and script dispatch. The transform cache and dispatch changes above came from this
measurement.

So a quiet view at speed 3 is limited by the simulation, as the busy view is. Making it faster
means the game-side work (the per-item systems above, or the data-layout rewrite below), not
rendering.

## Data layout: what a rewrite could buy (spike)

The question: how much would rewriting per-item systems over flat arrays (one array per field,
indexed per item, as in an entity-component system) save, against the game's objects? A temporary
PerfProbe mode (not kept) timed the same decisions both ways in U57, every frame, game running at
speed 3:
- **Objects:** what the game reads now, through the components.
- **Arrays:** the same fields copied once into flat arrays, read in a loop. The caches were
  emptied (64 MB written) before each array pass, so the arrays start cold, as they would after
  the rest of a frame.

| Per-item pass | Items | Objects | Arrays | Per item |
|---|---|---|---|---|
| Sublimation's decision (cell from the position, the cell's mass against the item's limit, emit state, sealed tag, temperature from the sim's element chunk, element's low temperature) | 2,492 | 0.97–1.14 ms | 0.013–0.023 ms | ~400 ns → ~5 ns |
| A pickupable scan of the kind the fetch and sensor searches make (edible tag, valid cell, mass) | 27,551 | 4.3–4.8 ms | 0.027–0.029 ms | ~160 ns → ~1 ns |

- **The arrays compute the same answers.** Both passes found the same decisions and the same
  total mass from a fresh copy. Agreement drifted to 98% over a minute because the copies were
  not kept current.
- **The object cost is chains of dependent cache misses**, not the instructions. For example,
  HasTag reads the KPrefabID, its HashSet, the buckets and the entries; the position goes
  through the transform. The same sublimation pass with the game paused costs 0.40 ms: the
  running game's own work evicts what the pass needs.
- **Keeping the arrays current is cheap:** about 200 tag changes and 25 pickupable cell changes
  a frame across the colony (counted with patches on KPrefabID.AddTag/RemoveTag). Each is one
  array write.

**What this means.** For the per-item systems (Sublimates 2.4 ms, reachability 2.5, the brain
sensors' searches inside BrainScheduler's 2.7, critter temperature 1.1, RequireInputs 1.4,
EnergySim 1.6), almost all of the time is in visiting objects, and a table layout removes that
cost almost entirely. What would remain is the real work, the few items whose state changes
(emitting, status changes, events). The upper bound for these systems together is about 10 of
the frame's 70 ms. The other large costs are rendering, animation, cell-change handlers and the
save, which this does not touch. So even fully done, U57 would reach about 16–17 fps from 14.

**What a retrofit takes.** The objects stay the owners of the game state (saves, mods and the UI
read them). A table per system mirrors the fields its decisions read. The table is updated at
every place those fields change (tag add/remove, cell change, storage change, reservation, mass)
and read by a replacement of the system's loop that calls the game's own methods only for items
whose decision says something happens. Correctness depends on finding every write. Each table
needs a verify mode comparing it to the objects every frame (as the radbolt tweak does), and
mods that write the fields directly would bypass the mirror. A table for one system costs about
a day to build and verify; the pickupable table (tags, cell, unreserved amount, storage) would
serve the fetch, sensor and inventory searches together.

## Recommendations, in order

1. ~~Canvas dirty tracking~~: measured, the UI changes every frame (above); incremental building
   remains possible but is a larger design.
2. ~~`fast-sublimation`~~: measured, no gain (above).
3. ~~`fast-reachability`~~: the same per-object cost; not attempted.
4. **`fast-particle-collision`**: done, 0.5 ms.
5. **Engine, `GetComponent` cache:** about 0.5 ms. A per-object type-to-component cache,
   invalidated when components are added or removed. A.
6. **Studied:** animation updates (above: real work). Cell-change handling (above: real work).
   Not studied: brain sensors (2.7 ms; critter searches, where gains would change behaviour).
   These are the largest game systems, but the work they do is real; any gain needs the per-object
   counts and change rates measured first.

What remains is exact loop skips found system by system, and incremental canvas building. Most of the frame is game logic that
does real work for a colony this size, so no single change makes a large difference.
