using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor
{
    /// <summary>グラフの整合性を検査する。UnityEditor に依存しないのでランタイムでも使える。</summary>
    public static class GraphValidator
    {
        /// <summary><paramref name="graph"/> を検査して問題の一覧を返す。問題が無ければ空。</summary>
        public static List<GraphIssue> Validate(NodeGraphAsset graph)
        {
            var issues = Validate(graph.Nodes, graph.Edges);
            issues.AddRange(ValidateParameters(graph.Parameters));
            return issues;
        }

        /// <summary>パラメータ（Blackboard）を検査する: 空の名前・重複する名前は Error。</summary>
        public static List<GraphIssue> ValidateParameters(IReadOnlyList<GraphParameter> parameters)
        {
            var issues = new List<GraphIssue>();
            var names = new HashSet<string>();
            var reportedDuplicates = new HashSet<string>();
            var ids = new HashSet<string>();
            var reportedDuplicateIds = new HashSet<string>();
            foreach (var parameter in parameters)
            {
                if (parameter == null)
                {
                    continue;
                }

                // Blackboard は ID でパラメータを引くので、ID が重複すると別のパラメータを編集してしまう
                // （Inspector の Debug モードなどで生のリストを複製すると起きる）
                if (!ids.Add(parameter.Id) && reportedDuplicateIds.Add(parameter.Id))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"Parameter '{parameter.Name}' has the same ID as another parameter. Delete it and add it again."));
                }

                if (string.IsNullOrWhiteSpace(parameter.Name))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error, "A parameter has an empty name."));
                }
                else if (!names.Add(parameter.Name) && reportedDuplicates.Add(parameter.Name))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"More than one parameter is named '{parameter.Name}'."));
                }
            }

            return issues;
        }

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
                    // ルートの開始点。コンテナの中の EntryNode は ContainerValidator が Error にする
                    case EntryNode _ when node.IsAtRoot:
                        entries.Add(node);
                        break;
                    case SceneNode scene when scene.Scene.IsEmpty:
                        issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                            $"'{node.Title}' has no scene assigned.", node.Id, GraphIssueKind.SceneNotSet));
                        break;
                    case EventNode eventNode when string.IsNullOrEmpty(eventNode.EventName):
                        issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                            $"'{node.Title}' has no event name, so it cannot be raised.", node.Id));
                        break;
                }

                // 型が削除・改名されて読めなくなった振る舞い。実行時は飛ばす
                if (node is IBehaviourHost host && host.Behaviours.Any(b => b == null))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                        $"'{node.Title}' has a behaviour that could not be loaded. Its script may have been renamed or deleted.", node.Id));
                }
            }

            issues.AddRange(ValidateEventNames(nodes));

            if (entries.Count == 0)
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error, "The graph has no Entry node.", kind: GraphIssueKind.MissingEntry));
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

            issues.AddRange(ContainerValidator.Validate(nodes, edges, nodesById));
            return issues;
        }

        // Raise は完全一致なので、大文字小文字・前後の空白だけが違う名前（表記ゆれ）と、前後の空白を警告する
        private static IEnumerable<GraphIssue> ValidateEventNames(IReadOnlyList<NodeData> nodes)
        {
            var events = nodes.OfType<EventNode>().Where(e => !string.IsNullOrEmpty(e.EventName)).ToList();
            foreach (var eventNode in events.Where(e => EventNameCheck.HasSurroundingSpaces(e.EventName)))
            {
                yield return new GraphIssue(GraphIssueSeverity.Warning,
                    $"'{eventNode.Title}' has spaces at the start or end of its event name \"{eventNode.EventName}\". " +
                    "Raise must match exactly, so remove them.", eventNode.Id);
            }

            var reported = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var (name, looksLike) in EventNameCheck.FindNearDuplicates(events.Select(e => e.EventName)))
            {
                if (!reported.Add(name))
                {
                    continue;
                }

                foreach (var eventNode in events.Where(e => e.EventName == name))
                {
                    yield return new GraphIssue(GraphIssueSeverity.Warning,
                        $"Event name \"{name}\" differs from \"{looksLike}\" only in case or spaces. " +
                        "Raise must match exactly, so use one spelling.", eventNode.Id);
                }
            }
        }
    }
}
