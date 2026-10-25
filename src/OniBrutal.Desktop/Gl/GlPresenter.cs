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
            // What the GL thread drew into it (GL thread): anything this frame, and whether the
            // last thing drawn was a clear; since its storage was (re)allocated, anything at all.
            public bool DrawnThisFrame, ClearedLast, DrawnSinceAllocation;
            public int Index, DrawnFrame = -1;
        }

        readonly GlfwWindow window;
        const int MaxQueued = 2, MaxQueuedWithVsync = 1;
        readonly Buffer[] buffers = [new() { Index = 0 }, new() { Index = 1 }, new() { Index = 2 }];
        int current;
        readonly Thread thread;
        readonly object gate = new();
        // Handed to the presenter under the gate; a frame counts as queued until it has been
        // presented.
        readonly System.Collections.Generic.Queue<(Buffer Buffer, GlSync Fence, int Interval, int Width, int Height)> queue = new();
        int queued;
        bool stopping;
        Exception failure;
        int swapInterval = -1;

        /// The player's loading splash, drawn over the window by this thread (null: none).
        readonly GlSplash splash;
        // The window's framebuffer size as of the latest frame (GL thread writes): what a redraw
        // between frames fills.
        volatile int windowWidth, windowHeight;
        // The buffer last copied to the window (presenter thread).
        Buffer shown;

        public GlPresenter(GlfwWindow window, GlSplash splash = null, int width = 0, int height = 0)
        {
            this.window = window;
            this.splash = splash;
            windowWidth = width;
            windowHeight = height;
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
            windowWidth = width;
            windowHeight = height;
            // A frame keeps the size it began drawing at: allocating anew would discard what it
            // drew (the window's size changed part way through the frame).
            if (b.Framebuffer == 0 || (b.Width != width || b.Height != height) && !b.DrawnThisFrame) Allocate(b, width, height);
            else if (PresentTrace.Enabled && (b.Width != width || b.Height != height))
                PresentTrace.Log($"frame {Frame}: back buffer {b.Index} kept at {b.Width}x{b.Height} for the rest of the frame (the window is {width}x{height})");
            return b.Framebuffer;
        }

        /// The back buffer to read from (ReadPixels of the screen, captures): this frame's, at the
        /// size it was drawn, even when the window's size has changed since.
        public uint ReadBuffer(int width, int height)
        {
            var b = buffers[current];
            return b.Framebuffer != 0 && b.DrawnThisFrame ? b.Framebuffer : BackBuffer(width, height);
        }

        const ulong TimeoutIgnored = ulong.MaxValue;

        /// The GL thread drew into the current back buffer (a clear when `clear`).
        public void Drew(bool clear)
        {
            var b = buffers[current];
            b.DrawnThisFrame = true;
            b.DrawnSinceAllocation = true;
            b.ClearedLast = clear;
        }

        /// The main thread's frame the GL thread is executing (diagnostics).
        public int Frame;

        void Allocate(Buffer b, int width, int height)
        {
            if (PresentTrace.Enabled)
                PresentTrace.Log($"frame {Frame}: back buffer {b.Index} allocated {width}x{height} (was {b.Width}x{b.Height}){(b.DrawnThisFrame ? ", discarding what the frame drew into it" : "")}");
            b.DrawnSinceAllocation = false;
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

        /// The buffer last handed to the presenter (GL thread).
        Buffer handedOver;

        internal enum PresentChoice { Frame, LastFrameAgain, Nothing }

        /// What a present shows: the frame, when it drew into its back buffer after the buffer's
        /// storage was last allocated; otherwise the last frame shown, again, or nothing before
        /// the first.
        internal static PresentChoice Choose(bool allocated, bool drawnSinceAllocation, bool drawnThisFrame, bool anyShown)
            => allocated && drawnSinceAllocation && drawnThisFrame ? PresentChoice.Frame
                : anyShown ? PresentChoice.LastFrameAgain : PresentChoice.Nothing;

        /// Hands the frame drawn into the current back buffer to the presenter, once fewer than
        /// two frames are queued (one with vsync), and moves on to the next buffer. The window's
        /// framebuffer is `width` x `height` now; a frame drawn at another size is scaled to it.
        ///
        /// A frame that drew nothing into its back buffer, or whose buffer was allocated anew
        /// after it drew (the window's size changed), is not shown: its buffer holds an older
        /// frame or undefined content (black on most drivers). The last frame shown is presented
        /// again in its place and the next frame draws into the same buffer, so the screen keeps
        /// the last complete frame, as a game's back buffer does across stalls and resizes.
        public void Present(int interval, int width, int height)
        {
            var b = buffers[current];
            var choice = Choose(b.Framebuffer != 0, b.DrawnSinceAllocation, b.DrawnThisFrame, handedOver != null);
            bool complete = choice == PresentChoice.Frame;
            if (PresentTrace.Enabled)
            {
                if (b.Framebuffer == 0 || !b.DrawnSinceAllocation) PresentTrace.Log($"frame {Frame}: back buffer {b.Index} ({b.Width}x{b.Height}) has nothing drawn since it was allocated: {(handedOver != null ? $"frame {handedOver.DrawnFrame} shown again" : "not presented")}");
                else if (!b.DrawnThisFrame) PresentTrace.Log($"frame {Frame}: nothing drawn into back buffer {b.Index}: {(handedOver != null ? $"frame {handedOver.DrawnFrame} shown again" : "not presented")}");
                else if (b.ClearedLast) PresentTrace.Log($"frame {Frame}: back buffer {b.Index} presented with nothing drawn after its last clear");
            }
            if (!complete)
            {
                b.DrawnThisFrame = false;
                if (choice == PresentChoice.Nothing) return;
                b = handedOver;
            }
            else
            {
                b.DrawnFrame = Frame;
                b.DrawnThisFrame = false;
            }
            var fence = N.glFenceSync(SyncGpuCommandsComplete, default);
            N.glFlush();
            lock (gate)
            {
                while (queued >= (interval > 0 ? MaxQueuedWithVsync : MaxQueued) && failure == null) Monitor.Wait(gate);
                if (failure != null) throw new InvalidOperationException("Presenting failed.", failure);
                queue.Enqueue((b, fence, interval, Math.Max(1, width), Math.Max(1, height)));
                queued++;
                Monitor.PulseAll(gate);
            }
            if (!complete) return;
            handedOver = b;
            current = (current + 1) % buffers.Length;
        }

        // Under the gate: the window's contents were damaged since the last copy to it.
        bool refreshWanted;

        /// The window's contents were damaged (main thread, from the window's refresh and focus
        /// events): unless a frame is on its way, the presenter copies the last frame to the
        /// window again and swaps, without waiting for the next frame to render. A compositor
        /// that changes how it shows the window (a fullscreen window losing or regaining the
        /// focus) otherwise shows black, or what was there before, until the next frame.
        public void Refresh()
        {
            lock (gate)
            {
                refreshWanted = true;
                Monitor.PulseAll(gate);
            }
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
                    (Buffer Buffer, GlSync Fence, int Interval, int Width, int Height) frame = default;
                    bool redraw = false;
                    lock (gate)
                    {
                        while (queue.Count == 0 && !stopping && !(refreshWanted && shown != null))
                        {
                            // While the splash runs, the window is redrawn about 60 times a
                            // second when no game frame comes.
                            if (splash != null && splash.Active)
                            {
                                if (!Monitor.Wait(gate, 16)) { redraw = true; break; }
                            }
                            else Monitor.Wait(gate);
                        }
                        if (queue.Count > 0)
                        {
                            frame = queue.Dequeue();
                            redraw = false;
                        }
                        else if (stopping) break;
                        // Otherwise the splash is due, or the window's contents were damaged:
                        // the last frame is shown again at once.
                        else redraw = true;
                        refreshWanted = false;
                        // A redraw counts as queued while it reads the frame last shown: every
                        // frame handed over has been presented, so the GL thread draws into
                        // another buffer, and it can hand over only one more meanwhile.
                        if (redraw) queued++;
                    }
                    if (redraw) Redraw();
                    else PresentBuffer(frame.Buffer, frame.Fence, frame.Interval, frame.Width, frame.Height);
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

        void PresentBuffer(Buffer b, GlSync? frameFence, int interval, int width, int height)
        {
            if (interval != swapInterval)
            {
                Glfw.SwapInterval(interval);
                swapInterval = interval;
            }
            // The frame's commands complete before the copy reads them. The wait is on this
            // thread rather than on the window context's GPU queue, so neither context's queue
            // ever waits on the other's. (A frame shown again was complete already.)
            if (frameFence is { } fence)
            {
                while (N.glClientWaitSync(fence, default, WaitSliceNanoseconds) == TimeoutExpired) { }
                N.glDeleteSync(fence);
            }
            if (b.PresentGeneration != b.Generation)
            {
                if (b.PresentFramebuffer == 0) b.PresentFramebuffer = GlApi.GenFramebuffer();
                GlApi.BindFramebuffer(ReadFramebuffer, b.PresentFramebuffer);
                N.glFramebufferTexture2D(ReadFramebuffer, ColorAttachment0, TexTarget2D, b.Color, 0);
                b.PresentGeneration = b.Generation;
            }
            GlApi.BindFramebuffer(ReadFramebuffer, b.PresentFramebuffer);
            GlApi.BindFramebuffer(DrawFramebuffer, 0);
            // A frame drawn before the window changed size fills the window scaled, rather than
            // leaving part of it undefined.
            bool scaled = b.Width != width || b.Height != height;
            N.glBlitFramebuffer(0, 0, b.Width, b.Height, 0, 0, width, height, ColorBufferBit, scaled ? Linear : Nearest);
            // The GL thread draws into the buffer again only after the copy has read it. A frame
            // shown again replaces the fence of its earlier copy (the GL thread waits for a
            // buffer's fence only once it draws into it again, which it does not while the
            // buffer is queued).
            if (b.HasCopyDone) N.glDeleteSync(b.CopyDone);
            b.CopyDone = N.glFenceSync(SyncGpuCommandsComplete, default);
            b.HasCopyDone = true;
            shown = b;
            if (splash != null && splash.Active)
            {
                splash.GameFrame();
                splash.Draw(width, height);
            }
            N.glFlush();
            if (PresentTrace.Enabled) Probe(b, width, height);
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

        /// Copies the frame last shown to the window again (black before the first), scaled to
        /// the window's size now, with the splash over it while it runs, and swaps.
        void Redraw()
        {
            int width = windowWidth, height = windowHeight;
            if (PresentTrace.Enabled && !(splash != null && splash.Active)) PresentTrace.Log($"window contents damaged: frame {shown?.DrawnFrame ?? -1} shown again");
            GlApi.BindFramebuffer(DrawFramebuffer, 0);
            if (shown != null)
            {
                GlApi.BindFramebuffer(ReadFramebuffer, shown.PresentFramebuffer);
                bool scaled = shown.Width != width || shown.Height != height;
                N.glBlitFramebuffer(0, 0, shown.Width, shown.Height, 0, 0, width, height, ColorBufferBit, scaled ? Linear : Nearest);
            }
            else
            {
                N.glClearColor(0f, 0f, 0f, 1f);
                N.glClear(ColorBufferBit);
            }
            if (splash != null && splash.Active) splash.Draw(width, height);
            N.glFlush();
            window.SwapBuffers();
        }

        // ----- Diagnostics (presenter thread) -----

        uint probeTexture, probeFramebuffer;
        const int ProbeSize = 16;
        readonly byte[] probePixels = new byte[ProbeSize * ProbeSize * 4];
        bool lastDark;
        long lastSwap;
        int presented;

        /// Mean luminance of what is about to be swapped to the screen, from a 16x16 reduction of
        /// the window's back buffer; logs frames that turn near black, and long gaps between
        /// swaps.
        void Probe(Buffer b, int width, int height)
        {
            if (probeFramebuffer == 0)
            {
                probeTexture = GlApi.GenTexture();
                GlApi.BindTexture(TexTarget2D, probeTexture);
                N.glTexImage2D(TexTarget2D, 0, (int)Rgba8, ProbeSize, ProbeSize, 0, Rgba, UnsignedByte, null);
                probeFramebuffer = GlApi.GenFramebuffer();
                GlApi.BindFramebuffer(DrawFramebuffer, probeFramebuffer);
                N.glFramebufferTexture2D(DrawFramebuffer, ColorAttachment0, TexTarget2D, probeTexture, 0);
            }
            GlApi.BindFramebuffer(ReadFramebuffer, 0);
            GlApi.BindFramebuffer(DrawFramebuffer, probeFramebuffer);
            N.glBlitFramebuffer(0, 0, width, height, 0, 0, ProbeSize, ProbeSize, ColorBufferBit, Linear);
            GlApi.BindFramebuffer(ReadFramebuffer, probeFramebuffer);
            N.glPixelStorei(PackAlignment, 4);
            fixed (byte* p = probePixels) N.glReadPixels(0, 0, ProbeSize, ProbeSize, Rgba, UnsignedByte, p);
            GlApi.BindFramebuffer(DrawFramebuffer, 0);
            double sum = 0;
            for (int i = 0; i < probePixels.Length; i += 4) sum += 0.299 * probePixels[i] + 0.587 * probePixels[i + 1] + 0.114 * probePixels[i + 2];
            double mean = sum / (ProbeSize * ProbeSize);
            bool dark = mean < 3;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            double gap = lastSwap == 0 ? 0 : (now - lastSwap) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            lastSwap = now;
            presented++;
            // Gaps well above the usual frame time (a main-thread stall such as a save).
            if (gap > 200 && gap > 4 * typicalGap) PresentTrace.Log($"swap {presented} (frame {b.DrawnFrame}): {gap:F0} ms since the last swap");
            if (dark != lastDark) PresentTrace.Log($"swap {presented} (back buffer {b.Index}, {b.Width}x{b.Height}, frame {b.DrawnFrame}): {(dark ? "near black" : "visible again")} (mean luminance {mean:F1})");
            lastDark = dark;
            if (gap > 0) typicalGap = typicalGap == 0 ? gap : typicalGap * 0.95 + Math.Min(gap, 4 * typicalGap) * 0.05;
        }

        double typicalGap;

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
