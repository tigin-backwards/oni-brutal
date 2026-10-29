using System;
using System.Collections.Generic;
using Brutal.GlfwApi;
using OniMoono.Engine;
using static OniMoono.Desktop.Gl.GlConst;
using N = Brutal.OpenGlApi.OpenGl.Native;

namespace OniMoono.Desktop.Gl
{
    /// Windows: the window's picture goes to the screen through a Direct3D 11 flip-model swap
    /// chain, as the original's (Unity 6 on D3D11) does, so that Windows knows it is SDR: OpenGL's
    /// own presentation has no swap chain Windows can see, and Auto HDR, RTX HDR or the driver's
    /// "layered on DXGI" present can then treat the game as HDR content. ARCHITECTURE §3.5,
    /// "Presentation on Windows".
    ///
    /// The presenter draws into a GL texture that is a D3D11 texture of ours, shared through
    /// WGL_NV_DX_interop2 (locked while GL draws into it); Show copies it into the swap chain's
    /// back buffer (CopySubresourceRegion) and presents. The swap chain's own buffers are never
    /// registered with GL: re-registering them after a resize is suspected of a use-after-free
    /// in NVIDIA's driver (Godot PR #94503). The shared texture is registered once and made anew
    /// only when the window outgrows it.
    ///
    /// Threads: everything here is made, used and released on the presenter thread, with the
    /// window's GL context current; no other thread holds any of it. DXGI is told to leave the
    /// window alone (DXGI_MWA_NO_WINDOW_CHANGES | NO_ALT_ENTER) and the swap chain is never put
    /// in DXGI's fullscreen state, so neither Present nor ResizeBuffers sends the window a message
    /// that the main thread (the window's thread, which may be waiting on the GL thread, which
    /// may be waiting on this one) would have to answer. A window resized by the main thread is
    /// stretched by DXGI (DXGI_SCALING_STRETCH) until the next picture resizes the buffers.
    internal sealed unsafe class DxgiTarget : IWindowTarget
    {
        /// Present through DXGI (the Moono Tweaks screen's "dxgi" row, `--dxgi on|off`); set by the
        /// host before the device starts.
        public static bool Enabled;

        /// Diagnostics (`--dxgi-fault WHAT[,WHAT]`): failures made up, for testing the ways back:
        /// "extension" (no WGL_NV_DX_interop2), "adapter", "device", "swapchain", "register"
        /// (at start-up: OpenGL's presentation instead), "lock:N", "resize:N" and "removed:N"
        /// (the first target's Nth lock, resize or Present failing; DXGI is then restarted), and
        /// "norestart" (every restart fails too). Each happens once, so a restart succeeds.
        public static string Fault;

        static bool faultDone;

        static bool HasFault(string token) => Fault != null && Array.IndexOf(Fault.Split(','), token) >= 0;

        /// The made-up fault at `step` (start-up), once.
        static bool Faulty(string step)
        {
            if (faultDone || !HasFault(step)) return false;
            faultDone = true;
            return true;
        }

        /// The made-up fault at the Nth time of `step` (1-based), once.
        static bool Faulty(string step, int count)
        {
            if (faultDone || !HasFault($"{step}:{count}")) return false;
            faultDone = true;
            return true;
        }

        const uint WglAccessWriteDiscard = 2;
        const uint BufferCount = 2;
        const uint MaximumFrameLatency = 1;
        /// How long Show waits for a picture to be on its way before going on regardless.
        const uint WaitMilliseconds = 1000;

        readonly IntPtr hwnd;
        // COM objects (one reference each, released once in Dispose).
        void* factory, device, context, swapChain, swapChain2, backBuffer, shared;
        // Win32 handles: the frame-latency waitable object; the interop device and object.
        IntPtr waitable, interopDevice, interopObject;
        // GL objects: the texture registered with `shared`, and the framebuffer it is attached to.
        uint texture, framebuffer, attached;
        int width, height, capacityWidth, capacityHeight;
        uint swapFlags;
        bool tearing, locked, occluded;
        /// The waitable object's count may be off by pictures not waited for (it starts at the
        /// latency; an occluded picture; a wait that timed out): taken down to nothing before the
        /// next Present.
        bool resync = true;
        int begins, resizes, presents, waitTimeouts;
        string adapterName = "";

