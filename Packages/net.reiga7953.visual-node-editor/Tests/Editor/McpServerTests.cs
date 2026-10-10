using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Mcp;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>MCP の HTTP サーバー（127.0.0.1 だけ・Origin の確認・POST の応答）と設定。</summary>
    public sealed class McpServerTests
    {
        // ---- 設定 ----

        [Test]
        public void ClientConfig_PointsAtTheLocalEndpoint()
        {
            var config = (Dictionary<string, object>)McpJson.Parse(McpSettingsProvider.GetClientConfig(9123));
            var server = (Dictionary<string, object>)((Dictionary<string, object>)config["mcpServers"])["visual-node-editor"];

            Assert.That(server["type"], Is.EqualTo("http"));
            Assert.That(server["url"], Is.EqualTo("http://127.0.0.1:9123/mcp"));
        }

        [Test]
        public void Settings_KeepThePortUsable()
        {
            Assert.That(McpSettings.Clamp(8790), Is.EqualTo(8790));
            Assert.That(McpSettings.Clamp(80), Is.EqualTo(McpSettings.DefaultPort), "privileged ports fall back to the default");
            Assert.That(McpSettings.Clamp(70000), Is.EqualTo(McpSettings.DefaultPort));
            Assert.That(McpSettingsProvider.GetStatusText(false, false, null, "u"), Does.StartWith("Stopped"));
            Assert.That(McpSettingsProvider.GetStatusText(true, true, null, "http://x"), Is.EqualTo("Running at http://x"));
            Assert.That(McpSettingsProvider.GetStatusText(true, false, "Port in use", "u"), Is.EqualTo("Port in use"));
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("http://localhost:3000", true)]
        [TestCase("http://127.0.0.1:5173", true)]
        [TestCase("https://evil.example", false)]
        [TestCase("http://localhost.evil.example", false)]
        [TestCase("not a url", false)]
        public void Origin_OnlyLocalPagesMayCall(string origin, bool allowed)
        {
            Assert.That(McpHttpServer.IsAllowedOrigin(origin), Is.EqualTo(allowed));
        }

        // ---- HTTP ----

        [Test]
        public void Http_AnswersPostsOnTheEndpointOnly()
        {
            var protocol = new McpProtocol("test", "0", new McpTool[0]);
            using var server = new McpHttpServer(GetFreePort(), body => McpMainThread.Run(() => protocol.Handle(body), TimeSpan.FromSeconds(10)));
            server.Start();

            var initialize = Wait(Send("POST", server.Url, "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}"));
            Assert.That(initialize.Status, Is.EqualTo(200));
            Assert.That(initialize.Body, Does.Contain("\"protocolVersion\""));

            var notification = Wait(Send("POST", server.Url, "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}"));
            Assert.That(notification.Status, Is.EqualTo(202));

            Assert.That(Wait(Send("POST", server.Url, new string(' ', McpHttpServer.MaxBodyBytes + 1))).Status, Is.EqualTo(413));
            Assert.That(Wait(Send("GET", server.Url, null)).Status, Is.EqualTo(405), "no server-sent event stream");
            Assert.That(Wait(Send("POST", $"http://127.0.0.1:{server.Port}/other", "{}")).Status, Is.EqualTo(404));
            Assert.That(Wait(Send("POST", server.Url, "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"ping\"}", "https://evil.example")).Status,
                Is.EqualTo(403));

            server.Stop();
            Assert.That(server.IsRunning, Is.False);
        }

        [Test]
        public void Start_OnAPortInUse_FailsWithoutLeavingAListener()
        {
            var taken = new TcpListener(IPAddress.Loopback, 0);
            taken.Start();
            try
            {
                var port = ((IPEndPoint)taken.LocalEndpoint).Port;
                using var server = new McpHttpServer(port, _ => null);

                Assert.That(() => server.Start(), Throws.Exception);
                Assert.That(server.IsRunning, Is.False);
            }
            finally
            {
                taken.Stop();
            }
        }

        // ---- メインスレッドでの実行 ----

        [Test]
        public void MainThread_WorkThatNeverStarts_TimesOutAndIsDropped()
        {
            var ran = false;
            var call = Task.Run(() => McpMainThread.Run(() => ran = true, TimeSpan.FromMilliseconds(50)));

            // メインスレッド（このテスト）が処理しないまま、時間切れになる
            Assert.That(() => call.Wait(TimeSpan.FromSeconds(5)), Throws.InnerException.TypeOf<TimeoutException>());
            McpMainThread.Pump();
            Assert.That(ran, Is.False, "a timed-out request never runs later");
        }

        [Test]
        public void MainThread_WorkThatStarted_IsAwaitedPastTheTimeout()
        {
            // 時間切れの直前に始まった編集は、終わるまで待って結果を返す（保存済みの変更を「失敗」と返さない）
            var call = Task.Run(() => McpMainThread.Run(() =>
            {
                Thread.Sleep(300);
                return 42;
            }, TimeSpan.FromMilliseconds(100)));

            Assert.That(Wait(call), Is.EqualTo(42));
        }

        // HttpWebRequest は System.dll にあるので、エディタのアセンブリから追加の参照なしで使える
        private static Task<(int Status, string Body)> Send(string method, string url, string body, string origin = null) =>
            Task.Run(() =>
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;
                request.Proxy = null;
                if (origin != null)
                {
                    request.Headers["Origin"] = origin;
                }

                if (body != null)
                {
                    var bytes = Encoding.UTF8.GetBytes(body);
                    request.ContentType = "application/json";
                    request.ContentLength = bytes.Length;
                    using var stream = request.GetRequestStream();
                    stream.Write(bytes, 0, bytes.Length);
                }

                HttpWebResponse response;
                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                }
                catch (WebException exception) when (exception.Response is HttpWebResponse errorResponse)
                {
                    response = errorResponse;
                }

                using (response)
                using (var reader = new System.IO.StreamReader(response.GetResponseStream() ?? System.IO.Stream.Null))
                {
                    return ((int)response.StatusCode, reader.ReadToEnd());
                }
            });

        // 要求の処理はメインスレッド（このテストのスレッド）で行うので、待つ間にたまった処理を実行する
        private static T Wait<T>(Task<T> task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            {
                McpMainThread.Pump();
                Thread.Sleep(5);
            }

            Assert.That(task.IsCompleted, Is.True, "the server answered in time");
            return task.Result;
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
