using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Unity.Collections
{
    /// A view over unmanaged memory. Arrays the engine hands out (Texture2D.GetRawTextureData,
    /// GetPixelData) alias engine-owned memory and are not freed by Dispose; arrays the engine
    /// allocates for the caller own their memory.
    public unsafe partial struct NativeArray<T>
    {
        internal void* m_Buffer;
        internal int m_Length;
        internal int m_AllocatorLabel; // 0 = not owned (Unity's Allocator.None), 1 = owned

        internal NativeArray(void* buffer, int length, bool owned)
        {
            m_Buffer = buffer;
            m_Length = length;
            m_AllocatorLabel = owned ? 1 : 0;
        }

        /// Allocates zeroed memory owned by the array.
        internal static NativeArray<T> Allocate(int length)
        {
            nuint bytes = (nuint)length * (nuint)Unsafe.SizeOf<T>();
            void* p = NativeMemory.AlignedAlloc(bytes == 0 ? 1 : bytes, 16);
            NativeMemory.Clear(p, bytes);
            return new NativeArray<T>(p, length, owned: true);
        }

        public int Length => m_Length;
        public bool IsCreated => m_Buffer != null;

        public T this[int index]
        {
            get
            {
                CheckIndex(index);
                return Unsafe.Read<T>((byte*)m_Buffer + (long)index * Unsafe.SizeOf<T>());
            }
            set
            {
                CheckIndex(index);
                Unsafe.Write((byte*)m_Buffer + (long)index * Unsafe.SizeOf<T>(), value);
            }
        }

        void CheckIndex(int index)
        {
            if ((uint)index >= (uint)m_Length)
                throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length.");
        }

        public void Dispose()
        {
            if (m_Buffer == null) throw new ObjectDisposedException("The NativeArray is already disposed.");
            if (m_AllocatorLabel == 1) NativeMemory.AlignedFree(m_Buffer);
            m_Buffer = null;
            m_Length = 0;
        }

        public T[] ToArray()
        {
            var result = new T[m_Length];
            Copy(this, 0, result, 0, m_Length);
            return result;
        }

        public void CopyFrom(T[] array)
        {
            if (array.Length != m_Length) throw new ArgumentException("source and destination length must be the same");
            Copy(array, 0, this, 0, m_Length);
        }

        public void CopyTo(T[] array)
        {
            if (array.Length != m_Length) throw new ArgumentException("source and destination length must be the same");
            Copy(this, 0, array, 0, m_Length);
        }

        public NativeArray<T> GetSubArray(int start, int length)
        {
            if (start < 0 || length < 0 || start + length > m_Length) throw new ArgumentOutOfRangeException(nameof(start));
            return new NativeArray<T>((byte*)m_Buffer + (long)start * Unsafe.SizeOf<T>(), length, owned: false);
        }

        static void CheckRange(int index, int length, int total, string name)
        {
            if (index < 0 || length < 0 || index + length > total)
                throw new ArgumentOutOfRangeException(name, "Index and length must refer to a location within the array.");
        }

        public static void Copy(T[] src, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            CheckRange(srcIndex, length, src.Length, nameof(srcIndex));
            CheckRange(dstIndex, length, dst.m_Length, nameof(dstIndex));
            if (length == 0) return;
            new ReadOnlySpan<T>(src, srcIndex, length).CopyTo(new Span<T>((byte*)dst.m_Buffer + (long)dstIndex * Unsafe.SizeOf<T>(), length));
        }

        public static void Copy(NativeArray<T> src, int srcIndex, T[] dst, int dstIndex, int length)
        {
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            CheckRange(srcIndex, length, src.m_Length, nameof(srcIndex));
            CheckRange(dstIndex, length, dst.Length, nameof(dstIndex));
            if (length == 0) return;
            new ReadOnlySpan<T>((byte*)src.m_Buffer + (long)srcIndex * Unsafe.SizeOf<T>(), length).CopyTo(new Span<T>(dst, dstIndex, length));
        }

        public static void Copy(NativeArray<T> src, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
            CheckRange(srcIndex, length, src.m_Length, nameof(srcIndex));
            CheckRange(dstIndex, length, dst.m_Length, nameof(dstIndex));
            if (length == 0) return;
            Buffer.MemoryCopy((byte*)src.m_Buffer + (long)srcIndex * Unsafe.SizeOf<T>(), (byte*)dst.m_Buffer + (long)dstIndex * Unsafe.SizeOf<T>(),
                (long)length * Unsafe.SizeOf<T>(), (long)length * Unsafe.SizeOf<T>());
        }

        public static void Copy(T[] src, NativeArray<T> dst) => Copy(src, 0, dst, 0, src.Length);
        public static void Copy(NativeArray<T> src, T[] dst) => Copy(src, 0, dst, 0, src.m_Length);
        public static void Copy(NativeArray<T> src, NativeArray<T> dst) => Copy(src, 0, dst, 0, src.m_Length);

        public Enumerator GetEnumerator() => new Enumerator(ref this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(ref this);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<T>
        {
            private NativeArray<T> m_Array;
            private int m_Index;

            public Enumerator(ref NativeArray<T> array)
            {
                m_Array = array;
                m_Index = -1;
            }

            public void Dispose() { }
            public bool MoveNext() => ++m_Index < m_Array.m_Length;
            public void Reset() => m_Index = -1;
            public T Current => m_Array[m_Index];
            object IEnumerator.Current => Current;
        }

        public bool Equals(NativeArray<T> other) => m_Buffer == other.m_Buffer && m_Length == other.m_Length;
        public override bool Equals(object obj) => obj is NativeArray<T> other && Equals(other);
        public override int GetHashCode() => ((int)(nint)m_Buffer * 397) ^ m_Length;
        public static bool operator ==(NativeArray<T> left, NativeArray<T> right) => left.Equals(right);
        public static bool operator !=(NativeArray<T> left, NativeArray<T> right) => !left.Equals(right);
    }
}

namespace Unity.Collections.LowLevel.Unsafe
{
    public static unsafe partial class NativeArrayUnsafeUtility
    {
        public static void* GetUnsafePtr<T>(NativeArray<T> nativeArray) where T : struct => nativeArray.m_Buffer;
        public static void* GetUnsafeReadOnlyPtr<T>(NativeArray<T> nativeArray) where T : struct => nativeArray.m_Buffer;
        public static void* GetUnsafeBufferPointerWithoutChecks<T>(NativeArray<T> nativeArray) where T : struct => nativeArray.m_Buffer;
    }
}

