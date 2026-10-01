namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>イベントノード（<see cref="EventNode"/>）の View。入力・出力を 1 つずつ持つ。</summary>
    [CustomNodeView(typeof(EventNode))]
    public sealed class EventNodeView : NodeView
    {
        protected override void CreatePorts()
        {
            AddInputPort(InputPortName);
            AddOutputPort(OutputPortName);
        }
    }
}
