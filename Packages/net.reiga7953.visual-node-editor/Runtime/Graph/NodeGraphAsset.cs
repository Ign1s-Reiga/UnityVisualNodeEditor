using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ノードグラフ 1 枚分を保持する ScriptableObject。
    /// ノード・エッジは <see cref="SerializeReference"/> により多態シリアライズされる。
    /// 新しいグラフはエディタのメニュー（Assets > Create > Visual Node Editor > Node Graph）で Entry 付きで作る。
    /// </summary>
    public sealed class NodeGraphAsset : ScriptableObject
    {
        [SerializeReference] private List<NodeData> _nodes = new();
        [SerializeField] private List<EdgeData> _edges = new();
        [SerializeField] private List<GroupData> _groups = new();
        [SerializeField] private List<StickyNoteData> _stickyNotes = new();
        [SerializeField] private List<GraphParameter> _parameters = new();

        // セーブの置き場の名前（エディタが新しいグラフを作るときに新しい ID を入れ、複製したら入れ直す。NodeGraphFactory・GraphSaveKeySync）
        [SerializeField, HideInInspector] private string _saveKey;

        /// <summary>
        /// グラフごとに一意なセーブの置き場の名前（<see cref="GraphSaveSlots"/> のフォルダに付ける）。エディタが新しいグラフを作るときに新しい ID を入れ、
        /// アセットを複製したら別の ID に入れ直すので、同じ名前の別のグラフとスロットが混ざらない。
        /// アセットの GUID とは限らない（新しく作ったグラフはランダムな ID。0.13.1 で入れたものと、複製して入れ直したものはそのアセットの GUID）。
        /// 空（0.13.0 までに作ったアセット。書き換えない）ならグラフ名だけで置き場を決める。
        /// </summary>
        public string SaveKey
        {
            get => _saveKey ?? string.Empty;
            internal set => _saveKey = value;
        }

        /// <summary>グラフに含まれる全ノード。型を読み込めなかった要素は null になりうる。</summary>
        public IReadOnlyList<NodeData> Nodes => _nodes;

        /// <summary>グラフに含まれる全エッジ。</summary>
        public IReadOnlyList<EdgeData> Edges => _edges;

        /// <summary>グラフに含まれる全グループ。</summary>
        public IReadOnlyList<GroupData> Groups => _groups;

        /// <summary>グラフに含まれる全付箋。</summary>
        public IReadOnlyList<StickyNoteData> StickyNotes => _stickyNotes;

        /// <summary>グラフのパラメータ（Blackboard の並び順）。</summary>
        public IReadOnlyList<GraphParameter> Parameters => _parameters;

        public void AddParameter(GraphParameter parameter) => _parameters.Add(parameter);

        public bool RemoveParameter(GraphParameter parameter) => _parameters.Remove(parameter);

        public GraphParameter FindParameter(string id) => _parameters.Find(p => p != null && p.Id == id);

        /// <summary>名前が一致する最初のパラメータ（大文字小文字を区別する）。無ければ null。</summary>
        public GraphParameter FindParameterByName(string name) => _parameters.Find(p => p != null && p.Name == name);

        /// <summary>パラメータを <paramref name="index"/> の位置へ移す（範囲外は端に寄せる）。含まれていなければ false。</summary>
        public bool MoveParameter(GraphParameter parameter, int index)
        {
            if (!_parameters.Remove(parameter))
            {
                return false;
            }

            _parameters.Insert(Mathf.Clamp(index, 0, _parameters.Count), parameter);
            return true;
        }

        public void AddNode(NodeData node) => _nodes.Add(node);

        /// <summary>
        /// ノードを削除し、それに接続するエッジとグループへの所属も取り除く。
        /// コンテナなら子孫（入れ子のコンテナの中身も含む）と、それらのエッジ・グループへの所属、
        /// 中の階層に置かれたグループ・付箋もまとめて削除する。
        /// </summary>
        public bool RemoveNode(NodeData node)
        {
            if (node == null || !_nodes.Contains(node))
            {
                return false;
            }

            var removed = new List<NodeData> { node };
            removed.AddRange(GetDescendants(node.Id));
            var removedIds = new HashSet<string>();
            foreach (var target in removed)
            {
                removedIds.Add(target.Id);
            }

            _edges.RemoveAll(e => removedIds.Contains(e.FromNodeId) || removedIds.Contains(e.ToNodeId));
            foreach (var group in _groups)
            {
                foreach (var id in removedIds)
                {
                    group.RemoveNode(id);
                }
            }

            // 消えるコンテナの中の階層に置かれていたグループ・付箋も消す
            _groups.RemoveAll(g => g != null && removedIds.Contains(g.ParentId));
            _stickyNotes.RemoveAll(s => s != null && removedIds.Contains(s.ParentId));

            foreach (var target in removed)
            {
                _nodes.Remove(target);
            }

            return true;
        }

        /// <summary><paramref name="parentId"/> の階層に直接置かれたノード（空文字ならルート階層）。</summary>
        public IEnumerable<NodeData> GetChildren(string parentId)
        {
            foreach (var node in _nodes)
            {
                if (node != null && IsSameLevel(node.ParentId, parentId))
                {
                    yield return node;
                }
            }
        }

        /// <summary>
        /// <paramref name="containerId"/> の子孫（子、孫、…）。親の参照が輪になっていても止まる。
        /// </summary>
        public List<NodeData> GetDescendants(string containerId)
        {
            var result = new List<NodeData>();
            if (string.IsNullOrEmpty(containerId))
            {
                return result;
            }

            var visited = new HashSet<string> { containerId };
            var queue = new Queue<string>();
            queue.Enqueue(containerId);
            while (queue.Count > 0)
            {
                var parentId = queue.Dequeue();
                foreach (var child in _nodes)
                {
                    if (child != null && child.ParentId == parentId && visited.Add(child.Id))
                    {
                        result.Add(child);
                        queue.Enqueue(child.Id);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// コンテナの出口を削除し、その出力ポートから出るエッジも削除する。削除したエッジを返す。
        /// その出口を指していた Exit ノードは残す（付け替えるまで検証で Error になる）。出口が無ければ何もせず空を返す。
        /// </summary>
        public List<EdgeData> RemoveContainerExit(ContainerNode container, string exitId)
        {
            var removedEdges = new List<EdgeData>();
            if (container == null || !container.RemoveExit(exitId))
            {
                return removedEdges;
            }

            removedEdges.AddRange(_edges.FindAll(e => e.FromNodeId == container.Id && e.FromPort == exitId));
            _edges.RemoveAll(e => e.FromNodeId == container.Id && e.FromPort == exitId);
            return removedEdges;
        }

        /// <summary>2 つの ParentId が同じ階層を指すか（null と空文字はどちらもルート）。</summary>
        public static bool IsSameLevel(string parentIdA, string parentIdB) =>
            string.Equals(parentIdA ?? string.Empty, parentIdB ?? string.Empty, System.StringComparison.Ordinal);

        public void AddEdge(EdgeData edge) => _edges.Add(edge);

        public bool RemoveEdge(EdgeData edge) => _edges.Remove(edge);

        /// <summary>
        /// <paramref name="edge"/> を、同じ位置のまま <paramref name="replacement"/> に置き換える。<paramref name="edge"/> が無ければ何もせず false。
        /// エッジの並び順は行き先を決める（Advance などは最初のエッジに進む）ので、付け替えで順を変えないために使う。
        /// </summary>
        public bool ReplaceEdge(EdgeData edge, EdgeData replacement)
        {
            var index = replacement == null ? -1 : _edges.IndexOf(edge);
            if (index < 0)
            {
                return false;
            }

            _edges[index] = replacement;
            return true;
        }

        /// <summary>
        /// <paramref name="edge"/> を、同じノードから出ているエッジ（アセット内の順）の中で <paramref name="index"/> 番目へ移す（範囲外は端に寄せる）。
        /// 同じノードからの遷移はこの順で調べる（最初に当てはまるものに進む）ので、優先順を変えるのに使う。ほかのノードのエッジの位置は変えない。
        /// <paramref name="edge"/> が無ければ何もせず false。
        /// </summary>
        public bool MoveEdge(EdgeData edge, int index)
        {
            if (edge == null || !_edges.Contains(edge))
            {
                return false;
            }

            // 同じノードのエッジが占めている位置に、並べ替えた順で入れ直す
            var slots = new List<int>();
            var siblings = new List<EdgeData>();
            for (var i = 0; i < _edges.Count; i++)
            {
                if (_edges[i] != null && _edges[i].FromNodeId == edge.FromNodeId)
                {
                    slots.Add(i);
                    siblings.Add(_edges[i]);
                }
            }

            siblings.Remove(edge);
            siblings.Insert(Mathf.Clamp(index, 0, siblings.Count), edge);
            for (var i = 0; i < slots.Count; i++)
            {
                _edges[slots[i]] = siblings[i];
            }

            return true;
        }

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
