using System;
using System.IO;
using System.Linq;
using OniBrutal.Engine.Platform;
using Xunit;

namespace OniBrutal.Tests
{
    /// Install layouts, on fake trees: Linux's and Windows' OxygenNotIncluded_Data, and the macOS
    /// bundle with its plugin bundles.
    public sealed class GameLayoutTests : IDisposable
    {
        readonly string root = Directory.CreateTempSubdirectory("oni-brutal-layout-").FullName;

        public void Dispose() => Directory.Delete(root, true);

        string Touch(params string[] parts)
        {
            var path = Path.Combine([root, .. parts]);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "");
            return path;
        }

        static string Plist(params (string Key, string Value)[] entries) =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
            + "<plist version=\"1.0\">\n<dict>\n\t<key>CFBundleDevelopmentRegion</key>\n\t<string>English</string>\n\t<key>CFBundleSupportedPlatforms</key>\n\t<array>\n\t\t<string>MacOSX</string>\n\t</array>\n"
            + string.Concat(entries.Select(e => $"\t<key>{e.Key}</key>\n\t<string>{e.Value}</string>\n")) + "</dict>\n</plist>\n";

        /// OxygenNotIncluded.app under `parent`, laid out as the Steam depot's.
        string MacApp(string parent)
        {
            var app = Path.Combine(root, parent, "OxygenNotIncluded.app");
            Directory.CreateDirectory(Path.Combine(app, "Contents", "Resources", "Data", "Managed"));
            Directory.CreateDirectory(Path.Combine(app, "Contents", "MacOS"));
            File.WriteAllText(Path.Combine(app, "Contents", "MacOS", GameLayout.MacExecutable), "");
            File.WriteAllText(Path.Combine(app, "Contents", "Info.plist"), Plist(("CFBundleExecutable", GameLayout.MacExecutable), ("CFBundleIdentifier", "unity.Klei.Oxygen Not Included")));
            foreach (var (bundle, executable) in new[] { ("SimDLL", "SimDLL"), ("fmodstudio", "fmodstudio"), ("steam_api", "libsteam_api.dylib") })
            {
                var contents = Path.Combine(app, "Contents", "PlugIns", bundle + ".bundle", "Contents");
                Directory.CreateDirectory(Path.Combine(contents, "MacOS"));
                File.WriteAllText(Path.Combine(contents, "Info.plist"), Plist(("CFBundleExecutable", executable), ("CFBundleIdentifier", "com.klei." + bundle)));
                File.WriteAllText(Path.Combine(contents, "MacOS", executable), "");
            }
            return app;
        }

        [Fact]
        public void LinuxAndWindowsLayoutsKeepTheirPlugins()
        {
            var linux = Path.Combine(root, "linux");
            var so = Touch("linux", "OxygenNotIncluded_Data", "Plugins", "libSimDLL.so");
            var layout = GameLayout.Find(linux);
            Assert.False(layout.IsMacBundle);
            Assert.Equal(Path.Combine(linux, "OxygenNotIncluded_Data"), layout.DataDir);
            Assert.Equal(layout.DataDir, layout.DataPath);
            Assert.Equal(so, layout.Plugin("SimDLL", windows: false));
            Assert.Null(layout.Plugin("fmodstudio", windows: false));

            var windows = Path.Combine(root, "windows");
            var dll = Touch("windows", "OxygenNotIncluded_Data", "Plugins", "x86_64", "SimDLL.dll");
            layout = GameLayout.Find(windows);
            Assert.Equal(Path.GetDirectoryName(dll), layout.PluginsDir);
            Assert.Equal(dll, layout.Plugin("SimDLL", windows: true));

            Assert.Null(GameLayout.Find(Path.Combine(root, "nothing")));
        }

