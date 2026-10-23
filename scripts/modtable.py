#!/usr/bin/env python3
"""scripts/modtable.py CATALOG.tsv > docs/MOD_COMPATIBILITY.md
The compatibility table from onimaxxing's catalog ranks (a TSV of rank, id, subscriptions, flags,
title) and what our sweeps recorded: the sweep-*.txt ID lists and modtable-*.tsv results in out/
(or ONI_SWEEP_DIR), and the status sets below."""
import glob, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SWEEPS = os.environ.get('ONI_SWEEP_DIR') or os.path.join(ROOT, 'out')
if len(sys.argv) != 2: sys.exit(__doc__)
cat = [l.rstrip('\n').split('\t') for l in open(sys.argv[1], encoding='utf-8')]
by_id = {r[1]: r for r in cat}
LAST_RANK = 750

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
    '2854869130': "issue #2's set",
    # Played by their specs (tests/e2e/mods, scripts/e2e.py): passed once in the original and
    # twice in ours, with no new log signature and screens matching the original's.
    '1717463209': 'spec tests/e2e/mods/1717463209-bigger-camera-zoom-out.yaml: the mouse wheel zooms out past the game\'s 20, to 200; world shots at three zoom levels within 3.1 % of the original',
    '2527442674': 'spec tests/e2e/mods/2527442674-full-miner-yield.yaml: a dig leaves debris weighing as much as the dug cells',
    '2094698134': 'spec tests/e2e/mods/2094698134-airlock-door.yaml: built from copper through a build order, its side screen and status as in the original, shut and uncharged without power',
    '1893887916': 'spec tests/e2e/mods/1893887916-research-queue.yaml: Shift+click queues a tech with its prerequisites and numbers the queue; a click takes one out or replaces the queue; the research screen as in the original',
}
# Played by their specs, with a small difference in what the UI reports (not in what it shows):
# ⚠️ until it is fixed. The differences are the engine's, found by the specs' layout dumps.
differences = {
    '1863428350': 'spec tests/e2e/mods/1863428350-sweep-by-type.yaml: the filtered sweep marks only the checked material, and its list looks as in the original (no pixel differs); its layout dump does not match: a nested canvas reports scaleFactor 1 (0.86 in the original), its scroll view normalizedPosition x 1 (0), and an unused input field has laid out its caret',
    '3468585385': 'spec tests/e2e/mods/3468585385-blueprints-expanded.yaml: a blueprint created, chosen, rotated and placed through its windows ([#6](https://github.com/tigin-backwards/oni-brutal/issues/6) fixed), screens within 0.6 % of the original\'s pixels; the preview\'s scroll view reports normalizedPosition x 1 (0 in the original) and its content sits 3.4 px aside',
    '2856555858': 'spec tests/e2e/mods/2856555858-duplicant-stat-selector.yaml: stat editing ([#5](https://github.com/tigin-backwards/oni-brutal/issues/5) fixed), removing an interest and a trait, adding one through its searchable list, and printing the Duplicant with it; screens within 0.3 % of the original\'s pixels; the list\'s empty search field holds a zero-width space (empty in the original), which does not show',
}
# Loads and Colony as swept, Played withdrawn until a spec drives the mod's UI (mod-play-testing.md).
unplayed = {}
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
# Fails to load in the original only.
original_only = {
    '1729794261': 'loads in ours but not in the original: Mono rejects its `PixelPack2` class (an interface method it lacks) at first use, where ours stubs the missing method (ARCHITECTURE D22); left out of the colony runs, which the original\'s error dialog stops',
}
unranked = {'3427974009': 'Synchronized Building Groups', '2977728791': 'Preset Manager', '3775625682': 'LocalCanvas-本地画框', '3781314647': 'Morphs Not Included'}
translations = {'929139073', '927642162', '1198657258', '928606476', '928348692', '1902735059', '1269554374',
                '1454392573', '929036307', '928441385', '1416077105', '929305589', '934649708', '931119869',
                '1562134514', '2906930548', '929224194', '1714866600', '1823722773', '3229139332', '2769557641',
                '2872159695', '1123693010', '1142641133', '2467094947', '2377723746',
                '2889982891', '2819586273', '1821957996', '3327324912', '1758933220', '930830432', '1831722850', '2701205009', '1493391479', '2566414917', '1419578320', '2000481647',
                '1135041189', '1817639915', '1561634664', '929154970', '933302292', '2679329370'}
worldgen = {'3743010929', '3382061617', '2919833683'}
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

rows = [row('–', None, 'Fast Track (GitHub)', Y, Y, Y, 'about 30 fps on U57, where the original runs 5–6')]
listed = set()
for r in cat:
    rank, mid, _, flags, title = int(r[0]), r[1], r[2], r[3], r[4]
    if rank > LAST_RANK and mid not in played: continue
    listed.add(mid)
    if mid in played:
        rows.append(row(rank, mid, title, Y, Y, Y, played[mid]))
    elif mid in differences:
        rows.append(row(rank, mid, title, Y, Y, W, differences[mid]))
    elif mid in unplayed:
        rows.append(row(rank, mid, title, Y, Y, N, unplayed[mid]))
    elif mid in broken:
        rows.append(row(rank, mid, title, X, X, N, broken[mid] + ' (same in the original)'))
    elif flags:
        rows.append(row(rank, mid, title, N, N, N, f'flagged by onimaxxing ({flags}); not tested'))
    elif mid in worldgen:
        rows.append(row(rank, mid, title, N, N, N, 'worldgen mod: out of scope'))
    elif mid in translations:
        rows.append(row(rank, mid, title, N, N, N, 'translation: the game applies one only from a Steam subscription; not testable here'))
    elif mid in original_only:
        rows.append(row(rank, mid, title, Y, N, N, original_only[mid]))
    elif mid in fifth_failing:
        rows.append(row(rank, mid, title, X, X, N, 'fails to load in the original as well, in its sweep batch'))
    elif mid in third_results:
        loads, colony, note = third_results[mid]
        rows.append(row(rank, mid, title, loads, colony, N, note))
    elif mid in fifth:
        rows.append(row(rank, mid, title, Y, Y, N, ''))
    elif mid in swept:
        rows.append(row(rank, mid, title, Y, Y, N, ''))
    elif mid in third:
        rows.append(row(rank, mid, title, N, N, N, ''))
    else:
        rows.append(row(rank, mid, title, N, N, N, ''))
for mid in played:
    if mid not in listed and mid in by_id:
        r = by_id[mid]
        rows.append(row(r[0], mid, r[4], Y, Y, Y, played[mid]))
    elif mid not in listed:
        rows.append(row('–', mid, unranked[mid], Y, Y, Y, played[mid]))

print(f'''# Mod compatibility

Which Workshop mods have been tried in this player, by popularity: the {LAST_RANK} most-subscribed
in [onimaxxing's catalog](https://onimaxxing.com/mods), and mods players reported. Worldgen mods are
out of scope.

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
