using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class NodeGraphAssetTests
    {
        [Test]
        public void RemoveNode_RemovesConnectedEdges()
        {
            var graph = ScriptableObject.CreateInstance<NodeGraphAsset>();
            var a = new EntryNode();
            var b = new SceneNode();
            graph.AddNode(a);
            graph.AddNode(b);
            graph.AddEdge(new EdgeData(a.Id, "out", b.Id, "in"));

            graph.RemoveNode(b);

            Assert.That(graph.Nodes, Has.Count.EqualTo(1));
            Assert.That(graph.Edges, Is.Empty);
            Object.DestroyImmediate(graph);
        }

        [Test]
        public void NodeData_HasUniqueIds()
        {
            var a = new NoteNode();
            var b = new NoteNode();
            Assert.That(a.Id, Is.Not.EqualTo(b.Id));
        }
    }
}
