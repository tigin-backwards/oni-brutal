using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    /// A recorded list of rendering commands, executed when a camera reaches the CameraEvent the
    /// buffer is attached to.
    public partial class CommandBuffer
    {
        internal readonly List<OniBrutal.Engine.Rendering.RenderCommand> Commands = new();

        public CommandBuffer() { }

        public string name { get; set; }

        public int sizeInBytes => Commands.Count;

        public void Clear() => Commands.Clear();

        public void Release() => Commands.Clear();

        public virtual void Dispose() => Release();
    }
}

namespace OniBrutal.Engine.Rendering
{
    /// One recorded CommandBuffer command (defined with the renderer).
    internal abstract class RenderCommand
    {
    }
}
