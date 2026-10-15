using System;
using System.IO;

namespace OniBrutal.Assets.Tests
{
    /// The reference game install (ONI_GAME_DIR, default ~/projects/oni-linux). Corpus tests need
    /// it; they fail with a clear message when it is missing rather than silently passing.
    static class GameData
    {
        public static string DataDir
        {
            get
            {
                var game = Environment.GetEnvironmentVariable("ONI_GAME_DIR")
                           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects", "oni-linux");
                var dir = Path.Combine(game, "OxygenNotIncluded_Data");
                if (!Directory.Exists(dir)) throw new InvalidOperationException($"Game data not found at {dir}; set ONI_GAME_DIR.");
                return dir;
            }
        }

        public static readonly string[] SerializedFiles =
        [
            "globalgamemanagers", "globalgamemanagers.assets", "level0", "level1", "level2",
            "resources.assets", "sharedassets0.assets", "sharedassets1.assets", "sharedassets2.assets",
            "Resources/unity default resources", "Resources/unity_builtin_extra",
        ];
    }
}
