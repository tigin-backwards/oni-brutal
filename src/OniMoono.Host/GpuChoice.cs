#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace OniMoono.Host
{
    /// The GPU option for laptops with two GPUs (the Moono Tweaks screen's "Graphics card" row,
    /// settings.json's "gpu", `--gpu`): which GPU the OpenGL driver should pick, applied before
    /// GLFW loads it (Program.Main, before Player.Run opens the window). ARCHITECTURE §2.
    ///
    /// Windows: OniMoono.exe (src/OniMoono.NativeHost) exports NvOptimusEnablement and
    /// AmdPowerXpressRequestHighPerformance with the value 1, as the original game's exe does;
    /// NVIDIA's and AMD's drivers read them from the main executable when the OpenGL driver loads,
    /// and Windows' own Graphics settings page overrides them. "default" leaves them at 1 (the
    /// original's behaviour), "discrete" writes 1 and "integrated" 0.
    ///
    /// Linux: the original sets nothing, nor does "default". "discrete" asks for PRIME render
    /// offload (NVIDIA's proprietary driver when it is there, else Mesa's DRI_PRIME=1);
    /// "integrated" sets DRI_PRIME=0. The same variables steer Vulkan's GPU order (NVIDIA's
    /// VK_LAYER_NV_optimus, loaded by __NV_PRIME_RENDER_OFFLOAD and sorted by
    /// __VK_LAYER_NV_optimus; Mesa's device-select layer reads DRI_PRIME), while the Vulkan
    /// device picks its GPU by type itself (VkContext.Choose), whatever the order. Variables are set in the native environment (libc setenv:
    /// .NET's Environment.SetEnvironmentVariable changes only its own copy on Unix, which libGL
    /// never reads) and in .NET's, for processes the player starts. One the user set is never
    /// changed; those Moono set are named in ONI_MOONO_GPU_ENV, so that the run App.Restart starts
    /// (which inherits them) replaces them with its own choice.
    internal static class GpuChoice
    {
        public const string Default = "default", Discrete = "discrete", Integrated = "integrated";
        public static readonly string[] Choices = [Default, Discrete, Integrated];
        public static bool IsChoice(string value) => Array.IndexOf(Choices, value) >= 0;

        /// The variables set by Moono in this environment (inherited from the run before a restart).
        public const string MarkerVariable = "ONI_MOONO_GPU_ENV";
        public const string NvOptimus = "NvOptimusEnablement", AmdPowerXpress = "AmdPowerXpressRequestHighPerformance";

        /// Windows: the value both exports get, or null to leave them as the exe has them (1).
        public static uint? WindowsExportValue(string choice) => choice switch
        {
            Discrete => 1,
            Integrated => 0,
            _ => null,
        };

        /// Linux: the variables a choice asks for. `nvidiaOffload`: NVIDIA's proprietary driver is
        /// installed (it offloads with its own variables, which Mesa ignores).
        public static IReadOnlyList<(string Name, string Value)> LinuxVariables(string choice, bool nvidiaOffload) => choice switch
        {
            Discrete when nvidiaOffload => [("__NV_PRIME_RENDER_OFFLOAD", "1"), ("__GLX_VENDOR_LIBRARY_NAME", "nvidia"), ("__VK_LAYER_NV_optimus", "NVIDIA_only")],
            Discrete => [("DRI_PRIME", "1")],
            Integrated when nvidiaOffload => [("DRI_PRIME", "0"), ("__VK_LAYER_NV_optimus", "non_NVIDIA_only")],
            Integrated => [("DRI_PRIME", "0")],
            _ => [],
        };

        /// What to do to the environment: the variables to set (null value: to unset, one an
        /// earlier Moono run set that this choice doesn't want), those left as the user set them,
        /// and the marker's new value (null: unset).
        public sealed record LinuxPlan(IReadOnlyList<(string Name, string? Value)> Changes, IReadOnlyList<(string Name, string Value)> UserSet, string? Marker);

        public static LinuxPlan PlanLinux(string choice, bool nvidiaOffload, Func<string, string?> getenv)
        {
            var ours = (getenv(MarkerVariable) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
            var changes = new List<(string, string?)>();
            var userSet = new List<(string, string)>();
            var set = new List<string>();
            var wanted = LinuxVariables(choice, nvidiaOffload);
            foreach (var (name, value) in wanted)
            {
                if (getenv(name) is { } existing && !ours.Contains(name)) userSet.Add((name, existing));
                else
                {
                    changes.Add((name, value));
                    set.Add(name);
                }
            }
            foreach (var name in ours.Order(StringComparer.Ordinal))
                if (!wanted.Any(w => w.Name == name) && getenv(name) != null) changes.Add((name, null));
            return new LinuxPlan(changes, userSet, set.Count > 0 ? string.Join(",", set) : null);
        }

        /// Applies the choice to this process, returning the log lines.
        public static IReadOnlyList<string> Apply(string choice)
        {
            if (OperatingSystem.IsWindows()) return ApplyWindows(choice);
            if (OperatingSystem.IsLinux()) return ApplyLinux(choice, NvidiaOffloadAvailable());
            return [$"gpu: {choice}: nothing to set on this platform"];
        }

        static IReadOnlyList<string> ApplyWindows(string choice)
        {
            var exe = GetModuleHandleW(IntPtr.Zero);
            IntPtr nv = GetProcAddress(exe, NvOptimus), amd = GetProcAddress(exe, AmdPowerXpress);
            if (nv == IntPtr.Zero || amd == IntPtr.Zero)
                return [$"gpu: {choice}: OniMoono.exe has no {NvOptimus}/{AmdPowerXpress} exports (the .NET SDK's apphost, from a build without the Visual Studio C++ tools), so the driver picks the GPU"];
            if (WindowsExportValue(choice) is uint value)
            {
                Marshal.WriteInt32(nv, (int)value);
                Marshal.WriteInt32(amd, (int)value);
            }
            return [$"gpu: {choice}: {NvOptimus}={(uint)Marshal.ReadInt32(nv)}, {AmdPowerXpress}={(uint)Marshal.ReadInt32(amd)} (Windows' Graphics settings override these)"];
        }

        internal static IReadOnlyList<string> ApplyLinux(string choice, bool nvidiaOffload)
        {
            var plan = PlanLinux(choice, nvidiaOffload, NativeGetenv);
            var log = new List<string>();
            foreach (var (name, value) in plan.Changes)
            {
                SetBoth(name, value);
                log.Add(value != null ? $"gpu: {choice}: set {name}={value}" : $"gpu: {choice}: unset {name} (set by Moono before the restart)");
            }
            foreach (var (name, value) in plan.UserSet) log.Add($"gpu: {choice}: left {name}={value} as it was set");
            SetBoth(MarkerVariable, plan.Marker);
            if (log.Count == 0) log.Add($"gpu: {choice}: nothing set");
            else if (choice == Discrete) log.Add($"gpu: {choice}: {(nvidiaOffload ? "NVIDIA's PRIME render offload" : "Mesa's PRIME offload (DRI_PRIME)")}");
            return log;
        }

        /// NVIDIA's proprietary driver, whose PRIME render offload the discrete choice asks for:
        /// its kernel module is loaded and its GLX vendor library is there.
        static bool NvidiaOffloadAvailable()
        {
            if (!File.Exists("/proc/driver/nvidia/version")) return false;
            if (!NativeLibrary.TryLoad("libGLX_nvidia.so.0", out var library)) return false;
            NativeLibrary.Free(library);
            return true;
        }

        /// The native environment (what libGL and Mesa read with getenv) and .NET's.
        internal static void SetBoth(string name, string? value)
        {
            if (value != null) setenv(name, value, 1);
            else unsetenv(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        internal static string? NativeGetenv(string name) => Marshal.PtrToStringUTF8(getenv(name));

        [DllImport("libc")]
        static extern int setenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int overwrite);

        [DllImport("libc")]
        static extern int unsetenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport("libc")]
        static extern IntPtr getenv([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandleW(IntPtr name);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false)]
        static extern IntPtr GetProcAddress(IntPtr module, string name);
    }
}
