using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// MCP の JSON-RPC のメッセージを処理する（HTTP とは切り離した純粋なクラス）。
    /// <c>initialize</c> / <c>ping</c> / <c>tools/list</c> / <c>tools/call</c> と、通知（応答しない）を扱う。
    /// ツールの処理を呼ぶので、メインスレッドで使う。
    /// </summary>
    public sealed class McpProtocol
    {
        /// <summary>対応している MCP のバージョン（新しい順）。</summary>
        public static readonly IReadOnlyList<string> SupportedProtocolVersions = new[] { "2025-06-18", "2025-03-26", "2024-11-05" };

        // JSON-RPC のエラーコード
        internal const int ParseError = -32700;
        internal const int InvalidRequest = -32600;
        internal const int MethodNotFound = -32601;
        internal const int InvalidParams = -32602;
        internal const int InternalError = -32603;

        private readonly string _serverName;
        private readonly string _serverVersion;
        private readonly string _instructions;
        private readonly Dictionary<string, McpTool> _tools;
        private readonly Action<Exception> _logException;

        /// <param name="logException">ツールの思わぬ例外を記録する（既定は Console へ）。</param>
        public McpProtocol(string serverName, string serverVersion, IEnumerable<McpTool> tools, string instructions = null,
            Action<Exception> logException = null)
        {
            _serverName = serverName;
            _serverVersion = serverVersion;
            _instructions = instructions;
            _tools = (tools ?? Enumerable.Empty<McpTool>()).ToDictionary(t => t.Name, StringComparer.Ordinal);
            _logException = logException ?? Debug.LogException;
        }

        /// <summary>ツールの名前（<c>tools/list</c> の順）。</summary>
        public IEnumerable<string> ToolNames => _tools.Keys;

        /// <summary>
        /// 本文（JSON の文字列）を処理し、応答の JSON を返す。応答の要らないもの（通知・クライアントからの応答）だけなら null。
        /// メッセージの配列（JSON-RPC の batch。MCP 2025-03-26 以前で使える）なら、応答も配列で返す。
        /// </summary>
        public string Handle(string message)
        {
            object parsed;
            try
            {
                parsed = McpJson.Parse(message);
            }
            catch (FormatException exception)
            {
                return Error(null, ParseError, "Parse error: " + exception.Message);
            }

            switch (parsed)
            {
                case Dictionary<string, object> request:
                    var reply = HandleRequest(request);
                    return reply == null ? null : McpJson.Serialize(reply);
                case List<object> batch when batch.Count > 0:
                    var replies = batch
                        .Select(item => item is Dictionary<string, object> entry
                            ? HandleRequest(entry)
                            : ErrorReply(null, InvalidRequest, "Expected a JSON-RPC message object."))
                        .Where(r => r != null)
                        .ToList();
                    return replies.Count == 0 ? null : McpJson.Serialize(replies);
                default:
                    return Error(null, InvalidRequest, "Expected a JSON-RPC message object, or a non-empty array of them.");
            }
        }

        // メッセージ 1 つを処理し、応答を返す（応答の要らないものには null）
        private Dictionary<string, object> HandleRequest(Dictionary<string, object> request)
        {
            request.TryGetValue("id", out var id);
            var hasId = request.ContainsKey("id");
            if (!request.TryGetValue("method", out var methodValue) || !(methodValue is string method))
            {
                // クライアントからの応答（サーバーからは要求を送らないので、来ても無視する）か、壊れたメッセージ
                return hasId && !request.ContainsKey("result") && !request.ContainsKey("error")
                    ? ErrorReply(id, InvalidRequest, "Missing method.")
                    : null;
            }

            // 通知（id が無い）には応答しない（notifications/initialized、notifications/cancelled など）
            if (!hasId)
            {
                return null;
            }

            request.TryGetValue("params", out var paramsValue);
            var parameters = paramsValue as Dictionary<string, object> ?? new Dictionary<string, object>();
            switch (method)
            {
                case "initialize":
                    return ResultReply(id, Initialize(parameters));
                case "ping":
                    return ResultReply(id, new Dictionary<string, object>());
                case "tools/list":
                    return ResultReply(id, new Dictionary<string, object> { ["tools"] = _tools.Values.Select(DescribeTool).ToList() });
                case "tools/call":
                    return CallTool(id, parameters);
                default:
                    return ErrorReply(id, MethodNotFound, $"Method not found: {method}");
            }
        }

        /// <summary>クライアントが求めたバージョンに対応していればそれ、そうでなければ最新。</summary>
        public static string NegotiateVersion(string requested) =>
            requested != null && SupportedProtocolVersions.Contains(requested) ? requested : SupportedProtocolVersions[0];

        private Dictionary<string, object> Initialize(Dictionary<string, object> parameters)
        {
            parameters.TryGetValue("protocolVersion", out var requested);
            var result = new Dictionary<string, object>
            {
                ["protocolVersion"] = NegotiateVersion(requested as string),
                ["capabilities"] = new Dictionary<string, object>
                {
                    ["tools"] = new Dictionary<string, object> { ["listChanged"] = false },
                },
                ["serverInfo"] = new Dictionary<string, object> { ["name"] = _serverName, ["version"] = _serverVersion },
            };
            if (!string.IsNullOrEmpty(_instructions))
            {
                result["instructions"] = _instructions;
            }

            return result;
        }

        private static Dictionary<string, object> DescribeTool(McpTool tool) => new()
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["inputSchema"] = tool.InputSchema,
        };

        private Dictionary<string, object> CallTool(object id, Dictionary<string, object> parameters)
        {
            parameters.TryGetValue("name", out var nameValue);
            if (!(nameValue is string name) || !_tools.TryGetValue(name, out var tool))
            {
                return ErrorReply(id, InvalidParams, $"Unknown tool: {nameValue ?? "(none)"}");
            }

            parameters.TryGetValue("arguments", out var argumentsValue);
            if (argumentsValue != null && !(argumentsValue is Dictionary<string, object>))
            {
                return ErrorReply(id, InvalidParams, "The tool arguments must be an object.");
            }

            try
            {
                var arguments = new McpArguments(argumentsValue as Dictionary<string, object>);
                var known = tool.InputSchema.TryGetValue("properties", out var properties) && properties is IDictionary<string, object> names
                    ? names.Keys
                    : Enumerable.Empty<string>();
                var unknown = arguments.GetUnknown(known);
                if (unknown.Count > 0)
                {
                    throw new McpToolException($"Unknown argument(s): {string.Join(", ", unknown)}. Expected: {string.Join(", ", known)}.");
                }

                var output = tool.Handler(arguments);
                return ResultReply(id, ToolResult(output as string ?? McpJson.Serialize(output), false));
            }
            catch (McpToolException exception)
            {
                return ResultReply(id, ToolResult(exception.Message, true));
            }
            catch (Exception exception)
            {
                _logException(exception);
                return ResultReply(id, ToolResult($"The tool failed inside Unity: {exception.GetType().Name}: {exception.Message}", true));
            }
        }

        private static Dictionary<string, object> ToolResult(string text, bool isError) => new()
        {
            ["content"] = new List<object> { new Dictionary<string, object> { ["type"] = "text", ["text"] = text } },
            ["isError"] = isError,
        };

        private static Dictionary<string, object> ResultReply(object id, object result) => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result,
        };

        private static Dictionary<string, object> ErrorReply(object id, int code, string message) => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new Dictionary<string, object> { ["code"] = code, ["message"] = message },
        };

        /// <summary>
        /// 本文の要求（配列ならその 1 つずつ）に、同じ <c>id</c> でエラーを返す応答（処理できなかったとき。例: 時間切れ）。
        /// 応答の要る要求が無ければ null。本文が読めなければ <c>id</c> の無いエラー。<c>id</c> が合っていないと、クライアントはどの要求の失敗か分からない。
        /// </summary>
        public static string ErrorForRequests(string body, int code, string message)
        {
            object parsed;
            try
            {
                parsed = McpJson.Parse(body);
            }
            catch (FormatException)
            {
                return Error(null, code, message);
            }

            var requests = parsed is List<object> batch ? batch : new List<object> { parsed };
            var replies = requests
                .OfType<Dictionary<string, object>>()
                .Where(request => request.ContainsKey("id") && request.ContainsKey("method"))
                .Select(request => ErrorReply(request["id"], code, message))
                .ToList();
            if (replies.Count == 0)
            {
                return null;
            }

            return parsed is List<object> ? McpJson.Serialize(replies) : McpJson.Serialize(replies[0]);
        }

        /// <summary>JSON-RPC のエラー応答（JSON の文字列）。</summary>
        internal static string Error(object id, int code, string message) => McpJson.Serialize(ErrorReply(id, code, message));
    }
}