        [Fact]
        public void TheMacBundleIsFoundByItselfOrByItsFolder()
        {
            var app = MacApp("steam");
            foreach (var gameDir in new[] { app, app + "/", Path.Combine(root, "steam") })
            {
                var layout = GameLayout.Find(gameDir);
                Assert.True(layout.IsMacBundle);
                Assert.Equal(Path.Combine(app, "Contents", "Resources", "Data"), layout.DataDir.Replace("//", "/"));
                // Application.dataPath is the bundle's Contents, as in Unity's Mac player.
                Assert.Equal(Path.Combine(app, "Contents"), layout.DataPath.Replace("//", "/"));
                Assert.Equal(Path.Combine(layout.DataDir, "StreamingAssets"), layout.StreamingAssetsPath);
                Assert.Equal("unity.Klei.Oxygen Not Included", layout.BundleIdentifier);
            }
            // Two bundles: which one is meant is not known.
            MacApp(Path.Combine("two", "a"));
            Directory.Move(Path.Combine(root, "two", "a", "OxygenNotIncluded.app"), Path.Combine(root, "two", "First.app"));
            MacApp("two");
            Assert.Null(GameLayout.Find(Path.Combine(root, "two")));
        }

        [Fact]
        public void MacPluginsAreTheirBundlesExecutables()
        {
            var layout = GameLayout.Find(MacApp("mac"));
            var plugIns = Path.Combine(layout.App, "Contents", "PlugIns");
            Assert.Equal(plugIns, layout.PluginsDir);
            Assert.Equal(Path.Combine(plugIns, "SimDLL.bundle", "Contents", "MacOS", "SimDLL"), layout.Plugin("SimDLL", windows: false));
            // steam_api's executable is named as a library (its Info.plist says which).
            Assert.Equal(Path.Combine(plugIns, "steam_api.bundle", "Contents", "MacOS", "libsteam_api.dylib"), layout.Plugin("steam_api", windows: false));
            // A plain library beside the bundles, then nothing.
            Touch("mac", "OxygenNotIncluded.app", "Contents", "PlugIns", "libresonanceaudio.dylib");
            Assert.Equal(Path.Combine(plugIns, "libresonanceaudio.dylib"), layout.Plugin("resonanceaudio", windows: false));
            Assert.Null(layout.Plugin("cimgui", windows: false));
            Assert.Equal([Path.Combine(plugIns, "libcimgui.dylib"), Path.Combine(plugIns, "cimgui.dylib")], layout.PluginCandidates("cimgui", false));
        }

        /// A thin Mach-O header of CPU type `cpu`, or a universal one with these slices.
        static byte[] MachOHeader(params int[] cpus)
        {
            static byte[] Be(int v) => BitConverter.GetBytes(v).Reverse().ToArray();
            if (cpus.Length == 1) return [.. BitConverter.GetBytes(0xFEEDFACF), .. BitConverter.GetBytes(cpus[0]), .. new byte[24]];
            byte[] fat = [.. Be(unchecked((int)0xCAFEBABE)), .. Be(cpus.Length)];
            foreach (var cpu in cpus) fat = [.. fat, .. Be(cpu), .. new byte[16]];
            return fat;
        }

        [Fact]
        public void APluginWithoutTheProcesssArchitectureGivesWayToTheHostsReplacement()
        {
            var layout = GameLayout.Find(MacApp("arch"));
            var sim = layout.Plugin("SimDLL", windows: false);
            File.WriteAllBytes(sim, MachOHeader(NativeExports.X8664));
            File.WriteAllBytes(layout.Plugin("fmodstudio", false), MachOHeader(NativeExports.X8664, NativeExports.Arm64));
            var host = Path.Combine(root, "host", GameLayout.ReplacementsFolderName);
            Directory.CreateDirectory(host);
            // No replacement: Klei's x86_64 SimDLL is what there is, for either process.
            Assert.Equal((sim, false), layout.Native("SimDLL", false, NativeExports.Arm64, host));
            Assert.Equal((sim, false), layout.Native("SimDLL", false, NativeExports.X8664, host));
            // An x86_64 replacement does not help an arm64 process.
            var port = Path.Combine(host, "libSimDLL.dylib");
            File.WriteAllBytes(port, MachOHeader(NativeExports.X8664));
            Assert.Equal((sim, false), layout.Native("SimDLL", false, NativeExports.Arm64, host));
            // An arm64 one replaces it in an arm64 process only; a universal plugin is kept.
            File.WriteAllBytes(port, MachOHeader(NativeExports.Arm64));
            Assert.Equal((port, true), layout.Native("SimDLL", false, NativeExports.Arm64, host));
            Assert.Equal((sim, false), layout.Native("SimDLL", false, NativeExports.X8664, host));
            File.WriteAllBytes(Path.Combine(host, "libfmodstudio.dylib"), MachOHeader(NativeExports.Arm64));
            Assert.False(layout.Native("fmodstudio", false, NativeExports.Arm64, host)!.Value.Replacement);
            // A plugin the install lacks is replaced too; a name neither has is null.
            File.WriteAllBytes(Path.Combine(host, "libextra.dylib"), MachOHeader(NativeExports.Arm64));
            Assert.Equal((Path.Combine(host, "libextra.dylib"), true), layout.Native("extra", false, NativeExports.Arm64, host));
            Assert.Null(layout.Native("cimgui", false, NativeExports.Arm64, host));
            Assert.True(GameLayout.HasArchitecture(port, NativeExports.Arm64));
            Assert.False(GameLayout.HasArchitecture(port, NativeExports.X8664));
        }

