namespace Reiga.VisualNodeEditor.Editor.Issues
{
    /// <summary>ツールバーの問題表示の状態（色分けに使う）。</summary>
    public enum IssueDisplayState
    {
        /// <summary>問題なし。</summary>
        None,

        /// <summary>警告のみ。</summary>
        Warning,

        /// <summary>エラーが 1 件以上。</summary>
        Error,
    }
}
