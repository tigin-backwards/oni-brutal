#!/usr/bin/env python3
"""End-to-end specs: a colony checkpoint, played through OniMcp and E2eDriver, checked by its outcome.
  scripts/e2e.py [--player ours|original|both] [--repeat N] [--bless] [--lanes N] [--first-lane L]
                 [--displays :88,:89] [--no-perf-wait] [--results PATH] SPEC...
  scripts/e2e.py --check SPEC...             parse and validate specs only
A spec (tests/e2e/NAME.yaml; tests/e2e/README.md describes every step) starts from a fixture save
(tests/e2e/fixtures), installs Workshop mods, gives orders through OniMcp's tools (an in-game MCP
server), drives the UI with real X11 input (XTEST) located through E2eDriver (our test mod, a
JSON-lines server in the game), runs the game and checks what came of it. The simulation is not
deterministic, so a spec checks outcomes and invariants, never a trajectory, and --repeat runs it
several times. --player both runs it in the original Unity player first: a spec passes there
first, and then our player is compared with it (docs/research/mod-play-testing.md §5): new log
signatures, required log lines, UI dumps (ui-diff.py), cropped and masked screenshots
(qa-tools.py image) and values a spec compares with the original's. --bless copies an original
run's saves into tests/e2e/fixtures and its evidence into out/e2e-refs/SPEC, which a run of ours
alone compares against.

Lanes: --lanes N runs specs from a queue on N lanes at once. Lane L's OniMcp listens on
8800 + 10 L and its E2eDriver on 8801 + 10 L (E2E_DRIVER_PORT); lanes are numbered from
--first-lane (1). Each lane has its own display: xvfb-run -a, or one of --displays. A lane holds
out/e2e/.lane-L.lock, and out/e2e/.lock shared, so a perf job taking that lock exclusively
(`flock out/e2e/.lock CMD`) keeps specs from starting; specs also wait while ps shows a perf job.

Needs, in the environment or out/local-tools/qa.env: QA_U57_GAME and QA_U57_STEAM (the DLC
content and a Steam double owning it, as for qa.sh's U57 stages), E2E_ONIMCP (a build of
OniMcp's mod folder; its licence does not allow vendoring it), E2E_DRIVER (E2eDriver's mod
folder; default out/bin/E2eDriver/release) for specs that use it and, for --player original or
both, E2E_ORIGINAL (the original player's executable, with a DLC-owning Steam double in its
Plugins). Each run's log, results and evidence are in out/e2e/SPEC/PLAYER-N; out/e2e/results.json
holds one record per spec run.
"""
import contextlib, ctypes, fcntl, importlib.util, json, os, re, shutil, signal, socket, subprocess, sys
import threading, time, urllib.error, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPECS = os.path.join(ROOT, 'tests', 'e2e')
FIXTURES = os.path.join(SPECS, 'fixtures')
OUT = os.path.join(ROOT, 'out', 'e2e')
REFS = os.path.join(ROOT, 'out', 'e2e-refs')
WIDTH, HEIGHT = 1280, 720
PROFILE = ('config', 'unity3d', 'Klei', 'Oxygen Not Included')
# Unity's Mono has ValueTuple in mscorlib but no System.ValueTuple facade, which the
# System.Text.Json merged into OniMcp references: the original gets the package's forwarder.
VALUETUPLE = os.path.expanduser('~/.nuget/packages/system.valuetuple/4.6.1/lib/net47/System.ValueTuple.dll')
MAX_LANES = 6          # the memory budget's hard maximum (§6.2)
SHOT_THRESHOLD = 0.02  # fraction of pixels differing (§5.3), set per step after calibration
WORLD_THRESHOLD = 0.05
UI_TOLERANCE = 2       # pixels, for ui-diff.py --tolerance
FRAME_FALLBACK = 0.1   # seconds a "frame" lasts without E2eDriver to count real ones
# Perf jobs a spec never starts beside (matched against `ps -eo args`): the QA stages, the sweep
# and measurement scripts, profilers.
PERF_JOBS = [r'(^|/)scripts/qa\.sh\b', r'(^|/)scripts/m9-compare\.sh\b', r'(^|/)scripts/flamegraph\.py\b',
             r'out/local-tools/(sweep|measure|ft-bench|compare-queued|minnow-prof|glut-prof|queue|orig-queue|tw-run)',
             r'^perf (record|stat)\b']


class SpecFailure(Exception):
    pass


class SpecError(Exception):
    """A spec that does not parse or validate."""


printing = threading.Lock()


def say(*parts):
    with printing:
        print(*parts, flush=True)


def environment():
    """The settings from the environment, else from out/local-tools/qa.env (a bash file)."""
    names = ['QA_U57_GAME', 'QA_U57_STEAM', 'E2E_ONIMCP', 'E2E_ORIGINAL', 'E2E_DRIVER', 'GALLIUM_DRIVER',
             'MESA_D3D12_DEFAULT_ADAPTER_NAME']
    env = {n: os.environ[n] for n in names if n in os.environ}
    qa_env = os.path.join(ROOT, 'out', 'local-tools', 'qa.env')
    if os.path.exists(qa_env):
        script = f'root={ROOT!r}; set -a; source {qa_env!r}; ' + '; '.join(f'echo "{n}=${{{n}:-}}"' for n in names)
        for line in subprocess.run(['bash', '-c', script], capture_output=True, text=True).stdout.splitlines():
            name, _, value = line.partition('=')
            if value and name not in env:
                env[name] = value
    if not env.get('E2E_DRIVER'):
        default = os.path.join(ROOT, 'out', 'bin', 'E2eDriver', 'release')
        if os.path.isdir(default):
            env['E2E_DRIVER'] = default
    return env


