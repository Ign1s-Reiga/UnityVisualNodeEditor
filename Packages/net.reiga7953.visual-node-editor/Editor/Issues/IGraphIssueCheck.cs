using System.Collections.Generic;

namespace Reiga.VisualNodeEditor.Editor.Issues
{
    /// <summary>
    /// エディタでしか分からない検証（AssetDatabase を見るものなど）を足す拡張点。
    /// 実装したクラス（抽象でなく、引数なしのコンストラクタを持つもの）は <see cref="GraphIssues.Collect"/> が自動で見つけて呼ぶ。
    /// 例: Yarn Spinner 連携の「どの Yarn プロジェクトにも無いノード名」。
    /// </summary>
    public interface IGraphIssueCheck
    {
        /// <summary><paramref name="graph"/> の問題（無ければ空）。</summary>
        IEnumerable<GraphIssue> Check(NodeGraphAsset graph);
    }
}
