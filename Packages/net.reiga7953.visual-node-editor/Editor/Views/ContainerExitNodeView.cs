namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// コンテナの中の出口（<see cref="ContainerExitNode"/>）の View。入力ポートのみ持つ。
    /// タイトルは指している出口の名前（出口を改名すると追従する）。ユーザーがタイトルを付けたら、出口の名前はサマリーに出す。
    /// </summary>
    [CustomNodeView(typeof(ContainerExitNode))]
    public sealed class ContainerExitNodeView : NodeView
    {
        /// <summary>出口が見つからない（親がコンテナでない・出口が削除された）ときのサマリー。</summary>
        public const string MissingExitSummary = "Missing exit";

        /// <summary>Exit ノードが指す出口の名前。親のコンテナか出口が見つからなければ null。</summary>
        public static string GetExitName(NodeGraphAsset graph, ContainerExitNode node) =>
            node != null && graph != null && graph.FindNode(node.ParentId) is ContainerNode parent
                ? parent.FindExit(node.ExitId)?.Name
                : null;

        protected override void CreatePorts()
        {
            AddInputPort(InputPortName);
        }

        protected override string GetDisplayTitle() =>
            Data.HasCustomTitle
                ? Data.Title.Trim()
                : GetExitName(Graph, Data as ContainerExitNode) ?? base.GetDisplayTitle();

        protected override string GetSummary()
        {
            var exitName = GetExitName(Graph, Data as ContainerExitNode);
            if (exitName == null)
            {
                return Graph != null ? MissingExitSummary : string.Empty;
            }

            return Data.HasCustomTitle ? "Exit: " + exitName : string.Empty;
        }
    }
}