        [Fact]
        public void PlistValuesAreReadByKey()
        {
            var path = Touch("p", "Info.plist");
            File.WriteAllText(path, Plist(("CFBundleExecutable", "libsteam_api.dylib"), ("CFBundleName", "steam_api")));
            Assert.Equal("libsteam_api.dylib", GameLayout.PlistString(path, "CFBundleExecutable"));
            Assert.Equal("steam_api", GameLayout.PlistString(path, "CFBundleName"));
            // A key whose value is not a string, and one that is absent.
            Assert.Null(GameLayout.PlistString(path, "CFBundleSupportedPlatforms"));
            Assert.Null(GameLayout.PlistString(path, "CFBundleIdentifier"));
            Assert.Null(GameLayout.PlistString(Path.Combine(root, "none.plist"), "CFBundleExecutable"));
        }

        [Fact]
        public void SteamsCommandNamesTheGameFolder()
        {
            Assert.Equal("/games/oni", GameLayout.GameDirOfExecutable("/games/oni/OxygenNotIncluded"));
            Assert.Equal("/games/oni", GameLayout.GameDirOfExecutable("/games/oni/OxygenNotIncluded.exe"));
            Assert.Equal("/s/common/OxygenNotIncluded/OxygenNotIncluded.app", GameLayout.GameDirOfExecutable("/s/common/OxygenNotIncluded/OxygenNotIncluded.app"));
            Assert.Equal("/s/common/OxygenNotIncluded/OxygenNotIncluded.app", GameLayout.GameDirOfExecutable("/s/common/OxygenNotIncluded/OxygenNotIncluded.app/"));
            Assert.Equal("/s/common/OxygenNotIncluded/OxygenNotIncluded.app",
                GameLayout.GameDirOfExecutable("/s/common/OxygenNotIncluded/OxygenNotIncluded.app/Contents/MacOS/Oxygen Not Included"));
            Assert.Null(GameLayout.GameDirOfExecutable("/s/common/OxygenNotIncluded/Other.app/Contents/MacOS/Oxygen Not Included"));
            Assert.Null(GameLayout.GameDirOfExecutable("/usr/bin/steam-runtime-launch-client"));
            Assert.Equal("/Users/u/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded", GameLayout.DefaultMacSteamInstall("/Users/u"));
        }

        [Fact]
        public void MacUserFoldersFollowUnitysMacPlayer()
        {
            var f = MacUserFolders.For("/Users/u", "unity.Klei.Oxygen Not Included", "Klei", "Oxygen Not Included");
            Assert.Equal("/Users/u/Library/Application Support/unity.Klei.Oxygen Not Included", f.PersistentDataPath);
            Assert.Equal("/Users/u/Library/Caches/unity.Klei.Oxygen Not Included", f.TemporaryCachePath);
            Assert.Equal("/Users/u/Library/Logs/Klei/Oxygen Not Included/Player.log", f.ConsoleLogPath);
        }
    }
}
