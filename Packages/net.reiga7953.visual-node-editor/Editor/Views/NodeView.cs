using UnityEditor.Experimental.GraphView;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// <see cref="NodeData"/> 1 件に対応する GraphView ノード。
    /// ノード種別ごとの UI はサブクラスで拡張する。
    /// </summary>
    public class NodeView : Node
    {
        public NodeView(NodeData data)
        {
            Data = data;
            title = data.Title;
            viewDataKey = data.Id;
            SetPosition(new UnityEngine.Rect(data.Position, UnityEngine.Vector2.zero));
        }

        public NodeData Data { get; }
    }
}
