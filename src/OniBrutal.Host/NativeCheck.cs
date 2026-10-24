using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using OniBrutal.Engine.Platform;

namespace OniBrutal.Host
{
    /// `--check-natives`: every P/Invoke of the assemblies the host would run with this install,
    /// against the libraries it would bind to, read from their files without loading them
    /// (NativeExports). The game's plugins (SimDLL, fmodstudio, steam_api…) resolve as the host
    /// resolves them for the install's layout (GameLayout), so a Mac bundle can be checked on Linux.
    /// Fails when an entry point a found plugin lacks is imported.
    internal static class NativeCheck
    {
        /// Library names that are the operating system's or a runtime's, not the game's: Windows'
        /// (never called on another OS), Unix and macOS system libraries, and BRUTAL's natives,
        /// which BRUTAL resolves from its packages (an osx-x64 package carries none:
        /// docs/research/macos-feasibility.md §3.2).
        static string? Outside(string module)
        {
            var name = module.ToLowerInvariant();
            if (name.StartsWith("/system/library/") || name.StartsWith("/usr/lib/") || name == "libsystem") return "macOS system library";
            if (name is "libc" or "dl" or "libdl.so.2" or "libc.so.6" or "libasound" or "__internal") return "Unix system library";
            if (name.EndsWith(".dll") || name is "kernel32" or "kernelbase" or "advapi32" or "gdi32" or "gdiplus" or "user32" or "ole32" or "shell32") return "Windows library";
            if (name is "glfw" or "freetype" or "stb" or "glad") return "BRUTAL native (its package's)";
            return null;
        }

        /// Plugins an install lacks by design, and why. A P/Invoke binds when first called, so
        /// their imports fail only if called, as in Klei's own player of the same install.
        static readonly Dictionary<string, string> absentPlugins = new(StringComparer.Ordinal)
        {
            ["cimgui"] = "Klei's ImGui dev tools; the Mac build compiles them out and ships no cimgui",
            ["libEOSSDK-Mac-Shipping"] = "Epic Online Services; the game never creates an EOS platform",
            ["EOSSDK-Win64-Shipping.dll"] = "Epic Online Services; the game never creates an EOS platform",
            ["rail_api64"] = "WeGame's Rail SDK (firstpass' bindings); neither the Linux nor the Mac build ships it",
            ["sdkencryptedappticket"] = "Steam's encrypted app tickets (Steamworks.NET's bindings); no game assembly names them, and the Mac build ships no library",
        };

        /// Entry points the game's own plugins lack, on Linux (where the game runs) as on macOS:
        /// Steamworks.NET 20.1.0 binds a newer SDK than the game's steam_api, and
        /// Klei's Linux cimgui lacks SetKAssertCB (ARCHITECTURE D20).
        static readonly Dictionary<string, string[]> absentEntries = new(StringComparer.Ordinal)
        {
            ["steam_api"] =
            [
                "SteamAPI_ISteamNetworkingConnectionSignaling_Release", "SteamAPI_ISteamNetworkingConnectionSignaling_SendSignal",
                "SteamAPI_ISteamNetworkingSignalingRecvContext_OnConnectRequest", "SteamAPI_ISteamNetworkingSignalingRecvContext_SendRejectionSignal",
                "SteamGameServerClient", "SteamGameServer_ReleaseCurrentThreadMemory",
            ],
            ["cimgui"] = ["SetKAssertCB"],
            ["sdkencryptedappticket"] = ["SteamEncryptedAppTicket_BIsLicenseBorrowed", "SteamEncryptedAppTicket_BIsLicenseTemporary", "SteamEncryptedAppTicket_BIsTicketSigned"],
        };

        public static int Run(GameLayout layout, IReadOnlyList<string> assemblies)
        {
            // module -> entry point -> assemblies importing it
            var imports = new SortedDictionary<string, SortedDictionary<string, SortedSet<string>>>(StringComparer.Ordinal);
            foreach (var path in assemblies)
            {
                using var pe = new PEReader(File.OpenRead(path));
                if (!pe.HasMetadata) continue;
                var md = pe.GetMetadataReader();
                foreach (var h in md.MethodDefinitions)
                {
                    var import = md.GetMethodDefinition(h).GetImport();
                    if (import.Module.IsNil) continue;
                    var module = md.GetString(md.GetModuleReference(import.Module).Name);
                    var entry = md.GetString(import.Name);
                    if (!imports.TryGetValue(module, out var entries)) imports[module] = entries = new(StringComparer.Ordinal);
                    if (!entries.TryGetValue(entry, out var from)) entries[entry] = from = new(StringComparer.Ordinal);
                    from.Add(Path.GetFileName(path));
                }
            }
            Console.WriteLine($"{assemblies.Count} assemblies, {imports.Count} libraries, {imports.Values.Sum(e => e.Count)} entry points ({(layout.IsMacBundle ? "macOS bundle " + layout.App : layout.DataDir)})");
            int unexpected = 0;
            foreach (var (module, entries) in imports)
            {
                string importers = string.Join(" ", entries.Values.SelectMany(s => s).Distinct().Order(StringComparer.Ordinal));
                var plugin = layout.Plugin(module, windows: OperatingSystem.IsWindows() && !layout.IsMacBundle);
                if (plugin == null)
                {
                    var outside = Outside(module);
                    if (outside != null) Console.WriteLine($"  {module}: {entries.Count} entry points, {outside} ({importers})");
                    else if (absentPlugins.TryGetValue(module, out var why)) Console.WriteLine($"  {module}: {entries.Count} entry points, no plugin in the install, expected: {why} ({importers})");
                    else
                    {
                        unexpected += entries.Count;
                        Console.WriteLine($"  {module}: {entries.Count} entry points, NO PLUGIN in the install ({importers})");
                    }
                    continue;
                }
                var bytes = File.ReadAllBytes(plugin);
                var exports = NativeExports.Read(bytes);
                var absent = entries.Keys.Where(e => !exports.Contains(e)).ToList();
                var known = absentEntries.GetValueOrDefault(module, []);
                // A thin file's format names its architecture; a universal one lists its slices.
                string arch = NativeExports.Format(bytes) == "Mach-O universal"
                    ? " " + string.Join("+", NativeExports.Architectures(bytes).Select(c => c switch { 0x01000007 => "x86_64", 0x0100000C => "arm64", _ => $"cpu{c:x}" }))
                    : "";
                Console.WriteLine($"  {module}: {entries.Count - absent.Count} of {entries.Count} entry points exported by {Path.GetRelativePath(layout.PluginsDir, plugin)} ({NativeExports.Format(bytes)}{arch}, {exports.Count} exports; {importers})");
                foreach (var e in absent)
                {
                    bool expected = Array.IndexOf(known, e) >= 0;
                    if (!expected) unexpected++;
                    Console.WriteLine($"      {(expected ? "missing, as on Linux:" : "MISSING")} {e} ({string.Join(" ", entries[e])})");
                }
            }
            Console.WriteLine(unexpected == 0 ? "every entry point of the install's plugins is exported, or its absence expected" : $"{unexpected} entry points missing");
            return unexpected == 0 ? 0 : 1;
        }
    }
}
