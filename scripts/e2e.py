#!/usr/bin/env python3
"""End-to-end specs: a colony checkpoint, played through OniMcp, checked by its outcome.
  scripts/e2e.py [--player ours|original|both] [--repeat N] [--bless] SPEC...
A spec (tests/e2e/NAME.yaml) starts from a fixture save (tests/e2e/fixtures), gives orders
through OniMcp's tools (an in-game MCP server, http://127.0.0.1:8788/mcp/), runs the game and
checks what came of it: buildings finished, Duplicants alive, rooms formed. The simulation is not
deterministic (its random numbers, the frame timing), so a spec checks outcomes and invariants,
never a trajectory, and --repeat runs it several times. --player both runs it in the original
Unity player too: a spec passes there first, and our player fails on a log exception or error whose
signature the original's runs lack. --bless copies the saves an original run made (a spec's
`save` steps) into tests/e2e/fixtures, as checkpoints for the specs after it.

Needs, in the environment or out/local-tools/qa.env: QA_U57_GAME and QA_U57_STEAM (the DLC
content and a Steam double owning it, as for qa.sh's U57 stages), E2E_ONIMCP (a build of
OniMcp's mod folder; its licence does not allow vendoring it) and, for --player original or
both, E2E_ORIGINAL (the original player's executable, with a DLC-owning Steam double in its
Plugins). Each run's log, results and screenshots are in out/e2e/SPEC/PLAYER-N.

Spec steps (YAML list under `steps`):
  dig: [x1, y1, x2, y2]                      dig orders on a rectangle (priority: 5)
  build: {prefab: P, at: [x, y]}             a blueprint, its lower-left cell at x, y; or
         {prefab: P, cells: [[x, y], ...]}    one per cell; `priority`, `material` optional
  run: {seconds: N, until: {...}}            game seconds at speed 3, stopping once `until`
                                              (as `expect`) holds; fails if it never does
  expect: {...}                              buildings: {Prefab: N}, duplicants: N,
                                              pending_construction: N, pending_digs: N,
                                              rooms: {Type: N},
                                              cycle: N; N is a number or ">=N", "<=N", ">N", "<N"
  call: {tool: T, args: {...}}               any OniMcp tool call, which must not fail
  save: NAME                                 save the colony as NAME (blessed into fixtures)
  shot: NAME                                 a screenshot into the run's folder
"""
import json, os, re, shutil, signal, socket, subprocess, sys, time, urllib.error, urllib.request

import yaml

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPECS = os.path.join(ROOT, 'tests', 'e2e')
FIXTURES = os.path.join(SPECS, 'fixtures')
OUT = os.path.join(ROOT, 'out', 'e2e')
URL = 'http://127.0.0.1:8788/mcp/'
PORT = 8788
WIDTH, HEIGHT = 1280, 720
PROFILE = ('config', 'unity3d', 'Klei', 'Oxygen Not Included')
# Unity's Mono has ValueTuple in mscorlib but no System.ValueTuple facade, which the
# System.Text.Json merged into OniMcp references: the original gets the package's forwarder.
VALUETUPLE = os.path.expanduser('~/.nuget/packages/system.valuetuple/4.6.1/lib/net47/System.ValueTuple.dll')


class SpecFailure(Exception):
    pass


def environment():
    """The settings from the environment, else from out/local-tools/qa.env (a bash file)."""
    names = ['QA_U57_GAME', 'QA_U57_STEAM', 'E2E_ONIMCP', 'E2E_ORIGINAL', 'GALLIUM_DRIVER',
             'MESA_D3D12_DEFAULT_ADAPTER_NAME']
    env = {n: os.environ[n] for n in names if n in os.environ}
    qa_env = os.path.join(ROOT, 'out', 'local-tools', 'qa.env')
    if os.path.exists(qa_env):
        script = f'root={ROOT!r}; set -a; source {qa_env!r}; ' + '; '.join(f'echo "{n}=${{{n}:-}}"' for n in names)
        for line in subprocess.run(['bash', '-c', script], capture_output=True, text=True).stdout.splitlines():
            name, _, value = line.partition('=')
            if value and name not in env:
                env[name] = value
    return env


