using System;
using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor
{
    /// <summary>
    /// ノードの場所（タイトルを <c>/</c> で並べたもの。例: <c>Paused</c>、<c>Round/Paused</c>、コンテナなら <c>Round</c>）。
    /// UXML は人が読み書きするテキストなので、UI Toolkit の Graph Node Panel はノードを ID ではなくこれで指す。
    /// </summary>
    public static class GraphNodePath
    {
        /// <summary>場所の区切り。</summary>
        public const char Separator = '/';

        /// <summary>
        /// 場所に使うノードの名前: 付けたタイトル（前後の空白を除く）、無ければ Scene ノードはシーン名、内容から決まる名前（Dialogue ノードの Yarn のノード名など）、型の既定のタイトル。
        /// </summary>
        public static string NameOf(NodeData node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (node.HasCustomTitle)
            {
                return node.Title.Trim();
            }

            if (node is SceneNode scene && !string.IsNullOrEmpty(scene.SceneName))
            {
                return scene.SceneName;
            }

            return !string.IsNullOrWhiteSpace(node.ContentTitle) ? node.ContentTitle.Trim() : node.Title;
        }

        /// <summary>ノードの場所（入っているコンテナを外側から並べ、最後にノード。例: <c>Round/Paused</c>）。</summary>
        public static string Of(GraphQuery query, NodeData node)
        {
            var names = query.GetContainerPath(node).Select(NameOf).ToList();
            names.Add(NameOf(node));
            return string.Join(Separator.ToString(), names);
        }

        /// <summary>
        /// <paramref name="runner"/> が <paramref name="path"/> にいるか。Runner の今の場所（入っているコンテナを外側から並べ、最後に今のノード）の中に、
        /// その並びが続けて現れれば true（<c>Paused</c> は <c>Round › Paused</c> にも当てはまる。コンテナを指せば、中のどこにいても当てはまる）。
        /// Runner が無い・止まっている・場所が空なら false。
        /// </summary>
        public static bool IsAt(GraphRunner runner, string path)
        {
            var current = runner != null && runner.IsRunning ? runner.Current : null;
            var wanted = Split(path);
            if (current == null || wanted.Count == 0)
            {
                return false;
            }

            var here = runner.ContainerPath.Select(NameOf).ToList();
            here.Add(NameOf(current));
            for (var start = 0; start + wanted.Count <= here.Count; start++)
            {
                var matches = true;
                for (var i = 0; i < wanted.Count && matches; i++)
                {
                    matches = string.Equals(here[start + i], wanted[i], StringComparison.Ordinal);
                }

                if (matches)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>場所を名前の並びにする（前後の空白と、空の部分は除く）。</summary>
        public static List<string> Split(string path) =>
            (path ?? string.Empty).Split(Separator).Select(part => part.Trim()).Where(part => part.Length > 0).ToList();
    }
}