def load_script(name):
    """A script of ours with a dash in its name, as a module."""
    path = os.path.join(ROOT, 'scripts', name)
    spec = importlib.util.spec_from_file_location(name.replace('-', '_').removesuffix('.py'), path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def ws_mods():
    """ws-mods.py (Workshop items as Local mods; it needs profile(MODS_DIR, IDS) and
    mono_hash(TEXT)): from scripts/ once it has moved there (decision 5), else out/local-tools."""
    for path in (os.path.join(ROOT, 'scripts', 'ws-mods.py'), os.path.join(ROOT, 'out', 'local-tools', 'ws-mods.py')):
        if os.path.exists(path):
            spec = importlib.util.spec_from_file_location('ws_mods', path)
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            return module
    raise SpecFailure('ws-mods.py is in neither scripts/ nor out/local-tools/')


# ---------------------------------------------------------------- specs

TOP_KEYS = {'name', 'from', 'mods', 'options', 'setup', 'steps', 'require_log', 'mask_text', 'tolerance',
            'thresholds'}
# Steps that talk to E2eDriver (a spec with any of them needs it installed).
DRIVER_STEPS = {'click', 'toggle', 'dropdown', 'type', 'drag', 'tool', 'expect_ui', 'expect_state', 'ui_dump',
                'ui_wait', 'world_shot', 'driver', 'click_world', 'scroll'}
STEP_KINDS = {'dig', 'build', 'run', 'expect', 'call', 'save', 'shot', 'open', 'key'} | DRIVER_STEPS
DRIVER_COMMANDS = {'ui.find', 'ui.dump', 'ui.wait', 'world.to_screen', 'screen.to_cell', 'state.get', 'camera.get',
                   'camera.set', 'canvas.hide', 'canvas.show', 'setup.call', 'seed'}


def _cell(value):
    return isinstance(value, (list, tuple)) and len(value) == 2 and all(isinstance(v, (int, float)) for v in value)


def _rect(value):
    return isinstance(value, (list, tuple)) and len(value) == 4 and all(isinstance(v, (int, float)) for v in value)


def _ui_query(where, value, problems):
    if not isinstance(value, dict) or not (value.get('ui') or value.get('text')):
        problems.append(f'{where}: needs `ui` (a path) or `text`')


def check_step(where, kind, value, in_setup):
    """The problems of one step, as readable lines."""
    problems = []
    need = lambda ok, text: ok or problems.append(f'{where}: {text}')
    if kind not in STEP_KINDS:
        return [f'{where}: unknown step {kind!r}']
    if kind == 'dig':
        need(_rect(value), 'dig takes [x1, y1, x2, y2]')
    elif kind == 'build':
        need(isinstance(value, dict) and value.get('prefab') and (_cell(value.get('at')) or value.get('cells')),
             'build takes {prefab, at: [x, y]} or {prefab, cells: [...]}')
    elif kind == 'run':
        need(isinstance(value, dict) and isinstance(value.get('seconds'), (int, float)), 'run takes {seconds: N}')
    elif kind in ('expect', 'expect_state'):
        need(isinstance(value, dict) and value, f'{kind} takes a mapping of expectations')
    elif kind == 'call':
        need(isinstance(value, dict) and value.get('tool'), 'call takes {tool, args}')
    elif kind == 'save':
        need(isinstance(value, str) and value, 'save takes a name')
    elif kind == 'shot':
        need(isinstance(value, str) or (isinstance(value, dict) and value.get('name')), 'shot takes NAME or {name, crop, mask}')
        if isinstance(value, dict):
            for m in value.get('mask', []):
                need(isinstance(m, str) or _rect(m), f'mask {m!r} is neither a UI path nor [x, y, w, h]')
    elif kind == 'world_shot':
        need(isinstance(value, dict) and value.get('name'), 'world_shot takes {name, ortho, at, hide_ui, mask}')
        if isinstance(value, dict):
            if 'at' in value:
                need(_cell(value['at']), '`at` is a cell [x, y]')
            for m in value.get('mask', []):
                need(_rect(m) or (isinstance(m, str) and re.search(r'[/*]', m)),
                     f'mask {m!r}: world shots mask [x, y, w, h] or UI paths (no symbolic masks yet)')
    elif kind == 'open':
        need(isinstance(value, dict) and len([k for k in value if k in ('management', 'key', 'user_menu')]) == 1
             and set(value) <= {'management', 'key', 'user_menu', 'at', 'id'},
             'open takes {management: NAME}, {key: CODE} or {user_menu: ACTION_KEY, at: [x, y] | id: ID}')
        if isinstance(value, dict) and 'key' in value:
            problems += [f'{where}: {p}' for p in key_problems(value['key'])]
    elif kind == 'key':
        need(isinstance(value, dict) and value.get('code'), 'key takes {code: "Shift+B"}')
        if isinstance(value, dict) and value.get('code'):
            problems += [f'{where}: {p}' for p in key_problems(value['code'])]
    elif kind == 'click':
        _ui_query(where, value, problems)
        if isinstance(value, dict):
            for m in value.get('mods', []):
                problems += [f'{where}: {p}' for p in key_problems(m)]
    elif kind == 'toggle':
        _ui_query(where, value, problems)
        need(isinstance(value, dict) and isinstance(value.get('to'), bool), 'toggle needs `to: true|false`')
    elif kind == 'dropdown':
        _ui_query(where, value, problems)
        need(isinstance(value, dict) and value.get('option') is not None, 'dropdown needs `option`')
    elif kind == 'type':
        need(isinstance(value, dict) and value.get('ui'), 'type needs `ui` (the input field\'s path)')
        need(isinstance(value, dict) and isinstance(value.get('text'), str), 'type needs `text`')
    elif kind == 'drag':
        need(isinstance(value, dict) and _cell(value.get('from')) and _cell(value.get('to')),
             'drag takes {from: [x, y], to: [x, y]} (world cells)')
    elif kind == 'click_world':
        need(isinstance(value, dict) and _cell(value.get('cell')), 'click_world takes {cell: [x, y]}')
    elif kind == 'scroll':
        need(isinstance(value, dict) and isinstance(value.get('notches'), int), 'scroll takes {at, notches: N}')
    elif kind == 'tool':
        need(isinstance(value, dict) and (value.get('toolbar') or value.get('ui') or value.get('key')),
             'tool takes {toolbar: NAME}, {ui: PATH} or {key: CODE}')
    elif kind == 'expect_ui':
        need(isinstance(value, dict) and value and all(isinstance(v, dict) for v in value.values()),
             'expect_ui takes {PATH: {text, active, interactable, isOn, exists}}')
    elif kind == 'ui_dump':
        need(isinstance(value, dict) and value.get('name') and value.get('root'), 'ui_dump takes {name, root}')
    elif kind == 'ui_wait':
        need(isinstance(value, dict) and value.get('path'), 'ui_wait takes {path, cond, frames}')
    elif kind == 'driver':
        need(isinstance(value, dict) and len(value) == 1, 'driver takes one {command: args}')
        if isinstance(value, dict) and len(value) == 1:
            (command, _), = value.items()
            need(command in DRIVER_COMMANDS, f'unknown E2eDriver command {command!r}')
            # Reflection may prepare a test, never perform the thing under test (§1, issue #6).
            need(in_setup or command != 'setup.call', 'driver setup.call is only allowed in setup:')
    return problems


def check_spec(spec):
    """The problems of a parsed spec, as readable lines (empty when it is valid)."""
    if not isinstance(spec, dict):
        return ['a spec is a mapping']
    problems = [f'unknown key {k!r}' for k in spec if k not in TOP_KEYS]
    if not spec.get('from'):
        problems.append('`from` names the fixture')
    if not isinstance(spec.get('steps'), list):
        problems.append('`steps` is a list')
    mods = spec.get('mods', [])
    if not isinstance(mods, list) or not all(re.fullmatch(r'\d+', str(m)) for m in mods):
        problems.append('`mods` is a list of Workshop IDs')
    options = spec.get('options', {})
    if not isinstance(options, dict) or not all(isinstance(v, dict) for v in options.values()):
        problems.append('`options` maps a static ID to {Field: value}')
    for key in ('require_log', 'mask_text'):
        if not isinstance(spec.get(key, []), list):
            problems.append(f'`{key}` is a list of regular expressions')
        else:
            for pattern in spec.get(key, []):
                try:
                    re.compile(pattern)
                except (re.error, TypeError) as e:
                    problems.append(f'`{key}` pattern {pattern!r}: {e}')
    for block in ('setup', 'steps'):
        for number, step in enumerate(spec.get(block) or [], 1):
            where = f'{block} {number}'
            if not isinstance(step, dict) or len(step) != 1:
                problems.append(f'{where}: a step is one {{kind: value}}')
                continue
            (kind, value), = step.items()
            problems += check_step(where, kind, value, block == 'setup')
    return problems


def load_spec(name):
    path = name if name.endswith('.yaml') else os.path.join(SPECS, name + '.yaml')
    import yaml
    with open(path) as f:
        spec = yaml.safe_load(f)
    problems = check_spec(spec)
    if problems:
        raise SpecError(f'{path}:\n  ' + '\n  '.join(problems))
    spec.setdefault('name', os.path.splitext(os.path.basename(path))[0])
    spec.setdefault('setup', [])
    spec['mods'] = [str(m) for m in spec.get('mods', [])]
    return spec


def needs_driver(spec):
    for step in (spec.get('setup') or []) + spec['steps']:
        (kind, value), = step.items()
        if kind in DRIVER_STEPS and not (kind == 'expect_state' and all(k.startswith('file:') for k in value)):
            return True
        if kind == 'shot' and isinstance(value, dict) and (value.get('crop') or value.get('mask')):
            return True
    return False


# ---------------------------------------------------------------- lanes and the machine lock

def lane_ports(lane):
    """OniMcp's and E2eDriver's ports of a lane (§4.4)."""
    return 8800 + 10 * lane, 8801 + 10 * lane


def port_open(port):
    with socket.socket() as s:
        s.settimeout(1)
        return s.connect_ex(('127.0.0.1', port)) == 0


def parse_displays(text):
    """--displays ":88,:89" (or "88,89") as display names."""
    displays = []
    for part in text.split(','):
        part = part.strip()
        if not re.fullmatch(r':?\d+', part):
            raise ValueError(f'bad display {part!r}')
        displays.append(part if part.startswith(':') else ':' + part)
    if len(set(displays)) != len(displays):
        raise ValueError('a display is named twice')
    return displays


class Lane:
    """A lane held by this process: its number, ports, display (None for xvfb-run -a) and lock."""

    def __init__(self, number, lock_file, display=None):
        self.number, self.lock_file, self.display = number, lock_file, display
        self.mcp_port, self.driver_port = lane_ports(number)

    def release(self):
        if self.lock_file:
            fcntl.flock(self.lock_file, fcntl.LOCK_UN)
            self.lock_file.close()
            self.lock_file = None


def try_lane(number, lock_dir, busy=port_open):
    """Lane NUMBER if no other runner holds it and its ports are free, else None."""
    os.makedirs(lock_dir, exist_ok=True)
    f = open(os.path.join(lock_dir, f'.lane-{number}.lock'), 'w')
    try:
        fcntl.flock(f, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        f.close()
        return None
    if any(busy(p) for p in lane_ports(number)):
        fcntl.flock(f, fcntl.LOCK_UN)
        f.close()
        return None
    return Lane(number, f)


def allocate_lane(first, lock_dir=OUT, busy=port_open, wait=True, log=say):
    """The first free lane from FIRST on (up to MAX_LANES lanes), waiting for one if WAIT."""
    announced = False
    while True:
        for number in range(first, first + MAX_LANES):
            lane = try_lane(number, lock_dir, busy)
            if lane:
                return lane
        if not wait:
            return None
        if not announced:
            log(f'waiting for a free lane ({first}..{first + MAX_LANES - 1})')
            announced = True
        time.sleep(5)


def perf_jobs(lines=None):
    """The ps lines of running perf jobs."""
    if lines is None:
        lines = subprocess.run(['ps', '-eo', 'args'], capture_output=True, text=True).stdout.splitlines()[1:]
    found = []
    for line in lines:
        args = re.sub(r'^(/usr/bin/env )?(ba)?sh ', '', line.strip())
        if any(re.search(p, args) for p in PERF_JOBS):
            found.append(line.strip())
    return found


@contextlib.contextmanager
def machine_lock(lock_dir=OUT, perf_wait=True, log=say):
    """Shared hold of out/e2e/.lock for a spec run, after any perf job has finished: a perf job
    that takes the lock exclusively, or one ps shows, keeps a spec from starting."""
    os.makedirs(lock_dir, exist_ok=True)
    f = open(os.path.join(lock_dir, '.lock'), 'w')
    announced = False
    try:
        while True:
            jobs = perf_jobs() if perf_wait else []
            if not jobs:
                try:
                    fcntl.flock(f, fcntl.LOCK_SH | fcntl.LOCK_NB)
                    break
                except OSError:
                    jobs = ['a perf job holds out/e2e/.lock']
            if not announced:
                log('waiting for a perf job: ' + jobs[0][:160])
                announced = True
            time.sleep(20)
        yield
    finally:
        f.close()


class JobQueue:
    """Spec runs in order; a job starts once the jobs it depends on are done (ours after the
    original's runs of the same spec, whose evidence it is compared with)."""

    def __init__(self, jobs):
        self.jobs = list(jobs)
        self.taken, self.finished = set(), set()
        self.condition = threading.Condition()

    def next(self):
        with self.condition:
            while True:
                pending = [j for j in self.jobs if j['id'] not in self.taken]
                if not pending:
                    return None
                for job in pending:
                    if all(d in self.finished for d in job['after']):
                        self.taken.add(job['id'])
                        return job
                self.condition.wait()

    def done(self, job):
        with self.condition:
            self.finished.add(job['id'])
            self.condition.notify_all()


def plan_jobs(specs, kinds, repeat):
    jobs = []
    for spec in specs:
        originals = []
        for kind in kinds:
            for index in range(1, repeat + 1):
                job = {'id': f'{spec["name"]}/{kind}-{index}', 'spec': spec, 'kind': kind, 'index': index,
                       'after': list(originals) if kind == 'ours' else []}
                if kind == 'original':
                    originals.append(job['id'])
                jobs.append(job)
    return jobs


# ---------------------------------------------------------------- OniMcp

class Mcp:
    """A minimal client for OniMcp's streamable HTTP endpoint (JSON text results)."""

    def __init__(self, port):
        self.url = f'http://127.0.0.1:{port}/mcp/'
        self.session = None

    def post(self, body):
        headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
        if self.session:
            headers['Mcp-Session-Id'] = self.session
        request = urllib.request.Request(self.url, data=json.dumps(body).encode(), headers=headers)
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


# ---------------------------------------------------------------- E2eDriver

class DriverError(SpecFailure):
    pass


class Element:
    """One ui.find match, normalised: path, rect (x, y, w, h; pixels, origin top-left), state, and
    the path of the top raycast hit at its centre."""

    def __init__(self, raw):
        self.raw = raw
        self.path = raw.get('path', '')
        self.rect = Element.parse_rect(raw.get('rect') if 'rect' in raw else raw.get('corners'))
        self.active = raw.get('active', True)
        self.interactable = raw.get('interactable')
        self.is_on = raw.get('isOn')
        self.text = raw.get('text')
        hit = raw.get('hit', raw.get('topHit', raw.get('raycast')))
        self.hit = hit.get('path') if isinstance(hit, dict) else hit

    @staticmethod
    def parse_rect(r):
        if r is None:
            return None
        if isinstance(r, dict):
            return (float(r['x']), float(r['y']), float(r.get('w', r.get('width'))), float(r.get('h', r.get('height'))))
        if len(r) == 4 and all(isinstance(v, (int, float)) for v in r):
            return tuple(float(v) for v in r)
        if len(r) == 4 and all(isinstance(v, (list, tuple)) for v in r):  # four corners
            xs, ys = [p[0] for p in r], [p[1] for p in r]
            return (min(xs), min(ys), max(xs) - min(xs), max(ys) - min(ys))
        raise DriverError(f'bad rect {r!r}')

    @property
    def center(self):
        x, y, w, h = self.rect
        return int(round(x + w / 2)), int(round(y + h / 2))

    def hit_is_self(self):
        """The top raycast hit is this element or one of its children."""
        if not self.hit:
            return False
        return self.hit == self.path or self.hit.startswith(self.path.rstrip('/') + '/')


class Driver:
    """E2eDriver's client (§4.2): JSON lines over TCP on 127.0.0.1:E2E_DRIVER_PORT, one request per
    line, handled on the game's main thread one per frame. A request is
    {"id": N, "cmd": "ui.find", "args": {...}}; a reply {"id": N, "ok": true, "result": ...} or
    {"id": N, "ok": false, "error": "..."}."""

    def __init__(self, port, host='127.0.0.1', timeout=60):
        self.address, self.timeout = (host, port), timeout
        self.sock = self.reader = None
        self.next_id = 0
        self.lock = threading.Lock()

    def connect(self, deadline=60, alive=lambda: True):
        end = time.time() + deadline
        while True:
            try:
                self.sock = socket.create_connection(self.address, timeout=self.timeout)
                self.reader = self.sock.makefile('r', encoding='utf-8', newline='\n')
                return self
            except OSError:
                if time.time() > end or not alive():
                    raise DriverError(f'E2eDriver is not listening on port {self.address[1]}')
                time.sleep(1)

    def close(self):
        if self.sock:
            with contextlib.suppress(OSError):
                self.reader.close()
                self.sock.close()
        self.sock = self.reader = None

    def request(self, cmd, **args):
        if not self.sock:
            raise DriverError('E2eDriver is not connected')
        with self.lock:
            self.next_id += 1
            ident = self.next_id
            line = json.dumps({'id': ident, 'cmd': cmd, 'args': args}) + '\n'
            try:
                self.sock.sendall(line.encode())
                while True:
                    reply = self.reader.readline()
                    if not reply:
                        raise DriverError(f'E2eDriver closed the connection during {cmd}')
                    reply = json.loads(reply)
                    if reply.get('id') in (ident, None):
                        break
            except (OSError, ValueError) as e:
                raise DriverError(f'E2eDriver {cmd}: {e}')
        if reply.get('ok') is False or reply.get('error'):
            raise DriverError(f'E2eDriver {cmd} {json.dumps(args)[:300]}: {reply.get("error")}')
        return reply.get('result')

    # The commands of §4.2.
    def find(self, path=None, text=None, type=None, under=None):
        query = {k: v for k, v in (('path', path), ('text', text), ('type', type), ('under', under)) if v is not None}
        result = self.request('ui.find', **query)
        items = result.get('elements', []) if isinstance(result, dict) else (result or [])
        return [Element(e) for e in items]

    def find_one(self, query, index=None):
        found = self.find(path=query.get('ui'), text=query.get('text'), type=query.get('type'), under=query.get('under'))
        found = [e for e in found if e.active is not False]
        what = query.get('ui') or f'text {query.get("text")!r}' + (f' under {query["under"]}' if query.get('under') else '')
        if not found:
            raise SpecFailure(f'no active element {what}')
        index = query.get('index', 0) if index is None else index
        if index >= len(found):
            raise SpecFailure(f'{what}: {len(found)} match(es), wanted index {index}')
        if found[index].rect is None:
            raise SpecFailure(f'{what}: E2eDriver gave no rect')
        return found[index]

    def dump(self, root):
        result = self.request('ui.dump', root=root)
        if isinstance(result, dict):
            result = result.get('text', result.get('lines', ''))
        return '\n'.join(result) + '\n' if isinstance(result, list) else str(result)

    def wait(self, path, cond='exists', frames=300):
        return self.request('ui.wait', path=path, cond=cond, frames=frames)

    def to_screen(self, cell):
        return point(self.request('world.to_screen', cell=list(cell)))

    def to_cell(self, x, y):
        return point(self.request('screen.to_cell', x=x, y=y))

    def state(self, expr):
        result = self.request('state.get', expr=expr)
        return result.get('value') if isinstance(result, dict) and set(result) <= {'value', 'type'} else result

    def camera_get(self):
        return self.request('camera.get')

    def camera_set(self, **args):
        return self.request('camera.set', **args)

    def canvas(self, hide):
        return self.request('canvas.hide' if hide else 'canvas.show')

    def setup_call(self, **args):
        return self.request('setup.call', **args)

    def seed(self, n):
        return self.request('seed', n=n)

    def frames(self, n):
        """Lets N frames pass: each request is answered in its own frame's Update."""
        for _ in range(n):
            self.request('camera.get')


def point(value):
    if isinstance(value, dict):
        return value['x'], value['y']
    return tuple(value[:2])


# ---------------------------------------------------------------- XTEST input

# KKeyCode names (the game's key enum) as X keysym names, US layout (Xvfb's default keymap).
KKEYCODES = {
    'Escape': 'Escape', 'Return': 'Return', 'Space': 'space', 'Tab': 'Tab', 'Backspace': 'BackSpace',
    'Delete': 'Delete', 'Insert': 'Insert', 'Home': 'Home', 'End': 'End', 'PageUp': 'Prior', 'PageDown': 'Next',
    'UpArrow': 'Up', 'DownArrow': 'Down', 'LeftArrow': 'Left', 'RightArrow': 'Right',
    'LeftShift': 'Shift_L', 'RightShift': 'Shift_R', 'LeftControl': 'Control_L', 'RightControl': 'Control_R',
    'LeftAlt': 'Alt_L', 'RightAlt': 'Alt_R', 'CapsLock': 'Caps_Lock', 'Numlock': 'Num_Lock',
    'ScrollLock': 'Scroll_Lock', 'Pause': 'Pause', 'Print': 'Print',
    'Comma': 'comma', 'Period': 'period', 'Slash': 'slash', 'Backslash': 'backslash', 'Semicolon': 'semicolon',
    'Quote': 'apostrophe', 'BackQuote': 'grave', 'LeftBracket': 'bracketleft', 'RightBracket': 'bracketright',
    'Minus': 'minus', 'Equals': 'equal',
    'KeypadPeriod': 'KP_Decimal', 'KeypadDivide': 'KP_Divide', 'KeypadMultiply': 'KP_Multiply',
    'KeypadMinus': 'KP_Subtract', 'KeypadPlus': 'KP_Add', 'KeypadEnter': 'KP_Enter', 'KeypadEquals': 'KP_Equal',
    **{chr(c): chr(c).lower() for c in range(ord('A'), ord('Z') + 1)},
    **{f'Alpha{d}': str(d) for d in range(10)},
    **{f'Keypad{d}': f'KP_{d}' for d in range(10)},
    **{f'F{n}': f'F{n}' for n in range(1, 16)},
}
MODIFIERS = {'shift': 'Shift_L', 'ctrl': 'Control_L', 'control': 'Control_L', 'alt': 'Alt_L'}
_KEYS_LOWER = {k.lower(): v for k, v in KKEYCODES.items()}
BUTTONS = {'left': 1, 'middle': 2, 'right': 3}


def keysym_name(name):
    """A KKeyCode name (or a letter, digit or modifier) as an X keysym name, or None."""
    name = str(name).strip()
    if re.fullmatch(r'\d', name):
        return name
    return MODIFIERS.get(name.lower()) or _KEYS_LOWER.get(name.lower())


def parse_key(code):
    """"Shift+B" as ([modifier keysyms], key keysym); a lone modifier is the key itself."""
    parts = [p for p in re.split(r'\s*\+\s*', str(code).strip()) if p]
    if not parts:
        raise SpecError(f'empty key {code!r}')
    names = []
    for part in parts:
        name = keysym_name(part)
        if name is None:
            raise SpecError(f'unknown key {part!r} in {code!r}')
        names.append(name)
    for part in parts[:-1]:
        if part.lower() not in MODIFIERS and keysym_name(part) not in MODIFIERS.values():
            raise SpecError(f'{part!r} in {code!r} is not a modifier (Shift, Ctrl, Alt)')
    return names[:-1], names[-1]


def key_problems(code):
    try:
        parse_key(code)
        return []
    except SpecError as e:
        return [str(e)]


_xauth = threading.Lock()


class Input:
    """Real input on a display through XTEST (ctypes on libXtst and libX11; nothing to install).
    FRAMES(n) lets n game frames pass (E2eDriver round trips, or a sleep)."""

    def __init__(self, display, xauthority=None, frames=None):
        self.x11 = x11 = ctypes.CDLL('libX11.so.6')
        self.xtst = xtst = ctypes.CDLL('libXtst.so.6')
        x11.XOpenDisplay.argtypes, x11.XOpenDisplay.restype = [ctypes.c_char_p], ctypes.c_void_p
        x11.XCloseDisplay.argtypes = [ctypes.c_void_p]
        x11.XFlush.argtypes = [ctypes.c_void_p]
        x11.XSync.argtypes = [ctypes.c_void_p, ctypes.c_int]
        x11.XStringToKeysym.argtypes, x11.XStringToKeysym.restype = [ctypes.c_char_p], ctypes.c_ulong
        x11.XKeysymToKeycode.argtypes, x11.XKeysymToKeycode.restype = [ctypes.c_void_p, ctypes.c_ulong], ctypes.c_ubyte
        x11.XkbKeycodeToKeysym.argtypes = [ctypes.c_void_p, ctypes.c_ubyte, ctypes.c_int, ctypes.c_int]
        x11.XkbKeycodeToKeysym.restype = ctypes.c_ulong
        xtst.XTestFakeMotionEvent.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_ulong]
        xtst.XTestFakeButtonEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_int, ctypes.c_ulong]
        xtst.XTestFakeKeyEvent.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_int, ctypes.c_ulong]
        # libX11 reads XAUTHORITY when it connects; lanes have their own (xvfb-run's), so set it
        # only around the connect.
        with _xauth:
            saved = os.environ.get('XAUTHORITY')
            if xauthority:
                os.environ['XAUTHORITY'] = xauthority
            try:
                self.dpy = x11.XOpenDisplay(display.encode())
            finally:
                if xauthority:
                    if saved is None:
                        os.environ.pop('XAUTHORITY', None)
                    else:
                        os.environ['XAUTHORITY'] = saved
        if not self.dpy:
            raise SpecFailure(f'cannot open display {display}')
        self.display = display
        self.frames = frames or (lambda n: time.sleep(n * FRAME_FALLBACK))
        self.held = []

    def close(self):
        if self.dpy:
            for keycode in reversed(self.held):
                self.xtst.XTestFakeKeyEvent(self.dpy, keycode, 0, 0)
            self.x11.XSync(self.dpy, 0)
            self.x11.XCloseDisplay(self.dpy)
            self.dpy = None

    def _flush(self):
        self.x11.XFlush(self.dpy)

    def keycode(self, name):
        keysym = self.x11.XStringToKeysym(name.encode())
        if not keysym and len(name) == 1:
            keysym = ord(name)  # Latin-1 keysyms are their code points
        keycode = self.x11.XKeysymToKeycode(self.dpy, keysym) if keysym else 0
        if not keycode:
            raise SpecFailure(f'no key for {name!r} on {self.display}')
        return keycode, keysym

    def move(self, x, y):
        self.xtst.XTestFakeMotionEvent(self.dpy, -1, int(x), int(y), 0)
        self._flush()

    def button(self, button, down):
        self.xtst.XTestFakeButtonEvent(self.dpy, BUTTONS.get(button, button), 1 if down else 0, 0)
        self._flush()

    def key_event(self, name, down):
        keycode, _ = self.keycode(name)
        self.xtst.XTestFakeKeyEvent(self.dpy, keycode, 1 if down else 0, 0)
        if down:
            self.held.append(keycode)
        elif keycode in self.held:
            self.held.remove(keycode)
        self._flush()

    @contextlib.contextmanager
    def modifiers(self, names):
        names = [keysym_name(n) or n for n in names or []]
        for n in names:
            self.key_event(n, True)
        if names:
            self.frames(2)
        try:
            yield
        finally:
            for n in reversed(names):
                self.key_event(n, False)
            if names:
                self.frames(2)

    def click(self, x, y, button='left', mods=()):
        """Move, hover two frames, press, two frames, release (§4.3)."""
        with self.modifiers(mods):
            self.move(x, y)
            self.frames(2)
            self.button(button, True)
            self.frames(2)
            self.button(button, False)
            self.frames(2)

    def key(self, code):
        mods, key = parse_key(code)
        with self.modifiers(mods):
            self.key_event(key, True)
            self.frames(2)
            self.key_event(key, False)
            self.frames(2)

    def type_text(self, text):
        for ch in text:
            name = {'\n': 'Return', '\t': 'Tab', ' ': 'space'}.get(ch, ch)
            keycode, keysym = self.keycode(name)
            # A character on a key's shifted level needs Shift held.
            shifted = self.x11.XkbKeycodeToKeysym(self.dpy, keycode, 0, 0) != keysym
            with self.modifiers(['Shift_L'] if shifted else []):
                self.xtst.XTestFakeKeyEvent(self.dpy, keycode, 1, 0)
                self._flush()
                self.frames(1)
                self.xtst.XTestFakeKeyEvent(self.dpy, keycode, 0, 0)
                self._flush()
                self.frames(1)

    def drag(self, start, end, button='left', mods=(), steps=8):
        """Press at START, move to END in STEPS motion events a frame apart, release."""
        with self.modifiers(mods):
            self.move(*start)
            self.frames(2)
            self.button(button, True)
            self.frames(2)
            for i in range(1, steps + 1):
                self.move(start[0] + (end[0] - start[0]) * i / steps, start[1] + (end[1] - start[1]) * i / steps)
                self.frames(1)
            self.frames(1)
            self.button(button, False)
            self.frames(2)

    def scroll(self, x, y, notches):
        """Wheel notches at x, y: positive up (zoom in), negative down."""
        self.move(x, y)
        self.frames(2)
        for _ in range(abs(notches)):
            b = 4 if notches > 0 else 5
            self.button(b, True)
            self.button(b, False)
            self.frames(1)


