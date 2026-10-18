using System;
using OniBrutal.Desktop.Gl;
using OniBrutal.Engine.Rendering;
using UnityEngine;
using Xunit;
using static OniBrutal.Desktop.Gl.GlConst;

namespace OniBrutal.Tests
{
    /// The device resolves what a material and the globals give a program once per their
    /// versions; every draw must still see the current values, its own block's overrides and
    /// its own per-draw values.
    public class UniformRecordingTests
    {
        static readonly int ColorId = Shader.PropertyToID("_RecordingTestColor");
        static readonly int CutoffId = Shader.PropertyToID("_RecordingTestCutoff");
        static readonly int GlobalId = Shader.PropertyToID("_RecordingTestGlobal");

        static DeviceProgram Program()
        {
            var p = new DeviceProgram();
            p.Uniforms.Add(new UniformBinding { Name = "_Color", Type = FloatVec4, Size = 1, Source = UniformSource.Property, PropertyId = ColorId });
            p.Uniforms.Add(new UniformBinding { Name = "_Cutoff", Type = FloatType, Size = 1, Source = UniformSource.Property, PropertyId = CutoffId });
            p.Uniforms.Add(new UniformBinding { Name = "_Global", Type = FloatVec4, Size = 1, Source = UniformSource.Property, PropertyId = GlobalId });
            p.Uniforms.Add(new UniformBinding { Name = "unity_ObjectToWorld", Type = FloatMat4, Size = 4, IsMatrix = true, Source = UniformSource.ObjectToWorld });
            p.LooseLength = 4 + 1 + 4 + 16;
            return p;
        }

        static float[] Record(DeviceProgram p, DrawInputs d)
        {
            var values = new float[p.LooseLength];
            GlUniforms.Record(p, d, values, Span<byte>.Empty);
            return values;
        }

        [Fact]
        public void DrawsSeeCurrentMaterialGlobalBlockAndPerDrawValues()
        {
            var p = Program();
            var material = new MaterialNative();
            material.Properties.SetVector(ColorId, new Vector4(1f, 0f, 0f, 1f));
            material.Properties.SetFloat(CutoffId, 0.5f);
            ShaderGlobals.Properties.SetVector(GlobalId, new Vector4(7f, 7f, 7f, 7f));
            var d = new DrawInputs { Material = material, ObjectToWorld = Matrix4x4.Translate(new Vector3(3f, 0f, 0f)) };

            var v = Record(p, d);
            Assert.Equal(1f, v[0]);
            Assert.Equal(0.5f, v[4]);
            Assert.Equal(7f, v[5]);
            Assert.Equal(3f, v[9 + 12]); // translation x (column-major m03)

            // The material changes.
            material.Properties.SetFloat(CutoffId, 0.25f);
            Assert.Equal(0.25f, Record(p, d)[4]);

            // A block overrides one draw only.
            var block = new PropertySheet();
            block.SetVector(ColorId, new Vector4(0f, 0f, 1f, 1f));
            d.Block = block;
            v = Record(p, d);
            Assert.Equal(0f, v[0]);
            Assert.Equal(1f, v[2]);
            Assert.Equal(0.25f, v[4]);
            d.Block = null;
            Assert.Equal(1f, Record(p, d)[0]);

            // The globals change, and so does the draw's matrix.
            ShaderGlobals.Properties.SetVector(GlobalId, new Vector4(9f, 9f, 9f, 9f));
            d.ObjectToWorld = Matrix4x4.Translate(new Vector3(5f, 0f, 0f));
            v = Record(p, d);
            Assert.Equal(9f, v[5]);
            Assert.Equal(5f, v[9 + 12]);
        }
    }
}
