# Main-thread work on other threads (2026-10-23)

Which of the main thread's work can be split across threads, or moved off the main thread,
without changing the game's results, judged against the per-system costs in
[gluttony-frame-inventory.md](gluttony-frame-inventory.md) and
[u57-frame-inventory.md](u57-frame-inventory.md). Those inventories call animation updates and
batch writes "not candidates" in the sense of skipping work; this note asks whether the same
work can run in parallel. The rules are the tweaks' rules: the same results by construction,
proved by a verify mode.

Two shapes are possible here. Klei's `GlobalJobManager.Run` is fork-join: the main thread hands
out items and waits, so only the time the items take together comes back. `AsyncPathProber`
runs on threads of its own and the main thread never waits for it. Everything below is
fork-join; nothing found could run truly asynchronously, because the frame needs each result
before it goes on.

## Method

- **Mechanisms:** read from Klei's decompiled code (`KAnimBatch`, `KAnimBatchManager`,
  `BatchSet`, `KBatchedAnimController`, `KBatchedAnimInstanceData`, `SymbolInstanceGpuData`,
  `SymbolOverrideController`, `FaceGraph`, `NavGridUpdater`, `CellChangeMonitor`,
  `WorldInventory`) and from this engine's code for the engine calls they make.
- **Profiles:** `perf` on Linux as in the inventories (cpu-clock, 499 Hz, JIT symbols through
  perf maps), on Gluttony's cleaned save (`presync-clean.sav`) with every tweak: the inventory's
  own profile, and one with the prototype.
- **Timings:** each prototype's own stopwatch (`ONI_TWEAK_TIMING=1`): main-thread wall time of
  the patched entry point, averaged over 600-frame windows, the same build run serially
  (`ONI_FAST_ANIM_BATCHES_SERIAL=1`) and in parallel, back to back under the machine-wide perf
  lock. Other agents' players were running on the machine (Gluttony at 15–20 fps, against 24
  in the inventory), so only the steady windows (the third onwards) are compared.

## `KAnimBatch.UpdateDirty` (Gluttony 1.9 ms, U57 1.7 ms)

`KAnimBatchManager.UpdateDirty` walks the visible batch sets in list order; a dirty set walks
its batches; a batch with `needsWrite` runs `KAnimBatch.UpdateDirty`, which for each index in
its `dirtySet`, in order:
1. skips a null or destroyed controller;
2. **W**, the instance data: `KBatchedAnimInstanceData.WriteToTexture` reads the controller's
   frame index, frame count, visibility, post-processing parameters, first frame, position
   (`GetPositionData`, or a `KBatchedAnimTracker` delegate reading its parent's pivot symbol)
   and transform matrix (`GetTransformMatrix`: transform position, rotation, scale), stores
   them in the controller's own 112-byte record and copies it into the batch's data texture at
   the controller's index;
3. **S**, the symbol instance block: when the batch's slot for that index holds another block
   or version, copies the controller's `SymbolInstanceGpuData` into the batch's symbol texture
   and updates the slot;
4. **A**, `ApplySymbolOverrides`: re-applies the batch's atlases to its property block, and
   for a controller with a `SymbolOverrideController` re-applies its overrides when their
   version changed or every frame if asked (sorting, pooled dictionaries,
   `KAnimBatchManager.GetBatchGroupData`, which can insert, `FaceGraph.ApplyShape`,
   `SpeechMonitor.DrawMouth`, which adds an override and marks the controller dirty), then its
   own atlases; creates the batch's override texture on first need;
5. **O**, the override block: as S, for `SymbolOverrideInfoGpuData` into the override
   texture.

Then the batch's dirty set is cleared and its textures' `Apply` called.

### What it touches

| State | Who writes it | Shared between batches? |
|---|---|---|
| The batch's data, symbol and override textures, its slots, dirty set, `needsWrite` | its own update | no: one batch's |
| A controller's instance record and transform matrix | W of that controller | no: a controller is registered with one batch |
| `SymbolInstanceGpuData`, `SymbolOverrideInfoGpuData` | the controller's setters (A for overrides) | no: per controller |
| The batch's `MaterialPropertyBlock`, `AtlasList` | A | no, but A is game code |
| `KAnimBatchTextureCache` (static), `KAnimBatchManager.batchGroupData` | `Init`, override texture creation, A | **yes** |
| `DictionaryPool`/`ListPool` of `SymbolOverrideController` (static) | A | **yes** |
| Transforms' cached world matrices and rotations | read lazily by W (a tracker reads its parent's) | **yes**: a parent can be read from two batches |
| `BatchSet.dirty` | A (through `SetDirty`) and the set's walk | per set |

