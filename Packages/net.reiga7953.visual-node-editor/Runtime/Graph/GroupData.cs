using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reiga.VisualNodeEditor
{
    /// <summary>ノードをまとめる枠。所属ノードは ID で保持する。</summary>
    [Serializable]
    public sealed class GroupData
    {
        [SerializeField] private string _id = Guid.NewGuid().ToString("N");
        [SerializeField] private string _title = "Group";
        [SerializeField] private Vector2 _position;
        [SerializeField] private List<string> _nodeIds = new();

        /// <summary>グラフ内で一意な ID。</summary>
        public string Id => _id;

        /// <summary>枠に表示するタイトル。</summary>
        public string Title
        {
            get => _title;
            set => _title = value;
        }

        /// <summary>エディタ上の表示位置（Runtime では無視される）。</summary>
        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        /// <summary>所属ノードの ID。</summary>
        public IReadOnlyList<string> NodeIds => _nodeIds;

        /// <summary>ノードを所属させる。既に所属していれば何もせず false を返す。</summary>
        public bool AddNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || _nodeIds.Contains(nodeId))
            {
                return false;
            }

            _nodeIds.Add(nodeId);
            return true;
        }

        /// <summary>ノードの所属を外す。</summary>
        public bool RemoveNode(string nodeId) => _nodeIds.Remove(nodeId);

        /// <summary>新しい ID を振り直す（貼り付け・複製でコピーを作るときに使う）。</summary>
        internal void AssignNewId() => _id = Guid.NewGuid().ToString("N");

        /// <summary>所属ノードの ID をまとめて置き換える（貼り付け時に新しいノード ID へ付け替える）。</summary>
        internal void ReplaceNodeIds(IEnumerable<string> nodeIds)
        {
            _nodeIds.Clear();
            foreach (var nodeId in nodeIds)
            {
                AddNode(nodeId);
            }
        }
    }
}
