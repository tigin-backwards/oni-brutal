using System;
using System.Collections.Generic;
using System.Threading;
using Brutal.GlfwApi;

namespace OniBrutal.Desktop.Gl
{
    /// The thread that owns the rendering context and executes all GL work, as Unity's render
    /// thread: the main thread records a frame's work as commands that carry everything they
    /// read (resolved state, uniform values, copies of changed texel and vertex data) and hands
    /// the frame over at present; the GL thread executes it while the main thread records the
    /// next one. One frame is in flight: handing over a frame first waits until the previous
    /// one has been executed. Work whose result the main thread needs (pixel read-back,
    /// program compilation, diagnostics) is a call that runs after everything recorded before
    /// it and waits for it.
    internal sealed class GlWorker : IDisposable
    {
        /// One frame's commands and the data they reference.
        internal sealed class Frame
        {
            public readonly List<Action> Commands = new();
            public readonly FrameArena Arena = new();

            public void Reset()
            {
                Commands.Clear();
                Arena.Reset();
            }
        }

        readonly Thread thread;
        readonly object gate = new();
        Frame recording = new();
        Frame spare = new();
        // Handed to the GL thread under the gate.
        Frame submitted;
        bool stopping;
        Exception failure;

        /// Reports a command that threw (GL thread). The frame goes on with the next command, as
        /// an exception in a draw on the main thread used to unwind only that draw.
        readonly Action<Exception> commandFailed;

        /// Runs on the GL thread before each handed-over batch of commands executes.
        public Action BatchStarting;

        public GlWorker(GlfwWindow context, Action<Exception> commandFailed)
        {
            this.commandFailed = commandFailed;
            // The context moves from the creating thread to the GL thread.
            Glfw.PInvoke.MakeContextCurrent(null!);
            thread = new Thread(() => Run(context)) { Name = "GL", IsBackground = true };
            thread.Start();
        }

        /// The frame being recorded (main thread).
        public Frame Recording => recording;

        public FrameArena Arena => recording.Arena;

        /// Appends work to the frame being recorded.
        public void Post(Action command) => recording.Commands.Add(command);

        /// Hands the recorded frame to the GL thread once the previous one has been executed.
        public void Submit()
        {
            Frame next;
            lock (gate)
            {
                // After shutdown nothing executes recorded work any more.
                if (stopping)
                {
                    recording.Reset();
                    return;
                }
                while (submitted != null && failure == null) Monitor.Wait(gate);
                ThrowIfFailed();
                // The previous frame has been executed: its storage records the next one.
                next = finished ?? spare;
                finished = null;
                spare = null;
                submitted = recording;
                Monitor.PulseAll(gate);
            }
            recording = next ?? new Frame();
        }

        // The executed frame, returned to the main thread for reuse.
        Frame finished;

        /// Runs `work` on the GL thread after everything recorded so far and returns its result.
        public T Call<T>(Func<T> work)
        {
            T result = default;
            Exception error = null;
            Post(() =>
            {
                try { result = work(); }
                catch (Exception e) { error = e; }
            });
            Submit();
            WaitIdle();
            if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(error);
            return result;
        }

        public void Call(Action work) => Call(() => { work(); return 0; });

        /// Waits until every submitted frame has been executed.
        public void WaitIdle()
        {
            lock (gate)
            {
                while (submitted != null && failure == null && !stopping) Monitor.Wait(gate);
                ThrowIfFailed();
            }
        }

        void ThrowIfFailed()
        {
            if (failure != null) throw new InvalidOperationException("Rendering failed on the GL thread.", failure);
        }

        void Run(GlfwWindow context)
        {
            try
            {
                context.MakeContextCurrent();
                while (true)
                {
                    Frame frame;
                    lock (gate)
                    {
                        while (submitted == null && !stopping) Monitor.Wait(gate);
                        if (submitted == null) break;
                        frame = submitted;
                    }
                    BatchStarting?.Invoke();
                    long start = GlTimings.Now;
                    foreach (var command in frame.Commands)
                    {
                        try { command(); }
                        catch (Exception e) { commandFailed(e); }
                    }
                    GlTimings.Busy += GlTimings.Now - start;
                    frame.Reset();
                    lock (gate)
                    {
                        submitted = null;
                        finished = frame;
                        Monitor.PulseAll(gate);
                    }
                }
            }
            catch (Exception e)
            {
                lock (gate)
                {
                    failure = e;
                    Monitor.PulseAll(gate);
                }
            }
            finally
            {
                Glfw.PInvoke.MakeContextCurrent(null!);
            }
        }

        /// Executes what was submitted, then stops the thread.
        public void Dispose()
        {
            lock (gate)
            {
                stopping = true;
                Monitor.PulseAll(gate);
            }
            thread.Join();
        }
    }

    /// Per-frame storage for command data (uniform values and block contents), reused once the
    /// frame has been executed. Growing replaces the arrays: commands recorded earlier keep
    /// the arrays they were given.
    internal sealed class FrameArena
    {
        float[] floats = new float[1 << 16];
        int floatCount;
        byte[] bytes = new byte[1 << 18];
        int byteCount;

        public void Reset()
        {
            floatCount = 0;
            byteCount = 0;
        }

        /// `length` floats: the array and the offset of the range in it.
        public (float[] Array, int Offset) Floats(int length)
        {
            if (floatCount + length > floats.Length)
            {
                floats = new float[Math.Max(floats.Length * 2, length)];
                floatCount = 0;
            }
            int offset = floatCount;
            floatCount += length;
            return (floats, offset);
        }

        /// `length` zeroed bytes.
        public (byte[] Array, int Offset) Bytes(int length)
        {
            if (byteCount + length > bytes.Length)
            {
                bytes = new byte[Math.Max(bytes.Length * 2, length)];
                byteCount = 0;
            }
            int offset = byteCount;
            byteCount += length;
            Array.Clear(bytes, offset, length);
            return (bytes, offset);
        }
    }
}