# ---------------------------------------------------------------- the player

def descendants(pid):
    children = {}
    for entry in os.listdir('/proc'):
        if entry.isdigit():
            try:
                with open(f'/proc/{entry}/stat') as f:
                    ppid = int(f.read().rsplit(')', 1)[1].split()[1])
            except (OSError, IndexError, ValueError):
                continue
            children.setdefault(ppid, []).append(int(entry))
    found, todo = [], [pid]
    while todo:
        p = todo.pop()
        for c in children.get(p, []):
            found.append(c)
            todo.append(c)
    return found


def process_environ(pid):
    try:
        with open(f'/proc/{pid}/environ', 'rb') as f:
            items = f.read().split(b'\0')
    except OSError:
        return {}
    return dict(i.decode(errors='replace').partition('=')[::2] for i in items if b'=' in i)


def display_ready(number):
    """Whether X server :NUMBER accepts connections: on its abstract socket (where Xvfb listens
    when /tmp/.X11-unix is not writable, as under WSLg) or its socket file."""
    for address in (f'\0/tmp/.X11-unix/X{number}', f'/tmp/.X11-unix/X{number}'):
        with socket.socket(socket.AF_UNIX) as s:
            try:
                s.connect(address)
                return True
            except OSError:
                pass
    return False


# xvfb-run -a picks the first free display, and two lanes starting at once may pick the same:
# starts are serialised, here and (through a lock file) across runners.
_xvfb_start = threading.Lock()


