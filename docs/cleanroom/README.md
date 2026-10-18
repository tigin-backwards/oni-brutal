# Clean-room rewrites

Some engine code was ported from Unity's own managed C# rather than written independently: the
IMGUI module (`src/OniBrutal.UnityEngine/IMGUI/`) and UnityEvents (`Core/UnityEvents.cs`). It
reproduced Unity's private structure, internal names and quirks. Unity's C# reference source is
published under a reference-only licence, so that code is replaced. This folder holds the
specifications the replacements are written from, and the rules for writing them.

## Roles

- **Specification** (the side that has seen the old code and Unity's behaviour): writes a
  functional specification of what the code must do: its observable behaviour, the API it must
  offer and the data formats it must read. A specification says what happens, not how: no code,
  no private structure, no internal names other than those an interface requires.
- **Implementation** (a separate agent session, started without this project's history): writes
  the replacement from the specification alone. It works in a branch where the old files are
  deleted, and does not read them, their history (`git log -p`, `git show` of earlier commits),
  Unity's C# reference source, or decompiled Unity assemblies. It designs its own structure and
  private names.

## What the implementation may use

- The specification in this folder.
- Unity's public documentation (the Scripting API reference and the manual).
- Interface facts that the game and its data require, listed in each specification:
  - public type and member names and signatures, which `tools/ApiCheck` reports as missing once
    the old files are gone;
  - serialized field names, which the game's data files contain;
  - names the game looks up by reflection.
- The rest of this repository: the engine's own native layer, the serializer, the renderer and
  the tests.
- Measurement: running the game, the tests, or `tools/UnityProbe` in the original player.

## Records

Each replacement's commit names the specification it was written from. The specification's
history and the implementation's branch are kept, so the order (specification first,
implementation from it) can be shown.