        public long AdapterLuid { get; private set; }
        public string Name => "DXGI flip model";
        public uint Framebuffer => framebuffer;
        public bool FlipY => true;
        public string Failure { get; private set; }

        delegate* unmanaged<void*, IntPtr> dxOpenDevice;
        delegate* unmanaged<IntPtr, int> dxCloseDevice;
        delegate* unmanaged<IntPtr, void*, uint, uint, uint, IntPtr> dxRegisterObject;
        delegate* unmanaged<IntPtr, IntPtr, int> dxUnregisterObject;
        delegate* unmanaged<IntPtr, int, IntPtr*, int> dxLockObjects, dxUnlockObjects;

        DxgiTarget(IntPtr hwnd) => this.hwnd = hwnd;

        /// The target for window `hwnd`, whose GL context is current, for a first picture of
        /// `width` x `height`; null, with the reason, when any step fails (what was made is
        /// released).
        public static DxgiTarget Create(IntPtr hwnd, int width, int height, out string reason)
        {
            var target = new DxgiTarget(hwnd);
            try
            {
                reason = target.Start(Math.Max(1, width), Math.Max(1, height));
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                reason = e.Message;
            }
            if (reason == null) return target;
            target.Dispose();
            return null;
        }

        string Start(int w, int h)
        {
            if (faultDone && HasFault("norestart")) return "a restart that fails";
            if (Faulty("extension") || !Glfw.ExtensionSupported("WGL_NV_DX_interop2")) return "the OpenGL driver has no WGL_NV_DX_interop2";
            dxOpenDevice = (delegate* unmanaged<void*, IntPtr>)Glfw.GetProcAddress("wglDXOpenDeviceNV"u8);
            dxCloseDevice = (delegate* unmanaged<IntPtr, int>)Glfw.GetProcAddress("wglDXCloseDeviceNV"u8);
            dxRegisterObject = (delegate* unmanaged<IntPtr, void*, uint, uint, uint, IntPtr>)Glfw.GetProcAddress("wglDXRegisterObjectNV"u8);
            dxUnregisterObject = (delegate* unmanaged<IntPtr, IntPtr, int>)Glfw.GetProcAddress("wglDXUnregisterObjectNV"u8);
            dxLockObjects = (delegate* unmanaged<IntPtr, int, IntPtr*, int>)Glfw.GetProcAddress("wglDXLockObjectsNV"u8);
            dxUnlockObjects = (delegate* unmanaged<IntPtr, int, IntPtr*, int>)Glfw.GetProcAddress("wglDXUnlockObjectsNV"u8);
            if (dxOpenDevice == null || dxCloseDevice == null || dxRegisterObject == null || dxUnregisterObject == null || dxLockObjects == null || dxUnlockObjects == null)
                return "WGL_NV_DX_interop2's entry points are missing";

            var iid = Dxgi.IIDFactory2;
            void* f;
            int hr = Dxgi.CreateDXGIFactory2(0, &iid, &f);
            if (Dxgi.Failed(hr)) return $"CreateDXGIFactory2 failed ({Dxgi.Hr(hr)})";
            factory = f;

            void* adapter = FindAdapter(out string how);
            if (adapter == null) return how;
            void* d = null, c = null;
            int level = 0;
            hr = Faulty("device") ? unchecked((int)0x887A0004) : Dxgi.D3D11CreateDevice(adapter, Dxgi.DriverTypeUnknown, IntPtr.Zero, 0, null, 0, Dxgi.D3D11SdkVersion, &d, &level, &c);
            Dxgi.Release(ref adapter);
            if (Dxgi.Failed(hr)) return $"D3D11CreateDevice on {adapterName} failed ({Dxgi.Hr(hr)})";
            device = d;
            context = c;

            interopDevice = dxOpenDevice(device);
            if (interopDevice == IntPtr.Zero) return $"wglDXOpenDeviceNV failed (error 0x{Dxgi.GetLastError():X})";

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
            width = w;
            height = h;
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
            if (!MakeShared(w, h)) return Failure;
            // The whole round once, cleared to black, so that a driver that cannot do it fails
            // here rather than on the first frame.
            if (!Lock()) return Failure;
            GlApi.BindFramebuffer(DrawFramebuffer, framebuffer);
            Attach();
            uint status = N.glCheckFramebufferStatus(DrawFramebuffer).Id;
            N.glClearColor(0f, 0f, 0f, 1f);
            N.glClear(ColorBufferBit);
            if (!Unlock()) return Failure;
            if (status != FramebufferComplete) return $"the shared texture's framebuffer is incomplete (0x{status:X})";

            var actual = new Dxgi.SwapChainDesc1();
            hr = Dxgi.GetDesc1(swapChain, &actual);
            if (Dxgi.Failed(hr)) return $"IDXGISwapChain1::GetDesc1 failed ({Dxgi.Hr(hr)})";
            EngineLog.Engine($"present: DXGI on {adapterName} ({how}), feature level {level >> 12}_{(level >> 8) & 0xF}: swap chain {actual.Width}x{actual.Height} "
                + $"{(actual.Format == Dxgi.FormatR8G8B8A8Unorm ? "R8G8B8A8_UNORM" : actual.Format.ToString())}, "
                + $"{(actual.SwapEffect == Dxgi.SwapEffectFlipDiscard ? "FLIP_DISCARD" : actual.SwapEffect == Dxgi.SwapEffectFlipSequential ? "FLIP_SEQUENTIAL" : actual.SwapEffect.ToString())}, "
                + $"{actual.BufferCount} buffers, colour space RGB_FULL_G22_NONE_P709 (default, SDR), scaling {(actual.Scaling == Dxgi.ScalingStretch ? "STRETCH" : actual.Scaling.ToString())}, "
                + $"maximum frame latency {MaximumFrameLatency} (waitable), tearing without vsync {(tearing ? "allowed" : "not supported")}; "
                + $"shared texture {capacityWidth}x{capacityHeight} (WGL_NV_DX_interop2)");
            return null;
        }

