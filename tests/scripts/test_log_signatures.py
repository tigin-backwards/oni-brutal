"""qa-tools.py's log signatures: the same error reads the same from Mono's log and from ours."""
import contextlib, io, os, tempfile, unittest

import helpers

qa_tools = helpers.script('qa-tools.py')


def signatures(text):
    with tempfile.TemporaryDirectory() as d:
        path = os.path.join(d, 'log')
        with open(path, 'w') as f:
            f.write(text)
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            qa_tools.signatures(path)
        return out.getvalue().splitlines()


class LogSignatures(unittest.TestCase):
    def test_io_message_and_runtime_frames(self):
        ours = signatures("FileNotFoundException: Could not find file '/x/State.json'.\n"
                          "  at Interop.ThrowExceptionForIoErrno (Interop+ErrorInfo errorInfo)\n"
                          "  at Microsoft.Win32.SafeHandles.SafeFileHandle.Open (System.String fullPath)\n"
                          "  at DraggableUI.State.Load () [0x00000] in <0>:0\n")
        mono = signatures('FileNotFoundException: Could not find file "/x/State.json"\n'
                          '  at System.IO.FileStream..ctor (System.String path) [0x00001] in <aa>:0\n'
                          '  at DraggableUI.State.Load () [0x00000] in <bb>:0\n')
        self.assertEqual(ours, mono)

    def test_logged_error_frames(self):
        ours = signatures('[12:00:01.000] [1] [ERROR] Could not find ChoreGroup:\n'
                          'UnityEngine.Debug:LogError (object)\n'
                          'CharacterContainer:SetInfoText ()\n')
        mono = signatures('[12:00:01.000] [1] [ERROR] Could not find ChoreGroup:\n'
                          '  at CharacterContainer.SetInfoText () [0x00012] in <abc123>:0\n')
        self.assertEqual(ours, mono)

    def test_run_folder_is_masked(self):
        # The original and ours run in separate invocations, each in a folder of its run id.
        ours = signatures("FileNotFoundException: Could not find file '/w/out/e2e/s/20261024-040121-cfd644/"
                          "ours-1/home/config/mods/Local/1/State.json'.\n")
        mono = signatures('FileNotFoundException: Could not find file "/w/out/e2e/s/20261024-035631-81d98e/'
                          'original-1/home/config/mods/Local/1/State.json"\n')
        self.assertEqual(ours, mono)
        self.assertEqual(qa_tools.unknown(ours, mono), [])

    def test_trailing_period(self):
        self.assertEqual(qa_tools.canonical('NullReferenceException: Object reference not set to an instance of an object.'),
                         qa_tools.canonical('NullReferenceException: Object reference not set to an instance of an object'))

    def test_canonical_is_idempotent(self):
        for s in ["X: it's 'here'. @ at A.B (int) [N] in <abc>:N", 'Y: plain', 'Z: m @ UnityEngine.Debug:LogError (object)']:
            once = qa_tools.canonical(s)
            self.assertEqual(once, qa_tools.canonical(once))

    def test_reference_without_frame_covers_any_frame(self):
        # An older reference kept the message alone when its first frame was runtime internals.
        self.assertEqual(qa_tools.unknown(['X: msg. @ Mod.F'], ['X: msg']), [])
        self.assertEqual(qa_tools.unknown(['X: other @ Mod.F'], ['X: msg']), ['X: other @ Mod.F'])

    def test_a_different_frame_is_new(self):
        self.assertEqual(qa_tools.unknown(['X: msg @ Mod.G'], ['X: msg @ Mod.F']), ['X: msg @ Mod.G'])


if __name__ == '__main__':
    unittest.main()
