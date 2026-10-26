using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Xunit;

namespace OniMoono.Tests
{
    public class WebRequestTests : EngineTest
    {
        /// A loopback HTTP server answering each request with handler(context).
        sealed class Server : IDisposable
        {
            readonly HttpListener listener = new();
            public readonly string Url;
            public string LastBody;
            public string LastContentType;
            public NameValueCollectionView LastHeaders;

            public sealed class NameValueCollectionView(System.Collections.Specialized.NameValueCollection c)
            {
                public string this[string name] => c[name];
            }

            public Server(Action<HttpListenerContext> handler)
            {
                int port = FreePort();
                Url = $"http://127.0.0.1:{port}/";
                listener.Prefixes.Add(Url);
                listener.Start();
                Task.Run(async () =>
                {
                    while (listener.IsListening)
                    {
                        HttpListenerContext ctx;
                        try { ctx = await listener.GetContextAsync(); }
                        catch { return; }
                        using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) LastBody = reader.ReadToEnd();
                        LastContentType = ctx.Request.ContentType;
                        LastHeaders = new NameValueCollectionView(ctx.Request.Headers);
                        try { handler(ctx); }
                        catch { }
                    }
                });
            }

            public void Dispose() => listener.Close();
        }

        static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        static void Reply(HttpListenerContext ctx, int status, string body, string contentType = "text/plain; charset=utf-8")
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            ctx.Response.AddHeader("X-Test", "yes");
            ctx.Response.OutputStream.Write(bytes);
            ctx.Response.Close();
        }

        /// Runs frames until the operation completes (it completes only on the main thread).
        static void Pump(UnityWebRequestAsyncOperation op)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!op.isDone)
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException();
                Frame();
                Thread.Sleep(5);
            }
        }

        [Fact]
        public void GetDownloadsTextAndCompletesOnMainThread()
        {
            using var server = new Server(ctx => Reply(ctx, 200, "héllo"));
            var request = UnityWebRequest.Get(server.Url + "motd.json");
            request.timeout = 3;
            request.SetRequestHeader("Content-Type", "application/json");
            Assert.Equal(-1f, request.uploadProgress);
            var op = request.SendWebRequest();
            Assert.Same(request, op.webRequest);
            int completedOn = -1;
            op.completed += _ => completedOn = Environment.CurrentManagedThreadId;
            Pump(op);
            Assert.Equal(Environment.CurrentManagedThreadId, completedOn);
            Assert.True(request.isDone);
            Assert.Equal(UnityWebRequest.Result.Success, request.result);
            Assert.Null(request.error);
            Assert.Equal(200, request.responseCode);
            Assert.Equal("héllo", request.downloadHandler.text);
            Assert.Equal(Encoding.UTF8.GetBytes("héllo"), request.downloadHandler.data);
            Assert.Equal("yes", request.GetResponseHeader("x-test"));
            Assert.Equal("yes", request.GetResponseHeaders()["X-TEST"]);
            Assert.Equal("application/json", server.LastContentType);
            Assert.StartsWith("UnityPlayer/", server.LastHeaders["User-Agent"]);
            Assert.Equal(1f, request.uploadProgress);
            Assert.Throws<InvalidOperationException>(() => request.SendWebRequest());
            Assert.Throws<InvalidOperationException>(() => request.SetRequestHeader("A", "b"));
            request.Dispose();
            Assert.Throws<NullReferenceException>(() => request.downloadHandler.text);
        }

        [Fact]
        public void HttpErrorIsAProtocolError()
        {
            using var server = new Server(ctx => Reply(ctx, 404, "missing"));
            var request = UnityWebRequest.Get(server.Url);
            Pump(request.SendWebRequest());
            Assert.Equal(UnityWebRequest.Result.ProtocolError, request.result);
            Assert.Equal(404, request.responseCode);
            Assert.Equal("HTTP/1.1 404 Not Found", request.error);
            Assert.Equal("missing", request.downloadHandler.text);
        }

        [Fact]
        public void FileRequestThatCannotBeReadStillCompletes()
        {
            // A path the runtime rejects outright (a NUL character) fails the request; it does
            // not leave it in progress.
            var request = UnityWebRequest.Get("file:///tmp/oni%00brutal");
            Pump(request.SendWebRequest());
            Assert.Equal(UnityWebRequest.Result.ConnectionError, request.result);
        }

        [Fact]
        public void RefusedConnection()
        {
            var request = UnityWebRequest.Get($"http://127.0.0.1:{FreePort()}/");
            Pump(request.SendWebRequest());
            Assert.Equal(UnityWebRequest.Result.ConnectionError, request.result);
            Assert.Equal("Cannot connect to destination host", request.error);
            Assert.Equal(-1, request.responseCode);
            Assert.Null(request.downloadHandler.data);
            Assert.Equal("", request.downloadHandler.text);
        }

        [Fact]
        public void TimeoutAndAbort()
        {
            using var server = new Server(ctx => { Thread.Sleep(3000); Reply(ctx, 200, "late"); });
            var slow = UnityWebRequest.Get(server.Url);
            slow.timeout = 1;
            Pump(slow.SendWebRequest());
            Assert.Equal("Request timeout", slow.error);

            var aborted = UnityWebRequest.Get(server.Url);
            var op = aborted.SendWebRequest();
            aborted.Abort();
            Pump(op);
            Assert.Equal(UnityWebRequest.Result.ConnectionError, aborted.result);
            Assert.Equal("Request aborted", aborted.error);
        }

        [Fact]
        public void MultipartPost()
        {
            using var server = new Server(ctx => Reply(ctx, 200, "ok"));
            var sections = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("metadata", "{}"),
                new MultipartFormFileSection("archiveFile", new byte[] { 65, 66 }, "Archive.zip", "application/octet-stream"),
            };
            var boundary = Encoding.ASCII.GetBytes("BOUNDARY");
            var request = UnityWebRequest.Post(new Uri(server.Url + "submit"), sections, boundary);
            var op = request.SendWebRequest();
            Pump(op);
            Assert.Equal(UnityWebRequest.Result.Success, request.result);
            Assert.Equal("multipart/form-data; boundary=BOUNDARY", server.LastContentType);
            Assert.Equal(
                "\r\n--BOUNDARY\r\nContent-Disposition: form-data; name=\"metadata\"\r\nContent-Type: text/plain; encoding=utf-8\r\n\r\n{}" +
                "\r\n--BOUNDARY\r\nContent-Disposition: form-data; name=\"archiveFile\"; filename=\"Archive.zip\"\r\nContent-Type: application/octet-stream\r\n\r\nAB" +
                "\r\n--BOUNDARY--\r\n",
                server.LastBody);
            Assert.Equal(1f, request.uploadProgress);
            Assert.Equal(1f, op.progress);
            Assert.Equal(40, UnityWebRequest.GenerateBoundary().Length);
        }

        [Fact]
        public void UrlNormalization()
        {
            Assert.Equal("https://localhost/a/b", new UnityWebRequest("/a/b").url);
            Assert.Equal("https://example.com/x", new UnityWebRequest("example.com/x").url);
            Assert.Equal("POST", new UnityWebRequest("http://h/", "post").method);
        }
    }
}
