namespace UnityEngine
{
    public partial class GraphicsBuffer
    {
        /// GPU storage behind the buffer; released by Release/Dispose.
        internal OniBrutal.Engine.Rendering.GpuBuffer Buffer;

        public void Release()
        {
            Buffer?.Release();
            Buffer = null;
        }

        public void Dispose() => Release();
    }
}

namespace OniBrutal.Engine.Rendering
{
    /// Engine-side GPU buffer handle (defined with the renderer).
    internal abstract class GpuBuffer
    {
        public abstract void Release();
    }
}
