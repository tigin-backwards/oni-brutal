#!/usr/bin/env python3
"""scripts/modtable.py [CATALOG.tsv] > docs/MOD_COMPATIBILITY.md
The compatibility table from onimaxxing's catalog ranks (a TSV of rank, id, subscriptions, flags,
title; default scripts/mod-catalog.tsv) and what our runs recorded:
- the sweeps' sweep-*.txt ID lists and modtable-*.tsv results in out/ (or ONI_SWEEP_DIR; in a
  worktree whose out/ has none, the main checkout's out/);
- the spec results in tests/e2e/results/*.json (or ONI_RESULTS_DIR), written by
  `scripts/e2e.py --record`: a record's verdict and note decide the Played column, ahead of the
  hand-written status sets below, which stay for mods no spec has played yet."""
import glob, json, os, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def main_checkout_out():
    """The main checkout's out/ when this is a worktree (git's common dir is the main .git)."""
    common = subprocess.run(['git', '-C', ROOT, 'rev-parse', '--path-format=absolute', '--git-common-dir'],
                            capture_output=True, text=True).stdout.strip()
    return os.path.join(os.path.dirname(common), 'out') if common else None


def sweep_dir():
    if os.environ.get('ONI_SWEEP_DIR'):
        return os.environ['ONI_SWEEP_DIR']
    own = os.path.join(ROOT, 'out')
    if glob.glob(os.path.join(own, 'sweep-*.txt')):
        return own
    main = main_checkout_out()
    return main if main and glob.glob(os.path.join(main, 'sweep-*.txt')) else own


def read_results(folder):
    """The spec records (tests/e2e/results/SPEC.json) as {mod id: record}; pending ones (not yet
    played to a verdict) are left out."""
    by_mod = {}
    for path in sorted(glob.glob(os.path.join(folder, '*.json'))):
        with open(path, encoding='utf-8') as f:
            record = json.load(f)
        if record.get('verdict') not in ('played', 'warning', 'failed'):
            continue
        # A spec is the record of the mod it is named for (mods/ID-slug.yaml); the other mods it
        # installs (a base mod an addon needs) are not played by it.
        named = os.path.basename(record.get('spec', '')).split('-')[0]
        mods = [str(m) for m in record.get('mods', [])]
        for mid in ([named] if named in mods else mods):
            by_mod[mid] = record
    return by_mod


def read_findings(path):
    if not os.path.exists(path):
        return {}
    import yaml
    with open(path, encoding='utf-8') as f:
        return {x['id']: x for x in yaml.safe_load(f) or []}


def finding_text(ident, findings):
    link = findings.get(ident, {}).get('link', '')
    return f'[{ident}]({link})' if link.startswith('http') else f'`{ident}`'


def record_status(record, findings):
    """(Played mark, note) of a spec record."""
    note = f'spec tests/e2e/{record["spec"]}' + (f': {record["note"]}' if record.get('note') else '')
    if record['verdict'] == 'played':
        return '✅', note
    if record['verdict'] == 'warning':
        cited = ', '.join(finding_text(i, findings) for i in record.get('findings', []))
        return '⚠️', note + (f'; known findings: {cited}' if cited else '')
    return '⬜', f'spec fails in ours: {note[5:]}' + (f' ({record["why"]})' if record.get('why') else '')


SWEEPS = sweep_dir()
RESULTS = os.environ.get('ONI_RESULTS_DIR') or os.path.join(ROOT, 'tests', 'e2e', 'results')
FINDINGS = read_findings(os.path.join(ROOT, 'tests', 'e2e', 'known-findings.yaml'))
if len(sys.argv) > 2: sys.exit(__doc__)
catalog = sys.argv[1] if len(sys.argv) == 2 else os.path.join(ROOT, 'scripts', 'mod-catalog.tsv')
cat = [l.rstrip('\n').split('\t') for l in open(catalog, encoding='utf-8')]
by_id = {r[1]: r for r in cat}
LAST_RANK = 750
results = read_results(RESULTS)

def ids(pattern):
    s = set()
    for f in glob.glob(os.path.join(SWEEPS, pattern)):
        s.update(open(f).read().split())
    return s

