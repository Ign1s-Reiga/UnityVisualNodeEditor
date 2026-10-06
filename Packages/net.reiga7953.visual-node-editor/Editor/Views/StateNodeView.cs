namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>ステートノード（<see cref="StateNode"/>）の View。入力・出力を 1 つずつ持ち、サマリーに説明の 1 行目を出す。</summary>
    [CustomNodeView(typeof(StateNode))]
    public sealed class StateNodeView : NodeView
    {
        protected override void CreatePorts()
        {
            AddInputPort(InputPortName);
            AddOutputPort(OutputPortName);
        }

        protected override string GetSummary() => (Data as StateNode)?.Description;
    }
}
