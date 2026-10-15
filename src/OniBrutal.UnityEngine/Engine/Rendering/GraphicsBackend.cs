using UnityEngine;
using UnityEngine.Rendering;

namespace OniBrutal.Engine.Rendering
{
    /// What the graphics device is and can do (SystemInfo). The OpenGL backend reports the real
    /// device; without graphics the Null device answers like Unity's -nographics player.
    internal abstract class GraphicsCaps
    {
        public abstract GraphicsDeviceType DeviceType { get; }
        public abstract string DeviceName { get; }
        public abstract string DeviceVendor { get; }
        public abstract string DeviceVersion { get; }
        public abstract int DeviceId { get; }
        public abstract int VendorId { get; }
        public abstract int MemorySizeMB { get; }
        public abstract int ShaderLevel { get; }
        public abstract int MaxTextureSize { get; }
        public abstract int MaxRenderTargets { get; }
        public abstract bool Supports2DArrayTextures { get; }
        public abstract bool Supports3DTextures { get; }
        public abstract bool SupportsInstancing { get; }
        public abstract bool SupportsComputeShaders { get; }
        public abstract bool SupportsTexture(TextureFormat format);
        public abstract bool SupportsRenderTexture(RenderTextureFormat format);
    }

    internal sealed class NullCaps : GraphicsCaps
    {
        public override GraphicsDeviceType DeviceType => GraphicsDeviceType.Null;
        public override string DeviceName => "Null Device";
        public override string DeviceVendor => "Unity Technologies";
        public override string DeviceVersion => "NULL 1.0 [1.0]";
        public override int DeviceId => 0;
        public override int VendorId => 0;
        public override int MemorySizeMB => 0;
        public override int ShaderLevel => 50;
        public override int MaxTextureSize => 16384;
        public override int MaxRenderTargets => 8;
        public override bool Supports2DArrayTextures => true;
        public override bool Supports3DTextures => true;
        public override bool SupportsInstancing => false;
        public override bool SupportsComputeShaders => false;
        // The null device accepts every format (nothing is created on a GPU).
        public override bool SupportsTexture(TextureFormat format) => true;
        public override bool SupportsRenderTexture(RenderTextureFormat format) => true;
    }

    internal static class GraphicsDevice
    {
        public static GraphicsCaps Caps { get; private set; } = new NullCaps();

        public static void Install(GraphicsCaps caps) => Caps = caps;
    }
}
