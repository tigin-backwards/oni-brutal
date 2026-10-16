using System;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine.Rendering;
using UnityEngine;
using N = Brutal.OpenGlApi.OpenGl.Native;
using static OniBrutal.Desktop.Gl.GlConst;

namespace OniBrutal.Desktop.Gl
{
    /// A ShaderLab state value: a constant or a material property reference ([_Prop]).
    internal readonly struct StateValue
    {
        public readonly float Constant;
        public readonly int PropertyId;

        public StateValue(Fields f)
        {
            Constant = f.Float("val");
            var name = f.String("name");
            PropertyId = name.Length == 0 || name == "<noninit>" ? 0 : ShaderPropertyIds.Get(name);
        }

        public float Resolve(DrawInputs d) => PropertyId != 0 && d.TryFloat(PropertyId, out var v) ? v : Constant;
    }

    /// The fixed-function state of a pass (SerializedShaderState), parsed once.
    internal sealed class PassState
    {
        public StateValue SrcBlend, DstBlend, SrcBlendAlpha, DstBlendAlpha, BlendOp, BlendOpAlpha, ColorMask;
        public StateValue ZTest, ZWrite, Cull, OffsetFactor, OffsetUnits, AlphaToMask;
        public StateValue StencilRef, StencilReadMask, StencilWriteMask;
        public StateValue FrontComp, FrontPass, FrontFail, FrontZFail, BackComp, BackPass, BackFail, BackZFail;

        public PassState(Fields state)
        {
            var blend = state.Struct("rtBlend0");
            SrcBlend = new(blend.Struct("srcBlend"));
            DstBlend = new(blend.Struct("destBlend"));
            SrcBlendAlpha = new(blend.Struct("srcBlendAlpha"));
            DstBlendAlpha = new(blend.Struct("destBlendAlpha"));
            BlendOp = new(blend.Struct("blendOp"));
            BlendOpAlpha = new(blend.Struct("blendOpAlpha"));
            ColorMask = new(blend.Struct("colMask"));
            ZTest = new(state.Struct("zTest"));
            ZWrite = new(state.Struct("zWrite"));
            Cull = new(state.Struct("culling"));
            OffsetFactor = new(state.Struct("offsetFactor"));
            OffsetUnits = new(state.Struct("offsetUnits"));
            AlphaToMask = new(state.Struct("alphaToMask"));
            StencilRef = new(state.Struct("stencilRef"));
            StencilReadMask = new(state.Struct("stencilReadMask"));
            StencilWriteMask = new(state.Struct("stencilWriteMask"));
            // Face-specific ops (CompFront, PassBack…) are serialized only when given; faces left
            // at their defaults (Always, Keep) use the shared Stencil block values.
            var shared = state.Struct("stencilOp");
            var front = IsDefault(state.Struct("stencilOpFront")) ? shared : state.Struct("stencilOpFront");
            var back = IsDefault(state.Struct("stencilOpBack")) ? shared : state.Struct("stencilOpBack");
            FrontComp = new(front.Struct("comp"));
            FrontPass = new(front.Struct("pass"));
            FrontFail = new(front.Struct("fail"));
            FrontZFail = new(front.Struct("zFail"));
            BackComp = new(back.Struct("comp"));
            BackPass = new(back.Struct("pass"));
            BackFail = new(back.Struct("fail"));
            BackZFail = new(back.Struct("zFail"));
        }

        static bool IsDefault(Fields op)
        {
            static bool Constant(Fields v, float value) => v.Float("val") == value && v.String("name") is "" or "<noninit>";
            return Constant(op.Struct("comp"), 8f) && Constant(op.Struct("pass"), 0f) && Constant(op.Struct("fail"), 0f) && Constant(op.Struct("zFail"), 0f);
        }
    }