class Mcp:
    """A minimal client for OniMcp's streamable HTTP endpoint (JSON text results)."""

    def __init__(self):
        self.session = None

    def post(self, body):
        headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
        if self.session:
            headers['Mcp-Session-Id'] = self.session
        request = urllib.request.Request(URL, data=json.dumps(body).encode(), headers=headers)
        with urllib.request.urlopen(request, timeout=120) as response:
            self.session = response.headers.get('Mcp-Session-Id') or self.session
            raw = response.read().decode()
        if raw.startswith('event:') or raw.startswith('data:') or '\ndata:' in raw:
            data = [line[5:].strip() for line in raw.splitlines() if line.startswith('data:')]
            raw = data[-1] if data else '{}'
        return json.loads(raw) if raw.strip() else {}

    def connect(self):
        self.session = None
        self.post({'jsonrpc': '2.0', 'id': 1, 'method': 'initialize', 'params': {
            'protocolVersion': '2025-06-18', 'capabilities': {}, 'clientInfo': {'name': 'e2e', 'version': '1'}}})
        self.post({'jsonrpc': '2.0', 'method': 'notifications/initialized'})

    @staticmethod
    def parse(result):
        if 'error' in result:
            raise SpecFailure('MCP error ' + json.dumps(result['error']))
        body = result.get('result', {})
        parts = body.get('content') or body.get('contents') or []
        text = '\n'.join(p.get('text', '') for p in parts)
        try:
            value = json.loads(text)
        except ValueError:
            value = text
        return value, bool(body.get('isError'))

    def call(self, tool, **args):
        args.setdefault('task', 'e2e spec')
        value, error = self.parse(self.post({'jsonrpc': '2.0', 'id': 2, 'method': 'tools/call',
                                             'params': {'name': tool, 'arguments': args}}))
        if error:
            raise SpecFailure(f'{tool} failed: {str(value)[:600]}')
        return value

    def read(self, uri):
        return self.parse(self.post({'jsonrpc': '2.0', 'id': 3, 'method': 'resources/read', 'params': {'uri': uri}}))[0]


def port_open():
    with socket.socket() as s:
        s.settimeout(1)
        return s.connect_ex(('127.0.0.1', PORT)) == 0


