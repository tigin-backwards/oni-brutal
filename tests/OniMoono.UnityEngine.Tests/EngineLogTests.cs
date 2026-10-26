using System;
using System.IO;
using OniMoono.Engine;
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
    }
}
