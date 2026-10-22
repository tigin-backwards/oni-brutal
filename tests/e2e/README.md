# End-to-end specs

A spec is one YAML file here, run by `scripts/e2e.py`. It starts from a fixture save, installs the
mods it names, drives the game the way a player does, and checks what came of it. The plan behind
it is [docs/research/mod-play-testing.md](../../docs/research/mod-play-testing.md).

```sh
scripts/e2e.py --check NAME                 # parse and validate only
scripts/e2e.py NAME                         # ours, lane 1
scripts/e2e.py --player both --repeat 2 NAME
scripts/e2e.py --player original --bless NAME   # bless fixtures and references
scripts/e2e.py --lanes 4 A B C D            # four at once, from a queue
```

## How a run goes

- **Players.** `--player both` runs the original Unity player first. A spec that fails there is a
  broken spec, or a mod broken upstream, never a verdict on us. Our runs of that spec then skip.
  Otherwise our runs are compared with the original's evidence (see [Verdicts](#verdicts)).
  `--player ours` alone compares with the references `--bless` stored in `out/e2e-refs/SPEC/`.
- **Lanes.** `--lanes N` runs up to N spec runs at once. Lane L uses:
  - OniMcp on port 8800 + 10 L, written to the run's `OniMcpConfig.json`;
  - E2eDriver on 8801 + 10 L, passed as `E2E_DRIVER_PORT`;
  - its own display, from `xvfb-run -a` by default, or from `--displays :88,:89`.

  Lanes are taken from `--first-lane` (default 1) to `--last-lane` (default 6 lanes on), so
  `--first-lane 2 --last-lane 3` keeps a runner on lanes 2 and 3. A busy lane is skipped: one
  another runner holds (`out/e2e/.lane-L.lock`), or one whose port another program listens on.
  At most 6 lanes.
- **Machine lock.** A run holds `out/e2e/.lock` shared. A perf job that must not share the machine
  takes it exclusively: `flock out/e2e/.lock scripts/qa.sh`. Runs also wait while `ps` shows a perf
  job (`qa.sh`, the sweep and measurement scripts, `perf record`; see `PERF_JOBS`).
  `--no-perf-wait` skips the `ps` check.
- **Paused time.** The fixture loads paused. Time passes only in `run` steps. Take all UI evidence
  before the first `run`, or right after an expectation that pins the state.
- **Output.** Each run writes to `out/e2e/SPEC/PLAYER-N/`:
  - `player.log` or `stdout.txt`;
  - `results.jsonl`, one line per step;
  - `result.json`, the run's record;
  - `lane.json`, the lane's ports and display;
  - the evidence: `NAME.ui.txt`, `NAME.png` with `NAME.shot.json`, and `NAME.diff.png`.

  `out/e2e/results.json` (or `--results PATH`) holds one record per spec run.

## Top-level keys

```yaml
# The mod card goes here as a comment: what the mod does, how a player reaches each feature,
# what changes when it is used.
from: early-base                 # fixture: tests/e2e/fixtures/early-base.sav
mods: [1863428350]               # Workshop IDs, installed as Local mods (out/ws, ws-mods.py)
options:                         # PLib options, merged into the mod's config.json before launch
  SweepByType: {DisableIcons: true}       # by static ID (or Workshop ID)
require_log: ['\[SweepByType\] options saved']   # regexes: in the original's log -> must be in ours
mask_text: ['\d+:\d+', 'Cycle \d+']      # masked in every ui_dump's text before the diff
tolerance: 2                     # ui_dump rect tolerance in pixels (default 2)
thresholds: {shot: 0.02, world_shot: 0.05}   # pixel fractions (the defaults)
setup: [...]                     # steps run first; the only place for driver setup.call
steps: [...]
```

Every spec gets OniMcp and, in both players, E2eDriver: `E2eDriver.dll` from its build
(`E2E_DRIVER`, default `out/bin/E2eDriver/release`) with `tools/E2eDriver/mod.yaml` and
`mod_info.yaml`, stamped 1399985550 in `mods.json`. A spec with driver steps fails without a
build of it. Each Local mod's stamp is its folder name's Mono string hash
(`ws-mods.py mono_hash`), the one KMod computes in both players. PLib reads options from
`config.json` beside the mod's assembly, or from `mods/config/ASSEMBLY/` when the mod uses the
shared location. The runner writes both, in every folder of the mod that holds a DLL, because KMod
may load an archived version.

## Steps

