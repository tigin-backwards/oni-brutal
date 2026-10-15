using System;
using System.Collections.Generic;

namespace OniBrutal.Engine.Assets
{
    /// Native record types of engine asset classes (textures, meshes…), by engine type.
    internal static class AssetNatives
    {
        static readonly Dictionary<Type, Func<NativeObject>> factories = new()
        {
            [typeof(UnityEngine.TextAsset)] = () => new TextAssetNative(),
            [typeof(UnityEngine.AssetBundle)] = () => new AssetBundleNative(),
            [typeof(UnityEngine.Texture2D)] = () => new Rendering.Texture2DNative(),
            [typeof(UnityEngine.Texture2DArray)] = () => new Rendering.Texture2DArrayNative(),
            [typeof(UnityEngine.Texture3D)] = () => new Rendering.Texture3DNative(),
            [typeof(UnityEngine.RenderTexture)] = () => new Rendering.RenderTextureNative(),
            [typeof(UnityEngine.Shader)] = () => new Rendering.ShaderNative(),
            [typeof(UnityEngine.Material)] = () => new Rendering.MaterialNative(),
            [typeof(UnityEngine.Mesh)] = () => new Rendering.MeshNative(),
            [typeof(UnityEngine.Sprite)] = () => new Rendering.SpriteNative(),
            [typeof(UnityEngine.U2D.SpriteAtlas)] = () => new Rendering.SpriteAtlasNative(),
            [typeof(UnityEngine.RuntimeAnimatorController)] = () => new Mecanim.AnimatorControllerNative(),
            [typeof(UnityEngine.Avatar)] = () => new Mecanim.AvatarNative(),
            [typeof(UnityEngine.AvatarMask)] = () => new Mecanim.AvatarMaskNative(),
        };

        public static NativeObject Create(Type engineType)
        {
            for (var t = engineType; t != null && t != typeof(UnityEngine.Object); t = t.BaseType)
                if (factories.TryGetValue(t, out var f)) return f();
            return new NativeObject();
        }
    }

    internal sealed class TextAssetNative : NativeObject
    {
        /// Pinned, so TextAsset.GetData can alias it with a NativeArray.
        public byte[] Bytes = Pinned(ReadOnlySpan<byte>.Empty);

        public static byte[] Pinned(ReadOnlySpan<byte> data)
        {
            var bytes = GC.AllocateUninitializedArray<byte>(data.Length, pinned: true);
            data.CopyTo(bytes);
            return bytes;
        }
    }
}
