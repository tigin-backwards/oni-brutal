using System;
using OniMoono.Engine;

namespace OniMoono.Desktop.Gl
{
    /// Windows: the D3D11 device and flip-model swap chain both devices present through
    /// (DxgiTarget for OpenGL, Vulkan.VkDxgiTarget for Vulkan), as the original (Unity 6 on D3D11)
    /// presents, so that Windows knows the game is SDR: R8G8B8A8_UNORM, FLIP_DISCARD, two buffers,
    /// the default colour space, a frame-latency waitable object at a latency of 1, tearing only
    /// without vsync. Each device renders into a D3D11 texture of the target's, shared with it;
    /// Present copies it into the back buffer (CopySubresourceRegion) and presents.
    /// ARCHITECTURE §3.5, "Presentation on Windows".
    ///
    /// Threads: made on one thread and from then on used and released by the presenter thread
    /// alone. DXGI is told to leave the window alone (DXGI_MWA_NO_WINDOW_CHANGES | NO_ALT_ENTER)
    /// and the swap chain is never put in DXGI's fullscreen state, so neither Present nor
    /// ResizeBuffers sends the window a message the main thread (the window's, which may be
    /// waiting on the device thread, which may be waiting on the presenter) would have to answer.
    /// A window resized by the main thread is stretched by DXGI (DXGI_SCALING_STRETCH) until the
    /// next picture resizes the buffers.
    internal sealed unsafe class DxgiSwapChain : IDisposable
    {
        /// Present through DXGI (the Moono Tweaks screen's "dxgi" row, `--dxgi on|off`); set by the
        /// host before the device starts.
        public static bool Enabled;

        /// Diagnostics (`--dxgi-fault WHAT[,WHAT]`): failures made up, for testing the ways back.
        /// At start-up (the device's own presentation instead): "adapter", "device", "swapchain";
        /// OpenGL's "extension" (no WGL_NV_DX_interop2) and "register"; Vulkan's "import". While
        /// running (DXGI is then restarted): "lock:N" (the Nth interop lock, or keyed-mutex
        /// acquire), "resize:N", "removed:N" (the Nth Present failing with
        /// DXGI_ERROR_DEVICE_REMOVED). "norestart": every restart fails too. Each happens once,
        /// so a restart succeeds.
        public static string Fault;

        static bool faultDone;

        static bool HasFault(string token) => Fault != null && Array.IndexOf(Fault.Split(','), token) >= 0;

        /// The made-up fault at `step` (start-up), once.
        public static bool Faulty(string step)
        {
            if (faultDone || !HasFault(step)) return false;
            faultDone = true;
            return true;
        }

        /// The made-up fault at the Nth time of `step` (1-based), once.
        public static bool Faulty(string step, int count)
        {
            if (faultDone || !HasFault($"{step}:{count}")) return false;
            faultDone = true;
            return true;
        }

        /// A restart after a made-up fault, with "norestart": it fails.
        public static bool RestartRefused => faultDone && HasFault("norestart");

        public const uint BufferCount = 2, MaximumFrameLatency = 1;
        /// How long a present waits for its picture to be on its way before going on regardless.
        const uint WaitMilliseconds = 1000;

        readonly IntPtr hwnd;
        // COM objects (one reference each, released once in Dispose).
        void* factory, device, context, swapChain, swapChain2, backBuffer;
        IntPtr waitable;
        uint swapFlags;
        bool tearing, occluded;
        int featureLevel;
        /// The waitable object's count may be off by pictures not waited for (it starts at the
        /// latency; an occluded picture; a wait that timed out): taken down to nothing before the
        /// next Present.
        bool resync = true;
        int resizes, presents, waitTimeouts;

        public void* Device => device;
        public void* Context => context;
        public long AdapterLuid { get; }
        public string AdapterName { get; private set; } = "";
        public int Width { get; private set; }
        public int Height { get; private set; }
        public string Failure { get; private set; }
        /// The part of the last Present spent waiting for the picture to be on its way.
        public long LastWait { get; private set; }

