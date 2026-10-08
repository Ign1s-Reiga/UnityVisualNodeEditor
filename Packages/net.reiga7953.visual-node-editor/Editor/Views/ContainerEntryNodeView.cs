using UnityEditor.Experimental.GraphView;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// コンテナの中の開始点（<see cref="ContainerEntryNode"/>）の View。出力ポートのみ持つ。
    /// コンテナごとにちょうど 1 つなので、単独では削除・コピー（複製・切り取り）できない。
    /// </summary>
    [CustomNodeView(typeof(ContainerEntryNode))]
    public sealed class ContainerEntryNodeView : NodeView
    {
        public ContainerEntryNodeView()
        {
            capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable);
        }

        protected override void CreatePorts()
        {
            AddOutputPort(OutputPortName);
        }
    }
}
