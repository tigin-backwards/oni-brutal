using Brutal.VulkanApi;

namespace OniMoono.Desktop.Vulkan
{
    internal static class VkCheck
    {
        /// Throws VkResultError for an error result (the raw function-table calls return it).
        public static void Ok(VkResult result, string what)
        {
            if (result < 0) throw new VkResultError(result, $"{what}: {result}");
        }
    }
}
