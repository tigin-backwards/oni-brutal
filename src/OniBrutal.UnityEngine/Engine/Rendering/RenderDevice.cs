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

    /// The graphics device: owns GPU resources and executes rendering work. Engine objects keep
    /// their data on the CPU side (textures, meshes, materials) with version counters; the device
    /// creates and updates its resources from them when they are used, and keeps its handle in
    /// the object's `Gpu` slot.
    internal abstract class RenderDevice
    {
        public abstract GraphicsCaps Caps { get; }

        /// False for the null device: the player renders no frames (cameras and overlay canvases
        /// are not drawn), as Unity's -nographics player.
        public abstract bool RendersFrames { get; }

        /// Copies pixels from a render target (null: the back buffer) into a texture's CPU data.
        public abstract void ReadPixels(RenderTextureNative source, RectInt rect, Texture2DNative destination, int destX, int destY);

        /// Frees the device resource held in an object's Gpu slot.
        public abstract void ReleaseResource(object gpu);

        /// Activates a material pass for immediate drawing (GL, Graphics.DrawMeshNow). The pass
        /// index has been validated against the material's shader.
        public abstract bool SetPass(Material material, int pass);

        /// Compiles every shader program in advance (Shader.WarmupAllShaders).
        public abstract void WarmupShaders();
    }

    /// Unity's null graphics device (-nographics): it accepts all work and produces nothing; no
    /// GPU resources are ever created.
    internal sealed class NullRenderDevice : RenderDevice
    {
        public override GraphicsCaps Caps { get; } = new NullCaps();
        public override bool RendersFrames => false;

        // There is no framebuffer to read: the destination keeps its contents.
        public override void ReadPixels(RenderTextureNative source, RectInt rect, Texture2DNative destination, int destX, int destY) { }

        public override void ReleaseResource(object gpu) { }

        public override bool SetPass(Material material, int pass) => true;

        // The null device has no programs to compile.
        public override void WarmupShaders() { }
    }

    internal static class GraphicsDevice
    {
        public static RenderDevice Current { get; private set; } = new NullRenderDevice();

        public static GraphicsCaps Caps => Current.Caps;

        public static void Install(RenderDevice device) => Current = device;

        /// Releases an object's device resource and clears the slot.
        public static void Release(ref object gpu)
        {
            if (gpu == null) return;
            Current.ReleaseResource(gpu);
            gpu = null;
        }
    }
}
