using System;

namespace Reiga.VisualNodeEditor.Editor.Search
{
    /// <summary>ノード追加メニューの 1 項目（<see cref="NodeMenuAttribute"/> のパスとノード型の組）。</summary>
    public readonly struct NodeMenuItem
    {
        public NodeMenuItem(string path, Type nodeType)
        {
            Path = path;
            NodeType = nodeType;
        }

        /// <summary>"/" 区切りのメニューパス。例: "Flow/Scene"</summary>
        public string Path { get; }

        /// <summary>生成する <see cref="NodeData"/> の型。</summary>
        public Type NodeType { get; }
    }
}
