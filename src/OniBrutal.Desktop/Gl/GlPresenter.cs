using System;
using System.Threading;
using Brutal.GlfwApi;
using Brutal.OpenGlApi;
using OniBrutal.Engine;
using static OniBrutal.Desktop.Gl.GlConst;
using N = Brutal.OpenGlApi.OpenGl.Native;

namespace OniBrutal.Desktop.Gl
{
    /// Presentation on a thread of its own, as Unity's player presents from its render thread:
    /// the GL thread draws each frame into an offscreen back buffer of the rendering context;
    /// the presenter thread, current on the window's context (shared with it), waits for that
    /// frame's commands to complete, copies it to the window and swaps. A swap that blocks
    /// (vsync, or a display path that reads the frame back) then overlaps the next frames' work
    /// instead of stalling it. Without vsync up to two frames are queued for presentation. With
    /// vsync, one, and a frame counts as queued until the GPU has done its swap: the driver
    /// returns from SwapBuffers at once and queues frames of its own, which came on top of ours.
    /// Measured on Windows at 60 Hz (a key press to the screen changing, out/local-tools/winlat):
    /// the original 50 ms, ours 83 ms before, 50 ms so. Three back buffers rotate, so the GL
    /// thread never draws into a buffer that is queued or being shown.
    internal sealed unsafe class GlPresenter : IDisposable
    {
        const uint SyncGpuCommandsComplete = 0x9117;
        const uint TimeoutExpired = 0x911B;
        const ulong WaitSliceNanoseconds = 100_000_000;

        /// One offscreen back buffer: colour texture and depth-stencil renderbuffer, attached to
        /// a framebuffer of the rendering context; the presenter attaches the same texture to a
        /// framebuffer of its own (framebuffers are not shared between contexts).
        sealed class Buffer
        {
            public uint Color, Depth, Framebuffer;
            public int Width, Height, Generation;
            // Presenter side.
            public uint PresentFramebuffer;
            public int PresentGeneration = -1;
            /// Signalled when the presenter's copy has read the buffer: the GL thread waits for it
            /// (on the GPU) before drawing into the buffer again.
            public GlSync CopyDone;
            public bool HasCopyDone;
        }

        readonly GlfwWindow window;
        const int MaxQueued = 2, MaxQueuedWithVsync = 1;
        readonly Buffer[] buffers = [new(), new(), new()];
        int current;
        readonly Thread thread;
        readonly object gate = new();
        // Handed to the presenter under the gate; a frame counts as queued until it has been
        // presented.
        readonly System.Collections.Generic.Queue<(Buffer Buffer, GlSync Fence, int Interval)> queue = new();
        int queued;
        bool stopping;
        Exception failure;
        int swapInterval = -1;

        public GlPresenter(GlfwWindow window)
        {
            this.window = window;
            thread = new Thread(Run) { Name = "Presenter", IsBackground = true };
            thread.Start();
        }

        /// The framebuffer standing in for the back buffer this frame, sized to the window's
        /// framebuffer.
        public uint BackBuffer(int width, int height)
        {
            var b = buffers[current];
            if (b.HasCopyDone)
            {
                N.glWaitSync(b.CopyDone, default, TimeoutIgnored);
                N.glDeleteSync(b.CopyDone);
                b.HasCopyDone = false;
            }
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            if (b.Framebuffer == 0 || b.Width != width || b.Height != height) Allocate(b, width, height);
            return b.Framebuffer;
        }

        const ulong TimeoutIgnored = ulong.MaxValue;

        static void Allocate(Buffer b, int width, int height)
        {
            if (b.Framebuffer == 0)
            {
                b.Framebuffer = GlApi.GenFramebuffer();
                b.Color = GlApi.GenTexture();
                b.Depth = GlApi.GenRenderbuffer();
            }
            GlBindings.BindTexture(TexTarget2D, b.Color);
            N.glTexImage2D(TexTarget2D, 0, (int)Rgba8, width, height, 0, Rgba, UnsignedByte, null);
            N.glTexParameteri(TexTarget2D, TextureMinFilter, (int)Nearest);
            N.glTexParameteri(TexTarget2D, TextureMagFilter, (int)Nearest);
            N.glTexParameteri(TexTarget2D, TextureMaxLevel, 0);
            N.glBindRenderbuffer(Renderbuffer, new GlRenderbuffer(b.Depth));
            N.glRenderbufferStorage(Renderbuffer, Depth24Stencil8, width, height);
            GlApi.BindFramebuffer(Framebuffer, b.Framebuffer);
            N.glFramebufferTexture2D(Framebuffer, ColorAttachment0, TexTarget2D, b.Color, 0);
            N.glFramebufferRenderbuffer(Framebuffer, DepthStencilAttachment, Renderbuffer, new GlRenderbuffer(b.Depth));
            b.Width = width;
            b.Height = height;
            b.Generation++;
            GlDevice.InvalidateFramebufferBinding();
            GlStateCache.Invalidate();
        }

