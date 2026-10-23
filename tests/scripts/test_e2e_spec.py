"""e2e.py's spec parsing and validation, options, keys and expectations."""
import json, os, tempfile, unittest

import helpers  # noqa: F401  (puts scripts/ on the path)
import e2e
import yaml


def spec(text):
    return yaml.safe_load(text)


class SpecValidation(unittest.TestCase):
    def test_existing_spec_is_valid(self):
        loaded = e2e.load_spec('early-base')
        self.assertEqual(loaded['name'], 'early-base')
        self.assertEqual(loaded['setup'], [])
        self.assertFalse(e2e.needs_driver(loaded))

    def test_every_new_step_kind_of_the_plan(self):
        # The step list of §4.4, as written there.
        s = spec('''
from: early-base
mods: [3468585385]
options: {SweepByType: {DisableIcons: true}}
setup:
  - call: {tool: game_control, args: {domain: speed, action: pause}}
  - driver: {setup.call: {type: X, method: Y, args: []}}
  - driver: {seed: 12345}
steps:
  - open: {management: research}
  - open: {key: R}
  - open: {user_menu: "Label"}
  - click: {ui: "*/ResearchScreen/**/Tech_FarmingTech/Button", mods: [Shift]}
  - click: {text: "Quick Learner", under: "UnityDuplicitySelectionScreen"}
  - toggle: {ui: "a/b", to: false}
  - dropdown: {ui: "a/b", option: "Sandstone"}
  - type: {ui: ".../Input", text: "e2e-blueprint", enter: true}
  - key: {code: "Shift+B"}
  - drag: {from: [110, 207], to: [118, 209]}
  - tool: {toolbar: "SweepByType"}
  - expect_ui: {"**/ToReplace/CurrentlyActive/Label": {text: "Quick Learner", active: true}}
  - expect_state: {"Research.Instance.queuedTech.Count": ">=3"}
  - ui_dump: {name: selector, root: "**/BlueprintSelector"}
  - shot: {name: selector, crop: "**/BlueprintSelector", mask: ["**/Portrait", "**/DateLabel"]}
  - world_shot: {name: zoomed, ortho: 45, at: [128, 210], hide_ui: true}
  - ui_wait: {path: "**/BlueprintSelector", cond: stable}
  - click_world: {cell: [150, 212]}
  - scroll: {at: center, notches: -40}
  - shot: end-of-ui
''')
        self.assertEqual(e2e.check_spec(s), [])
        self.assertTrue(e2e.needs_driver(s))

    def test_setup_call_only_in_setup(self):
        s = spec('''
from: landing
steps:
  - driver: {setup.call: {type: X, method: Y}}
''')
        self.assertEqual(e2e.check_spec(s), ['steps 1: driver setup.call is only allowed in setup:'])

    def test_problems_are_reported(self):
        s = spec('''
from: landing
mods: [abc]
colour: blue
steps:
  - frobnicate: 1
  - click: {under: X}
  - toggle: {ui: X}
  - key: {code: "Hyper+Q"}
  - key: {code: "B+Shift"}
  - drag: {from: [1, 2]}
  - world_shot: {name: w, mask: [dupes]}
  - driver: {teleport: {}}
  - {dig: [1, 2, 3, 4], run: {seconds: 1}}
''')
        problems = e2e.check_spec(s)
        joined = '\n'.join(problems)
        for text in ("unknown key 'colour'", '`mods` is a list of Workshop IDs', "steps 1: unknown step 'frobnicate'",
                     'steps 2: needs `ui`', 'steps 3: toggle needs `to', "unknown key 'Hyper'",
                     "'B' in 'B+Shift' is not a modifier", 'steps 6: drag takes', 'no symbolic masks',
                     "unknown E2eDriver command 'teleport'", 'steps 9: a step is one'):
            self.assertIn(text, joined)

    def test_key_held_down_across_steps(self):
        s = spec('''
from: early-base
steps:
  - key: {code: Q, down: true, at: [110, 207]}
  - run: {seconds: 2}
  - key: {code: Q, up: true}
''')
        self.assertEqual(e2e.check_spec(s), [])
        s = spec('''
from: early-base
steps:
  - key: {code: Q, down: true, up: true}
  - key: {code: "Shift+Q", down: true}
''')
        joined = '\n'.join(e2e.check_spec(s))
        self.assertIn('steps 1: key takes `down: true` or `up: true`, not both', joined)
        self.assertIn('steps 2: a key held down (`down`/`up`) is one key, without modifiers', joined)

    def test_file_only_expect_state_needs_no_driver(self):
        s = spec('from: x\nsteps:\n  - expect_state: {"file:blueprints/e2e.blueprint": exists}\n')
        self.assertEqual(e2e.check_spec(s), [])
        self.assertFalse(e2e.needs_driver(s))
        s = spec('from: x\nsteps:\n  - shot: {name: a, crop: "**/X"}\n')
        self.assertTrue(e2e.needs_driver(s))

    def test_load_spec_rejects_invalid(self):
        with tempfile.NamedTemporaryFile('w', suffix='.yaml', delete=False) as f:
            f.write('from: landing\nsteps:\n  - nope: 1\n')
        try:
            with self.assertRaises(e2e.SpecError):
                e2e.load_spec(f.name)
        finally:
            os.remove(f.name)


