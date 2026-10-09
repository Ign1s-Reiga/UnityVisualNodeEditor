namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// イベントノード（<see cref="EventNode"/>）の View。入力・出力を 1 つずつ持つ。
    /// 遷移として読めるよう、タイトルを付けていなければイベント名をタイトルにし（タイトルを付けたらイベント名はサマリーに出す）、
    /// ステートの箱と見分けられる札の形にする（USS クラス <see cref="TransitionClassName"/>）。
    /// </summary>
    [CustomNodeView(typeof(EventNode))]
    public sealed class EventNodeView : NodeView
    {
        /// <summary>遷移の札の形にする USS クラス。</summary>
        public const string TransitionClassName = "vne-node--transition";

        public EventNodeView()
        {
            AddToClassList(TransitionClassName);
        }

        protected override void CreatePorts()
        {
            AddInputPort(InputPortName);
            AddOutputPort(OutputPortName);
        }

        protected override string GetDisplayTitle() => NodeDisplay.GetNodeLabel(Data);

        protected override string GetSummary() => Data.HasCustomTitle ? (Data as EventNode)?.EventName : string.Empty;
    }
}
