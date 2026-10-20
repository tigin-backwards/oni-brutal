# oni-brutal

A super fast, super sussy replacement for the Unity layer of Oxygen Not Included, artistically
crafted by Claude.

I did this mostly for fun, to see how far Oxygen Not Included's performance can go on a modern
.NET runtime (CoreCLR). I'd had the idea for a while, but none of the existing C# renderers
sparked joy. When [BRUTAL](https://github.com/ahwootech/BRUTAL) went open source, it seemed like
a good excuse to try a sexy new toy.

Klei's game DLLs load as they are, apart from two small compatibility fixes. Whatever they call
into Unity for is reimplemented here.

## Setup

You need your own copy of the game on Steam, on Linux or Windows (no macOS). Because this replaces
the engine rather than patching the game, it can't ship as an ordinary Harmony mod. Instead, a setup script hooks it into the game's
Steam launch options, so you still start the game from Steam as usual.

<!-- TODO: release download link, and the exact script names once they exist -->

1. Download the latest release, or clone this repository.
2. Run the setup script:
   - Linux: `./setup.sh`
   - Windows: `setup.ps1` (right-click → Run with PowerShell)

   It finds your Oxygen Not Included install through Steam and prints one line for the game's
   launch options.
3. In Steam, right-click Oxygen Not Included → Properties → Launch Options, and paste that line.

Then press Play in Steam as usual. To go back to the original game, clear the launch options.

You'll need an OpenGL 4.5 graphics driver. The game uses your normal Steam account, DLCs, saves,
settings and mods.

## Performance

The biggest change is swapping the very garbage Mono runtime that Unity ships for CoreCLR. On
top of that there are optimizations tuned to the way Oxygen Not Included uses the engine.

On a late-game save (U57: Spaced Out with every DLC, cycle 17,000+), on the same machine, back to back:

| | Original | oni-brutal |
|---|---|---|
| Frame rate at speed 3 | 6.5–6.8 fps | about 26 fps |
| Loading the save | 3 min 50 s | 26–28 s |
| Autosave freeze | <!-- TODO: measure the original --> | 4.8 s with the save tweaks (11.7 s without), compressing and writing moved off the main thread |

Garbage-collection hitches are mostly gone: collections now run in the background, spread
across every core, and their pauses are about a quarter as long.

<!-- TODO: machine specs. The frame rates are Linux, Xvfb at 1024×768. -->
<!-- TODO: frame-rate numbers depend on the U57 tweak set; re-check after the setup's defaults are chosen -->
<!-- TODO: the 26 fps figure is with every tweak, the "changes gameplay" ones included; without any tweak it's 17–18.5 fps -->


<!-- TODO: before/after gameplay video showing the frame rate -->

### Optional tweaks

These change how some of the game's own code does its work, and are off unless you turn them on
(`--game-tweak NAME`, or `--game-tweak all`). Most give exactly the same results faster. The
ones marked **changes gameplay** trade a little exactness for speed.

<!-- TODO: say which ones the setup script turns on by default -->

- `fast-jobs`: the game's worker threads stop waking and waiting on each other for tiny jobs.
- `fast-anim-events`, `fast-anim-cleanup`, `fast-symbol-overrides`: cheaper animation
  bookkeeping every frame.
- `fast-offset-tables`: Duplicants' fetch updates skip items whose work spots haven't changed.
- `fast-sublimation`: skips sublimating items that are already blocked by full gas cells.
- `fast-particle-collision`: radbolts stop searching every stored item for other radbolts.
- `fast-serialization`, `fast-save-type-checks`, `fast-save-compression`, `no-save-gc`,
  `background-save`: much shorter autosave freezes.
- `off-screen-conveyors` (**changes gameplay**): items travelling on conveyor rails off screen
  are only placed where they are once they stop or come into view. The rails carry exactly the
  same things, but the items' positions on screen and in the world lag behind meanwhile.
- `slow-reachability` (**changes gameplay**): checks whether jobs and items can be reached less
  often, so Duplicants may notice a newly blocked or opened path a little later.
  Temperature and building checks are never delayed.

## Save compatibility

Saves from the original game should load, and saves made here should still load in the
original game (including ones written with the `fast-save-compression` tweak).

## Mod compatibility

Most mods should theoretically work, since the game's code and its Harmony load as shipped. Mods
that call Unity's API directly are a coin flip. I haven't tested any of them.

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

Unlike RimWorld, which uses very little of Unity, Oxygen Not Included leans on much more of it.

So I'd only seriously look at something like this if Unity 7's CoreCLR runtime can't get
close to the same numbers.

## Contributing

Feel free to slop. [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) has the build, the test harness
and the rest of the documentation.

## Issues

File an issue, and please attach your log. It's `Player.log` in:
- Linux: `~/.config/unity3d/Klei/Oxygen Not Included/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\`

<!-- TODO: before release: license; BRUTAL credit, logo and NOTICE.txt (its license requires them) -->

## License

<!-- TODO -->

Built on [BRUTAL](https://github.com/ahwootech/BRUTAL), Copyright (c) 2024 Ahwoo Limited.
Oxygen Not Included is © Klei Entertainment; this project isn't affiliated with Klei or Unity.
