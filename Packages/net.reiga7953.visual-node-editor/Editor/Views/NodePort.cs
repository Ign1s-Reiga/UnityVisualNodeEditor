using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Reiga.VisualNodeEditor.Editor.Views
{
    /// <summary>
    /// ノードのポート。GraphView の既定のポートは、エッジを空き地で離しても何もしないため、
    /// 空き地で離したら <see cref="NodeGraphView"/> にノードの検索を頼むリスナーを付ける。ポートの上で離したときの動きは既定と同じ。
    /// </summary>
    public sealed class NodePort : Port
    {
        private NodePort(Direction direction, Capacity capacity)
            : base(Orientation.Horizontal, direction, capacity, typeof(bool))
        {
        }

        /// <summary>ポートを作る（エッジのドラッグ操作も付ける）。</summary>
        public static NodePort Create(Direction direction, Capacity capacity)
        {
            var port = new NodePort(direction, capacity);
            port.m_EdgeConnector = new EdgeConnector<Edge>(new Listener());
            port.AddManipulator(port.m_EdgeConnector);
            return port;
        }

        /// <summary>
        /// 空き地で離したエッジの、繋がったままの側のポート（新しいノードを繋ぐ側）。
        /// ポートから引いた新しいエッジは片側しか無い。既存のエッジを端から外したときは、離すまで外した側も残っているので、
        /// ドラッグを始めた側（その端の <see cref="EdgeDragHelper.draggedPort"/> が自分自身の側）を選ぶ。
        /// </summary>
        internal static Port GetDraggedPort(Edge edge)
        {
            if (edge == null)
            {
                return null;
            }

            if (edge.input != null && edge.output != null)
            {
                if (IsBeingDragged(edge.input))
                {
                    return edge.input;
                }

                if (IsBeingDragged(edge.output))
                {
                    return edge.output;
                }
            }

            return edge.output ?? edge.input;
        }

        private static bool IsBeingDragged(Port port) => port.edgeConnector?.edgeDragHelper?.draggedPort == port;

        // GraphView の DefaultEdgeConnectorListener と同じく繋ぎ、空き地で離したときだけノードの検索を開く
        private sealed class Listener : IEdgeConnectorListener
        {
            private readonly List<Edge> _edgesToCreate = new();
            private readonly List<GraphElement> _edgesToDelete = new();
            private readonly GraphViewChange _change;

            public Listener()
            {
                _change.edgesToCreate = _edgesToCreate;
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                var port = GetDraggedPort(edge);
                port?.GetFirstAncestorOfType<NodeGraphView>()?.RequestConnectedNode(port, position);
            }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                _edgesToCreate.Clear();
                _edgesToCreate.Add(edge);
                _edgesToDelete.Clear();
                if (edge.input.capacity == Capacity.Single)
                {
                    foreach (var existing in edge.input.connections)
                    {
                        if (existing != edge)
                        {
                            _edgesToDelete.Add(existing);
                        }
                    }
                }

                if (edge.output.capacity == Capacity.Single)
                {
                    foreach (var existing in edge.output.connections)
                    {
                        if (existing != edge)
                        {
                            _edgesToDelete.Add(existing);
                        }
                    }
                }

                if (_edgesToDelete.Count > 0)
                {
                    graphView.DeleteElements(_edgesToDelete);
                }

                var edgesToCreate = graphView.graphViewChanged != null
                    ? graphView.graphViewChanged(_change).edgesToCreate
                    : _edgesToCreate;
                foreach (var created in edgesToCreate)
                {
                    graphView.AddElement(created);
                    edge.input.Connect(created);
                    edge.output.Connect(created);
                }
            }
        }
    }
}