swept = ids('sweep-b*.txt') | ids('sweep-d*.txt') | ids('sweep-e*.txt') | ids('sweep-f*.txt')
third = ids('sweep-g*.txt')
# Ranks 501-750: their colony runs used 2minnow (sweep-o*.txt, rerun without the mods below).
fifth = ids('sweep-o[0-9].txt')
fifth_failing = set(ids('sweep-o-excluded.txt'))

played = {
    '2837919908': 'clicks select what is under the cursor (its flat-scaled world selector); played on U57 and the Twinkleverse save it was reported with',
    '3427974009': 'side screen and group dialog as in the original',
    '1960947963': 'hover cards draw as in the original (needed ARCHITECTURE D17, D18)',
    '2815406414': 'tiles connect as in the original (needed D21)',
    '3630759126': 'menu and U57 colony with 0 errors (needed its IMGUI and asset bundle API, D20, the TMP caret fix); a multiplayer session needs two Steam accounts, not tested',
    '3775625682': "a player's images packed and shown on a canvas as in the original (needed `Texture2D.PackTextures`)",
    '3781314647': 'a colony with its new elements, 0 errors in both players (needed `Shader.GetPropertyCount/Name/Type`)',
    '2958243747': "issue #2's set",
    '2977728791': "issue #2's set",
    '3381262702': "issue #2's set",
    # Played by their specs (tests/e2e/mods, scripts/e2e.py): passed once in the original and
    # twice in ours, with no new log signature and screens matching the original's.
    '1717463209': 'spec tests/e2e/mods/1717463209-bigger-camera-zoom-out.yaml: the mouse wheel zooms out past the game\'s 20, to 200; world shots at three zoom levels within 3.1 % of the original',
    '2094698134': 'spec tests/e2e/mods/2094698134-airlock-door.yaml: built from copper through a build order, its side screen and status as in the original, shut and uncharged without power',
    '1893887916': 'spec tests/e2e/mods/1893887916-research-queue.yaml: Shift+click queues a tech with its prerequisites and numbers the queue; a click takes one out or replaces the queue; the research screen as in the original',
}
# Played by their specs, with a small difference in what the UI reports (not in what it shows):
# ⚠️ until it is fixed. The differences are the engine's, found by the specs' layout dumps.
differences = {
    '1863428350': 'spec tests/e2e/mods/1863428350-sweep-by-type.yaml: the filtered sweep marks only the checked material, and its list looks as in the original (no pixel differs); its layout dump does not match: a nested canvas reports scaleFactor 1 (0.86 in the original), its scroll view normalizedPosition x 1 (0), and an unused input field has laid out its caret',
    '3468585385': 'spec tests/e2e/mods/3468585385-blueprints-expanded.yaml: a blueprint created, chosen, rotated and placed through its windows ([#6](https://github.com/tigin-backwards/oni-brutal/issues/6) fixed), screens within 0.6 % of the original\'s pixels; the preview\'s scroll view reports normalizedPosition x 1 (0 in the original) and its content sits up to 30 px aside (no more than 0.4 % of the window\'s pixels differ)',
    '2856555858': 'spec tests/e2e/mods/2856555858-duplicant-stat-selector.yaml: stat editing ([#5](https://github.com/tigin-backwards/oni-brutal/issues/5) fixed), removing an interest and a trait, adding one through its searchable list, and printing the Duplicant with it; screens within 0.3 % of the original\'s pixels; the list\'s empty search field holds a zero-width space (empty in the original), which does not show',
}
# Loads and Colony as swept, Played withdrawn until a spec drives the mod's UI (mod-play-testing.md).
unplayed = {
    # Batch 14: content packs without code.
    # Batch 17.
    '3635632392': 'not played: a multiplayer mod (its own LiteNetLib client and server); a session needs two players on two Steam accounts',
    # Batch 18: base-game-only mods (every spec fixture is a Spaced Out colony).
    '1891479671': 'not played: it supports the base game only (`supportedContent: VANILLA_ID`; it patches the base game\'s Biological Cargo Bay, which Spaced Out lacks), and every spec fixture is a Spaced Out colony',
    '1886699455': 'not played: it supports the base game only (`supportedContent: VANILLA_ID`), and every spec fixture is a Spaced Out colony',
    # Batch 21.
}
broken = {
    '1715248368': 'broken upstream: patches `Deconstructable.TriggerDestroy`, which now has two overloads',
    '2363561445': 'broken upstream: `STRINGS.UI.TOOLS.FILTERLAYERS.ALL` is no longer a field',
    '1885337332': 'broken upstream: calls `TreeFilterable.GetTags()`, which the game no longer has',
    '2705951522': 'broken upstream: Harmony patch targets now have overloads',
    '1971998416': 'broken upstream: Harmony patch targets now have overloads',
    '1871382174': 'broken upstream: Harmony patch targets now have overloads',
    '1758223146': 'broken upstream: a Harmony patch whose target is gone',
    '2271373459': 'broken upstream: `OverlayModes.Mode.CreateDefaultFilters` returns another type now',
    '2968184715': 'broken upstream: its animations fail to load',
    '2542160819': 'broken upstream: calls `SimMessages.SetCellProperties(int, byte)`, which is gone',
    '3231839363': 'broken upstream: calls `SimMessages.SetCellProperties(int, byte)`, which is gone',
    '2119648603': "broken upstream: uses TextMeshPro's `FaceInfo`, which the game's TextMeshPro lacks",
    '3046440992': "broken upstream: uses TextMeshPro's `FaceInfo`, which the game's TextMeshPro lacks",
    '1708810461': 'broken upstream: CaiLib calls `EntityTemplates.ExtendEntityToBasicPlant` with arguments the game no longer takes, and the save fails to load',
    '2808978019': 'broken upstream: a Harmony patch whose target is gone (`Deconstructable.SpawnItemsFromConstruction`), and the error dialog quits the game',
    '2661907148': 'broken upstream: adds a space POI the game already has ("Granite"), and the save fails to load',
    '2111768571': 'broken upstream (its title says so): its plants call a removed `EntityTemplates.ExtendEntityToBasicPlant` overload',
    '1992978572': 'broken upstream: its filter patch reads the game\'s changed filter type and crashes the game',
    '1823182964': 'broken upstream: CaiLib calls `EntityTemplates.ExtendEntityToBasicPlant` with arguments the game no longer takes, and the save fails to load',
}
# Ranks 501-750 that fail as they do in the original (out of the sweep's colony runs).
broken.update({
    '2778263368': 'broken upstream: a Harmony transpiler whose target method is gone, and the error dialog quits the game',
    '2646067420': 'broken upstream: calls `ConsumedElement(Tag, float)`, which the game no longer has, and the error dialog quits the game',
    '2043689746': 'broken upstream: calls a `SettingLevel` constructor the game no longer has, and the error dialog quits the game',
})
# Found by the e2e specs of batch 05: the spec fails the same way in the original.
broken.update({
    '2584828134': 'broken upstream (its title says so): its sleeping spots still name the game\'s former `Worker` class, so a Duplicant who lies down in one gets a TypeLoadException and none of the bed\'s effects',
    '1820638089': 'broken upstream: its LIQUIDBOTTLER building\'s codex entry collides with the game\'s own Bottle Filler (LiquidBottler), and loading a colony stops at the crash screen',
})
# Found by the e2e specs of batch 07: the spec fails the same way in the original.
broken.update({
    '2869608898': 'broken upstream: its printing pod side screen calls `Dropdown.OptionData(string, Sprite)`, which the game\'s UI library no longer has, so the selected pod (with the mod\'s BioPrinter) shows no Bio-Ink screen and the crash report screen opens (spec tests/e2e/mods/2869608898-bio-inks.yaml)',
})
# Found by the e2e specs of batch 06: the spec fails the same way in the original.
broken.update({
    '2629866529': 'broken upstream: its PliersWorkable overrides `OnCompleteWork(Worker)`, and the game no longer has a `Worker` type: the type fails to load while the game creates its prefabs, and the player aborts loading a colony; spec tests/e2e/mods/2629866529-pliers-2021.yaml',
    '2539984678': 'broken upstream: its Constructable.OnSpawn transpiler multiplies the AETN\'s 2 cores by a core\'s 25 kg, but the game counts cores by the unit, so a blueprint asks for 50 Neutronium Cores and is never built; spec tests/e2e/mods/2539984678-rebuildable-aetn.yaml',
})
# Found by the e2e specs of batch 08: the spec fails the same way in the original.
broken.update({
    '1718226085': 'broken upstream: Mono cannot set up its emitter and receiver types (`ISliderControl` changed: "VTable setup of type WirelessAutomation.WirelessSignalEmitter failed"), which stops the game\'s prefab creation in `Assets.OnPrefabInit`, so loading a colony leaves prefabs missing and the game crashes (SIGSEGV) twice in two runs (spec tests/e2e/mods/1718226085-wireless-automation.yaml)',
    '1972768699': 'broken upstream: its 0 button and tool work, and a dig errand set to 0 carries the mod\'s precondition and priority class, yet a Duplicant digs it within 300 s, so priority 0 no longer holds errands back (spec tests/e2e/mods/1972768699-priority-zero.yaml)',
    '2356326908': 'broken upstream: its PLib side screens look for `DetailsScreen.sideScreenContentBody`, which the game no longer has, so loading a colony opens the crash screen (spec tests/e2e/mods/2356326908-wireless-power.yaml)',
    '2037089892': 'broken upstream: Mono cannot set up its `Trashcan` type (`ISidescreenButtonControl` gained `SetButtonTextOverride` and `HorizontalGroupID`: "VTable setup of type Trashcans.Trashcan failed"), which stops the game\'s prefab creation in `Assets.OnPrefabInit`, so loading a colony crashes (SIGSEGV) (spec tests/e2e/mods/2037089892-trashcans.yaml)',
    '2053191586': 'broken upstream: Mono cannot set up its `InfiniteSource` type (`ISliderControl.GetSliderTooltip` now takes an index: "VTable setup of type InfiniteSourceSink.InfiniteSource failed"), its buildings fail to register, and loading a colony quits the game (spec tests/e2e/mods/2053191586-new-infinite-sources.yaml)',
    '2524767809': 'broken upstream: its emptier\'s state machine calls `StateMachine.Instance.Subscribe(int, Action<object>)`, which the game no longer has, so the finished building throws a MissingMethodException in `BottleUserEmptier.OnSpawn` and the crash screen opens (spec tests/e2e/mods/2524767809-bottle-emptier-variable-size.yaml)',
})
# Found by the e2e specs of batch 12: the spec fails the same way in the original.
broken.update({
    '2897886291': 'broken upstream: its overlay overrides `OverlayModes.Mode.CreateDefaultFilters`, which returns another type now, so its `OverlayScreen.RegisterModes` postfix throws a MissingMethodException while the colony loads, the overlay screen has no modes and the crash screen opens (spec tests/e2e/mods/2897886291-sweep-zones.yaml)',
})
# Found by the e2e specs of batch 13: the spec fails the same way in the original.
broken.update({
    '1703400426': 'broken upstream: its DebugHandler.OnKeyDown prefix consumes Action 173, which the game\'s Action enum now gives to SreenShot32x (the debug toggle is 174), so with debug mode on Backspace still runs the game\'s debug toggle (its paint screen opens) and reveals the map (spec tests/e2e/mods/1703400426-debug-does-not-discover-map.yaml)',
})
# Found by the e2e specs of batch 17: the spec fails the same way in the original.
broken.update({
    '2872099618': 'broken upstream: its SpaceBomb\'s BuildingComplete.OnSpawn postfix calls `SimMessages.Dig(int, int, bool)`, which the game no longer has, so a finished bomb deletes itself and marks its background as space but digs nothing (spec tests/e2e/mods/2872099618-bomb-space.yaml)',
    '2367992483': 'broken upstream: its prefix names the 19-parameter `EntityTemplates.ExtendEntityToBasicPlant` of 2021 (the game\'s has 20); Harmony lists the method as patched and the game builds its plants through it, yet the prefix never runs, so no plant data or drop times are written or read (spec tests/e2e/mods/2367992483-automatic-harvesting.yaml)',
})
# Found by the e2e specs of batch 20: the spec fails the same way in the original.
broken.update({
    '3036286512': 'broken upstream: its DLL references PLib 4.8.1 but ships without it, so its `OnLoad` (`PUtil.InitLibrary`) throws a FileNotFoundException, its building fails to register (NullReferenceException, its anim missing), and the error dialog quits the game before the colony loads (spec tests/e2e/mods/3036286512-gas-liquefaction-machine.yaml)',
    '3425252325': 'broken upstream: its DLL references PLib 4.16 but ships without it, so its types fail to load (FileNotFoundException), `Assets.OnPrefabInit` stops on a ReflectionTypeLoadException and the game quits before OniMcp starts; it loads only beside another mod that ships PLib (spec tests/e2e/mods/3425252325-auto-drop-gas-bottler.yaml)',
    '1869090131': 'broken upstream: Mono cannot set up its `DestructibleWorkable` type ("VTable setup of type DestructibleFeatures.DestructibleWorkable failed": `ISidescreenButtonControl` gained members, and it overrides `OnCompleteWork(Worker)`), so a spawned geyser gets no workable and no DESTROY button (spec tests/e2e/mods/1869090131-destructible-features.yaml)',
})
# Found by the e2e specs of batch 21: the spec fails the same way in the original.
broken.update({
    '1873884303': 'broken upstream: on the base-game fixture its `OnLoad` stops on a HarmonyException (its `DetailsScreen.OnPrefabInit` patch takes `___sideScreenContentBody`, a field the game no longer has), its scanner then fails to register ("First anim file needs to be non-null", a NullReferenceException in RegisterBuilding), and the game disables it ("Failed to load mod ...disabling") and closes before the save loads',
    '2215696830': 'broken upstream: its tile\'s `ItemPermeableTile.OnSpawn` calls `SimMessages.ClearCellProperties(int, byte)`, which the game no longer has, so every finished Item Permeable Tile throws a MissingMethodException and opens the crash report screen (spec tests/e2e/mods/2215696830-item-permeable-tiles.yaml)',
})
# Fails to load in the original only.
original_only = {
    '1729794261': 'loads in ours but not in the original: Mono rejects its `PixelPack2` class (an interface method it lacks) at first use, where ours stubs the missing method (ARCHITECTURE D22); left out of the colony runs, which the original\'s error dialog stops',
}
unranked = {'3427974009': 'Synchronized Building Groups', '2977728791': 'Preset Manager', '3775625682': 'LocalCanvas-本地画框', '3781314647': 'Morphs Not Included',
            '3281716506': 'Mod Preset Manager (Light) [MPML]', '3746027329': 'Geyser Tab', '3671379574': 'Resource Dashboard'}
