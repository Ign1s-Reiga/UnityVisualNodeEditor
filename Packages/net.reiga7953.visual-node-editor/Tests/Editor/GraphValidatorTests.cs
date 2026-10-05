using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Reiga.VisualNodeEditor.Tests
{
    public sealed class GraphValidatorTests
    {
        private NodeGraphAsset _graph;

        [SetUp]
        public void SetUp() => _graph = ScriptableObject.CreateInstance<NodeGraphAsset>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_graph);

        [Test]
        public void ValidGraph_HasNoIssues()
        {
            var entry = Add(new EntryNode());
            var scene = Add(new SceneNode { Scene = new SceneReference("guid", "Assets/Title.unity") });
            var state = Add(new StateNode());
            Add(new NoteNode());
            _graph.AddEdge(new EdgeData(entry.Id, "out", scene.Id, "in"));
            _graph.AddEdge(new EdgeData(scene.Id, "out", state.Id, "in"));
            _graph.AddEdge(new EdgeData(state.Id, "out", scene.Id, "in"));

            Assert.That(GraphValidator.Validate(_graph), Is.Empty);
        }

        [Test]
        public void MissingEntry_IsError()
        {
            Add(new StateNode());

            var issue = GraphValidator.Validate(_graph).Single();

            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Error));
            Assert.That(issue.NodeId, Is.Null);
        }

        [Test]
        public void ExtraEntries_AreErrorsOnEachExtraNode()
        {
            Add(new EntryNode());
            var second = Add(new EntryNode());
            var third = Add(new EntryNode());

            var issues = GraphValidator.Validate(_graph);

            Assert.That(issues.Select(i => i.NodeId), Is.EquivalentTo(new[] { second.Id, third.Id }));
            Assert.That(issues.All(i => i.Severity == GraphIssueSeverity.Error), Is.True);
        }

        [Test]
        public void EdgeToMissingNode_IsError()
        {
            var entry = Add(new EntryNode());
            _graph.AddEdge(new EdgeData(entry.Id, "out", "missing", "in"));

            var issue = GraphValidator.Validate(_graph).Single();

            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Error));
            Assert.That(issue.NodeId, Is.EqualTo(entry.Id));
        }

        [Test]
        public void ConnectedNote_IsWarningOnNote()
        {
            var entry = Add(new EntryNode());
            var note = Add(new NoteNode());
            _graph.AddEdge(new EdgeData(entry.Id, "out", note.Id, "in"));

            var issue = GraphValidator.Validate(_graph).Single();

            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
            Assert.That(issue.NodeId, Is.EqualTo(note.Id));
        }

        [Test]
        public void DuplicateEdge_IsWarning()
        {
            var entry = Add(new EntryNode());
            var state = Add(new StateNode());
            _graph.AddEdge(new EdgeData(entry.Id, "out", state.Id, "in"));
            _graph.AddEdge(new EdgeData(entry.Id, "out", state.Id, "in"));

            var issue = GraphValidator.Validate(_graph).Single();

            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
        }

        [Test]
        public void SceneWithoutScene_IsWarning()
        {
            Add(new EntryNode());
            var scene = Add(new SceneNode());

            var issue = GraphValidator.Validate(_graph).Single();

            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
            Assert.That(issue.NodeId, Is.EqualTo(scene.Id));
        }

        [Test]
        public void NullNode_IsWarning()
        {
            var nodes = new NodeData[] { new EntryNode(), null };

            var issue = GraphValidator.Validate(nodes, new EdgeData[0]).Single();

            Assert.That(issue.Severity, Is.EqualTo(GraphIssueSeverity.Warning));
        }

        [Test]
        public void DuplicateNodeId_IsError()
        {
            var entry = new EntryNode();
            var nodes = new NodeData[] { entry, entry };

            var issues = GraphValidator.Validate(nodes, new EdgeData[0]);

            // 同じインスタンスを 2 回入れているので Entry 重複も同時に出る
            Assert.That(issues.Count(i => i.Message.Contains("same ID")), Is.EqualTo(1));
        }

        private T Add<T>(T node) where T : NodeData
        {
            _graph.AddNode(node);
            return node;
        }
    }
}
