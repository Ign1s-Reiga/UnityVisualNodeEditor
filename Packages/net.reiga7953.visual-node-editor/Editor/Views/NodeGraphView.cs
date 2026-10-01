using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
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

            graphViewChanged = OnGraphViewChanged;
            RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += OnUndoRedo);
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedo);
        }

        /// <summary>現在表示しているアセット。</summary>
        public NodeGraphAsset Asset => _asset;

        /// <summary>アセットの内容でビューを再構築する。再構築中の変更はアセットへ書き戻さない。</summary>
        public void Populate(NodeGraphAsset asset)
        {
            _asset = asset;

            graphViewChanged -= OnGraphViewChanged;
            try
            {
                DeleteElements(graphElements.ToList());
                if (asset != null)
                {
                    BuildElements(asset);
                }
            }
            finally
            {
                graphViewChanged += OnGraphViewChanged;
            }
        }

        /// <summary>
        /// <paramref name="nodeType"/> のノードを <paramref name="position"/>（グラフ座標）に追加する。
        /// アセット未設定のときは何もせず null を返す。
        /// </summary>
        public NodeView CreateNode(Type nodeType, Vector2 position)
        {
            if (_asset == null)
            {
                return null;
            }

            var data = (NodeData)Activator.CreateInstance(nodeType);
            data.Position = position;

            Undo.RecordObject(_asset, "Add Node");
            _asset.AddNode(data);
            EditorUtility.SetDirty(_asset);

            var view = NodeViewFactory.Create(data);
            AddElement(view);
            return view;
        }

        /// <summary>向きが逆・別ノード・まだ接続されていないポートだけを接続候補にする。</summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList()
                .Where(p => p.direction != startPort.direction
                    && p.node != startPort.node
                    && !p.connections.Any(e => e.input == startPort || e.output == startPort))
                .ToList();
        }

        private void BuildElements(NodeGraphAsset asset)
        {
            var views = new Dictionary<string, NodeView>();
            foreach (var node in asset.Nodes)
            {
                // 型が削除・改名されると SerializeReference は null を返すことがある
                if (node == null)
                {
                    continue;
                }

                var view = NodeViewFactory.Create(node);
                AddElement(view);
                views[node.Id] = view;
            }

            foreach (var edgeData in asset.Edges)
            {
                var output = views.TryGetValue(edgeData.FromNodeId, out var from)
                    ? from.FindPort(edgeData.FromPort, Direction.Output)
                    : null;
                var input = views.TryGetValue(edgeData.ToNodeId, out var to)
                    ? to.FindPort(edgeData.ToPort, Direction.Input)
                    : null;
                if (output == null || input == null)
                {
                    Debug.LogWarning($"[VisualNodeEditor] Skipped edge {edgeData.FromNodeId}.{edgeData.FromPort} -> "
                        + $"{edgeData.ToNodeId}.{edgeData.ToPort} in '{asset.name}': port not found.", asset);
                    continue;
                }

                var edge = output.ConnectTo(input);
                edge.userData = edgeData;
                AddElement(edge);
            }
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_asset == null)
            {
                return change;
            }

            var changed = false;

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                Undo.RecordObject(_asset, "Delete Graph Elements");
                foreach (var element in change.elementsToRemove)
                {
                    switch (element)
                    {
                        case Edge edge:
                            RemoveEdgeData(edge);
                            break;
                        case NodeView nodeView:
                            _asset.RemoveNode(nodeView.Data);
                            break;
                    }
                }

                changed = true;
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                Undo.RecordObject(_asset, "Connect Ports");
                foreach (var edge in change.edgesToCreate)
                {
                    AddEdgeData(edge);
                }

                changed = true;
            }

            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                Undo.RecordObject(_asset, "Move Nodes");
                foreach (var nodeView in change.movedElements.OfType<NodeView>())
                {
                    nodeView.Data.Position = nodeView.GetPosition().position;
                }

                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(_asset);
            }

            return change;
        }

        private void AddEdgeData(Edge edge)
        {
            if (!(edge.output?.node is NodeView from) || !(edge.input?.node is NodeView to))
            {
                return;
            }

            var edgeData = _asset.FindEdge(from.Data.Id, edge.output.portName, to.Data.Id, edge.input.portName);
            if (edgeData == null)
            {
                edgeData = new EdgeData(from.Data.Id, edge.output.portName, to.Data.Id, edge.input.portName);
                _asset.AddEdge(edgeData);
            }

            edge.userData = edgeData;
        }

        private void RemoveEdgeData(Edge edge)
        {
            if (edge.userData is EdgeData edgeData)
            {
                _asset.RemoveEdge(edgeData);
            }
        }

        private void OnUndoRedo()
        {
            if (_asset != null)
            {
                Populate(_asset);
            }
        }
    }
}
