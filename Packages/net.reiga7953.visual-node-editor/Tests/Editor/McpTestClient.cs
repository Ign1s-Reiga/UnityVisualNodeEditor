using System.Collections.Generic;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor.Mcp;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>テストから MCP のメッセージを送る（HTTP を通さず <see cref="McpProtocol"/> へ直接）。</summary>
    internal static class McpTestClient
    {
        private static long _nextId = 1;

        /// <summary>要求を送り、応答（JSON-RPC の応答全体）を返す。</summary>
        public static Dictionary<string, object> Request(McpProtocol protocol, string method, Dictionary<string, object> parameters = null)
        {
            var message = new Dictionary<string, object> { ["jsonrpc"] = "2.0", ["id"] = _nextId++, ["method"] = method };
            if (parameters != null)
            {
                message["params"] = parameters;
            }

            var reply = protocol.Handle(McpJson.Serialize(message));
            Assert.That(reply, Is.Not.Null, "a request with an id gets a reply");
            return (Dictionary<string, object>)McpJson.Parse(reply);
        }

        /// <summary>ツールを呼び、結果の text と isError を返す。</summary>
        public static (string Text, bool IsError) CallTool(McpProtocol protocol, string name, Dictionary<string, object> arguments = null)
        {
            var reply = Request(protocol, "tools/call", new Dictionary<string, object>
            {
                ["name"] = name,
                ["arguments"] = arguments ?? new Dictionary<string, object>(),
            });
            Assert.That(reply.ContainsKey("result"), Is.True, "tool calls answer with a result: " + McpJson.Serialize(reply));
            var result = (Dictionary<string, object>)reply["result"];
            var content = (List<object>)result["content"];
            var text = (string)((Dictionary<string, object>)content[0])["text"];
            return (text, (bool)result["isError"]);
        }

        /// <summary>ツールを呼び、成功した結果の JSON を読んで返す（失敗ならテストを失敗にする）。</summary>
        public static Dictionary<string, object> CallToolForObject(McpProtocol protocol, string name, Dictionary<string, object> arguments = null)
        {
            var (text, isError) = CallTool(protocol, name, arguments);
            Assert.That(isError, Is.False, text);
            return (Dictionary<string, object>)McpJson.Parse(text);
        }
    }
}
