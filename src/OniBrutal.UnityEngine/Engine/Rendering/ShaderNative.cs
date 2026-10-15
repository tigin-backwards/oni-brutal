using System;
using System.Collections.Generic;
using System.Linq;
using OniBrutal.Assets.TypeTrees;
using OniBrutal.Engine.Assets;
using OniBrutal.Engine.Serialization;
using UnityEngine;

namespace OniBrutal.Engine.Rendering
{
    /// SerializedPropertyType.
    internal enum ShaderPropertyKind
    {
        Color = 0,
        Vector = 1,
        Float = 2,
        Range = 3,
        Texture = 4,
        Int = 5,
    }

    /// SerializedPropertyFlag.
    [Flags]
    internal enum ShaderPropertyAttributes
    {
        HideInInspector = 1,
        PerRendererData = 2,
        NoScaleOffset = 4,
        Normal = 8,
        HDR = 16,
        Gamma = 32,
        NonModifiableTextureData = 64,
        MainTexture = 128,
        MainColor = 256,
    }

    internal sealed class ShaderProperty
    {
        public string Name;
        public int Id;
        public ShaderPropertyKind Kind;
        public ShaderPropertyAttributes Attributes;
        public Vector4 Default;
        /// Default texture of a texture property: "white", "black", "gray", "bump", "red" ("" is gray).
        public string DefaultTexture;
        /// TextureDimension of a texture property.
        public int TextureDimension;
    }

    internal sealed class ShaderPass
    {
        public string Name;
        /// 0 normal pass, 1 UsePass, 2 GrabPass.
        public int Type;
        public string UseName;
        public Dictionary<string, string> Tags;
        /// The pass's SerializedPass data (render state and programs) for the device.
        public Fields Data;
    }

    internal sealed class SubShader
    {
        public ShaderPass[] Passes;
        public Dictionary<string, string> Tags;
        public int Lod;
    }

    /// A shader: its parsed ShaderLab form plus the compiled program data.
    internal sealed class ShaderNative : NativeObject
    {
        /// GLCore in ShaderCompilerPlatform.
        public const uint OpenGLCorePlatform = 15;

        public string ShaderName = "";
        public ShaderProperty[] Properties = Array.Empty<ShaderProperty>();
        public Dictionary<int, ShaderProperty> PropertiesById = new();
        public SubShader[] SubShaders = Array.Empty<SubShader>();
        public string[] KeywordNames = Array.Empty<string>();
        public string FallbackName = "";
        public bool HasOpenGLCorePrograms;
        public int MaximumLOD = -1;
        /// The whole serialized shader, for the device (program blobs and their tables).
        public Fields Data;

        /// Shader (class 48).
        public override void Bind(Fields f, IReferenceResolver resolver)
        {
            Serialized = null;
            Data = f;
            var parsed = f.Struct("m_ParsedForm");
            ShaderName = parsed.String("m_Name");
            if (Name.Length == 0) Name = ShaderName;
            FallbackName = parsed.String("m_FallbackName");
            KeywordNames = parsed.Array("m_KeywordNames").Cast<string>().ToArray();

            var props = parsed.Struct("m_PropInfo").Array("m_Props").Cast<Fields>().Select(p =>
            {
                var tex = p.Struct("m_DefTexture");
                var name = p.String("m_Name");
                return new ShaderProperty
                {
                    Name = name,
                    Id = ShaderPropertyIds.Get(name),
                    Kind = (ShaderPropertyKind)p.Int("m_Type"),
                    Attributes = (ShaderPropertyAttributes)p.Get<uint>("m_Flags"),
                    Default = new Vector4(p.Float("m_DefValue[0]"), p.Float("m_DefValue[1]"), p.Float("m_DefValue[2]"), p.Float("m_DefValue[3]")),
                    DefaultTexture = tex.String("m_DefaultName"),
                    TextureDimension = tex.Int("m_TexDim"),
                };
            }).ToArray();
            Properties = props;
            PropertiesById = new Dictionary<int, ShaderProperty>();
            foreach (var p in props) PropertiesById.TryAdd(p.Id, p);

            SubShaders = parsed.Array("m_SubShaders").Cast<Fields>().Select(s => new SubShader
            {
                Tags = TagMap(s.Struct("m_Tags")),
                Lod = s.Int("m_LOD"),
                Passes = s.Array("m_Passes").Cast<Fields>().Select(p => new ShaderPass
                {
                    Name = p.Struct("m_State").String("m_Name"),
                    Type = p.Int("m_Type"),
                    UseName = p.String("m_UseName"),
                    Tags = TagMap(p.Struct("m_Tags")),
                    Data = p,
                }).ToArray(),
            }).ToArray();

            HasOpenGLCorePrograms = f.Get<uint[]>("platforms").Contains(OpenGLCorePlatform);
            ShaderRegistry.Register(this);
        }

