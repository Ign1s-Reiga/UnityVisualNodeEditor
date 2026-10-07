using System;
using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// グラフの読み取り専用ビュー。生成時点のノードとエッジから索引を作る（以後のアセットの変更は反映しない）。
    /// 遷移先が複数あるときは、アセット内のエッジの順で並ぶ。
    /// </summary>
    public sealed class GraphQuery
    {
        private static readonly IReadOnlyList<EdgeData> NoEdges = new EdgeData[0];

        private readonly Dictionary<string, NodeData> _nodes = new();
        private readonly Dictionary<string, List<EdgeData>> _outgoing = new();

        public GraphQuery(NodeGraphAsset graph)
        {
            Graph = graph != null ? graph : throw new ArgumentNullException(nameof(graph));

            foreach (var node in graph.Nodes)
            {
                // 型を読み込めなかったノードと、ID の重複（先勝ち）は無視する
                if (node != null && !_nodes.ContainsKey(node.Id))
                {
                    _nodes.Add(node.Id, node);
                }
            }

            Entry = graph.Nodes.OfType<EntryNode>().FirstOrDefault();

            foreach (var edge in graph.Edges)
            {
                if (edge == null)
                {
                    continue;
                }

                if (!_outgoing.TryGetValue(edge.FromNodeId, out var edges))
                {
                    edges = new List<EdgeData>();
                    _outgoing.Add(edge.FromNodeId, edges);
                }

                edges.Add(edge);
            }
        }

        /// <summary>元のグラフ。</summary>
        public NodeGraphAsset Graph { get; }

        /// <summary>開始点。無ければ null（複数あれば最初のもの）。</summary>
        public EntryNode Entry { get; }

        /// <summary>ID に対応するノード。無ければ null。</summary>
        public NodeData FindNode(string nodeId) =>
            nodeId != null && _nodes.TryGetValue(nodeId, out var node) ? node : null;

        /// <summary>指定した型のノードをすべて返す（アセット内の順）。</summary>
        public IEnumerable<T> GetNodes<T>() where T : NodeData => Graph.Nodes.OfType<T>();

        /// <summary>ノードから出ているエッジ（アセット内の順）。</summary>
        public IReadOnlyList<EdgeData> GetOutgoingEdges(string nodeId) =>
            nodeId != null && _outgoing.TryGetValue(nodeId, out var edges) ? edges : NoEdges;

        /// <summary>ノードの遷移先（エッジの順、重複なし。存在しないノードを指すエッジは無視）。</summary>
        public IReadOnlyList<NodeData> GetNext(string nodeId) =>
            GetOutgoingEdges(nodeId).Select(e => FindNode(e.ToNodeId)).Where(n => n != null).Distinct().ToList();

        /// <summary>Event 以外の最初の遷移先（<see cref="GraphRunner.Advance"/> の行き先）。無ければ null。</summary>
        public NodeData GetFirstNonEventNext(string nodeId) => GetNext(nodeId).FirstOrDefault(n => !(n is EventNode));

        /// <summary>
        /// ノードから出ている、イベント名が一致する最初の Event ノード（<see cref="GraphRunner.Raise"/> の行き先）。無ければ null。
        /// </summary>
        public EventNode FindEventTransition(string nodeId, string eventName) =>
            string.IsNullOrEmpty(eventName)
                ? null
                : GetNext(nodeId).OfType<EventNode>().FirstOrDefault(e => string.Equals(e.EventName, eventName, StringComparison.Ordinal));

        /// <summary>Scene ノードが参照しているシーン（ノードの順、重複と未設定を除く）。</summary>
        public IReadOnlyList<SceneReference> GetScenes()
        {
            var seen = new HashSet<string>();
            var scenes = new List<SceneReference>();
            foreach (var scene in GetNodes<SceneNode>().Select(n => n.Scene))
            {
                var key = string.IsNullOrEmpty(scene.Guid) ? scene.Path : scene.Guid;
                if (!scene.IsEmpty && seen.Add(key))
                {
                    scenes.Add(scene);
                }
            }

            return scenes;
        }
    }
}
