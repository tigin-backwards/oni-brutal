using System;
using System.Runtime.InteropServices;

namespace OniBrutal.Engine.Video
{
    /// The system's libvpx (VP8 decoder), bound by our own function-pointer P/Invokes. The library
    /// is not shipped with the game or the engine: it is looked up under its sonames
    /// (libvpx.so.9 … libvpx.so.6, then libvpx.so) through the platform loader, which honours
    /// LD_LIBRARY_PATH. Every libvpx since 1.8 has the same decoder ABI (VPX_DECODER_ABI_VERSION 12).
    internal static unsafe class Vpx
    {
        // vpx_image.h: VPX_IMAGE_ABI_VERSION 5; vpx_codec.h: +4; vpx_decoder.h: +3.
        const int DecoderAbiVersion = 12;

        public const int ImageFormatI420 = 0x102; // VPX_IMG_FMT_PLANAR | 2

        static readonly string[] Sonames = ["libvpx.so.9", "libvpx.so.8", "libvpx.so.7", "libvpx.so.6", "libvpx.so"];

        static readonly object gate = new();
        static bool loaded;
        static string loadError;
        static string libraryName;

        static delegate* unmanaged<IntPtr> vp8DxInterface;
        static delegate* unmanaged<IntPtr, IntPtr, DecoderConfig*, long, int, int> decInitVer;
        static delegate* unmanaged<IntPtr, byte*, uint, IntPtr, long, int> decode;
        static delegate* unmanaged<IntPtr, IntPtr*, Image*> getFrame;
        static delegate* unmanaged<IntPtr, int> destroy;
        static delegate* unmanaged<IntPtr, IntPtr> codecError;
        static delegate* unmanaged<IntPtr, IntPtr> codecErrorDetail;

        /// The soname that was loaded, or null.
        public static string LibraryName
        {
            get
            {
                EnsureLoaded();
                return libraryName;
            }
        }

        /// Loads libvpx once. Returns null on success, otherwise why it is unavailable.
        public static string EnsureLoaded()
        {
            lock (gate)
            {
                if (loaded) return loadError;
                loaded = true;
                IntPtr handle = IntPtr.Zero;
                foreach (var name in Sonames)
                {
                    if (NativeLibrary.TryLoad(name, out handle))
                    {
                        libraryName = name;
                        break;
                    }
                }
                if (handle == IntPtr.Zero)
                {
                    loadError = "libvpx was not found (looked for " + string.Join(", ", Sonames) + "); install the system libvpx package to play videos.";
                    return loadError;
                }
                try
                {
                    vp8DxInterface = (delegate* unmanaged<IntPtr>)NativeLibrary.GetExport(handle, "vpx_codec_vp8_dx");
                    decInitVer = (delegate* unmanaged<IntPtr, IntPtr, DecoderConfig*, long, int, int>)NativeLibrary.GetExport(handle, "vpx_codec_dec_init_ver");
                    decode = (delegate* unmanaged<IntPtr, byte*, uint, IntPtr, long, int>)NativeLibrary.GetExport(handle, "vpx_codec_decode");
                    getFrame = (delegate* unmanaged<IntPtr, IntPtr*, Image*>)NativeLibrary.GetExport(handle, "vpx_codec_get_frame");
                    destroy = (delegate* unmanaged<IntPtr, int>)NativeLibrary.GetExport(handle, "vpx_codec_destroy");
                    codecError = (delegate* unmanaged<IntPtr, IntPtr>)NativeLibrary.GetExport(handle, "vpx_codec_error");
                    codecErrorDetail = (delegate* unmanaged<IntPtr, IntPtr>)NativeLibrary.GetExport(handle, "vpx_codec_error_detail");
                }
                catch (EntryPointNotFoundException e)
                {
                    loadError = $"{libraryName} lacks a required export: {e.Message}";
                }
                return loadError;
            }
        }

        /// vpx_codec_dec_cfg_t.
        [StructLayout(LayoutKind.Sequential)]
        struct DecoderConfig
        {
            public uint Threads;
            public uint Width;
            public uint Height;
        }

        /// vpx_image_t (ABI 5).
        [StructLayout(LayoutKind.Sequential)]
        public struct Image
        {
            public int Format;
            public int ColorSpace;
            public int Range;
            public uint Width, Height, BitDepth;
            public uint DisplayWidth, DisplayHeight;
            public uint RenderWidth, RenderHeight;
            public uint ChromaShiftX, ChromaShiftY;
            public byte* PlaneY, PlaneU, PlaneV, PlaneA;
            public int StrideY, StrideU, StrideV, StrideA;
            public int BitsPerSample;
            public IntPtr UserPrivate;
            public byte* ImageData;
            public int ImageDataOwner;
            public int SelfAllocated;
            public IntPtr FrameBufferPrivate;
        }

        /// One VP8 decoder context (vpx_codec_ctx_t, kept in unmanaged memory because libvpx
        /// holds on to it). Not thread-safe: one decoder belongs to one decode thread.
        public sealed class Decoder : IDisposable
        {
            // sizeof(vpx_codec_ctx_t) is 56 on LP64; the block is generously sized.
            const int ContextSize = 128;
            IntPtr context;

            public Decoder(int threads)
            {
                var error = EnsureLoaded();
                if (error != null) throw new InvalidOperationException(error);
                context = (IntPtr)NativeMemory.AllocZeroed(ContextSize);
                var config = new DecoderConfig { Threads = (uint)Math.Max(1, threads) };
                int result = decInitVer(context, vp8DxInterface(), &config, 0, DecoderAbiVersion);
                if (result != 0)
                {
                    var message = ErrorText();
                    NativeMemory.Free((void*)context);
                    context = IntPtr.Zero;
                    throw new InvalidOperationException($"vpx_codec_dec_init failed ({result}): {message}");
                }
            }

            /// Decodes one compressed frame and returns the image it produced (valid until the
            /// next call), or null if the frame produced no image (e.g. an invisible frame).
            public Image* Decode(ReadOnlySpan<byte> frame)
            {
                if (context == IntPtr.Zero) throw new ObjectDisposedException(nameof(Decoder));
                int result;
                fixed (byte* data = frame)
                    result = decode(context, data, (uint)frame.Length, IntPtr.Zero, 0);
                if (result != 0) throw new InvalidOperationException($"vpx_codec_decode failed ({result}): {ErrorText()}");
                IntPtr iterator = IntPtr.Zero;
                Image* image = null, next;
                // A VP8 packet yields at most one image; drain the iterator regardless.
                while ((next = getFrame(context, &iterator)) != null) image = next;
                return image;
            }

            string ErrorText()
            {
                var text = Marshal.PtrToStringUTF8(codecError(context)) ?? "";
                var detail = Marshal.PtrToStringUTF8(codecErrorDetail(context));
                return string.IsNullOrEmpty(detail) ? text : text + " (" + detail + ")";
            }

            public void Dispose()
            {
                if (context == IntPtr.Zero) return;
                destroy(context);
                NativeMemory.Free((void*)context);
                context = IntPtr.Zero;
                GC.SuppressFinalize(this);
            }

            ~Decoder()
            {
                if (context == IntPtr.Zero) return;
                destroy(context);
                NativeMemory.Free((void*)context);
            }
        }
    }
}
