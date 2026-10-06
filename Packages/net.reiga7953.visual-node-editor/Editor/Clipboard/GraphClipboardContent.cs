using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Editor.Clipboard
{
    /// <summary>
    /// 貼り付ける要素一式。すべて新しい ID が振られ、エッジ・グループは新しいノード ID を指す。
    /// アセットへそのまま追加できる。
    /// </summary>
    public sealed class GraphClipboardContent
    {
        public GraphClipboardContent(
            List<NodeData> nodes, List<EdgeData> edges, List<GroupData> groups, List<StickyNoteData> stickyNotes)
        {
            Nodes = nodes;
            Edges = edges;
            Groups = groups;
            StickyNotes = stickyNotes;
        }

        public IReadOnlyList<NodeData> Nodes { get; }

        public IReadOnlyList<EdgeData> Edges { get; }

        public IReadOnlyList<GroupData> Groups { get; }

        public IReadOnlyList<StickyNoteData> StickyNotes { get; }

        /// <summary>貼り付けるものが何も無いか。</summary>
        public bool IsEmpty => Nodes.Count == 0 && Groups.Count == 0 && StickyNotes.Count == 0;
    }
}
