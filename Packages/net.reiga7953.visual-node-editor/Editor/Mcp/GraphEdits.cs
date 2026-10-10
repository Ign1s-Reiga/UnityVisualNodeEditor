using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Search;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// MCP のツールがグラフを書き換える処理。エディタでの操作と同じ規則で行い（置ける階層、同じ階層だけのエッジ、コンテナの中身）、
    /// <see cref="Undo"/> に記録し（エディタで Undo できる）、アセットを保存して、開いているウィンドウに知らせる（<see cref="Edited"/>）。
    /// できないときは、エージェントが直せる理由を添えた <see cref="McpToolException"/>。
    /// </summary>
    public static class GraphEdits
    {
        // 位置を指定しないとき、その階層の一番右のノードからどれだけ右に置くか
        private const float NextColumnSpacing = 250f;

        /// <summary>グラフが書き換わったとき。開いているウィンドウは表示を作り直す。</summary>
        public static event Action<NodeGraphAsset> Edited;

        /// <summary>新しいグラフ（Entry 入り）を <paramref name="path"/> に作る。無いフォルダは作る。</summary>
        public static NodeGraphAsset CreateGraph(string path)
        {
            var normalized = (path ?? string.Empty).Replace('\\', '/').Trim();
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) || !normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new McpToolException($"The path must be inside Assets and end with .asset, e.g. 'Assets/Flows/Main.asset' (got '{path}').");
            }

            if (AssetDatabase.LoadMainAssetAtPath(normalized) != null)
            {
                throw new McpToolException($"Something already exists at '{normalized}'. Pick another path.");
            }

            EnsureFolder(Path.GetDirectoryName(normalized)?.Replace('\\', '/'));
            var graph = NodeGraphFactory.CreateNew();
            AssetDatabase.CreateAsset(graph, normalized);
            AssetDatabase.SaveAssets();
            return graph;
        }

        /// <summary>
        /// <paramref name="nodeType"/> のノードを <paramref name="parentId"/> の階層（null・空ならルート）に作る。
        /// <paramref name="configure"/> で値を入れてから追加する（コンテナの出口もここで決める）。コンテナは中に Entry / Exit も作る。
        /// </summary>
        public static NodeData AddNode(NodeGraphAsset asset, Type nodeType, string parentId, Vector2? position, Action<NodeData> configure)
        {
            var level = parentId ?? string.Empty;
            if (level.Length > 0 && !(asset.FindNode(level) is ContainerNode))
            {
                throw new McpToolException($"'{parentId}' is not a container in this graph. Pass the id of a Container node, or leave 'parent' out for the root.");
            }

            if (!NodeMenuCatalog.IsAvailableAt(nodeType, level.Length > 0))
            {
                throw new McpToolException(level.Length > 0
                    ? $"A {NodeDisplay.GetTypeDisplayName(nodeType)} node can only be at the root of the graph, not inside a container."
                    : $"A {NodeDisplay.GetTypeDisplayName(nodeType)} node can only be inside a container (pass the container's id as 'parent').");
            }

            var node = (NodeData)Activator.CreateInstance(nodeType);
            node.ParentId = level;
            node.Position = position ?? GetNextPosition(asset, level);
            if (node is ContainerExitNode exitNode)
            {
                ContainerContents.PointAtFirstExit(asset, exitNode);
            }

            // 値の誤りはここで分かる（まだグラフに入れていないので、失敗しても何も変わらない）
            configure?.Invoke(node);
            Undo.RecordObject(asset, "MCP: Add Node");
            asset.AddNode(node);
            if (node is ContainerNode container)
            {
                ContainerContents.Create(asset, container);
            }

            Commit(asset);
            return node;
        }

        /// <summary>
        /// ノードの値を変える。<paramref name="prepare"/> は値をすべて確かめてから、変更を行う処理を返す
        /// （確かめる途中で失敗したら何も変えない。一部だけ変わったまま残らないように）。
        /// </summary>
        public static NodeData UpdateNode(NodeGraphAsset asset, string nodeId, Func<NodeData, Action> prepare)
        {
            var node = FindNode(asset, nodeId);
            var apply = prepare(node);
            Undo.RecordObject(asset, "MCP: Update Node");
            apply();
            Commit(asset);
            return node;
        }

        /// <summary>ノードを消す（コンテナなら中身ごと）。コンテナの Entry は単独では消さない（エディタと同じ）。</summary>
        public static void RemoveNode(NodeGraphAsset asset, string nodeId)
        {
            var node = FindNode(asset, nodeId);
            if (node is ContainerEntryNode)
            {
                throw new McpToolException("A container's Entry is created and removed with its container. Remove the container instead.");
            }

            Undo.RecordObject(asset, "MCP: Remove Node");
            asset.RemoveNode(node);
            Commit(asset);
        }

        /// <summary>
        /// <paramref name="fromId"/> の出力ポートから <paramref name="toId"/> の入力ポートへエッジを足す。
        /// ポートは ID か表示名（コンテナの出口の名前）で指し、省略するとそのノードの唯一のポート（ふつうは <c>out</c> / <c>in</c>）。
        /// </summary>
        public static EdgeData Connect(NodeGraphAsset asset, string fromId, string fromPort, string toId, string toPort)
        {
            var from = FindNode(asset, fromId);
            var to = FindNode(asset, toId);
            if (from == to)
            {
                throw new McpToolException("A node cannot be connected to itself.");
            }

            if (!NodeGraphAsset.IsSameLevel(from.ParentId, to.ParentId))
            {
                throw new McpToolException(
                    $"'{NodeDisplay.GetNodeLabel(from)}' and '{NodeDisplay.GetNodeLabel(to)}' are in different containers. " +
                    "Edges only connect nodes in the same container; leave a container through an Exit node and its output port.");
            }

            var outputId = ResolvePort(from, NodePorts.GetOutputs(asset, from), fromPort, "output");
            var inputId = ResolvePort(to, NodePorts.GetInputs(asset, to), toPort, "input");
            if (asset.FindEdge(from.Id, outputId, to.Id, inputId) != null)
            {
                throw new McpToolException("These ports are already connected.");
            }

            var edge = new EdgeData(from.Id, outputId, to.Id, inputId);
            Undo.RecordObject(asset, "MCP: Connect");
            asset.AddEdge(edge);
            Commit(asset);
            return edge;
        }

        /// <summary>エッジを消す。ポートを省略すると、その 2 つのノードの間のエッジすべて。消した数を返す。</summary>
        public static int Disconnect(NodeGraphAsset asset, string fromId, string fromPort, string toId, string toPort)
        {
            var from = FindNode(asset, fromId);
            var to = FindNode(asset, toId);
            var outputId = string.IsNullOrEmpty(fromPort) ? null : ResolvePort(from, NodePorts.GetOutputs(asset, from), fromPort, "output");
            var inputId = string.IsNullOrEmpty(toPort) ? null : ResolvePort(to, NodePorts.GetInputs(asset, to), toPort, "input");
            var edges = asset.Edges
                .Where(e => e.FromNodeId == from.Id && e.ToNodeId == to.Id
                            && (outputId == null || e.FromPort == outputId) && (inputId == null || e.ToPort == inputId))
                .ToList();
            if (edges.Count == 0)
            {
                throw new McpToolException($"There is no edge from '{NodeDisplay.GetNodeLabel(from)}' to '{NodeDisplay.GetNodeLabel(to)}' to remove.");
            }

            Undo.RecordObject(asset, "MCP: Disconnect");
            foreach (var edge in edges)
            {
                asset.RemoveEdge(edge);
            }

            Commit(asset);
            return edges.Count;
        }

        /// <summary>ノードをコンテナにまとめる（Group into Container と同じ規則）。まとめられなければ理由を返す。</summary>
        public static ContainerNode Group(NodeGraphAsset asset, IReadOnlyList<string> nodeIds, string title)
        {
            var nodes = nodeIds.Select(id => FindNode(asset, id)).ToList();
            var level = nodes[0].ParentId ?? string.Empty;
            if (nodes.Any(n => !NodeGraphAsset.IsSameLevel(n.ParentId, level)))
            {
                throw new McpToolException("All the nodes to group must be in the same container (or all at the root).");
            }

            if (ContainerGrouping.Plan(asset, nodeIds, level, out var problem) == null)
            {
                throw new McpToolException(problem);
            }

            Undo.RecordObject(asset, "MCP: Group into Container");
            var container = ContainerGrouping.Apply(asset, nodeIds, level, out _);
            if (!string.IsNullOrWhiteSpace(title))
            {
                container.Title = title.Trim();
            }

            Commit(asset);
            return container;
        }

        /// <summary>ID のノード。無ければ、直し方を添えた <see cref="McpToolException"/>。</summary>
        public static NodeData FindNode(NodeGraphAsset asset, string nodeId) =>
            asset.FindNode(nodeId) ?? throw new McpToolException($"No node with id '{nodeId}' in '{asset.name}'. Use get_graph to see the node ids.");

        /// <summary>シーンのアセットのパスから参照を作る。無ければ <see cref="McpToolException"/>。</summary>
        public static SceneReference FindScene(string scenePath)
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (scene == null)
            {
                throw new McpToolException($"No scene at '{scenePath}'. Use a scene asset path such as 'Assets/Scenes/Title.unity'.");
            }

            return new SceneReference(AssetDatabase.AssetPathToGUID(scenePath), scenePath);
        }

        /// <summary>
        /// コンテナの出口を名前で探す（まず完全一致、無ければ大文字小文字を区別せず 1 つだけ当てはまるもの）。
        /// 無い・いくつも当てはまるときは、出口の一覧を添えた <see cref="McpToolException"/>。
        /// </summary>
        public static ContainerExit FindExit(ContainerNode container, string name)
        {
            var exits = container?.Exits.Where(e => e != null).ToList() ?? new List<ContainerExit>();
            var exit = MatchByName(exits, name, e => e.Name, "exit");
            if (exit != null)
            {
                return exit;
            }

            var names = exits.Count == 0 ? "(none)" : string.Join(", ", exits.Select(e => e.Name));
            throw new McpToolException($"The container has no exit named '{name}'. Its exits are: {names}.");
        }

        // 位置を指定しないときは、その階層の一番右のノードの右（同じ高さ）。階層が空なら原点
        private static Vector2 GetNextPosition(NodeGraphAsset asset, string level)
        {
            var rightmost = asset.GetChildren(level).OrderByDescending(n => n.Position.x).FirstOrDefault();
            return rightmost == null ? Vector2.zero : new Vector2(rightmost.Position.x + NextColumnSpacing, rightmost.Position.y);
        }

        private static string ResolvePort(NodeData node, List<(string Id, string Label)> ports, string requested, string kind)
        {
            if (ports.Count == 0)
            {
                throw new McpToolException($"'{NodeDisplay.GetNodeLabel(node)}' ({NodeDisplay.GetTypeDisplayName(node.GetType())}) has no {kind} port.");
            }

            if (string.IsNullOrEmpty(requested))
            {
                if (ports.Count == 1)
                {
                    return ports[0].Id;
                }

                throw new McpToolException($"'{NodeDisplay.GetNodeLabel(node)}' has several {kind} ports ({string.Join(", ", ports.Select(p => p.Label))}). Say which one.");
            }

            var byId = ports.Where(p => p.Id == requested).ToList();
            if (byId.Count == 1)
            {
                return byId[0].Id;
            }

            var match = MatchByName(ports, requested, p => p.Label, $"{kind} port");
            if (match.Id == null)
            {
                throw new McpToolException($"'{NodeDisplay.GetNodeLabel(node)}' has no {kind} port '{requested}'. Its {kind} ports are: {string.Join(", ", ports.Select(p => p.Label))}.");
            }

            return match.Id;
        }

        // 名前で 1 つ選ぶ: 完全一致が 1 つならそれ。無ければ大文字小文字を区別せずに 1 つだけ当てはまるもの。
        // 大文字小文字の違いだけでいくつも当てはまるときは、黙ってどれかを選ばずに知らせる
        private static T MatchByName<T>(IReadOnlyList<T> items, string name, Func<T, string> getName, string kind)
        {
            var exact = items.Where(item => string.Equals(getName(item), name, StringComparison.Ordinal)).ToList();
            if (exact.Count == 1)
            {
                return exact[0];
            }

            var loose = items.Where(item => string.Equals(getName(item), name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (loose.Count > 1 || exact.Count > 1)
            {
                throw new McpToolException(
                    $"More than one {kind} matches '{name}' ({string.Join(", ", loose.Select(getName))}). Use the exact name.");
            }

            return loose.Count == 1 ? loose[0] : default;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // 変更を確定する: 未保存の印を付け、アセットなら保存し（エージェントの変更が保存し忘れで消えないように）、開いているウィンドウに知らせる
        private static void Commit(NodeGraphAsset asset)
        {
            EditorUtility.SetDirty(asset);
            if (AssetDatabase.Contains(asset))
            {
                AssetDatabase.SaveAssetIfDirty(asset);
            }

            Edited?.Invoke(asset);
        }
    }
}
