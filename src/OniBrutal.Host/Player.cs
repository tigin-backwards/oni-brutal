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
            OniBrutal.Desktop.GlfwPlatform? platform = null;
            if (options.Headless || options.BatchMode) OniBrutal.Engine.Platform.PlatformLoop.Install(null);
            else
            {
                platform = new OniBrutal.Desktop.GlfwPlatform(WindowSettingsFor(options));
                OniBrutal.Engine.Platform.PlatformLoop.Install(platform);
                var device = new OniBrutal.Desktop.Gl.GlDevice(platform);
                OniBrutal.Engine.Rendering.GraphicsDevice.Install(device);
            }
            OniBrutal.Engine.Rendering.FrameRenderer.Install();
            OniBrutal.Engine.UI.CanvasSystem.Install();
            OniBrutal.Engine.IMGUI.GUIManager.Install();
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
                foreach (var (captureFrame, path, scale) in options.Captures)
                    if (captureFrame == frame) ScreenCapture.CaptureScreenshot(path, scale);
                options.Input.Apply(frame);
                PlayerLoop.RunFrame(dt);
                frame++;
                Throttle(clock, now, platform != null, frame <= options.MaxFrameRateFrames ? options.MaxFrameRate : 0);
            }

            // Unity's quit: OnApplicationQuit, then every object is destroyed, then the process
            // ends (threads the game started do not keep it alive).
            PlayerLoop.SendApplicationQuit();
            SceneLoader.UnloadAll();
            Lifecycle.ProcessDestroyQueue();
            // The graphics device and window go before the process exits, as in Unity's player
            // (a GL context left current into process exit can hang driver exit handlers).
            if (platform != null)
            {
                OniBrutal.Engine.Rendering.GraphicsDevice.Install(new OniBrutal.Engine.Rendering.NullRenderDevice());
                platform.Dispose();
            }
            EngineLog.Engine($"Exiting after {frame} frames with code {PlayerLoop.ExitCode}");
            EngineLog.Close();
            Console.Out.Flush();
            Console.Error.Flush();
            // The process ends here without running native libraries' exit-time teardown: game
            // threads may still be inside native plugins (worldgen settling in SimDLL), and the
            // plugins' static destructors would free state under them. Unity's player likewise
            // ends without waiting for or tearing down under foreign threads. The game registers
            // no ProcessExit handlers.
            ProcessExit(PlayerLoop.ExitCode);
            return PlayerLoop.ExitCode;
        }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "_exit")]
        static extern void ProcessExit(int status);

        /// The player window from PlayerSettings (native resolution when so configured),
        /// overridden by Unity's -screen-* / -window-mode arguments.
        static OniBrutal.Desktop.WindowSettings WindowSettingsFor(Options options)
        {
            var ps = ProjectSettings.PlayerSettings;
            bool native = ps.Bool("defaultIsNativeResolution");
            int width = native ? -1 : ps.Int("defaultScreenWidth");
            int height = native ? -1 : ps.Int("defaultScreenHeight");
            var mode = (FullScreenMode)ps.Int("fullscreenMode");
            if (options.ScreenWidth > 0) width = options.ScreenWidth;
            if (options.ScreenHeight > 0) height = options.ScreenHeight;
            if (options.ScreenFullscreen == 0) mode = FullScreenMode.Windowed;
            else if (options.ScreenFullscreen == 1 && mode == FullScreenMode.Windowed) mode = FullScreenMode.FullScreenWindow;
            mode = options.WindowMode switch
            {
                "exclusive" => FullScreenMode.ExclusiveFullScreen,
                "borderless" => FullScreenMode.FullScreenWindow,
                "windowed" => FullScreenMode.Windowed,
                _ => mode,
            };
            return new OniBrutal.Desktop.WindowSettings(EngineConfig.ProductName, width, height, mode) { IgnoreUserInput = options.Input.Any };
        }

        /// Frame pacing as in Unity's desktop player: with vSync the swap paces frames (the
        /// presenter's wait for the previous frame), otherwise Application.targetFrameRate when
        /// set, otherwise none. Without a display there is no swap; frames are paced at
        /// targetFrameRate or 60 Hz. --max-frame-rate bounds the rate in any case.
        static void Throttle(Stopwatch clock, double frameStart, bool display, int maxFrameRate)
        {
            int target = PlayerLoop.TargetFrameRate;
            if (display && QualitySettings.vSyncCount > 0) target = 0;
            else if (!display && target <= 0) target = 60;
            if (maxFrameRate > 0 && (target <= 0 || target > maxFrameRate)) target = maxFrameRate;
            if (target <= 0) return;
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
            var list = doc.RootElement.GetProperty("root"u8).EnumerateArray().Select(e =>
            {
                var ns = e.GetProperty("nameSpace"u8).GetString()!;
                var cls = e.GetProperty("className"u8).GetString()!;
                return (e.GetProperty("loadTypes"u8).GetInt32(), e.GetProperty("assemblyName"u8).GetString()!, ns.Length > 0 ? ns + "." + cls : cls, e.GetProperty("methodName"u8).GetString()!);
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