class Arguments(unittest.TestCase):
    def test_defaults(self):
        o = e2e.parse_args(['early-base'])
        self.assertEqual((o['players'], o['lanes'], o['first_lane'], o['displays'], o['perf_wait']),
                         ('ours', 1, 1, None, True))

    def test_lanes_and_displays(self):
        o = e2e.parse_args(['--lanes', '2', '--first-lane', '2', '--displays', ':88,89', '--no-perf-wait', 'a', 'b'])
        self.assertEqual((o['lanes'], o['first_lane'], o['displays'], o['perf_wait'], o['names']),
                         (2, 2, [':88', ':89'], False, ['a', 'b']))

    def test_bad(self):
        for argv in (['--lanes', '7', 'a'], ['--lanes', '2', '--displays', ':88', 'a'], ['--player', 'x', 'a'],
                     ['--displays', ':88,:88', 'a'], ['--frob', 'a'], [], ['--lanes']):
            with self.assertRaises(e2e.SpecError, msg=argv):
                e2e.parse_args(argv)


class Keys(unittest.TestCase):
    def test_combos(self):
        self.assertEqual(e2e.parse_key('Shift+B'), (['Shift_L'], 'b'))
        self.assertEqual(e2e.parse_key('Ctrl + Alt + F5'), (['Control_L', 'Alt_L'], 'F5'))
        self.assertEqual(e2e.parse_key('Escape'), ([], 'Escape'))
        self.assertEqual(e2e.parse_key('Alpha3'), ([], '3'))
        self.assertEqual(e2e.parse_key('7'), ([], '7'))
        self.assertEqual(e2e.parse_key('LeftShift+UpArrow'), (['Shift_L'], 'Up'))
        self.assertEqual(e2e.parse_key('Shift'), ([], 'Shift_L'))
        self.assertEqual(e2e.parse_key('KeypadPlus'), ([], 'KP_Add'))
        self.assertEqual(e2e.parse_key('return'), ([], 'Return'))

    def test_table_covers_the_plan(self):
        for name in ['A', 'Z', 'Alpha0', 'Alpha9', 'F1', 'F12', 'LeftShift', 'LeftControl', 'LeftAlt', 'Escape',
                     'Return', 'UpArrow', 'DownArrow', 'LeftArrow', 'RightArrow']:
            self.assertIsNotNone(e2e.keysym_name(name), name)

    def test_unknown(self):
        with self.assertRaises(e2e.SpecError):
            e2e.parse_key('Meta+Q')


