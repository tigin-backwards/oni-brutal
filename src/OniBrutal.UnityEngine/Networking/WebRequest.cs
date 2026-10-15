namespace UnityEngine.Networking
{
    public partial class DownloadHandler
    {
        /// Releases buffered data. Handlers hold no unmanaged resources.
        public virtual void Dispose() => ReleaseData();

        internal virtual void ReleaseData() { }
    }

    public partial class UnityWebRequest
    {
        internal DownloadHandler m_DownloadHandler;

        public bool disposeDownloadHandlerOnDispose { get; set; } = true;
        public bool disposeUploadHandlerOnDispose { get; set; } = true;
        public bool disposeCertificateHandlerOnDispose { get; set; } = true;

        public virtual void Dispose()
        {
            if (disposeDownloadHandlerOnDispose) m_DownloadHandler?.Dispose();
            m_DownloadHandler = null;
        }
    }
}
