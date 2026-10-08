using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reiga.VisualNodeEditor.Editor.Views;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// コンテナの出口を削除したときに影響を受けるもの: 出口の出力ポートから出るエッジ（一緒に削除される）と、
    /// その出口を指している Exit ノード（残るが、付け替えるまで検証で Error になる）。
    /// </summary>
    public sealed class ContainerExitRemoval
    {
        private ContainerExitRemoval(ContainerNode container, ContainerExit exit, List<EdgeData> edges, List<ContainerExitNode> exitNodes)
        {
            Container = container;
            Exit = exit;
            Edges = edges;
            ExitNodes = exitNodes;
        }

        /// <summary>出口を持つコンテナ。</summary>
        public ContainerNode Container { get; }

        /// <summary>削除する出口。</summary>
        public ContainerExit Exit { get; }

        /// <summary>出口の出力ポートから出るエッジ（出口と一緒に削除される）。</summary>
        public IReadOnlyList<EdgeData> Edges { get; }

        /// <summary>出口を指している Exit ノード（削除されず、付け替えるまで Error になる）。</summary>
        public IReadOnlyList<ContainerExitNode> ExitNodes { get; }

        /// <summary>何にも影響しない（確認せずに削除してよい）か。</summary>
        public bool IsEmpty => Edges.Count == 0 && ExitNodes.Count == 0;

        /// <summary>出口を削除したときの影響を調べる。コンテナに出口が無ければ null。</summary>
        public static ContainerExitRemoval Inspect(NodeGraphAsset asset, ContainerNode container, string exitId)
        {
            var exit = container?.FindExit(exitId);
            if (asset == null || exit == null)
            {
                return null;
            }

            var edges = asset.Edges.Where(e => e.FromNodeId == container.Id && e.FromPort == exitId).ToList();
            var exitNodes = asset.GetChildren(container.Id).OfType<ContainerExitNode>().Where(n => n.ExitId == exitId).ToList();
            return new ContainerExitRemoval(container, exit, edges, exitNodes);
        }

        /// <summary>確認ダイアログの本文。エッジの行き先と Exit ノードを一覧する。</summary>
        public string Describe(NodeGraphAsset asset)
        {
            var builder = new StringBuilder();
            builder.Append($"Remove the exit '{Exit.Name}' from '{TitleOf(Container)}'?");
            if (Edges.Count > 0)
            {
                builder.Append("\n\nThese connections will be removed:");
                foreach (var edge in Edges)
                {
                    builder.Append($"\n  • {Exit.Name} → {TitleOf(asset != null ? asset.FindNode(edge.ToNodeId) : null)}");
                }
            }

            if (ExitNodes.Count > 0)
            {
                builder.Append(ExitNodes.Count == 1
                    ? "\n\n1 Exit node inside the container uses this exit. It stays, and shows an error until you pick another exit."
                    : $"\n\n{ExitNodes.Count} Exit nodes inside the container use this exit. They stay, and show an error until you pick another exit.");
            }

            builder.Append("\n\nYou can undo this.");
            return builder.ToString();
        }

        private static string TitleOf(NodeData node) =>
            node == null ? "(missing node)" : NodeDisplay.ResolveTitle(node.Title, NodeDisplay.GetTypeDisplayName(node.GetType()));
    }
}
