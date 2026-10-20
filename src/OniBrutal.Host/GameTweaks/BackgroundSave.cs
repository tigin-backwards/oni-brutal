using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using OniBrutal.Engine;

namespace OniBrutal.Host.GameTweaks
{
    /// Game tweak `background-save` (opt-in): the game serializes the colony into memory and then
    /// compresses it into the file on the main thread (5 s for a large colony with Ionic.Zlib,
    /// 1.3 s with fast-save-compression). The serialized bytes are a finished snapshot, so the
    /// compression and the file write run on a background thread instead, and only serializing
    /// stays on the main thread.
    ///
    /// The saved file is the same. It is written beside the target and moved over it when
    /// complete; until then the target holds the header the game wrote, which is what the save
    /// lists read. Whatever reads, replaces or deletes saves waits for the writes in flight first:
    /// loading a save, the next save, the load screen's delete and move, and the end of the run.
    /// A failed write is logged; the game's own failure dialog does not appear for it. A run that
    /// ends by an unhandled exception waits for the write too.
    internal static class BackgroundSave
    {
        public const string Name = "background-save";

        static readonly object gate = new();
        static Task pending = Task.CompletedTask;
        /// Compress with .NET's zlib (fast-save-compression) rather than Ionic.Zlib, as the game does.
        static bool fast;

        public static void Install(Harmony harmony, bool fastCompression)
        {
            fast = fastCompression;
            // A run ending by an unhandled exception still finishes the save in flight (the game
            // has already truncated its file to the header). A hard kill during the write loses
            // it, as killing the game during its own in-place write does.
            AppDomain.CurrentDomain.UnhandledException += (_, _) =>
            {
                Task t;
                lock (gate) t = pending;
                t.Wait(TimeSpan.FromSeconds(30));
            };
            harmony.Patch(AccessTools.Method("SaveLoader:CompressContents"), prefix: new HarmonyMethod(typeof(BackgroundSave), nameof(Compress)));
            var wait = new HarmonyMethod(typeof(BackgroundSave), nameof(WaitPrefix));
            harmony.Patch(AccessTools.Method("SaveLoader:Save", [typeof(string), typeof(bool), typeof(bool)]), prefix: wait);
            harmony.Patch(AccessTools.Method("SaveLoader:Load", [typeof(string)]), prefix: wait);
            harmony.Patch(AccessTools.Method("LoadScreen:DeleteFileAndEmptyFolder"), prefix: wait);
            foreach (var m in AccessTools.GetDeclaredMethods(AccessTools.TypeByName("LoadScreen")))
                if (m.Name.Contains("Move", StringComparison.Ordinal) || m.Name.Contains("Copy", StringComparison.Ordinal)) harmony.Patch(m, prefix: wait);
        }

        static void WaitPrefix() => WaitAll();

        /// Blocks until every save in flight is on disk.
        public static void WaitAll()
        {
            Task t;
            lock (gate) t = pending;
            if (!t.IsCompleted) EngineLog.Engine("waiting for a save being written");
            t.Wait();
        }

        /// SaveLoader.CompressContents(BinaryWriter, byte[], int): the header is in the file; the
        /// body is compressed and the whole file written in the background.
        static bool Compress(BinaryWriter fileWriter, byte[] uncompressed, int length)
        {
            fileWriter.Flush();
            var file = (FileStream)fileWriter.BaseStream;
            string path = file.Name;
            var header = new byte[file.Position];
            file.Position = 0;
            file.ReadExactly(header);
            file.Position = header.Length;
            // Each write follows the previous one whatever its outcome, and never faults the chain
            // (a fault would make every later save and load throw).
            lock (gate) pending = pending.ContinueWith(_ =>
            {
                try { Write(path, header, uncompressed, length); }
                catch (Exception e) { EngineLog.Engine($"background save of {path}: {e}"); }
            }, TaskScheduler.Default);
            return false;
        }

        static void Write(string path, byte[] header, byte[] body, int length)
        {
            string temporary = path + ".writing";
            try
            {
                using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
                {
                    output.Write(header);
                    if (fast)
                    {
                        using var zlib = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true);
                        zlib.Write(body, 0, length);
                    }
                    else
                    {
                        using var zlib = new Ionic.Zlib.ZlibStream(output, Ionic.Zlib.CompressionMode.Compress, Ionic.Zlib.CompressionLevel.BestSpeed, leaveOpen: true);
                        zlib.Write(body, 0, length);
                        zlib.Flush();
                    }
                }
                // The game closes its handle on the target right after the header; a move racing
                // that close is retried.
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        File.Move(temporary, path, overwrite: true);
                        break;
                    }
                    catch (IOException) when (attempt < 50)
                    {
                        Thread.Sleep(20);
                    }
                }
                EngineLog.Engine($"background save written: {path}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Background save of {path} failed: {e}");
                try { File.Delete(temporary); } catch (Exception) { } // the write's failure is what matters
            }
        }
    }
}
