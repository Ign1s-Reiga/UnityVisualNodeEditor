using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// MCP の Streamable HTTP の、ここで使う分だけを受け持つ HTTP サーバー（127.0.0.1 だけで待ち受ける）。
    /// <c>POST /mcp</c> の本文を <c>handle</c> に渡し、返った JSON を返す（null なら 202）。<c>GET</c> / <c>DELETE</c> は 405。
    /// 受け取りは別スレッドで行い、<c>handle</c> もそのスレッドから呼ぶ（メインスレッドへ渡すのは呼び出し側）。
    /// </summary>
    public sealed class McpHttpServer : IDisposable
    {
        /// <summary>MCP のエンドポイントのパス。</summary>
        public const string EndpointPath = "/mcp";

        /// <summary>受け付ける本文の大きさの上限（バイト）。MCP の要求はこれよりずっと小さい。</summary>
        public const int MaxBodyBytes = 1024 * 1024;

        private readonly Func<string, string> _handle;
        private HttpListener _listener;
        private Thread _thread;

        /// <param name="handle">要求の本文を受け取り、応答の JSON（応答しないなら null）を返す。別スレッドから呼ばれる。</param>
        public McpHttpServer(int port, Func<string, string> handle)
        {
            Port = port;
            _handle = handle;
        }

        /// <summary>待ち受けるポート。</summary>
        public int Port { get; }

        /// <summary>待ち受けているか。</summary>
        public bool IsRunning => _listener != null && _listener.IsListening;

        /// <summary>クライアントに設定する URL。</summary>
        public string Url => $"http://127.0.0.1:{Port}{EndpointPath}";

        /// <summary>待ち受けを始める。ポートが使われているなどで始められなければ例外。</summary>
        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            try
            {
                listener.Start();
            }
            catch
            {
                // 始められなかった（ポートが使われているなど）ものを残さない
                listener.Close();
                throw;
            }

            _listener = listener;
            _thread = new Thread(() => Listen(listener)) { IsBackground = true, Name = "Visual Node Editor MCP" };
            _thread.Start();
        }

        /// <summary>待ち受けをやめる。</summary>
        public void Stop()
        {
            var listener = _listener;
            _listener = null;
            if (listener == null)
            {
                return;
            }

            try
            {
                listener.Stop();
                listener.Close();
            }
            catch (ObjectDisposedException)
            {
            }

            _thread = null;
        }

        /// <summary>待ち受けをやめる（<see cref="Stop"/> と同じ）。</summary>
        public void Dispose() => Stop();

        /// <summary>
        /// 受け付けてよい <c>Origin</c> か。無い（コマンドラインのクライアント）か、localhost / 127.0.0.1 のものだけ
        /// （ブラウザから来る DNS リバインディングを防ぐ。MCP の仕様どおり）。
        /// </summary>
        public static bool IsAllowedOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin))
            {
                return true;
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "[::1]" || uri.Host == "::1";
        }

        private void Listen(HttpListener listener)
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (Exception)
                {
                    // Stop で止めたとき
                    return;
                }

                ThreadPool.QueueUserWorkItem(_ => Respond(context));
            }
        }

        private void Respond(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            try
            {
                if (!IsAllowedOrigin(request.Headers["Origin"]))
                {
                    Send(response, 403, McpProtocol.Error(null, McpProtocol.InvalidRequest, "Origin not allowed."));
                    return;
                }

                if (request.Url.AbsolutePath.TrimEnd('/') != EndpointPath)
                {
                    Send(response, 404, null);
                    return;
                }

                if (request.HttpMethod != "POST")
                {
                    // サーバーから送る SSE の流れも、セッションも持たない
                    response.AddHeader("Allow", "POST");
                    Send(response, 405, null);
                    return;
                }

                var body = ReadBody(request.InputStream);
                if (body == null)
                {
                    Send(response, 413, McpProtocol.Error(null, McpProtocol.InvalidRequest, $"The request body is larger than {MaxBodyBytes} bytes."));
                    return;
                }

                string reply;
                try
                {
                    reply = _handle(body);
                }
                catch (TimeoutException exception)
                {
                    Send(response, 503, McpProtocol.Error(null, McpProtocol.InternalError, exception.Message));
                    return;
                }

                Send(response, reply == null ? 202 : 200, reply);
            }
            catch (Exception exception)
            {
                try
                {
                    Send(response, 500, McpProtocol.Error(null, McpProtocol.InternalError, exception.Message));
                }
                catch (Exception)
                {
                    // 接続が切れていれば返せない
                }
            }
        }

        // 本文を UTF-8 で読む。MaxBodyBytes を超えたら読むのをやめて null（大きすぎる要求でメモリを使い切らないように）
        private static string ReadBody(Stream input)
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + read > MaxBodyBytes)
                {
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }

        private static void Send(HttpListenerResponse response, int status, string json)
        {
            response.StatusCode = status;
            if (json != null)
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
            }

            response.Close();
        }
    }
}
