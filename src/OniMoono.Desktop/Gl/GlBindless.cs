using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Brutal.GlfwApi;
using Brutal.OpenGlApi;
using N = Brutal.OpenGlApi.OpenGl.Native;
using static OniMoono.Desktop.Gl.GlConst;

namespace OniMoono.Desktop.Gl
{
    /// ARB_bindless_texture, where the driver has it: sampler uniforms are set to resident
    /// handles of (texture, sampler object) pairs instead of texture units, so draws never change
    /// texture bindings. NVIDIA's OpenGL driver serializes draws whose vertex stage reads
    /// textures other than the previous draw's (the game's animation batches each have their
    /// own data textures: ~37 µs a draw, most of a large colony's frame); with handles there are
    /// no binding changes. The programs' sampler uniforms are declared bindless when compiled
    /// (`layout(bindless_sampler) uniform;`). A texture with a handle keeps its parameters and
    /// image layout fixed (sampling state lives in sampler objects, GlSamplers), so a texture
    /// whose image is specified again is recreated under a new name (GlTextures).
    internal static unsafe class GlBindless
    {
        /// Diagnostics (--no-bindless): the binding path even where bindless textures exist.
        public static bool Disabled;

        public static bool Enabled { get; private set; }

        static delegate* unmanaged[Cdecl]<uint, uint, ulong> getTextureSamplerHandle;
        static delegate* unmanaged[Cdecl]<ulong, void> makeResident, makeNonResident;
        static delegate* unmanaged[Cdecl]<uint, int, ulong, void> programUniformHandle;

        /// Loads the entry points (the rendering context is current).
        public static void Initialize()
        {
            if (Disabled || !GlPrograms.Extensions.Contains("GL_ARB_bindless_texture")) return;
            getTextureSamplerHandle = (delegate* unmanaged[Cdecl]<uint, uint, ulong>)Glfw.GetProcAddress("glGetTextureSamplerHandleARB"u8);
            makeResident = (delegate* unmanaged[Cdecl]<ulong, void>)Glfw.GetProcAddress("glMakeTextureHandleResidentARB"u8);
            makeNonResident = (delegate* unmanaged[Cdecl]<ulong, void>)Glfw.GetProcAddress("glMakeTextureHandleNonResidentARB"u8);
            programUniformHandle = (delegate* unmanaged[Cdecl]<uint, int, ulong, void>)Glfw.GetProcAddress("glProgramUniformHandleui64ARB"u8);
            Enabled = getTextureSamplerHandle != null && makeResident != null && makeNonResident != null && programUniformHandle != null;
        }

        static readonly Regex FirstUniform = new(@"^[ \t]*(UNITY_LOCATION\(\d+\)[ \t]*)?uniform\b", RegexOptions.Multiline);

        /// A program stage's text with its sampler uniforms declared bindless (the default
        /// qualifier goes before the first uniform declaration), and the extension it requires.
        public static string Prepare(string body, out string extension)
        {
            extension = "#extension GL_ARB_bindless_texture : require\n";
            var m = FirstUniform.Match(body);
            if (!m.Success) return body;
            return body[..m.Index] + "layout(bindless_sampler) uniform;\n" + body[m.Index..];
        }

        /// The resident handle of a texture with its current sampler object.
        public static ulong Handle(DeviceTexture t)
        {
            uint sampler = t.Sampler != 0 ? t.Sampler : GlSamplers.Default;
            t.Handles ??= new Dictionary<uint, ulong>();
            if (t.Handles.TryGetValue(sampler, out var handle)) return handle;
            handle = getTextureSamplerHandle(t.Id, sampler);
            makeResident(handle);
            t.Handles[sampler] = handle;
            return handle;
        }

        public static bool HasHandles(DeviceTexture t) => t.Handles is { Count: > 0 };

        /// Makes the texture's handles non-resident (before the texture is deleted or replaced).
        public static void Release(DeviceTexture t)
        {
            if (t.Handles == null) return;
            foreach (var handle in t.Handles.Values) makeNonResident(handle);
            t.Handles.Clear();
        }

        public static void SetSampler(uint program, int location, ulong handle) => programUniformHandle(program, location, handle);
    }

    /// Sampler objects by sampling state (bindless handles pair a texture with one).
    internal static class GlSamplers
    {
        static readonly Dictionary<(GlTextures.SamplerSettings Settings, bool Mips, uint Target), uint> samplers = new();
        static uint defaultSampler;

        /// The state of a texture that no sampler settings have been applied to: linear
        /// filtering, repeat (GL's texture defaults, as the device's default textures use).
        public static uint Default
        {
            get
            {
                if (defaultSampler == 0)
                {
                    defaultSampler = Create();
                    Set(defaultSampler, TextureMinFilter, (int)Linear);
                    Set(defaultSampler, TextureMagFilter, (int)Linear);
                }
                return defaultSampler;
            }
        }

        public static uint Get(GlTextures.SamplerSettings s, bool mips, uint target)
        {
            var key = (s, mips, target);
            if (samplers.TryGetValue(key, out var id)) return id;
            id = Create();
            uint min = s.FilterMode switch
            {
                UnityEngine.FilterMode.Point => mips ? NearestMipmapNearest : Nearest,
                UnityEngine.FilterMode.Bilinear => mips ? LinearMipmapNearest : Linear,
                _ => mips ? LinearMipmapLinear : Linear,
            };
            Set(id, TextureMinFilter, (int)min);
            Set(id, TextureMagFilter, (int)(s.FilterMode == UnityEngine.FilterMode.Point ? Nearest : Linear));
            Set(id, TextureWrapS, (int)GlTextures.Wrap(s.WrapU));
            Set(id, TextureWrapT, (int)GlTextures.Wrap(s.WrapV));
            Set(id, TextureWrapR, (int)GlTextures.Wrap(s.WrapW));
            SetF(id, TextureLodBias, s.MipBias);
            if (GlDevice.MaxAnisotropy > 1f)
                SetF(id, TextureMaxAnisotropy, s.FilterMode == UnityEngine.FilterMode.Point ? 1f : Math.Clamp(s.Aniso, 1, GlDevice.MaxAnisotropy));
            samplers[key] = id;
            return id;
        }

        static unsafe uint Create()
        {
            GlSampler id = default;
            N.glCreateSamplers(1, &id);
            return id.Id;
        }

        static unsafe void Set(uint sampler, uint name, int value) => N.glSamplerParameteri(new GlSampler(sampler), name, value);
        static unsafe void SetF(uint sampler, uint name, float value) => N.glSamplerParameterf(new GlSampler(sampler), name, value);
    }
}