        /// The adapter the GL context renders on (a reference the caller releases): by the LUID
        /// the GL driver gives (EXT_external_objects_win32), else by GL_VENDOR and GL_RENDERER
        /// (Dxgi.MatchAdapter). Null, with the reason in `how`, when none matches.
        void* FindAdapter(out string how)
        {
            how = null;
            if (Faulty("adapter"))
            {
                how = "no DXGI adapter matches the OpenGL renderer";
                return null;
            }
            if (GlPrograms.Extensions.Contains("GL_EXT_memory_object_win32") || GlPrograms.Extensions.Contains("GL_EXT_semaphore_win32"))
            {
                var getUnsignedBytes = (delegate* unmanaged<uint, byte*, void>)Glfw.GetProcAddress("glGetUnsignedBytevEXT"u8);
                long luid = 0;
                const uint DeviceLuid = 0x9599;
                if (getUnsignedBytes != null) getUnsignedBytes(DeviceLuid, (byte*)&luid);
                void* f4, a;
                if (luid != 0 && !Dxgi.Failed(Dxgi.QueryInterface(factory, Dxgi.IIDFactory4, &f4)))
                {
                    int hr = Dxgi.EnumAdapterByLuid(f4, luid, Dxgi.IIDAdapter1, &a);
                    Dxgi.Release(ref f4);
                    if (!Dxgi.Failed(hr))
                    {
                        Dxgi.AdapterDesc1 desc;
                        if (!Dxgi.Failed(Dxgi.GetDesc1(a, &desc))) adapterName = desc.Name;
                        AdapterLuid = luid;
                        how = "the OpenGL context's LUID";
                        return a;
                    }
                }
            }
            string glVendor = GlApi.GetString(Vendor), glRenderer = GlApi.GetString(RendererName);
            var found = new List<(uint, string, bool)>();
            var luids = new List<long>();
            for (uint i = 0; ; i++)
            {
                void* a;
                if (Dxgi.Failed(Dxgi.EnumAdapters1(factory, i, &a))) break;
                Dxgi.AdapterDesc1 desc = default;
                bool ok = !Dxgi.Failed(Dxgi.GetDesc1(a, &desc));
                found.Add(ok ? (desc.VendorId, desc.Name, (desc.Flags & Dxgi.AdapterFlagSoftware) != 0) : (0u, "", true));
                luids.Add(desc.Luid);
                Dxgi.Release(ref a);
            }
            int index = Dxgi.MatchAdapter(glVendor, glRenderer, found);
            if (index < 0)
            {
                how = $"no DXGI adapter matches the OpenGL renderer \"{glRenderer}\" ({glVendor}); adapters: {string.Join(", ", found.ConvertAll(f => f.Item2))}";
                return null;
            }
            void* chosen;
            if (Dxgi.Failed(Dxgi.EnumAdapters1(factory, (uint)index, &chosen)))
            {
                how = "the matching DXGI adapter went away";
                return null;
            }
            adapterName = found[index].Item2;
            AdapterLuid = luids[index];
            how = "matched by GL_RENDERER";
            return chosen;
        }

