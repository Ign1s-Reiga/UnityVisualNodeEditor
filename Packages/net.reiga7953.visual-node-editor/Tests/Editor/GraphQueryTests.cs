using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class GraphQueryTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp() => _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void Next_IsInEdgeOrderWithoutDuplicatesOrMissingNodes()
        {
            var entry = Add(new EntryNode());
            var a = Add(new StateNode());
            var b = Add(new StateNode());
            Connect(entry, b);
            Connect(entry, a);
            Connect(entry, b);
            _graph.AddEdge(new EdgeData(entry.Id, "out", "missing", "in"));

            var query = new GraphQuery(_graph);

            Assert.That(query.Entry, Is.SameAs(entry));
            Assert.That(query.GetNext(entry.Id), Is.EqualTo(new NodeData[] { b, a }));
            Assert.That(query.GetOutgoingEdges(entry.Id), Has.Count.EqualTo(4));
            Assert.That(query.GetNext("unknown"), Is.Empty);
        }

        [Test]
        public void EventTransition_MatchesNameExactly()
        {
            var state = Add(new StateNode());
            var lower = Add(new EventNode { EventName = "start" });
            var exact = Add(new EventNode { EventName = "Start" });
            Connect(state, lower);
            Connect(state, exact);

            var query = new GraphQuery(_graph);

            Assert.That(query.FindEventTransition(state.Id, "Start"), Is.SameAs(exact));
            Assert.That(query.FindEventTransition(state.Id, ""), Is.Null);
            Assert.That(query.GetFirstNonEventNext(state.Id), Is.Null);
        }

        [Test]
        public void Scenes_AreDistinctAndSkipUnassigned()
        {
            Add(new SceneNode { Scene = new SceneReference("g1", "Assets/A.unity") });
            Add(new SceneNode { Scene = new SceneReference("g1", "Assets/A.unity") });
            Add(new SceneNode());
            Add(new SceneNode { Scene = new SceneReference("g2", "Assets/B.unity") });

            var scenes = new GraphQuery(_graph).GetScenes();

            Assert.That(scenes.Select(s => s.Guid), Is.EqualTo(new[] { "g1", "g2" }));
        }

        [Test]
        public void Validator_WarnsAboutEventWithoutName()
        {
            Add(new EntryNode());
            var unnamed = Add(new EventNode());

            var issue = GraphValidator.Validate(_graph).Single();

            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
            Assert.That(issue.NodeId, Is.EqualTo(unnamed.Id));
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }

        private void Connect(NodeData from, NodeData to) => _graph.AddEdge(new EdgeData(from.Id, "out", to.Id, "in"));
    }
}
