using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor.Experimental.GraphView;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>
    /// エッジの端を別のノードへ付け替えても、同じノードから出ているエッジの中での順（遷移を調べる順）を変えない。
    /// GraphView は付け替えを「古いエッジ 1 本の削除」→「新しいエッジの作成」の 2 回の graphViewChanged で知らせるので、それを同じ順で送る。
    /// </summary>
    public sealed class EdgeReconnectTests
    {
        private NodeGraphAsset _graph;
        private StateNode _story;
        private EventNode _loved;
        private EventNode _plain;
        private EventNode _other;
        private NodeGraphView _view;

        [SetUp]
        public void SetUp()
        {
            _graph = NodeGraphFactory.CreateNew();
            _story = Add(new StateNode { Title = "Story" });
            _loved = Add(new EventNode { EventName = "Finish", Title = "Loved" });
            _plain = Add(new EventNode { EventName = "Finish" });
            _other = Add(new EventNode { EventName = "Finish", Title = "Other" });
            _graph.AddEdge(new EdgeData(_story.Id, NodeView.OutputPortName, _loved.Id, NodeView.InputPortName));
            _graph.AddEdge(new EdgeData(_story.Id, NodeView.OutputPortName, _plain.Id, NodeView.InputPortName));
            _view = new NodeGraphView();
            _view.Populate(_graph);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void MovingTheInputEnd_KeepsTheEdgeInItsPlace()
        {
            Reconnect(_loved, _story, _other);

            Assert.That(TargetsOf(_story), Is.EqualTo(new[] { _other.Id, _plain.Id }), "still checked before the plain Finish");
        }

        [Test]
        public void MovingTheOutputEndToAnotherNode_GoesAfterThatNodesEdges()
        {
            var chapter = Add(new StateNode { Title = "Chapter" });
            _graph.AddEdge(new EdgeData(chapter.Id, NodeView.OutputPortName, _other.Id, NodeView.InputPortName));
            _view.Populate(_graph);

            Reconnect(_loved, chapter, _loved);

            Assert.That(TargetsOf(chapter), Is.EqualTo(new[] { _other.Id, _loved.Id }));
            Assert.That(TargetsOf(_story), Is.EqualTo(new[] { _plain.Id }));
        }

        // GraphView と同じく、古いエッジの削除と新しいエッジの作成を別々に知らせる
        private void Reconnect(EventNode oldTarget, NodeData from, NodeData to)
        {
            var oldEdge = _view.edges.ToList().Single(e => ((NodeView)e.input.node).NodeId == oldTarget.Id);
            _view.graphViewChanged(new GraphViewChange { elementsToRemove = new List<GraphElement> { oldEdge } });

            var newEdge = new Edge
            {
                output = _view.FindNodeView(from.Id).FindPort(NodeView.OutputPortName, Direction.Output),
                input = _view.FindNodeView(to.Id).FindPort(NodeView.InputPortName, Direction.Input),
            };
            _view.graphViewChanged(new GraphViewChange { edgesToCreate = new List<Edge> { newEdge } });
        }

        private string[] TargetsOf(NodeData node) => _graph.Edges.Where(e => e.FromNodeId == node.Id).Select(e => e.ToNodeId).ToArray();

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }
    }
}