class Player:
    """One player process on its own display, with a scratch profile holding OniMcp, E2eDriver,
    the spec's mods and options, and a fixture."""

    def __init__(self, kind, out, env, fixture, lane, mods=(), options=None, driver=False):
        self.kind, self.out, self.env, self.lane = kind, out, env, lane
        self.home = os.path.join(out, 'home')
        self.profile = os.path.join(self.home, *PROFILE)
        self.log = os.path.join(out, 'player.log' if kind == 'original' else 'stdout.txt')
        self.save_dir = os.path.join(self.profile, 'save_files', 'e2e')
        self.fixture = os.path.join(self.save_dir, os.path.basename(fixture))
        self.display = self.xauthority = None
        self.process = self.xvfb = None
        shutil.rmtree(out, ignore_errors=True)
        os.makedirs(os.path.join(self.profile, 'mods', 'Local'))
        os.makedirs(self.save_dir)
        shutil.copy(fixture, self.fixture)
        self.mods = self.install_mods(list(mods), driver)
        self.option_files = write_options(self.profile, self.mods, options or {})
        # OniMcp's options: the lane's port, JSON tool text, and none of its own gameplay policies
        # (it turns the global auto-disinfect off and dismisses tutorial notifications by default).
        with open(os.path.join(self.profile, 'OniMcpConfig.json'), 'w') as f:
            json.dump({'SecurityMigrationVersion': 1, 'Host': 'localhost', 'Port': lane.mcp_port, 'AuthEnabled': False,
                       'GlobalAutoDisinfectDisabled': False, 'AutoDismissTutorialNotifications': False,
                       'ToolTextFormat': 'json'}, f)
        with open(os.path.join(self.profile, 'kplayerprefs.yaml'), 'w') as f:
            f.write(f'strings: {{}}\nints:\n  ResolutionWidth: {WIDTH}\n  ResolutionHeight: {HEIGHT}\n'
                    '  RefreshRateNumerator: 60\n  RefreshRateDenominator: 1\n  FullScreen: 0\nfloats: {}\n')

    def install_mods(self, ids, driver):
        """The spec's Workshop mods as Local mods (ws-mods.py profile), then OniMcp and E2eDriver;
        returns {folder: staticID}."""
        mods_dir = os.path.join(self.profile, 'mods')
        entries = []
        if ids:
            ws = ws_mods()
            ws.profile(mods_dir, ids)
            with open(os.path.join(mods_dir, 'mods.json')) as f:
                entries = json.load(f)['mods']
            hash_of = ws.mono_hash
        else:
            hash_of = None
        own = [('OniMcp', self.env['E2E_ONIMCP'])]
        if driver:
            own.append(('E2eDriver', self.env['E2E_DRIVER']))
        for folder, source in own:
            dest = os.path.join(mods_dir, 'Local', folder)
            shutil.copytree(source, dest)
            if folder == 'OniMcp' and self.kind == 'original':
                shutil.copy(VALUETUPLE, dest)
            static = mod_yaml_value(dest, 'staticID') or folder
            if hash_of is None:
                hash_of = ws_mods().mono_hash
            # A local mod's version is its folder name's Mono hash, which KMod recomputes.
            entries.append({'label': {'distribution_platform': 0, 'id': folder, 'title': folder,
                                      'version': hash_of(folder)},
                            'status': 1, 'enabled': True, 'enabledForDlc': ['', 'EXPANSION1_ID'], 'crash_count': 0,
                            'reinstall_path': None, 'staticID': static})
        with open(os.path.join(mods_dir, 'mods.json'), 'w') as f:
            json.dump({'version': 1, 'mod_load_in_progress': False, 'mods': entries}, f)
        return {e['label']['id']: e['staticID'] for e in entries}

    def start(self):
        for port in (self.lane.mcp_port, self.lane.driver_port):
            if port_open(port):
                raise SpecFailure(f'port {port} is already in use (another session?)')
        size = ['-screen-width', str(WIDTH), '-screen-height', str(HEIGHT), '-screen-fullscreen', '0']
        env = dict(os.environ, XDG_CONFIG_HOME=os.path.join(self.home, 'config'),
                   XDG_CACHE_HOME=os.path.join(self.home, 'cache'),
                   E2E_DRIVER_PORT=str(self.lane.driver_port), E2E_LANE=str(self.lane.number))
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
        # The runner's own display (a desktop's) must reach neither the player nor xvfb-run's
        # server, whose environment find_display reads: input would land on the desktop.
        env.pop('DISPLAY', None)
        env.pop('XAUTHORITY', None)
        if self.lane.display:
            self.start_xvfb(self.lane.display)
            env['DISPLAY'] = self.display = self.lane.display
            self.process = subprocess.Popen(command, env=env, stdout=open(stdout, 'w'), stderr=subprocess.STDOUT,
                                            start_new_session=True)
        else:
            command = ['xvfb-run', '-a', '-s', f'-screen 0 {WIDTH}x{HEIGHT}x24', *command]
            os.makedirs(OUT, exist_ok=True)
            with _xvfb_start, open(os.path.join(OUT, '.xvfb.lock'), 'w') as lock:
                fcntl.flock(lock, fcntl.LOCK_EX)
                self.process = subprocess.Popen(command, env=env, stdout=open(stdout, 'w'), stderr=subprocess.STDOUT,
                                                start_new_session=True)
                self.find_display()
        with open(os.path.join(self.out, 'lane.json'), 'w') as f:
            json.dump(self.lane_record(), f)
        deadline = time.time() + 900
        while not port_open(self.lane.mcp_port):
            if self.process.poll() is not None:
                raise SpecFailure(f'the player exited ({self.process.returncode}) before OniMcp started')
            if time.time() > deadline:
                raise SpecFailure('OniMcp did not start within 15 minutes')
            time.sleep(3)

    def lane_record(self):
        return {'lane': self.lane.number, 'onimcp_port': self.lane.mcp_port, 'driver_port': self.lane.driver_port,
                'display': self.display}

    def start_xvfb(self, display):
        number = display.lstrip(':')
        if os.path.exists(f'/tmp/.X{number}-lock') or os.path.exists(f'/tmp/.X11-unix/X{number}'):
            raise SpecFailure(f'display {display} is in use')
        self.xvfb = subprocess.Popen(['Xvfb', display, '-screen', '0', f'{WIDTH}x{HEIGHT}x24', '-nolisten', 'tcp'],
                                     stdout=subprocess.DEVNULL, stderr=open(os.path.join(self.out, 'xvfb.txt'), 'w'),
                                     start_new_session=True)
        deadline = time.time() + 20
        while not display_ready(number):
            if self.xvfb.poll() is not None or time.time() > deadline:
                raise SpecFailure(f'Xvfb {display} did not start')
            time.sleep(0.2)

    def find_display(self):
        """The display and X authority file xvfb-run gave the player (from its environment)."""
        deadline = time.time() + 60
        while time.time() < deadline:
            for pid in descendants(self.process.pid):
                environ = process_environ(pid)
                if environ.get('DISPLAY') and environ.get('XAUTHORITY'):  # what xvfb-run gives its command
                    self.display, self.xauthority = environ['DISPLAY'], environ.get('XAUTHORITY')
                    number = self.display.lstrip(':').split('.')[0]
                    while not display_ready(number) and time.time() < deadline:
                        time.sleep(0.2)
                    return
            if self.process.poll() is not None:
                raise SpecFailure(f'xvfb-run exited ({self.process.returncode})')
            time.sleep(0.2)
        raise SpecFailure('xvfb-run started no player with a display')

    def alive(self):
        return self.process is not None and self.process.poll() is None

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
        for process in (self.process, self.xvfb):
            if process and process.poll() is None:
                os.killpg(process.pid, signal.SIGTERM)
                try:
                    process.wait(30)
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait()
        deadline = time.time() + 30
        while (port_open(self.lane.mcp_port) or port_open(self.lane.driver_port)) and time.time() < deadline:
            time.sleep(1)

    def signatures(self):
        if not os.path.exists(self.log):
            return set()
        result = subprocess.run([sys.executable, os.path.join(ROOT, 'scripts', 'qa-tools.py'), 'signatures', self.log],
                                capture_output=True, text=True)
        return set(result.stdout.splitlines())


