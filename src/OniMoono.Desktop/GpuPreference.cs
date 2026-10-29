using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using OniMoono.Engine;

namespace OniMoono.Desktop
{
    /// Which GPU the player gets on a laptop with two of them.
    ///
    /// A hybrid laptop (NVIDIA Optimus, AMD switchable graphics, Intel plus a discrete card) starts
    /// an application that asks for nothing on the integrated GPU: the discrete card costs battery,
    /// so it is opt-in, and the driver decides per application. Unity's player was linked with the
    /// two exported flags the drivers look for (`NvOptimusEnablement`,
    /// `AmdPowerXpressRequestHighPerformance`), which is why the original game runs on the discrete
    /// GPU. A .NET host cannot export data symbols from its executable, so this player asks the
    /// system for the high-performance GPU instead, before GLFW opens the window and the driver
    /// picks an adapter for its context:
    ///
    /// - **Windows**: the per-application preference behind Settings > System > Display > Graphics
    ///   (`HKCU\Software\Microsoft\DirectX\UserGpuPreferences`), which the display drivers read for
    ///   OpenGL as well as Direct3D. An entry the player already has is left as it is: either an
    ///   earlier run wrote it, or the player (or a user who chose the power-saving GPU there) did.
    /// - **Linux**: the variables the drivers read when they load. NVIDIA's proprietary driver
    ///   needs PRIME render offload (`__NV_PRIME_RENDER_OFFLOAD`, `__GLX_VENDOR_LIBRARY_NAME`),
    ///   Mesa (Intel, AMD, nouveau) needs `DRI_PRIME`. This runs before the first GL call, so they
    ///   are in place before libGL is loaded. Variables that are already set are left alone: they
    ///   are a user's or a launcher's choice.
    ///
    /// `ONI_MOONO_GPU=default` turns it off, for a machine whose discrete GPU is the slower one or
    /// whose driver is broken. Either way the log says `OpenGL: … / <renderer> / <vendor>`
    /// (GlDevice), so the run shows which GPU it ended up on.
    internal static class GpuPreference
    {
        static bool requested;

        /// Asks for the high-performance GPU, once (later calls do nothing).
        public static void RequestHighPerformance()
        {
            if (requested) return;
            requested = true;
            var setting = Environment.GetEnvironmentVariable("ONI_MOONO_GPU");
            if (!Wanted(setting))
            {
                EngineLog.Engine($"gpu: ONI_MOONO_GPU={setting}: the driver chooses, as without Moono");
                return;
            }
            if (OperatingSystem.IsWindows()) RequestOnWindows();
            else if (OperatingSystem.IsLinux()) RequestOnLinux();
        }

        /// Whether the setting asks for the high-performance GPU. The value names the GPU as the
        /// Windows graphics settings page does (high performance, power saving), with `system` and
        /// `off` for the arithmetic.
        internal static bool Wanted(string setting)
        {
            if (string.IsNullOrWhiteSpace(setting)) return true;
            return setting.Trim().ToLowerInvariant() switch
            {
                "default" or "low" or "integrated" or "power-saving" or "system" or "off" or "0" => false,
                _ => true,
            };
        }

        // ----- Windows -----

        const string UserGpuPreferencesKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
        /// `GpuPreference`: 0 lets Windows decide (what the settings page writes), 1 is the
        /// power-saving GPU, 2 the high-performance one.
        const string HighPerformance = "GpuPreference=2;";
        static readonly IntPtr HkeyCurrentUser = unchecked((IntPtr)(long)(int)0x80000001);
        const int KeyQueryValue = 0x0001, KeySetValue = 0x0002;
        const int RegSz = 1, ErrorSuccess = 0, ErrorMoreData = 234;

