using System;
using System.Diagnostics;
using OniMoono.Engine;
using Xunit;

namespace OniMoono.Tests
{
    /// Personal paths in the log (LogScrubber, `--log-paths hidden`): each rule anchored to a
    /// known folder or name, Windows rules for any case and slash, nothing else touched.
    public class LogScrubberTests(ITestOutputHelper output)
    {
        static readonly LogScrubber Windows = LogScrubber.Create(windows: true, @"C:\Users\Admin", @"E:\Documents", "Admin", [@"C:\Users\ADMINI~1"]);
        static readonly LogScrubber Linux = LogScrubber.Create(windows: false, "/home/nigit", "/home/nigit/Documents", "nigit");

        [Theory]
        [InlineData(@"[engine] Moono starting: commit=abc exe=C:\Users\Admin\AppData\Local\oni-moono\player\OniMoono.exe game=D:\Steam\OxygenNotIncluded",
            @"[engine] Moono starting: commit=abc exe=%USERPROFILE%\AppData\Local\oni-moono\player\OniMoono.exe game=D:\Steam\OxygenNotIncluded")]
        // Unity's own forward slashes, and mixed ones.
        [InlineData("settings: C:/Users/Admin/AppData/LocalLow/Klei/Oxygen Not Included/oni-moono/settings.json",
            "settings: %USERPROFILE%/AppData/LocalLow/Klei/Oxygen Not Included/oni-moono/settings.json")]
        [InlineData(@"C:/Users\Admin/AppData\LocalLow", @"%USERPROFILE%/AppData\LocalLow")]
        // Any case of the drive and the folders; escaped as in JSON.
        [InlineData(@"c:\users\ADMIN\Desktop", @"%USERPROFILE%\Desktop")]
        [InlineData(@"{""path"":""C:\\Users\\Admin\\x""}", @"{""path"":""%USERPROFILE%\\x""}")]
        // The profile alone, at the end of a line or in quotes.
        [InlineData(@"home is C:\Users\Admin", @"home is %USERPROFILE%")]
        [InlineData(@"'C:\Users\Admin'", @"'%USERPROFILE%'")]
        // Its short form, as the temp folder can name it.
        [InlineData(@"C:\Users\ADMINI~1\AppData\Local\Temp\x.tmp", @"%USERPROFILE%\AppData\Local\Temp\x.tmp")]
        // Documents on another drive, with the Steam ID of cloud saves.
        [InlineData(@"Loading save file: E:\Documents\Klei\OxygenNotIncluded\save_files\Colony\Colony.sav",
            @"Loading save file: %DOCUMENTS%\Klei\OxygenNotIncluded\save_files\Colony\Colony.sav")]
        [InlineData(@"E:\Documents\Klei\OxygenNotIncluded\cloud_save_files\76561197960265729\Colony\Colony.sav",
            @"%DOCUMENTS%\Klei\OxygenNotIncluded\cloud_save_files\<steam id>\Colony\Colony.sav")]
        [InlineData(@"E:/documents/klei", @"%DOCUMENTS%/klei")]
        // A path in a stack trace's frame.
        [InlineData(@"  at Foo.Bar () [0x00000] in C:\Users\Admin\source\Mod\Patch.cs:42",
            @"  at Foo.Bar () [0x00000] in %USERPROFILE%\source\Mod\Patch.cs:42")]
        // The user name as a segment of another path (a profile elsewhere).
        [InlineData(@"D:\Users\Admin\mods\x.dll", @"D:\Users\<user>\mods\x.dll")]
        public void WindowsPaths(string line, string scrubbed) => Assert.Equal(scrubbed, Windows.Scrub(line));

        [Theory]
        // A longer name that starts like the profile, the bare user name, a SteamID64 that is
        // no path segment, other drives' folders: left alone.
        [InlineData(@"C:\Users\Administrator\x")]
        [InlineData(@"C:\Users\Admin2\x")]
        [InlineData(@"E:\Documents2\x")]
        [InlineData("Admin logged in as Admin")]
        [InlineData(@"C:\Users\Admins\x and Admin")]
        [InlineData("lobby 76561197960265729 joined")]
        [InlineData(@"F:\Games\Steam\steamapps\common")]
        [InlineData("a line without a slash at all")]
        public void WindowsTextLeftAlone(string line) => Assert.Same(line, Windows.Scrub(line));

