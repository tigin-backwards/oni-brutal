# UnityProbe

A mod for the original game, built against the local install's assemblies, that measures how
Unity's player behaves where documentation is silent (lifecycle timing around `Destroy`,
reactivation of objects awaiting destruction, `OnApplicationQuit` order). The engine's behaviour
in those areas follows its output; `tests/OniBrutal.UnityEngine.Tests/LifecycleTests.cs` encodes
the results.

Use it only with a scratch copy of the game and a scratch profile (`XDG_CONFIG_HOME`), never the
player's own mods folder:

1. `dotnet build -c Release` (set `OniManaged` if the game is not at `~/projects/oni-linux`).
2. Copy `SemanticsProbe.dll` with a `mod.yaml` (`staticID: SemanticsProbe`) and a `mod_info.yaml`
   (`supportedContent: ALL`, `APIVersion: 2`) into the scratch profile's
   `Klei/Oxygen Not Included/mods/Local/SemanticsProbe/`.
3. Start the original once so it lists the mod in `mods/mods.json`, set the entry's `enabled` to
   `true` and `enabledForDlc` to `[""]`, and start it again.
4. Read the `PROBE` lines in `Player.log`.
