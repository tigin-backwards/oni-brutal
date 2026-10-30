using OniMoono.Engine.Rendering;

namespace OniMoono.Desktop.Gl
{
    /// The resource work recording posts for the device thread (GlWorker.Resources): what the
    /// main thread records about textures, meshes and programs is the same for every device
    /// (GlTextures.Prepare, GlMeshes.Prepare, GlDrawRecorder), what executes it is the device's.
    /// Every method runs on the device thread.
    internal interface IResourceWork
    {
        /// (Re)creates a render texture's surfaces for its shape.
        void CreateRenderTexture(DeviceTexture texture, RenderShape shape, string name);

        /// Uploads texels into a texture and frees them.
        void UploadTexture(DeviceTexture texture, TexelUpload upload);

        void ApplySampler(DeviceTexture texture, GlTextures.SamplerSettings sampler);

        void UploadMesh(DeviceMesh mesh, MeshUpload upload);

        /// The shader's program for a variant blob, built on first use (its Id is 0 when it
        /// cannot be built).
        DeviceProgram Program(ShaderNative shader, int blobIndex);
    }

    /// The OpenGL device's resource work.
    internal sealed class GlResourceWork : IResourceWork
    {
        public static readonly GlResourceWork Instance = new();

        public void CreateRenderTexture(DeviceTexture texture, RenderShape shape, string name)
        {
            long t = GlTimings.Now;
            GlTextures.EnsureRenderTexture(texture, shape, name);
            GlTimings.Targets += GlTimings.Now - t;
        }

        public void UploadTexture(DeviceTexture texture, TexelUpload upload)
        {
            long t = GlTimings.Now;
            uint q = GlGpuProfile.Enabled ? GlGpuProfile.Begin() : 0;
            upload.Execute(texture);
            if (GlGpuProfile.Enabled) GlGpuProfile.End($"texture upload {upload.Width}x{upload.Height} {upload.Format}", q);
            GlTimings.TextureUploads += GlTimings.Now - t;
            System.Threading.Interlocked.Add(ref OniMoono.Engine.StartupStats.GlUploadTicks, GlTimings.Now - t);
        }

        public void ApplySampler(DeviceTexture texture, GlTextures.SamplerSettings sampler) => GlTextures.ApplySamplerNow(sampler, texture);

        public void UploadMesh(DeviceMesh mesh, MeshUpload upload)
        {
            long t = GlTimings.Now;
            uint q = GlGpuProfile.Enabled ? GlGpuProfile.Begin() : 0;
            GlMeshes.Upload(mesh, upload);
            if (GlGpuProfile.Enabled) GlGpuProfile.End("mesh upload", q);
            GlTimings.MeshUploads += GlTimings.Now - t;
        }

        public DeviceProgram Program(ShaderNative shader, int blobIndex) => GlPrograms.Get(shader, blobIndex);
    }
}