class Player:
    """One player process on its own Xvfb display, with a scratch profile holding OniMcp and a fixture."""

    def __init__(self, kind, out, env, fixture):
        self.kind, self.out, self.env = kind, out, env
        self.home = os.path.join(out, 'home')
        self.profile = os.path.join(self.home, *PROFILE)
        self.log = os.path.join(out, 'player.log' if kind == 'original' else 'stdout.txt')
        self.save_dir = os.path.join(self.profile, 'save_files', 'e2e')
        self.fixture = os.path.join(self.save_dir, os.path.basename(fixture))
        shutil.rmtree(out, ignore_errors=True)
        os.makedirs(os.path.join(self.profile, 'mods', 'Local'))
        os.makedirs(self.save_dir)
        shutil.copy(fixture, self.fixture)
        mod = os.path.join(self.profile, 'mods', 'Local', 'OniMcp')
        shutil.copytree(env['E2E_ONIMCP'], mod)
        if kind == 'original':
            shutil.copy(VALUETUPLE, mod)
        # The mod's static ID, its folder name as the label (a local mod's version is its folder's
        # hash, which KMod recomputes), on for both content sets.
        with open(os.path.join(self.profile, 'mods', 'mods.json'), 'w') as f:
            json.dump({'version': 1, 'mod_load_in_progress': False, 'mods': [{
                'label': {'distribution_platform': 0, 'id': 'OniMcp', 'title': 'OniMcp', 'version': 312605614},
                'status': 1, 'enabled': True, 'enabledForDlc': ['', 'EXPANSION1_ID'], 'crash_count': 0,
                'reinstall_path': None, 'staticID': 'LIghtJUNction.OniMcp'}]}, f)
        # OniMcp's options: JSON tool text, and none of its own gameplay policies (it turns the
        # global auto-disinfect off and dismisses tutorial notifications by default).
        with open(os.path.join(self.profile, 'OniMcpConfig.json'), 'w') as f:
            json.dump({'SecurityMigrationVersion': 1, 'Host': 'localhost', 'Port': PORT, 'AuthEnabled': False,
                       'GlobalAutoDisinfectDisabled': False, 'AutoDismissTutorialNotifications': False,
                       'ToolTextFormat': 'json'}, f)
        with open(os.path.join(self.profile, 'kplayerprefs.yaml'), 'w') as f:
            f.write(f'strings: {{}}\nints:\n  ResolutionWidth: {WIDTH}\n  ResolutionHeight: {HEIGHT}\n'
                    '  RefreshRateNumerator: 60\n  RefreshRateDenominator: 1\n  FullScreen: 0\nfloats: {}\n')
        self.process = None

    def start(self):
        if port_open():
            raise SpecFailure(f'port {PORT} is already in use (another OniMcp session?)')
        size = ['-screen-width', str(WIDTH), '-screen-height', str(HEIGHT), '-screen-fullscreen', '0']
        env = dict(os.environ, XDG_CONFIG_HOME=os.path.join(self.home, 'config'),
                   XDG_CACHE_HOME=os.path.join(self.home, 'cache'))
        for name in ('GALLIUM_DRIVER', 'MESA_D3D12_DEFAULT_ADAPTER_NAME'):
            if name in self.env:
                env[name] = self.env[name]
        if self.kind == 'original':
            command = [self.env['E2E_ORIGINAL'], *size, '-logFile', self.log]
            stdout = os.path.join(self.out, 'stdout.txt')
        else:
            env.update(ONI_GAME_DIR=self.env['QA_U57_GAME'], ONI_RUN_HOME=self.home, ONI_CONFIG='release')
            command = [os.path.join(ROOT, 'scripts', 'run.sh'), '--native-override',
                       'steam_api=' + self.env['QA_U57_STEAM'], *size, '--game-tweak', 'exact']
            stdout = self.log
        command = ['xvfb-run', '-a', '-s', f'-screen 0 {WIDTH}x{HEIGHT}x24', *command]
        self.process = subprocess.Popen(command, env=env, stdout=open(stdout, 'w'), stderr=subprocess.STDOUT,
                                        start_new_session=True)
        deadline = time.time() + 900
        while not port_open():
            if self.process.poll() is not None:
                raise SpecFailure(f'the player exited ({self.process.returncode}) before OniMcp started')
            if time.time() > deadline:
                raise SpecFailure('OniMcp did not start within 15 minutes')
            time.sleep(3)

    def load(self, mcp):
        mcp.connect()
        mcp.call('game_control', domain='launch', action='start', path=self.fixture, confirm=True)
        deadline = time.time() + 900
        while True:
            time.sleep(3)
            if self.process.poll() is not None:
                raise SpecFailure(f'the player exited ({self.process.returncode}) while loading')
            try:
                status = mcp.read('oni://colony/status')
            except (urllib.error.URLError, ConnectionError, SpecFailure):
                status = {}
            # The player starts at the main menu, so a colony with Duplicants is the fixture's.
            if isinstance(status, dict) and status.get('duplicantCount', 0) > 0:
                break
            if time.time() > deadline:
                raise SpecFailure('the fixture did not load within 15 minutes')
        mcp.call('game_control', domain='speed', action='pause', confirm=True)

    def stop(self):
        if self.process and self.process.poll() is None:
            os.killpg(self.process.pid, signal.SIGTERM)
            try:
                self.process.wait(30)
            except subprocess.TimeoutExpired:
                os.killpg(self.process.pid, signal.SIGKILL)
                self.process.wait()
        deadline = time.time() + 30
        while port_open() and time.time() < deadline:
            time.sleep(1)

    def signatures(self):
        result = subprocess.run([sys.executable, os.path.join(ROOT, 'scripts', 'qa-tools.py'), 'signatures', self.log],
                                capture_output=True, text=True)
        return set(result.stdout.splitlines())