translations = {'929139073', '927642162', '1198657258', '928606476', '928348692', '1902735059', '1269554374',
                '1454392573', '929036307', '928441385', '1416077105', '929305589', '934649708', '931119869',
                '1562134514', '2906930548', '929224194', '1714866600', '1823722773', '3229139332', '2769557641',
                '2872159695', '1123693010', '1142641133', '2467094947', '2377723746',
                '2889982891', '2819586273', '1821957996', '3327324912', '1758933220', '930830432', '1831722850', '2701205009', '1493391479', '2566414917', '1419578320', '2000481647',
                '1135041189', '1817639915', '1561634664', '929154970', '933302292', '2679329370'}
# Worldgen mods no sweep ran, as their specs' runs of ours found them (id -> (loads, colony)): a
# spec that gets to its new game loaded a colony first; Single Asteroid Clusters' worlds log
# errors while the game loads them, which open the error dialog at the main menu in ours, and
# loading a colony then quits.
worldgen = {'3743010929': ('✅', '✅'), '3382061617': ('✅', '✅'), '2919833683': ('❌', '❌')}
# Third hundred: filled in from that sweep's results (id -> (loads, colony, note)).
third_results = {}
for extra in glob.glob(os.path.join(SWEEPS, 'modtable-*.tsv')):
    for line in open(extra, encoding='utf-8'):
        i, loads, colony, note = (line.rstrip('\n').split('\t') + [''] * 4)[:4]
        third_results[i] = (loads, colony, note)

