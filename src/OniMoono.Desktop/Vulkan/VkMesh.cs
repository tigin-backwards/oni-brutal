using OniMoono.Desktop.Gl;

namespace OniMoono.Desktop.Vulkan
{
    /// A mesh's device side on Vulkan (DeviceMesh.Vk), device thread only: one buffer holding
    /// every channel and the indices. Each upload makes a new buffer (the old one is released
    /// when the frames using it are done), so a draw recorded before a change reads the data it
    /// was recorded with, as on OpenGL, without the rendering waiting for a copy.
    internal sealed class VkMesh
    {
        public VkBufferAlloc Buffer;
        /// Each channel's offset in Buffer (-1: absent), and the indices' offset.
        public readonly long[] ChannelOffsets = new long[(int)Channel.Count];
        public long IndexOffset;
    }
}