def compare(actual, wanted):
    if isinstance(wanted, (int, float)):
        return actual == wanted
    match = re.fullmatch(r'\s*(>=|<=|>|<|==)?\s*(-?\d+(?:\.\d+)?)\s*', str(wanted))
    if not match:
        raise SpecFailure(f'bad expectation {wanted!r}')
    op, number = match.group(1) or '==', float(match.group(2))
    return {'>=': actual >= number, '<=': actual <= number, '>': actual > number,
            '<': actual < number, '==': actual == number}[op]


def facts(mcp, keys):
    """What `expect` and `until` check, read only as far as the keys need."""
    found = {}
    if 'buildings' in keys:
        summary = mcp.read('oni://colony/summary')
        found['buildings'] = {b['id']: b['count'] for b in summary.get('buildings', [])}
    if 'duplicants' in keys or 'cycle' in keys:
        status = mcp.read('oni://colony/status')
        found['duplicants'], found['cycle'] = status.get('duplicantCount', 0), status.get('cycle', 0)
    if 'pending_construction' in keys:
        index = mcp.call('world_editor', command='read', path='/active/index.md', format='progress', limit=1)
        text = index.get('content', '') if isinstance(index, dict) else str(index)
        match = re.search(r'pending=(\d+)', text)
        if not match:
            raise SpecFailure('no construction progress in /active/index.md')
        found['pending_construction'] = int(match.group(1))
    if 'pending_digs' in keys:
        pending = mcp.call('game_control', domain='speed', action='step_facts').get('pendingOrders', {})
        found['pending_digs'] = pending.get('remainingDigOrders', 0)
    if 'rooms' in keys:
        rooms = mcp.read('oni://rooms/list')
        counts = {}
        for room in rooms.get('rooms', []) if isinstance(rooms, dict) else []:
            kind = room.get('roomType') or room.get('type') or room.get('id')
            counts[kind] = counts.get(kind, 0) + 1
        found['rooms'] = counts
    return found


def unmet(mcp, expect):
    """The expectations that do not hold, as readable lines."""
    found = facts(mcp, expect.keys())
    misses = []
    for key, wanted in expect.items():
        if isinstance(wanted, dict):
            for name, want in wanted.items():
                have = found[key].get(name, 0)
                if not compare(have, want):
                    misses.append(f'{key}.{name} is {have}, wanted {want}')
        elif key in found:
            if not compare(found[key], wanted):
                misses.append(f'{key} is {found[key]}, wanted {wanted}')
        else:
            raise SpecFailure(f'unknown expectation {key!r}')
    return misses


def edit(mcp, path, search, replace):
    result = mcp.call('world_editor', command='edit', path=path, confirm=True,
                      content=f'<<<<<<< SEARCH\n{search}\n=======\n{replace}\n>>>>>>> REPLACE')
    if not (isinstance(result, dict) and result.get('ok') and not result.get('failed')):
        raise SpecFailure(f'edit of {path} failed: {json.dumps(result)[:1500]}')
    return result


def dig(mcp, rect, priority=5):
    x1, y1, x2, y2 = rect
    edit(mcp, '/active/ops/orders.md', '', f'挖 x1={x1} y1={y1} x2={x2} y2={y2} priority={priority}')


