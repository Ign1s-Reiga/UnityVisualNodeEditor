using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// <see cref="NodeData"/> 1 件に対応する GraphView ノード。
    /// ノード種別ごとの UI はサブクラスで拡張し、<see cref="CustomNodeViewAttribute"/> で登録する。
    /// 生成は <see cref="NodeViewFactory.Create"/> を通すこと。
    /// </summary>
    public class NodeView : Node
    {
        /// <summary>標準の入力ポート名。</summary>
        public const string InputPortName = "in";

        /// <summary>標準の出力ポート名。</summary>
        public const string OutputPortName = "out";

        /// <summary>この View が表示しているノードデータ。</summary>
        public NodeData Data { get; private set; }

        internal void Initialize(NodeData data)
        {
            Data = data;
            title = data.Title;
            viewDataKey = data.Id;
            AddToClassList("vne-node");
            SetPosition(new Rect(data.Position, Vector2.zero));

            CreatePorts();
            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>
        /// 指定した向き・名前のポートを返す。存在しなければ null。
        /// </summary>
        public Port FindPort(string portName, Direction direction)
        {
            var container = direction == Direction.Input ? inputContainer : outputContainer;
            return container.Query<Port>().Where(p => p.portName == portName).First();
        }

        /// <summary>ポートを定義する。既定ではポートを持たない。</summary>
        protected virtual void CreatePorts()
        {
        }

        /// <summary>入力ポートを追加する。<paramref name="portName"/> がそのままエッジのポート名になる。</summary>
        protected Port AddInputPort(string portName, Port.Capacity capacity = Port.Capacity.Multi)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Input, capacity, typeof(bool));
            port.portName = portName;
            inputContainer.Add(port);
            return port;
        }

        /// <summary>出力ポートを追加する。<paramref name="portName"/> がそのままエッジのポート名になる。</summary>
        protected Port AddOutputPort(string portName, Port.Capacity capacity = Port.Capacity.Multi)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, capacity, typeof(bool));
            port.portName = portName;
            outputContainer.Add(port);
            return port;
        }
    }
}
