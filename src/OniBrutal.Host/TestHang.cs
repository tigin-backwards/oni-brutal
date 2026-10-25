using System;
using System.Runtime.CompilerServices;
using System.Threading;
using OniBrutal.Engine;

namespace OniBrutal.Host
{
    /// A test hook for the harnesses' hang watchdogs (scripts/hang-dump.sh): ONI_TEST_HANG=FRAME
    /// or FRAME:spin makes the main thread spin forever after frame FRAME, FRAME:wait makes it
    /// block forever on an event no one sets, as a busy loop and a deadlock would. Unset, it costs
    /// one comparison a frame.
    internal static class TestHang
    {
        static readonly (int Frame, bool Spin) at = Parse(Environment.GetEnvironmentVariable("ONI_TEST_HANG"));

        static (int, bool) Parse(string? value)
        {
            if (string.IsNullOrEmpty(value)) return (-1, false);
            var parts = value.Split(':');
            return int.TryParse(parts[0], out var frame) ? (frame, parts.Length < 2 || parts[1] != "wait") : (-1, false);
        }

        public static void Frame(int frame)
        {
            if (frame != at.Frame) return;
            EngineLog.Engine($"ONI_TEST_HANG: the main thread hangs after frame {frame} ({(at.Spin ? "spinning" : "waiting")})");
            Console.Out.Flush();
            if (at.Spin) Spin(); else Wait();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Spin()
        {
            long n = 0;
            while (Volatile.Read(ref n) >= 0) n++;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Wait() => new ManualResetEventSlim(false).Wait();
    }
}