        DxgiSwapChain(IntPtr hwnd, long luid)
        {
            this.hwnd = hwnd;
            AdapterLuid = luid;
        }

        /// A device on the adapter `luid` and a swap chain for window `hwnd`, `width` x `height`;
        /// null, with the reason, when any step fails (what was made is released).
        public static DxgiSwapChain Create(IntPtr hwnd, long luid, int width, int height, out string reason)
        {
            var chain = new DxgiSwapChain(hwnd, luid);
            try
            {
                reason = chain.Start(Math.Max(1, width), Math.Max(1, height));
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                reason = e.Message;
            }
            if (reason == null) return chain;
            chain.Dispose();
            return null;
        }

        string Start(int w, int h)
        {
            var iid = Dxgi.IIDFactory2;
            void* f;
            int hr = Dxgi.CreateDXGIFactory2(0, &iid, &f);
            if (Dxgi.Failed(hr)) return $"CreateDXGIFactory2 failed ({Dxgi.Hr(hr)})";
            factory = f;
            void* f4 = null, adapter = null;
            hr = Dxgi.QueryInterface(factory, Dxgi.IIDFactory4, &f4);
            if (Dxgi.Failed(hr)) return $"no IDXGIFactory4 ({Dxgi.Hr(hr)})";
            hr = Dxgi.EnumAdapterByLuid(f4, AdapterLuid, Dxgi.IIDAdapter1, &adapter);
            Dxgi.Release(ref f4);
            if (Dxgi.Failed(hr)) return $"no DXGI adapter with the device's LUID {AdapterLuid:X} ({Dxgi.Hr(hr)})";
            Dxgi.AdapterDesc1 desc1;
            if (!Dxgi.Failed(Dxgi.GetDesc1(adapter, &desc1))) AdapterName = desc1.Name;
            void* d = null, c = null;
            int level = 0;
            hr = Faulty("device") ? unchecked((int)0x887A0004) : Dxgi.D3D11CreateDevice(adapter, Dxgi.DriverTypeUnknown, IntPtr.Zero, 0, null, 0, Dxgi.D3D11SdkVersion, &d, &level, &c);
            Dxgi.Release(ref adapter);
            if (Dxgi.Failed(hr)) return $"D3D11CreateDevice on {AdapterName} failed ({Dxgi.Hr(hr)})";
            device = d;
            context = c;
            featureLevel = level;

            void* f5;
            if (!Dxgi.Failed(Dxgi.QueryInterface(factory, Dxgi.IIDFactory5, &f5)))
            {
                int allow = 0;
                tearing = !Dxgi.Failed(Dxgi.CheckFeatureSupport(f5, Dxgi.FeaturePresentAllowTearing, &allow, sizeof(int))) && allow != 0;
                Dxgi.Release(ref f5);
            }
            swapFlags = Dxgi.SwapChainFlagFrameLatencyWaitable | (tearing ? Dxgi.SwapChainFlagAllowTearing : 0);
            var desc = new Dxgi.SwapChainDesc1
            {
                Width = (uint)w, Height = (uint)h, Format = Dxgi.FormatR8G8B8A8Unorm, SampleCount = 1,
                BufferUsage = Dxgi.UsageRenderTargetOutput, BufferCount = BufferCount, Scaling = Dxgi.ScalingStretch,
                SwapEffect = Dxgi.SwapEffectFlipDiscard, AlphaMode = Dxgi.AlphaModeIgnore, Flags = swapFlags,
            };
            void* s = null;
            hr = Faulty("swapchain") ? unchecked((int)0x80070005) : Dxgi.CreateSwapChainForHwnd(factory, device, hwnd, &desc, &s);
            if (Dxgi.Failed(hr)) return $"CreateSwapChainForHwnd failed ({Dxgi.Hr(hr)})";
            swapChain = s;
            Width = w;
            Height = h;
            hr = Dxgi.MakeWindowAssociation(factory, hwnd, Dxgi.WindowAssociationNoWindowChanges | Dxgi.WindowAssociationNoAltEnter);
            if (Dxgi.Failed(hr)) return $"MakeWindowAssociation failed ({Dxgi.Hr(hr)})";
            void* s2;
            hr = Dxgi.QueryInterface(swapChain, Dxgi.IIDSwapChain2, &s2);
            if (Dxgi.Failed(hr)) return $"no IDXGISwapChain2 ({Dxgi.Hr(hr)})";
            swapChain2 = s2;
            hr = Dxgi.SetMaximumFrameLatency(swapChain2, MaximumFrameLatency);
            if (Dxgi.Failed(hr)) return $"SetMaximumFrameLatency failed ({Dxgi.Hr(hr)})";
            waitable = Dxgi.GetFrameLatencyWaitableObject(swapChain2);
            if (waitable == IntPtr.Zero) return "no frame latency waitable object";
            if (!GetBackBuffer()) return Failure;
            return null;
        }