        /// Hands the frame drawn into the current back buffer to the presenter, once fewer than
        /// two frames are queued (one with vsync), and moves on to the next buffer.
        public void Present(int interval)
        {
            var b = buffers[current];
            if (b.Framebuffer == 0) return;
            var fence = N.glFenceSync(SyncGpuCommandsComplete, default);
            N.glFlush();
            lock (gate)
            {
                while (queued >= (interval > 0 ? MaxQueuedWithVsync : MaxQueued) && failure == null) Monitor.Wait(gate);
                if (failure != null) throw new InvalidOperationException("Presenting failed.", failure);
                queue.Enqueue((b, fence, interval));
                queued++;
                Monitor.PulseAll(gate);
            }
            current = (current + 1) % buffers.Length;
        }

        /// Waits until every frame handed over has been presented.
        public void WaitIdle()
        {
            lock (gate)
                while (queued > 0 && failure == null) Monitor.Wait(gate);
        }

        void Run()
        {
            try
            {
                window.MakeContextCurrent();
                while (true)
                {
                    (Buffer Buffer, GlSync Fence, int Interval) frame;
                    lock (gate)
                    {
                        while (queue.Count == 0 && !stopping) Monitor.Wait(gate);
                        if (queue.Count == 0) break;
                        frame = queue.Dequeue();
                    }
                    PresentBuffer(frame.Buffer, frame.Fence, frame.Interval);
                    lock (gate)
                    {
                        queued--;
                        Monitor.PulseAll(gate);
                    }
                }
            }
            catch (Exception e)
            {
                lock (gate)
                {
                    failure = e;
                    queue.Clear();
                    queued = 0;
                    Monitor.PulseAll(gate);
                }
            }
            finally
            {
                Glfw.PInvoke.MakeContextCurrent(null!);
            }
        }

        void PresentBuffer(Buffer b, GlSync fence, int interval)
        {
            if (interval != swapInterval)
            {
                Glfw.SwapInterval(interval);
                swapInterval = interval;
            }
            // The frame's commands complete before the copy reads them. The wait is on this
            // thread rather than on the window context's GPU queue, so neither context's queue
            // ever waits on the other's.
            while (N.glClientWaitSync(fence, default, WaitSliceNanoseconds) == TimeoutExpired) { }
            N.glDeleteSync(fence);
            if (b.PresentGeneration != b.Generation)
            {
                if (b.PresentFramebuffer == 0) b.PresentFramebuffer = GlApi.GenFramebuffer();
                GlApi.BindFramebuffer(ReadFramebuffer, b.PresentFramebuffer);
                N.glFramebufferTexture2D(ReadFramebuffer, ColorAttachment0, TexTarget2D, b.Color, 0);
                b.PresentGeneration = b.Generation;
            }
            GlApi.BindFramebuffer(ReadFramebuffer, b.PresentFramebuffer);
            GlApi.BindFramebuffer(DrawFramebuffer, 0);
            N.glBlitFramebuffer(0, 0, b.Width, b.Height, 0, 0, b.Width, b.Height, ColorBufferBit, Nearest);
            // The GL thread draws into the buffer again only after the copy has read it.
            b.CopyDone = N.glFenceSync(SyncGpuCommandsComplete, default);
            b.HasCopyDone = true;
            N.glFlush();
            window.SwapBuffers();
            if (interval > 0)
            {
                // With vsync the swap is done when the frame is on its way to the display.
                var swapped = N.glFenceSync(SyncGpuCommandsComplete, default);
                N.glFlush();
                while (N.glClientWaitSync(swapped, default, WaitSliceNanoseconds) == TimeoutExpired) { }
                N.glDeleteSync(swapped);
            }
        }

        /// Presents what was handed over, then stops the thread and releases the window's
        /// context.
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
}
