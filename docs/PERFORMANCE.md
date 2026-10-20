# Performance

Where a late-game colony's frame time goes, what made it faster, and what's still in the way.
The full working notes, including every dead end, are in
[research/u57-frame-inventory.md](research/u57-frame-inventory.md).

## The test colony

Everything here is measured on **U57**:
- Spaced Out with every DLC, cycle 17,077;
- 24 Duplicants and 1,186 critters;
- about 62,000 buildings and 90,000 animated objects;
- played at speed 3.

The machine is an AMD Ryzen 7 9800X3D (8 cores, 16 threads) with an RTX 3090, running Linux
under WSL2. Both players ran on the same save, at 1024×768, with the same probe mod
(`tools/PerfProbe`) loading the save, holding speed 3 and logging the frame rate every 600
frames.

The machine's background load moves a single build by about ±10% between runs. So every
comparison below is either back to back or alternating runs.

## Headline numbers

| | Original | oni-brutal |
|---|---|---|
| Frame rate, no tweaks | 6.5–6.8 fps | 17.0–18.5 fps |
| Frame rate, `exact` tweaks (the default) | | <!-- TODO: measure --> |
| Frame rate, every tweak | | 25–27 fps (mean 26) |
| Frame rate in the autosave window | 4.1 fps | 11.7 fps |
| Loading the save | 3 min 50 s | 26–28 s |
| Autosave freeze | <!-- TODO: measure --> | 11.7 s without tweaks, 4.8 s with the save tweaks |

The no-tweak row is the engine and runtime alone: about 2.7 times the original's frame rate,
from the same game code doing the same work.

On Windows (native driver, the original in an isolated copy), U57 went from about 8.5 fps to
17–18 before the game tweaks existed. The base-game colony DeCrew went from 17–20 fps to 48–56.
At that point a game cycle took 210 s against 230 s; speed 3 allows no less than 200 s, so the
simulation's own pace was the limit there, not the frame rate.

## Why the engine alone is faster

- **The runtime.** The game's code runs on CoreCLR (.NET 10) instead of Unity's Mono. It gets a
  tiered JIT with profile-guided optimization, and a generational garbage collector. With server
  GC, collections run in parallel on every core, and its pauses are about a quarter as long. GC
  is now about 1.4% of a frame.
- **Engine paths shaped by the game's habits.** The engine was profiled on U57 and fixed where
  the game leans on it hardest:

| Change | Saved per frame |
|---|---|
| Component queries: one prefab's instances share a cached layout, so `GetComponent` reads only the components it returns, instead of testing about 30 | 1.7 ms |
| Canvas walk skips UI screens hidden by their groups (the game's closed screens stay active) | 1.3 ms |
| World rotation and scale cached with the world matrix (TextMeshPro reads them for every text, every frame) | 1.1 ms |
| Texture snapshots copied on worker threads (the game re-applies about 20 MB of animation textures a frame) | 2.2 ms |
| Draw records pooled, uniform values resolved once per material version, allocations down two thirds | (U57 12.5 → 17 fps with the profile's other fixes) |
| Script dispatch: no per-call removal checks while nothing is pending | 0.1–0.2 ms |

## Where a frame goes now

Main-thread wall time at about 24 fps with every tweak (a stopwatch around each entry point,
mean of 600-frame windows). Nested rows are part of the row above.

| Entry point | ms a frame |
|---|---|
| `Game.Update` | 19.2 |
| · `Game.SimEveryTick` | 16.9 |
| · · state machine sim sub-ticks (about 7 a frame) | 12.1 |
| · · `Game.UnsafeSim200ms` | 4.8 |
| · · · the native simulation step (SimDLL): waited on, not reducible here | 4.4 |
| `Game.LateUpdate` | 5.7 |
| `StateMachineUpdater.RenderEveryTick` | 4.7 |
| · Duplicant and critter brains | 4.0 |
| `Global.LateUpdate` (animation updates) | 2.6 |
| `CellChangeMonitor.RenderEveryTick` | 1.7 |
| `World.LateUpdate` | 1.4 |
| Animation batch uploads | 1.0 |
| Animation events | 0.8 |

Rendering itself is mostly off the main thread. The GL thread is busy about 22 ms a frame in
parallel, 10 ms of it uploading textures.

About two thirds of the frame is the game's own logic, spread over dozens of systems. Each one
reads thousands of scattered objects, and none of them is above a few milliseconds.

## What the tweaks bought

All are opt-in (`--game-tweak NAME`). The class says whether the game's results stay exactly the
same; the two that don't are left out of `--game-tweak exact`.

| Tweak | Before → after | Results |
|---|---|---|
| `fast-jobs`: worker threads aren't all woken and waited on for every small job run (14 a frame) | about +11% frame rate | exact |
| `slow-reachability`: reachability re-checked every 4 game seconds instead of every second | 1.88 → 0.53 ms | changes timing |
| `fast-anim-events`: the animation event loop reads a compact mirror of each controller | 2.07 → 0.90 ms | exact |
| `fast-sublimation`: items already blocked by full gas cells skipped in a batched update | 1.45 → about 1.0 ms | exact |
| `fast-particle-collision`: radbolts skip collider gathers with no other radbolt nearby | 0.94 → 0.44 ms | exact |
| `fast-offset-tables`: fetch updates recheck only items whose work offsets can have changed | 1.74 → 1.33 ms | exact |
| `fast-anim-cleanup`: the destroyed-animation sweep skips chunks that can't hold any | 0.48 → 0.12 ms | exact |
| `fast-symbol-overrides`: unchanged symbol-override blocks aren't uploaded again | 1.47 → 1.20 ms | exact |
| `off-screen-conveyors`: items on rails off screen are placed when they stop or come into view | conveyor moves of about 85% of 950 items | changes positions while off screen |
| Save tweaks (`fast-serialization`, `fast-save-type-checks`, `fast-save-compression`, `no-save-gc`, `background-save`) | autosave freeze 11.7 → 4.8 s | exact (saves load in the original) |

The exact per-frame tweaks that skip work have a verify mode, which recomputes what was skipped
and counts disagreements. `scripts/qa.sh` runs them on U57 for at least 10 game minutes and
requires zero. `fast-symbol-overrides` is exact by construction: it skips only writes of values
already held.

## Tried and dropped

These are measured ideas that didn't pay off, so they don't need trying again:
- **Caching the whole canvas build:** never hit, because the HUD changes every frame.
- **Skipping uploads of identical animation frames:** not exact (some objects change data
  without marking it dirty), and no faster anyway.
- **Recording draws on worker threads:** the game changes materials between draws, so each draw's
  inputs would need copying, which costs about what it saves.
- **Early-outs inside the per-item systems (sublimation, sensors):** the cost is reaching each
  object at all, not the work after it.
- **Fast Track (Peter Han's mod) equivalents:** reviewed option by option. The ones that would
  matter here change what the game does.

## What's left

The rest of the frame is real game work. The biggest per-object costs are animation updates,
cell-change handlers, brains, conduits, power and critter temperature, each 1–3 ms. It's spread
thin, and each system's time goes to cache misses chasing object references, not to
computation.

A spike measured how much flat arrays would help, against reading the game's objects:
- sublimation's decision fell from about 400 ns to about 5 ns per item;
- a pickupable scan fell from about 160 ns to about 1 ns per item.

Rewriting the per-item systems that way could save up to about 10 ms of a 70 ms frame. But it
means mirroring game state that mods and the UI also write, so it's a rewrite of the game's
systems rather than a tweak, and it isn't done.

## Loading

Loading U57 takes 26–28 s against 3 min 50 s in the original:
- about two thirds is the game's own work (reading the save, setting up each object);
- about a quarter is the engine creating those objects;
- unloading unused assets during the load takes 1.2 s.

The JIT isn't the limit. Compiling ahead of time (ReadyToRun) saved nothing measurable.