class Expectations(unittest.TestCase):
    def check(self, have, wanted, saved=None, original=None):
        return e2e.state_check(have, wanted, {} if saved is None else saved, original)

    def test_values_and_relations(self):
        self.assertEqual(self.check(3, '>=3'), (True, False))
        self.assertEqual(self.check(2, '>=3'), (False, False))
        self.assertEqual(self.check(1, 1), (True, False))
        self.assertEqual(self.check('x', 'x'), (True, False))
        self.assertEqual(self.check(True, True), (True, False))
        self.assertEqual(self.check(1, True), (False, False))
        self.assertEqual(self.check(None, 'exists'), (False, False))
        self.assertEqual(self.check('text', 'exists'), (True, False))
        self.assertEqual(self.check(None, 'missing'), (True, False))
        self.assertEqual(self.check(['QuickLearner', 'Uncultured'], {'contains': 'QuickLearner'}), (True, False))
        self.assertEqual(self.check(None, {'contains': 'QuickLearner'}), (False, False))

    def test_saved_values(self):
        saved = {}
        self.assertEqual(self.check(100.0, {'save_as': 'before'}, saved), (True, False))
        self.assertEqual(saved, {'before': 100.0})
        self.assertEqual(self.check(96.0, '>= 0.95 * before', saved), (True, False))
        self.assertEqual(self.check(90.0, '>= 0.95 * before', saved), (False, False))
        with self.assertRaises(e2e.SpecFailure):
            self.check(1, '== later', saved)
        # Text in quotes, for names such as an enum's.
        self.assertEqual(self.check('Agriculture', "!= 'AnimalControl'", saved), (True, False))
        self.assertEqual(self.check('R90', '== "R90"', saved), (True, False))

    def test_original_is_deferred(self):
        self.assertEqual(self.check(4, '== original'), (True, True))
        self.assertEqual(self.check(4, '== original', original=4), (True, False))
        self.assertEqual(self.check(3, '== original', original=4), (False, False))

    def test_ui(self):
        element = e2e.Element({'path': 'a/b', 'rect': {'x': 0, 'y': 0, 'w': 10, 'h': 10}, 'active': True, 'isOn': False,
                               'interactable': True, 'text': 'Quick Learner (+2)'})
        self.assertEqual(e2e.ui_mismatches(element, {'active': True, 'text': '~Quick Learner'}), [])
        self.assertEqual(e2e.ui_mismatches(element, {'text': '/^Quick .*\\(\\+\\d\\)$/'}), [])
        self.assertEqual(e2e.ui_mismatches(element, {'isOn': True}), ['isOn is False, wanted True'])
        self.assertEqual(e2e.ui_mismatches(None, {'exists': False}), [])
        self.assertEqual(e2e.ui_mismatches(None, {'active': True}), ['no such element'])
        self.assertEqual(e2e.ui_mismatches(element, {'exists': False}), ['exists is True, wanted False'])

    def test_compare_keeps_the_old_forms(self):
        self.assertTrue(e2e.compare(3, 3))
        self.assertTrue(e2e.compare(3, '>=2'))
        self.assertFalse(e2e.compare(3, '<3'))
        with self.assertRaises(e2e.SpecFailure):
            e2e.compare(3, 'three')


class RunStep(unittest.TestCase):
    def test_a_paused_game_is_resumed_and_paused_again(self):
        calls = []
        steps = iter([{'elapsedGameSeconds': 0.0, 'status': 'completed', 'isPaused': True},
                      {'elapsedGameSeconds': 60.0, 'status': 'completed'},
                      {'elapsedGameSeconds': 60.0, 'status': 'completed'}])

        class Mcp:
            def call(self, tool, **args):
                calls.append(args['action'])
                return next(steps) if args['action'] == 'step_until' else {}
        sleep, e2e.time.sleep = e2e.time.sleep, lambda s: None
        try:
            self.assertEqual(e2e.run(Mcp(), 120), 120.0)
        finally:
            e2e.time.sleep = sleep
        self.assertEqual(calls, ['step_until', 'resume', 'step_until', 'step_until', 'pause'])