    /// Applies resolved state with redundant-call elimination.
    internal static unsafe class GlStateCache
    {
        struct Resolved : IEquatable<Resolved>
        {
            public int Src, Dst, SrcA, DstA, Op, OpA, Mask, ZTest, ZWrite, Cull, AlphaToMask;
            public float OffsetFactor, OffsetUnits;
            public int StencilRef, ReadMask, WriteMask, FComp, FPass, FFail, FZFail, BComp, BPass, BFail, BZFail;

            public bool Equals(Resolved o) => Src == o.Src && Dst == o.Dst && SrcA == o.SrcA && DstA == o.DstA && Op == o.Op && OpA == o.OpA && Mask == o.Mask
                && ZTest == o.ZTest && ZWrite == o.ZWrite && Cull == o.Cull && AlphaToMask == o.AlphaToMask && OffsetFactor == o.OffsetFactor && OffsetUnits == o.OffsetUnits
                && StencilRef == o.StencilRef && ReadMask == o.ReadMask && WriteMask == o.WriteMask && FComp == o.FComp && FPass == o.FPass && FFail == o.FFail
                && FZFail == o.FZFail && BComp == o.BComp && BPass == o.BPass && BFail == o.BFail && BZFail == o.BZFail;
        }

        static Resolved current;
        static bool valid;

        public static void Invalidate() => valid = false;

        public static void Apply(PassState s, DrawInputs d)
        {
            var r = new Resolved
            {
                Src = (int)s.SrcBlend.Resolve(d), Dst = (int)s.DstBlend.Resolve(d), SrcA = (int)s.SrcBlendAlpha.Resolve(d), DstA = (int)s.DstBlendAlpha.Resolve(d),
                Op = (int)s.BlendOp.Resolve(d), OpA = (int)s.BlendOpAlpha.Resolve(d), Mask = (int)s.ColorMask.Resolve(d),
                ZTest = (int)s.ZTest.Resolve(d), ZWrite = (int)s.ZWrite.Resolve(d), Cull = (int)s.Cull.Resolve(d), AlphaToMask = (int)s.AlphaToMask.Resolve(d),
                OffsetFactor = s.OffsetFactor.Resolve(d), OffsetUnits = s.OffsetUnits.Resolve(d),
                StencilRef = (int)s.StencilRef.Resolve(d), ReadMask = (int)s.StencilReadMask.Resolve(d), WriteMask = (int)s.StencilWriteMask.Resolve(d),
                FComp = (int)s.FrontComp.Resolve(d), FPass = (int)s.FrontPass.Resolve(d), FFail = (int)s.FrontFail.Resolve(d), FZFail = (int)s.FrontZFail.Resolve(d),
                BComp = (int)s.BackComp.Resolve(d), BPass = (int)s.BackPass.Resolve(d), BFail = (int)s.BackFail.Resolve(d), BZFail = (int)s.BackZFail.Resolve(d),
            };
            if (valid && r.Equals(current)) return;
            current = r;
            valid = true;

            // Blending: One/Zero for both colour and alpha with Add is off.
            bool blend = !(r.Src == 1 && r.Dst == 0 && r.SrcA == 1 && r.DstA == 0 && r.Op == 0 && r.OpA == 0);
            GlApi.Enable(Blend, blend);
            if (blend)
            {
                N.glBlendFuncSeparate(Factor(r.Src), Factor(r.Dst), Factor(r.SrcA), Factor(r.DstA));
                N.glBlendEquationSeparate(Equation(r.Op), Equation(r.OpA));
            }
            N.glColorMask((r.Mask & 8) != 0, (r.Mask & 4) != 0, (r.Mask & 2) != 0, (r.Mask & 1) != 0);

            // CompareFunction.Disabled turns the depth test off.
            GlApi.Enable(DepthTest, r.ZTest != 0);
            if (r.ZTest != 0) N.glDepthFunc((Brutal.OpenGlApi.GlFunc)Compare(r.ZTest));
            N.glDepthMask(r.ZWrite != 0);

            // Unity's front faces wind clockwise on screen (glFrontFace(GL_CW) at device start).
            GlApi.Enable(CullFace, r.Cull != 0);
            if (r.Cull != 0) N.glCullFace(r.Cull == 1 ? Brutal.OpenGlApi.GlFace.Front : Brutal.OpenGlApi.GlFace.Back);

            bool offset = r.OffsetFactor != 0f || r.OffsetUnits != 0f;
            GlApi.Enable(PolygonOffsetFill, offset);
            if (offset) N.glPolygonOffset(r.OffsetFactor, r.OffsetUnits);
            GlApi.Enable(SampleAlphaToCoverage, r.AlphaToMask != 0);

            // Stencil: comp Disabled (0) on both faces means no Stencil block.
            bool stencil = r.FComp != 0 || r.BComp != 0;
            GlApi.Enable(StencilTest, stencil);
            if (stencil)
            {
                // Faces with the same state are set with one call. Separate calls with identical
                // values are equivalent in GL, but Mesa's d3d12 driver then fails stencil tests it
                // should pass (masked UI portraits disappear).
                if (r.FComp == r.BComp && r.FFail == r.BFail && r.FZFail == r.BZFail && r.FPass == r.BPass)
                {
                    N.glStencilFunc((Brutal.OpenGlApi.GlFunc)Compare(r.FComp == 0 ? 8 : r.FComp), r.StencilRef, (uint)r.ReadMask);
                    N.glStencilOp((Brutal.OpenGlApi.GlStencilOp)StencilOp(r.FFail), (Brutal.OpenGlApi.GlStencilOp)StencilOp(r.FZFail), (Brutal.OpenGlApi.GlStencilOp)StencilOp(r.FPass));
                }
                else
                {
                    N.glStencilFuncSeparate(Brutal.OpenGlApi.GlFace.Front, (Brutal.OpenGlApi.GlFunc)Compare(r.FComp == 0 ? 8 : r.FComp), r.StencilRef, (uint)r.ReadMask);
                    N.glStencilFuncSeparate(Brutal.OpenGlApi.GlFace.Back, (Brutal.OpenGlApi.GlFunc)Compare(r.BComp == 0 ? 8 : r.BComp), r.StencilRef, (uint)r.ReadMask);
                    N.glStencilOpSeparate(Brutal.OpenGlApi.GlFace.Front, StencilOp(r.FFail), StencilOp(r.FZFail), StencilOp(r.FPass));
                    N.glStencilOpSeparate(Brutal.OpenGlApi.GlFace.Back, StencilOp(r.BFail), StencilOp(r.BZFail), StencilOp(r.BPass));
                }
                N.glStencilMask((uint)r.WriteMask);
            }
        }