No static scratch buffer is used by W, S or O; `KBatchGroupData` (frame elements, symbol
counts) is only read by them.

### Is the order observable?

Not between batches for W, S and O: each writes its own batch's memory, reading state that no
other batch's update changes. A is the exception: it is game code with shared pools and
dictionaries, and the order of its calls is the order of the events it raises. So W and S can
run for every batch before the loop, and O after it, if A of one controller never changes what
W, S or O of another reads. It does not: A writes only its own controller's override state,
its batch's atlases and property block, the shared pools (not read by W, S, O), and the
controller's dirty flag (already set). Within one controller, W and S come before A in the game,
and stay before it; O comes after A, and stays after it.

### The engine calls

`Texture2D.GetRawTextureData<T>` (a view of the texture's memory; it first waits for the GL
device's snapshot of that texture, if one is still copying), `NativeArray.Copy` (a span copy),
`Texture2D.Apply` (bumps the content version; no mip levels to rebuild here; nothing reads the
memory until the frame is drawn), `Transform.position`/`rotation`/`localScale`,
`Component.transform` (a cached reference), and Unity's null test (a volatile read). None
asserts the main thread. All are per-object except the transform caches: a world matrix or
rotation is computed on first read after a change and kept with a dirty flag. Two threads
reading the same transform both compute the same value; the flag is now written with release
and read with acquire semantics (plain moves on x64), so a reader that finds it clear also sees
the stored value (`TransformConcurrentReadTests`).

### Prototype: `fast-anim-batches`

- A prefix on `KAnimBatchManager.UpdateDirty` (last of the prefixes, only if the original runs)
  collects the dirty batches the walk will reach and, when they hold at least 128 dirty
  controllers, runs W and S for each on `Parallel.For`, one batch per work item, recording per
  index that it was written.
- A transpiler on `KAnimBatch.UpdateDirty` routes its W, S and O calls through the tweak: W and
  S return at once for an index written ahead (S with the game's answer), O makes the game's
  slot test and, when the block is stale, returns "written" and queues the copy.
- A postfix makes the queued O copies on workers, one batch per item, before anything else runs.
- Left to the game's loop: UI batches (`GetTransformMatrix` reads canvas layout and writes
  fields), batches being set up, controllers not exactly `KBatchedAnimController`, position
  delegates other than the tracker's, and every frame when another Harmony owner patches a
  method on the worker path.
- Verify mode (`ONI_TWEAK_VERIFY=1`): at each skipped call's original place, W and S are redone
  into scratch memory and compared byte for byte with the worker's; S's slot must match the
  controller's version; a controller written ahead that the loop never reached is a mismatch;
  each deferred O block must have kept the version it had when the loop reached it.

| Gluttony, Linux, stopwatch | Serial | Parallel |
|---|---|---|
| `KAnimBatchManager.UpdateDirty`, steady windows (ms a frame) | 1.92–2.01 | 1.49–1.65 |
| of which the workers' phases (wall) | | 0.55–0.61 |
| Frames run in parallel | | 94% |

Verified: Gluttony, 3.2 million writes ahead and 55,000 deferred override blocks, 6.4 million
checks, 0 mismatches; U57, 5,400 frames (96% in parallel): 4.3 million writes ahead, 16,000
deferred override blocks, 8.6 million checks, 0 mismatches.

Why it saves only about a fifth: in the profile with the tweak, what stays on the main thread
is A, 1.0 ms (`SymbolOverrideController.ApplyOverrides` 0.79, of which
`fast-symbol-overrides`' value comparison 0.42, `FaceGraph.ApplyShape` 0.13), the atlas
re-applies 0.24, and the loop's own visits to each controller. The workers' 0.6 ms wall time
did what took the main thread about 1 ms; the rest of the frame-time win is lost to the fork
and join and to the main thread then reading the same controllers again, from the shared cache
instead of memory. A is game logic with shared pools and events, so it stays.

## Other candidates

Costs are the inventories' Linux main-thread ms a frame (Gluttony unless marked).

| Work | ms | What blocks it | Verdict |
|---|---|---|---|
| Animation batch writes (above) | 1.9 | A is game code | **done**: `fast-anim-batches`, about 0.4 ms |
| `NavGrid.UpdateGraph`, the links phase | 0.5 of 0.87 | nothing: per-cell, disjoint writes, reads only the nav table already updated; the game runs the same code on its job threads for full rebuilds | **tried, dropped**: prototyped exactly (links computed on workers, written in the game's order with its overflow behaviour; 20 million cells verified identical). About 19 updates a frame of about 40 µs each; the fork and join cost more than they saved (0.55–0.63 serial, 0.54–0.61 with only updates of 600 cells or more in parallel, worse below that) |
| Queued draw recording (`FrameRenderer.DrawQueue` → `GlDevice.Record`) | ~3 (5.1 of a 70 ms frame) | the recorder's caches written on first use (program variants per material, programs, material values per version, texture and mesh preparation, which schedules uploads), the per-frame arena | **candidate, engine.** The earlier ruling holds for command buffers and immediate draws; but a queue is a plain loop over sorted draw items with no game code in between, so its inputs are stable while it records. A serial pass for the cache-filling steps, uniform resolution into per-draw slots on workers, posting to the GL worker in order: estimated 1–1.5 ms. Canvas overlays (1.0) are the same kind of loop. Not prototyped |
| `WorldInventory.Update` | 0.53 | none: per item `GetMyWorldId`, a tag test and `TotalAmount` are reads | **candidate (a):** read each item's amount on workers, sum on the main thread in the set's order (float sums keep their order). ~0.3 ms; not prototyped |
| Reachability checks (`ReachabilityMonitor`) | 0.62 | each changed result triggers its state machine | **candidate (a):** compute `IsAllReachable` on workers, apply changes in order (the prober's grid is already read while its own threads write it). ~0.4 ms; not prototyped |
| `fast-sublimation`'s batch | 0.46 (U57 ~1.0) | full updates of earlier items in the batch | **to study:** the skip decisions read the item, its cell's mass from the sim's last step and its chunk temperature, which earlier full updates do not change within a batch; decisions on workers, updates in order |
| `KBatchedAnimController.UpdateAnim` | 1.59 | almost every branch has effects: batch dirty lists shared by a batch's controllers, re-registration on chunk change, `SuspendUpdates` editing the updater's lists mid-walk, synchronizers driving other controllers, events | no |
| `CellChangeMonitor.RenderEveryTick` | 1.42 | handlers (1.0 of it) run in order and move other transforms and add handlers before their entries are reached; only the cell computation (0.4) is pure, and revalidating it reads the same transforms | no |
| Animation events (`fast-anim-events` loop) | 0.72 | events change later entries and the entry list | no |
| Brains: chore search, sensors | 2.4 | preconditions with caches and reservations, order-dependent | no (fetch updates already run on the job threads) |
| `RequireInputs.CheckRequirements` | 0.83 | status-item toggles and events per building; the check would need reimplementing to split reads from writes | not worth it (≤0.4) |
| Critter temperature | 0.38 | transitions in order | small (≤0.2) |
| Canvas update and build | 1.56 | layout and graphic rebuild callbacks in order; TextMeshPro adds glyphs to shared font atlases | no |
| Input module | 0.54 | raycast filters are game code; sorting needs the same input order | small |
| `PropertyTextures.LateUpdate` copies | 0.3 | `LoadRawTextureData` must copy before returning (the caller may reuse its buffer); only splitting the copy across threads, which is memory-bound | not worth it |
| Sim sub-ticks (386 systems) | 13 | shared game state, order of callbacks observable | no |
| Instantiate, destroy, behaviour dispatch | ~1.5 | order of object creation and callbacks | no |

## What's left

The one large structural candidate is recording queued draws on workers (estimated 1–1.5 ms,
engine-side, no game code involved). Of the game-side ones, the reads in `WorldInventory` and
reachability can be split from their effects exactly, for a few tenths of a millisecond each.
The rest of the main thread is game logic whose order is part of its results.
