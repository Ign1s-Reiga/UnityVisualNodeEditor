using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// コンテナ（サブグラフ）の条件を検査する。番号は docs/01-architecture.md「コンテナ」の「守るべき条件」と対応する。
    /// 条件 8（コンテナの削除で子孫も消える）は <see cref="NodeGraphAsset.RemoveNode"/> が守り、
    /// 残ってしまった子孫は条件 4（存在しない親）として検出する。
    /// 条件 9（Entry を単独で削除・複製しない）はエディタが守り、欠けたり増えたりすれば条件 2 として検出する。
    /// </summary>
    internal static class ContainerValidator
    {
        public static List<GraphIssue> Validate(
            IReadOnlyList<NodeData> nodes, IReadOnlyList<EdgeData> edges, IReadOnlyDictionary<string, NodeData> nodesById)
        {
            var issues = new List<GraphIssue>();
            var present = nodes.Where(n => n != null).ToList();

            foreach (var node in present)
            {
                CheckPlacement(node, nodesById, issues);
            }

            foreach (var container in present.OfType<ContainerNode>())
            {
                CheckEntries(container, present, issues);
                CheckExits(container, issues);
            }

            foreach (var edge in edges)
            {
                if (edge == null
                    || !nodesById.TryGetValue(edge.FromNodeId ?? string.Empty, out var from)
                    || !nodesById.TryGetValue(edge.ToNodeId ?? string.Empty, out var to))
                {
                    // 存在しないノードを指すエッジは GraphValidator が報告する
                    continue;
                }

                // 条件 1: エッジは同じ階層のノード同士だけを結ぶ
                if (!NodeGraphAsset.IsSameLevel(from.ParentId, to.ParentId))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"The edge from '{from.Title}' to '{to.Title}' connects nodes at different levels.", from.Id));
                }

                // 条件 7: コンテナの出力ポートから出るエッジは、存在する出口を指す
                if (from is ContainerNode container && container.FindExit(edge.FromPort) == null)
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"An edge leaves container '{container.Title}' from an exit that no longer exists.", container.Id));
                }
            }

            return issues;
        }

        private static void CheckPlacement(NodeData node, IReadOnlyDictionary<string, NodeData> nodesById, List<GraphIssue> issues)
        {
            // 条件 3: コンテナの Entry / Exit はコンテナの中だけ。ルートの EntryNode はルートだけ
            if (node.IsAtRoot && (node is ContainerEntryNode || node is ContainerExitNode))
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                    $"'{node.Title}' can only be placed inside a container.", node.Id));
            }

            if (!node.IsAtRoot && node is EntryNode)
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                    $"'{node.Title}' is the graph's start and can only be placed at the root level. Containers have their own Entry.", node.Id));
            }

            if (node.IsAtRoot)
            {
                return;
            }

            // 条件 4: 親は存在するコンテナで、親をたどって輪にならない
            if (!nodesById.TryGetValue(node.ParentId, out var parent) || !(parent is ContainerNode parentContainer))
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                    $"'{node.Title}' is inside a container that does not exist.", node.Id));
                return;
            }

            if (node is ContainerNode && IsInParentLoop(node, nodesById))
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                    $"Container '{node.Title}' is inside itself (the parent chain forms a loop).", node.Id));
            }

            // 条件 6: Exit ノードは親コンテナに存在する出口を指す
            if (node is ContainerExitNode exitNode && parentContainer.FindExit(exitNode.ExitId) == null)
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                    $"'{node.Title}' refers to an exit that container '{parentContainer.Title}' does not have. Choose one of its exits.",
                    node.Id));
            }
        }

        private static bool IsInParentLoop(NodeData container, IReadOnlyDictionary<string, NodeData> nodesById)
        {
            var visited = new HashSet<string>();
            var current = container;
            while (current != null && !current.IsAtRoot)
            {
                if (!visited.Add(current.Id))
                {
                    return false;
                }

                if (!nodesById.TryGetValue(current.ParentId, out var parent))
                {
                    return false;
                }

                if (parent.Id == container.Id)
                {
                    return true;
                }

                current = parent;
            }

            return false;
        }

        // 条件 2: コンテナごとに Entry がちょうど 1 つ
        private static void CheckEntries(ContainerNode container, IEnumerable<NodeData> present, List<GraphIssue> issues)
        {
            var entries = present.OfType<ContainerEntryNode>().Where(e => e.ParentId == container.Id).ToList();
            if (entries.Count == 0)
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                    $"Container '{container.Title}' has no Entry.", container.Id));
            }

            for (var i = 1; i < entries.Count; i++)
            {
                issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                    $"Container '{container.Title}' has more than one Entry; '{entries[i].Title}' is an extra one.", entries[i].Id));
            }
        }

        // 条件 5: 出口の名前は空でなく重複しない。出口の ID も重複しない
        private static void CheckExits(ContainerNode container, List<GraphIssue> issues)
        {
            var names = new HashSet<string>();
            var ids = new HashSet<string>();
            var reportedNames = new HashSet<string>();
            foreach (var exit in container.Exits)
            {
                if (exit == null)
                {
                    continue;
                }

                if (!ids.Add(exit.Id))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"Container '{container.Title}' has two exits with the same ID. Remove one and add it again.", container.Id));
                }

                if (string.IsNullOrWhiteSpace(exit.Name))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"Container '{container.Title}' has an exit with an empty name.", container.Id));
                }
                else if (!names.Add(exit.Name) && reportedNames.Add(exit.Name))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"Container '{container.Title}' has more than one exit named '{exit.Name}'.", container.Id));
                }
            }
        }
    }
}
