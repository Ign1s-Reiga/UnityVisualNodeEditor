using System.Collections.Generic;
using Reiga.VisualNodeEditor.Editor.Issues;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// テスト用の <see cref="IGraphIssueCheck"/>。<see cref="GraphIssues.Collect"/> が自動で見つけることを確かめる。
    /// 他のテストの問題の数を変えないよう、名前が <see cref="GraphName"/> のグラフにだけ問題を返す。
    /// </summary>
    public sealed class ProbeIssueCheck : IGraphIssueCheck
    {
        public const string GraphName = "IssueCheckProbe";

        public IEnumerable<GraphIssue> Check(NodeGraphAsset graph)
        {
            if (graph.name == GraphName)
            {
                yield return new GraphIssue(GraphIssueSeverity.Warning, "Found by the probe check.");
            }
        }
    }
}
