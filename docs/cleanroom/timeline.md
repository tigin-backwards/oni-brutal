# Timeline (Unity.Timeline): functional specification

This project's replacement of Unity's Timeline package, the assembly `Unity.Timeline`
(`src/Unity.Timeline`, namespace `UnityEngine.Timeline`). The rules are in
`docs/cleanroom/README.md`, and PLAN M9 describes the stage.

Only FMODUnity references Timeline: its FMOD event track type for timelines. The game ships no
Timeline content: no `TimelineAsset`, no `PlayableDirector`. Nothing here is ever played. The
types must exist so that FMODUnity's types load and bind.

## Interface

- **Generated shapes:** `src/Unity.Timeline/Generated/`. Do not edit them.
- **Members FMODUnity uses** (`out/surface/package-members.tsv`, the `Unity.Timeline` rows):
  - the attributes `NotKeyableAttribute()`, `TrackBindingTypeAttribute(Type)`,
    `TrackClipTypeAttribute(Type)` and `TrackColorAttribute(float r, float g, float b)`. Each
    keeps its arguments in properties as documented: `type`, `color` and so on.
  - `TrackAsset`: its parameterless constructor, `GetClips()` and the virtual
    `CreateTrackMixer(PlayableGraph, GameObject, int)`, which FMODUnity overrides.
  - `TimelineClip`: `start`, `end` and `asset`.

## Behaviour

- **`TrackAsset`:**
  - A `PlayableAsset` holding its clips in serialized order.
  - `GetClips()` returns them.
  - `CreateTrackMixer` returns `Playable.Create(graph, inputCount)` by default, as documented.
- **`TimelineClip`:**
  - `start` and `duration` are seconds; `end` is `start + duration`.
  - `asset` is the clip's playable asset.
- **The remaining generated types** (`MarkerList`, `ClipCaps`, the interfaces) need only what
  compiling the shapes requires. Members that cannot be reached without Timeline content
  throw `NotSupportedException`, with a message saying that Timeline playback is not
  implemented. This matches how the engine treats Timeline content (`docs/PLAN.md`, Open
  items).

## Verification

- `scripts/build-packages.sh`: nothing missing for FMODUnity.
- `--check-types` loads FMODUnity's Timeline types.
- A unit test constructs a track with clips and reads them back.