class Options(unittest.TestCase):
    def test_plib_config_beside_each_dll_and_shared(self):
        with tempfile.TemporaryDirectory() as profile:
            mod = os.path.join(profile, 'mods', 'Local', '1863428350')
            os.makedirs(os.path.join(mod, 'archived_versions', 'old'))
            open(os.path.join(mod, 'SweepByType.dll'), 'w').close()
            open(os.path.join(mod, 'archived_versions', 'old', 'SweepByType.dll'), 'w').close()
            with open(os.path.join(mod, 'config.json'), 'w') as f:
                json.dump({'Kept': 1, 'DisableIcons': False}, f)
            written = e2e.write_options(profile, {'1863428350': 'SweepByType'}, {'SweepByType': {'DisableIcons': True}})
            self.assertEqual(sorted(os.path.relpath(w, profile) for w in written), [
                'mods/Local/1863428350/archived_versions/old/config.json',
                'mods/Local/1863428350/config.json',
                'mods/config/SweepByType/config.json'])
            self.assertEqual(json.load(open(os.path.join(mod, 'config.json'))), {'Kept': 1, 'DisableIcons': True})
            # By Workshop ID too.
            e2e.write_options(profile, {'1863428350': 'SweepByType'}, {'1863428350': {'X': 2}})
            self.assertEqual(json.load(open(os.path.join(mod, 'config.json')))['X'], 2)
            with self.assertRaises(e2e.SpecFailure):
                e2e.write_options(profile, {'1863428350': 'SweepByType'}, {'Other': {'X': 1}})

    def test_config_file_named_by_the_spec(self):
        with tempfile.TemporaryDirectory() as profile:
            mod = os.path.join(profile, 'mods', 'Local', '2890083659')
            os.makedirs(mod)
            open(os.path.join(mod, 'MiniBox.dll'), 'w').close()
            written = e2e.write_options(profile, {'2890083659': '2890083659'},
                                        {'2890083659': {'$file': 'MiniBox.json', 'WireLoadKw': 3.0}})
            self.assertEqual(sorted(os.path.relpath(w, profile) for w in written), [
                'mods/Local/2890083659/MiniBox.json',
                'mods/config/MiniBox/MiniBox.json'])
            self.assertEqual(json.load(open(os.path.join(mod, 'MiniBox.json'))), {'WireLoadKw': 3.0})
        problems = e2e.check_spec({'steps': [], 'options': {'X': {'$file': '../x.json'}}})
        self.assertTrue(any('$file' in p for p in problems), problems)


class Install(unittest.TestCase):
    def test_onimcp_and_driver_beside_the_mods(self):
        with tempfile.TemporaryDirectory() as root:
            onimcp, driver = os.path.join(root, 'onimcp'), os.path.join(root, 'driver')
            os.makedirs(onimcp)
            with open(os.path.join(onimcp, 'mod.yaml'), 'w') as f:
                f.write('title: "x"\nstaticID: "LIghtJUNction.OniMcp"\n')
            # E2eDriver: its build's assembly, and its yaml files from tools/E2eDriver.
            os.makedirs(driver)
            for name in ('E2eDriver.dll', 'E2eDriver.pdb', 'E2eDriver.deps.json'):
                open(os.path.join(driver, name), 'w').close()
            fixture = os.path.join(root, 'landing.sav')
            open(fixture, 'w').close()
            lane = e2e.Lane(3, None)
            player = e2e.Player('ours', os.path.join(root, 'run'), {'E2E_ONIMCP': onimcp, 'E2E_DRIVER': driver},
                                fixture, lane, driver=True)
            mods = json.load(open(os.path.join(player.profile, 'mods', 'mods.json')))['mods']
            self.assertEqual([(m['label']['id'], m['staticID'], m['label']['version']) for m in mods],
                             [('OniMcp', 'LIghtJUNction.OniMcp', 312605614),
                              ('E2eDriver', 'E2eDriver', 1399985550)])
            installed = os.path.join(player.profile, 'mods', 'Local', 'E2eDriver')
            self.assertEqual(sorted(os.listdir(installed)), ['E2eDriver.dll', 'mod.yaml', 'mod_info.yaml'])
            config = json.load(open(os.path.join(player.profile, 'OniMcpConfig.json')))
            self.assertEqual(config['Port'], 8830)
            self.assertEqual(lane.driver_port, 8831)


if __name__ == '__main__':
    unittest.main()
