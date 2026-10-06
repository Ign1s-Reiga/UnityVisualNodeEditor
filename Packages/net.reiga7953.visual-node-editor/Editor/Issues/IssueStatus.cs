using System.Collections.Generic;
using System.Linq;

namespace Reiga.VisualNodeEditor.Editor.Issues
{
    /// <summary>問題件数からツールバーの表示（文言・状態）と、一覧を自動で開くかどうかを決める。</summary>
    public static class IssueStatus
    {
        /// <summary>問題が無いときの表示。</summary>
        public const string NoIssuesText = "No issues";

        /// <summary>件数から状態を決める。エラーがあれば Error、警告のみなら Warning。</summary>
        public static IssueDisplayState GetState(int errorCount, int warningCount) =>
            errorCount > 0 ? IssueDisplayState.Error
            : warningCount > 0 ? IssueDisplayState.Warning
            : IssueDisplayState.None;

        /// <summary>件数の表示文言。例: "No issues" / "1 error" / "2 errors, 1 warning" / "3 warnings"。</summary>
        public static string GetText(int errorCount, int warningCount)
        {
            var parts = new List<string>(2);
            if (errorCount > 0)
            {
                parts.Add(Count(errorCount, "error"));
            }

            if (warningCount > 0)
            {
                parts.Add(Count(warningCount, "warning"));
            }

            return parts.Count == 0 ? NoIssuesText : string.Join(", ", parts);
        }

        /// <summary>前回に無かったエラーが現れたか。一覧を自動で開くかどうかの判定に使う。</summary>
        public static bool HasNewErrors(IEnumerable<string> previousErrorKeys, IEnumerable<string> currentErrorKeys)
        {
            var previous = new HashSet<string>(previousErrorKeys);
            return currentErrorKeys.Any(key => !previous.Contains(key));
        }

        /// <summary>エラーを同一視するためのキー（ノード ID とメッセージ）。</summary>
        public static string GetKey(GraphIssue issue) => issue.NodeId + "\n" + issue.Message;

        private static string Count(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
    }
}
