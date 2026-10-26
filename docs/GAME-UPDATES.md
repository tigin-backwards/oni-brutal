# When the game updates

Checks to run again on a new build of the game, besides the gate (`scripts/check.sh`, the unit
tests) and the parity scenarios in `docs/PARITY.md`. They are one-offs: too slow or too broad for
every change, but the place a new build can break assumptions this engine makes about its data.

- **Videos, every frame.** The unit tests compare the first 60 frames of each clip with libvpx.
  With a new build, compare every frame of every clip:
  `ONI_FULL_VIDEO_CHECK=1 LD_LIBRARY_PATH=<dir with libvpx.so.9> out/bin/OniMoono.UnityEngine.Tests/debug/OniMoono.UnityEngine.Tests -method OniMoono.Tests.Vp8DecoderTests.ClipsDecodeAsLibvpxDecodesThem`
  (about 17,000 frames, most of an hour in a debug build). Without a system libvpx, Ubuntu's
  package can be unpacked into a scratch directory: `apt-get download libvpx9`, then
  `dpkg-deb -x libvpx9_*.deb <dir>`.
- **Windows and Linux builds alike.** The Windows player assumes the Windows build is the same
  game build as the Linux one (`KleiVersion.ChangeList`) and that its shaders carry OpenGL
  programs; `tools/AssemblyDiff` between the two builds' assemblies and `--check-shaders` on
  Windows show whether that still holds.
