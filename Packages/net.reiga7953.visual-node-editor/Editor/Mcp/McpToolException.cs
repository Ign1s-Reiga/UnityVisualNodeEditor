using System;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// ツールが目的を果たせなかったとき（グラフが無い・置けない階層など）に投げる。
    /// メッセージは、エージェントが読んで直せる文にする（ツールの結果に <c>isError: true</c> で入る）。
    /// </summary>
    public sealed class McpToolException : Exception
    {
        /// <summary><paramref name="message"/> はエージェントが読む理由と直し方。</summary>
        public McpToolException(string message)
            : base(message)
        {
        }
    }
}