Y, N, X, W = '✅', '⬜', '❌', '⚠️'

def row(rank, mid, title, loads, colony, play, note):
    link = f'[{title.replace("|", "/")}](https://steamcommunity.com/sharedfiles/filedetails/?id={mid})' if mid else title
    return f'| {rank} | {link} | {loads} | {colony} | {play} | {note} |'

def status(mid, flags):
    """(Loads, Colony, Played, note) of a mod from the sweeps and the hand-written sets."""
    if mid in played:
        return Y, Y, Y, played[mid]
    if mid in differences:
        return Y, Y, W, differences[mid]
    if mid in unplayed:
        return Y, Y, N, unplayed[mid]
    if mid in broken:
        return X, X, N, broken[mid] + ' (same in the original)'
    if flags:
        return N, N, N, f'flagged by onimaxxing ({flags}); not tested'
    if mid in worldgen:
        return (*worldgen[mid], N, 'worldgen mod: spec pending (functional expectations)')
    if mid in translations:
        return N, N, N, 'translation: the game applies one only from a Steam subscription; not testable here'
    if mid in original_only:
        return Y, N, N, original_only[mid]
    if mid in fifth_failing:
        return X, X, N, 'fails to load in the original as well, in its sweep batch'
    if mid in third_results:
        loads, colony, note = third_results[mid]
        return loads, colony, N, note
    if mid in fifth or mid in swept:
        return Y, Y, N, ''
    return N, N, N, ''


