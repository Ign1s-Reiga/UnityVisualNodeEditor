using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeGraphAssetTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp() => _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void RemoveNode_RemovesConnectedEdges()
        {
            var a = new EntryNode();
            var b = new SceneNode();
            _graph.AddNode(a);
            _graph.AddNode(b);
            _graph.AddEdge(new EdgeData(a.Id, "out", b.Id, "in"));

            _graph.RemoveNode(b);

            Assert.That(_graph.Nodes, Has.Count.EqualTo(1));
            Assert.That(_graph.Edges, Is.Empty);
        }

        [Test]
        public void NodeData_HasUniqueIds()
        {
            var a = new NoteNode();
            var b = new NoteNode();
            Assert.That(a.Id, Is.Not.EqualTo(b.Id));
        }

        [Test]
        public void FindNode_ReturnsNodeWithMatchingId()
        {
            var a = new EntryNode();
            _graph.AddNode(a);

            Assert.That(_graph.FindNode(a.Id), Is.SameAs(a));
            Assert.That(_graph.FindNode("missing"), Is.Null);
        }

        [Test]
        public void FindEdge_MatchesAllFourKeys()
        {
            var edge = new EdgeData("a", "out", "b", "in");
            _graph.AddEdge(edge);

            Assert.That(_graph.FindEdge("a", "out", "b", "in"), Is.SameAs(edge));
            Assert.That(_graph.FindEdge("b", "out", "a", "in"), Is.Null);
            Assert.That(_graph.FindEdge("a", "other", "b", "in"), Is.Null);
            Assert.That(_graph.FindEdge("a", "out", "b", "other"), Is.Null);
        }

        [Test]
        public void RemoveEdge_RemovesOnlyThatEdge()
        {
            var keep = new EdgeData("a", "out", "b", "in");
            var remove = new EdgeData("b", "out", "c", "in");
            _graph.AddEdge(keep);
            _graph.AddEdge(remove);

            Assert.That(_graph.RemoveEdge(remove), Is.True);
            Assert.That(_graph.Edges, Is.EqualTo(new[] { keep }));
        }

        [Test]
        public void RemoveNode_RemovesNodeFromGroups()
        {
            var a = new StateNode();
            var b = new StateNode();
            _graph.AddNode(a);
            _graph.AddNode(b);
            var group = new GroupData();
            group.AddNode(a.Id);
            group.AddNode(b.Id);
            _graph.AddGroup(group);

            _graph.RemoveNode(a);

            Assert.That(group.NodeIds, Is.EqualTo(new[] { b.Id }));
        }

        [Test]
        public void RemoveNode_IgnoresNull()
        {
            Assert.That(_graph.RemoveNode(null), Is.False);
        }

        [Test]
        public void GroupData_DoesNotAddSameNodeTwice()
        {
            var group = new GroupData();

            Assert.That(group.AddNode("a"), Is.True);
            Assert.That(group.AddNode("a"), Is.False);
            Assert.That(group.AddNode(null), Is.False);
            Assert.That(group.NodeIds, Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void FindGroup_ReturnsGroupWithMatchingId()
        {
            var group = new GroupData();
            _graph.AddGroup(group);

            Assert.That(_graph.FindGroup(group.Id), Is.SameAs(group));
            Assert.That(_graph.RemoveGroup(group), Is.True);
            Assert.That(_graph.FindGroup(group.Id), Is.Null);
        }
    }
}
