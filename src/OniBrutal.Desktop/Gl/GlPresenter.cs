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
    /// the main thread draws each frame into an offscreen back buffer of the rendering context;
    /// the presenter thread, current on the window's context (shared with it), waits for that
    /// frame's commands to complete, copies it to the window and swaps. A swap that blocks
    /// (vsync, or a display path that reads the frame back) then overlaps the next frame's work
    /// instead of stalling it. One frame is in flight: presenting a frame first waits until the previous
    /// one has been presented, so the main thread never draws into a buffer still being shown.
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
        }

        readonly GlfwWindow window;
        readonly Buffer[] buffers = [new(), new()];
        int current;
        readonly Thread thread;
        readonly object gate = new();
        // Handed to the presenter under the gate.
        Buffer pending;
        GlSync pendingFence;
        int pendingInterval;
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
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            if (b.Framebuffer == 0 || b.Width != width || b.Height != height) Allocate(b, width, height);
            return b.Framebuffer;
        }

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

        /// Hands the frame drawn into the current back buffer to the presenter and moves on to
        /// the other buffer once the previous frame has been presented.
        public void Present(int interval)
        {
            var b = buffers[current];
            if (b.Framebuffer == 0) return;
            var fence = N.glFenceSync(SyncGpuCommandsComplete, default);
            N.glFlush();
            lock (gate)
            {
                while (pending != null && failure == null) Monitor.Wait(gate);
                if (failure != null) throw new InvalidOperationException("Presenting failed.", failure);
                pending = b;
                pendingFence = fence;
                pendingInterval = interval;
                Monitor.PulseAll(gate);
            }
            current ^= 1;
        }

        /// Waits until every frame handed over has been presented.
        public void WaitIdle()
        {
            lock (gate)
                while (pending != null && failure == null) Monitor.Wait(gate);
        }

        void Run()
        {
            try
            {
                window.MakeContextCurrent();
                while (true)
                {
                    Buffer b;
                    GlSync fence;
                    int interval;
                    lock (gate)
                    {
                        while (pending == null && !stopping) Monitor.Wait(gate);
                        if (pending == null) break;
                        b = pending;
                        fence = pendingFence;
                        interval = pendingInterval;
                    }
                    PresentBuffer(b, fence, interval);
                    lock (gate)
                    {
                        pending = null;
                        Monitor.PulseAll(gate);
                    }
                }
            }
            catch (Exception e)
            {
                lock (gate)
                {
                    failure = e;
                    pending = null;
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
            window.SwapBuffers();
            // The copy has read the buffer before the main thread may draw into it again.
            N.glFinish();
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
