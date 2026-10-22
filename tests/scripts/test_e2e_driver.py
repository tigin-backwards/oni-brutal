"""e2e.py's E2eDriver client against a small fake server speaking §4.2's protocol (JSON lines over
TCP), and the UI steps built on it, with a fake XTEST input standing in for the X server."""
import json, os, socket, socketserver, tempfile, threading, unittest

import helpers  # noqa: F401
import e2e


class FakeGame:
    """The game side: UI elements by path, state values, a camera, canvases; what a click at a
    point does is up to the test."""

    def __init__(self):
        self.elements = {}
        self.state = {}
        self.requests = []
        self.camera = {'pos': [0, 0], 'ortho': 20}
        self.canvases_hidden = False
        self.lock = threading.Lock()

    def add(self, path, rect, hit=None, **fields):
        self.elements[path] = dict(path=path, rect=rect, active=True, hit=hit or path, **fields)

    @staticmethod
    def glob(pattern, path):
        """A path pattern: `**/` any number of levels (none too), `*` within one name."""
        import re
        regex = re.escape(pattern).replace(r'\*\*/', '(?:.*/)?').replace(r'\*', '[^/]*')
        return re.fullmatch(regex, path) is not None

    def matches(self, args):
        found = []
        for path, e in self.elements.items():
            if 'path' in args and not self.glob(args['path'], path):
                continue
            if 'text' in args and e.get('text') != args['text']:
                continue
            if 'under' in args and not self.glob(args['under'] + '/**/*', path):
                continue
            found.append(dict(e))
        return found

    def handle(self, cmd, args):
        with self.lock:
            self.requests.append((cmd, args))
        if cmd == 'ui.find':
            return {'elements': self.matches(args)}
        if cmd == 'ui.dump':
            return {'lines': [f'{p}\trect {",".join(map(str, e["rect"]))}' for p, e in sorted(self.elements.items())
                              if p.startswith(args['root'].lstrip('*/')) or args['root'] == '**']}
        if cmd == 'ui.wait':
            if not self.matches({'path': args['path']}):
                raise KeyError(f'timeout waiting for {args["path"]}')
            return {'frames': 1}
        if cmd == 'world.to_screen':
            x, y = args['cell']
            return {'x': x * 10, 'y': 720 - y * 3}
        if cmd == 'screen.to_cell':
            return [args['x'] // 10, (720 - args['y']) // 3]
        if cmd == 'state.get':
            if args['expr'] not in self.state:
                raise KeyError(f'no member {args["expr"]}')
            return {'value': self.state[args['expr']]}
        if cmd == 'camera.get':
            return dict(self.camera)
        if cmd == 'camera.set':
            self.camera.update(args)
            return dict(self.camera)
        if cmd in ('canvas.hide', 'canvas.show'):
            self.canvases_hidden = cmd == 'canvas.hide'
            return {}
        if cmd in ('seed', 'setup.call'):
            return {'done': True}
        raise KeyError(f'unknown command {cmd}')


class Handler(socketserver.StreamRequestHandler):
    def handle(self):
        for line in self.rfile:
            request = json.loads(line)
            if request.get('cmd') == 'noise':  # an unrelated line first, then the reply
                self.wfile.write(b'{"id": -1, "ok": true, "result": "ignored"}\n')
            try:
                reply = {'id': request['id'], 'ok': True, 'result': self.server.game.handle(request['cmd'], request.get('args', {}))}
            except KeyError as e:
                reply = {'id': request['id'], 'ok': False, 'error': str(e.args[0])}
            self.wfile.write((json.dumps(reply) + '\n').encode())


class FakeServer(socketserver.ThreadingTCPServer):
    daemon_threads = True
    allow_reuse_address = True


class FakeInput:
    """Records XTEST input; a click at a point runs the game's handler for it."""

    def __init__(self, on_click=None):
        self.events, self.on_click = [], on_click or (lambda x, y, mods: None)

    def click(self, x, y, button='left', mods=()):
        self.events.append(('click', x, y, button, tuple(mods)))
        self.on_click(x, y, mods)

    def key(self, code):
        self.events.append(('key', code))

    def type_text(self, text):
        self.events.append(('type', text))

    def drag(self, start, end, button='left', mods=(), steps=8):
        self.events.append(('drag', tuple(start), tuple(end)))

    def move(self, x, y):
        self.events.append(('move', x, y))

    def scroll(self, x, y, notches):
        self.events.append(('scroll', x, y, notches))


class FakeMcp:
    def __init__(self, folder):
        self.folder, self.calls, self.port = folder, [], 8820

    def call(self, tool, **args):
        self.calls.append((tool, args))
        if tool == 'navigation_control' and args.get('action') == 'screenshot':
            path = os.path.join(self.folder, args['filename'] + '.png')
            from PIL import Image
            Image.new('RGB', (1280, 720)).save(path)
            return {'path': path}
        return {}


class DriverTests(unittest.TestCase):
    def setUp(self):
        self.game = FakeGame()
        self.server = FakeServer(('127.0.0.1', 0), Handler)
        self.server.game = self.game
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.port = self.server.server_address[1]
        self.driver = e2e.Driver(self.port).connect(5)
        self.temp = tempfile.TemporaryDirectory()

    def tearDown(self):
        self.driver.close()
        self.server.shutdown()
        self.server.server_close()
        self.temp.cleanup()

    def run_for(self, steps_input=None):
        run = e2e.Run({'name': 'unit', 'steps': [], 'setup': [], 'mods': []}, 'ours', 1, {}, e2e.Lane(2, None), lambda s: None)
        run.out = self.temp.name
        run.driver = self.driver
        run.input = steps_input or FakeInput()
        run.mcp = FakeMcp(self.temp.name)

        class StubPlayer:
            display, xauthority, profile = ':88', None, self.temp.name
        run.player = StubPlayer()
        return run

    # -- the client
    def test_wire_format(self):
        received = []
        server = socket.socket()
        server.bind(('127.0.0.1', 0))
        server.listen(1)

        def serve():
            conn, _ = server.accept()
            with conn, conn.makefile('rw') as f:
                received.append(json.loads(f.readline()))
                f.write('{"id": 1, "ok": true, "result": {"x": 3, "y": 4}}\n')
                f.flush()
        t = threading.Thread(target=serve)
        t.start()
        driver = e2e.Driver(server.getsockname()[1]).connect(5)
        self.assertEqual(driver.to_screen([10, 20]), (3, 4))
        t.join(5)
        driver.close()
        server.close()
        self.assertEqual(received, [{'id': 1, 'cmd': 'world.to_screen', 'args': {'cell': [10, 20]}}])

    def test_errors_and_unrelated_lines(self):
        with self.assertRaises(e2e.DriverError) as caught:
            self.driver.state('Research.Instance.nothing')
        self.assertIn('no member Research.Instance.nothing', str(caught.exception))
        with self.assertRaises(e2e.DriverError):
            self.driver.request('noise')  # the fake answers an unknown command with an error, after an unrelated line
        self.game.state['x'] = 5
        self.assertEqual(self.driver.state('x'), 5)  # the connection is still in step

    def test_connect_fails_when_nothing_listens(self):
        s = socket.socket()
        s.bind(('127.0.0.1', 0))
        port = s.getsockname()[1]
        s.close()
        with self.assertRaises(e2e.DriverError):
            e2e.Driver(port).connect(1)

    def test_find_normalises(self):
        self.game.add('Canvas/A', [10, 20, 100, 40], text='Hello', isOn=True, interactable=False)
        self.game.elements['Canvas/B'] = {'path': 'Canvas/B', 'rect': {'x': 1, 'y': 2, 'width': 3, 'height': 4},
                                          'topHit': {'path': 'Canvas/B/Label'}}
        self.game.elements['Canvas/C'] = {'path': 'Canvas/C', 'corners': [[0, 0], [0, 10], [20, 10], [20, 0]]}
        a, = self.driver.find(path='Canvas/A')
        self.assertEqual((a.rect, a.center, a.text, a.is_on, a.interactable, a.hit_is_self()),
                         ((10, 20, 100, 40), (60, 40), 'Hello', True, False, True))
        b, = self.driver.find(path='Canvas/B')
        self.assertEqual((b.rect, b.hit, b.hit_is_self()), ((1, 2, 3, 4), 'Canvas/B/Label', True))
        c, = self.driver.find(path='Canvas/C')
        self.assertEqual((c.rect, c.hit_is_self()), ((0, 0, 20, 10), False))
        self.assertFalse(e2e.Element({'path': 'Canvas/B', 'rect': [0, 0, 1, 1], 'hit': 'Canvas/Button'}).hit_is_self())

    def test_frames_are_round_trips(self):
        before = len(self.game.requests)
        self.driver.frames(3)
        self.assertEqual([c for c, _ in self.game.requests[before:]], ['camera.get'] * 3)

    def test_dump(self):
        self.game.add('Screen/List/Item', [0, 0, 5, 5])
        self.assertEqual(self.driver.dump('**/Screen'), 'Screen/List/Item\trect 0,0,5,5\n')

    # -- the steps
    def test_click_checks_occlusion(self):
        self.game.add('Screen/Button', [100, 100, 50, 20], hit='Screen/Button/Label')
        self.game.add('Screen/Covered', [300, 100, 50, 20], hit='Popup/Blocker')
        run = self.run_for()
        run.step('click', {'ui': 'Screen/Button', 'mods': ['Shift']}, 1)
        self.assertEqual(run.input.events, [('click', 125, 110, 'left', ('Shift',))])
        with self.assertRaises(e2e.SpecFailure) as caught:
            run.step('click', {'ui': 'Screen/Covered'}, 2)
        self.assertEqual(str(caught.exception), 'Screen/Covered is occluded by Popup/Blocker')
        self.assertEqual(len(run.input.events), 1)  # no blind click
        with self.assertRaises(e2e.SpecFailure) as caught:
            run.step('click', {'text': 'Nope', 'under': 'Screen'}, 3)
        self.assertIn('no active element', str(caught.exception))

    def test_click_by_text_under(self):
        self.game.add('Screen/List/Item0', [0, 0, 10, 10], text='Sandstone')
        self.game.add('Other/Item0', [50, 50, 10, 10], text='Sandstone')
        run = self.run_for()
        run.step('click', {'text': 'Sandstone', 'under': 'Screen'}, 1)
        self.assertEqual(run.input.events[0][1:3], (5, 5))

    def test_toggle_asserts_the_new_state(self):
        self.game.add('Screen/Toggle', [0, 0, 20, 20], isOn=False)
        flips = FakeInput(lambda x, y, mods: self.game.elements['Screen/Toggle'].update(isOn=True))
        run = self.run_for(flips)
        run.step('toggle', {'ui': 'Screen/Toggle', 'to': True}, 1)
        self.assertEqual(len(flips.events), 1)
        run.step('toggle', {'ui': 'Screen/Toggle', 'to': True}, 2)  # already on: no click
        self.assertEqual(len(flips.events), 1)
        dead = FakeInput()  # #5's case: the click lands and nothing happens
        run = self.run_for(dead)
        with self.assertRaises(e2e.SpecFailure) as caught:
            run.step('toggle', {'ui': 'Screen/Toggle', 'to': False}, 3)
        self.assertIn('isOn stayed True', str(caught.exception))

    def test_dropdown(self):
        self.game.add('Screen/Dropdown', [0, 0, 100, 20], text='Copper')

        def clicked(x, y, mods):
            if (x, y) == (50, 10):
                self.game.add('Screen/Dropdown/Dropdown List/Item 1', [0, 20, 100, 20], text='Sandstone')
            elif (x, y) == (50, 30):
                del self.game.elements['Screen/Dropdown/Dropdown List/Item 1']
                self.game.elements['Screen/Dropdown']['text'] = 'Sandstone'
        run = self.run_for(FakeInput(clicked))
        run.step('dropdown', {'ui': 'Screen/Dropdown', 'option': 'Sandstone'}, 1)
        self.assertEqual([e[1:3] for e in run.input.events], [(50, 10), (50, 30)])

    def test_type_key_drag_scroll(self):
        self.game.add('Dialog/Input', [10, 10, 100, 20])
        run = self.run_for()
        run.step('type', {'ui': 'Dialog/Input', 'text': 'e2e', 'enter': True}, 1)
        run.step('key', {'code': 'Shift+B'}, 2)
        run.step('drag', {'from': [110, 207], 'to': [118, 209]}, 3)
        run.step('click_world', {'cell': [150, 212]}, 4)
        run.step('scroll', {'at': [100, 200], 'notches': -3}, 5)
        self.assertEqual(run.input.events, [('click', 60, 20, 'left', ()), ('type', 'e2e'), ('key', 'Return'),
                                            ('key', 'Shift+B'), ('drag', (1100, 99), (1180, 93)),
                                            ('click', 1500, 84, 'left', ()), ('scroll', 1000, 120, -3)])

    def test_tool_by_toolbar_name(self):
        self.game.add('ToolMenu/Tools/SweepByType', [0, 600, 40, 40])
        run = self.run_for()
        run.step('tool', {'toolbar': 'SweepByType'}, 1)
        self.assertEqual(run.input.events[0][1:3], (20, 620))
        with self.assertRaises(e2e.SpecFailure):
            run.step('tool', {'toolbar': 'Nothing'}, 2)

    def test_expect_ui_and_state(self):
        self.game.add('Card/Traits', [0, 0, 10, 10], text='Quick Learner, Uncultured')
        self.game.state['Research.Instance.queuedTech.Count'] = 4
        run = self.run_for()
        run.step('expect_ui', {'Card/Traits': {'text': '~Quick Learner', 'active': True}, 'Card/Gone': {'exists': False}}, 1)
        with self.assertRaises(e2e.SpecFailure) as caught:
            run.expect_ui({'Card/Traits': {'text': 'Other'}}, tries=2)
        self.assertIn("text is 'Quick Learner, Uncultured', wanted 'Other'", str(caught.exception))
        run.step('expect_state', {'Research.Instance.queuedTech.Count': '>=4'}, 2)
        run.step('expect_state', {'Research.Instance.queuedTech.Count': '== original'}, 3)
        self.assertEqual(run.values[-1], {'step': 3, 'expr': 'Research.Instance.queuedTech.Count', 'value': 4,
                                          'wanted': '== original', 'deferred': True})
        with self.assertRaises(e2e.SpecFailure):
            run.step('expect_state', {'Research.Instance.queuedTech.Count': '>4'}, 4)
        os.makedirs(os.path.join(self.temp.name, 'blueprints'))
        open(os.path.join(self.temp.name, 'blueprints', 'e2e.blueprint'), 'w').close()
        run.step('expect_state', {'file:blueprints/e2e.blueprint': 'exists', 'file:blueprints/x': 'missing'}, 5)

    def test_original_value_is_compared_when_known(self):
        self.game.state['n'] = 4
        run = self.run_for()
        run.original_values = {'7:n': 5}
        with self.assertRaises(e2e.SpecFailure):
            run.step('expect_state', {'n': '== original'}, 7)

    def test_evidence_steps(self):
        self.game.add('Screen/Selector', [100, 50, 400, 300])
        self.game.add('Screen/Selector/Date', [110, 60, 50, 10])
        run = self.run_for()
        run.step('ui_dump', {'name': 'selector', 'root': 'Screen/Selector'}, 1)
        self.assertTrue(open(os.path.join(self.temp.name, 'selector.ui.txt')).read().startswith('Screen/Selector\t'))
        run.step('shot', {'name': 'selector', 'crop': 'Screen/Selector', 'mask': ['Screen/Selector/Date', [0, 0, 5, 5]]}, 2)
        meta = json.load(open(os.path.join(self.temp.name, 'selector.shot.json')))
        self.assertEqual((meta['crop'], meta['masks'], meta['file']),
                         ([100, 50, 400, 300], [[110, 60, 50, 10], [0, 0, 5, 5]], 'selector.png'))
        self.assertEqual(run.input.events[0], ('move', 1279, 719))  # the pointer off the UI first
        run.step('world_shot', {'name': 'zoomed', 'ortho': 45, 'at': [128, 210]}, 3)
        commands = [c for c, _ in self.game.requests]
        self.assertEqual(self.game.camera, {'pos': [128.5, 210.5], 'ortho': 45})
        self.assertLess(commands.index('canvas.hide'), commands.index('canvas.show'))
        self.assertFalse(self.game.canvases_hidden)
        self.assertEqual([e['name'] for e in run.evidence['world_shot']], ['zoomed'])
        run.step('shot', 'plain', 4)  # the old form
        self.assertTrue(os.path.exists(os.path.join(self.temp.name, 'plain.png')))

    def test_driver_and_wait_steps(self):
        run = self.run_for()
        run.step('driver', {'seed': 12345}, 'setup 1')
        run.step('driver', {'setup.call': {'type': 'T', 'method': 'M', 'args': []}}, 'setup 2')
        self.assertIn(('seed', {'n': 12345}), self.game.requests)
        self.assertIn(('setup.call', {'type': 'T', 'method': 'M', 'args': []}), self.game.requests)
        self.game.add('Screen/Ready', [0, 0, 1, 1])
        run.step('ui_wait', {'path': 'Screen/Ready', 'cond': 'stable'}, 1)
        with self.assertRaises(e2e.DriverError):
            run.step('ui_wait', {'path': 'Screen/Never'}, 2)

    def test_open(self):
        run = self.run_for()
        run.step('open', {'management': 'research'}, 1)
        run.step('open', {'key': 'R'}, 2)
        self.assertEqual(run.mcp.calls[0][0], 'game_control')
        self.assertEqual(run.input.events, [('key', 'R')])


if __name__ == '__main__':
    unittest.main()
