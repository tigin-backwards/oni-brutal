using OniBrutal.Engine.Assets;
using UnityEngine;

namespace OniBrutal.Engine.Platform
{
    /// Connects the platform to the frame: OS events and input are processed at the start of
    /// EarlyUpdate, as Unity does.
    internal static class PlatformLoop
    {
        static bool focused = true;

        /// Installs the platform (a headless one at the project's default resolution when null)
        /// and the project's input axes.
        public static void Install(IPlatform platform)
        {
            platform ??= new HeadlessPlatform(
                ProjectSettings.PlayerSettings.Int("defaultScreenWidth"),
                ProjectSettings.PlayerSettings.Int("defaultScreenHeight"));
            PlatformServices.Install(platform);
            InputState.Instance.InstallAxes(ProjectSettings.InputAxes);
            focused = platform.Focused;
            PlayerLoop.HasFocus = focused;
            PlayerLoop.EarlyUpdate -= Poll;
            PlayerLoop.EarlyUpdate += Poll;
        }

        static void Poll()
        {
            var platform = PlatformServices.Current;
            // Closing the window quits the player like Application.Quit.
            if (!platform.PollEvents(InputState.Instance)) Application.Quit();
            MainThreadPump.Polled();
            if (platform.Focused != focused)
            {
                focused = platform.Focused;
                EngineLog.Engine(focused ? "application focus gained" : "application focus lost");
                // Unity releases held keys when focus is lost.
                if (!focused) InputState.Instance.Reset();
                PlayerLoop.SendApplicationFocus(focused);
            }
            InputState.Instance.BeginFrame(Time.unscaledDeltaTime);
        }
    }
}
