# Performance

Where a late-game colony's frame time goes, what made it faster, and what's still in the way.
The raw working notes, dead ends included, are in
[research/u57-frame-inventory.md](research/u57-frame-inventory.md).

## The test colony

Everything here is measured on U57, a Spaced Out colony with every DLC at cycle 17,077: 24
Duplicants, 1,186 critters, about 62,000 buildings and 90,000 animated objects, played at speed 3.

The machine is a Ryzen 7 9800X3D (8 cores, 16 threads) with an RTX 3090, running Linux under
WSL2. Both players load the same save at 1024×768, with the same probe mod (`tools/PerfProbe`)
holding speed 3 and logging the frame rate every 600 frames. Background load moves a single
build by about ±10% between runs, so comparisons are always back to back or alternating.

## The numbers

| | Original | oni-brutal |
|---|---|---|
| Frame rate, no tweaks | 6.5–6.8 fps | 17.0–18.5 fps |
| Frame rate, `exact` tweaks (the default) | | <!-- TODO: measure --> |
| Frame rate, every tweak | | 25–27 fps (mean 26) |
| Frame rate in the autosave window | 4.1 fps | 11.7 fps |
| Loading the save | 3 min 50 s | 26–28 s |
| Autosave freeze | <!-- TODO: measure --> | 11.7 s without tweaks, 4.8 s with the save tweaks |

With no tweaks it's the same game code doing the same work, so that row is the engine and
runtime alone: about 2.7 times the original's frame rate.

On Windows, before any tweaks existed, U57 went from about 8.5 fps to 17–18. A smaller base-game
colony (DeCrew) went from 17–20 fps to 48–56. At that point it was simulating nearly as fast as speed 3
allows, so the frame rate was no longer what held it back.

## Why the engine alone is faster

Most of it is the runtime. The game's code runs on CoreCLR (.NET 10) instead of Unity's Mono,
with a tiered JIT and profile-guided optimization. Its garbage collector runs in parallel on every
core, its pauses are about a quarter as long, and it's now about 1.4% of a frame.

The rest is the engine itself, profiled on U57 and fixed where the game leans on it hardest:

| Change | Saved per frame |
|---|---|
| Texture snapshots copied on worker threads (the game re-applies about 20 MB of animation textures every frame) | 2.2 ms |
| Component lookups: instances of one prefab share a cached layout, so `GetComponent` goes straight to what it returns instead of testing about 30 components | 1.7 ms |
| The UI walk skips screens hidden by their groups (the game keeps its closed screens active) | 1.3 ms |
| World rotation and scale cached (TextMeshPro reads them for every text, every frame) | 1.1 ms |
| Script dispatch skips removal checks while nothing is being removed | 0.1–0.2 ms |

Pooling draw records and resolving each material's values once per change also cut allocations
by two thirds.

## System by system, before the tweaks

This profile is from before any game tweaks: an 82 ms frame (12 fps), sampled with `perf` over
90 s of play. The game's own code was 71% of the main thread (58 ms), the engine 13% (11 ms),
and native code (memory copies, maths, GC) 7%. The last column is what happened to each system
since.

| System | ms | Objects | Why it costs that | Since |
|---|---|---|---|---|
| Rendering (engine) | 6.3 | ~1,500 draws | recording draws, and copying 20 MB a frame of animation textures the game re-applies | copies moved to worker threads, records pooled |
| Animation updates | 3.2 | 89,794 controllers | advancing each playing animation's frame, every frame | real work, scales with what's on screen |
| Cell-change monitor | 2.8 | ~1,400 moved objects a frame | recomputing each moved object's cell, and the handlers for those entering a new cell | real work, mostly conveyor items (`off-screen-conveyors`) |
| Duplicant and critter brains | 2.7 | 1,186 critters, 24 Duplicants | sensors, fetch updates, food and tree searches | real work |
| Animation events | 2.5 | ~6,450 playing animations | reading every playing animation's controller to find its frame events | `fast-anim-events`: 0.9 ms |
| Reachability | 2.5 | ~27,000 workables | every workable re-checked once a game second | `slow-reachability`: 0.53 ms |
| Sublimation | 2.4 | 2,457 items | every item five times a game second, most of them already blocked | `fast-sublimation`: about 1.0 ms |
| UI walk (engine) | 2.4 | ~11,000 UI objects | the game's closed screens stay active, hidden only by their groups | hidden screens skipped: 0.8 ms |
| Plant element absorbers | 1.8 | irrigated plants | the game's event system finding handlers | real work |
| Animation batch uploads | 1.7 | dirty batches | writing per-instance data into batch textures | `fast-symbol-overrides` skips unchanged blocks |
| Workers doing work | 1.6 | 24 Duplicants, 62 remote worker docks | work ticks, splitting element chunks | real work |
| Power | 1.6 | 109 generators | each generator, five times a game second | real work |
| Conveyor rails (sim) | 1.5 | 209 consumers | bridges, consumers, object-layer refreshes | real work |
| Pipes | 1.5 | 821 consumers | consumers, dispensers, bridges | real work |
| Building input checks | 1.4 | 1,918 buildings | power and pipe connections, status items | real work |
| State machine transitions | 1.2 | polled transitions | radiation, fertility and incubation checks | real work |
| Critter temperature | 1.1 | 1,186 critters | each critter's cells checked every tick | real work |
| Conveyor items moving | 1.1 | ~950 items | every item in transit moved along its rail, every frame | `off-screen-conveyors` (changes gameplay) |
| Radbolt collisions | 0.9 | **4 radbolts** | each one gathers all 93,840 colliders around it and asks every one whether it's a radbolt | `fast-particle-collision`: 0.44 ms |
| Ranch stations | 0.9 | stations × critters | type checks on every ranchable critter | real work |
| UI input | 0.7 | | raycasts on mouse events | |
| Moving colliders | 0.7 | | partitioner re-inserts | real work |

