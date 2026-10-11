using Reiga.VisualNodeEditor.Editor.Views;

namespace Reiga.VisualNodeEditor.YarnSpinner.Editor
{
    /// <summary>
    /// Dialogue ノード（<see cref="DialogueNode"/>）の View。入力・出力を 1 つずつ持つ。
    /// タイトルを付けていなければ Yarn のノード名をタイトルにし（付けていれば Yarn のノード名はサマリーに出す）、
    /// 完了イベントが既定の Done でなければサマリーに出す。
    /// </summary>
    [CustomNodeView(typeof(DialogueNode))]
    public sealed class DialogueNodeView : NodeView
    {
        protected override void CreatePorts()
        {
            AddInputPort(InputPortName);
            AddOutputPort(OutputPortName);
        }

        protected override string GetDisplayTitle() => NodeDisplay.GetNodeLabel(Data);

        protected override string GetSummary() => GetSummary(Data as DialogueNode);

        /// <summary>サマリー行（例: <c>Yarn: Chapter1 · then Finished</c>）。出すものが無ければ空文字。</summary>
        internal static string GetSummary(DialogueNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            var yarn = node.HasCustomTitle && !string.IsNullOrWhiteSpace(node.YarnNode) ? "Yarn: " + node.YarnNode.Trim() : string.Empty;
            var then = node.CompleteEvent != DialogueNode.DefaultCompleteEvent && !string.IsNullOrEmpty(node.CompleteEvent)
                ? "then " + node.CompleteEvent
                : string.Empty;
            return yarn.Length > 0 && then.Length > 0 ? $"{yarn} · {then}" : yarn + then;
        }
    }
}