        /// Unity BlendMode → GL blend factor.
        static uint Factor(int m) => m switch
        {
            0 => Zero,
            1 => One,
            2 => DstColor,
            3 => SrcColor,
            4 => OneMinusDstColor,
            5 => SrcAlpha,
            6 => OneMinusSrcColor,
            7 => DstAlpha,
            8 => OneMinusDstAlpha,
            9 => SrcAlphaSaturate,
            10 => OneMinusSrcAlpha,
            _ => One,
        };

        /// Unity BlendOp → GL equation.
        static uint Equation(int op) => op switch
        {
            1 => FuncSubtract,
            2 => FuncReverseSubtract,
            3 => Min,
            4 => Max,
            _ => FuncAdd,
        };

        /// Unity CompareFunction → GL comparison.
        public static uint Compare(int c) => c switch
        {
            1 => Never,
            2 => Less,
            3 => Equal,
            4 => Lequal,
            5 => Greater,
            6 => NotEqual,
            7 => Gequal,
            _ => Always,
        };

        /// Unity StencilOp → GL.
        static uint StencilOp(int op) => op switch
        {
            1 => Zero,
            2 => Replace,
            3 => Incr,
            4 => Decr,
            5 => Invert,
            6 => IncrWrap,
            7 => DecrWrap,
            _ => Keep,
        };
    }
}