"Real work" means the time goes to objects that really do need visiting, and nothing exact was
found to skip.

## Where a frame goes now

Main-thread time at about 24 fps with every tweak, from a stopwatch around each entry point,
averaged over 600-frame windows. Indented rows are part of the row above.

| Entry point | ms a frame |
|---|---|
| `Game.Update` | 19.2 |
| · `Game.SimEveryTick` | 16.9 |
| · · state machine sim sub-ticks (about 7 a frame) | 12.1 |
| · · `Game.UnsafeSim200ms` | 4.8 |
| · · · waiting on the native simulation step (SimDLL) | 4.4 |
| `Game.LateUpdate` | 5.7 |
| `StateMachineUpdater.RenderEveryTick` | 4.7 |
| · Duplicant and critter brains | 4.0 |
| `Global.LateUpdate` (animation updates) | 2.6 |
| `CellChangeMonitor.RenderEveryTick` | 1.7 |
| `World.LateUpdate` | 1.4 |
| Animation batch uploads | 1.0 |
| Animation events | 0.8 |

Rendering is mostly off the main thread now. The GL thread is busy for about 22 ms a frame in
parallel, 10 ms of it uploading textures.

About two thirds of what's left is the game's own logic, spread over dozens of systems. Each one
reads thousands of scattered objects, and none takes more than a few milliseconds.

## The tweaks

All of them are opt-in (`--game-tweak NAME`). The last column says whether the game's results
stay exactly the same; the two that don't are left out of `--game-tweak exact`.

| Tweak | Before → after | Results |
|---|---|---|
| `fast-jobs`: worker threads aren't all woken and waited on for every small job (about 14 a frame) | about +11% frame rate | exact |
| `fast-anim-events`: the animation event loop reads a compact copy of each controller | 2.07 → 0.90 ms | exact |
| `fast-sublimation`: items already blocked by full gas cells are skipped in one batched update | 1.45 → about 1.0 ms | exact |
| `fast-particle-collision`: a radbolt skips the collider gather when no other radbolt is nearby | 0.94 → 0.44 ms | exact |
| `fast-offset-tables`: fetch updates recheck only items whose work spots can have changed | 1.74 → 1.33 ms | exact |
| `fast-anim-cleanup`: the sweep for destroyed animations skips chunks that can't hold any | 0.48 → 0.12 ms | exact |
| `fast-symbol-overrides`: unchanged symbol-override blocks aren't uploaded again | 1.47 → 1.20 ms | exact |
| Save tweaks: `fast-serialization`, `fast-save-type-checks`, `fast-save-compression`, `no-save-gc`, `background-save` | autosave freeze 11.7 → 4.8 s | exact, and saves still load in the original |
| `slow-reachability`: reachability re-checked every 4 game seconds instead of every second | 1.88 → 0.53 ms | changes timing |
| `off-screen-conveyors`: items on rails off screen are placed when they stop or come into view | skips most of about 950 moves a frame | changes positions while off screen |

The exact tweaks that skip work each have a verify mode, which recomputes whatever was skipped
and counts disagreements. `scripts/qa.sh` runs them all on U57 for at least 10 game minutes and
requires zero. `fast-symbol-overrides` doesn't need one: it only skips writing values that are
already there.

## Tried and dropped

Measured ideas that didn't pay off, so nobody needs to try them again:
- Caching the whole UI build: it never hit, because the HUD changes every frame.
- Skipping uploads of identical animation frames: not exact (some objects change their data
  without marking it dirty), and no faster anyway.
- Recording draws on worker threads: the game changes materials between draws, so each draw's
  inputs would need copying, which costs about as much as it saves.
- Early-outs inside per-item systems like sublimation and sensors: the cost is reaching each
  object at all, not the work after that.
- Equivalents of Fast Track (Peter Han's mod), reviewed option by option: the ones that would
  matter here all change what the game does.

## What's left

The biggest per-object costs are animation updates, cell-change
handlers, brains, conduits, power and critter temperature, at 1–3 ms each. It's spread thin, and
the time goes to cache misses chasing object references, not to computation.

A spike compared flat arrays against reading the game's objects. Sublimation's
decision dropped from about 400 ns to about 5 ns per item, and a pickupable scan from about
160 ns to about 1 ns. Rewriting the per-item systems that way could save up to about 10 ms of a
70 ms frame. But mods and the UI write the same game state, so it would mean rewriting the game's
systems rather than tweaking them.

## Loading

Loading U57 takes 26–28 s, against 3 min 50 s in the original. About two thirds of that is the
game's own work (reading the save and setting up each object), and about a quarter is the engine
creating those objects. Unloading unused assets during the load takes another 1.2 s. The JIT
isn't the bottleneck: compiling ahead of time (ReadyToRun) saved nothing measurable.