Each step is a one-key mapping. Paths in `ui`, `under`, `root`, `crop` and `mask` are E2eDriver
hierarchy paths, where `*` matches within a name and `**` matches any number of levels. Take real
paths from a `ui.find` or `ui_dump` of a run, not from guesses.

### Orders and the game (OniMcp)

```yaml
- dig: [110, 207, 118, 209]                      # dig orders on a rectangle (priority 5)
- build: {prefab: Outhouse, at: [119, 207]}      # a blueprint, its lower-left cell at x, y
- build: {prefab: Tile, cells: [[1, 2], [2, 2]], priority: 7, material: Granite}
- run: {seconds: 600, until: {pending_digs: 0}}  # game time at speed 3, until `until` holds
- expect: {duplicants: 3, buildings: {Bed: 1}, pending_construction: 0, rooms: {Latrine: 1}}
- call: {tool: navigation_control, args: {action: focus_cell, x: 140, y: 207}}   # any OniMcp tool
- save: early-base                               # saved; --bless copies it into fixtures/
```

`expect` and `until` take `buildings: {Prefab: N}`, `blueprints: {Prefab: N}` (unfinished
ones), `duplicants`, `pending_construction`, `pending_digs`, `rooms: {Type: N}` and `cycle`. N is a
number, or `">=N"`, `"<=N"`, `">N"`, `"<N"`.

`build` places a blueprint through OniMcp's viewport map. OniMcp would also connect a powered
building to a power source with wire blueprints and dig orders of its own choosing; the runner
turns that off (`autoConnectPower: false`) unless the step says `wire: true`. A blueprint whose
cells are not dug yet is deferred by OniMcp until they are: the step fails then, so dig those
cells in an earlier step.

### Opening screens

```yaml
- open: {management: research}        # OniMcp's whitelist: vitals, consumables, priorities,
                                      # schedule, skills, research, starmap, report, database
- open: {key: R}                      # the screen's hotkey, through XTEST
- open: {user_menu: toggle_compost, at: [130, 207]}   # a user-menu button by OniMcp action key
                                      # (on the object at a cell, or `id: ID`)
```

### Real input (XTEST, located through E2eDriver)

Before each click, the runner finds the element with `ui.find`. The top raycast hit at its centre
must be the element or one of its children. If it is not, the step fails with "occluded by PATH",
and nothing is clicked. A click is: move, 2 frames of hover, press, 2 frames, release.

```yaml
- click: {ui: "**/ResearchScreen/**/Tech_FarmingTech/Button"}
- click: {ui: "**/Tech_FineDining/Button", mods: [Shift]}      # modifier-click
- click: {text: "Quick Learner", under: "**/UnityDuplicitySelectionScreen"}
- click: {ui: "**/List/Item", index: 2, button: right}         # the third match; right button
- toggle: {ui: "**/TypeSelectScreen/**/Sandstone", to: true}   # clicks only if needed, then checks isOn
- dropdown: {ui: "**/MaterialDropdown", option: "Sandstone"}   # opens it, clicks the option by text
- type: {ui: "**/FileNameDialog/**/Input", text: "e2e", enter: true, clear: true}
- key: {code: "Shift+B"}                                       # KKeyCode names; Shift, Ctrl, Alt
- key: {code: Escape}
- drag: {from: [108, 212], to: [136, 205]}                     # world cells; `mods`, `button`, `steps`
- click_world: {cell: [150, 212]}
- scroll: {at: center, notches: -40}                           # or at: [x, y] (a cell), or a UI path
- tool: {toolbar: SweepByType}            # the ToolMenu entry whose path ends in the name, else its text
- tool: {ui: "**/ToolMenu/**/SomeButton"}
- tool: {key: "Alt+S"}
```

Key names follow the game's `KKeyCode`: `A`–`Z`, `Alpha0`–`Alpha9` (or `0`–`9`), `F1`–`F15`,
`Escape`, `Return`, `Space`, `Tab`, `Backspace`, `Delete`, `UpArrow`/`DownArrow`/`LeftArrow`/
`RightArrow`, `LeftShift`, `LeftControl`, `LeftAlt`, `Keypad0`–`Keypad9`, `KeypadPlus`,
`Comma`, `Period`, `Slash`, `Minus`, `Equals`, `LeftBracket` and others (`KKEYCODES` in
e2e.py). `type` types any printable ASCII, on a US keymap.

### Expectations on the UI and on state (E2eDriver)

