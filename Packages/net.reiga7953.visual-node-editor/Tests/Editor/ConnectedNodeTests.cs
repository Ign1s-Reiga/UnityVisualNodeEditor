using System.Linq;
using NUnit.Framework;
using Reiga.VisualNodeEditor.Editor;
using Reiga.VisualNodeEditor.Editor.Search;
using Reiga.VisualNodeEditor.Editor.Views;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reiga.VisualNodeEditor.Tests
{
    /// <summary>ポートからエッジを空き地へ落として、ノードを作って繋ぐ。</summary>
    public sealed class ConnectedNodeTests
    {
        private NodeGraphAsset _graph;
        private StateNode _title;
        private NodeGraphView _view;

        [SetUp]
        public void SetUp()
        {
            _graph = NodeGraphFactory.CreateNew();
            _title = new StateNode { Title = "Title" };
            _graph.AddNode(_title);
            _view = new NodeGraphView();
            _view.Populate(_graph);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        // ---- 検索に出すノード ----

        [Test]
        public void FromAnOutput_OnlyNodesWithAnInput()
        {
            Assert.That(ConnectionCandidates.CanConnect(typeof(StateNode), Direction.Output), Is.True);
            Assert.That(ConnectionCandidates.CanConnect(typeof(EventNode), Direction.Output), Is.True);
            Assert.That(ConnectionCandidates.CanConnect(typeof(ContainerNode), Direction.Output), Is.True);
            Assert.That(ConnectionCandidates.CanConnect(typeof(ContainerExitNode), Direction.Output), Is.True);
            Assert.That(ConnectionCandidates.CanConnect(typeof(EntryNode), Direction.Output), Is.False);
            Assert.That(ConnectionCandidates.CanConnect(typeof(ContainerEntryNode), Direction.Output), Is.False);
            Assert.That(ConnectionCandidates.CanConnect(typeof(NoteNode), Direction.Output), Is.False);
        }

        [Test]
        public void FromAnInput_OnlyNodesWithAnOutput()
        {
            Assert.That(ConnectionCandidates.CanConnect(typeof(EntryNode), Direction.Input), Is.True);
            Assert.That(ConnectionCandidates.CanConnect(typeof(SceneNode), Direction.Input), Is.True);
            Assert.That(ConnectionCandidates.CanConnect(typeof(ContainerExitNode), Direction.Input), Is.False);
            Assert.That(ConnectionCandidates.CanConnect(typeof(NoteNode), Direction.Input), Is.False);
        }

        [Test]
        public void EventComesFirst_FromTheOutputOfAStateOrScene()
        {
            Assert.That(ConnectionCandidates.GetFeaturedType(new PendingConnection("n", "out", Direction.Output, true)),
                Is.EqualTo(typeof(EventNode)));
            Assert.That(ConnectionCandidates.GetFeaturedType(new PendingConnection("n", "in", Direction.Input, true)), Is.Null);
            Assert.That(ConnectionCandidates.GetFeaturedType(new PendingConnection("n", "out", Direction.Output, false)), Is.Null,
                "from Entry and similar pass-through nodes, nothing is featured");
            Assert.That(ConnectionCandidates.GetFeaturedType(null), Is.Null);
            Assert.That(ConnectionCandidates.IsWaitNode(new StateNode()), Is.True);
            Assert.That(ConnectionCandidates.IsWaitNode(new SceneNode()), Is.True);
            Assert.That(ConnectionCandidates.IsWaitNode(new EntryNode()), Is.False);
        }

        [Test]
        public void FeaturedType_IsListedFirstAndStillInItsGroup()
        {
            var items = NodeMenuCatalog.GetItems(false);

            var tree = NodeSearchWindow.BuildSearchTree(items, null, typeof(EventNode));

            Assert.That(tree[1].userData, Is.EqualTo(typeof(EventNode)));
            Assert.That(tree[1].level, Is.EqualTo(1));
            Assert.That(tree.Count(e => (e.userData as System.Type) == typeof(EventNode)), Is.EqualTo(2));
        }

        // ---- 作って繋ぐ ----

        [Test]
        public void FromAnOutput_CreatesTheNodeAndConnectsItsInput()
        {
            var pending = new PendingConnection(_title.Id, NodeView.OutputPortName, Direction.Output, true);

            var created = _view.CreateConnectedNode(typeof(EventNode), new Vector2(300f, 0f), pending);

            var edge = _graph.Edges.Single(e => e.FromNodeId == _title.Id);
            Assert.That(edge.ToNodeId, Is.EqualTo(created.NodeId));
            Assert.That(edge.ToPort, Is.EqualTo(NodeView.InputPortName));
            Assert.That(created.Data.Position, Is.EqualTo(new Vector2(300f, 0f)));
            Assert.That(_view.selection.OfType<NodeView>().Single().NodeId, Is.EqualTo(created.NodeId));
        }

        [Test]
        public void FromAnInput_ConnectsTheNewNodesOutput()
        {
            var pending = new PendingConnection(_title.Id, NodeView.InputPortName, Direction.Input, false);

            var created = _view.CreateConnectedNode(typeof(StateNode), Vector2.zero, pending);

            var edge = _graph.Edges.Single(e => e.ToNodeId == _title.Id);
            Assert.That(edge.FromNodeId, Is.EqualTo(created.NodeId));
            Assert.That(edge.FromPort, Is.EqualTo(NodeView.OutputPortName));
        }

        [Test]
        public void FromAContainerExit_UsesThatExitsPort()
        {
            var container = (ContainerNode)_view.CreateNode(typeof(ContainerNode), Vector2.zero).Data;
            var exitId = container.Exits[0].Id;
            var pending = new PendingConnection(container.Id, exitId, Direction.Output, false);

            var created = _view.CreateConnectedNode(typeof(StateNode), Vector2.zero, pending);

            Assert.That(_graph.Edges.Any(e => e.FromNodeId == container.Id && e.FromPort == exitId && e.ToNodeId == created.NodeId),
                Is.True);
        }

        [Test]
        public void CreateAndConnect_IsOneUndoStep()
        {
            var pending = new PendingConnection(_title.Id, NodeView.OutputPortName, Direction.Output, true);
            Undo.IncrementCurrentGroup();

            _view.CreateConnectedNode(typeof(EventNode), Vector2.zero, pending);
            Undo.PerformUndo();

            Assert.That(_graph.Nodes.OfType<EventNode>(), Is.Empty);
            Assert.That(_graph.Edges.Any(e => e.FromNodeId == _title.Id), Is.False);
        }

        // ---- ポートと要求 ----

        [Test]
        public void Ports_UseTheConnectingPort_AndDropsAskTheWindow()
        {
            var titleView = _view.FindNodeView(_title.Id);
            var port = titleView.FirstPort(Direction.Output);
            Assert.That(port, Is.TypeOf<NodePort>());

            PendingConnection requested = null;
            _view.ConnectedNodeRequested += (pending, _) => requested = pending;
            _view.RequestConnectedNode(port, Vector2.zero);

            Assert.That(requested.NodeId, Is.EqualTo(_title.Id));
            Assert.That(requested.PortId, Is.EqualTo(NodeView.OutputPortName));
            Assert.That(requested.Direction, Is.EqualTo(Direction.Output));
            Assert.That(requested.FromWaitNode, Is.True);
        }
    }
}
