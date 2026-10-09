using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>ノードの表示文字列（タイトル・型の表示名・フィールドラベル）を決める。</summary>
    public static class NodeDisplay
    {
        private const string NodeSuffix = "Node";

        /// <summary>
        /// 型の表示名。<see cref="NodeMenuAttribute"/> パスの末尾、無ければ型名から "Node" を除いて整形したもの。
        /// 例: <c>[NodeMenu("Flow/Entry")] EntryNode</c> → "Entry"、属性の無い <c>BossBattleNode</c> → "Boss Battle"。
        /// </summary>
        public static string GetTypeDisplayName(Type nodeType)
        {
            if (nodeType == null)
            {
                return string.Empty;
            }

            var menuPath = nodeType.GetCustomAttribute<NodeMenuAttribute>()?.Path?.Trim().Trim('/');
            if (!string.IsNullOrEmpty(menuPath))
            {
                var leaf = menuPath.Substring(menuPath.LastIndexOf('/') + 1).Trim();
                if (leaf.Length > 0)
                {
                    return leaf;
                }
            }

            var name = nodeType.Name;
            if (name.Length > NodeSuffix.Length && name.EndsWith(NodeSuffix, StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - NodeSuffix.Length);
            }

            return ObjectNames.NicifyVariableName(name);
        }

        /// <summary>
        /// 文章の中でノードを呼ぶ名前（グラフのタイトルと同じ規則）: ユーザーのタイトル、無ければ Scene ノードはシーン名、それ以外は型の表示名。
        /// </summary>
        public static string GetNodeLabel(NodeData node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            if (node.HasCustomTitle)
            {
                return node.Title.Trim();
            }

            return node is SceneNode scene && !string.IsNullOrEmpty(scene.SceneName)
                ? scene.SceneName
                : GetTypeDisplayName(node.GetType());
        }

        /// <summary>表示するタイトル。ユーザーの入力が空（空白のみを含む）なら型の表示名を使う。</summary>
        public static string ResolveTitle(string title, string typeDisplayName) =>
            string.IsNullOrWhiteSpace(title) ? typeDisplayName : title.Trim();

        /// <summary>複数行の文字列の、空でない最初の行（前後の空白を除く）。null や空なら空文字。</summary>
        public static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    return trimmed;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// インスペクタのフィールドラベル。シリアライズ名から英語で生成する（例: "_eventName" → "Event Name"）。
        /// エディタの言語設定で displayName が一部だけ翻訳されるのを避けるため、displayName は使わない。
        /// </summary>
        public static string GetFieldLabel(string propertyName) => ObjectNames.NicifyVariableName(propertyName);

        /// <summary>振る舞いの表示名（クラス名を単語に区切る。例: "PlayerMovement" → "Player Movement"）。</summary>
        public static string GetBehaviourName(Type behaviourType) =>
            behaviourType == null ? string.Empty : ObjectNames.NicifyVariableName(behaviourType.Name);

        /// <summary>
        /// ノードに出す振る舞いの一覧（例: "Player Movement, Timer"）。読めなかったものは "Missing"。振る舞いが無ければ空文字。
        /// </summary>
        public static string GetBehavioursText(IReadOnlyList<NodeBehaviour> behaviours)
        {
            if (behaviours == null || behaviours.Count == 0)
            {
                return string.Empty;
            }

            var names = new string[behaviours.Count];
            for (var i = 0; i < behaviours.Count; i++)
            {
                names[i] = behaviours[i] == null ? MissingBehaviourName : GetBehaviourName(behaviours[i].GetType());
            }

            return string.Join(", ", names);
        }

        /// <summary>ノード上の一覧で、読めなかった振る舞いに出す名前。</summary>
        public const string MissingBehaviourName = "Missing";
    }
}
