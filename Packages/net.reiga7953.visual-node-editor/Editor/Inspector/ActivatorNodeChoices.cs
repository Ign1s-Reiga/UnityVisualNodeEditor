using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Views;

namespace Reiga.VisualNodeEditor.Editor.Inspector
{
    /// <summary>
    /// Graph Node Activator で選べるノード: Runner が「いる」ことのあるノード（待機ノード）とコンテナ。
    /// 通過ノード（Entry・Event・コンテナの Entry / Exit）と Note は出さない。表示はコンテナの場所付き（例: <c>Round › Playing</c>）。
    /// </summary>
    public static class ActivatorNodeChoices
    {
        /// <summary>場所の区切り（Now running パネルと同じ）。</summary>
        public const string PathSeparator = " › ";

        /// <summary>選べるか。</summary>
        public static bool IsChoosable(NodeData node) =>
            node != null && !(node is EntryNode) && !(node is EventNode) && !(node is ContainerEntryNode) && !(node is ContainerExitNode) && !(node is NoteNode);

        /// <summary>
        /// 選べるノードと、その表示（場所付き）。表示の順（同じならグラフの中の順）。
        /// 同じ表示のノードには番号を付ける（例: <c>Round › State</c>, <c>Round › State (2)</c>。ドロップダウンで別のノードとして選べるように）。
        /// </summary>
        public static List<KeyValuePair<string, string>> Get(NodeGraphAsset graph)
        {
            if (graph == null)
            {
                return new List<KeyValuePair<string, string>>();
            }

            var query = new GraphQuery(graph);
            var sorted = graph.Nodes.Where(IsChoosable)
                .Select(node => new KeyValuePair<string, string>(node.Id, GetLabel(query, node)))
                .OrderBy(choice => choice.Value, System.StringComparer.Ordinal)
                .ToList();
            var labels = DropdownLabels.MakeUnique(sorted.Select(choice => choice.Value));
            return sorted.Select((choice, i) => new KeyValuePair<string, string>(choice.Key, labels[i])).ToList();
        }

        /// <summary>ノードの表示（入っているコンテナを外側から並べ、最後にノード。例: <c>Round › Playing</c>）。</summary>
        public static string GetLabel(GraphQuery query, NodeData node)
        {
            var names = query.GetContainerPath(node).Select(NodeDisplay.GetNodeLabel).ToList();
            names.Add(NodeDisplay.GetNodeLabel(node));
            return string.Join(PathSeparator, names);
        }
    }
}