        static Dictionary<string, string> TagMap(Fields tagMap)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Fields pair in tagMap.Array("tags")) map[pair.String("first")] = pair.String("second");
            return map;
        }

        /// The subshader used for rendering: the first whose LOD is within the shader's and the
        /// global maximum LOD.
        public SubShader ActiveSubShader
        {
            get
            {
                int max = Math.Min(MaximumLOD < 0 ? int.MaxValue : MaximumLOD, ShaderGlobals.MaximumLOD);
                foreach (var s in SubShaders)
                    if (s.Lod <= max) return s;
                return SubShaders.Length > 0 ? SubShaders[^1] : null;
            }
        }

        /// From the active subshader's "Queue" tag ("Geometry+1"…); Geometry (2000) otherwise.
        public int RenderQueue
        {
            get
            {
                var s = ActiveSubShader;
                if (s == null || !s.Tags.TryGetValue("Queue", out var queue)) return 2000;
                return ParseQueue(queue);
            }
        }

        public static int ParseQueue(string queue)
        {
            int offset = 0;
            int sign = queue.IndexOfAny(['+', '-']);
            string name = queue;
            if (sign > 0)
            {
                name = queue[..sign];
                offset = int.Parse(queue[sign..].TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
            }
            int baseQueue = name.Trim().ToLowerInvariant() switch
            {
                "background" => 1000,
                "geometry" => 2000,
                "alphatest" => 2450,
                "geometrylast" => 2500,
                "transparent" => 3000,
                "overlay" => 4000,
                var n => int.TryParse(n, out int v) ? v : 2000,
            };
            return baseQueue + offset;
        }

        public int PassCount => ActiveSubShader?.Passes.Length ?? 0;

        public ShaderProperty FirstWith(ShaderPropertyAttributes attribute)
        {
            foreach (var p in Properties)
                if ((p.Attributes & attribute) != 0) return p;
            return null;
        }
    }

    /// Shader.Find's name table: shaders already loaded, then the project's ShaderNameRegistry.
    internal static class ShaderRegistry
    {
        static readonly Dictionary<string, ShaderNative> loaded = new(StringComparer.Ordinal);

        public static void Register(ShaderNative shader)
        {
            lock (loaded) loaded[shader.ShaderName] = shader;
        }

        public static void Unregister(ShaderNative shader)
        {
            lock (loaded)
                if (loaded.TryGetValue(shader.ShaderName, out var s) && s == shader) loaded.Remove(shader.ShaderName);
        }

        public static Shader Find(string name)
        {
            if (name == null) return null;
            lock (loaded)
                if (loaded.TryGetValue(name, out var s) && s.Managed is Shader live && live != null) return live;
            var pptr = ProjectSettings.ShaderNames.GetValueOrDefault(name);
            if (pptr.IsNull) return null;
            return ObjectLoader.Resolve(ProjectSettings.Managers, pptr) as Shader;
        }
    }
}