def with_record(mid, flags):
    """A mod's status, its Played column and note from its spec record when it has one: a played
    or ⚠️ spec ran it in a colony, so Loads and Colony are ✅."""
    loads, colony, play, note = status(mid, flags)
    if mid not in results:
        return loads, colony, play, note
    play, note = record_status(results[mid], FINDINGS)
    return (Y, Y, play, note) if play != N else (loads, colony, play, note)


rows = [row('–', None, 'Fast Track (GitHub)', Y, Y, Y, 'about 30 fps on U57, where the original runs 5–6')]
listed = set()
for r in cat:
    rank, mid, _, flags, title = int(r[0]), r[1], r[2], r[3], r[4]
    if rank > LAST_RANK and mid not in played and mid not in results: continue
    listed.add(mid)
    rows.append(row(rank, mid, title, *with_record(mid, flags)))
for mid in list(played) + [m for m in results if m not in played]:
    if mid not in listed and mid in by_id:
        r = by_id[mid]
        rows.append(row(r[0], mid, r[4], *with_record(mid, r[3])))
    elif mid not in listed:
        rows.append(row('–', mid, unranked.get(mid, mid), *with_record(mid, '')))

print(f'''# Mod compatibility

Which Workshop mods have been tried in this player, by popularity: the {LAST_RANK} most-subscribed
in [onimaxxing's catalog](https://onimaxxing.com/mods), and mods players reported. Worldgen mods are
played with functional expectations (their worlds generate with their content), not identical layouts.

**Columns:**
- **Loads:** reaches the main menu with no error of ours.
- **Colony:** loaded with its batch of about 25 mods and played 90 seconds of a Spaced Out colony,
  with no error of ours: U57 (late game) for ranks 1–500, 2minnow (mid game) for 501–750.
- **Played:** a save played with the mod, its screens and logs compared with the original player.
  ⚠️ means played with a small known difference, its issue linked.

"No error of ours" means no error that the original player's log lacks. ❌ means the mod fails the
same way in the original player (it needs an update from its author). Every run uses the
Workshop build of each mod, installed as a local mod (the closest to a subscription; Dev-folder
installs get developer checks subscribers never see).

| Rank | Mod | Loads | Colony | Played | Notes |
|---|---|---|---|---|---|
''' + '\n'.join(rows) + '''

## Known differences

- **Particle features no game data uses** (collision, lights, noise, clamp velocity, sub
  emitters, some shapes) are not simulated, and are logged; mods' effects (Capsule 2.0) look
  simpler.
- **LineRenderer:** rounded corners and caps are drawn square.
- **A mod class missing a method of a game interface** (built for an older game) loads, as on
  Mono; where Mono fails as soon as the game checks the class against the interface, ours fails
  only when the missing method is called (ARCHITECTURE D22).
- **Rephysicalized:** the reported build was the author's development build; not tracked.''')
