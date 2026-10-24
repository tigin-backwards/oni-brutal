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

        /// Transparent DrawMesh draws at the same distance go by their materials' instance IDs,
        /// ascending, whatever order they were submitted in (UnityProbe draworder): the material
        /// made last draws first, under the other. A nearer draw still goes last.
        [Fact]
        public void TransparentDrawsAtTheSameDistanceGoByMaterial()
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
                camera.orthographic = true;
                camera.transform.position = new Vector3(0f, 0f, -100f);
                var mesh = new Mesh { vertices = [Vector3.zero, Vector3.up, Vector3.right], triangles = [0, 1, 2] };
                var shader = Shader.Find("Hidden/Internal-GUITextureClip");
                var first = new Material(shader) { name = "first", renderQueue = 3500 };
                var second = new Material(shader) { name = "second", renderQueue = 3500 };
                string Drawn() => string.Join(",", device.MeshDraws.Select(d => d.Material.name));

                foreach (var (a, b) in new[] { (first, second), (second, first) })
                {
                    device.MeshDraws.Clear();
                    Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0f, 0f, -23.5f)), a, 0);
                    Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0f, 0f, -23.5f)), b, 0);
                    Frame();
                    Assert.Equal("second,first", Drawn());
                }
                // Within a float step at the camera's distance: still a tie.
                device.MeshDraws.Clear();
                Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0f, 0f, -23.5f + 9.174312e-05f)), first, 0);
                Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0f, 0f, -23.5f + 8.928571e-05f)), second, 0);
                Frame();
                Assert.Equal("second,first", Drawn());
                // Nearer draws last.
                device.MeshDraws.Clear();
                Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0f, 0f, -23.6f)), first, 0);
                Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0f, 0f, -23.5f)), second, 0);
                Frame();
                Assert.Equal("second,first", Drawn());
                device.MeshDraws.Clear();
                Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0f, 0f, -23.5f)), first, 0);
                Graphics.DrawMesh(mesh, Matrix4x4.Translate(new Vector3(0f, 0f, -23.6f)), second, 0);
                Frame();
                Assert.Equal("first,second", Drawn());
            }
            finally
            {
                GraphicsDevice.Install(previous);
            }
        }
    }
}
