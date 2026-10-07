using System.Collections.Generic;

namespace Reiga.VisualNodeEditor
{
    /// <summary>グラフの整合性を検査する。UnityEditor に依存しないのでランタイムでも使える。</summary>
    public static class GraphValidator
    {
        /// <summary><paramref name="graph"/> を検査して問題の一覧を返す。問題が無ければ空。</summary>
        public static List<GraphIssue> Validate(NodeGraphAsset graph) => Validate(graph.Nodes, graph.Edges);

        /// <summary>ノードとエッジの組を検査して問題の一覧を返す。問題が無ければ空。</summary>
        public static List<GraphIssue> Validate(IReadOnlyList<NodeData> nodes, IReadOnlyList<EdgeData> edges)
        {
            var issues = new List<GraphIssue>();
            var nodesById = new Dictionary<string, NodeData>();
            var entries = new List<NodeData>();

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                        $"Node #{i} could not be loaded. Its type may have been renamed or deleted."));
                    continue;
                }

                if (!nodesById.TryAdd(node.Id, node))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"'{node.Title}' has the same ID as another node.", node.Id));
                }

                switch (node)
                {
                    case EntryNode _:
                        entries.Add(node);
                        break;
                    case SceneNode scene when scene.Scene.IsEmpty:
                        issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                            $"'{node.Title}' has no scene assigned.", node.Id));
                        break;
                    case EventNode eventNode when string.IsNullOrEmpty(eventNode.EventName):
                        issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                            $"'{node.Title}' has no event name, so it cannot be raised.", node.Id));
                        break;
                }
            }

            if (entries.Count == 0)
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error, "The graph has no Entry node."));
            }

            for (var i = 1; i < entries.Count; i++)
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                    $"Only one Entry node is allowed, but '{entries[i].Title}' is an extra one.", entries[i].Id));
            }

            var seenEdges = new HashSet<(string, string, string, string)>();
            foreach (var edge in edges)
            {
                if (edge == null)
                {
                    continue;
                }

                var hasFrom = nodesById.TryGetValue(edge.FromNodeId ?? string.Empty, out var from);
                var hasTo = nodesById.TryGetValue(edge.ToNodeId ?? string.Empty, out var to);
                if (!hasFrom || !hasTo)
                {
                    var existingId = hasFrom ? from.Id : hasTo ? to.Id : null;
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"Edge {edge.FromNodeId}.{edge.FromPort} -> {edge.ToNodeId}.{edge.ToPort} points to a missing node.",
                        existingId));
                    continue;
                }

                if (from is NoteNode || to is NoteNode)
                {
                    var note = from is NoteNode ? from : to;
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                        $"Note '{note.Title}' should not be connected.", note.Id));
                }

                if (!seenEdges.Add((edge.FromNodeId, edge.FromPort, edge.ToNodeId, edge.ToPort)))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                        $"Edge {from.Title}.{edge.FromPort} -> {to.Title}.{edge.ToPort} is duplicated.", from.Id));
                }
            }

            return issues;
        }
    }
}
