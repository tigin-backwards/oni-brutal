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
        public byte[] Bytes = Array.Empty<byte>();
    }
}
