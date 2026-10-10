using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Views;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// <c>get_graph</c> の結果: グラフの中身を、エージェントが読める辞書にする。
    /// ノードは平らな一覧で、どのコンテナに入っているかは <c>parent</c>（ルートなら null）で表す（アセットと同じ形）。
    /// </summary>
    public static class GraphDescription
    {
        /// <summary>グラフ全体（パス・名前・ノード・エッジ・パラメータ）。</summary>
        public static Dictionary<string, object> Describe(NodeGraphAsset asset, string path)
        {
            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["name"] = asset.name,
                ["nodes"] = asset.Nodes.Where(n => n != null).Select(n => DescribeNode(asset, n)).ToList(),
                ["edges"] = asset.Edges.Select(DescribeEdge).ToList(),
                ["parameters"] = asset.Parameters.Where(p => p != null).Select(DescribeParameter).ToList(),
            };
        }

        /// <summary>ノード 1 つ（ID・型・名前・親・位置・型ごとの値・ポート）。</summary>
        public static Dictionary<string, object> DescribeNode(NodeGraphAsset asset, NodeData node)
        {
            var description = new Dictionary<string, object>
            {
                ["id"] = node.Id,
                ["type"] = NodeDisplay.GetTypeDisplayName(node.GetType()),
                ["label"] = NodeDisplay.GetNodeLabel(node),
                ["title"] = node.HasCustomTitle ? node.Title : string.Empty,
                ["parent"] = node.IsAtRoot ? null : node.ParentId,
                ["position"] = new Dictionary<string, object> { ["x"] = node.Position.x, ["y"] = node.Position.y },
            };

            switch (node)
            {
                case SceneNode scene:
                    description["scene"] = scene.Scene.IsEmpty
                        ? null
                        : new Dictionary<string, object> { ["name"] = scene.Scene.Name, ["path"] = scene.Scene.Path };
                    break;
                case EventNode eventNode:
                    description["eventName"] = eventNode.EventName ?? string.Empty;
                    break;
                case StateNode state:
                    description["description"] = state.Description ?? string.Empty;
                    break;
                case NoteNode note:
                    description["text"] = note.Text ?? string.Empty;
                    break;
                case ContainerNode container:
                    description["exits"] = container.Exits.Select(e => new Dictionary<string, object> { ["id"] = e.Id, ["name"] = e.Name }).ToList();
                    break;
                case ContainerExitNode exitNode:
                    var exitName = ContainerExitNodeView.GetExitName(asset, exitNode);
                    description["exit"] = exitName == null ? null : new Dictionary<string, object> { ["id"] = exitNode.ExitId, ["name"] = exitName };
                    break;
            }

            if (node is IBehaviourHost host && host.Behaviours.Count > 0)
            {
                description["behaviours"] = host.Behaviours.Select(b => b == null ? NodeDisplay.MissingBehaviourName : b.GetType().FullName).ToList();
            }

            var (inputs, outputs) = NodePorts.Get(asset, node);
            description["ports"] = new Dictionary<string, object>
            {
                ["inputs"] = inputs.Select(DescribePort).ToList(),
                ["outputs"] = outputs.Select(DescribePort).ToList(),
            };
            return description;
        }

        private static Dictionary<string, object> DescribePort((string Id, string Label) port) =>
            new() { ["id"] = port.Id, ["label"] = port.Label };

        private static Dictionary<string, object> DescribeEdge(EdgeData edge) => new()
        {
            ["from"] = edge.FromNodeId,
            ["fromPort"] = edge.FromPort,
            ["to"] = edge.ToNodeId,
            ["toPort"] = edge.ToPort,
        };

        private static Dictionary<string, object> DescribeParameter(GraphParameter parameter)
        {
            object value = parameter.Type switch
            {
                GraphParameterType.Bool => parameter.BoolValue,
                GraphParameterType.Int => parameter.IntValue,
                GraphParameterType.Float => parameter.FloatValue,
                _ => parameter.StringValue,
            };
            return new Dictionary<string, object> { ["name"] = parameter.Name, ["type"] = parameter.Type, ["default"] = value };
        }
    }
}
