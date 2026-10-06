using System;
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
    }
}
