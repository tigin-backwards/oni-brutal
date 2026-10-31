using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMoono.Engine.Rendering
{
    /// Diagnostics (ONI_TEXTURE_MEMORY_LOG=N): every N frames, the texel memory held on the CPU
    /// side and the bytes of the texel textures that have a device copy, with the largest
    /// textures; and each Resources.UnloadAsset call with what it was handed.
    internal static class TextureMemoryLog
    {
        static readonly int Every = int.TryParse(Environment.GetEnvironmentVariable("ONI_TEXTURE_MEMORY_LOG"), out var n) ? n : 0;
        public static bool Enabled => Every > 0;

        public static void Frame()
        {
            if (Every <= 0 || Time.frameCount % Every != 0) return;
            long gpu = 0, cpuTextures = 0; int count = 0, onGpu = 0;
            var big = new List<(long Bytes, string Name, bool Gpu, bool Cpu)>();
            var all = ObjectTable.Snapshot();
            foreach (var native in all)
            {
                if (native is not TexelTextureNative t || native.Destroyed) continue;
                count++;
                long bytes = t.SliceSize * t.Depth;
                bool hasGpu = t.Gpu != null, hasCpu = t.HasTexels && !t.Unloaded;
                if (hasGpu) { onGpu++; gpu += bytes; }
                if (hasCpu) cpuTextures += bytes;
                big.Add((bytes, t.Name, hasGpu, hasCpu));
            }
            var top = string.Join(", ", big.OrderByDescending(b => b.Bytes).Take(8).Select(b => $"{b.Name} {b.Bytes >> 20} MB{(b.Gpu ? " gpu" : "")}{(b.Cpu ? " cpu" : "")}"));
            var gc = GC.GetGCMemoryInfo();
            EngineLog.Engine($"texture memory f{Time.frameCount}: {count} texel textures, {onGpu} on the device ({gpu >> 20} MB), texels held {PixelMemory.LiveBytes >> 20} MB (textures with texels {cpuTextures >> 20} MB); GC heap {gc.HeapSizeBytes >> 20} MB, committed {gc.TotalCommittedBytes >> 20} MB, objects {all.Length}; largest: {top}");
        }

        public static void Unload(TexelTextureNative texture, long freed)
        {
            if (Every <= 0) return;
            EngineLog.Engine($"UnloadAsset f{Time.frameCount}: texture '{texture.Name}' persistent {texture.IsPersistent}, {freed >> 10} KiB freed");
        }
    }
}
