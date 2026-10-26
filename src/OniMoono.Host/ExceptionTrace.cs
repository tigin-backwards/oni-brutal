using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace OniMoono.Host
{
    /// `--trace-exceptions`: logs each first-chance exception with the IL offset of every frame,
    /// which locates the throwing instruction in assemblies without symbols (the game's).
    static class ExceptionTrace
    {
        [ThreadStatic] static bool inside;

        public static void Install() => AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            if (inside) return;
            inside = true;
            try
            {
                var text = new StringBuilder($"[first-chance] {e.Exception.GetType().FullName}: {e.Exception.Message} (thread {Environment.CurrentManagedThreadId})\n");
                foreach (var frame in new StackTrace(1, false).GetFrames())
                {
                    var m = frame.GetMethod();
                    if (m == null) continue;
                    text.Append($"    {m.DeclaringType?.FullName}::{m.Name} IL_{Math.Max(0, frame.GetILOffset()):x4}\n");
                }
                OniMoono.Engine.EngineLog.Engine(text.ToString().TrimEnd());
            }
            finally
            {
                inside = false;
            }
        };
    }
}
