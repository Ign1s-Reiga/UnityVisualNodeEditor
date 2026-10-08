using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>表示中の階層のノードから、キャンバスに出す案内（<see cref="EmptyStateKind"/>）を決める。</summary>
    public static class EmptyStateHint
    {
        /// <summary>
        /// <paramref name="nodesOnLevel"/>（表示中の階層のノード）に対する案内。
        /// ルートでは Entry 以外のノードが、コンテナの中では Entry / Exit 以外のノードが 1 つでもあれば案内を出さない。
        /// </summary>
        public static EmptyStateKind For(IEnumerable<NodeData> nodesOnLevel, bool insideContainer)
        {
            var nodes = (nodesOnLevel ?? Enumerable.Empty<NodeData>()).Where(n => n != null).ToList();
            if (insideContainer)
            {
                return nodes.All(n => n is ContainerEntryNode || n is ContainerExitNode)
                    ? EmptyStateKind.EmptyContainer
                    : EmptyStateKind.None;
            }

            if (nodes.Count == 0)
            {
                return EmptyStateKind.EmptyGraph;
            }

            return nodes.All(n => n is EntryNode) ? EmptyStateKind.OnlyEntry : EmptyStateKind.None;
        }

        /// <summary>案内の USS クラス（例: "vne-empty-hint--only-entry"）。案内を出さないなら空文字。</summary>
        public static string GetClassName(EmptyStateKind kind) => kind switch
        {
            EmptyStateKind.EmptyGraph => "vne-empty-hint--empty-graph",
            EmptyStateKind.OnlyEntry => "vne-empty-hint--only-entry",
            EmptyStateKind.EmptyContainer => "vne-empty-hint--empty-container",
            _ => string.Empty,
        };
    }
}
