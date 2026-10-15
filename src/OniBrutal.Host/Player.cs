using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using OniBrutal.Engine;
using OniBrutal.Engine.Assets;
using UnityEngine;

namespace OniBrutal.Host
{
    /// The player: start-up in Unity's order, then the frame loop.
    internal static class Player
    {
        public static int Run(Options options, string dataDir)
        {
            AssetSystem.Initialize(dataDir);
            ProjectSettings.Load();
            PlayerLoop.Initialize();
            EngineLog.Engine($"Project: {EngineConfig.CompanyName} / {EngineConfig.ProductName} {EngineConfig.Version} (Unity {EngineConfig.UnityVersion})");

            var hooks = RuntimeInitializeHooks.Load(dataDir);
            hooks.Run(RuntimeInitializeLoadType.SubsystemRegistration);
            hooks.Run(RuntimeInitializeLoadType.AfterAssembliesLoaded);
            hooks.Run(RuntimeInitializeLoadType.BeforeSplashScreen);
            hooks.Run(RuntimeInitializeLoadType.BeforeSceneLoad);
            SceneLoader.LoadNow(0);
            hooks.Run(RuntimeInitializeLoadType.AfterSceneLoad);

            var clock = Stopwatch.StartNew();
            double last = 0;
            int frame = 0;
            while (!PlayerLoop.QuitRequested && (options.Frames < 0 || frame < options.Frames))
            {
                double now = clock.Elapsed.TotalSeconds;
                float dt = frame == 0 ? 0f : (float)(now - last);
                last = now;
                PlayerLoop.RunFrame(dt);
                frame++;
                Throttle(clock, now);
            }

            PlayerLoop.SendApplicationQuit();
            EngineLog.Engine($"Exiting after {frame} frames with code {PlayerLoop.ExitCode}");
            return PlayerLoop.ExitCode;
        }

        /// Frame pacing: Application.targetFrameRate when set; otherwise the display rate a
        /// windowed player would have under vsync (60 Hz without a display).
        static void Throttle(Stopwatch clock, double frameStart)
        {
            int target = PlayerLoop.TargetFrameRate > 0 ? PlayerLoop.TargetFrameRate : 60;
            double remaining = 1.0 / target - (clock.Elapsed.TotalSeconds - frameStart);
            if (remaining > 0.001) Thread.Sleep(TimeSpan.FromSeconds(remaining));
        }
    }

    /// [RuntimeInitializeOnLoadMethod] methods, as recorded at build time in
    /// RuntimeInitializeOnLoads.json, run by load phase.
    internal sealed class RuntimeInitializeHooks
    {
        readonly (int phase, string assembly, string type, string method)[] entries;

        RuntimeInitializeHooks((int, string, string, string)[] e) => entries = e;

        public static RuntimeInitializeHooks Load(string dataDir)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataDir, "RuntimeInitializeOnLoads.json")));
            var list = doc.RootElement.GetProperty("root").EnumerateArray().Select(e =>
            {
                var ns = e.GetProperty("nameSpace").GetString()!;
                var cls = e.GetProperty("className").GetString()!;
                return (e.GetProperty("loadTypes").GetInt32(), e.GetProperty("assemblyName").GetString()!, ns.Length > 0 ? ns + "." + cls : cls, e.GetProperty("methodName").GetString()!);
            }).ToArray();
            return new RuntimeInitializeHooks(list);
        }

        public void Run(RuntimeInitializeLoadType phase)
        {
            foreach (var (p, asmName, typeName, methodName) in entries)
            {
                if (p != (int)phase) continue;
                // Hooks of assemblies the player does not load (Unity's support packages) don't run.
                var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == asmName);
                if (asm == null) continue;
                var method = asm.GetType(typeName, true)!.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                             ?? throw new MissingMethodException(typeName, methodName);
                try
                {
                    method.Invoke(null, null);
                }
                catch (TargetInvocationException e) when (e.InnerException != null)
                {
                    UnityEngine.Debug.LogException(e.InnerException);
                }
            }
        }
    }
}
