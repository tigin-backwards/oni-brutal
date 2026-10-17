using System;
using System.Diagnostics;
using System.Text.Json;

namespace OniBrutal.Restarter
{
    /// Restarter EXECUTABLE (Windows): started by the game's App.Restart before the player quits
    /// (e.g. after a language change). Once the player has exited, the executable is started again
    /// with the arguments the player was started with, which the host records in
    /// ONI_BRUTAL_RESTART_ARGV (a JSON array) with its process id in ONI_BRUTAL_RESTART_PID.
    internal static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("usage: Restarter EXECUTABLE");
                return 2;
            }
            if (int.TryParse(Environment.GetEnvironmentVariable("ONI_BRUTAL_RESTART_PID"), out int pid))
            {
                try
                {
                    using var player = Process.GetProcessById(pid);
                    player.WaitForExit();
                }
                catch (ArgumentException)
                {
                    // Already gone.
                }
            }
            var start = new ProcessStartInfo(args[0]) { UseShellExecute = false };
            var argv = Environment.GetEnvironmentVariable("ONI_BRUTAL_RESTART_ARGV");
            if (!string.IsNullOrEmpty(argv))
                foreach (var a in JsonSerializer.Deserialize<string[]>(argv) ?? []) start.ArgumentList.Add(a);
            Process.Start(start);
            return 0;
        }
    }
}
