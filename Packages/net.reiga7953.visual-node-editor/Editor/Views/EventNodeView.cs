namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// イベントノード（<see cref="EventNode"/>）の View。入力・出力を 1 つずつ持つ。
    /// 遷移として読めるよう、タイトルを付けていなければイベント名をタイトルにし（タイトルを付けたらイベント名はサマリーに出す）、
    /// ステートの箱と見分けられる札の形にする（USS クラス <see cref="TransitionClassName"/>）。
    /// 条件はサマリー行に出し（例: <c>if Affection &gt;= 5</c>）、自動の Event には印を付ける（USS クラス <see cref="AutomaticClassName"/>）。
    /// </summary>
    [CustomNodeView(typeof(EventNode))]
    public sealed class EventNodeView : NodeView
    {
        /// <summary>遷移の札の形にする USS クラス。</summary>
        public const string TransitionClassName = "vne-node--transition";

        /// <summary>条件を満たしたら自分で進む Event に付ける USS クラス。</summary>
        public const string AutomaticClassName = "vne-node--automatic";

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

        protected override string GetSummary() => NodeDisplay.GetEventSummary(Data as EventNode, Graph);

        protected override void OnRebind() => EnableInClassList(AutomaticClassName, (Data as EventNode)?.IsAutomatic == true);
    }
}
