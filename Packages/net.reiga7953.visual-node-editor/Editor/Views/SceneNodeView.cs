namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>シーンノード（<see cref="SceneNode"/>）の View。入力・出力を 1 つずつ持つ。</summary>
    [CustomNodeView(typeof(SceneNode))]
    public sealed class SceneNodeView : NodeView
    {
        protected override void CreatePorts()
        {
            AddInputPort(InputPortName);
            AddOutputPort(OutputPortName);
        }
    }
}
