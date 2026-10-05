namespace Reiga.VisualNodeEditor
{
    /// <summary><see cref="GraphIssue"/> の重要度。</summary>
    public enum GraphIssueSeverity
    {
        /// <summary>動作はするが意図と違う可能性がある。</summary>
        Warning,

        /// <summary>グラフとして不正。</summary>
        Error,
    }
}
