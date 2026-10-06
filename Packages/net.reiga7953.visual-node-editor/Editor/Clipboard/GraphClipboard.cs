using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Editor.Clipboard
{
    /// <summary>
    /// コピー・貼り付け用の文字列を作り、読み戻す。GraphView の描画には依存しない純粋なロジック。
    /// 中身は一時的な <see cref="NodeGraphAsset"/> を Unity のシリアライザで JSON にしたもの
    /// （<see cref="SerializeReference"/> のノードも型ごと保存される）。
    /// </summary>
    public static class GraphClipboard
    {
        /// <summary>
        /// クリップボード文字列の先頭に付ける識別子。これで始まらない文字列は貼り付けない。
        /// OS のクリップボードで改行コードが変わっても一致するよう、改行を含めない。
        /// </summary>
        public const string Header = "Reiga.VisualNodeEditor/clipboard/v1:";

        /// <summary>
        /// 指定した要素をクリップボード文字列にする。グループを含めるとその所属ノードも含める。
        /// エッジは両端のノードがどちらも含まれるものだけを含める。何も含まれなければ空文字を返す。
        /// </summary>
        public static string Serialize(
            NodeGraphAsset source,
            IEnumerable<string> nodeIds,
            IEnumerable<string> groupIds,
            IEnumerable<string> stickyNoteIds)
        {
            if (source == null)
            {
                return string.Empty;
            }

            var groups = (groupIds ?? Enumerable.Empty<string>())
                .Select(source.FindGroup).Where(g => g != null).Distinct().ToList();
            var nodeIdSet = new HashSet<string>(nodeIds ?? Enumerable.Empty<string>());
            nodeIdSet.UnionWith(groups.SelectMany(g => g.NodeIds));
            var nodes = source.Nodes.Where(n => n != null && nodeIdSet.Contains(n.Id)).ToList();
            var stickyNotes = (stickyNoteIds ?? Enumerable.Empty<string>())
                .Select(source.FindStickyNote).Where(s => s != null).Distinct().ToList();
            if (nodes.Count == 0 && groups.Count == 0 && stickyNotes.Count == 0)
            {
                return string.Empty;
            }

            var copiedIds = new HashSet<string>(nodes.Select(n => n.Id));
            var edges = source.Edges.Where(e => copiedIds.Contains(e.FromNodeId) && copiedIds.Contains(e.ToNodeId));

            // 元のインスタンスを一時アセットに入れて JSON にするだけなので、元のグラフには影響しない
            var container = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                nodes.ForEach(container.AddNode);
                foreach (var edge in edges)
                {
                    container.AddEdge(edge);
                }

                groups.ForEach(container.AddGroup);
                stickyNotes.ForEach(container.AddStickyNote);
                return Header + EditorJsonUtility.ToJson(container);
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        /// <summary>このツールのクリップボード文字列かどうか。</summary>
        public static bool CanPaste(string data) =>
            !string.IsNullOrEmpty(data) && data.StartsWith(Header, StringComparison.Ordinal);

        /// <summary>
        /// クリップボード文字列から、新しい ID を振り直した要素一式を作る。位置は <paramref name="offset"/> だけずらす。
        /// このツールの文字列でなければ null を返す。
        /// </summary>
        public static GraphClipboardContent Deserialize(string data, Vector2 offset)
        {
            if (!CanPaste(data))
            {
                return null;
            }

            var container = ScriptableObject.CreateInstance<NodeGraphAsset>();
            try
            {
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(data.Substring(Header.Length), container);
                }
                catch (ArgumentException)
                {
                    return null;
                }

                return Remap(container, offset);
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        private static GraphClipboardContent Remap(NodeGraphAsset copied, Vector2 offset)
        {
            var newIds = new Dictionary<string, string>();
            var nodes = new List<NodeData>();
            foreach (var node in copied.Nodes)
            {
                // 型が削除・改名されて読めなかったノードは貼り付けない
                if (node == null)
                {
                    continue;
                }

                var oldId = node.Id;
                node.AssignNewId();
                node.Position += offset;
                newIds[oldId] = node.Id;
                nodes.Add(node);
            }

            var edges = copied.Edges
                .Where(e => newIds.ContainsKey(e.FromNodeId) && newIds.ContainsKey(e.ToNodeId))
                .Select(e => new EdgeData(newIds[e.FromNodeId], e.FromPort, newIds[e.ToNodeId], e.ToPort))
                .ToList();

            var groups = new List<GroupData>();
            foreach (var group in copied.Groups)
            {
                group.AssignNewId();
                group.Position += offset;
                group.ReplaceNodeIds(group.NodeIds.Where(newIds.ContainsKey).Select(id => newIds[id]).ToList());
                groups.Add(group);
            }

            var stickyNotes = new List<StickyNoteData>();
            foreach (var stickyNote in copied.StickyNotes)
            {
                stickyNote.AssignNewId();
                stickyNote.Rect = new Rect(stickyNote.Rect.position + offset, stickyNote.Rect.size);
                stickyNotes.Add(stickyNote);
            }

            return new GraphClipboardContent(nodes, edges, groups, stickyNotes);
        }
    }
}