```yaml
- expect_ui:
    "**/CharacterContainer/**/Traits": {text: "~Quick Learner", active: true}   # ~ contains
    "**/Clock": {text: "/^\\d+:\\d\\d$/"}                 # /regex/
    "**/OkButton": {interactable: true}
    "**/Toggle": {isOn: false}
    "**/ErrorPopup": {exists: false}
- ui_wait: {path: "**/BlueprintSelector", cond: stable, frames: 300}   # E2eDriver's ui.wait
- expect_state:
    "Research.Instance.queuedTech.Count": ">=3"
    "type:BlueprintsV2.BlueprintState.rotation": 1
    "selected:MinionIdentity.GetComponent<Traits>": {contains: QuickLearner}
    "file:blueprints/e2e.blueprint": exists         # file:PATH, in the run's profile (no driver)
    "mass:debris": {save_as: before}                # remember a value...
- expect_state: {"mass:debris": ">= 0.95 * before"} # ...and compare with it later
- expect_state: {"Research.Instance.queuedTech.Count": "== original"}   # the original's value here
```

`expect_ui` retries for a few frames before it fails. An `expect_state` value is one of:
- a value;
- `"OP EXPR"`, where OP is `>=`, `<=`, `>`, `<`, `==` or `!=`, and EXPR is made of numbers,
  names, `+ - * /` and parentheses: a name is a value saved with `save_as`, or `original`
  (`">= before + 0.95 * dug"`, `">= 0.9 * original"`);
- `exists` or `missing`;
- `{contains: X}`, `{equals: X}` or `{save_as: NAME}`.

Besides `file:PATH`, the runner reads `cells:X1,Y1..X2,Y2::EXPR`: the sum of a `state.get`
expression over a rectangle of cells, `{cell}` standing for each cell's index, as in
`cells:110,207..118,209::Grid.Mass.get_Item({cell})`.

`== original` is checked after our run, against the value the original player recorded at the same
step. The original run itself records it and passes.

### Evidence (compared with the original afterwards)

```yaml
- ui_dump: {name: selector, root: "**/BlueprintSelector", mask_text: ['\d{4}-\d\d-\d\d']}
- shot: {name: selector, crop: "**/BlueprintSelector", mask: ["**/Portrait", "**/DateLabel", [0, 0, 64, 64]]}
- shot: {name: dialog, crop: "**/Dialog", threshold: 0.04}     # a text-dense screen
- shot: overview                                               # the whole frame
- world_shot: {name: zoomed, ortho: 45, at: [128, 210], hide_ui: true, mask: [[600, 300, 80, 80]]}
```

- `shot` moves the pointer to the bottom-right corner first, then takes the whole frame through
  OniMcp. It records the crop rect (the element's `ui.find` rect) and mask rects (UI paths, or
  screen rects `[x, y, w, h]`) beside the shot.
- `world_shot` sets the camera through E2eDriver (`at` is a cell, `ortho` the orthographic size),
  hides the UI canvases (`hide_ui`, default true), shoots, and shows them again. Its masks are
  screen rects or UI paths. Symbolic masks such as Duplicants are not supported yet.

### E2eDriver commands

```yaml
setup:
  - driver: {seed: 12345}                            # before a random screen opens
  - driver: {setup.call: {type: "BlueprintsV2.Store", method: "Seed", args: ["e2e"]}}   # setup only
  - driver: {camera.set: {pos: [128.5, 210.5], ortho: 20}}
```

`setup.call` (reflection) is allowed only in `setup:`. It may prepare a test, never perform the
thing under test: issue #6 was "Played" through reflection while its screens were broken.

## Verdicts

A run of either player fails if a step fails, a click lands on an occluded element, an
expectation does not hold, or the player is not alive at the end.

A run of ours also fails on these differences from the original (§5.3 of the plan):

| Evidence | Fails when |
|---|---|
| log signatures (`qa-tools.py signatures`) | ours has one that none of the original's runs has |
| `require_log` | a line is in the original's log, not in ours |
| `ui_dump` (`ui-diff.py --tolerance 2 --mask-text ...`) | an element is only in one dump; a state or text differs after masking; a rect is off by more than 2 px, a size by more than 2 px and 2 %; an element is 0-sized in one only |
| `shot` (`qa-tools.py image --crop --mask`) | more than `threshold` of the unmasked pixels differ by more than 40 in a channel (default 2 %); each player is cropped to its own rect |
| `world_shot` | the same, with a default threshold of 5 % |
| `== original` | ours does not meet the relation to the original's value |
| an `expect_ui`/`expect_state` failure | the result notes that it held in the original |

Each check's numbers are in `result.json` under `checks`. A shot's diff mask is `NAME.diff.png`
in our run's folder: white pixels differ, grey ones are masked.
