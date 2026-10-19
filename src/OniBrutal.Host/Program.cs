using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using OniBrutal.Engine;

namespace OniBrutal.Host
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            var options = Options.Parse(args);
            if (options == null) return 2;
            // For the Restarter next to the host (App.Restart): the arguments to start again with
            // (shell-quoted for the Linux script; as a JSON array with the process id for the
            // Windows one) and the process to wait for.
            Environment.SetEnvironmentVariable("ONI_BRUTAL_RESTART_ARGS", string.Join(" ", args.Select(a => "'" + a.Replace("'", "'\\''") + "'")));
            Environment.SetEnvironmentVariable("ONI_BRUTAL_RESTART_ARGV", System.Text.Json.JsonSerializer.Serialize(args));
            Environment.SetEnvironmentVariable("ONI_BRUTAL_RESTART_PID", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

            var dataDir = Path.Combine(options.GameDir, "OxygenNotIncluded_Data");
            if (!Directory.Exists(dataDir))
            {
                Console.Error.WriteLine($"Game data not found: {dataDir}");
                return 2;
            }

            // Code pages (IBM437 etc.), which Mono provided through I18N.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Paths.Configure(dataDir, options);
            EngineLog.Open(options.LogPath ?? EngineConfig.ConsoleLogPath);
            EngineLog.Engine($"OniBrutal host starting: game={options.GameDir} .NET={Environment.Version}");

            AssemblyPolicy.Install(dataDir, AppContext.BaseDirectory, options.UiPackages, options.ShippedUiPackages);
            var assemblies = AssemblyPolicy.PreloadScriptingAssemblies(dataDir);
            EngineLog.Engine($"Preloaded {assemblies.Count} scripting assemblies");

            if (options.CheckTypes) return CheckTypes(assemblies);
            if (options.CheckAssets) return AssetCheck.Run(dataDir, options.ListDataScripts);
            if (options.CheckBind) return BindCheck.Run(dataDir);
            if (options.CheckShaders) return ShaderCheck.Run(dataDir);
            if (options.DumpShader != null) return ShaderDump.Run(dataDir, options.DumpShader);
            if (options.DumpScript != null) return AssetCheck.Dump(dataDir, options.DumpScript);

            if (options.TraceExceptions) ExceptionTrace.Install();
            if (options.RunHome != null) RunHome.Install(options.RunHome);
            if (options.AudioWav != null) AudioCapture.Install(options.AudioWav);
            ModVersionStamp.Install();
            GameTweaks.GameTweaks.Install(options.GameTweaks);
            StateMachineSaveNames.Install(dataDir, AppContext.BaseDirectory);
            return Player.Run(options, dataDir);
        }

        /// M0 gate: every type of every loaded assembly must load, as the game's whole-domain
        /// GetTypes() scans require.
        static int CheckTypes(List<Assembly> assemblies)
        {
            int failures = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = asm.GetTypes();
                    Console.WriteLine($"ok   {asm.GetName().Name,-45} {types.Length,6} types");
                }
                catch (ReflectionTypeLoadException e)
                {
                    failures++;
                    Console.WriteLine($"FAIL {asm.GetName().Name}");
                    foreach (var le in e.LoaderExceptions)
                        if (le != null) Console.WriteLine("       " + le.Message);
                }
            }
            Console.WriteLine(failures == 0 ? "All assemblies type-load." : $"{failures} assemblies failed to type-load.");

            // Where the replaced assemblies came from.
            foreach (var name in new[] { "UnityEngine.CoreModule", "OniBrutal.UnityEngine", "com.rlabrecque.steamworks.net", "mscorlib" })
                Console.WriteLine($"from {name,-35} {Assembly.Load(name).Location}");

            failures += CheckRuntimeServices();
            return failures == 0 ? 0 : 1;
        }

        /// The services Mono provided in-box, exercised through the game's own code paths
        /// (ARCHITECTURE §2.1): Newtonsoft.Json (save header), BinaryFormatter (worldgen settings
        /// cloning), code pages (Ionic.Zip).
        static int CheckRuntimeServices()
        {
            int failures = 0;
            void Check(string what, Func<string> test)
            {
                try
                {
                    Console.WriteLine($"ok   {what}: {test()}");
                }
                catch (Exception e)
                {
                    failures++;
                    Console.WriteLine($"FAIL {what}: {(e is TargetInvocationException t ? t.InnerException : e)}");
                }
            }

            Check("Newtonsoft.Json round trip", () =>
            {
                var convert = Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert", true)!;
                var json = (string)convert.GetMethod("SerializeObject", [typeof(object)])!.Invoke(null, [new Dictionary<string, int> { ["cycle"] = 42 }])!;
                var back = convert.GetMethods().First(m => m.Name == "DeserializeObject" && m.IsGenericMethod && m.GetParameters().Length == 1)
                    .MakeGenericMethod(typeof(Dictionary<string, int>)).Invoke(null, [json]);
                return ((Dictionary<string, int>)back!)["cycle"] == 42 ? json : throw new InvalidOperationException("value lost");
            });
            Check("BinaryFormatter via ObjectCloner.SerializingCloner.Copy", () =>
            {
                var cloner = Assembly.Load("Assembly-CSharp-firstpass").GetType("ObjectCloner.SerializingCloner", true)!;
                var copy = cloner.GetMethod("Copy")!.MakeGenericMethod(typeof(List<string>)).Invoke(null, [new List<string> { "a", "b" }]);
                return string.Join(",", (List<string>)copy!);
            });
            Check("Code page IBM437 (Ionic.Zip)", () => Encoding.GetEncoding("IBM437").EncodingName);
            return failures;
        }
    }

    internal sealed class Options
    {
        public string GameDir = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "OxygenNotIncluded")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects", "oni-linux");
        /// Test runs (`--run-home DIR`): the player's per-user data (Unity's persistent and cache
        /// paths, and on Windows the Documents and local application data folders the game reads)
        /// under DIR instead of the user's (RunHome).
        public string? RunHome;
        /// Test runs (`--audio-wav DIR`): the game's audio written to DIR/fmodoutput.wav instead of
        /// the sound device (AudioCapture).
        public string? AudioWav;
        public string? LogPath;
        public bool Headless;
        public bool BatchMode;
        public bool CheckTypes;
        /// This project's replacements of Unity's packages (PLAN M9) from this directory instead of
        /// packages/ next to the host; ShippedUiPackages loads Unity's from Managed/ instead.
        public string? UiPackages;
        public bool ShippedUiPackages;
        /// Optional changes to the game's own code (GameTweaks), by name.
        public readonly HashSet<string> GameTweaks = new(StringComparer.Ordinal);
        public bool CheckAssets;
        /// With --check-assets: where to list the script classes the data uses.
        public string? ListDataScripts;
        public bool CheckBind;
        public bool CheckShaders;
        public string? DumpShader;
        public string? DumpScript;
        public int Frames = -1;
        /// --max-frame-rate N[:FRAMES]: an upper bound on the frame rate whatever the game asks
        /// for (scripted runs whose schedules count frames), for the first FRAMES frames when
        /// given; 0 for none.
        public int MaxFrameRate, MaxFrameRateFrames = int.MaxValue;
        /// Diagnostics: log every first-chance exception with IL offsets per frame (game code
        /// has no symbols, so offsets locate the throwing instruction).
        public bool TraceExceptions;
        /// Diagnostics (--gl-debug): the driver's GL debug output in the log (debug context).
        public bool GlDebug;
        /// Diagnostics (--vsync-count N): the swap interval regardless of the quality settings.
        public int VSyncCount = -1;
        /// Diagnostics (--gpu-profile): GPU time by operation with the frame counters.
        public bool GpuProfile;
        /// Diagnostics (--no-bindless): textures bound to units even where bindless textures exist.
        public bool NoBindless;
        public readonly ScriptedInput Input = new();
        // Unity's standalone player arguments.
        public int ScreenWidth = -1, ScreenHeight = -1;
        public int ScreenFullscreen = -1;
        public string? WindowMode;
        /// Development: "frame:path.png[:superSize]" calls ScreenCapture.CaptureScreenshot in
        /// that frame.
        public readonly List<(int Frame, string Path, int Scale)> Captures = new();
        /// --probe-ui FRAME:X,Y: logs the canvas elements drawn at a window point (top-left
        /// origin, as --input) as of that frame.
        public readonly List<(int Frame, float X, float Y)> Probes = new();
        /// --dump-hierarchy FRAME:PATH: logs the subtree at a GameObject path (GameObject.Find
        /// syntax) as of that frame: active flags, rect transforms, components.
        public readonly List<(int Frame, string Path)> HierarchyDumps = new();
        /// --report-builtin-assets FILE: at exit, the objects the run loaded from Unity's built-in
        /// resource files (tab-separated).
        public string? BuiltinReport;
        /// --dump-component FRAME:PATH|TYPE: logs the serialized fields of a component (by type
        /// name) on the GameObject at PATH as of that frame.
        public readonly List<(int Frame, string Spec)> ComponentDumps = new();
        /// --dump-ui FRAME:PATH: writes every active UI element (rects, canvas renderer output,
        /// package component state) as of that frame (PLAN M9's differential oracle).
        public readonly List<(int Frame, string Path)> UiDumps = new();

        public static Options? Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--game": o.GameDir = args[++i]; break;
                    case "--log": o.LogPath = args[++i]; break;
                    case "--run-home": o.RunHome = Path.GetFullPath(args[++i]); break;
                    case "--audio-wav": o.AudioWav = Path.GetFullPath(args[++i]); break;
                    case "--headless": o.Headless = true; break;
                    case "--batchmode": o.BatchMode = true; break;
                    case "--check-types": o.CheckTypes = true; break;
                    case "--check-assets": o.CheckAssets = true; break;
                    case "--list-data-scripts": o.ListDataScripts = args[++i]; break;
                    case "--ui-packages": o.UiPackages = Path.GetFullPath(args[++i]); break;
                    case "--shipped-ui-packages": o.ShippedUiPackages = true; break;
                    case "--game-tweak":
                    {
                        var name = args[++i];
                        if (!OniBrutal.Host.GameTweaks.GameTweakNames.IsKnown(name))
                        {
                            Console.Error.WriteLine($"Unknown game tweak {name}; known: all, {string.Join(", ", OniBrutal.Host.GameTweaks.GameTweakNames.All)}");
                            return null;
                        }
                        o.GameTweaks.Add(name);
                        break;
                    }
                    case "--check-bind": o.CheckBind = true; break;
                    case "--check-shaders": o.CheckShaders = true; break;
                    case "--dump-shader": o.DumpShader = args[++i]; break;
                    case "--dump-script": o.DumpScript = args[++i]; break;
                    case "--frames": o.Frames = int.Parse(args[++i]); break;
                    case "--trace-exceptions": o.TraceExceptions = true; break;
                    case "--gl-debug": o.GlDebug = true; break;
                    case "--vsync-count": o.VSyncCount = int.Parse(args[++i]); break;
                    case "--gpu-profile": o.GpuProfile = true; break;
                    case "--no-bindless": o.NoBindless = true; break;
                    case "--input": o.Input.Add(args[++i]); break;
                    case "--frame-stats": OniBrutal.Engine.PlayerLoop.FrameStats = true; break;
                    case "--max-frame-rate":
                        {
                            var spec = args[++i].Split(':');
                            o.MaxFrameRate = int.Parse(spec[0]);
                            if (spec.Length > 1) o.MaxFrameRateFrames = int.Parse(spec[1]);
                            break;
                        }
                    case "--dump-render-textures":
                        {
                            var spec = args[++i];
                            int colon = spec.IndexOf(':');
                            OniBrutal.Engine.Rendering.FrameRenderer.DumpFrame = int.Parse(spec[..colon]);
                            OniBrutal.Engine.Rendering.FrameRenderer.DumpDirectory = Path.GetFullPath(spec[(colon + 1)..]);
                            break;
                        }
                    case "--native-override":
                        {
                            var spec = args[++i];
                            int eq = spec.IndexOf('=');
                            if (eq <= 0) throw new ArgumentException("--native-override expects NAME=PATH");
                            AssemblyPolicy.NativeOverrides[spec[..eq]] = Path.GetFullPath(spec[(eq + 1)..]);
                            break;
                        }
                    case "-screen-width": o.ScreenWidth = int.Parse(args[++i]); break;
                    case "-screen-height": o.ScreenHeight = int.Parse(args[++i]); break;
                    case "-screen-fullscreen": o.ScreenFullscreen = int.Parse(args[++i]); break;
                    case "-window-mode": o.WindowMode = args[++i]; break;
                    case "--dump-component":
                    {
                        var spec = args[++i];
                        int colon = spec.IndexOf(':');
                        o.ComponentDumps.Add((int.Parse(spec[..colon]), spec[(colon + 1)..]));
                        break;
                    }
                    case "--report-builtin-assets": o.BuiltinReport = args[++i]; break;
                    case "--dump-hierarchy":
                    {
                        var spec = args[++i];
                        int colon = spec.IndexOf(':');
                        o.HierarchyDumps.Add((int.Parse(spec[..colon]), spec[(colon + 1)..]));
                        break;
                    }
                    case "--dump-ui-vertices": OniBrutal.Engine.UI.UiDump.VerticesFilter = args[++i]; break;
                    case "--dump-ui":
                    {
                        var spec = args[++i];
                        int colon = spec.IndexOf(':');
                        o.UiDumps.Add((int.Parse(spec[..colon]), spec[(colon + 1)..]));
                        break;
                    }
                    case "--probe-ui":
                    {
                        var parts = args[++i].Split(':', ',');
                        o.Probes.Add((int.Parse(parts[0]), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture)));
                        break;
                    }
                    case "--capture":
                    {
                        // FRAME:PATH[:SCALE]; the path may hold a colon itself (a Windows drive).
                        var spec = args[++i];
                        int colon = spec.IndexOf(':');
                        var path = spec[(colon + 1)..];
                        int scale = 1;
                        int last = path.LastIndexOf(':');
                        if (last > 0 && int.TryParse(path[(last + 1)..], out int s)) { scale = s; path = path[..last]; }
                        o.Captures.Add((int.Parse(spec[..colon]), path, scale));
                        break;
                    }
                    default:
                        Console.Error.WriteLine($"Unknown option {args[i]}");
                        Console.Error.WriteLine("usage: OniBrutal [--game DIR] [--log FILE] [--run-home DIR] [--audio-wav DIR] [--headless] [--batchmode] [--frames N] [--native-override NAME=PATH] [--trace-exceptions] [--gl-debug] [--vsync-count N] [--gpu-profile] [--no-bindless] [--input FRAME:ACTION[:ARGS]] [--frame-stats] [--max-frame-rate N[:FRAMES]] [--dump-render-textures FRAME:DIR] [-screen-width W] [-screen-height H] [-screen-fullscreen 0|1] [-window-mode exclusive|borderless|windowed] [--check-types] [--ui-packages DIR | --shipped-ui-packages] [--game-tweak NAME|all] [--check-assets [--list-data-scripts FILE]] [--check-bind] [--check-shaders] [--dump-shader NAME:DIR] [--capture FRAME:PATH[:SCALE]] [--probe-ui FRAME:X,Y] [--dump-ui FRAME:PATH] [--dump-ui-vertices PATH-SUBSTRING] [--dump-hierarchy FRAME:PATH] [--report-builtin-assets FILE] [--dump-component FRAME:PATH|TYPE]");
                        return null;
                }
            }
            return o;
        }
    }

    /// Unity's player paths for this game: the Linux player's (XDG directories) or the Windows
    /// player's (LocalLow, the temporary folder; with forward slashes, as Unity reports them).
    internal static class Paths
    {
        public static void Configure(string dataDir, Options options)
        {
            // app.info: company name, then product name.
            var info = File.ReadAllLines(Path.Combine(dataDir, "app.info"));
            EngineConfig.CompanyName = info.Length > 0 ? info[0] : "";
            EngineConfig.ProductName = info.Length > 1 ? info[1] : "";
            if (OperatingSystem.IsWindows()) ConfigureWindows(dataDir, options.RunHome);
            else ConfigureLinux(dataDir, options.RunHome);
            EngineConfig.ConsoleLogPath = EngineConfig.PersistentDataPath + "/Player.log";
            EngineConfig.IsBatchMode = options.BatchMode;
            EngineConfig.Headless = options.Headless;
        }

        static void ConfigureLinux(string dataDir, string? runHome)
        {
            EngineConfig.DataPath = dataDir;
            EngineConfig.StreamingAssetsPath = Path.Combine(dataDir, "StreamingAssets");
            // Unity's Linux player: $XDG_CONFIG_HOME/unity3d/<company>/<product>, defaulting to ~/.config.
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var config = runHome != null ? Path.Combine(runHome, "config") : Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(config)) config = Path.Combine(home, ".config");
            EngineConfig.PersistentDataPath = Path.Combine(config, "unity3d", EngineConfig.CompanyName, EngineConfig.ProductName);
            var cache = runHome != null ? Path.Combine(runHome, "cache") : Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (string.IsNullOrEmpty(cache)) cache = Path.Combine(home, ".cache");
            EngineConfig.TemporaryCachePath = Path.Combine(cache, EngineConfig.CompanyName, EngineConfig.ProductName);
        }

        static void ConfigureWindows(string dataDir, string? runHome)
        {
            static string Unity(string path) => path.Replace('\\', '/');
            EngineConfig.DataPath = Unity(Path.GetFullPath(dataDir));
            EngineConfig.StreamingAssetsPath = EngineConfig.DataPath + "/StreamingAssets";
            var localLow = runHome != null ? Path.Combine(runHome, "AppData", "LocalLow") : WindowsFolders.LocalAppDataLow();
            EngineConfig.PersistentDataPath = Unity(Path.Combine(localLow, EngineConfig.CompanyName, EngineConfig.ProductName));
            var temp = runHome != null ? Path.Combine(runHome, "AppData", "Local", "Temp") : Path.GetTempPath();
            EngineConfig.TemporaryCachePath = Unity(Path.Combine(temp, EngineConfig.CompanyName, EngineConfig.ProductName));
        }
    }

    /// Windows known folders .NET's SpecialFolder does not name.
    internal static class WindowsFolders
    {
        static readonly Guid LocalAppDataLowId = new("A520A1A4-1780-4FF6-BD18-167343C5AF16");

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out IntPtr path);

        [System.Runtime.InteropServices.DllImport("ole32.dll")]
        static extern void CoTaskMemFree(IntPtr p);

        /// %USERPROFILE%\AppData\LocalLow, where Unity's Windows player keeps persistent data.
        public static string LocalAppDataLow()
        {
            int hr = SHGetKnownFolderPath(LocalAppDataLowId, 0, IntPtr.Zero, out var p);
            if (hr != 0) throw new InvalidOperationException($"The LocalLow folder could not be found (0x{hr:X8}).");
            try { return System.Runtime.InteropServices.Marshal.PtrToStringUni(p)!; }
            finally { CoTaskMemFree(p); }
        }
    }
}
