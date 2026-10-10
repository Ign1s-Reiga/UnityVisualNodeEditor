using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// ノードツリー（左のペイン）に出す階層を作る。ルートのノードを並べ、コンテナの中のノードをその子にする。
    /// 各階層の並びは、Entry → 位置の順（左から右、同じ列なら上から下。流れはふつう左から右へ組むため）→ コンテナの Exit。
    /// </summary>
    public static class NodeTree
    {
        private static readonly IReadOnlyList<NodeTreeEntry> NoChildren = new NodeTreeEntry[0];

        /// <summary><paramref name="asset"/> のノードの階層。親の参照が壊れたノード（どのコンテナにも入っていない）は出さない。</summary>
        public static List<NodeTreeEntry> Build(NodeGraphAsset asset)
        {
            if (asset == null)
            {
                return new List<NodeTreeEntry>();
            }

            var byParent = asset.Nodes.Where(n => n != null).ToLookup(n => n.ParentId ?? string.Empty);
            return BuildLevel(asset, string.Empty, byParent, new HashSet<string>());
        }

        private static List<NodeTreeEntry> BuildLevel(
            NodeGraphAsset asset, string parentId, ILookup<string, NodeData> byParent, HashSet<string> visited)
        {
            // ID が重なっていても、同じノードを 2 回出したり、親の輪で止まらなくなったりしないように
            var nodes = byParent[parentId]
                .Where(n => visited.Add(n.Id))
                .OrderBy(GetOrderGroup)
                .ThenBy(n => n.Position.x)
                .ThenBy(n => n.Position.y)
                .ToList();

            return nodes
                .Select(n => new NodeTreeEntry(
                    n.Id,
                    GetLabel(asset, n),
                    NodeDisplay.GetTypeDisplayName(n.GetType()),
                    NodeCategory.FromType(n.GetType()),
                    n is ContainerNode,
                    n is ContainerNode ? BuildLevel(asset, n.Id, byParent, visited) : NoChildren))
                .ToList();
        }

        // 0 = Entry（階層の始まり）、1 = それ以外、2 = コンテナの Exit（階層の終わり）
        private static int GetOrderGroup(NodeData node)
        {
            switch (node)
            {
                case EntryNode _:
                case ContainerEntryNode _:
                    return 0;
                case ContainerExitNode _:
                    return 2;
                default:
                    return 1;
            }
        }

        // グラフのタイトルと同じ規則。コンテナの Exit は（タイトルを付けていなければ）指している出口の名前
        private static string GetLabel(NodeGraphAsset asset, NodeData node)
        {
            var exitName = node is ContainerExitNode exitNode && !exitNode.HasCustomTitle
                ? ContainerExitNodeView.GetExitName(asset, exitNode)
                : null;
            return string.IsNullOrEmpty(exitName) ? NodeDisplay.GetNodeLabel(node) : exitName;
        }
    }
}
