namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>開始点ノード（<see cref="EntryNode"/>）の View。出力ポートのみ持つ。</summary>
    [CustomNodeView(typeof(EntryNode))]
    public sealed class EntryNodeView : NodeView
    {
        protected override void CreatePorts()
        {
            AddOutputPort(OutputPortName);
        }
    }
}
