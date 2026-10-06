using System;
using System.Reflection;
using System.Text;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// ノードのカテゴリ（<see cref="NodeMenuAttribute"/> パスの先頭セグメント）を扱う。
    /// カテゴリは USS クラス名に使うため、小文字の英数字とハイフンに正規化する。
    /// </summary>
    public static class NodeCategory
    {
        /// <summary>
        /// メニューパスからカテゴリ名を取り出す。例: "Flow/Entry" → "flow"。
        /// 階層の無いパス（"Entry"）や空文字ではカテゴリ無しとして空文字を返す。
        /// </summary>
        public static string FromMenuPath(string menuPath)
        {
            if (string.IsNullOrWhiteSpace(menuPath))
            {
                return string.Empty;
            }

            var segments = menuPath.Trim().Trim('/').Split('/');
            return segments.Length < 2 ? string.Empty : Normalize(segments[0]);
        }

        /// <summary>ノード型に付いた <see cref="NodeMenuAttribute"/> からカテゴリ名を取り出す。属性が無ければ空文字。</summary>
        public static string FromType(Type nodeType) =>
            FromMenuPath(nodeType?.GetCustomAttribute<NodeMenuAttribute>()?.Path);

        private static string Normalize(string segment)
        {
            var builder = new StringBuilder(segment.Length);
            foreach (var c in segment.Trim().ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    builder.Append(c);
                }
                else if (builder.Length > 0 && builder[builder.Length - 1] != '-')
                {
                    builder.Append('-');
                }
            }

            return builder.ToString().TrimEnd('-');
        }
    }
}
