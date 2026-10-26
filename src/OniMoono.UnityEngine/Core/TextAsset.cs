using System;
using System.Text;
using OniMoono.Engine.Assets;
using Unity.Collections;

namespace UnityEngine
{
    public partial class TextAsset
    {
        public TextAsset() : this(string.Empty) { }

        public TextAsset(string text)
        {
            Bind(new TextAssetNative { Bytes = TextAssetNative.Pinned(Encoding.UTF8.GetBytes(text ?? "")) });
        }

        TextAssetNative Data => (TextAssetNative)NativeOrThrow;

        /// The contents decoded as text. Unity decodes UTF-8 (skipping a byte-order mark) unless the
        /// data starts with a UTF-16/32 byte-order mark.
        public string text
        {
            get
            {
                var b = Data.Bytes;
                if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
                if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
                if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
                return Encoding.UTF8.GetString(b);
            }
        }

        public byte[] bytes => (byte[])Data.Bytes.Clone();

        public long dataSize => Data.Bytes.LongLength;

        /// The raw bytes as a NativeArray aliasing the asset's memory (length = bytes / size of T).
        public unsafe NativeArray<T> GetData<T>() where T : struct
        {
            var bytes = Data.Bytes;
            int length = bytes.Length / System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            return new NativeArray<T>(System.Runtime.CompilerServices.Unsafe.AsPointer(ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(bytes)), length, owned: false);
        }

        public override string ToString() => text;
    }
}