def mod_yaml_value(folder, key):
    path = os.path.join(folder, 'mod.yaml')
    if not os.path.exists(path):
        return None
    with open(path, encoding='utf-8-sig', errors='replace') as f:
        match = re.search(rf'^{key}:\s*["\']?(.*?)["\']?\s*$', f.read(), re.M)
    return match.group(1) if match else None


def write_options(profile, mods, options):
    """A mod's PLib options before launch: OPTIONS maps a static ID (or Workshop ID) to fields,
    merged into config.json where POptions reads it, beside the mod's assembly (every folder of
    the mod holding a DLL, as KMod may load an archived version) and in the shared location
    mods/config/ASSEMBLY/. Returns the files written."""
    written = []
    mods_dir = os.path.join(profile, 'mods')
    for key, fields in options.items():
        folder = next((f for f, static in mods.items() if key in (f, static)), None)
        if folder is None:
            raise SpecFailure(f'options for {key!r}, which is not among the spec\'s mods')
        root = os.path.join(mods_dir, 'Local', folder)
        targets = set()
        for directory, _, files in os.walk(root):
            dlls = [f for f in files if f.lower().endswith('.dll')]
            if dlls:
                targets.add(os.path.join(directory, 'config.json'))
                for dll in dlls:
                    targets.add(os.path.join(mods_dir, 'config', os.path.splitext(dll)[0], 'config.json'))
        for target in sorted(targets):
            data = {}
            if os.path.exists(target):
                with contextlib.suppress(ValueError), open(target, encoding='utf-8-sig') as f:
                    data = json.load(f)
            data.update(fields)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            with open(target, 'w') as f:
                json.dump(data, f, indent=2)
            written.append(target)
    return written


# ---------------------------------------------------------------- orders and expectations (OniMcp)

def compare(actual, wanted):
    if isinstance(wanted, bool) or isinstance(actual, bool):
        return actual == wanted
    if isinstance(wanted, (int, float)):
        return actual == wanted
    match = re.fullmatch(r'\s*(>=|<=|>|<|==|!=)?\s*(-?\d+(?:\.\d+)?)\s*', str(wanted))
    if not match:
        raise SpecFailure(f'bad expectation {wanted!r}')
    op, number = match.group(1) or '==', float(match.group(2))
    return apply_op(op, actual, number)


def apply_op(op, actual, number):
    if not isinstance(actual, (int, float)) or isinstance(actual, bool):
        return {'==': actual == number, '!=': actual != number}.get(op, False)
    return {'>=': actual >= number, '<=': actual <= number, '>': actual > number,
            '<': actual < number, '==': actual == number, '!=': actual != number}[op]


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
    """A full-frame screenshot through OniMcp, as OUT/NAME.png; returns its path or None."""
    result = mcp.call('navigation_control', action='screenshot', filename=f'e2e-{name}', waitFrames=5)
    path = (result.get('path') or result.get('file')) if isinstance(result, dict) else None
    if path and os.path.exists(path):
        dest = os.path.join(out, name + os.path.splitext(path)[1])
        shutil.copy(path, dest)
        return dest
    return None


# ---------------------------------------------------------------- UI and state expectations

def ui_mismatches(element, wanted):
    """How a found element (or None) differs from {text, active, interactable, isOn, exists}."""
    misses = []
    if 'exists' in wanted and bool(wanted['exists']) != (element is not None):
        return [f'exists is {element is not None}, wanted {wanted["exists"]}']
    if element is None:
        return ['no such element'] if wanted.get('exists', True) else []
    for field, have in (('active', element.active), ('interactable', element.interactable), ('isOn', element.is_on)):
        if field in wanted and have != wanted[field]:
            misses.append(f'{field} is {have}, wanted {wanted[field]}')
    if 'text' in wanted and not text_matches(element.text, wanted['text']):
        misses.append(f'text is {element.text!r}, wanted {wanted["text"]!r}')
    return misses


def text_matches(have, want):
    """WANT is exact text, "~part" (contained) or "/regex/"."""
    have = '' if have is None else str(have)
    want = str(want)
    if want.startswith('~'):
        return want[1:] in have
    if len(want) > 1 and want.startswith('/') and want.endswith('/'):
        return re.search(want[1:-1], have) is not None
    return have == want


