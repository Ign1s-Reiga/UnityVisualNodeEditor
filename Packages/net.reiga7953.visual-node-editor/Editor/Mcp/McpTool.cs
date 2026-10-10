using System;
using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>MCP のツール 1 つ（名前・説明・引数の JSON Schema・処理）。処理はメインスレッドで呼ばれる。</summary>
    public sealed class McpTool
    {
        /// <param name="handler">引数を受け取り、結果（JSON にする値、または文字列）を返す。失敗は <see cref="McpToolException"/> で知らせる。</param>
        public McpTool(string name, string description, IDictionary<string, object> inputSchema, Func<McpArguments, object> handler)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            Handler = handler;
        }

        /// <summary>ツールの名前（<c>tools/call</c> で指す）。</summary>
        public string Name { get; }

        /// <summary>エージェント向けの説明（いつ使うか・何を返すか）。</summary>
        public string Description { get; }

        /// <summary>引数の JSON Schema（<c>type: object</c>）。</summary>
        public IDictionary<string, object> InputSchema { get; }

        /// <summary>ツールの処理。</summary>
        public Func<McpArguments, object> Handler { get; }
    }
}