        bool Fail(string reason)
        {
            Failure ??= reason;
            return false;
        }

        string DeviceState(int hr)
            => Dxgi.DeviceLost(hr) && device != null ? $"; device removed reason {Dxgi.Hr(Dxgi.GetDeviceRemovedReason(device))}" : "";

        bool GetBackBuffer()
        {
            void* b;
            int hr = Dxgi.GetBuffer(swapChain, 0, Dxgi.IIDTexture2D, &b);
            if (Dxgi.Failed(hr)) return Fail($"GetBuffer failed ({Dxgi.Hr(hr)}{DeviceState(hr)})");
            backBuffer = b;
            return true;
        }

        /// The shared texture, at least `w` x `h`, made anew (registered with GL) when the one
        /// there is smaller.
        bool MakeShared(int w, int h)
        {
            ReleaseShared();
            capacityWidth = capacityWidth == 0 ? w : Dxgi.Capacity(capacityWidth, w);
            capacityHeight = capacityHeight == 0 ? h : Dxgi.Capacity(capacityHeight, h);
            var desc = new Dxgi.Texture2DDesc
            {
                Width = (uint)capacityWidth, Height = (uint)capacityHeight, MipLevels = 1, ArraySize = 1, Format = Dxgi.FormatR8G8B8A8Unorm,
                SampleCount = 1, BindFlags = Dxgi.BindRenderTarget | Dxgi.BindShaderResource,
            };
            void* t;
            int hr = Dxgi.CreateTexture2D(device, &desc, &t);
            if (Dxgi.Failed(hr)) return Fail($"CreateTexture2D {capacityWidth}x{capacityHeight} failed ({Dxgi.Hr(hr)}{DeviceState(hr)})");
            shared = t;
            texture = GlApi.GenTexture();
            interopObject = Faulty("register") ? IntPtr.Zero : dxRegisterObject(interopDevice, shared, texture, TexTarget2D, WglAccessWriteDiscard);
            if (interopObject == IntPtr.Zero) return Fail($"wglDXRegisterObjectNV failed (error 0x{Dxgi.GetLastError():X})");
            if (PresentTrace.Enabled) PresentTrace.Log($"dxgi: shared texture {capacityWidth}x{capacityHeight} for a {w}x{h} picture");
            return true;
        }

