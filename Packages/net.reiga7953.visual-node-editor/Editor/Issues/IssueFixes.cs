namespace Reiga.VisualNodeEditor.Editor.Issues
{
    /// <summary>問題一覧の項目に出す「直す」ボタンの文言（問題の種類ごと。直し方の無い問題は null）。</summary>
    public static class IssueFixes
    {
        /// <summary>Entry が無いとき。</summary>
        public const string AddEntryLabel = "Add Entry";

        /// <summary>Scene ノードのシーンが無い・削除されたとき。</summary>
        public const string PickSceneLabel = "Pick Scene…";

        /// <summary>シーンが Build Settings で有効でないとき。</summary>
        public const string AddToBuildSettingsLabel = "Add to Build Settings";

        /// <summary>Graph Runner が無いとき。</summary>
        public const string AddGraphRunnerLabel = "Add Graph Runner…";

        /// <summary><paramref name="issue"/> を直すボタンの文言。直し方が無ければ null。</summary>
        public static string GetLabel(GraphIssue issue)
        {
            switch (issue?.Kind)
            {
                case GraphIssueKind.MissingEntry:
                    return AddEntryLabel;
                case GraphIssueKind.SceneNotSet:
                case GraphIssueKind.SceneMissing:
                    return issue.NodeId != null ? PickSceneLabel : null;
                case GraphIssueKind.SceneNotInBuildSettings:
                    return AddToBuildSettingsLabel;
                case GraphIssueKind.NoGraphRunner:
                    return AddGraphRunnerLabel;
                default:
                    return null;
            }
        }
    }
}