RELATION = re.compile(r'\s*(>=|<=|>|<|==|!=)\s*(?:(-?\d+(?:\.\d+)?)\s*\*\s*)?([A-Za-z_]\w*|-?\d+(?:\.\d+)?)\s*')


def state_check(have, wanted, saved, original=None):
    """Whether a state.get value meets WANTED: a value, "OP N", "OP [F *] NAME" (a value saved
    with save_as, or `original`: the original player's value at this step), exists / missing,
    or {contains: X}, {equals: X}, {save_as: NAME}. Returns (ok, deferred): deferred when it
    refers to the original and no original value is known yet."""
    if isinstance(wanted, dict):
        ok = True
        if 'save_as' in wanted:
            saved[wanted['save_as']] = have
        if 'contains' in wanted:
            ok &= have is not None and wanted['contains'] in (have if isinstance(have, (list, str, dict)) else str(have))
        if 'equals' in wanted:
            ok &= have == wanted['equals']
        return ok, False
    if wanted == 'exists':
        return have is not None, False
    if wanted == 'missing':
        return have is None, False
    if isinstance(wanted, str):
        match = RELATION.fullmatch(wanted)
        if match:
            op, factor, operand = match.group(1), match.group(2), match.group(3)
            if re.fullmatch(r'-?\d+(?:\.\d+)?', operand):
                reference = float(operand)
            elif operand == 'original':
                if original is None:
                    return True, True
                reference = original
            elif operand in saved:
                reference = saved[operand]
            else:
                raise SpecFailure(f'{wanted!r}: no value saved as {operand!r}')
            if factor is not None:
                reference = float(factor) * reference
            return apply_op(op, have, reference), False
    if isinstance(wanted, bool) or isinstance(have, bool):
        return type(have) is type(wanted) and have == wanted, False  # 1 is not True
    return have == wanted, False


# ---------------------------------------------------------------- one spec run

class Run:
    """One spec in one player: the step interpreter."""

    def __init__(self, spec, kind, index, env, lane, log, perf_wait=True, original_values=None):
        self.spec, self.kind, self.index, self.env, self.lane, self.log = spec, kind, index, env, lane, log
        self.out = os.path.join(OUT, spec['name'], f'{kind}-{index}')
        self.perf_wait = perf_wait
        self.mcp = Mcp(lane.mcp_port)
        self.driver = self.input = None
        self.saves, self.values, self.saved, self.evidence = [], [], {}, {'ui_dump': [], 'shot': [], 'world_shot': []}
        self.original_values = original_values or {}
        self.player = None

    # -- helpers
    def need_driver(self):
        if not self.driver:
            raise SpecFailure('this step needs E2eDriver (E2E_DRIVER)')
        return self.driver

    def need_input(self):
        if not self.input:
            if not self.player.display or self.player.display == os.environ.get('DISPLAY'):
                raise SpecFailure(f'no lane display for input (got {self.player.display}; never the runner\'s own)')
            self.input = Input(self.player.display, self.player.xauthority,
                               frames=self.driver.frames if self.driver else None)
        return self.input

    def frames(self, n):
        if self.driver:
            self.driver.frames(n)
        else:
            time.sleep(n * FRAME_FALLBACK)

    def element(self, query):
        """The element QUERY names, which must not be covered by another graphic."""
        element = self.need_driver().find_one(query)
        if not element.hit_is_self():
            raise SpecFailure(f'{element.path} is occluded by {element.hit or "nothing (no raycast hit)"}')
        return element

    def click_element(self, query, mods=(), button='left'):
        element = self.element(query)
        self.need_input().click(*element.center, button=button, mods=mods)
        return element

    def pointer_away(self):
        """The pointer off the UI before a shot (§5.4)."""
        if self.player and self.player.display:
            self.need_input().move(WIDTH - 1, HEIGHT - 1)
            self.frames(2)

    def rects(self, paths):
        rects = []
        for path in paths:
            if _rect(path):
                rects.append([float(v) for v in path])
            else:
                rects += [list(e.rect) for e in self.need_driver().find(path=path) if e.rect and e.active is not False]
        return rects

    # -- steps
    def step(self, kind, value, number):
        mcp = self.mcp
        if kind == 'dig':
            dig(mcp, value)
        elif kind == 'build':
            cells = value.get('cells') or [value['at']]
            build(mcp, value['prefab'], cells, value.get('priority', 5), value.get('material'))
        elif kind == 'run':
            spent = run(mcp, value['seconds'], value.get('until'), self.log)
            self.log(f'  run: {spent:.0f} game seconds')
        elif kind == 'expect':
            misses = unmet(mcp, value)
            if misses:
                raise SpecFailure('; '.join(misses))
        elif kind == 'call':
            mcp.call(value['tool'], **value.get('args', {}))
        elif kind == 'save':
            saved = mcp.call('game_control', domain='save', action='save', name=value, overwrite=True,
                             updateActiveSave=False, confirm=True)
            path = saved.get('saved') if isinstance(saved, dict) else None
            if not path or not os.path.exists(path):
                raise SpecFailure(f'save {value} failed: {json.dumps(saved)[:600]}')
            shutil.copy(path, os.path.join(self.out, value + '.sav'))
            self.saves.append(value)
        elif kind == 'shot':
            self.shot(value if isinstance(value, dict) else {'name': value})
        elif kind == 'world_shot':
            self.world_shot(value)
        elif kind == 'open':
            self.open(value)
        elif kind == 'key':
            self.need_input().key(value['code'])
        elif kind == 'click':
            self.click_element(value, value.get('mods', ()), value.get('button', 'left'))
        elif kind == 'click_world':
            x, y = self.need_driver().to_screen(value['cell'])
            self.need_input().click(x, y, button=value.get('button', 'left'), mods=value.get('mods', ()))
        elif kind == 'toggle':
            element = self.element(value)
            if element.is_on is None:
                raise SpecFailure(f'{element.path} is not a toggle (no isOn)')
            if element.is_on != value['to']:
                self.need_input().click(*element.center)
                self.frames(2)
                after = self.driver.find(path=element.path)
                if not after or after[0].is_on != value['to']:
                    raise SpecFailure(f'{element.path} isOn stayed {element.is_on} after a click')
        elif kind == 'dropdown':
            self.dropdown(value)
        elif kind == 'type':
            # `text` is what to type here: the field is found by its path alone.
            self.click_element({k: value[k] for k in ('ui', 'under', 'index') if k in value})
            if value.get('clear'):
                self.input.key('Ctrl+A')
                self.input.key('Backspace')
            self.input.type_text(value['text'])
            if value.get('enter'):
                self.input.key('Return')
        elif kind == 'drag':
            driver = self.need_driver()
            start, end = driver.to_screen(value['from']), driver.to_screen(value['to'])
            self.need_input().drag(start, end, button=value.get('button', 'left'), mods=value.get('mods', ()),
                                   steps=value.get('steps', 8))
        elif kind == 'scroll':
            at = value.get('at', 'center')
            if at == 'center':
                x, y = WIDTH // 2, HEIGHT // 2
            elif _cell(at):
                x, y = self.need_driver().to_screen(at)
            else:
                x, y = self.element({'ui': at}).center
            self.need_input().scroll(x, y, value['notches'])
        elif kind == 'tool':
            self.tool(value)
        elif kind == 'expect_ui':
            self.expect_ui(value)
        elif kind == 'expect_state':
            self.expect_state(value, number)
        elif kind == 'ui_wait':
            self.need_driver().wait(value['path'], value.get('cond', 'exists'), value.get('frames', 300))
        elif kind == 'ui_dump':
            text = self.need_driver().dump(value['root'])
            path = os.path.join(self.out, value['name'] + '.ui.txt')
            with open(path, 'w') as f:
                f.write(text)
            self.evidence['ui_dump'].append({'name': value['name'], 'root': value['root'],
                                             'mask_text': value.get('mask_text', [])})
        elif kind == 'driver':
            (command, args), = value.items()
            args = args if isinstance(args, dict) else {'n': args}
            self.need_driver().request(command, **args)
        else:
            raise SpecFailure(f'unknown step {kind!r}')

    def open(self, value):
        if 'key' in value:
            self.need_input().key(value['key'])
        elif 'management' in value:
            self.mcp.call('game_control', domain='ui', uiDomain='action', action='open_management',
                          screen=value['management'], confirm=True)
        else:
            # A user-menu button by its OniMcp action key (… surface=user_menu action=list), on the
            # object at a cell or with an ID.
            target = {'x': value['at'][0], 'y': value['at'][1]} if 'at' in value else \
                ({'id': value['id']} if 'id' in value else {})
            self.mcp.call('building_control', domain='side_surface', surface='user_menu', action='press',
                          actionKey=value['user_menu'], confirm=True, **target)
        self.frames(5)

    def tool(self, value):
        if value.get('key'):
            self.need_input().key(value['key'])
        elif value.get('ui'):
            self.click_element(value)
        else:
            # A ToolMenu entry by its tool's name: the toolbar button whose path ends in it, else
            # whose text is it.
            name = value['toolbar']
            driver = self.need_driver()
            found = driver.find(path=f'**/ToolMenu/**/{name}') or driver.find(text=name, under='**/ToolMenu')
            if not found:
                raise SpecFailure(f'no toolbar entry {name!r}')
            self.click_element({'ui': found[0].path})
        self.frames(5)

    def dropdown(self, value):
        element = self.click_element(value)
        # Unity's (TMP_)Dropdown opens its list as a child "Dropdown List" of the dropdown.
        option = {'text': str(value['option']), 'under': element.path}
        for _ in range(30):
            if any(e.active is not False for e in self.driver.find(**option)):
                break
            self.frames(2)
        self.click_element(option)
        self.frames(2)
        after = self.driver.find(path=element.path)
        if after and after[0].text is not None and str(value['option']) not in str(after[0].text):
            raise SpecFailure(f'{element.path} shows {after[0].text!r} after choosing {value["option"]!r}')

    def expect_ui(self, value, tries=10):
        for attempt in range(tries):
            misses = []
            for path, wanted in value.items():
                found = [e for e in self.need_driver().find(path=path)]
                active = [e for e in found if e.active is not False]
                element = (active or found or [None])[0]
                misses += [f'{path}: {m}' for m in ui_mismatches(element, wanted)]
            if not misses:
                return
            self.frames(3)
        raise SpecFailure('; '.join(misses))

    def state_value(self, expr):
        if expr.startswith('file:'):
            path = os.path.join(self.player.profile, expr[5:])
            if not os.path.exists(path):
                return None
            with open(path, encoding='utf-8', errors='replace') as f:
                return f.read()
        return self.need_driver().state(expr)

    def expect_state(self, value, number):
        misses = []
        for expr, wanted in value.items():
            have = self.state_value(expr)
            original = self.original_values.get(f'{number}:{expr}')
            ok, deferred = state_check(have, wanted, self.saved, original)
            self.values.append({'step': number, 'expr': expr, 'value': have, 'wanted': wanted, 'deferred': deferred})
            if not ok:
                misses.append(f'{expr} is {json.dumps(have)[:200]}, wanted {wanted}')
        if misses:
            raise SpecFailure('; '.join(misses))

    def shot(self, value):
        name = value['name']
        self.pointer_away()
        meta = {'name': name, 'crop': None, 'masks': [], 'threshold': value.get('threshold')}
        if value.get('crop'):
            meta['crop'] = list(self.need_driver().find_one({'ui': value['crop']}).rect)
        if value.get('mask'):
            meta['masks'] = self.rects(value['mask'])
        path = screenshot(self.mcp, self.out, name)
        if not path:
            raise SpecFailure(f'shot {name}: OniMcp made no screenshot')
        meta['file'] = os.path.basename(path)
        with open(os.path.join(self.out, name + '.shot.json'), 'w') as f:
            json.dump(meta, f)
        self.evidence['shot'].append(meta)

    def world_shot(self, value):
        name = value['name']
        driver = self.need_driver()
        camera = {}
        if 'at' in value:
            camera['pos'] = [value['at'][0] + 0.5, value['at'][1] + 0.5]  # a cell's centre in world units
        if 'ortho' in value:
            camera['ortho'] = value['ortho']
        if camera:
            driver.camera_set(**camera)
        self.pointer_away()
        masks = self.rects([m for m in value.get('mask', []) if not _rect(m)]) + \
            [[float(v) for v in m] for m in value.get('mask', []) if _rect(m)]
        hide = value.get('hide_ui', True)
        if hide:
            driver.canvas(True)
            self.frames(2)
        try:
            path = screenshot(self.mcp, self.out, name)
        finally:
            if hide:
                driver.canvas(False)
        if not path:
            raise SpecFailure(f'world_shot {name}: OniMcp made no screenshot')
        meta = {'name': name, 'crop': None, 'masks': masks, 'threshold': value.get('threshold'),
                'file': os.path.basename(path), 'world': True}
        with open(os.path.join(self.out, name + '.shot.json'), 'w') as f:
            json.dump(meta, f)
        self.evidence['world_shot'].append(meta)

    # -- the run
    def execute(self):
        spec = self.spec
        fixture = os.path.join(FIXTURES, spec['from'] + '.sav')
        uses_driver = needs_driver(spec)
        outcome, label, failed_kind = None, 'start', None
        self.player = None
        records = None
        try:
            if uses_driver and not self.env.get('E2E_DRIVER'):
                raise SpecFailure('the spec needs E2eDriver, and E2E_DRIVER names no build of it')
            if not os.path.exists(fixture):
                raise SpecFailure(f'no fixture {fixture}')
            self.player = Player(self.kind, self.out, self.env, fixture, self.lane, spec['mods'], spec.get('options'),
                                 driver=uses_driver)
            records = open(os.path.join(self.out, 'results.jsonl'), 'w')
            started = time.time()
            self.player.start()
            self.log(f'  lane {self.lane.number}: OniMcp :{self.lane.mcp_port}, E2eDriver :{self.lane.driver_port}, '
                     f'display {self.player.display}')
            self.player.load(self.mcp)
            self.log(f'  loaded {spec["from"]} in {time.time() - started:.0f} s')
            if uses_driver:
                self.driver = Driver(self.lane.driver_port).connect(120, self.player.alive)
            steps = [('setup', i, s) for i, s in enumerate(spec['setup'], 1)] + \
                    [('steps', i, s) for i, s in enumerate(spec['steps'], 1)]
            for block, number, step in steps:
                (kind_of_step, value), = step.items()
                failed_kind = kind_of_step
                label = number if block == 'steps' else f'setup {number}'
                began = time.time()
                self.step(kind_of_step, value, label)
                records.write(json.dumps({'step': label, kind_of_step: value, 'wall': round(time.time() - began, 1)}) + '\n')
                records.flush()
            failed_kind = None
            label = 'end'
            screenshot(self.mcp, self.out, 'end')
            if not self.player.alive():
                raise SpecFailure('the player is not alive at the end')
            errors = driver_errors(self.player.log) if uses_driver else []
            if errors:
                raise SpecFailure(f'E2eDriver logged {len(errors)} error(s): {errors[0][:300]}')
        except (SpecFailure, urllib.error.URLError, ConnectionError, TimeoutError, OSError) as e:
            outcome = f'step {label}: {e}'
            with contextlib.suppress(Exception):
                screenshot(self.mcp, self.out, 'failure')
        finally:
            if self.input:
                with contextlib.suppress(Exception):
                    self.input.close()
            if self.driver:
                self.driver.close()
            if self.player:
                self.player.stop()
            if records:
                records.close()
        if os.path.isdir(self.out):
            with open(os.path.join(self.out, 'values.json'), 'w') as f:
                json.dump(self.values, f, indent=1, default=str)
        return {'outcome': outcome, 'failed_step_kind': failed_kind,
                'signatures': self.player.signatures() if self.player else set(),
                'saves': self.saves, 'out': self.out, 'values': self.values, 'evidence': self.evidence,
                'lane': self.player.lane_record() if self.player else {'lane': self.lane.number},
                'options_written': self.player.option_files if self.player else [],
                'log': self.player.log if self.player else None}


