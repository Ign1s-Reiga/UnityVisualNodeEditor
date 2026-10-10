using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Mcp;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>MCP の JSON-RPC の処理（initialize / tools/list / tools/call / 通知 / エラー）。</summary>
    public sealed class McpProtocolTests
    {
        private McpProtocol _protocol;
        private readonly List<Exception> _logged = new();

        [SetUp]
        public void SetUp()
        {
            _logged.Clear();
            _protocol = new McpProtocol("test-server", "1.2.3", new[]
            {
                new McpTool("echo", "Echo the text back.",
                    McpSchema.Object(new Dictionary<string, object> { ["text"] = McpSchema.String("Text to echo.") }, "text"),
                    args => new Dictionary<string, object> { ["echo"] = args.GetString("text") }),
                new McpTool("fail", "Always fails the way tools report problems.", McpSchema.Empty(),
                    _ => throw new McpToolException("Nothing to do here.")),
                new McpTool("crash", "Throws an unexpected exception.", McpSchema.Empty(),
                    _ => throw new InvalidOperationException("boom")),
            }, "Use echo.", _logged.Add);
        }

        [Test]
        public void Initialize_AnswersWithTheVersionToolsAndServerInfo()
        {
            var reply = McpTestClient.Request(_protocol, "initialize", new Dictionary<string, object>
            {
                ["protocolVersion"] = "2025-03-26",
                ["capabilities"] = new Dictionary<string, object>(),
                ["clientInfo"] = new Dictionary<string, object> { ["name"] = "test", ["version"] = "1" },
            });
            var result = (Dictionary<string, object>)reply["result"];

            Assert.That(result["protocolVersion"], Is.EqualTo("2025-03-26"), "a supported version is echoed back");
            Assert.That(((Dictionary<string, object>)result["capabilities"]).ContainsKey("tools"), Is.True);
            Assert.That(((Dictionary<string, object>)result["serverInfo"])["name"], Is.EqualTo("test-server"));
            Assert.That(result["instructions"], Is.EqualTo("Use echo."));
            Assert.That(McpProtocol.NegotiateVersion("1999-01-01"), Is.EqualTo(McpProtocol.SupportedProtocolVersions[0]));
        }

        [Test]
        public void Notifications_AndResponses_GetNoReply()
        {
            Assert.That(_protocol.Handle("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}"), Is.Null);
            Assert.That(_protocol.Handle("{\"jsonrpc\":\"2.0\",\"id\":5,\"result\":{}}"), Is.Null);
        }

        [Test]
        public void ToolsList_DescribesEveryTool()
        {
            var result = (Dictionary<string, object>)McpTestClient.Request(_protocol, "tools/list")["result"];
            var tools = ((List<object>)result["tools"]).Cast<Dictionary<string, object>>().ToList();

            Assert.That(tools.Select(t => t["name"]), Is.EqualTo(new[] { "echo", "fail", "crash" }));
            var schema = (Dictionary<string, object>)tools[0]["inputSchema"];
            Assert.That(schema["type"], Is.EqualTo("object"));
            Assert.That(schema["required"], Is.EqualTo(new List<object> { "text" }));
        }

        [Test]
        public void ToolsCall_ReturnsTheResultAsText()
        {
            var result = McpTestClient.CallToolForObject(_protocol, "echo", new Dictionary<string, object> { ["text"] = "hi" });

            Assert.That(result["echo"], Is.EqualTo("hi"));
        }

        [Test]
        public void ToolProblems_AreResultsTheAgentCanRead()
        {
            Assert.That(McpTestClient.CallTool(_protocol, "fail"), Is.EqualTo(("Nothing to do here.", true)));

            var (missing, missingIsError) = McpTestClient.CallTool(_protocol, "echo");
            Assert.That(missingIsError, Is.True);
            Assert.That(missing, Does.Contain("'text' is required"));

            var (unknown, unknownIsError) = McpTestClient.CallTool(_protocol, "echo",
                new Dictionary<string, object> { ["text"] = "hi", ["txet"] = "typo" });
            Assert.That(unknownIsError, Is.True);
            Assert.That(unknown, Does.Contain("txet"));

            var (crash, crashIsError) = McpTestClient.CallTool(_protocol, "crash");
            Assert.That(crashIsError, Is.True);
            Assert.That(crash, Does.Contain("boom"));
            Assert.That(_logged.Single().Message, Is.EqualTo("boom"), "unexpected exceptions are also logged for the developer");
        }

        [Test]
        public void ProtocolErrors_UseJsonRpcCodes()
        {
            Assert.That(ErrorCode(_protocol.Handle("{not json")), Is.EqualTo(-32700));
            Assert.That(ErrorCode(_protocol.Handle("[]")), Is.EqualTo(-32600));
            Assert.That(ErrorCode(_protocol.Handle("42")), Is.EqualTo(-32600));
            Assert.That(ErrorCode(_protocol.Handle("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"resources/list\"}")), Is.EqualTo(-32601));
            Assert.That(ErrorCode(_protocol.Handle("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"nope\"}}")), Is.EqualTo(-32602));
            Assert.That(McpTestClient.Request(_protocol, "ping")["result"], Is.Empty);
        }

        [Test]
        public void Batches_AnswerEveryRequestInOneArray()
        {
            // MCP 2025-03-26 以前は、いくつかのメッセージを配列でまとめて送れる
            var reply = _protocol.Handle(
                "[{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}," +
                "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"ping\"}," +
                "7]");
            var replies = ((List<object>)McpJson.Parse(reply)).Cast<Dictionary<string, object>>().ToList();

            Assert.That(replies.Count, Is.EqualTo(2), "the notification gets no reply");
            Assert.That(replies[0]["id"], Is.EqualTo(3L));
            Assert.That(replies[0].ContainsKey("result"), Is.True);
            Assert.That(((Dictionary<string, object>)replies[1]["error"])["code"], Is.EqualTo(-32600L));
            Assert.That(_protocol.Handle("[{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}]"), Is.Null, "only notifications: nothing to answer");
        }

        [Test]
        public void DeeplyNestedJson_IsRefusedInsteadOfCrashing()
        {
            // 読むのは再帰なので、深すぎる入れ子はスタックを溢れさせてエディタごと落とす。上限で止める
            var deep = new string('[', 100_000) + new string(']', 100_000);
            Assert.That(ErrorCode(_protocol.Handle(deep)), Is.EqualTo(-32700));

            var allowed = new string('[', McpJson.MaxDepth) + new string(']', McpJson.MaxDepth);
            Assert.That(McpJson.Parse(allowed), Is.InstanceOf<List<object>>());
        }

        [Test]
        public void ErrorsForUnhandledRequests_KeepTheirIds()
        {
            // 時間切れなどで処理できなかったとき、クライアントがどの要求の失敗か分かるよう同じ id で返す
            var single = (Dictionary<string, object>)McpJson.Parse(McpProtocol.ErrorForRequests(
                "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\"}", -32603, "busy"));
            Assert.That(single["id"], Is.EqualTo(7L));
            Assert.That(((Dictionary<string, object>)single["error"])["message"], Is.EqualTo("busy"));

            var batch = ((List<object>)McpJson.Parse(McpProtocol.ErrorForRequests(
                "[{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"},{\"jsonrpc\":\"2.0\",\"id\":\"a\",\"method\":\"ping\"}]",
                -32603, "busy"))).Cast<Dictionary<string, object>>().ToList();
            Assert.That(batch.Select(r => r["id"]), Is.EqualTo(new[] { "a" }), "notifications get no reply");

            Assert.That(McpProtocol.ErrorForRequests("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", -32603, "busy"), Is.Null);
            Assert.That(((Dictionary<string, object>)McpJson.Parse(McpProtocol.ErrorForRequests("{broken", -32603, "busy")))["id"], Is.Null);
        }

        [TestCase("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}")]
        [TestCase("{\"jsonrpc\":\"2.0\",\"id\":2}")]
        [TestCase("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":5}")]
        [TestCase("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}")]
        [TestCase("{\"jsonrpc\":\"2.0\",\"id\":4,\"result\":{}}")]
        [TestCase("[{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"ping\"},7,{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}]")]
        [TestCase("[]")]
        [TestCase("42")]
        [TestCase("\"text\"")]
        public void TimeoutReplies_AnswerExactlyWhatHandleAnswers(string body)
        {
            // 時間切れでも、ふだん応答するものには同じ id で必ず答える（答えないとクライアントが待ち続ける）
            var handled = _protocol.Handle(body);
            var timedOut = McpProtocol.ErrorForRequests(body, -32603, "busy");

            Assert.That(timedOut == null, Is.EqualTo(handled == null));
            if (handled != null)
            {
                Assert.That(Ids(timedOut), Is.EqualTo(Ids(handled)));
            }
        }

        private static List<object> Ids(string reply)
        {
            var parsed = McpJson.Parse(reply);
            var replies = parsed is List<object> list ? list : new List<object> { parsed };
            return replies.Cast<Dictionary<string, object>>().Select(r => r["id"]).ToList();
        }

        private static long ErrorCode(string reply)
        {
            var error = (Dictionary<string, object>)((Dictionary<string, object>)McpJson.Parse(reply))["error"];
            return (long)error["code"];
        }
    }
}
