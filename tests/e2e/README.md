# End-to-end specs

A spec is one YAML file here, run by `scripts/e2e.py`. It starts from a fixture save, installs the
mods it names, drives the game the way a player does, and checks what came of it. The plan behind
it is [docs/research/mod-play-testing.md](../../docs/research/mod-play-testing.md).

```sh
scripts/e2e.py --check NAME                 # parse and validate only (and known-findings.yaml)
scripts/e2e.py NAME                         # ours, on the first free lane
scripts/e2e.py --player both --repeat 2 NAME
scripts/e2e.py --player original --bless NAME   # bless fixtures and references
scripts/e2e.py --lanes 4 A B C D            # four at once, from a queue
scripts/e2e.py --player ours --repeat 2 --record --note "..." NAME   # write tests/e2e/results/NAME.json
scripts/e2e.py --status                     # the machine's lanes, players and perf jobs
```

A batch of mods, one agent per worktree, follows [Batch workflow](#batch-workflow).

## How a run goes

- **Players.** `--player both` runs the original Unity player first. A spec that fails there is a
  broken spec, or a mod broken upstream, never a verdict on us. Our runs of that spec then skip.
  Otherwise our runs are compared with the original's evidence (see [Verdicts](#verdicts)).
  `--player ours` alone compares with the references `--bless` stored in `out/e2e-refs/SPEC/`.
- **The lock directory.** Runners in every worktree on the machine meet in one directory,
  `$E2E_LOCK_DIR` or by default `~/.cache/oni-brutal-e2e/`: lane locks, the players' budget, the
  perf lock and the reservations below live there, never in a worktree's `out/`. `--status`
  prints what it holds now: each held lane with its runner's pid and worktree, the players and
  their memory, perf jobs.
- **Lanes** are machine-wide. Lane L uses:
  - OniMcp on port 8800 + 10 L, written to the run's `OniMcpConfig.json`;
  - E2eDriver on 8801 + 10 L, passed as `E2E_DRIVER_PORT`;
  - display :(300 + L) (`E2E_DISPLAY_BASE`), an Xvfb the runner starts and stops. `--xvfb-run`
    uses `xvfb-run -a` instead; `--displays :88,:89` names the displays.

  `--lanes N` (at most 6) runs up to N spec runs at once, on lanes from `--first-lane` (default 1)
  to `--last-lane` (default 19): `--first-lane 7 --last-lane 8` keeps a runner on lanes 7 and 8.
  A lane is skipped while another runner holds it (`lane-L.lock`, whose content names the
  holder), while another program listens on its port or runs its display, and when a port or
  its display is reserved: lines `ports 8788 8820-8831` and `displays :80-:85 :91` in the lock
  directory's `reserved` file, or in `E2E_RESERVED` (`;` between lines). Reserve there what
  sessions or tools outside this lock directory use.
- **Memory.** Each player is counted in the machine's budget (`players.json`) for the memory its
  fixture needs: 6 GB on a small save, 11 on 2minnow, 15 on U57, or the spec's `memory_gb`. A
  player starts only while all of them stay within `E2E_MEMORY_GB` (default 40 of the machine's
  62) and their count within `E2E_MAX_PLAYERS` (default 6); one bigger than the whole budget
  starts alone. An entry whose process has gone is dropped.
- **Perf jobs.** A run holds `perf.lock` in the lock directory shared. A perf job that must not
  share the machine takes it exclusively: `flock -x ~/.cache/oni-brutal-e2e/perf.lock
  scripts/qa.sh`. Runs also wait while `ps` shows a perf job (`qa.sh`, the sweep and measurement
  scripts, `perf record`; see `PERF_JOBS`); `--no-perf-wait` skips the `ps` check.
- **Waits are visible.** A run waiting for a perf job, a lane or memory says what it waits for
  (the perf job's pid, how long it has run and its command; the lanes' or players' holders),
  again when that changes and every 10 minutes. `--max-wait MINUTES` (or `E2E_MAX_WAIT`) fails
  the run after that long, with what it was waiting for as its reason.
- **Paused time.** The fixture loads paused. Time passes only in `run` steps. Take all UI evidence
  before the first `run`, or right after an expectation that pins the state.
- **Output.** Each invocation has a run id (its start time and a random part, or `--run-id`), so
  invocations never overwrite each other. Each run writes to `out/e2e/SPEC/RUN_ID/PLAYER-N/`
  (`out/e2e/SPEC/latest` points at the newest run id):
  - `player.log` or `stdout.txt`;
  - `results.jsonl`, one line per step;
  - `result.json`, the run's record;
  - `lane.json`, the lane's ports and display;
  - the evidence: `NAME.ui.txt`, `NAME.png` with `NAME.shot.json`, and `NAME.diff.png`.

  `out/e2e/runs/RUN_ID.json` (or `--results PATH`) holds one record per spec run of the
  invocation; `out/e2e/runs/latest` points at the newest. References stay one folder per spec,
  `out/e2e-refs/SPEC/`, replaced whole by each `--bless`; its `source.json` names the run that
  blessed it. `E2E_OUT` moves `out/e2e` elsewhere, and the references beside it
  (`$E2E_OUT-refs`).

## Top-level keys

```yaml
# The mod card goes here as a comment: what the mod does, how a player reaches each feature,
# what changes when it is used.
from: early-base                 # fixture: tests/e2e/fixtures/early-base.sav
mods: [1863428350]               # Workshop IDs, installed as Local mods (out/ws, ws-mods.py)
options:                         # PLib options, merged into the mod's config.json before launch
  SweepByType: {DisableIcons: true}       # by static ID (or Workshop ID)
  "2890083659": {$file: MiniBox.json, Wires: 3.0}   # a [ConfigFile] name other than config.json
require_log: ['\[SweepByType\] options saved']   # regexes: in the original's log -> must be in ours
mask_text: ['\d+:\d+', 'Cycle \d+']      # masked in every ui_dump's text before the diff
ignore_fields: [depth]           # NAME=VALUE members left out of every ui_dump's diff
tolerance: 2                     # ui_dump rect tolerance in pixels (default 2)
thresholds: {shot: 0.02, world_shot: 0.05}   # pixel fractions (the defaults)
memory_gb: 11                    # the players' budget counts each player for this (default: by fixture)
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
- click: {ui: "**/WorldSelector/**/Row", double: true}         # a double click
- toggle: {ui: "**/TypeSelectScreen/**/Sandstone", to: true}   # clicks only if needed, then checks isOn
- dropdown: {ui: "**/MaterialDropdown", option: "Sandstone"}   # opens it, clicks the option by text
- type: {ui: "**/FileNameDialog/**/Input", text: "e2e", enter: true, clear: true}
- key: {code: "Shift+B"}                                       # KKeyCode names; Shift, Ctrl, Alt
- key: {code: Escape}
- key: {code: O, at: [110, 209]}                               # the pointer over a cell first
- key: {code: Q, down: true, at: [110, 209]}                   # held across the next steps (a run too),
- key: {code: Q, up: true}                                     # until released (or the run ends)
- drag: {from: [108, 212], to: [136, 205]}                     # world cells; `mods`, `button`, `steps`,
                                                               # `hold` (frames at `to` before release)
- drag: {from: [109, 207], via: [[109, 209], [110, 209]], to: [110, 207]}   # through cells in order
- click_world: {cell: [150, 212]}
- click_world: {cell: [106, 206], select: PiapiPlantHelper}  # clicks again until that is selected
- click_world: {pos: [109.5, 206.2], select: Granite}          # a world position: debris lies low in its cell
- scroll: {at: center, notches: -40}                           # or at: [x, y] (a cell), or a UI path
- tool: {toolbar: Sweep}                  # a ToolMenu collection by its text or icon name
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
    "file:mods/config/Mod/presets": ">= 1"           # a folder reads as its count of entries
    "mass:debris": {save_as: before}                # remember a value...
- expect_state: {"mass:debris": ">= 0.95 * before"} # ...and compare with it later
- expect_state: {"Research.Instance.queuedTech.Count": "== original"}   # the original's value here
- expect_hit: {at: center, not: "~TMP SubMeshUI"}  # the top raycast hit there (ui.at); or top: PATTERN
```

`expect_hit` takes `at` as `scroll` does (`center`, a cell or a UI path) and checks the path of
the topmost raycast hit there, the empty path when nothing is hit: `top` must match it, `not`
must not (exact, `~sub` or `/regex/`). It checks every 3 frames until that holds or `frames`
(default 30) have passed. It does not move the pointer.

`expect_ui` checks the first active match; `count` checks how many match (a value, `"OP EXPR"` or
`{save_as: NAME}`) and `any_text` passes when any match has that text. It retries for a few
frames before it fails. An `expect_state` value is one of:
- a value;
- `"OP EXPR"`, where OP is `>=`, `<=`, `>`, `<`, `==` or `!=`, and EXPR is made of numbers,
  names, `+ - * /` and parentheses: a name is a value saved with `save_as`, or `original`
  (`">= before + 0.95 * dug"`, `">= 0.9 * original"`);
- `"== 'TEXT'"` or `"!= 'TEXT'"`: text in quotes, for names such as an enum's;
- `exists` or `missing`;
- `{contains: X}`, `{equals: X}` or `{save_as: NAME}`.

Besides `file:PATH`, the runner reads these, each item through its own `state.get`:
- `cells:X1,Y1..X2,Y2::EXPR`: the sum of EXPR over a rectangle of cells, `{cell}` standing for
  each cell's index: `cells:119,209..120,210::Grid.Mass.get_Item({cell})`;
- `count:HEAD[*] if .MEMBER == VALUE and ...`: how many items of an array or list match, as in
  `count:all:Clearable::[*] if .isMarkedForClear == true`;
- `sum:HEAD[*].MEMBER if ...`: the sum of a member over the items that match, as in
  `sum:all:Clearable::[*].GetComponent<PrimaryElement>.Mass if .GetComponent<PrimaryElement>.ElementID == SandStone`.

A condition compares the member's value as text (`true`/`false` for booleans; `'TEXT'` in quotes
may hold spaces). These read one
item a frame: a few hundred items take some seconds.

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
thing under test: issue #6 was "Played" through reflection while its screens were broken. An
argument JSON cannot spell is given as a state expression, `{"$": "Db.Get().Techs.Get(\"HVAC\")"}`
(E2eDriver's README, setup.call).

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

A Graphic's `depth` is left out of every `ui_dump` diff (`IGNORE_FIELDS`): our player numbers the
graphics of a canvas differently, whatever is drawn, so it would fail every screen. A spec or a
`ui_dump` step adds members with `ignore_fields`, for values that follow random content.

Each check's numbers are in `result.json` under `checks`. A shot's diff mask is `NAME.diff.png`
in our run's folder: white pixels differ, grey ones are masked.

### Known findings

[`known-findings.yaml`](known-findings.yaml) lists engine differences a spec's `ui_dump` found,
each reviewed, with an id, a description, the issue or doc where it is described, and what it
matches: the ui-diff component (`Canvas`, `ScrollRect`, `rect`, …), the members that may differ,
and optionally element path globs, regexes on either player's value, and the specs it is limited
to. A difference one of them explains is no failure: the run of ours is `warn`, its `checks`
count the difference under the finding's id (`checks.ui_dump.NAME.known`), and the id goes into
the results record, so the mod reads ⚠️ with the id cited. Every other difference in the same
dump still fails the run.

A finding is added only for a difference understood to be the engine's (not the mod's, not a
spec's randomness), and only while it is being fixed: a finding hides that difference in every
spec it matches, so match narrowly (fields, path) and remove the finding with the fix.
`--check` validates the file.

### The results record

`--record` writes `results/SPEC.json`, the committed record of a spec (§7 of the plan):

```json
{"spec": "mods/2527442674-full-miner-yield.yaml", "mods": ["2527442674"],
 "verdict": "played", "why": "passed once in the original and twice in ours",
 "findings": [], "note": "a dig leaves debris weighing as much as the dug cells",
 "updated": "2026-10-22", "commit": "14e677a", "mod_dlls": {"2527442674": {"FullMinerYield.dll": "SHA-256"}},
 "runs": {"original": [{"run": "RUN_ID", "index": 1, "verdict": "pass", "signatures": 4, ...}],
          "ours": [{"run": "RUN_ID", "index": 1, "verdict": "pass", "reference_run": "RUN_ID",
                    "new_signatures": 0, "findings": [], "ui_dump": {...}, "shots": {...}}, ...]}}
```

An invocation's runs replace the record's runs of the same player; the others stay, so the
original's run (`--player original --bless --record`) and ours (`--player ours --repeat 2
--record`) come from separate invocations. `--note` sets the note, which is kept otherwise. The
verdict follows from the runs:

| Verdict | When | Table |
|---|---|---|
| `played` | an original run passed, and two runs of ours compared with it passed | ✅ |
| `warning` | the same, but a run of ours had differences known findings explain | ⚠️, the findings cited |
| `failed` | a run of ours compared with the passing original run failed | ⬜, "spec fails in ours" |
| `pending` | no passing original run, or fewer than two runs of ours against the newest one | not shown |

A run of ours counts only against the original run it was compared with (`reference_run`): a
new `--bless` makes earlier runs of ours stale until ours runs again. `scripts/modtable.py` reads
every record for the Played column and the note, ahead of its hand-written sets; those stay for
mods no spec has recorded.

## Mod specs and fixtures

A mod's spec is `mods/ID-slug.yaml`, its header the mod card: what the mod does, how a player
reaches each feature, what changes when it is used. Run it with its path under `tests/e2e`
(`scripts/e2e.py --player both --bless mods/ID-slug`): the original first, which blesses the
references, then ours, compared with them.

Fixtures are blessed saves (`--bless` copies a spec's `save:` into `fixtures/`, not kept in git):

- `landing`: a new Spaced Out colony at its printing pod;
- `early-base` (`early-base.yaml`): dug out on both sides of the pod, with an outhouse, a manual
  generator, an unpowered research station and a cot. Debris lies on the floors.
- `prehistoric1`: a Prehistoric colony at cycle 1 (copied from a played save, not blessed).

What a spec needs beyond them (a researched tech, materials, Duplicants on offer) it sets up in
`setup:` through E2eDriver, as the pilot specs do, rather than from a later fixture.

Writing a spec, `--hold` keeps the player running after the steps for a look through OniMcp
(its port) and E2eDriver (`ui.find`, `ui.dump`, `state.get`); `--hold-on-fail` does so after a
failing step. Paths come from what `ui.find` returns, never from guesses. Things that went wrong
in the pilot:

- Wait for a screen's moving part to settle (`ui_wait ... cond: stable` on the part that moves,
  not the screen's root), or a click lands on the wrong thing in one player only.
- Random content (immigrant cards, names) differs between the players even with `seed`: assert
  relations, and compare by structure only what does not follow it.
- Some game actions need the game to have run since the load (Blueprints Expanded placed nothing
  before): a short `run` before the UI steps is allowed.

## Batch workflow

Agents play batches of about 25 mods, each in its own worktree. A worktree needs:

```sh
ln -s ~/projects/oni-brutal/out/local-tools out/local-tools    # qa.env, OniMcp, the original
ln -s ~/projects/oni-brutal/out/ws out/ws                      # the Workshop builds
cp ~/projects/oni-brutal/tests/e2e/fixtures/*.sav tests/e2e/fixtures/   # or bless early-base
export PATH=$HOME/.dotnet:$HOME/.dotnet/tools:$PATH DOTNET_ROOT=$HOME/.dotnet
dotnet build src/OniBrutal.Host -c Release
dotnet build tools/E2eDriver -c Release
```

Locks, lanes and the players' budget are machine-wide (see [How a run goes](#how-a-run-goes)),
so agents can run beside each other. Each agent keeps to the lanes it is given
(`--first-lane A --last-lane B`), and `scripts/e2e.py --status` shows who holds what. Results are
one file per spec, so batches do not conflict; `docs/MOD_COMPATIBILITY.md` is regenerated, not
merged.

For each mod of the batch:

1. **Triage.** Read its row in [docs/research/mod-triage.md](../../docs/research/mod-triage.md):
   its classes (P, B, T, S, K, O, V, M, X) and flags (`touches_ui`, `asset_bundle_ui`, ...). An
   X mod (a translation, multiplayer, worldgen) gets no spec.
2. **Decompile** it: `ilspycmd -p -o out/local-tools/moddecomp/ID out/ws/ID/MOD.dll` (never
   committed), and confirm the classes from the code.
3. **The mod card.** Write it as the header of `mods/ID-slug.yaml`: what the mod does, how a
   player reaches each feature (button, hotkey, tool, side screen), what changes when it is used.
4. **Start from `early-base`.** Set up what the spec needs beyond it in `setup:` (reflection
   through `driver: {setup.call: ...}` is allowed there only).
5. **Explore in the original**, on your lanes: `scripts/e2e.py --player original --hold
   --first-lane A --last-lane B mods/ID-slug`. The log names OniMcp's and E2eDriver's ports and
   the display; take paths from E2eDriver's `ui.find` and `ui.dump`, never from guesses. Remove
   the `hold` file the log names to end it.
6. **Drive the UI by clicks**: `click`, `toggle`, `dropdown`, `type`, `key`, `drag`, `tool`, as a
   player would, then `expect_ui` and `expect_state` on the UI's response and on game state.
7. **Evidence only for what is not random**: `ui_dump` and `shot` of screens whose content does
   not follow random numbers; mask (`mask`, `mask_text`, `ignore_fields`) or leave out the rest,
   and assert relations instead.
8. **Pass in the original and bless**: `scripts/e2e.py --player original --bless --record
   --first-lane A --last-lane B mods/ID-slug`, until it passes. It fails there? Fix the spec,
   or, when the mod is broken upstream, say so in `broken` in `scripts/modtable.py`.
9. **Twice in ours**: `scripts/e2e.py --player ours --repeat 2 --record --note "WHAT WAS
   EXERCISED" --first-lane A --last-lane B mods/ID-slug`. The note says what the spec drove and
   how close the screens came, as the table's other notes do.
10. **The verdict** is the record's (`results/ID-slug.json`): `played`, `warning`, `failed` or
    `pending`. A `ui_dump` difference that a known finding explains makes it `warning` by itself.
    A new difference is the engine's to fix: file or link an issue, and only then, reviewed, add
    a finding to `known-findings.yaml` and run ours again. A failure in ours stays `failed`, its
    note saying what fails.
11. **Commit** the spec and its results file (and any new finding).
12. **Regenerate the table** (`scripts/modtable.py > docs/MOD_COMPATIBILITY.md`; it reads
    `scripts/mod-catalog.tsv`, and the sweeps from the main checkout's `out/`) and check with
    `git diff docs/MOD_COMPATIBILITY.md` that only your mods' rows changed. When a mod had a
    hand-written entry in `modtable.py` (`played`, `differences`, `unplayed`), remove it in the
    same commit. After a merge, regenerate rather than resolve the table by hand.
13. **Leave no processes behind**: `scripts/e2e.py --status` lists no lane or player of yours,
    and `ps -eo pid,args | grep "[X]vfb :3"` and `ps -eo pid,args | grep "[O]niBrutal"` show none
    from your worktree. Kill a leftover by its pid in a separate command, never with `pkill -f`.
