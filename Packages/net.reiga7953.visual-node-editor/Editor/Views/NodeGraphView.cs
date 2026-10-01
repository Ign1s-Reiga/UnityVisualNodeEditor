using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>GraphView 本体。アセットとビューの同期を担当する。</summary>
    public sealed class NodeGraphView : GraphView
    {
        private NodeGraphAsset _asset;

        public NodeGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
        }

        /// <summary>アセットの内容でビューを再構築する。</summary>
        public void Populate(NodeGraphAsset asset)
        {
            _asset = asset;
            DeleteElements(graphElements);

            // TODO: asset.Nodes から NodeView を生成し、asset.Edges を Edge として接続する
        }
    }
}
