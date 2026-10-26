using System;
using System.Text;

namespace UnityEngine.Networking
{
    /// A plain form field for UnityWebRequest.Post's multipart overload.
    public partial class MultipartFormDataSection
    {
        private string name;
        private byte[] data;
        private string content;

        public MultipartFormDataSection(string name, byte[] data, string contentType)
        {
            if (data == null || data.Length < 1)
                throw new ArgumentException("Cannot create a multipart form data section without body data");
            this.name = name;
            this.data = data;
            content = contentType;
        }

        public MultipartFormDataSection(string name, byte[] data) : this(name, data, (string)null) { }

        public MultipartFormDataSection(byte[] data) : this(null, data) { }

        // A null encoding throws NullReferenceException, as in Unity. A non-null content type
        // without "encoding=" gets "; encoding=<WebName>" appended (Unity's spelling, not "charset").
        public MultipartFormDataSection(string name, string data, Encoding encoding, string contentType)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("Cannot create a multipart form data section without body data");
            byte[] bytes = encoding.GetBytes(data);
            if (contentType != null && !contentType.Contains("encoding="))
                contentType = contentType.Trim() + "; encoding=" + encoding.WebName;
            this.name = name;
            this.data = bytes;
            content = contentType;
        }

        public MultipartFormDataSection(string name, string data, string contentType) : this(name, data, Encoding.UTF8, contentType) { }

        // Content type "text/plain; encoding=utf-8".
        public MultipartFormDataSection(string name, string data) : this(name, data, "text/plain") { }

        public MultipartFormDataSection(string data) : this(null, data) { }

        public string sectionName => name;
        public byte[] sectionData => data;
        // A data section never carries a file name.
        public string fileName => null;
        public string contentType => content;
    }

    /// A file upload part for UnityWebRequest.Post's multipart overload.
    public partial class MultipartFormFileSection
    {
        private string name;
        private byte[] data;
        private string file;
        private string content;

        // Empty or null file name defaults to "file.dat"; empty or null content type to
        // "application/octet-stream".
        public MultipartFormFileSection(string name, byte[] data, string fileName, string contentType)
        {
            if (data == null || data.Length < 1)
                throw new ArgumentException("Cannot create a multipart form file section without body data");
            this.name = name;
            this.data = data;
            file = string.IsNullOrEmpty(fileName) ? "file.dat" : fileName;
            content = string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType;
        }

        public MultipartFormFileSection(byte[] data) : this(null, data, null, null) { }

        public MultipartFormFileSection(string fileName, byte[] data) : this(null, data, fileName, null) { }

        // Text upload: null encoding means UTF-8; empty or null file name defaults to "file.txt";
        // the content type is always "text/plain; charset=<WebName>".
        public MultipartFormFileSection(string name, string data, Encoding dataEncoding, string fileName)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("Cannot create a multipart form file section without body data");
            dataEncoding ??= Encoding.UTF8;
            this.name = name;
            this.data = dataEncoding.GetBytes(data);
            file = string.IsNullOrEmpty(fileName) ? "file.txt" : fileName;
            content = "text/plain; charset=" + dataEncoding.WebName;
        }

        public MultipartFormFileSection(string data, Encoding dataEncoding, string fileName) : this(null, data, dataEncoding, fileName) { }

        public MultipartFormFileSection(string data, string fileName) : this(data, null, fileName) { }

        public string sectionName => name;
        public byte[] sectionData => data;
        public string fileName => file;
        public string contentType => content;
    }
}