        /// The start-up log line: the adapter (`how` it was found), the swap chain's settings as
        /// DXGI reports them, and `shared`, what the device renders into.
        public string Describe(string how, string shared)
        {
            var actual = new Dxgi.SwapChainDesc1();
            int hr = Dxgi.GetDesc1(swapChain, &actual);
            if (Dxgi.Failed(hr)) return $"present: DXGI on {AdapterName} ({how}); IDXGISwapChain1::GetDesc1 failed ({Dxgi.Hr(hr)})";
            return $"present: DXGI on {AdapterName} ({how}), feature level {featureLevel >> 12}_{(featureLevel >> 8) & 0xF}: swap chain {actual.Width}x{actual.Height} "
                + $"{(actual.Format == Dxgi.FormatR8G8B8A8Unorm ? "R8G8B8A8_UNORM" : actual.Format.ToString())}, "
                + $"{(actual.SwapEffect == Dxgi.SwapEffectFlipDiscard ? "FLIP_DISCARD" : actual.SwapEffect == Dxgi.SwapEffectFlipSequential ? "FLIP_SEQUENTIAL" : actual.SwapEffect.ToString())}, "
                + $"{actual.BufferCount} buffers, colour space RGB_FULL_G22_NONE_P709 (default, SDR), scaling {(actual.Scaling == Dxgi.ScalingStretch ? "STRETCH" : actual.Scaling.ToString())}, "
                + $"maximum frame latency {MaximumFrameLatency} (waitable), tearing without vsync {(tearing ? "allowed" : "not supported")}; {shared}";
        }

        public bool Fail(string reason)
        {
            Failure ??= reason;
            return false;
        }

        /// For a failed call's message: why the device was removed, when it was.
        public string DeviceState(int hr)
            => Dxgi.DeviceLost(hr) && device != null ? $"; device removed reason {Dxgi.Hr(Dxgi.GetDeviceRemovedReason(device))}" : "";

        bool GetBackBuffer()
        {
            void* b;
            int hr = Dxgi.GetBuffer(swapChain, 0, Dxgi.IIDTexture2D, &b);
            if (Dxgi.Failed(hr)) return Fail($"GetBuffer failed ({Dxgi.Hr(hr)}{DeviceState(hr)})");
            backBuffer = b;
            return true;
        }

