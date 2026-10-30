using System;
using System.IO;
using OniMoono.Desktop.Gl;
using OniMoono.Desktop.Vulkan;
using OniMoono.Engine;
using OniMoono.Engine.Rendering;

namespace OniMoono.Desktop
{
    /// The renderer choice (the Moono Tweaks screen's "Renderer" row, settings.json's
    /// "renderer", `--renderer`): Vulkan by default, OpenGL when Vulkan cannot start.
    /// ARCHITECTURE §3.5.
    internal static class Renderers
    {
        public const string Auto = "auto", Vulkan = "vulkan", OpenGl = "opengl";
        public static readonly string[] Choices = [Auto, Vulkan, OpenGl];
        public static bool IsChoice(string value) => Array.IndexOf(Choices, value) >= 0;

        /// Opens the window and its device for a choice, logging which renderer it is and why.
        /// "auto" and "vulkan" try Vulkan first: the instance and a GPU that meets the
        /// requirements before a window exists, then the window's surface and swapchain; any
        /// failure releases what was made and opens an OpenGL window instead, with the reason in
        /// the log. "auto" also refuses software Vulkan devices (lavapipe) and, after a lost
        /// device, the driver it was lost on. `source` says where the choice came from.
        public static (GlfwPlatform Platform, RenderDevice Device) Open(WindowSettings settings, string choice, string source, string gpu, string cacheDirectory)
        {
            string reason = null;
            if (choice == OpenGl) reason = $"OpenGL chosen ({source})";
            else
            {
                var (platform, device, failure) = TryVulkan(settings, gpu, allowSoftware: choice == Vulkan, honourLoss: choice == Auto, cacheDirectory);
                if (device != null)
                {
                    EngineLog.Engine($"renderer: Vulkan ({choice}, {source})");
                    return (platform, device);
                }
                reason = $"Vulkan unavailable: {failure}";
            }
            EngineLog.Engine($"renderer: OpenGL ({reason})");
            var glPlatform = new GlfwPlatform(settings with { Api = RenderApi.OpenGl });
            try
            {
                return (glPlatform, new GlDevice(glPlatform));
            }
            catch
            {
                glPlatform.Dispose();
                throw;
            }
        }

        static (GlfwPlatform, RenderDevice, string) TryVulkan(WindowSettings settings, string gpu, bool allowSoftware, bool honourLoss, string cacheDirectory)
        {
            VkContext context;
            try
            {
                context = new VkContext(gpu, allowSoftware, cacheDirectory);
            }
            catch (VulkanUnavailableException e)
            {
                return (null, null, e.Message);
            }
            catch (Exception e) when (e is DllNotFoundException or TypeInitializationException or EntryPointNotFoundException or BadImageFormatException)
            {
                return (null, null, $"{e.GetType().Name}: {e.Message}");
            }
            if (honourLoss && LostBefore(context, cacheDirectory) is { } lost)
            {
                context.Dispose();
                return (null, null, lost);
            }
            GlfwPlatform platform = null;
            try
            {
                platform = new GlfwPlatform(settings with { Api = RenderApi.Vulkan });
                return (platform, new VkDevice(platform, context, cacheDirectory), null);
            }
            catch (Exception e)
            {
                // VkDevice released what it made; the window and the context go too.
                platform?.Dispose();
                context.Dispose();
                return (null, null, e is VulkanUnavailableException ? e.Message : $"{e.GetType().Name}: {e.Message}");
            }
        }

        /// The device was lost in an earlier run with this driver (VkDevice.WriteLostMarker):
        /// the reason auto uses OpenGL; null otherwise (another driver: the marker goes).
        static string LostBefore(VkContext context, string cacheDirectory)
        {
            if (cacheDirectory == null) return null;
            var path = VkDevice.LostMarkerPath(cacheDirectory);
            try
            {
                if (!File.Exists(path)) return null;
                var lines = File.ReadAllLines(path);
                if (lines.Length > 0 && lines[0] == context.DriverInfo)
                    return $"the Vulkan device was lost in an earlier run with this driver ({(lines.Length > 2 ? lines[2] : "?")}: {(lines.Length > 3 ? lines[3] : "")}); --renderer vulkan tries it again";
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                EngineLog.Engine($"renderer: {path}: {e.Message}");
            }
            return null;
        }
    }
}
