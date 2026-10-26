using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace OniMoono.Assets.IO
{
    /// A file mapped read-only into memory for the life of the process: its pages are read as
    /// they are first touched, instead of the whole file up front (sharedassets0.assets is
    /// 391 MB, of which loading reads a part).
    public sealed unsafe class MappedFile
    {
        readonly MemoryMappedFile file;
        readonly MemoryMappedViewAccessor view;
        public byte* Pointer { get; }
        public long Length { get; }

        public MappedFile(string path)
        {
            // The file's own length (FileInfo gives a symbolic link's).
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Length = stream.Length;
            file = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
            view = file.CreateViewAccessor(0, Length, MemoryMappedFileAccess.Read);
            byte* p = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref p);
            Pointer = p + view.PointerOffset;
        }
    }
}
