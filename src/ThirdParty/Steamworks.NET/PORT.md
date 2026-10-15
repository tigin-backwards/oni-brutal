# Steamworks.NET port

The game ships `com.rlabrecque.steamworks.net.dll`, built by Unity from Steamworks.NET **20.1.0**
(https://github.com/rlabrecque/Steamworks.NET, commit `53d17180582752d7d8c9a757742da3a52dfc33d4`,
MIT licence, see LICENSE.md). `Runtime/` is that release's runtime source, unmodified except:

- `types/SteamDatagramTickets/SteamDatagramRelayAuthTicket.cs`: the private union
  `ExtraField.OptionValue` overlaps a managed `byte[]` with `long`/`ulong` at offset 0. Mono
  accepts that; CoreCLR's type loader rejects it, so any `GetTypes()` on the assembly throws
  (the game scans all assemblies at startup). The field becomes a 128-byte `fixed` buffer: same
  native layout, no object reference. The struct is private and unused by the library.

The project compiles with the symbols Unity defines for a 64-bit Linux standalone player, so the
API surface, P/Invoke entry points and struct packing match the shipped binary. The host ships
this build in place of the one in `Managed/` (ARCHITECTURE §2.1). `tools/AssemblyDiff` verifies
that its public surface and native layouts equal the shipped assembly's.

Verified with `tools/AssemblyDiff` against the shipped binary: the only differences are the union
above and `UnitySourceGeneratedAssemblyMonoScriptTypes_v1`, an internal class Unity's build
pipeline injects into every script assembly to describe its MonoScripts (unreferenced).
