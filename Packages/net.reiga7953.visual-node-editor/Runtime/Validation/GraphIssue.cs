namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="GraphValidator"/> が見つけた問題 1 件。</summary>
    public sealed class GraphIssue
    {
        public GraphIssue(GraphIssueSeverity severity, string message, string nodeId = null)
        {
            Severity = severity;
            Message = message;
            NodeId = nodeId;
        }

        /// <summary>重要度。</summary>
        public GraphIssueSeverity Severity { get; }

        /// <summary>人が読むためのメッセージ。</summary>
        public string Message { get; }

        /// <summary>問題に関係するノードの ID。グラフ全体の問題なら null。</summary>
        public string NodeId { get; }

        public override string ToString() => $"{Severity}: {Message}";
    }
}
