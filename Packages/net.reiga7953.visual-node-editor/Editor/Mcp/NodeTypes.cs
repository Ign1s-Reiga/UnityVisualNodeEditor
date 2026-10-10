using System;
using System.Collections.Generic;
using System.Linq;
using Reiga.VisualNodeEditor.Editor.Search;
using Reiga.VisualNodeEditor.Editor.Views;

namespace Reiga.VisualNodeEditor.Editor.Mcp
{
    /// <summary>
    /// <c>add_node</c> の <c>type</c> をノードの型にする。Create Node メニューに出る型だけを、
    /// 表示名（<c>Scene</c>）・メニューのパス（<c>Flow/Scene</c>）・クラス名（<c>SceneNode</c>、名前空間付きも可）のどれでも受け付ける（大文字小文字は区別しない）。
    /// </summary>
    public static class NodeTypes
    {
        /// <summary>作れるノードの型の、表示名とメニューのパス（説明・エラーの文用）。</summary>
        public static List<string> Describe() =>
            NodeMenuCatalog.GetItems().Select(item => $"{NodeDisplay.GetTypeDisplayName(item.NodeType)} ({item.Path})").ToList();

        /// <summary><paramref name="name"/> の型。見つからない・いくつも当てはまるときは、選べる型を添えた <see cref="McpToolException"/>。</summary>
        public static Type Resolve(string name)
        {
            var key = (name ?? string.Empty).Trim();
            var items = NodeMenuCatalog.GetItems();
            var matches = items.Where(item => Matches(item, key)).Select(item => item.NodeType).Distinct().ToList();
            if (matches.Count == 1)
            {
                return matches[0];
            }

            var choices = string.Join(", ", Describe());
            throw new McpToolException(matches.Count == 0
                ? $"Unknown node type '{name}'. Use one of: {choices}."
                : $"Node type '{name}' is ambiguous ({string.Join(", ", matches.Select(t => t.FullName))}). Use the menu path or the full class name.");
        }

        private static bool Matches(NodeMenuItem item, string key)
        {
            var type = item.NodeType;
            return Equal(item.Path, key)
                   || Equal(NodeDisplay.GetTypeDisplayName(type), key)
                   || Equal(type.Name, key)
                   || Equal(type.FullName, key);
        }

        private static bool Equal(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
