using System;
using System.IO;
using OniBrutal.Engine;
using Xunit;

namespace OniBrutal.Tests
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
    }
}
