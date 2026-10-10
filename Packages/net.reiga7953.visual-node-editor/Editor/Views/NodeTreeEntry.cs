using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>ノードツリーの 1 行（ノード 1 つ）。コンテナなら中のノードを子に持つ。</summary>
    public sealed class NodeTreeEntry
    {
        /// <summary>ノード 1 つ分の行。<paramref name="children"/> はコンテナの中のノード（コンテナでなければ空）。</summary>
        public NodeTreeEntry(string nodeId, string label, string typeName, string category, bool isContainer, IReadOnlyList<NodeTreeEntry> children)
        {
            NodeId = nodeId;
            Label = label;
            TypeName = typeName;
            Category = category;
            IsContainer = isContainer;
            Children = children;
        }

        /// <summary>ノードの ID。</summary>
        public string NodeId { get; }

        /// <summary>行に出す名前（グラフのタイトルと同じ規則。コンテナの Exit は出口の名前）。</summary>
        public string Label { get; }

        /// <summary>型の表示名（ツールチップに出す）。</summary>
        public string TypeName { get; }

        /// <summary>カテゴリ（色の USS クラスに使う。無ければ空文字）。</summary>
        public string Category { get; }

        /// <summary>コンテナか（中のノードを子に持つ）。</summary>
        public bool IsContainer { get; }

        /// <summary>コンテナの中のノード（並び順どおり）。コンテナでなければ空。</summary>
        public IReadOnlyList<NodeTreeEntry> Children { get; }
    }
}