        [Theory]
        [InlineData("[engine] Moono starting: commit=abc exe=/home/nigit/projects/oni-moono/out/OniMoono game=/home/nigit/.steam/steam/steamapps/common/OxygenNotIncluded",
            "[engine] Moono starting: commit=abc exe=~/projects/oni-moono/out/OniMoono game=~/.steam/steam/steamapps/common/OxygenNotIncluded")]
        [InlineData("Loading save file: /home/nigit/.config/unity3d/Klei/Oxygen Not Included/cloud_save_files/76561197960265729/Colony/Colony.sav",
            "Loading save file: ~/.config/unity3d/Klei/Oxygen Not Included/cloud_save_files/<steam id>/Colony/Colony.sav")]
        [InlineData("  at Foo.Bar () [0x00000] in /home/nigit/src/Mod/Patch.cs:42", "  at Foo.Bar () [0x00000] in ~/src/Mod/Patch.cs:42")]
        [InlineData("cwd /home/nigit", "cwd ~")]
        [InlineData("/mnt/c/Users/nigit/Documents", "/mnt/c/Users/<user>/Documents")]
        public void PosixPaths(string line, string scrubbed) => Assert.Equal(scrubbed, Linux.Scrub(line));

        [Theory]
        // POSIX paths are case-sensitive; a longer name or a path under another folder is not home.
        [InlineData("/home/NIGIT/x")]
        [InlineData("/home/nigitx/x")]
        [InlineData("/srv/home/nigit.old/x")]
        [InlineData("nigit started the game")]
        public void PosixTextLeftAlone(string line) => Assert.Same(line, Linux.Scrub(line));

        [Fact]
        public void ShortUserNamesAndRootHomesAreNoAnchors()
        {
            var root = LogScrubber.Create(windows: false, "/", "/", "me");
            const string line = "/home/me/x and /usr/lib";
            Assert.Same(line, root.Scrub(line));
        }

        /// The per-line cost, over a mix like a real log's: most lines have no slash, some a
        /// path to replace, some a slash and nothing to replace.
        [Fact]
        public void PerLineCost()
        {
            string[] lines =
            [
                "[INFO] [Game] Loading mod Plib 4.17",
                "[engine] Preloaded 151 scripting assemblies",
                @"Loading save file: E:\Documents\Klei\OxygenNotIncluded\cloud_save_files\76561197960265729\Colony\Colony.sav",
                "[WARNING] Duplicant 3/5 has no bed",
                "NullReferenceException: Object reference not set to an instance of an object",
                @"  at Foo.Bar () [0x00000] in C:\Users\Admin\source\Mod\Patch.cs:42",
                "[INFO] [Game] Sim tick 12345",
                "  at Game.Update () [0x0001f] in <a1b2c3d4e5f6>:0",
                "[INFO] ConduitFlow: 120 kg/s",
                "Fetch errand assigned",
            ];
            // Warmed up first, as the log's code is once the game has run a while: the tiered JIT
            // recompiles a method once it has been called for a while (100 ms at least).
            var warm = Stopwatch.StartNew();
            for (int i = 0; warm.ElapsedMilliseconds < 500; i++) Windows.Scrub(lines[i % lines.Length]);
            const int N = 100_000;
            long sum = 0;
            double Cost(Func<int, string> line)
            {
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < N; i++) sum += Windows.Scrub(line(i)).Length;
                return watch.Elapsed.TotalMilliseconds * 1e6 / N;
            }
            double ns = Cost(i => lines[i % lines.Length]);
            double pathNs = Cost(_ => lines[2]);
            const string gamePath = @"Loading mod from D:\Steam\steamapps\workshop\content\457140\1234567890\Mod.dll";
            double slashNs = Cost(_ => gamePath);
            double plainNs = Cost(_ => lines[0]);
            output.WriteLine($"LogScrubber: {N:N0} lines each, {ns:F0} ns a line (mixed), {pathNs:F0} ns (a save path replaced), "
                             + $"{slashNs:F0} ns (a path with nothing personal), {plainNs:F0} ns (no slash); {sum}");
            // A generous bound: a log line costs microseconds to write anyway.
            Assert.True(ns < 20_000, $"{ns:F0} ns a line");
        }
    }
}
