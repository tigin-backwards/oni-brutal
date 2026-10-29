using System;
using System.Collections.Generic;
using System.IO;
using OniMoono.Engine;
using UnityEngine;
using Xunit;

namespace OniMoono.Tests
{
    // The game's own logger (Klei's Debug.Log) writes its [INFO] lines with Console.WriteLine; the
    // original player's log file holds them, so the player's log takes the console's output too.
    [Collection("Console")]
    public class EngineLogTests
    {
        [Fact]
        public void ConsoleOutputReachesTheLogFile()
        {
            var path = Path.Combine(Path.GetTempPath(), $"enginelog-{Guid.NewGuid():N}", "Player.log");
            var stdout = Console.Out;
            try
            {
                EngineLog.Open(path);
                Console.WriteLine("[INFO] from the game's logger");
                EngineLog.Engine("an engine line");
            }
            finally
            {
                EngineLog.Close();
                Console.SetOut(stdout);
            }
            var lines = File.ReadAllLines(path);
            Assert.Contains("[INFO] from the game's logger", lines);
            Assert.Single(lines, l => l == "[engine] an engine line");
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }

        // As Unity's player: the default log's last session is kept as Player-prev.log, replacing
        // the one before; a log named on the command line is simply overwritten.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ThePreviousSessionsLogIsKeptForTheDefaultLog(bool keepPrevious)
        {
            var dir = Path.Combine(Path.GetTempPath(), $"enginelog-{Guid.NewGuid():N}");
            var path = Path.Combine(dir, "Player.log");
            var previous = Path.Combine(dir, "Player-prev.log");
            Directory.CreateDirectory(dir);
            File.WriteAllText(previous, "older session\n");
            File.WriteAllText(path, "last session\n");
            var stdout = Console.Out;
            try
            {
                EngineLog.Open(path, keepPrevious);
                EngineLog.Engine("this session");
            }
            finally
            {
                EngineLog.Close();
                Console.SetOut(stdout);
            }
            Assert.Equal(["[engine] this session"], File.ReadAllLines(path));
            Assert.Equal(keepPrevious ? "last session\n" : "older session\n", File.ReadAllText(previous));
            Directory.Delete(dir, true);
        }

        static readonly LogScrubber Scrubber = LogScrubber.Create(windows: true, @"C:\Users\Admin", @"E:\Documents", "Admin");

        /// The file's and the console's text after `action`, the log open with the scrubber.
        static (string File, string Console) Scrubbed(Action action)
        {
            var path = Path.Combine(Path.GetTempPath(), $"enginelog-{Guid.NewGuid():N}", "Player.log");
            var stdout = Console.Out;
            var console = new StringWriter();
            try
            {
                Console.SetOut(console);
                EngineLog.Open(path, scrubber: Scrubber);
                action();
            }
            finally
            {
                EngineLog.Close();
                Console.SetOut(stdout);
            }
            var text = File.ReadAllText(path).Replace("\r\n", "\n");
            Directory.Delete(Path.GetDirectoryName(path)!, true);
            return (text, console.ToString().Replace("\r\n", "\n"));
        }

        // Personal paths are replaced in the file and on the console; the log callbacks get the
        // message as logged (mods and the game's crash reporter read it there).
        [Fact]
        public void TheFileAndConsoleAreScrubbedButCallbacksAreNot()
        {
            const string message = @"Loading save file: E:\Documents\Klei\cloud_save_files\76561197960265729\Colony.sav";
            var got = new List<string>();
            var threaded = new List<string>();
            Application.LogCallback on = (c, s, t) => { if (c.StartsWith("Loading save file")) got.Add(c); };
            Application.LogCallback onThreaded = (c, s, t) => { if (c.StartsWith("Loading save file")) threaded.Add(c); };
            Application.logMessageReceived += on;
            Application.logMessageReceivedThreaded += onThreaded;
            (string File, string Console) text;
            try
            {
                text = Scrubbed(() =>
                {
                    UnityEngine.Debug.Log(message);
                    EngineLog.Engine(@"exe=C:\Users\Admin\OniMoono.exe");
                    Console.WriteLine("[INFO] C:/users/admin/x");
                });
            }
            finally
            {
                Application.logMessageReceived -= on;
                Application.logMessageReceivedThreaded -= onThreaded;
            }
            // logMessageReceived runs only on the engine's main thread, which a test's may not be;
            // both get the same text.
            if (Threads.IsMainThread) Assert.Equal([message], got);
            Assert.Equal([message], threaded);
            const string expected = @"Loading save file: %DOCUMENTS%\Klei\cloud_save_files\<steam id>\Colony.sav" + "\n"
                                    + @"[engine] exe=%USERPROFILE%\OniMoono.exe" + "\n[INFO] %USERPROFILE%/x\n";
            Assert.Equal(expected, text.File);
            Assert.Equal(expected, text.Console);
        }

        // A path written in pieces is replaced once its line is whole; a partial line still goes
        // out, in order, before the next line of other output, and on Close.
        [Fact]
        public void APathSplitAcrossWritesIsScrubbed()
        {
            var text = Scrubbed(() =>
            {
                Console.Write(@"path C:\Us");
                Console.Write('e');
                Console.Write(@"rs\Adm");
                Console.Write("in\\x\nnext ");
                Console.Out.Write(@"C:\Users".ToCharArray());
                Console.WriteLine(@"\Admin\y");
                Console.Write("partial ");
                EngineLog.Engine("an engine line");
                Console.Write(@"tail C:\Users\Admin");
            });
            const string expected = "path %USERPROFILE%\\x\nnext %USERPROFILE%\\y\npartial [engine] an engine line\ntail %USERPROFILE%";
            Assert.Equal(expected, text.File);
            Assert.Equal(expected, text.Console);
        }

        [Fact]
        public void WithoutAScrubberPathsAreWrittenAsLogged()
        {
            var path = Path.Combine(Path.GetTempPath(), $"enginelog-{Guid.NewGuid():N}", "Player.log");
            var stdout = Console.Out;
            try
            {
                EngineLog.Open(path);
                Console.Write(@"C:\Users\Ad");
                Console.WriteLine(@"min\x");
            }
            finally
            {
                EngineLog.Close();
                Console.SetOut(stdout);
            }
            Assert.Equal([@"C:\Users\Admin\x"], File.ReadAllLines(path));
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }
}
