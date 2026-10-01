namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>ステートノード（<see cref="StateNode"/>）の View。入力・出力を 1 つずつ持つ。</summary>
    [CustomNodeView(typeof(StateNode))]
    public sealed class StateNodeView : NodeView
    {
        protected override void CreatePorts()
        {
            AddInputPort(InputPortName);
            AddOutputPort(OutputPortName);
        }
    }
}