def driver_errors(log):
    """E2eDriver's own error lines in a player's log (§5.2)."""
    if not log or not os.path.exists(log):
        return []
    with open(log, encoding='utf-8', errors='replace') as f:
        return [l.strip() for l in f if 'E2eDriver' in l and ('[ERROR]' in l or re.search(r'\b\w+Exception\b', l))]


# ---------------------------------------------------------------- differential checks (§5.3)

def log_matches(log, patterns):
    if not patterns:
        return {}
    text = open(log, encoding='utf-8', errors='replace').read() if log and os.path.exists(log) else ''
    return {p: re.search(p, text, re.M) is not None for p in patterns}


class Reference:
    """The original's evidence for a spec: from this invocation's original runs, or blessed into
    out/e2e-refs/SPEC."""

    def __init__(self, folder, signatures, log_found, values, source):
        self.folder, self.signatures, self.log_found, self.values, self.source = folder, signatures, log_found, values, source

    @staticmethod
    def from_runs(spec, results):
        passed = [r for r in results if not r['outcome']]
        if not passed:
            return None
        first = passed[0]
        signatures = set().union(*(r['signatures'] for r in results))
        return Reference(first['out'], signatures, log_matches(first['log'], spec.get('require_log', [])),
                         values_by_step(first['values']), 'original run')

    @staticmethod
    def blessed(spec):
        folder = os.path.join(REFS, spec['name'])
        if not os.path.isdir(folder):
            return None
        read = lambda name, default: json.load(open(os.path.join(folder, name))) if os.path.exists(os.path.join(folder, name)) else default
        return Reference(folder, set(read('signatures.json', [])), read('log.json', {}),
                         values_by_step(read('values.json', [])), folder)


def values_by_step(values):
    return {f'{v["step"]}:{v["expr"]}': v['value'] for v in values}


def bless(spec, result):
    """An original run's evidence into out/e2e-refs/SPEC."""
    folder = os.path.join(REFS, spec['name'])
    shutil.rmtree(folder, ignore_errors=True)
    os.makedirs(folder)
    for name in os.listdir(result['out']):
        if name.endswith(('.ui.txt', '.shot.json', '.png', '.jpg')):
            shutil.copy(os.path.join(result['out'], name), folder)
    json.dump(sorted(result['signatures']), open(os.path.join(folder, 'signatures.json'), 'w'), indent=1)
    json.dump(log_matches(result['log'], spec.get('require_log', [])), open(os.path.join(folder, 'log.json'), 'w'), indent=1)
    json.dump(result['values'], open(os.path.join(folder, 'values.json'), 'w'), indent=1, default=str)
    return folder