        void ReleaseShared()
        {
            if (interopObject != IntPtr.Zero)
            {
                if (locked) Unlock();
                // Fails once the device is lost; the object is gone with it then.
                if (dxUnregisterObject(interopDevice, interopObject) == 0)
                    EngineLog.Engine($"present: wglDXUnregisterObjectNV failed (error 0x{Dxgi.GetLastError():X})");
                interopObject = IntPtr.Zero;
            }
            if (texture != 0)
            {
                GlApi.DeleteTexture(texture);
                texture = 0;
                // The next texture may get the same name: it is attached anew all the same.
                attached = 0;
            }
            Dxgi.Release(ref shared);
        }

        bool Lock()
        {
            var o = interopObject;
            if (Faulty("lock", begins) || dxLockObjects(interopDevice, 1, &o) == 0) return Fail($"wglDXLockObjectsNV failed (error 0x{Dxgi.GetLastError():X})");
            locked = true;
            return true;
        }

        bool Unlock()
        {
            var o = interopObject;
            locked = false;
            if (dxUnlockObjects(interopDevice, 1, &o) == 0) return Fail($"wglDXUnlockObjectsNV failed (error 0x{Dxgi.GetLastError():X})");
            return true;
        }

        /// The shared texture on the framebuffer (bound for drawing), once per texture.
        void Attach()
        {
            if (attached == texture) return;
            if (framebuffer == 0) framebuffer = GlApi.GenFramebuffer();
            GlApi.BindFramebuffer(DrawFramebuffer, framebuffer);
            N.glFramebufferTexture2D(DrawFramebuffer, ColorAttachment0, TexTarget2D, texture, 0);
            attached = texture;
        }

        bool Resize(int w, int h)
        {
            resizes++;
            Dxgi.Release(ref backBuffer);
            // Nothing of the swap chain's may still be referenced, bound or queued.
            Dxgi.ClearState(context);
            Dxgi.Flush(context);
            int hr = Faulty("resize", resizes) ? Dxgi.ErrorDeviceRemoved : Dxgi.ResizeBuffers(swapChain, 0, (uint)w, (uint)h, 0, swapFlags);
            if (Dxgi.Failed(hr)) return Fail($"ResizeBuffers to {w}x{h} failed ({Dxgi.Hr(hr)}{DeviceState(hr)})");
            if (PresentTrace.Enabled) PresentTrace.Log($"dxgi: buffers resized {width}x{height} -> {w}x{h}");
            width = w;
            height = h;
            return GetBackBuffer();
        }

        public bool Begin(int w, int h)
        {
            if (Failure != null) return false;
            begins++;
            w = Math.Max(1, w);
            h = Math.Max(1, h);
            if ((w != width || h != height) && !Resize(w, h)) return false;
            if ((w > capacityWidth || h > capacityHeight) && !MakeShared(w, h)) return false;
            if (!Lock()) return false;
            GlApi.BindFramebuffer(DrawFramebuffer, framebuffer);
            Attach();
            return true;
        }

        public long LastWait { get; private set; }

        public bool Show(int interval, bool again)
        {
            LastWait = 0;
            if (Failure != null) return false;
            if (!Unlock()) return false;
            var box = new Dxgi.Box { Right = (uint)width, Bottom = (uint)height, Back = 1 };
            Dxgi.CopySubresourceRegion(context, backBuffer, 0, 0, 0, 0, shared, 0, &box);
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
            // Done once the picture is on its way (one picture in flight, as the SwapBuffers path
            // has with vsync): the waitable object is signalled as each presented picture
            // retires. An occluded picture was not queued.
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

        /// Releases everything, in the reverse order of making it (presenter thread, the window's
        /// context current). Safe after a failure at any step, and more than once.
        public void Dispose()
        {
            ReleaseShared();
            if (framebuffer != 0)
            {
                GlApi.DeleteFramebuffer(framebuffer);
                framebuffer = attached = 0;
            }
            if (interopDevice != IntPtr.Zero)
            {
                if (dxCloseDevice(interopDevice) == 0) EngineLog.Engine($"present: wglDXCloseDeviceNV failed (error 0x{Dxgi.GetLastError():X})");
                interopDevice = IntPtr.Zero;
            }
            Dxgi.Release(ref shared);
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
