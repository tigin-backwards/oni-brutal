# Tweaks

Besides its engine, Moono changes how some of the game's own code does its work. Most of these
tweaks give exactly the same results, faster, and are on by default. Three, marked **changes
gameplay**, trade a little exactness for speed and are off unless you turn them on. What each one
gains is measured in [PERFORMANCE.md](PERFORMANCE.md#the-tweaks).

## Choosing them

On the main menu, **Moono Tweaks** (under Mods) lists every tweak with an on/off switch and a
line on what it does, plus two options of the player's own: the Gassy Moo loading splash (on by
default) and frame stats in the log (off by default). Changes apply when the game restarts, which
the screen does for you.

Your choices are kept in `oni-moono/settings.json` in the game's own data folder (usually
`~/.config/unity3d/Klei/Oxygen Not Included` on Linux,
`%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included` on Windows), which updates never touch.
The file records only what differs from the defaults, so a default changed by an update still
reaches you unless you chose otherwise.

## Launch options

Launch options still work and win over the screen, which then shows those rows as set by the
launch options:
- `--game-tweak exact`: every tweak but the three that change gameplay.
- `--game-tweak all`: every tweak.
- `--game-tweak none`: no tweaks.
- Tweaks by name: `--game-tweak exact --game-tweak slow-reachability`. `--game-tweak -NAME` leaves
  one out of `exact` or `all`.
- `--splash moo|none`: the loading splash.
- `--frame-stats`: where each frame's time went, in the log every 60 frames.

Older setups put `--game-tweak exact` in the launch options; remove it (the setup prints the new
line) to choose in the game instead.

At start-up the player logs every option it used and where it came from (default, settings file
or command line).

## How "exactly the same" is checked

Each exact tweak has a verify mode (`ONI_TWEAK_VERIFY=1`): the tweak still takes its shortcut, but
the game's own work is done as well and the two are compared, bit for bit where the result is
data. QA's verify stage runs a late-game colony with every tweak on in this mode, every overlay
opened in turn, for at least 10 game minutes, and fails on any mismatch.

## The tweaks

### Same results, on by default

- `fast-jobs`: the game's worker threads wake only as many threads as a job has work for, instead
  of all of them every time.
- `fast-anim-events`: animations' per-frame event checks read a compact copy of what they need.
- `fast-anim-cleanup`: the animation grid is swept for removed animations only where one was
  actually removed.
- `fast-anim-batches`: the animations' data for the graphics card is prepared on worker threads
  instead of one by one.
- `fast-symbol-overrides`: a Duplicant's or building's symbol swaps aren't uploaded again when
  nothing in them changed.
- `fast-offset-tables`: where an item can be picked up from is recomputed only when something
  around it changed.
- `fast-sublimation`: sublimating items (polluted dirt, bleach stone...) that sit in full gas
  cells and can't change are skipped.
- `fast-particle-collision`: radbolts stop searching every stored item for other radbolts.
- `fast-partitioner-cleanup`: the once-a-second cleanup of the game's spatial index looks only at
  entries actually removed.
- `fast-overlays`: the overlays (temperature most of all) skip rereading what can't have changed
  since the last frame.
- `fast-conveyor-paths`: no hitch when conveyor rails change: their routes are found from an
  index instead of searching every route again.
- `fast-serialization`, `fast-save-type-checks`, `fast-save-compression`, `no-save-gc`,
  `background-save`: much shorter autosave freezes. The save is written faster, compression and
  the file write move to a background thread, and the forced memory cleanup after each save is
  skipped. The files stay compatible with the original game.

### Changes gameplay, off by default

- `off-screen-conveyors`: items travelling on conveyor rails off screen are only placed where
  they are once they stop or come into view. The rails carry exactly the same things, but the
  items' positions on screen and in the world lag behind meanwhile.
- `slow-reachability`: whether errands and items can be reached is checked every 4 s instead of
  every second, so Duplicants may notice a newly blocked or opened path up to 3 s later.
  Temperature and building checks are never delayed.
- `off-world-fallers`: items that fall out through the bottom of a world (a chute out of a rocket
  interior) hang just below it forever, still being stepped as falling. They are landed where
  they hang instead, so they stop costing time every tick.
