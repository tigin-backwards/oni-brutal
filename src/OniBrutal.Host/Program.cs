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

            AssemblyPolicy.Install(dataDir, AppContext.BaseDirectory);
            var assemblies = AssemblyPolicy.PreloadScriptingAssemblies(dataDir);
            EngineLog.Engine($"Preloaded {assemblies.Count} scripting assemblies");

            if (options.CheckTypes) return CheckTypes(assemblies);
            if (options.CheckAssets) return AssetCheck.Run(dataDir);
            if (options.CheckBind) return BindCheck.Run(dataDir);
            if (options.CheckShaders) return ShaderCheck.Run(dataDir);
            if (options.DumpScript != null) return AssetCheck.Dump(dataDir, options.DumpScript);

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
        public string GameDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects", "oni-linux");
        public string? LogPath;
        public bool Headless;
        public bool BatchMode;
        public bool CheckTypes;
        public bool CheckAssets;
        public bool CheckBind;
        public bool CheckShaders;
        public string? DumpScript;
        public int Frames = -1;
        // Unity's standalone player arguments.
        public int ScreenWidth = -1, ScreenHeight = -1;
        public int ScreenFullscreen = -1;
        public string? WindowMode;
        /// Development: "frame:path.png[:superSize]" calls ScreenCapture.CaptureScreenshot in
        /// that frame.
        public readonly List<(int Frame, string Path, int Scale)> Captures = new();

        public static Options? Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--game": o.GameDir = args[++i]; break;
                    case "--log": o.LogPath = args[++i]; break;
                    case "--headless": o.Headless = true; break;
                    case "--batchmode": o.BatchMode = true; break;
                    case "--check-types": o.CheckTypes = true; break;
                    case "--check-assets": o.CheckAssets = true; break;
                    case "--check-bind": o.CheckBind = true; break;
                    case "--check-shaders": o.CheckShaders = true; break;
                    case "--dump-script": o.DumpScript = args[++i]; break;
                    case "--frames": o.Frames = int.Parse(args[++i]); break;
                    case "-screen-width": o.ScreenWidth = int.Parse(args[++i]); break;
                    case "-screen-height": o.ScreenHeight = int.Parse(args[++i]); break;
                    case "-screen-fullscreen": o.ScreenFullscreen = int.Parse(args[++i]); break;
                    case "-window-mode": o.WindowMode = args[++i]; break;
                    case "--capture":
                    {
                        var parts = args[++i].Split(':');
                        o.Captures.Add((int.Parse(parts[0]), parts[1], parts.Length > 2 ? int.Parse(parts[2]) : 1));
                        break;
                    }
                    default:
                        Console.Error.WriteLine($"Unknown option {args[i]}");
                        Console.Error.WriteLine("usage: OniBrutal [--game DIR] [--log FILE] [--headless] [--batchmode] [--frames N] [-screen-width W] [-screen-height H] [-screen-fullscreen 0|1] [-window-mode exclusive|borderless|windowed] [--check-types] [--check-assets] [--check-bind]");
                        return null;
                }
            }
            return o;
        }
    }

    /// Unity's player paths for this game on Linux.
    internal static class Paths
    {
        public static void Configure(string dataDir, Options options)
        {
            // app.info: company name, then product name.
            var info = File.ReadAllLines(Path.Combine(dataDir, "app.info"));
            EngineConfig.CompanyName = info.Length > 0 ? info[0] : "";
            EngineConfig.ProductName = info.Length > 1 ? info[1] : "";
            EngineConfig.DataPath = dataDir;
            EngineConfig.StreamingAssetsPath = Path.Combine(dataDir, "StreamingAssets");
            // Unity's Linux player: $XDG_CONFIG_HOME/unity3d/<company>/<product>, defaulting to ~/.config.
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(config)) config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            EngineConfig.PersistentDataPath = Path.Combine(config, "unity3d", EngineConfig.CompanyName, EngineConfig.ProductName);
            EngineConfig.ConsoleLogPath = Path.Combine(EngineConfig.PersistentDataPath, "Player.log");
            var cache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (string.IsNullOrEmpty(cache)) cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
            EngineConfig.TemporaryCachePath = Path.Combine(cache, EngineConfig.CompanyName, EngineConfig.ProductName);
            EngineConfig.IsBatchMode = options.BatchMode;
            EngineConfig.Headless = options.Headless;
        }
    }
}
