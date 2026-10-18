using System.Linq;
using OniBrutal.Engine;
using OniBrutal.Engine.Assets;
using OniBrutal.Engine.Rendering;
using UnityEngine;
using Xunit;

namespace OniBrutal.Tests
{
    public class DrawMeshTests : EngineTest
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// Graphics.DrawMesh draws with the block's values at the call: a change to the block
        /// before the frame renders reaches only later draws. After the frame, the block is written
        /// in place again (no copy).
        [Fact]
        public void ABlockChangedAfterDrawMeshKeepsTheQueuedValues()
        {
            var game = System.Environment.GetEnvironmentVariable("ONI_GAME_DIR")
                       ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "projects", "oni-linux");
            AssetSystem.Initialize(System.IO.Path.Combine(game, "OxygenNotIncluded_Data"));
            ProjectSettings.Load();
            FrameRenderer.Install();
            var device = new RecordingDevice();
            var previous = GraphicsDevice.Current;
            GraphicsDevice.Install(device);
            try
            {
                var camera = new GameObject("camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                var mesh = new Mesh { vertices = [Vector3.zero, Vector3.up, Vector3.right], triangles = [0, 1, 2] };
                var material = new Material(Shader.Find("Hidden/Internal-GUITextureClip"));
                var block = new MaterialPropertyBlock();

                block.SetColor(ColorId, Color.red);
                Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, block);
                block.SetColor(ColorId, Color.blue);
                Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, block);
                block.SetColor(ColorId, Color.green);
                Frame();
                var colors = device.MeshDrawProperties.Select(p => (Color)p.Vectors[ColorId]).ToArray();
                Assert.Equal(new[] { Color.red, Color.blue }, colors);

                var sheet = block.Sheet;
                block.SetColor(ColorId, Color.white);
                Assert.Same(sheet, block.Sheet);
                Assert.Equal(Color.white, block.GetColor(ColorId));
            }
            finally
            {
                GraphicsDevice.Install(previous);
            }
        }
    }
}