        static void RequestOnWindows()
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                EngineLog.Engine("gpu: no executable path to name in the GPU preference; the driver chooses");
                return;
            }
            // The preference names a program, and the driver reads it for the process's own image.
            // Started as `dotnet OniMoono.dll` (a development run) that image is the .NET host, and
            // an entry for it would apply to every .NET application on the machine.
            if (string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                EngineLog.Engine("gpu: run through the dotnet host, so there is no program to name; the driver chooses");
                return;
            }
            int status = RegCreateKeyExW(HkeyCurrentUser, UserGpuPreferencesKey, 0, null, 0, KeyQueryValue | KeySetValue, IntPtr.Zero, out var key, out _);
            if (status != ErrorSuccess)
            {
                EngineLog.Engine($"gpu: could not open HKCU\\{UserGpuPreferencesKey} (error {status}); the driver chooses");
                return;
            }
            try
            {
                var existing = ReadPreference(key, exe);
                if (existing == null)
                {
                    EngineLog.Engine($"gpu: {exe} has a GPU preference of another kind; left as it is");
                    return;
                }
                var value = WithHighPerformance(existing);
                if (value == null)
                {
                    EngineLog.Engine($"gpu: Windows has a GPU preference for {exe} already ({existing.TrimEnd(';')}); left as it is");
                    return;
                }
                var bytes = Encoding.Unicode.GetBytes(value + "\0");
                status = RegSetValueExW(key, exe, 0, RegSz, bytes, bytes.Length);
                if (status != ErrorSuccess)
                    EngineLog.Engine($"gpu: could not write the GPU preference for {exe} (error {status}); the driver chooses");
                else
                    EngineLog.Engine($"gpu: asked Windows for the high-performance GPU ({exe} -> {value})");
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        /// The entry's value with the high-performance preference added, or null when it already
        /// names a GPU (the user's choice, or an earlier run's).
        internal static string WithHighPerformance(string existing)
        {
            existing ??= "";
            if (existing.Contains("GpuPreference=", StringComparison.OrdinalIgnoreCase)) return null;
            // The entry holds other settings too (`SwapEffectUpgradeEnable=1;`, …) in the same
            // semicolon-terminated list; keep them.
            return existing.Length > 0 && !existing.EndsWith(';') ? existing + ";" + HighPerformance : existing + HighPerformance;
        }

        /// The entry's value (empty when it has none), or null when it has one that is not a
        /// string: that is not ours to rewrite.
        static string ReadPreference(IntPtr key, string exe)
        {
            int size = 0;
            int status = RegQueryValueExW(key, exe, IntPtr.Zero, out var type, null, ref size);
            if ((status != ErrorSuccess && status != ErrorMoreData) || size <= 0) return "";
            var data = new byte[size];
            status = RegQueryValueExW(key, exe, IntPtr.Zero, out type, data, ref size);
            if (status != ErrorSuccess) return "";
            if (type != RegSz) return null;
            return Encoding.Unicode.GetString(data, 0, size).TrimEnd('\0');
        }

        [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        static extern int RegCreateKeyExW(IntPtr key, string subKey, int reserved, string className, int options, int access, IntPtr security, out IntPtr result, out int disposition);

        [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        static extern int RegQueryValueExW(IntPtr key, string valueName, IntPtr reserved, out int type, [System.Runtime.InteropServices.Out] byte[] data, ref int size);

        [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        static extern int RegSetValueExW(IntPtr key, string valueName, int reserved, int type, byte[] data, int size);

        [System.Runtime.InteropServices.DllImport("advapi32.dll")]
        static extern int RegCloseKey(IntPtr key);

        // ----- Linux -----

        static void RequestOnLinux()
        {
            int cards = DisplayCards();
            // The proprietary driver: its kernel module, and the GLX vendor library that came with
            // it. Both, because asking GLX for a vendor library the machine does not have (or
            // cannot load) stops GLX working at all, which is worse than the integrated GPU.
            bool nvidia = File.Exists("/proc/driver/nvidia/version") && NativeLibrary.TryLoad("libGLX_nvidia.so.0", out _);
            var variables = Variables(nvidia, cards);
            if (variables.Length == 0)
            {
                EngineLog.Engine($"gpu: {cards} display card{(cards == 1 ? "" : "s")} and no NVIDIA GLX driver; the driver chooses");
                return;
            }
            foreach (var (name, value) in variables) Set(name, value);
        }

        /// The variables to set, for the machine `Variables` is given: NVIDIA's PRIME render
        /// offload when its (proprietary) driver is installed, else Mesa's `DRI_PRIME` when the
        /// machine has a second card to render on.
        internal static (string Name, string Value)[] Variables(bool nvidiaDriver, int displayCards)
        {
            // The vendor library is named only when that driver is installed and loaded: on a
            // machine without it, asking GLX for it stops GLX working at all.
            if (nvidiaDriver) return [("__NV_PRIME_RENDER_OFFLOAD", "1"), ("__GLX_VENDOR_LIBRARY_NAME", "nvidia")];
            // `DRI_PRIME=1` renders with the second GPU in Mesa's list, which on a hybrid laptop is
            // the discrete one; on one card it would be the same device through a copy.
            if (displayCards > 1) return [("DRI_PRIME", "1")];
            return [];
        }

        static void Set(string name, string value)
        {
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } existing)
            {
                EngineLog.Engine($"gpu: {name}={existing} was set already; left as it is");
                return;
            }
            Environment.SetEnvironmentVariable(name, value);
            EngineLog.Engine($"gpu: {name}={value}, for the discrete GPU");
        }

        /// The display cards the system has (each is a GPU that can render), from sysfs.
        static int DisplayCards()
        {
            try
            {
                int cards = 0;
                foreach (var path in Directory.EnumerateDirectories("/sys/class/drm"))
                {
                    // `card0`, `card1`, … (`card0-eDP-1` is a connector, not a card).
                    var name = Path.GetFileName(path);
                    if (!name.StartsWith("card", StringComparison.Ordinal) || !int.TryParse(name.AsSpan(4), out _)) continue;
                    if (File.Exists(Path.Combine(path, "device", "vendor"))) cards++;
                }
                return cards;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return 0;
            }
        }
    }
}
