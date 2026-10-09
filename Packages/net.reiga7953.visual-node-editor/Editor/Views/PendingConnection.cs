using UnityEditor.Experimental.GraphView;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>ポートからエッジを空き地へドラッグしたときの、繋ぎ先のノードを作るまでの情報（どのノードのどのポートから）。</summary>
    public sealed class PendingConnection
    {
        public PendingConnection(string nodeId, string portId, Direction direction, bool fromWaitNode)
        {
            NodeId = nodeId;
            PortId = portId;
            Direction = direction;
            FromWaitNode = fromWaitNode;
        }

        /// <summary>ドラッグを始めたノード。</summary>
        public string NodeId { get; }

        /// <summary>ドラッグを始めたポートの ID。</summary>
        public string PortId { get; }

        /// <summary>ドラッグを始めたポートの向き（出力からなら、作るノードの入力に繋ぐ）。</summary>
        public Direction Direction { get; }

        /// <summary>待機ノード（State・Scene）の出力から始めたか。そうなら検索で Event を先頭に出す。</summary>
        public bool FromWaitNode { get; }
    }
}
