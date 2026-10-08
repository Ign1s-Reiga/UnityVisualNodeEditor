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
        /// コンテナを含めると、その子孫と、中のグループ・付箋も含める。コンテナの Entry は、そのコンテナごとでなければ含めない。
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
            foreach (var selected in source.Nodes.OfType<ContainerNode>().Where(c => nodeIdSet.Contains(c.Id)).ToList())
            {
                nodeIdSet.UnionWith(source.GetDescendants(selected.Id).Select(n => n.Id));
            }

            // Entry は単独では複製しない（コンテナごとに 1 つだけ。検証の条件 2・9）
            nodeIdSet.RemoveWhere(id => source.FindNode(id) is ContainerEntryNode entry && !nodeIdSet.Contains(entry.ParentId));

            var nodes = source.Nodes.Where(n => n != null && nodeIdSet.Contains(n.Id)).ToList();
            var containerIds = new HashSet<string>(nodes.OfType<ContainerNode>().Select(c => c.Id));
            groups.AddRange(source.Groups.Where(g => containerIds.Contains(g.ParentId) && !groups.Contains(g)));
            var stickyNotes = (stickyNoteIds ?? Enumerable.Empty<string>())
                .Select(source.FindStickyNote).Where(s => s != null)
                .Concat(source.StickyNotes.Where(s => containerIds.Contains(s.ParentId)))
                .Distinct().ToList();
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
        /// クリップボード文字列から、新しい ID を振り直した要素一式を作る。このツールの文字列でなければ null を返す。
        /// 一緒にコピーしたコンテナの中にあったものは、新しいコンテナの中に入れる。
        /// それ以外は <paramref name="targetParentId"/> の階層（空文字ならルート）に置き、位置を <paramref name="offset"/> だけずらす。
        /// </summary>
        public static GraphClipboardContent Deserialize(string data, Vector2 offset, string targetParentId = "")
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

                return Remap(container, offset, targetParentId ?? string.Empty);
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        private static GraphClipboardContent Remap(NodeGraphAsset copied, Vector2 offset, string targetParentId)
        {
            // 型が削除・改名されて読めなかったノードは貼り付けない
            var copiedNodes = copied.Nodes.Where(n => n != null).ToList();
            var newIds = new Dictionary<string, string>();
            foreach (var node in copiedNodes)
            {
                var oldId = node.Id;
                node.AssignNewId();
                newIds[oldId] = node.Id;
            }

            // 一緒にコピーしたコンテナの中身はその新しいコンテナへ、それ以外は貼り付け先の階層へ。
            // 中身はコンテナの中の座標なので、ずらすのは貼り付け先の階層に置くものだけ
            var nodes = new List<NodeData>();
            foreach (var node in copiedNodes)
            {
                if (node.ParentId.Length > 0 && newIds.TryGetValue(node.ParentId, out var newParentId))
                {
                    node.ParentId = newParentId;
                }
                else
                {
                    node.ParentId = targetParentId;
                    node.Position += offset;
                }

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
                if (group.ParentId.Length > 0 && newIds.TryGetValue(group.ParentId, out var newParentId))
                {
                    group.ParentId = newParentId;
                }
                else
                {
                    group.ParentId = targetParentId;
                    group.Position += offset;
                }

                group.ReplaceNodeIds(group.NodeIds.Where(newIds.ContainsKey).Select(id => newIds[id]).ToList());
                groups.Add(group);
            }

            var stickyNotes = new List<StickyNoteData>();
            foreach (var stickyNote in copied.StickyNotes)
            {
                stickyNote.AssignNewId();
                if (stickyNote.ParentId.Length > 0 && newIds.TryGetValue(stickyNote.ParentId, out var newParentId))
                {
                    stickyNote.ParentId = newParentId;
                }
                else
                {
                    stickyNote.ParentId = targetParentId;
                    stickyNote.Rect = new Rect(stickyNote.Rect.position + offset, stickyNote.Rect.size);
                }

                stickyNotes.Add(stickyNote);
            }

            return new GraphClipboardContent(nodes, edges, groups, stickyNotes);
        }
    }
}
