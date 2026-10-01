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
    }
}