def build(mcp, prefab, cells, priority=5, material=None):
    """Blueprints through the viewport map (OniMcp places exact construction only that way): each
    cell's token on its map row becomes PREFAB:PRIORITY[#MATERIAL], one row per edit."""
    token = f'{prefab}:{priority}' + (f'#{material}' if material else '')
    rows = {}
    for x, y in cells:
        rows.setdefault(y, []).append(x)
    for y, xs in sorted(rows.items()):
        mcp.call('navigation_control', action='focus_cell', x=min(xs), y=y, snap=True)
        page = mcp.call('world_editor', command='read', path='/active/map/viewport.md', format='edit',
                        compact=False, view='default')
        text = page.get('content', '') if isinstance(page, dict) else str(page)
        bounds = re.search(r'X=(\d+)~(\d+)', text)
        line = next((l for l in text.splitlines() if l.startswith(f'Y={y}:')), None)
        if not bounds or line is None:
            raise SpecFailure(f'row {y} is not on the viewport map')
        x0, x_end = int(bounds.group(1)), int(bounds.group(2))
        head, cells_text = line.split(':', 1)
        parts = cells_text.split()
        tokens = [i for i, part in enumerate(parts) if part != '┆']
        for x in xs:
            if not x0 <= x <= x_end:
                raise SpecFailure(f'cell {x},{y} is outside the viewport ({x0}..{x_end})')
            parts[tokens[x - x0]] = token
        edit(mcp, '/active/map/viewport.md', line, head + ': ' + ' '.join(parts))


def run(mcp, seconds, until=None, log=None):
    """Game time at speed 3 in steps of up to a minute, until `until` holds or the time is spent."""
    spent, stalls = 0.0, 0
    while spent < seconds:
        if until and not unmet(mcp, until):
            return spent
        step = mcp.call('game_control', domain='speed', action='step_until', until={'fact': 'cycle_end'},
                        speed=3, maxGameSeconds=min(60, seconds - spent), maxWallSeconds=30, confirm=True)
        elapsed = step.get('elapsedGameSeconds', 0) if isinstance(step, dict) else 0
        if log:
            log(f'  ran {elapsed:.0f} s ({step.get("reason") or step.get("status")})')
        if elapsed <= 0:
            # Just after a load the game's speed controls are not up yet, and a step returns at
            # once; it stalls for good only if it keeps doing so.
            stalls += 1
            if stalls >= 10:
                raise SpecFailure(f'the game did not advance: {json.dumps(step)[:600]}')
            time.sleep(3)
            continue
        stalls = 0
        spent += elapsed
    if until:
        misses = unmet(mcp, until)
        if misses:
            raise SpecFailure(f'after {spent:.0f} game seconds: ' + '; '.join(misses))
    return spent


def screenshot(mcp, out, name):
    result = mcp.call('navigation_control', action='screenshot', filename=f'e2e-{name}', waitFrames=5)
    path = result.get('path') or result.get('file') if isinstance(result, dict) else None
    if path and os.path.exists(path):
        shutil.copy(path, os.path.join(out, name + os.path.splitext(path)[1]))