def differential(spec, result, reference):
    """Ours against the original's evidence: {check: details}, and the reasons it fails."""
    checks, reasons = {}, []
    new = sorted(result['signatures'] - reference.signatures)
    checks['signatures'] = {'new': new, 'reference': len(reference.signatures)}
    if new:
        reasons.append(f'{len(new)} log signature(s) the original lacks')
    ours_log = log_matches(result['log'], spec.get('require_log', []))
    missing = [p for p, found in reference.log_found.items() if found and not ours_log.get(p)]
    checks['require_log'] = {'missing': missing, 'ours': ours_log, 'original': reference.log_found}
    if missing:
        reasons.append('required log lines missing: ' + ', '.join(missing))
    ui_diff = load_script('ui-diff.py')
    tolerance = spec.get('tolerance', UI_TOLERANCE)
    checks['ui_dump'] = {}
    for dump in result['evidence']['ui_dump']:
        name = dump['name']
        a, b = os.path.join(reference.folder, name + '.ui.txt'), os.path.join(result['out'], name + '.ui.txt')
        if not os.path.exists(a):
            checks['ui_dump'][name] = {'ok': None, 'note': 'no reference dump'}
            continue
        masks = list(spec.get('mask_text', [])) + list(dump.get('mask_text', []))
        differences = ui_diff.diff(ui_diff.parse(a), ui_diff.parse(b), tolerance=tolerance, mask_text=masks)
        checks['ui_dump'][name] = {'ok': not differences, 'differences': len(differences), 'first': differences[:20]}
        if differences:
            reasons.append(f'ui_dump {name}: {len(differences)} difference(s)')
    qa_tools = load_script('qa-tools.py')
    thresholds = spec.get('thresholds', {})
    for kind, default in (('shot', SHOT_THRESHOLD), ('world_shot', WORLD_THRESHOLD)):
        checks[kind] = {}
        for meta in result['evidence'][kind]:
            name = meta['name']
            ref_meta_path = os.path.join(reference.folder, name + '.shot.json')
            if not os.path.exists(ref_meta_path):
                checks[kind][name] = {'ok': None, 'note': 'no reference shot'}
                continue
            ref_meta = json.load(open(ref_meta_path))
            crop_a, crop_b = ref_meta.get('crop'), meta.get('crop')
            masks = relative_masks(ref_meta.get('masks', []), crop_a) + relative_masks(meta.get('masks', []), crop_b)
            diff_png = os.path.join(result['out'], name + '.diff.png')
            compared = qa_tools.compare_images(os.path.join(reference.folder, ref_meta['file']),
                                               os.path.join(result['out'], meta['file']), diff_png,
                                               crop=crop_a, crop_new=crop_b, masks=masks)
            threshold = meta.get('threshold') or thresholds.get(kind) or default
            ok = compared['fraction'] <= threshold
            checks[kind][name] = dict(compared, threshold=threshold, ok=ok, diff=diff_png)
            if not ok:
                reasons.append(f'{kind} {name}: {compared["fraction"]:.2%} of pixels differ (> {threshold:.0%})')
    deferred = []
    for v in result['values']:
        if not v.get('deferred'):
            continue
        original = reference.values.get(f'{v["step"]}:{v["expr"]}')
        if original is None:
            deferred.append({'step': v['step'], 'expr': v['expr'], 'ok': None, 'note': 'no original value'})
            continue
        ok, _ = state_check(v['value'], v['wanted'], {}, original)
        deferred.append({'step': v['step'], 'expr': v['expr'], 'ours': v['value'], 'original': original, 'ok': ok})
        if not ok:
            reasons.append(f'step {v["step"]}: {v["expr"]} is {v["value"]}, wanted {v["wanted"]} ({original} in the original)')
    checks['expect_original'] = deferred
    return checks, reasons


def relative_masks(masks, crop):
    if not crop:
        return [list(m) for m in masks]
    return [[m[0] - crop[0], m[1] - crop[1], m[2], m[3]] for m in masks]


# ---------------------------------------------------------------- main

def usage():
    sys.exit(__doc__)


def parse_args(argv):
    opts = {'players': 'ours', 'repeat': 1, 'bless': False, 'lanes': 1, 'first_lane': 1, 'displays': None,
            'perf_wait': True, 'results': os.path.join(OUT, 'results.json'), 'check': False, 'names': []}
    i = 0
    takes = {'--player': 'players', '--repeat': 'repeat', '--lanes': 'lanes', '--first-lane': 'first_lane',
             '--displays': 'displays', '--results': 'results'}
    while i < len(argv):
        a = argv[i]
        if a in takes:
            if i + 1 >= len(argv):
                raise SpecError(f'{a} needs a value')
            opts[takes[a]] = argv[i + 1]
            i += 1
        elif a == '--bless':
            opts['bless'] = True
        elif a == '--no-perf-wait':
            opts['perf_wait'] = False
        elif a == '--check':
            opts['check'] = True
        elif a.startswith('-'):
            raise SpecError(f'unknown option {a}')
        else:
            opts['names'].append(a)
        i += 1
    if opts['players'] not in ('ours', 'original', 'both'):
        raise SpecError('--player is ours, original or both')
    for key in ('repeat', 'lanes', 'first_lane'):
        try:
            opts[key] = int(opts[key])
        except ValueError:
            raise SpecError(f'--{key.replace("_", "-")} takes a number')
    if not 1 <= opts['lanes'] <= MAX_LANES:
        raise SpecError(f'--lanes is 1..{MAX_LANES}')
    if opts['first_lane'] < 0 or opts['repeat'] < 1:
        raise SpecError('--first-lane is 0 or more, --repeat 1 or more')
    if opts['displays'] is not None:
        try:
            opts['displays'] = parse_displays(opts['displays'])
        except ValueError as e:
            raise SpecError(str(e))
        if len(opts['displays']) < opts['lanes']:
            raise SpecError('--displays names fewer displays than --lanes')
    if not opts['names']:
        raise SpecError('no spec named')
    return opts


def report(record):
    lines = []
    head = f'{record["spec"]} in {record["player"]} ({record["index"]})'
    if record['verdict'] == 'pass':
        lines.append(f'ok   {head} ({record["out"]})')
    elif record['verdict'] == 'skipped':
        lines.append(f'skip {head}: {record["reasons"][0]}')
    else:
        lines.append(f'FAIL {head}: ' + '; '.join(record['reasons']))
        for s in record.get('checks', {}).get('signatures', {}).get('new', []):
            lines.append('     ' + s)
    if record['player'] == 'original' and record.get('signature_count'):
        lines.append(f'     the original logged {record["signature_count"]} signature(s)')
    return '\n'.join(lines)


def main(argv):
    try:
        opts = parse_args(argv)
        specs = [load_spec(n) for n in opts['names']]
    except SpecError as e:
        print(f'e2e: {e}', file=sys.stderr)
        usage()
    if opts['check']:
        for spec in specs:
            print(f'ok   {spec["name"]}: {len(spec["setup"])} setup and {len(spec["steps"])} steps'
                  + (', needs E2eDriver' if needs_driver(spec) else ''))
        return 0
    env = environment()
    kinds = ['original', 'ours'] if opts['players'] == 'both' else [opts['players']]
    needed = ['QA_U57_GAME', 'QA_U57_STEAM', 'E2E_ONIMCP'] + (['E2E_ORIGINAL'] if 'original' in kinds else [])
    missing = [n for n in needed if not env.get(n)]
    if missing:
        print('e2e: skipped, needs ' + ', '.join(missing) + ' (out/local-tools/qa.env)')
        return 0
    queue = JobQueue(plan_jobs(specs, kinds, opts['repeat']))
    results, records, state = {}, [], threading.Lock()

    def process(job, lane, log):
        spec, kind, index = job['spec'], job['kind'], job['index']
        record = {'spec': spec['name'], 'player': kind, 'index': index, 'mods': spec['mods'],
                  'started': time.strftime('%Y-%m-%dT%H:%M:%S')}
        with state:
            originals = [results[d] for d in job['after'] if d in results]
        reference = None
        if kind == 'ours':
            reference = Reference.from_runs(spec, originals) if job['after'] else Reference.blessed(spec)
            if job['after'] and reference is None:
                return dict(record, verdict='skipped', reasons=['the spec fails in the original'], out=None)
        log(f'== {spec["name"]} in {kind} ({index}/{opts["repeat"]})')
        with machine_lock(perf_wait=opts['perf_wait'], log=log):
            result = Run(spec, kind, index, env, lane, log, opts['perf_wait'],
                         reference.values if reference else None).execute()
        with state:
            results[job['id']] = result
        record.update(result['lane'], out=result['out'], outcome=result['outcome'], saves=result['saves'],
                      options_written=result['options_written'], signature_count=len(result['signatures']))
        reasons = [result['outcome']] if result['outcome'] else []
        if reasons and reference and result['failed_step_kind'] in ('expect_ui', 'expect_state'):
            reasons[0] += ' (it held in the original: the interaction has no effect in ours)'
        if kind == 'ours':
            record['reference'] = reference.source if reference else None
            if reference:
                checks, more = differential(spec, result, reference)
                record['checks'] = checks
                reasons += more
        record.update(verdict='fail' if reasons else 'pass', reasons=reasons)
        if opts['bless'] and kind == 'original' and not result['outcome'] and index == 1:
            for save in result['saves']:
                shutil.copy(os.path.join(result['out'], save + '.sav'), os.path.join(FIXTURES, save + '.sav'))
                log(f'     blessed fixture {save}')
            log(f'     blessed references into {bless(spec, result)}')
        if os.path.isdir(result['out']):
            with open(os.path.join(result['out'], 'result.json'), 'w') as f:
                json.dump(record, f, indent=1, default=sorted)
        return record

    def worker(slot):
        lane = allocate_lane(opts['first_lane'])
        if opts['displays']:
            lane.display = opts['displays'][slot]
        log = lambda s: say(f'[lane {lane.number}] {s}')
        try:
            while True:
                job = queue.next()
                if job is None:
                    return
                try:
                    record = process(job, lane, log)
                except Exception as e:  # a runner bug fails this run, not the queue
                    import traceback
                    traceback.print_exc()
                    record = {'spec': job['spec']['name'], 'player': job['kind'], 'index': job['index'],
                              'verdict': 'fail', 'reasons': [f'runner error: {e!r}'], 'out': None}
                with state:
                    records.append(record)
                say(report(record))
                queue.done(job)
        finally:
            lane.release()

    threads = [threading.Thread(target=worker, args=(slot,)) for slot in range(min(opts['lanes'], len(queue.jobs)))]
    for t in threads:
        t.start()
        time.sleep(1)  # lanes are allocated in order
    for t in threads:
        t.join()
    os.makedirs(os.path.dirname(os.path.abspath(opts['results'])), exist_ok=True)
    with open(opts['results'], 'w') as f:
        json.dump(records, f, indent=1, default=sorted)
    failures = sum(1 for r in records if r['verdict'] == 'fail')
    say(f'== {failures} failure(s); results in {opts["results"]}')
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
