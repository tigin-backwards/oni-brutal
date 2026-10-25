# Moono

An unofficial, super fast, super sussy replacement for the Unity layer of Oxygen Not Included,
artisanally crafted by Claude. On a late-game colony the game runs at about 7× the original's
frame rate ([Performance](#performance)).

I did this mostly for fun, to see how far Oxygen Not Included's performance can go on a modern
.NET runtime (CoreCLR). I've had the idea for a while, but none of the existing C# renderers
sparked joy. When [BRUTAL](https://github.com/ahwootech/BRUTAL) went open source, it seemed like
a good excuse to try a sexy new toy.

Klei's game DLLs load as they are, apart from three small compatibility fixes. Whatever they call
into Unity for is reimplemented here.

## Setup

You need your own copy of the game on Steam, on Linux or Windows (no macOS). Because this replaces
the engine rather than patching the game, it can't ship as an ordinary Harmony mod. Instead, a
setup script hooks it into the game's Steam launch options, so you still start the game from
Steam as usual.

1. Download the latest release from the
   [releases page](https://github.com/tigin-backwards/oni-moono/releases/latest):
   `oni-moono-…-windows-x64.zip` or `oni-moono-…-linux-x64.tar.gz`, and extract it anywhere.
   (Or clone this repository; the setup then builds the player itself, installing the .NET SDK
   for your user if you don't have it.)
2. Run the setup script in the extracted `oni-moono` folder:
   - Windows: double-click `setup.cmd` (it runs `setup.ps1` and keeps the window open)
   - Linux: `./setup.sh`

   It finds your Oxygen Not Included install through Steam, and prints one line for the game's
   launch options, and puts it on your clipboard where it can.
3. In Steam, right-click Oxygen Not Included → Properties → Launch Options, and paste that line.

Then press Play in Steam as usual. The optional tweaks (below) are chosen in the game: **Moono
Tweaks** on the main menu, under Mods. To update, get the new version and run the setup again; the
launch options stay the same, and so do your tweak choices. To go back to the original game, clear
the launch options.

Coming from a release from before the Moono name (oni-brutal)? Its launch options start the old
player (`OniBrutal`), so run the new setup once and paste the new line it prints (the player is
now `OniMoono`, installed in an `oni-moono` folder). Your tweak choices carry over; the old
`oni-brutal` folder can be deleted afterwards.

On Linux, Steam installed as a Flatpak or a snap runs games in a sandbox that can't see most of
your home folder; the setup notices and installs the player inside the sandbox's own folder.
(Snap is tested; Flatpak isn't yet.)

You'll need an OpenGL 4.5 graphics driver. The game uses your normal Steam account, DLCs, saves,
settings and mods.

## Performance

The biggest change is swapping the very garbage Mono runtime that Unity ships for CoreCLR. On
top of that there are optimizations tuned to the way Oxygen Not Included uses the engine.

On a late-game save (U57: Spaced Out with every DLC, cycle 17,000+), on Windows, on the same machine, back to back:

| | Original | Moono | |
|---|---|---|---|
| Frame rate at speed&nbsp;3 (default tweaks) | about 11&nbsp;fps | about 78&nbsp;fps | about 7× |
| Frame rate at speed&nbsp;3 (every tweak) | about 11&nbsp;fps | about 84&nbsp;fps | about 7.4× |
| Loading the save | 2&nbsp;min&nbsp;22&nbsp;s | about 35&nbsp;s | about 4× faster |
| Autosave freeze | about 26&nbsp;s | about 2.3&nbsp;s | about 11× shorter |

Without the save tweaks the autosave freeze is about 8 s. They bring it down to about 2.3 s by
writing the save faster and moving the compression and file write to a background thread.

Garbage-collection hitches are mostly gone: collections now run in the background, spread
across every core, and their pauses are about a quarter as long.

**Note:** This uses more memory compared to the original. On a mid-game colony, memory peaked at
around 11GB resident compared to 7GB in the original. This is expected behavior for faster,
parallel collection.

Measured on a Ryzen 7 9800X3D with an RTX 3090. A mid-game colony gains less (about 3.5×, from
26 fps to 91); [docs/PERFORMANCE_CASE_STUDY_2.md](docs/PERFORMANCE_CASE_STUDY_2.md) looks at why.

https://github.com/user-attachments/assets/4d230247-9966-43d6-862f-589bad164382

The same save, view and machine on Windows, recorded back to back; each side's frame rate was
measured while it was recorded.

For a more detailed breakdown, see [docs/PERFORMANCE.md](docs/PERFORMANCE.md).

### Optional tweaks

These change how some of the game's own code does its work. Most give exactly the same results,
faster, and are on unless you turn them off. The three marked **changes gameplay** trade a little
exactness for speed, and are off unless you turn them on.

Choose them on the main menu: **Moono Tweaks**, under Mods, lists every tweak with an on/off
switch and a line on what it does (plus the Gassy Moo loading splash, on by default, and frame stats in the log).
Changes apply when the game restarts, which the screen does for you. Your choices are kept in
`oni-moono/settings.json` in the game's own data folder (usually
`~/.config/unity3d/Klei/Oxygen Not Included` on Linux,
`%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included` on Windows), which updates never touch.

Launch options still work and win over the screen, which then shows those rows as set by the
launch options: `--game-tweak exact` (every tweak but the three), `--game-tweak all`,
`--game-tweak none`, or tweaks by name (`--game-tweak exact --game-tweak slow-reachability`;
`--game-tweak -NAME` leaves one out); `--splash moo|none`; `--frame-stats`. Older setups put
`--game-tweak exact` in the launch options; remove it (the setup prints the new line) to choose in
the game instead.

- `fast-jobs`: the game's worker threads stop waking and waiting on each other for tiny jobs.
- `fast-anim-events`, `fast-anim-cleanup`, `fast-symbol-overrides`: cheaper animation
  bookkeeping every frame.
- `fast-offset-tables`: Duplicants' fetch updates skip items whose work spots haven't changed.
- `fast-sublimation`: skips sublimating items that are already blocked by full gas cells.
- `fast-particle-collision`: radbolts stop searching every stored item for other radbolts.
- `fast-partitioner-cleanup`: the once-a-second cleanup of the game's spatial index looks only
  at entries that were actually removed.
- `fast-overlays`: the overlays (temperature most of all) skip rereading what can't have
  changed since the last frame.
- `fast-conveyor-paths`: no hitch when conveyor rails change: their routes are found from an
  index instead of searching every route again.
- `fast-serialization`, `fast-save-type-checks`, `fast-save-compression`, `no-save-gc`,
  `background-save`: much shorter autosave freezes.
- `off-screen-conveyors` (**changes gameplay**): items travelling on conveyor rails off screen
  are only placed where they are once they stop or come into view. The rails carry exactly the
  same things, but the items' positions on screen and in the world lag behind meanwhile.
- `slow-reachability` (**changes gameplay**): checks whether jobs and items can be reached less
  often, so Duplicants may notice a newly blocked or opened path a little later.
  Temperature and building checks are never delayed.
- `off-world-fallers` (**changes gameplay**): items that fall out through the bottom of a world
  (a chute out of a rocket interior) hang just below it forever, still being stepped as falling.
  They are landed where they hang instead, so they stop costing time every tick.

## Save compatibility

Saves from the original game should load, and saves made here should still load in the
original game (including ones written with the `fast-save-compression` tweak).

## Mod compatibility

Most mods should work, since the game's code and its Harmony load as shipped. I've tested my own
set of 14 mods, and the 500 most popular Workshop mods have each been loaded and run in a late-game
colony; see [docs/MOD_COMPATIBILITY.md](docs/MOD_COMPATIBILITY.md) for what has been tested. Mods that call parts of
Unity the game itself never uses can still hit gaps; if one breaks, file an issue with your log.

## Could Klei do this?

Possibly. In the age of agents this took about 5 days, so it isn't the insurmountable task it
might once have looked like.

But it has plenty of bugs, known and unknown. Its only real mandates were to leave no safe
optimization behind, and not to crash *my* game. Much of the win comes from CoreCLR itself,
which Unity 7's CoreCLR move should bring to the original game too, eventually (it's been in
Soon™ territory for years).

This also rebuilds only what the game needs to run. Everything that Klei's development workflow
needs from Unity would be lost:
- the editor
- Unity's shader tooling
- the asset bundle build pipeline
- profiling and debugging tools
- no macOS support yet
- the general, accumulated knowledge of how Unity behaves

So I'd only seriously look at something like this if Unity 7's CoreCLR runtime can't get
close to the same numbers.

The optional tweaks are a different story. They are potential optimizations that probably also
apply to the original game. I haven't measured them there, though, and their gains could differ
on Mono. The engine-side wins are probably not possible to port to the original game, as it
would require building Unity from source.

## Contributing

Feel free to slop. See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Issues

File an issue, and please attach your log and the save you were playing.

The log is `Player.log` in:
- Windows: `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\`
- Linux: `~/.config/unity3d/Klei/Oxygen Not Included/`

Saves are `.sav` files in a folder named after the colony, under:
- Windows: `Documents\Klei\OxygenNotIncluded\cloud_save_files\<your Steam ID>\` (or
  `save_files\` for saves kept off Steam Cloud)
- Linux: `~/.config/unity3d/Klei/Oxygen Not Included/cloud_save_files/<your Steam ID>/` (or
  `save_files/`)

With Steam installed as a snap on Linux, both folders are under `~/snap/steam/common/` instead
of `~`. With Flatpak Steam, `~/.config/` becomes `~/.var/app/com.valvesoftware.Steam/config/`.

## License

All rights reserved.

Built on [BRUTAL](https://github.com/ahwootech/BRUTAL), Copyright (c) 2024 Ahwoo Limited.
Oxygen Not Included is © Klei Entertainment. Moono is unofficial, and not affiliated with or
endorsed by Klei Entertainment, Unity Technologies, or the Mono project.

## Unsigned releases

The releases are not signed, so to Windows every new one is an unknown program from the
internet. Extracting it or running setup may hold things up for a cloud antivirus scan, or show
"Windows protected your PC" (click More info → Run anyway). That's okay and expected for sussy
software like this. To skip most of it, right-click the downloaded zip → Properties → tick
Unblock before extracting.