def run_spec(spec, kind, index, env, log):
    out = os.path.join(OUT, spec['name'], f'{kind}-{index}')
    fixture = os.path.join(FIXTURES, spec['from'] + '.sav')
    player = Player(kind, out, env, fixture)
    mcp = Mcp()
    saves, outcome, number = [], None, 0
    records = open(os.path.join(out, 'results.jsonl'), 'w')
    try:
        started = time.time()
        player.start()
        player.load(mcp)
        log(f'  loaded {spec["from"]} in {time.time() - started:.0f} s')
        for number, step in enumerate(spec['steps'], 1):
            (kind_of_step, value), = step.items()
            began = time.time()
            if kind_of_step == 'dig':
                dig(mcp, value)
            elif kind_of_step == 'build':
                cells = value.get('cells') or [value['at']]
                build(mcp, value['prefab'], cells, value.get('priority', 5), value.get('material'))
            elif kind_of_step == 'run':
                spent = run(mcp, value['seconds'], value.get('until'), log)
                log(f'  run: {spent:.0f} game seconds')
            elif kind_of_step == 'expect':
                misses = unmet(mcp, value)
                if misses:
                    raise SpecFailure('; '.join(misses))
            elif kind_of_step == 'call':
                mcp.call(value['tool'], **value.get('args', {}))
            elif kind_of_step == 'save':
                saved = mcp.call('game_control', domain='save', action='save', name=value, overwrite=True,
                                 updateActiveSave=False, confirm=True)
                path = saved.get('saved') if isinstance(saved, dict) else None
                if not path or not os.path.exists(path):
                    raise SpecFailure(f'save {value} failed: {json.dumps(saved)[:600]}')
                shutil.copy(path, os.path.join(out, value + '.sav'))
                saves.append(value)
            elif kind_of_step == 'shot':
                screenshot(mcp, out, value)
            else:
                raise SpecFailure(f'unknown step {kind_of_step!r}')
            records.write(json.dumps({'step': number, kind_of_step: value, 'wall': round(time.time() - began, 1)}) + '\n')
            records.flush()
        screenshot(mcp, out, 'end')
    except (SpecFailure, urllib.error.URLError, ConnectionError, TimeoutError) as e:
        outcome = f'step {number}: {e}'
        try:
            screenshot(mcp, out, 'failure')
        except Exception:
            pass
    finally:
        player.stop()
        records.close()
    return outcome, player.signatures(), saves, out


def main(argv):
    players, repeat, bless, names = 'ours', 1, False, []
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == '--player':
            players = argv[i + 1]; i += 1
        elif a == '--repeat':
            repeat = int(argv[i + 1]); i += 1
        elif a == '--bless':
            bless = True
        elif a.startswith('-'):
            sys.exit(__doc__)
        else:
            names.append(a)
        i += 1
    if not names:
        sys.exit(__doc__)
    env = environment()
    kinds = ['original', 'ours'] if players == 'both' else [players]
    needed = ['QA_U57_GAME', 'QA_U57_STEAM', 'E2E_ONIMCP'] + (['E2E_ORIGINAL'] if 'original' in kinds else [])
    missing = [n for n in needed if not env.get(n)]
    if missing:
        print('e2e: skipped, needs ' + ', '.join(missing) + ' (out/local-tools/qa.env)')
        return 0
    failures = 0
    for name in names:
        path = name if name.endswith('.yaml') else os.path.join(SPECS, name + '.yaml')
        spec = yaml.safe_load(open(path))
        spec.setdefault('name', os.path.splitext(os.path.basename(path))[0])
        reference = set()
        for kind in kinds:
            for index in range(1, repeat + 1):
                print(f'== {spec["name"]} in {kind} ({index}/{repeat})', flush=True)
                outcome, signatures, saves, out = run_spec(spec, kind, index, env, lambda s: print(s, flush=True))
                if kind == 'original':
                    reference |= signatures
                new = sorted(signatures - reference) if kind == 'ours' and 'original' in kinds else []
                if outcome:
                    failures += 1
                    print(f'FAIL {spec["name"]} in {kind}: {outcome}')
                elif new:
                    failures += 1
                    print(f'FAIL {spec["name"]} in {kind}: log signatures the original lacks:')
                    for s in new:
                        print('     ' + s)
                else:
                    print(f'ok   {spec["name"]} in {kind} ({out})')
                if kind != 'ours' and signatures:
                    print(f'     the original logged {len(signatures)} signature(s)')
                if bless and kind == 'original' and not outcome and index == 1:
                    for save in saves:
                        shutil.copy(os.path.join(out, save + '.sav'), os.path.join(FIXTURES, save + '.sav'))
                        print(f'     blessed fixture {save}')
    print(f'== {failures} failure(s)')
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
