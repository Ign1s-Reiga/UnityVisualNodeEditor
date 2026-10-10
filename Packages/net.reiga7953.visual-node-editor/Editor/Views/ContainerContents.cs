using UnityEngine;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// 新しく作ったコンテナ・Exit ノードの中身を整える（エディタの Create Node と、MCP の add_node で共有する）。
    /// アセットのデータだけを変える（Undo の記録は呼び出し側で行う）。
    /// </summary>
    public static class ContainerContents
    {
        // 中の階層の左に Entry、右に出口ごとの Exit ノードを縦に並べる
        private static readonly Vector2 ExitOrigin = new Vector2(400f, 0f);
        private static readonly Vector2 ExitSpacing = new Vector2(0f, 100f);

        /// <summary>
        /// 新しいコンテナの中に、Entry と出口ごとの Exit ノードを作る。
        /// Entry は最初の出口の Exit ノードに繋ぎ、作ったばかりのコンテナがそのまま通り抜けられるようにする。作った Entry を返す。
        /// </summary>
        public static ContainerEntryNode Create(NodeGraphAsset asset, ContainerNode container)
        {
            var entry = new ContainerEntryNode { ParentId = container.Id, Position = Vector2.zero };
            asset.AddNode(entry);
            for (var i = 0; i < container.Exits.Count; i++)
            {
                var exitNode = new ContainerExitNode
                {
                    ParentId = container.Id,
                    ExitId = container.Exits[i].Id,
                    Position = ExitOrigin + ExitSpacing * i,
                };
                asset.AddNode(exitNode);
                if (i == 0)
                {
                    asset.AddEdge(new EdgeData(entry.Id, NodeView.OutputPortName, exitNode.Id, NodeView.InputPortName));
                }
            }

            return entry;
        }

        /// <summary>
        /// 新しい Exit ノードに、親のコンテナの最初の出口を指させる（出口を選ぶ前でも検証の Error にならないように）。
        /// 親がコンテナでない・出口が無ければ何もしない。
        /// </summary>
        public static void PointAtFirstExit(NodeGraphAsset asset, ContainerExitNode exitNode)
        {
            if (asset.FindNode(exitNode.ParentId) is ContainerNode parent && parent.Exits.Count > 0)
            {
                exitNode.ExitId = parent.Exits[0].Id;
            }
        }
    }
}
