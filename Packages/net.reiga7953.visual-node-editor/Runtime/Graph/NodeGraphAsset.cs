using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ノードグラフ 1 枚分を保持する ScriptableObject。
    /// ノード・エッジは <see cref="SerializeReference"/> により多態シリアライズされる。
    /// </summary>
    [CreateAssetMenu(menuName = "Visual Node Editor/Node Graph", fileName = "NewNodeGraph")]
    public sealed class NodeGraphAsset : ScriptableObject
    {
        [SerializeReference] private List<NodeData> _nodes = new();
        [SerializeField] private List<EdgeData> _edges = new();

        /// <summary>グラフに含まれる全ノード。</summary>
        public IReadOnlyList<NodeData> Nodes => _nodes;

        /// <summary>グラフに含まれる全エッジ。</summary>
        public IReadOnlyList<EdgeData> Edges => _edges;

        public void AddNode(NodeData node) => _nodes.Add(node);

        public bool RemoveNode(NodeData node)
        {
            _edges.RemoveAll(e => e.FromNodeId == node.Id || e.ToNodeId == node.Id);
            return _nodes.Remove(node);
        }

        public void AddEdge(EdgeData edge) => _edges.Add(edge);

        public bool RemoveEdge(EdgeData edge) => _edges.Remove(edge);

        public NodeData FindNode(string id) => _nodes.Find(n => n.Id == id);
    }
}
