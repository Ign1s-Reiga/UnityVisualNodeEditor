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
        [SerializeField] private List<GroupData> _groups = new();
        [SerializeField] private List<StickyNoteData> _stickyNotes = new();

        /// <summary>グラフに含まれる全ノード。型を読み込めなかった要素は null になりうる。</summary>
        public IReadOnlyList<NodeData> Nodes => _nodes;

        /// <summary>グラフに含まれる全エッジ。</summary>
        public IReadOnlyList<EdgeData> Edges => _edges;

        /// <summary>グラフに含まれる全グループ。</summary>
        public IReadOnlyList<GroupData> Groups => _groups;

        /// <summary>グラフに含まれる全付箋。</summary>
        public IReadOnlyList<StickyNoteData> StickyNotes => _stickyNotes;

        public void AddNode(NodeData node) => _nodes.Add(node);

        /// <summary>ノードを削除し、それに接続するエッジとグループへの所属も取り除く。</summary>
        public bool RemoveNode(NodeData node)
        {
            if (node == null)
            {
                return false;
            }

            _edges.RemoveAll(e => e.FromNodeId == node.Id || e.ToNodeId == node.Id);
            foreach (var group in _groups)
            {
                group.RemoveNode(node.Id);
            }

            return _nodes.Remove(node);
        }

        public void AddEdge(EdgeData edge) => _edges.Add(edge);

        public bool RemoveEdge(EdgeData edge) => _edges.Remove(edge);

        public void AddGroup(GroupData group) => _groups.Add(group);

        public bool RemoveGroup(GroupData group) => _groups.Remove(group);

        public NodeData FindNode(string id) => _nodes.Find(n => n != null && n.Id == id);

        /// <summary>指定したポート対を結ぶエッジを返す。存在しなければ null。</summary>
        public EdgeData FindEdge(string fromNodeId, string fromPort, string toNodeId, string toPort) =>
            _edges.Find(e => e.FromNodeId == fromNodeId && e.FromPort == fromPort
                && e.ToNodeId == toNodeId && e.ToPort == toPort);

        public GroupData FindGroup(string id) => _groups.Find(g => g.Id == id);

        public void AddStickyNote(StickyNoteData stickyNote) => _stickyNotes.Add(stickyNote);

        public bool RemoveStickyNote(StickyNoteData stickyNote) => _stickyNotes.Remove(stickyNote);

        public StickyNoteData FindStickyNote(string id) => _stickyNotes.Find(s => s.Id == id);
    }
}