        /// The buffers made `w` x `h` when they are not.
        public bool Resize(int w, int h)
        {
            if (Failure != null) return false;
            if (w == Width && h == Height) return true;
            resizes++;
            Dxgi.Release(ref backBuffer);
            // Nothing of the swap chain's may still be referenced, bound or queued.
            Dxgi.ClearState(context);
            Dxgi.Flush(context);
            int hr = Faulty("resize", resizes) ? Dxgi.ErrorDeviceRemoved : Dxgi.ResizeBuffers(swapChain, 0, (uint)w, (uint)h, 0, swapFlags);
            if (Dxgi.Failed(hr)) return Fail($"ResizeBuffers to {w}x{h} failed ({Dxgi.Hr(hr)}{DeviceState(hr)})");
            if (PresentTrace.Enabled) PresentTrace.Log($"dxgi: buffers resized {Width}x{Height} -> {w}x{h}");
            Width = w;
            Height = h;
            return GetBackBuffer();
        }

        /// Copies the top-left Width x Height of `source` (a D3D11 texture of the device's) into
        /// the back buffer, presents it `interval` vertical blanks after the last (0: at once)
        /// and waits until it is on its way (one picture in flight, as the OpenGL and Vulkan
        /// swapchain paths have with vsync): the waitable object is signalled as each presented
        /// picture retires; an occluded picture was not queued and is not waited for. The
        /// caller has made `source` ready for D3D11 (the interop unlock, the keyed mutex).
        public bool Present(void* source, int interval)
        {
            if (Failure != null) return false;
            CopyToBackBuffer(source);
            return PresentCopied(interval);
        }

        /// Presents what was copied into the back buffer (Present's second half, for a caller
        /// that must release `source` between the copy and Present: the keyed mutex).
        public bool PresentCopied(int interval)
        {
            LastWait = 0;
            if (Failure != null) return false;
            presents++;
            if (resync)
            {
                for (int i = 0; i < 16 && Dxgi.WaitForSingleObjectEx(waitable, 0, 1) == Dxgi.WaitObject0; i++) { }
                resync = false;
            }
            uint sync = (uint)Math.Clamp(interval, 0, 4);
            uint flags = sync == 0 && tearing ? Dxgi.PresentAllowTearing : 0;
            int hr = Faulty("removed", presents) ? Dxgi.ErrorDeviceRemoved : Dxgi.Present(swapChain, sync, flags);
            bool nowOccluded = hr == Dxgi.StatusOccluded;
            if (nowOccluded != occluded && PresentTrace.Enabled) PresentTrace.Log($"dxgi: present {presents}: {(nowOccluded ? "occluded (not shown)" : "shown again")}");
            occluded = nowOccluded;
            if (Dxgi.Failed(hr)) return Fail($"Present failed ({Dxgi.Hr(hr)}{DeviceState(hr)})");
            if (nowOccluded) resync = true;
            else
            {
                long t = GlTimings.Now;
                if (Dxgi.WaitForSingleObjectEx(waitable, WaitMilliseconds, 1) != Dxgi.WaitObject0)
                {
                    resync = true;
                    if (waitTimeouts++ < 3) EngineLog.Engine($"present: DXGI picture {presents} not on its way after {WaitMilliseconds} ms; going on");
                }
                LastWait = GlTimings.Now - t;
            }
            return true;
        }

        /// Copies into the back buffer without presenting (see PresentCopied).
        public void CopyToBackBuffer(void* source)
        {
            var box = new Dxgi.Box { Right = (uint)Width, Bottom = (uint)Height, Back = 1 };
            Dxgi.CopySubresourceRegion(context, backBuffer, 0, 0, 0, 0, source, 0, &box);
        }

        /// Releases everything, in the reverse order of making it. Safe after a failure at any
        /// step, and more than once. The caller releases its shared texture first.
        public void Dispose()
        {
            Dxgi.Release(ref backBuffer);
            Dxgi.Release(ref swapChain2);
            Dxgi.Release(ref swapChain);
            if (context != null)
            {
                Dxgi.ClearState(context);
                Dxgi.Flush(context);
                Dxgi.Release(ref context);
            }
            Dxgi.Release(ref device);
            Dxgi.Release(ref factory);
            if (waitable != IntPtr.Zero)
            {
                Dxgi.CloseHandle(waitable);
                waitable = IntPtr.Zero;
            }
        }
    }
}
