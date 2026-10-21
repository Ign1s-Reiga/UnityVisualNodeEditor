using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// 条件付き遷移（Event の条件とトリガー）を検査する。docs/01-architecture.md「条件付き遷移」の「検証」と対応する。
    /// </summary>
    internal static class ConditionValidator
    {
        public static List<GraphIssue> Validate(
            IReadOnlyList<NodeData> nodes, IReadOnlyList<EdgeData> edges, IReadOnlyList<GraphParameter> parameters)
        {
            var issues = new List<GraphIssue>();
            var present = nodes.Where(n => n != null).ToList();
            var nodesById = new Dictionary<string, NodeData>();
            foreach (var node in present)
            {
                nodesById.TryAdd(node.Id, node);
            }

            var parametersById = new Dictionary<string, GraphParameter>();
            foreach (var parameter in parameters.Where(p => p != null && !string.IsNullOrEmpty(p.Id)))
            {
                parametersById.TryAdd(parameter.Id, parameter);
            }

            var events = present.OfType<EventNode>().ToList();
            foreach (var eventNode in events)
            {
                CheckConditions(eventNode, parametersById, issues);
            }

            var validEdges = edges.Where(e => e != null && nodesById.ContainsKey(e.FromNodeId ?? string.Empty) && nodesById.ContainsKey(e.ToNodeId ?? string.Empty)).ToList();
            CheckUnusedConditions(events, validEdges, nodesById, issues);
            CheckHiddenTransitions(present, validEdges, nodesById, issues);
            return issues;
        }

        // 条件のパラメータが無い・比較が型に合わない
        private static void CheckConditions(EventNode eventNode, IReadOnlyDictionary<string, GraphParameter> parametersById, List<GraphIssue> issues)
        {
            foreach (var condition in eventNode.Conditions.Where(c => c != null))
            {
                if (!parametersById.TryGetValue(condition.ParameterId ?? string.Empty, out var parameter))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"'{eventNode.Title}' has a condition on a parameter that no longer exists.", eventNode.Id));
                }
                else if (!TransitionCondition.Supports(parameter.Type, condition.Operator))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Error,
                        $"'{eventNode.Title}' compares the {parameter.Type} parameter '{parameter.Name}' with " +
                        $"'{TransitionCondition.GetSymbol(condition.Operator)}', which does not work for that type.", eventNode.Id));
                }
            }
        }

        // 条件・自動を見るのは待機ノードから直接繋がった Event だけ。Event（とコンテナの出口）からしか来ない Event では使われない
        private static void CheckUnusedConditions(
            IEnumerable<EventNode> events, IReadOnlyList<EdgeData> edges, IReadOnlyDictionary<string, NodeData> nodesById, List<GraphIssue> issues)
        {
            foreach (var eventNode in events.Where(e => e.IsAutomatic || e.Conditions.Count > 0))
            {
                var sources = edges.Where(e => e.ToNodeId == eventNode.Id).Select(e => nodesById[e.FromNodeId]).ToList();
                if (sources.Count > 0 && sources.All(s => s is EventNode || s is ContainerNode))
                {
                    var what = eventNode.IsAutomatic ? "automatic trigger and conditions are" : "conditions are";
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning,
                        $"'{eventNode.Title}' is only reached through another Event or a container exit, so its {what} not used. " +
                        "Connect it straight from a State, Scene or other waiting node.", eventNode.Id));
                }
            }
        }

        // 選ばれることの無いエッジ（条件の無い自動の Event・同じ名前で条件の無い Event の後ろ、2 つ目以降の Advance の行き先、Event の 2 本目以降、
        // 入るとすぐに出ていくノード・待たない Entry の遷移）。問題は選ばれない Event か、並べ替えるノードに付ける（TransitionOrder.AnchorOf）
        private static void CheckHiddenTransitions(
            IEnumerable<NodeData> nodes, IReadOnlyList<EdgeData> edges, IReadOnlyDictionary<string, NodeData> nodesById, List<GraphIssue> issues)
        {
            var outgoing = new Dictionary<string, List<EdgeData>>();
            foreach (var edge in edges)
            {
                if (!outgoing.TryGetValue(edge.FromNodeId, out var list))
                {
                    outgoing.Add(edge.FromNodeId, list = new List<EdgeData>());
                }

                list.Add(edge);
            }

            IEnumerable<EdgeData> OutgoingOf(string id) => outgoing.TryGetValue(id, out var list) ? list : Enumerable.Empty<EdgeData>();
            NodeData FindNode(string id) => nodesById.TryGetValue(id, out var found) ? found : null;

            foreach (var node in nodes.Where(n => outgoing.ContainsKey(n.Id)))
            {
                foreach (var hidden in TransitionOrder.FindHidden(node, OutgoingOf, FindNode))
                {
                    issues.Add(new GraphIssue(GraphIssueSeverity.Warning, TransitionOrder.Describe(node, hidden), TransitionOrder.AnchorOf(node, hidden)));
                }
            }
        }
    }
}
