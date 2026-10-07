using System;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>ツールバーのノード検索の判定（一致・次の候補・件数の表示）。描画に依存しない純粋な関数。</summary>
    public static class NodeSearch
    {
        /// <summary>
        /// タイトルか型の表示名に <paramref name="query"/> が含まれるか（大文字小文字は区別しない、前後の空白は無視）。
        /// 空の検索語はどれにも一致しない。
        /// </summary>
        public static bool Matches(string query, string title, string typeName)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            var trimmed = query.Trim();
            return Contains(title, trimmed) || Contains(typeName, trimmed);
        }

        /// <summary>Enter で移る次の候補。最後の次は先頭に戻る。候補が無ければ -1。</summary>
        public static int NextIndex(int currentIndex, int matchCount)
        {
            if (matchCount <= 0)
            {
                return -1;
            }

            return currentIndex < 0 || currentIndex >= matchCount - 1 ? 0 : currentIndex + 1;
        }

        /// <summary>件数の表示。検索語が空なら空文字、一致なしなら "No matches"、移動中なら "2 / 5"。</summary>
        public static string GetCountText(string query, int matchCount, int currentIndex)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return string.Empty;
            }

            if (matchCount == 0)
            {
                return "No matches";
            }

            return currentIndex >= 0 && currentIndex < matchCount
                ? $"{currentIndex + 1} / {matchCount}"
                : matchCount == 1 ? "1 match" : $"{matchCount} matches";
        }

        private static bool Contains(string text, string query) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
